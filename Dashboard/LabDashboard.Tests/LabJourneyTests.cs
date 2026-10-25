using LabDashboard.Catalog;
using LabDashboard.Execution;
using LabDashboard.Journey;

namespace LabDashboard.Tests;

public class LabJourneyTests
{
    private static readonly DateTimeOffset Day = new(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);

    private static LabDefinition Lab(string id, string level = "Beginner", int checks = 4) => new()
    {
        Id = id,
        Number = id[^2..],
        Track = "Azure OpenAI",
        Title = id,
        Summary = "",
        Level = level,
        Path = $"Labs/{id}",
        StartProject = "Start/Start.csproj",
        SolutionProject = "Solution/Solution.csproj",
        Expectations = Enumerable.Range(1, checks).Select(i => new LabExpectation($"c{i}", $"Check {i}", ".")).ToList(),
    };

    private static int _minute;

    /// <summary>A run, later than every run created before it (the history is newest first: list runs from newest to oldest).</summary>
    private static RunRecord Run(RunTarget target, RunStatus status, int passedChecks = 0, int totalChecks = 4) => new()
    {
        RunId = Guid.NewGuid().ToString("N"),
        LabId = "lab",
        Target = target,
        Status = status,
        Summary = "",
        StartedAt = Day.AddMinutes(Interlocked.Increment(ref _minute)),
        DurationMs = 1000,
        Checks = Enumerable.Range(1, totalChecks).Select(i => new ExpectationResult($"c{i}", $"Check {i}", status == RunStatus.Passed || i <= passedChecks)).ToList(),
    };

    private static RunRecord Start(RunStatus status, int passedChecks = 0) => Run(RunTarget.Start, status, passedChecks);
    private static RunRecord Solution(RunStatus status = RunStatus.Passed) => Run(RunTarget.Solution, status);

    /// <summary>Runs given oldest first (as they happen), stored newest first (as the history keeps them).</summary>
    private static RunRecord[] History(params RunRecord[] oldestFirst) => oldestFirst.Reverse().ToArray();

    private static LabJourney ForLab(LabDefinition lab, params RunRecord[] oldestFirst) =>
        LearningJourney.ForLab(lab, History(oldestFirst), running: false);

    [Fact]
    public void An_empty_history_starts_the_journey_at_the_first_rank_with_every_distinction_locked()
    {
        LabDefinition[] labs = [Lab("lab01"), Lab("lab02"), Lab("lab03", "Intermediate")];

        JourneyModel journey = LearningJourney.Build(labs, _ => [], activeLabId: null);

        Assert.Equal((0, 350, 0, 3, 0), (journey.Xp, journey.BaseXp, journey.Completed, journey.Total, journey.Percent));
        Assert.Equal(new JourneyRank(1, "foundations", 0, new JourneyRankStep("tooling", 53)), journey.Rank); // 15 % of 350 = 52.5
        Assert.All(journey.Labs, lab => Assert.Equal(JourneyState.NotStarted, lab.State));
        Assert.Equal(LearningJourney.BadgeIds, journey.Badges.Select(b => b.Id));
        Assert.All(journey.Badges, badge => Assert.False(badge.Unlocked));
        Assert.Equal(new JourneyNextMission("lab01", JourneyState.NotStarted), journey.Next);
    }

    [Fact]
    public void Solution_runs_never_start_an_exercise()
    {
        LabJourney lab = ForLab(Lab("lab01"), Solution(), Solution(RunStatus.Failed));

        Assert.Equal((JourneyState.NotStarted, 0, 0), (lab.State, lab.Attempts, lab.Xp));
        Assert.Equal(100 + LearningJourney.FirstTryBonus, lab.PotentialXp);
    }

    [Fact]
    public void A_failed_attempt_is_in_progress_with_partial_xp_from_its_best_checks()
    {
        LabJourney lab = ForLab(Lab("lab04", "Intermediate", checks: 6), Start(RunStatus.Failed, passedChecks: 4), Start(RunStatus.Failed, passedChecks: 1));

        Assert.Equal(JourneyState.InProgress, lab.State);
        Assert.Equal((2, 4, 6), (lab.Attempts, lab.BestChecks, lab.TotalChecks));
        Assert.Equal(50, lab.Xp); // 150 × 0.5 × 4/6
        Assert.Equal(150 + LearningJourney.SoloBonus, lab.PotentialXp);
    }

    [Fact]
    public void A_lab_without_checks_earns_no_partial_xp()
    {
        LabJourney lab = ForLab(Lab("lab01", checks: 0), Start(RunStatus.Failed));

        Assert.Equal((JourneyState.InProgress, 0), (lab.State, lab.Xp));
    }

    [Fact]
    public void Passing_on_the_first_attempt_without_the_solution_earns_both_bonuses()
    {
        LabJourney lab = ForLab(Lab("lab01"), Start(RunStatus.Passed), Solution());

        Assert.Equal(JourneyState.Completed, lab.State);
        Assert.True(lab.FirstTry);
        Assert.True(lab.Solo);
        Assert.Equal(175, lab.Xp);
        Assert.Equal(lab.Xp, lab.PotentialXp);
    }

    [Fact]
    public void Running_the_solution_before_passing_removes_the_solo_bonus()
    {
        LabJourney lab = ForLab(Lab("lab02", "Intermediate"), Start(RunStatus.Failed), Solution(), Start(RunStatus.Passed));

        Assert.False(lab.FirstTry);
        Assert.False(lab.Solo);
        Assert.Equal(150, lab.Xp);
    }

    [Fact]
    public void A_failure_after_a_pass_puts_the_exercise_to_resume_and_keeps_its_xp()
    {
        LabJourney lab = ForLab(Lab("lab01"), Start(RunStatus.Passed), Start(RunStatus.TimedOut));

        Assert.Equal(JourneyState.ToResume, lab.State);
        Assert.Equal(175, lab.Xp);
        Assert.NotNull(lab.CompletedAt);
    }

    [Fact]
    public void A_cancelled_run_is_not_an_attempt()
    {
        LabJourney afterPass = ForLab(Lab("lab01"), Start(RunStatus.Passed), Start(RunStatus.Cancelled));
        LabJourney onlyCancelled = ForLab(Lab("lab02"), Start(RunStatus.Cancelled));
        LabJourney cancelledFirst = ForLab(Lab("lab03"), Start(RunStatus.Cancelled), Start(RunStatus.Passed));

        Assert.Equal((JourneyState.Completed, 1), (afterPass.State, afterPass.Attempts));
        // Same rule as the lab list: any Start run makes the exercise "in progress".
        Assert.Equal((JourneyState.InProgress, 0), (onlyCancelled.State, onlyCancelled.Attempts));
        Assert.True(cancelledFirst.FirstTry);
    }

    [Fact]
    public void Three_unsuccessful_attempts_before_the_pass_earn_persistence()
    {
        LabJourney twice = ForLab(Lab("lab01"), Start(RunStatus.Failed), Start(RunStatus.Failed), Start(RunStatus.Passed));
        LabJourney threeTimes = ForLab(Lab("lab02"), Start(RunStatus.Failed), Start(RunStatus.Failed), Start(RunStatus.TimedOut), Start(RunStatus.Passed));

        Assert.False(twice.Persistence);
        Assert.True(threeTimes.Persistence);
    }

    [Fact]
    public void An_active_run_is_shown_on_its_lab_only()
    {
        LabDefinition[] labs = [Lab("lab01"), Lab("lab02")];

        JourneyModel journey = LearningJourney.Build(labs, _ => [], activeLabId: "LAB02");

        Assert.Equal([false, true], journey.Labs.Select(lab => lab.Running));
    }

    [Theory]
    [InlineData(0, 1, "foundations")]
    [InlineData(269, 1, "foundations")]
    [InlineData(270, 2, "tooling")]
    [InlineData(630, 3, "integration")]
    [InlineData(1080, 4, "orchestration")]
    [InlineData(1530, 5, "mastery")]
    [InlineData(2775, 5, "mastery")]
    public void Ranks_are_shares_of_the_catalog_base_xp(int xp, int index, string id)
    {
        JourneyRank rank = LearningJourney.RankFor(xp, baseXp: 1800);

        Assert.Equal((index, id), (rank.Index, rank.Id));
        Assert.Equal(index == 5, rank.Next is null);
    }

    [Fact]
    public void Base_xp_depends_on_the_level()
    {
        Assert.Equal([100, 150, 200, 100], new[] { "Beginner", "Intermediate", "Advanced", "Expert" }.Select(LearningJourney.BaseXp));
    }

    [Fact]
    public void Milestones_group_consecutive_labs_of_the_same_level_and_unlock_the_distinctions()
    {
        LabDefinition[] labs = [Lab("lab01"), Lab("lab02"), Lab("lab03", "Intermediate"), Lab("lab04", "Intermediate")];
        Dictionary<string, RunRecord[]> runs = new()
        {
            ["lab01"] = History(Start(RunStatus.Passed)),
            ["lab02"] = History(Start(RunStatus.Failed), Start(RunStatus.Failed), Start(RunStatus.Failed), Start(RunStatus.Passed)),
            ["lab03"] = History(Start(RunStatus.Failed, passedChecks: 2)),
        };

        JourneyModel journey = LearningJourney.Build(labs, id => runs.GetValueOrDefault(id, []), activeLabId: null);

        Assert.Equal(["Beginner", "Intermediate"], journey.Milestones.Select(m => m.Level));
        Assert.Equal((2, 2, true), (journey.Milestones[0].Completed, journey.Milestones[0].Total, journey.Milestones[0].Cleared));
        Assert.Equal(journey.Labs[1].CompletedAt, journey.Milestones[0].ClearedAt);
        Assert.False(journey.Milestones[1].Cleared);

        Dictionary<string, JourneyBadge> badges = journey.Badges.ToDictionary(b => b.Id);
        Assert.Equal("lab01", badges["firstSteps"].LabId);
        Assert.Equal("lab01", badges["firstTry"].LabId);
        Assert.Equal("lab02", badges["persistence"].LabId);
        Assert.True(badges["tierCleared"].Unlocked);
        Assert.False(badges["summit"].Unlocked);

        Assert.Equal(175 + 125 + 38, journey.Xp); // lab03: 150 × 0.5 × 2/4 = 37.5, rounded up
        Assert.Equal((2, 50), (journey.Completed, journey.Percent));
        Assert.Equal(new JourneyNextMission("lab03", JourneyState.InProgress), journey.Next);
    }

    [Fact]
    public void The_next_mission_is_the_latest_exercise_in_progress_then_one_to_resume_then_the_next_to_start()
    {
        LabDefinition[] labs = [Lab("lab01"), Lab("lab02"), Lab("lab03"), Lab("lab04")];
        Dictionary<string, RunRecord[]> runs = new()
        {
            ["lab01"] = History(Start(RunStatus.Passed), Start(RunStatus.Failed)),
            ["lab03"] = History(Start(RunStatus.Failed)),
            ["lab02"] = History(Start(RunStatus.Failed)),
        };
        JourneyNextMission? Next() => LearningJourney.Build(labs, id => runs.GetValueOrDefault(id, []), activeLabId: null).Next;

        Assert.Equal(new JourneyNextMission("lab02", JourneyState.InProgress), Next());

        runs.Remove("lab02");
        runs.Remove("lab03");
        Assert.Equal(new JourneyNextMission("lab01", JourneyState.ToResume), Next());

        runs["lab01"] = History(Start(RunStatus.Passed));
        Assert.Equal(new JourneyNextMission("lab02", JourneyState.NotStarted), Next());
    }

    [Fact]
    public void Completing_every_exercise_reaches_the_summit()
    {
        LabDefinition[] labs = [Lab("lab01"), Lab("lab02", "Intermediate")];

        JourneyModel journey = LearningJourney.Build(labs, _ => History(Start(RunStatus.Passed)), activeLabId: null);

        Assert.Null(journey.Next);
        Assert.Equal((2, 100), (journey.Completed, journey.Percent));
        Assert.Equal("mastery", journey.Rank.Id);
        Assert.Equal(["firstSteps", "firstTry", "solo", "tierCleared", "summit"], journey.Badges.Where(b => b.Unlocked).Select(b => b.Id));
        Assert.Equal(journey.Labs.Max(lab => lab.CompletedAt), journey.Badges.Single(b => b.Id == "summit").EarnedAt);
    }

    [Fact]
    public void An_empty_catalog_stays_on_the_first_rank()
    {
        JourneyModel journey = LearningJourney.Build([], _ => [], activeLabId: null);

        Assert.Equal((1, 0, 0), (journey.Rank.Index, journey.Total, journey.Percent));
        Assert.Null(journey.Next);
        Assert.Empty(journey.Milestones);
        Assert.All(journey.Badges, badge => Assert.False(badge.Unlocked));
    }
}
