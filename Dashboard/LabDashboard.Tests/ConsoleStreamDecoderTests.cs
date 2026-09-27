using LabDashboard.Execution;

namespace LabDashboard.Tests;

public class ConsoleStreamDecoderTests
{
    private static List<ConsoleFragment> DecodeAll(params string[] chunks)
    {
        ConsoleStreamDecoder decoder = new();
        List<ConsoleFragment> fragments = [];
        foreach (string chunk in chunks)
        {
            fragments.AddRange(decoder.Decode(chunk));
        }

        fragments.AddRange(decoder.Complete());
        return fragments;
    }

    private static List<string> Lines(IEnumerable<ConsoleFragment> fragments)
    {
        List<string> lines = [];
        string current = "";
        foreach (ConsoleFragment fragment in fragments.Where(f => f.Kind == ConsoleFragmentKind.Text))
        {
            current += fragment.Text;
            if (fragment.EndOfLine)
            {
                lines.Add(current);
                current = "";
            }
        }

        return lines;
    }

    [Fact]
    public void Splits_lines_on_lf_and_crlf()
    {
        Assert.Equal(["one", "two", "three"], Lines(DecodeAll("one\ntwo\r\nthree\n")));
    }

    [Fact]
    public void Reports_spinner_frames_as_transient_and_keeps_the_final_line()
    {
        // Exactly what CommonUtilities.ConsoleSpinner writes, followed by the scenario header.
        List<ConsoleFragment> fragments = DecodeAll(
            "\r⠋ Running agent... [00:00]      ",
            "\r⠙ Running agent... [00:01]      ",
            "\r                                \r=== Scenario 1 ===\nParis\n");

        Assert.Equal(["=== Scenario 1 ===", "Paris"], Lines(fragments));
        Assert.Equal(
            ["⠋ Running agent... [00:00]", "⠙ Running agent... [00:01]"],
            fragments.Where(f => f.Kind == ConsoleFragmentKind.Transient).Select(f => f.Text));
    }

    [Fact]
    public void Flushes_streamed_text_before_the_end_of_the_line()
    {
        ConsoleStreamDecoder decoder = new();

        IReadOnlyList<ConsoleFragment> first = decoder.Decode("Once upon");
        IReadOnlyList<ConsoleFragment> second = decoder.Decode(" a time\n");

        Assert.Equal(new ConsoleFragment(ConsoleFragmentKind.Text, "Once upon"), Assert.Single(first));
        Assert.Equal(new ConsoleFragment(ConsoleFragmentKind.Text, " a time", EndOfLine: true), Assert.Single(second));
    }

    [Fact]
    public void Handles_a_crlf_split_across_two_reads()
    {
        Assert.Equal(["line"], Lines(DecodeAll("line\r", "\n")));
    }

    [Fact]
    public void Does_not_flush_a_line_written_after_a_bare_carriage_return_until_it_ends()
    {
        ConsoleStreamDecoder decoder = new();

        IReadOnlyList<ConsoleFragment> fragments = decoder.Decode("\r⠋ Running agent...");

        Assert.Empty(fragments);
    }

    [Fact]
    public void Emits_the_last_unterminated_line_on_completion()
    {
        Assert.Equal(["no newline"], Lines(DecodeAll("no newline")));
    }
}
