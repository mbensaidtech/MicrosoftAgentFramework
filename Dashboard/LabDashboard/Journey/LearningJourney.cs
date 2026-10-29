using LabDashboard.Catalog;
using LabDashboard.Execution;

namespace LabDashboard.Journey;

/// <summary>
/// The learner's journey through the catalog: state of each exercise, XP, rank, milestones, distinctions and next mission.
/// Recomputed from the local run history on every request (nothing else is stored), with the completion rule of the lab
/// list and of the admin progress snapshot: an exercise is completed once one of its Start runs passed every check.
/// </summary>
public static class LearningJourney
{
    public const int FirstTryBonus = 50;
    public const int SoloBonus = 25;

    /// <summary>Unsuccessful attempts before the first pass that earn the <c>persistence</c> distinction.</summary>
    public const int PersistenceAttempts = 3;

    /// <summary>Ranks as shares of the catalog base XP (bonuses excluded), so that they follow the size of the catalog.</summary>
    public static readonly IReadOnlyList<(string Id, double Share)> Ranks =
    [
        ("foundations", 0), ("tooling", .15), ("integration", .35), ("orchestration", .60), ("mastery", .85),
    ];

    public static readonly IReadOnlyList<string> BadgeIds = ["firstSteps", "firstTry", "solo", "persistence", "tierCleared", "summit"];

    public static int BaseXp(string level) => level.Trim().ToLowerInvariant() switch
    {
        "intermediate" => 150,
        "advanced" => 200,
        _ => 100,
    };

    public static JourneyModel Build(IReadOnlyList<LabDefinition> labs, Func<string, IReadOnlyList<RunRecord>> history, string? activeLabId)
    {
        List<LabJourney> journeys = labs
            .Select(lab => ForLab(lab, history(lab.Id), string.Equals(lab.Id, activeLabId, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        int xp = journeys.Sum(lab => lab.Xp);
        int baseXp = labs.Sum(lab => BaseXp(lab.Level));
        int completed = journeys.Count(IsDone);
        int percent = journeys.Count == 0 ? 0 : 100 * completed / journeys.Count;
        List<JourneyMilestone> milestones = Milestones(journeys);

        return new JourneyModel(xp, baseXp, completed, journeys.Count, percent, RankFor(xp, baseXp), journeys, milestones,
            Badges(journeys, milestones), NextMission(journeys));
    }

    /// <summary>Journey of one lab from its runs (newest first, as kept by the history).</summary>
    public static LabJourney ForLab(LabDefinition lab, IReadOnlyList<RunRecord> runs, bool running)
    {
        RunRecord[] startRuns = runs.Where(r => r.Target == RunTarget.Start).ToArray();
        // An attempt is a Start run that went to the end: a cancelled run proves nothing either way. With the scenario selection,
        // the exercise is completed by the attempt that validated its last scenario (ExerciseProgress).
        RunRecord[] attempts = ExerciseProgress.Attempts(runs);
        int firstPass = ExerciseProgress.CompletingAttempt(attempts);
        RunRecord? firstPassed = firstPass < 0 ? null : attempts[firstPass];
        RunRecord[] untilCompletion = firstPass < 0 ? attempts : attempts[..(firstPass + 1)];

        string state = firstPassed is not null
            ? attempts[^1].Status == RunStatus.Passed ? JourneyState.Completed : JourneyState.ToResume
            : startRuns.Length > 0 ? JourneyState.InProgress : JourneyState.NotStarted;

        int totalChecks = lab.Expectations.Count;
        // Checks passed at least once: a run of a few scenarios evaluates only their checks.
        int bestChecks = Math.Min(totalChecks, startRuns.SelectMany(r => r.Checks.Where(c => c.Passed).Select(c => c.Id)).Distinct().Count());
        // First try: no failed attempt until the exercise was completed (one run of every scenario, or several runs of a few).
        bool firstTry = firstPassed is not null && untilCompletion.All(r => r.Status == RunStatus.Passed);
        bool solo = firstPassed is not null && !runs.Any(r => r.Target == RunTarget.Solution && r.StartedAt < firstPassed.StartedAt);
        bool persistence = firstPassed is not null && untilCompletion.Count(r => r.Status != RunStatus.Passed) >= PersistenceAttempts;

        int baseXp = BaseXp(lab.Level);
        int xp = firstPassed is not null
            ? baseXp + (firstTry ? FirstTryBonus : 0) + (solo ? SoloBonus : 0)
            : totalChecks == 0 ? 0 : (int)Math.Round(baseXp * 0.5 * bestChecks / totalChecks, MidpointRounding.AwayFromZero);

        // What the next passing attempt would earn: the bonuses still within reach.
        int potentialXp = firstPassed is not null
            ? xp
            : baseXp + (attempts.All(r => r.Status == RunStatus.Passed) ? FirstTryBonus : 0) + (runs.Any(r => r.Target == RunTarget.Solution) ? 0 : SoloBonus);

        return new LabJourney(lab.Id, lab.Number, lab.Level, state, running, attempts.Length, bestChecks, totalChecks, xp, potentialXp,
            firstTry, solo, persistence, firstPassed?.StartedAt, startRuns.FirstOrDefault()?.StartedAt);
    }

    public static JourneyRank RankFor(int xp, int baseXp)
    {
        int[] thresholds = Ranks.Select(rank => (int)Math.Round(rank.Share * baseXp, MidpointRounding.AwayFromZero)).ToArray();
        // An empty catalog has every threshold at 0: stay on the first rank.
        int index = baseXp == 0 ? 0 : Array.FindLastIndex(thresholds, threshold => threshold <= xp);
        JourneyRankStep? next = index + 1 < Ranks.Count ? new JourneyRankStep(Ranks[index + 1].Id, thresholds[index + 1]) : null;
        return new JourneyRank(index + 1, Ranks[index].Id, thresholds[index], next);
    }

    private static bool IsDone(LabJourney lab) => lab.State is JourneyState.Completed or JourneyState.ToResume;

    /// <summary>One milestone per run of consecutive catalog labs sharing the same level.</summary>
    private static List<JourneyMilestone> Milestones(IReadOnlyList<LabJourney> labs)
    {
        List<List<LabJourney>> groups = [];
        foreach (LabJourney lab in labs)
        {
            if (groups.Count == 0 || !string.Equals(groups[^1][0].Level, lab.Level, StringComparison.OrdinalIgnoreCase))
            {
                groups.Add([]);
            }

            groups[^1].Add(lab);
        }

        return groups.Select(members =>
        {
            bool cleared = members.All(IsDone);
            return new JourneyMilestone(
                members[0].Level,
                members.Select(lab => lab.Id).ToList(),
                members.Count(IsDone),
                members.Count,
                cleared,
                cleared ? members.Max(lab => lab.CompletedAt) : null);
        }).ToList();
    }

    private static List<JourneyBadge> Badges(IReadOnlyList<LabJourney> labs, IReadOnlyList<JourneyMilestone> milestones)
    {
        // The first lab (by completion date) that met the condition earned the distinction.
        JourneyBadge Earliest(string id, Func<LabJourney, bool> condition)
        {
            LabJourney? lab = labs.Where(l => l.CompletedAt is not null && condition(l)).MinBy(l => l.CompletedAt);
            return new JourneyBadge(id, lab is not null, lab?.CompletedAt, lab?.Id);
        }

        JourneyMilestone? firstCleared = milestones.Where(m => m.Cleared).MinBy(m => m.ClearedAt);
        bool summit = labs.Count > 0 && labs.All(IsDone);

        return
        [
            Earliest("firstSteps", _ => true),
            Earliest("firstTry", lab => lab.FirstTry),
            Earliest("solo", lab => lab.Solo),
            Earliest("persistence", lab => lab.Persistence),
            new JourneyBadge("tierCleared", firstCleared is not null, firstCleared?.ClearedAt, null),
            new JourneyBadge("summit", summit, summit ? labs.Max(lab => lab.CompletedAt) : null, null),
        ];
    }

    /// <summary>The exercise being worked on, else one to fix, else the next one to start (catalog order).</summary>
    private static JourneyNextMission? NextMission(IReadOnlyList<LabJourney> labs)
    {
        LabJourney? current = labs.Where(lab => lab.State == JourneyState.InProgress).MaxBy(lab => lab.LastStartAt);
        if (current is not null)
        {
            return new JourneyNextMission(current.Id, JourneyState.InProgress);
        }

        LabJourney? next = labs.FirstOrDefault(lab => lab.State == JourneyState.ToResume)
            ?? labs.FirstOrDefault(lab => lab.State == JourneyState.NotStarted);
        return next is null ? null : new JourneyNextMission(next.Id, next.State);
    }
}

public static class JourneyState
{
    public const string NotStarted = "notStarted";
    public const string InProgress = "inProgress";
    public const string Completed = "completed";

    /// <summary>Completed earlier, but the latest attempt failed: still counted as completed, flagged to be fixed.</summary>
    public const string ToResume = "toResume";
}

public sealed record JourneyModel(
    int Xp,
    int BaseXp,
    int Completed,
    int Total,
    int Percent,
    JourneyRank Rank,
    IReadOnlyList<LabJourney> Labs,
    IReadOnlyList<JourneyMilestone> Milestones,
    IReadOnlyList<JourneyBadge> Badges,
    JourneyNextMission? Next);

public sealed record JourneyRank(int Index, string Id, int MinXp, JourneyRankStep? Next);

public sealed record JourneyRankStep(string Id, int MinXp);

public sealed record LabJourney(
    string Id,
    string Number,
    string Level,
    string State,
    bool Running,
    int Attempts,
    int BestChecks,
    int TotalChecks,
    int Xp,
    int PotentialXp,
    bool FirstTry,
    bool Solo,
    bool Persistence,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? LastStartAt);

public sealed record JourneyMilestone(string Level, IReadOnlyList<string> LabIds, int Completed, int Total, bool Cleared, DateTimeOffset? ClearedAt);

public sealed record JourneyBadge(string Id, bool Unlocked, DateTimeOffset? EarnedAt, string? LabId);

public sealed record JourneyNextMission(string LabId, string Reason);
