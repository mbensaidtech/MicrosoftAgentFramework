using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LabDashboard.Catalog;
using LabDashboard.Execution;
using LabDashboard.History;

namespace LabDashboard.Reporting;

/// <summary>
/// Builds the payloads that describe this dashboard to the server: the lab catalog (<c>catalog.synced</c>)
/// and the progress of every lab computed from the local run history (<c>progress.snapshot</c>, authoritative on the server).
/// </summary>
public static class ProgressSnapshot
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static JsonNode Catalog(LabCatalog catalog) => JsonSerializer.SerializeToNode(new
    {
        labs = catalog.Labs.Select((lab, index) => new
        {
            id = lab.Id,
            number = lab.Number,
            track = lab.Track,
            title = lab.Title,
            level = lab.Level,
            interactive = lab.Interactive,
            sortOrder = index,
        }),
    }, JsonOptions)!;

    public static JsonNode Progress(LabCatalog catalog, RunHistoryStore history) => JsonSerializer.SerializeToNode(new
    {
        labs = catalog.Labs.Select(lab => ForLab(lab.Id, history.ForLab(lab.Id))),
    }, JsonOptions)!;

    /// <summary>Same completion rule as the local UI (LabSummary): the exercise is completed once a Start run passed every check.</summary>
    public static LabProgressSnapshot ForLab(string labId, IReadOnlyList<RunRecord> runs)
    {
        // History is newest first.
        RunRecord[] startRuns = runs.Where(r => r.Target == RunTarget.Start).ToArray();
        RunRecord? firstPassed = startRuns.LastOrDefault(r => r.Status == RunStatus.Passed);
        RunRecord? last = runs.FirstOrDefault();

        string state = firstPassed is not null ? "completed" : startRuns.Length > 0 ? "inProgress" : "notStarted";
        return new LabProgressSnapshot(
            labId,
            state,
            startRuns.Length,
            runs.Count(r => r.Target == RunTarget.Solution),
            last?.StartedAt,
            last?.Status,
            last?.Target,
            firstPassed?.StartedAt,
            startRuns.LastOrDefault()?.StartedAt,
            startRuns.Sum(r => r.TokenUsage?.Total ?? 0));
    }
}

public sealed record LabProgressSnapshot(
    string LabId,
    string State,
    int RunCount,
    int SolutionRunCount,
    DateTimeOffset? LastRunAt,
    RunStatus? LastRunStatus,
    RunTarget? LastRunTarget,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? FirstRunAt,
    long TotalTokens);
