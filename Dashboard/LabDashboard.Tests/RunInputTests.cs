using System.Text;
using LabDashboard.Execution;

namespace LabDashboard.Tests;

public class InputSignalParserTests
{
    private static (string Text, List<int> Signals) Parse(params string[] chunks)
    {
        InputSignalParser parser = new();
        List<int> signals = [];
        StringBuilder text = new();
        foreach (string chunk in chunks)
        {
            text.Append(parser.Feed(chunk, signals.Add));
        }

        text.Append(parser.Complete());
        return (text.ToString(), signals);
    }

    [Fact]
    public void Removes_the_signals_and_keeps_the_surrounding_stderr_text()
    {
        (string text, List<int> signals) = Parse("warn: a\n\u001b]lab;input;1\u0007err\u001b]lab;input;12\u0007 end");

        Assert.Equal("warn: a\nerr end", text);
        Assert.Equal([1, 12], signals);
    }

    [Fact]
    public void Recognizes_a_signal_split_across_reads()
    {
        (string text, List<int> signals) = Parse("x\u001b]la", "b;inp", "ut;3", "\u0007y");

        Assert.Equal("xy", text);
        Assert.Equal([3], signals);
    }

    [Theory]
    [InlineData("\u001b[31mred\u001b[0m")]
    [InlineData("\u001b]lab;input;\u0007")]
    [InlineData("\u001b]lab;input;4x\u0007")]
    [InlineData("\u001b]lab;other;1\u0007")]
    public void Gives_back_anything_that_is_not_a_complete_signal(string raw)
    {
        (string text, List<int> signals) = Parse(raw);

        Assert.Equal(raw, text);
        Assert.Empty(signals);
    }

    [Fact]
    public void An_escape_inside_a_partial_signal_starts_a_new_one()
    {
        (string text, List<int> signals) = Parse("\u001b]la\u001b]lab;input;2\u0007");

        Assert.Equal("\u001b]la", text);
        Assert.Equal([2], signals);
    }

    [Fact]
    public void An_incomplete_signal_at_the_end_of_the_stream_is_text()
    {
        (string text, List<int> signals) = Parse("bye\u001b]lab;inp");

        Assert.Equal("bye\u001b]lab;inp", text);
        Assert.Empty(signals);
    }
}

public class RunInputTests
{
    private sealed class ManualTime : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _ticks;

        public void Advance(TimeSpan by) => _ticks += by.Ticks;
    }

    private readonly List<RunEvent> _events = [];
    private readonly List<string> _echoes = [];
    private readonly StringWriter _stdin = new();
    private readonly ManualTime _time = new();

    private RunInput Create(bool attach = true)
    {
        RunInput input = new(_events.Add, type => new RunEvent(type, 0), _echoes.Add, _time);
        if (attach)
        {
            input.Attach(_stdin);
        }

        return input;
    }

    private IEnumerable<string> Events => _events.Select(e => e.InputId is { } id ? $"{e.Type}#{id}" : e.Type);

    [Theory]
    [InlineData("", null)]
    [InlineData("héllo €", null)]
    [InlineData(null, "required")]
    [InlineData("a\nb", "newline")]
    [InlineData("a\r", "newline")]
    public void Validates_one_line(string? text, string? error) => Assert.Equal(error, RunInput.Validate(text));

    [Fact]
    public void Rejects_a_line_over_the_maximum_length()
    {
        Assert.Null(RunInput.Validate(new string('a', RunInput.MaxLength)));
        Assert.Equal("tooLong", RunInput.Validate(new string('a', RunInput.MaxLength + 1)));
    }

    [Fact]
    public void Answers_the_reads_in_order_and_writes_each_line_to_the_program()
    {
        RunInput input = Create();

        input.OnReadSignal(1);
        Assert.True(input.IsWaiting);
        Assert.Equal(InputResult.Accepted, input.Send("first"));
        input.OnReadSignal(2);
        Assert.Equal(InputResult.Accepted, input.Send(""));

        Assert.Equal("first\n\n", _stdin.ToString());
        Assert.Equal(["first", ""], _echoes);
        Assert.Equal(["input-request#1", "input-sent#1", "input-request#2", "input-sent#2"], Events);
        Assert.False(input.IsWaiting);
    }

    [Fact]
    public void A_line_typed_ahead_answers_the_next_read_without_a_prompt()
    {
        RunInput input = Create();

        Assert.Equal(InputResult.Accepted, input.Send("ahead"));
        Assert.False(input.IsWaiting);
        input.OnReadSignal(1);

        Assert.Equal(["input-sent#1"], Events);
        Assert.False(input.IsWaiting);
    }

    [Fact]
    public void Limits_the_lines_typed_ahead()
    {
        RunInput input = Create();
        for (int i = 0; i < RunInput.MaxTypedAhead; i++)
        {
            Assert.Equal(InputResult.Accepted, input.Send($"line {i}"));
        }

        Assert.Equal(InputResult.TooManyPending, input.Send("one too many"));
        input.OnReadSignal(1);
        Assert.Equal(InputResult.Accepted, input.Send("room again"));
    }

    [Fact]
    public void Accepts_nothing_before_the_program_starts_or_after_it_exits()
    {
        RunInput input = Create(attach: false);
        Assert.Equal(InputResult.NotAccepting, input.Send("x"));
        Assert.Equal(InputResult.NotAccepting, input.Close());

        input.Attach(_stdin);
        input.OnReadSignal(1);
        input.Detach();

        Assert.False(input.IsWaiting);
        Assert.Equal(InputResult.NotAccepting, input.Send("x"));
        input.OnReadSignal(2);
        Assert.DoesNotContain("input-request#2", Events);
    }

    [Fact]
    public void Closing_ends_the_input_for_good()
    {
        RunInput input = Create();
        input.OnReadSignal(1);

        Assert.Equal(InputResult.Accepted, input.Close());
        Assert.False(input.IsWaiting);
        Assert.Equal(InputResult.Closed, input.Send("late"));
        Assert.Equal(InputResult.Closed, input.Close());

        // The program's next read returns null at once: no prompt.
        input.OnReadSignal(2);
        Assert.Equal(["input-request#1", "input-closed"], Events);
    }

    [Fact]
    public void Measures_only_the_time_spent_waiting_for_the_learner()
    {
        RunInput input = Create();
        _time.Advance(TimeSpan.FromSeconds(5));   // program working
        input.OnReadSignal(1);
        _time.Advance(TimeSpan.FromSeconds(40));  // learner reading
        Assert.Equal(TimeSpan.FromSeconds(40), input.CurrentWait);
        input.Send("y");
        _time.Advance(TimeSpan.FromSeconds(3));   // program working
        input.OnReadSignal(2);
        _time.Advance(TimeSpan.FromSeconds(10));  // learner reading

        Assert.Equal(TimeSpan.FromSeconds(50), input.WaitedTime);
        Assert.Equal(TimeSpan.FromSeconds(10), input.CurrentWait);
        input.Close();
        Assert.Equal(TimeSpan.Zero, input.CurrentWait);
    }
}

public class TimeoutEvaluationTests
{
    private static readonly TimeSpan Lab = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Idle = TimeSpan.FromSeconds(600);

    [Fact]
    public void Waiting_for_the_learner_does_not_count_against_the_lab_timeout() =>
        Assert.Null(LabRunner.EvaluateTimeouts(TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(70), TimeSpan.FromSeconds(70), Lab, Idle));

    [Fact]
    public void The_working_time_still_times_out() =>
        Assert.Equal(LabRunner.TimeoutKind.Lab,
            LabRunner.EvaluateTimeouts(TimeSpan.FromSeconds(100), TimeSpan.FromSeconds(70), TimeSpan.Zero, Lab, Idle));

    [Fact]
    public void An_unanswered_prompt_times_out_on_the_idle_timeout() =>
        Assert.Equal(LabRunner.TimeoutKind.Input,
            LabRunner.EvaluateTimeouts(TimeSpan.FromSeconds(620), TimeSpan.FromSeconds(600), TimeSpan.FromSeconds(600), Lab, Idle));

    [Fact]
    public void Without_input_the_lab_timeout_is_unchanged()
    {
        Assert.Null(LabRunner.EvaluateTimeouts(TimeSpan.FromSeconds(29), TimeSpan.Zero, TimeSpan.Zero, Lab, Idle));
        Assert.Equal(LabRunner.TimeoutKind.Lab, LabRunner.EvaluateTimeouts(Lab, TimeSpan.Zero, TimeSpan.Zero, Lab, Idle));
    }
}
