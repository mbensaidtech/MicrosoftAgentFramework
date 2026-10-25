namespace LabDashboard.Reset;

/// <summary>Why a lab cannot be reset: nothing of it is deleted, and the reason is shown.</summary>
public enum ResetBlock
{
    /// <summary>A run of the lab is in progress.</summary>
    Running,

    /// <summary>_StartCompleted is a symbolic link or a junction: never followed nor deleted.</summary>
    Link,

    /// <summary>_StartCompleted is a file, not a folder.</summary>
    NotDirectory,

    /// <summary>A project of the catalog (Start, Solution or companion of any lab) lives inside _StartCompleted.</summary>
    ContainsProject,

    /// <summary>_StartCompleted cannot be read by the dashboard (permissions).</summary>
    Unreadable
}

public sealed record ResetPreviewRequest(IReadOnlyList<string>? LabIds);

/// <summary>The confirmation of a reset: the lab folder name typed for one lab, or the code shown by the preview for several (names not needed).</summary>
public sealed record ResetRequest(string? ConfirmationId, IReadOnlyList<string>? Names, string? Code);

/// <summary>The _StartCompleted folder of a lab as found on disk. Source files are the files outside bin/ and obj/.</summary>
public sealed record CompletedCopyInfo(string Path, int Files, long Bytes, int SourceFiles);

/// <summary>What a reset would remove for one lab.</summary>
public sealed record LabResetPreview(
    string Id,
    string FolderName,
    string Progress,
    int StartRuns,
    int SolutionRuns,
    CompletedCopyInfo? CompletedCopy,
    ResetBlock? Blocked)
{
    public bool NothingToReset => StartRuns + SolutionRuns == 0 && CompletedCopy is null && Blocked is null;
}

/// <summary>A preview: no confirmation (and no code) is issued while a lab of the selection is blocked.</summary>
public sealed record ResetPreview(string? ConfirmationId, DateTimeOffset? ExpiresAt, string? Code, IReadOnlyList<LabResetPreview> Labs);

public enum LabResetStatus
{
    Reset,
    NothingToReset,
    Failed
}

public sealed record LabResetResult(string Id, LabResetStatus Status, int RunsRemoved, int FilesRemoved, bool Verified, string? Error = null);

public enum ResetRefusal
{
    /// <summary>Unknown, expired, already used or cancelled confirmation.</summary>
    Expired,

    /// <summary>The third wrong code cancelled the confirmation.</summary>
    TooManyAttempts,
    Names,
    Code,
    Running,
    Busy
}

/// <summary>Either the per-lab results of a reset, or why it was refused (then nothing was deleted).</summary>
public sealed record ResetOutcome(IReadOnlyList<LabResetResult>? Labs, ResetRefusal? Refusal = null, IReadOnlyList<string>? RunningLabIds = null);
