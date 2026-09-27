using LabDashboard.Catalog;

namespace LabDashboard.Execution;

public enum RunStatus
{
    Running,
    Passed,
    Failed,
    Cancelled,
    TimedOut
}

public sealed record LogLine(string Stream, string Text)
{
    /// <summary>A prompt: the line continues with the learner's input (next line, stream "stdin"), as in a terminal.</summary>
    public bool? Prompt { get; init; }
}

public sealed record ExpectationResult(string Id, string Description, bool Passed);

/// <summary>One token usage block printed by the exercise (e.g. "Token Usage:" followed by the counts).</summary>
public sealed record TokenUsageReport(string Label, long? Input, long? Output, long? Reasoning, long? Total);

/// <summary>
/// Token usage as reported by the exercise output. It covers only the agent runs whose usage the exercise prints:
/// it is not the total consumption of the process.
/// </summary>
public sealed record TokenUsageSummary(IReadOnlyList<TokenUsageReport> Reports, long? Input, long? Output, long? Reasoning, long? Total)
{
    public string Source => "Reported by the exercise output";
}

public sealed record RunRecord
{
    public required string RunId { get; init; }
    public required string LabId { get; init; }
    public required RunTarget Target { get; init; }
    public required RunStatus Status { get; init; }

    /// <summary>build, run or checks when <see cref="Status"/> is Failed.</summary>
    public string? FailureStage { get; init; }

    public required string Summary { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public required long DurationMs { get; init; }
    public long? BuildDurationMs { get; init; }
    public long? RunDurationMs { get; init; }
    public int? ExitCode { get; init; }
    public IReadOnlyList<ExpectationResult> Checks { get; init; } = [];
    public TokenUsageSummary? TokenUsage { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<string> BuildWarnings { get; init; } = [];
    public IReadOnlyList<LogLine> Log { get; init; } = [];
}

/// <summary>Event pushed to the browser (Server-Sent Events) while a run progresses.</summary>
public sealed record RunEvent(string Type, double Elapsed)
{
    public string? Stream { get; init; }
    public string? Text { get; init; }
    public bool? EndOfLine { get; init; }
    public string? Phase { get; init; }
    public string? State { get; init; }

    /// <summary>Number of the program's read (input-request, input-sent).</summary>
    public int? InputId { get; init; }
    public RunRecord? Result { get; init; }
}
