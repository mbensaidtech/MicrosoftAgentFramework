namespace LabDashboard.Execution;

public enum InputResult
{
    Accepted,

    /// <summary>The program is not running (build phase, or it has exited).</summary>
    NotAccepting,

    /// <summary>Standard input was closed by the learner.</summary>
    Closed,

    /// <summary>Too many lines typed ahead of the program's reads.</summary>
    TooManyPending
}

/// <summary>
/// Standard input of an interactive run. The input hook announces each <c>Console.ReadLine</c> of the program
/// (<see cref="OnReadSignal"/>); the learner's lines are written to the program (<see cref="Send"/>) and answer the reads in order.
/// A line sent before the program asks is kept by the pipe and consumed by the next read, as in a terminal.
/// Also measures the time spent waiting for the learner, which does not count against the lab timeout.
/// </summary>
public sealed class RunInput
{
    public const int MaxLength = 4096;

    /// <summary>Lines typed ahead are few and short: the pipe buffer (at least 4 KB × 8) never blocks the writer.</summary>
    public const int MaxTypedAhead = 8;

    private readonly object _gate = new();
    private readonly Action<RunEvent> _publish;
    private readonly Func<string, RunEvent> _createEvent;
    private readonly Action<string> _echo;
    private readonly TimeProvider _time;
    private TextWriter? _writer;
    private bool _closed;
    private int _requested;
    private int _sent;
    private TimeSpan _waited;
    private long? _waitingSince;

    public RunInput(Action<RunEvent> publish, Func<string, RunEvent> createEvent, Action<string> echo, TimeProvider? time = null)
    {
        _publish = publish;
        _createEvent = createEvent;
        _echo = echo;
        _time = time ?? TimeProvider.System;
    }

    public RunInput(LabRun run, Action<string> echo)
        : this(run.Publish, run.CreateEvent, echo)
    {
    }

    /// <summary>Validation of one line of input: null when valid, otherwise an error code for the API.</summary>
    public static string? Validate(string? text) => text switch
    {
        null => "required",
        { Length: > MaxLength } => "tooLong",
        _ when text.AsSpan().IndexOfAny('\r', '\n') >= 0 => "newline",
        _ => null,
    };

    /// <summary>A read is pending: the program waits for the learner.</summary>
    public bool IsWaiting { get { lock (_gate) { return IsWaitingCore; } } }

    /// <summary>Total time spent waiting for the learner so far.</summary>
    public TimeSpan WaitedTime
    {
        get
        {
            lock (_gate)
            {
                return _waited + CurrentWaitCore;
            }
        }
    }

    /// <summary>How long the current pending read has been waiting (zero when none is pending).</summary>
    public TimeSpan CurrentWait { get { lock (_gate) { return CurrentWaitCore; } } }

    private bool IsWaitingCore => _writer is not null && !_closed && _requested > _sent;

    private TimeSpan CurrentWaitCore => _waitingSince is { } since ? _time.GetElapsedTime(since) : TimeSpan.Zero;

    /// <summary>The program started: its standard input is <paramref name="writer"/>.</summary>
    public void Attach(TextWriter writer)
    {
        lock (_gate)
        {
            _writer = writer;
        }
    }

    /// <summary>The program exited: nothing is accepted any more.</summary>
    public void Detach()
    {
        lock (_gate)
        {
            _writer = null;
            UpdateWaiting();
        }
    }

    /// <summary>The program entered its read number <paramref name="read"/> (1, 2, 3…).</summary>
    public void OnReadSignal(int read)
    {
        lock (_gate)
        {
            _requested = Math.Max(_requested, read);
            if (_closed || _writer is null)
            {
                // End of input (this read returns null at once), or the program already exited.
                return;
            }

            // A line typed ahead answers this read at once: no prompt to show.
            _publish(_createEvent(read <= _sent ? "input-sent" : "input-request") with { InputId = read });
            UpdateWaiting();
        }
    }

    public InputResult Send(string text)
    {
        lock (_gate)
        {
            if (_closed)
            {
                return InputResult.Closed;
            }

            if (_writer is null)
            {
                return InputResult.NotAccepting;
            }

            if (_sent - _requested >= MaxTypedAhead)
            {
                return InputResult.TooManyPending;
            }

            try
            {
                // "\n" ends a line for Console.ReadLine on every platform.
                _writer.Write(text + "\n");
                _writer.Flush();
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                // The program exited between the check and the write.
                return InputResult.NotAccepting;
            }

            _sent++;
            _echo(text);
            if (_sent <= _requested)
            {
                _publish(_createEvent("input-sent") with { InputId = _sent });
            }

            UpdateWaiting();
            return InputResult.Accepted;
        }
    }

    /// <summary>Closes standard input: the next <c>ReadLine</c> of the program returns null (end of input).</summary>
    public InputResult Close()
    {
        lock (_gate)
        {
            if (_closed)
            {
                return InputResult.Closed;
            }

            if (_writer is null)
            {
                return InputResult.NotAccepting;
            }

            try
            {
                _writer.Close();
            }
            catch (IOException)
            {
                // The program already exited: its input is closed anyway.
            }

            _closed = true;
            _publish(_createEvent("input-closed"));
            UpdateWaiting();
            return InputResult.Accepted;
        }
    }

    private void UpdateWaiting()
    {
        bool waiting = IsWaitingCore;
        if (waiting && _waitingSince is null)
        {
            _waitingSince = _time.GetTimestamp();
        }
        else if (!waiting && _waitingSince is { } since)
        {
            _waited += _time.GetElapsedTime(since);
            _waitingSince = null;
        }
    }
}
