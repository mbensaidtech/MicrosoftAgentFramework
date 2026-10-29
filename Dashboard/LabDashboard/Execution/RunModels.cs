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

public enum ScenarioStatus
{
    Passed,
    Failed,

    /// <summary>Selected, but the program stopped (or was stopped) before it started.</summary>
    NotRun,

    /// <summary>Not selected for this run.</summary>
    Skipped
}

/// <summary>
/// The result of one scenario of the program in a run: its status, the ids of its checks evaluated in the run,
/// and the errors of the run when the program stopped during this scenario.
/// </summary>
public sealed record ScenarioResult(int Number, string Title, ScenarioStatus Status)
{
    public IReadOnlyList<string> Checks { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];
}

/// <summary>The scenarios of the program when the run started, and those it runs (passed to the program with --scenarios).</summary>
public sealed record ScenarioRun(IReadOnlyList<CommonUtilities.LabScenario> Scenarios, IReadOnlyList<int> Selected);

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

    /// <summary>
    /// Every scenario of the program, selected or not (<see cref="ScenarioStatus.Skipped"/>). Null for a run without a scenario
    /// selection (a run made before the selection existed, or a lab without scenarios): every check was evaluated.
    /// </summary>
    public IReadOnlyList<ScenarioResult>? Scenarios { get; init; }
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
