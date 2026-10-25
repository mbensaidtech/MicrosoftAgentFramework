using System.Security.Cryptography;
using LabDashboard.Catalog;
using LabDashboard.Execution;
using LabDashboard.History;
using LabDashboard.Reporting;

namespace LabDashboard.Reset;

/// <summary>
/// Puts labs back to "Not started" (specs/dashboard-exercise-reset): removes their runs from the history and their
/// _StartCompleted folder, nothing else — Start/, Solution/, the instructions and the settings are never touched.
/// A reset needs a confirmation issued by a preview of exactly those labs, with the lab folder name typed for one lab,
/// or the random code of the preview for several: short-lived and single use, so a double click or a replayed request never resets twice.
/// </summary>
public sealed class LabResetService(
    LabCatalog catalog,
    RunHistoryStore history,
    LabRunner runner,
    ILogger<LabResetService> logger,
    ReportingClient? reporting = null,
    TimeProvider? time = null)
{
    /// <summary>Git-ignored completed copy of Start/ left in a lab folder (see .gitignore).</summary>
    public const string CompletedCopyFolder = "_StartCompleted";

    public const int CodeLength = 5;
    public const int MaxCodeAttempts = 3;
    public static readonly TimeSpan ConfirmationLifetime = TimeSpan.FromMinutes(10);

    // Read on screen and typed again: no 0/O nor 1/I/L.
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int MaxPendingConfirmations = 20;

    // Created by the operating system when a folder is browsed: not a sign that the copy holds code.
    private static readonly HashSet<string> SystemFiles = new(StringComparer.OrdinalIgnoreCase) { ".DS_Store", "Thumbs.db", "desktop.ini" };

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly object _gate = new();
    private readonly Dictionary<string, PendingReset> _pending = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _executing = new(1, 1);

    public string CompletedCopyPath(LabDefinition lab) => Path.Combine(catalog.LabDirectory(lab), CompletedCopyFolder);

    /// <summary>Something named _StartCompleted is in the lab folder (shown as a badge when choosing the labs to reset).</summary>
    public bool HasCompletedCopy(LabDefinition lab)
    {
        try
        {
            return Kind(CompletedCopyPath(lab)) != EntryKind.Absent;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    /// <summary>
    /// Describes what a reset of these labs would remove and, unless one is blocked, issues the confirmation (with a code
    /// for several labs). Nothing is deleted. Error codes: <c>required</c>, <c>unknown</c>.
    /// </summary>
    public ResetPreview? Preview(IReadOnlyList<string>? labIds, out string? error)
    {
        if (labIds is null || labIds.Count == 0)
        {
            error = "required";
            return null;
        }

        HashSet<string> selected = new(StringComparer.OrdinalIgnoreCase);
        foreach (string id in labIds)
        {
            if (catalog.Find(id ?? "") is not { } lab)
            {
                error = "unknown";
                return null;
            }

            selected.Add(lab.Id);
        }

        error = null;
        List<LabDefinition> labs = catalog.Labs.Where(lab => selected.Contains(lab.Id)).ToList();
        List<LabResetPreview> previews = labs.Select(Inspect).ToList();
        if (previews.Any(p => p.Blocked is not null))
        {
            return new ResetPreview(null, null, null, previews);
        }

        string confirmationId = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        string? code = labs.Count > 1 ? RandomNumberGenerator.GetString(CodeAlphabet, CodeLength) : null;
        DateTimeOffset expiresAt = _time.GetUtcNow() + ConfirmationLifetime;
        lock (_gate)
        {
            Prune();
            _pending[confirmationId] = new PendingReset(labs.Select(lab => lab.Id).ToList(), previews.Select(p => p.FolderName).ToList(), code, expiresAt);
        }

        return new ResetPreview(confirmationId, expiresAt, code, previews);
    }

    /// <summary>
    /// Resets the labs of a confirmation once the name (one lab) or the code (several labs) matches. Refusals delete nothing;
    /// the confirmation is consumed before the first deletion, so the same request can never reset twice.
    /// </summary>
    public async Task<ResetOutcome> ExecuteAsync(ResetRequest request)
    {
        if (!_executing.Wait(0))
        {
            return new ResetOutcome(null, ResetRefusal.Busy);
        }

        try
        {
            PendingReset? plan;
            lock (_gate)
            {
                Prune();
                if (request.ConfirmationId is not { } id || !_pending.TryGetValue(id, out plan))
                {
                    return new ResetOutcome(null, ResetRefusal.Expired);
                }

                // One lab: its folder name. Several labs: the code only, the names are not asked.
                if (plan.Code is null)
                {
                    if (!NamesMatch(plan.FolderNames, request.Names))
                    {
                        return new ResetOutcome(null, ResetRefusal.Names);
                    }
                }
                else if (!string.Equals(request.Code?.Trim(), plan.Code, StringComparison.OrdinalIgnoreCase))
                {
                    if (++plan.WrongCodes < MaxCodeAttempts)
                    {
                        return new ResetOutcome(null, ResetRefusal.Code);
                    }

                    _pending.Remove(id);
                    return new ResetOutcome(null, ResetRefusal.TooManyAttempts);
                }

                // A run started after this check is recorded after the reset, as a new run.
                string? activeLabId = runner.ActiveRun?.Lab.Id;
                if (activeLabId is not null && plan.LabIds.Contains(activeLabId, StringComparer.OrdinalIgnoreCase))
                {
                    return new ResetOutcome(null, ResetRefusal.Running, [activeLabId]);
                }

                _pending.Remove(id);
            }

            return new ResetOutcome(await ResetAsync(plan.LabIds.Select(labId => catalog.Find(labId)!).ToList()));
        }
        finally
        {
            _executing.Release();
        }
    }

    private async Task<IReadOnlyList<LabResetResult>> ResetAsync(IReadOnlyList<LabDefinition> labs)
    {
        Dictionary<string, FolderDeletion> folders = labs.ToDictionary(lab => lab.Id, DeleteCompletedCopy);

        // Runs are removed only where the folder is gone: a lab never looks reset while its folder remains.
        List<string> clearable = labs.Where(lab => folders[lab.Id].Error is null).Select(lab => lab.Id).ToList();
        IReadOnlyDictionary<string, int> removed = new Dictionary<string, int>();
        string? historyError = null;
        try
        {
            removed = await history.RemoveLabsAsync(clearable);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Reset: the run history could not be written for {LabIds}.", string.Join(", ", clearable));
            historyError = $"The run history could not be written: {ex.Message}";
        }

        if (removed.Values.Any(count => count > 0))
        {
            reporting?.ProgressChanged();
        }

        List<LabResetResult> results = labs.Select(lab => Result(lab, folders[lab.Id], removed.GetValueOrDefault(lab.Id), historyError)).ToList();
        foreach (LabResetResult result in results)
        {
            logger.LogInformation("Reset {LabId}: {Status}, {Runs} run(s) and {Files} file(s) removed{Error}.",
                result.Id, result.Status, result.RunsRemoved, result.FilesRemoved, result.Error is null ? "" : $" — {result.Error}");
        }

        return results;
    }

    private LabResetResult Result(LabDefinition lab, FolderDeletion folder, int runsRemoved, string? historyError)
    {
        if (folder.Error is not null || historyError is not null)
        {
            return new LabResetResult(lab.Id, LabResetStatus.Failed, 0, folder.FilesRemoved, false, folder.Error ?? historyError);
        }

        // Checked again from the outside: no run left, no folder, the exercise and its solution still in place.
        bool verified = history.ForLab(lab.Id).Count == 0
            && Kind(CompletedCopyPath(lab)) == EntryKind.Absent
            && File.Exists(catalog.ProjectPath(lab, RunTarget.Start))
            && File.Exists(catalog.ProjectPath(lab, RunTarget.Solution));
        if (!verified)
        {
            return new LabResetResult(lab.Id, LabResetStatus.Failed, runsRemoved, folder.FilesRemoved, false,
                "The reset could not be verified: a run or the _StartCompleted folder is still there, or a project is missing.");
        }

        LabResetStatus status = runsRemoved == 0 && !folder.Existed ? LabResetStatus.NothingToReset : LabResetStatus.Reset;
        return new LabResetResult(lab.Id, status, runsRemoved, folder.FilesRemoved, true);
    }

    private FolderDeletion DeleteCompletedCopy(LabDefinition lab)
    {
        // Checked again right before deleting: the folder may have changed since the preview.
        (CompletedCopyInfo? copy, ResetBlock? block) = InspectCompletedCopy(lab);
        if (copy is null)
        {
            return new FolderDeletion(false, 0, null);
        }

        if (block is not null)
        {
            return new FolderDeletion(true, 0, BlockMessage(block.Value, copy.Path));
        }

        string path = CompletedCopyPath(lab);
        try
        {
            // Links inside the folder are removed, never followed.
            Directory.Delete(path, recursive: true);
            return new FolderDeletion(true, copy.Files, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Reset {LabId}: {Folder} could not be deleted.", lab.Id, copy.Path);
            int left = Kind(path) == EntryKind.Directory ? Measure(path, copy.Path).Files : 0;
            return new FolderDeletion(true, copy.Files - left,
                $"{copy.Path} could not be deleted ({copy.Files - left} of {copy.Files} files deleted): {ex.Message}");
        }
    }

    private LabResetPreview Inspect(LabDefinition lab)
    {
        IReadOnlyList<RunRecord> runs = history.ForLab(lab.Id);
        (CompletedCopyInfo? copy, ResetBlock? block) = InspectCompletedCopy(lab);
        if (runner.ActiveRun?.Lab.Id == lab.Id)
        {
            block = ResetBlock.Running;
        }

        return new LabResetPreview(
            lab.Id,
            FolderName(lab),
            ProgressSnapshot.ForLab(lab.Id, runs).State,
            runs.Count(r => r.Target == RunTarget.Start),
            runs.Count(r => r.Target == RunTarget.Solution),
            copy,
            block);
    }

    /// <summary>The _StartCompleted entry of a lab and, when it must not be deleted, why (fixed name, real folder, no catalog project inside).</summary>
    private (CompletedCopyInfo? Copy, ResetBlock? Block) InspectCompletedCopy(LabDefinition lab)
    {
        string path = CompletedCopyPath(lab);
        string display = Path.GetRelativePath(catalog.RepositoryRoot, path);
        try
        {
            return Kind(path) switch
            {
                EntryKind.Absent => (null, null),
                EntryKind.Link => (new CompletedCopyInfo(display, 0, 0, 0), ResetBlock.Link),
                EntryKind.File => (new CompletedCopyInfo(display, 0, 0, 0), ResetBlock.NotDirectory),
                _ => (Measure(path, display), ContainsCatalogProject(path) ? ResetBlock.ContainsProject : null),
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Reset {LabId}: {Folder} cannot be read.", lab.Id, display);
            return (new CompletedCopyInfo(display, 0, 0, 0), ResetBlock.Unreadable);
        }
    }

    private bool ContainsCatalogProject(string folder)
    {
        string prefix = folder + Path.DirectorySeparatorChar;
        return catalog.Labs
            .SelectMany(lab => new[] { catalog.ProjectPath(lab, RunTarget.Start), catalog.ProjectPath(lab, RunTarget.Solution), catalog.CompanionProjectPath(lab) })
            .Any(project => project is not null && project.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Counts the entries of a folder without following links (a link is one entry, removed as such).</summary>
    private static CompletedCopyInfo Measure(string root, string display)
    {
        EnumerationOptions options = new() { AttributesToSkip = 0, IgnoreInaccessible = true };
        int files = 0, sourceFiles = 0;
        long bytes = 0;
        Stack<DirectoryInfo> folders = new([new DirectoryInfo(root)]);
        while (folders.TryPop(out DirectoryInfo? folder))
        {
            foreach (FileSystemInfo entry in folder.EnumerateFileSystemInfos("*", options))
            {
                bool isLink = entry.Attributes.HasFlag(FileAttributes.ReparsePoint);
                if (entry is DirectoryInfo directory && !isLink)
                {
                    folders.Push(directory);
                    continue;
                }

                files++;
                if (!isLink && entry is FileInfo file)
                {
                    bytes += file.Length;
                }

                if (!IsBuildOutput(Path.GetRelativePath(root, entry.FullName)) && !SystemFiles.Contains(entry.Name))
                {
                    sourceFiles++;
                }
            }
        }

        return new CompletedCopyInfo(display, files, bytes, sourceFiles);
    }

    private static bool IsBuildOutput(string relativePath) =>
        relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[..^1]
            .Any(segment => segment.Equals("bin", StringComparison.OrdinalIgnoreCase) || segment.Equals("obj", StringComparison.OrdinalIgnoreCase));

    private enum EntryKind
    {
        Absent,
        Directory,
        Link,
        File
    }

    /// <summary>What is at a path, without following a symbolic link or a junction (a broken link is a link).</summary>
    private static EntryKind Kind(string path)
    {
        DirectoryInfo entry = new(path);
        try
        {
            if (entry.LinkTarget is not null)
            {
                return EntryKind.Link;
            }
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return EntryKind.Absent;
        }

        if (entry.Exists)
        {
            return entry.Attributes.HasFlag(FileAttributes.ReparsePoint) ? EntryKind.Link : EntryKind.Directory;
        }

        return File.Exists(path) ? EntryKind.File : EntryKind.Absent;
    }

    private static string BlockMessage(ResetBlock block, string path) => block switch
    {
        ResetBlock.Link => $"{path} is a symbolic link or a junction: the dashboard never follows nor deletes it. Remove it by hand.",
        ResetBlock.NotDirectory => $"{path} is a file, not a folder: the dashboard does not delete it.",
        ResetBlock.ContainsProject => $"{path} contains a project of the catalog: it is not deleted.",
        ResetBlock.Unreadable => $"{path} cannot be read by the dashboard.",
        _ => $"{path} cannot be deleted now.",
    };

    /// <summary>The name of the exercise as typed to confirm a reset (and copied from the page): its lab folder, e.g. Lab01-FirstBasicAIAgent.</summary>
    public static string FolderName(LabDefinition lab) => Path.GetFileName(lab.Path.TrimEnd('/', '\\'));

    private static bool NamesMatch(IReadOnlyList<string> expected, IReadOnlyList<string>? typed) =>
        typed is not null && typed.Count == expected.Count &&
        typed.Select(name => name?.Trim() ?? "").Order(StringComparer.Ordinal).SequenceEqual(expected.Order(StringComparer.Ordinal), StringComparer.Ordinal);

    private void Prune()
    {
        DateTimeOffset now = _time.GetUtcNow();
        foreach (string id in _pending.Where(p => p.Value.ExpiresAt <= now).Select(p => p.Key).ToList())
        {
            _pending.Remove(id);
        }

        while (_pending.Count >= MaxPendingConfirmations)
        {
            _pending.Remove(_pending.MinBy(p => p.Value.ExpiresAt).Key);
        }
    }

    private sealed class PendingReset(IReadOnlyList<string> labIds, IReadOnlyList<string> folderNames, string? code, DateTimeOffset expiresAt)
    {
        public IReadOnlyList<string> LabIds { get; } = labIds;
        public IReadOnlyList<string> FolderNames { get; } = folderNames;
        public string? Code { get; } = code;
        public DateTimeOffset ExpiresAt { get; } = expiresAt;
        public int WrongCodes { get; set; }
    }

    private sealed record FolderDeletion(bool Existed, int FilesRemoved, string? Error);
}
