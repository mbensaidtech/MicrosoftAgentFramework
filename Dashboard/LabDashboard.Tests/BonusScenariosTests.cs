using CommonUtilities;
using LabDashboard.Catalog;
using LabDashboard.Execution;
using LabDashboard.Journey;
using LabDashboard.Reporting;

namespace LabDashboard.Tests;

/// <summary>
/// Bonus scenarios (labs.json <c>bonusScenarios</c>): the exercise is completed without them ("Completed"), and completed with its bonus
/// ("Completed+", <c>completedPlus</c>) once they passed too. A failed bonus scenario never undoes the exercise.
/// </summary>
public class BonusScenariosTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 7, 9, 0, 0, TimeSpan.Zero);
    private static readonly int[] Bonus = [4, 5];

    private static LabDefinition Lab(params int[] bonus) => new()
    {
        Id = "lab",
        Number = "04",
        Track = "Azure OpenAI",
        Title = "Lab",
        Summary = "",
        Level = "Intermediate",
        Path = "Labs/lab",
        StartProject = "Start/Start.csproj",
        SolutionProject = "Solution/Solution.csproj",
        BonusScenarios = bonus,
    };

    /// <summary>A Start run of the 5 scenarios of the lab; a scenario not listed was not selected (skipped).</summary>
    private static RunRecord Run(int minute, RunStatus status, params (int Number, ScenarioStatus Status)[] results) => new()
    {
        RunId = $"r{minute}",
        LabId = "lab",
        Target = RunTarget.Start,
        Status = status,
        Summary = "",
        StartedAt = T0.AddMinutes(minute),
        DurationMs = 1,
        Scenarios = Enumerable.Range(1, 5)
            .Select(n => new ScenarioResult(n, $"S{n}", results.FirstOrDefault(r => r.Number == n) is { Number: > 0 } r ? r.Status : ScenarioStatus.Skipped))
            .ToList(),
    };

    private static (int, ScenarioStatus) P(int number) => (number, ScenarioStatus.Passed);
    private static (int, ScenarioStatus) F(int number) => (number, ScenarioStatus.Failed);

    [Fact]
    public void The_exercise_is_completed_without_its_bonus_scenarios()
    {
        // Newest first. The learner never wrote scenarios 4 and 5: they are left out (skipped) by every run.
        RunRecord[] runs = [Run(1, RunStatus.Passed, P(1), P(2), P(3))];

        Assert.Equal("r1", ExerciseProgress.CompletingRun(runs, Bonus)?.RunId);
        Assert.Null(ExerciseProgress.BonusCompletingRun(runs, Bonus));
        // Without the bonus declaration, the same runs do not complete the exercise: scenarios 4 and 5 never passed.
        Assert.Null(ExerciseProgress.CompletingRun(runs));
    }

    [Fact]
    public void The_bonus_is_completed_once_every_bonus_scenario_passed_too_in_one_run_or_several()
    {
        RunRecord[] runs =
        [
            Run(3, RunStatus.Passed, P(5)),
            Run(2, RunStatus.Failed, P(4), F(5)),
            Run(1, RunStatus.Passed, P(1), P(2), P(3)),
        ];

        Assert.Equal("r1", ExerciseProgress.CompletingRun(runs, Bonus)?.RunId);
        Assert.Null(ExerciseProgress.BonusCompletingRun(runs[1..], Bonus));
        Assert.Equal("r3", ExerciseProgress.BonusCompletingRun(runs, Bonus)?.RunId);
    }

    [Fact]
    public void Bonus_scenarios_passed_before_the_exercise_complete_the_bonus_with_the_exercise()
    {
        RunRecord[] runs =
        [
            Run(2, RunStatus.Passed, P(3)),
            Run(1, RunStatus.Failed, P(1), P(2), F(3), P(4), P(5)),
        ];

        Assert.Equal("r2", ExerciseProgress.CompletingRun(runs, Bonus)?.RunId);
        Assert.Equal("r2", ExerciseProgress.BonusCompletingRun(runs, Bonus)?.RunId);
    }

    [Fact]
    public void A_lab_without_bonus_never_completes_a_bonus_and_a_legacy_run_only_completes_the_exercise()
    {
        RunRecord all = Run(1, RunStatus.Passed, P(1), P(2), P(3), P(4), P(5));
        RunRecord legacy = all with { RunId = "legacy", Scenarios = null };

        Assert.Null(ExerciseProgress.BonusCompletingRun([all], []));
        Assert.Equal("legacy", ExerciseProgress.CompletingRun([legacy], Bonus)?.RunId);
        Assert.Null(ExerciseProgress.BonusCompletingRun([legacy], Bonus));
    }

    [Fact]
    public void An_attempt_whose_only_failures_are_bonus_scenarios_passed_the_exercise()
    {
        Assert.True(ExerciseProgress.PassedExercise(Run(1, RunStatus.Failed, P(1), P(2), P(3), F(4), (5, ScenarioStatus.NotRun)), Bonus));
        Assert.False(ExerciseProgress.PassedExercise(Run(1, RunStatus.Failed, P(1), F(2), P(4)), Bonus));
        // Only bonus scenarios ran, and one failed: nothing of the exercise passed in this attempt.
        Assert.False(ExerciseProgress.PassedExercise(Run(1, RunStatus.Failed, P(4), F(5)), Bonus));
        Assert.False(ExerciseProgress.PassedExercise(Run(1, RunStatus.Failed, P(1), F(4)), []));
        Assert.True(ExerciseProgress.PassedExercise(Run(1, RunStatus.Passed, P(1)), []));
    }

    [Fact]
    public void A_failed_bonus_attempt_does_not_put_the_lab_back_to_resume_and_the_bonus_adds_xp()
    {
        LabDefinition lab = Lab(Bonus);
        RunRecord done = Run(1, RunStatus.Passed, P(1), P(2), P(3));
        RunRecord bonusFailed = Run(2, RunStatus.Failed, P(4), F(5)) with { Scenarios = Run(2, RunStatus.Failed, P(1), P(4), F(5)).Scenarios };
        RunRecord bonusPassed = Run(3, RunStatus.Passed, P(5));

        LabJourney afterFailure = LearningJourney.ForLab(lab, [bonusFailed, done], running: false);
        Assert.Equal(JourneyState.Completed, afterFailure.State);
        Assert.False(afterFailure.BonusCompleted);
        Assert.True(afterFailure.FirstTry);

        LabJourney plus = LearningJourney.ForLab(lab, [bonusPassed, bonusFailed, done], running: false);
        Assert.Equal(JourneyState.Completed, plus.State);
        Assert.True(plus.BonusCompleted);
        Assert.Equal(bonusPassed.StartedAt, plus.BonusCompletedAt);
        Assert.Equal(afterFailure.Xp + LearningJourney.BonusScenariosXp, plus.Xp);
        Assert.Equal(2, plus.BonusScenarios);
    }

    [Fact]
    public void The_progress_snapshot_keeps_the_completed_state_and_adds_the_date_of_the_bonus()
    {
        RunRecord done = Run(1, RunStatus.Passed, P(1), P(2), P(3));
        RunRecord bonus = Run(2, RunStatus.Passed, P(4), P(5));

        LabProgressSnapshot completed = ProgressSnapshot.ForLab("lab", [done], Bonus);
        Assert.Equal("completed", completed.State);
        Assert.Null(completed.BonusCompletedAt);

        LabProgressSnapshot plus = ProgressSnapshot.ForLab("lab", [bonus, done], Bonus);
        Assert.Equal("completed", plus.State);
        Assert.Equal(done.StartedAt, plus.CompletedAt);
        Assert.Equal(bonus.StartedAt, plus.BonusCompletedAt);
    }

    [Fact]
    public void A_bonus_comment_marks_a_scenario_as_bonus_in_the_program()
    {
        IReadOnlyList<LabScenario> scenarios = LabScenarios.Parse("""
            #region Scenario 1: Required
            Run();
            #endregion
            #region Scenario 2: Optional
            // Requires scenario 1
            // Bonus scenario (optional): the lab is completed without it
            Run();
            #endregion
            """);

        Assert.Equal([false, true], scenarios.Select(s => s.Bonus));
        Assert.Contains(LabScenarios.Describe(scenarios), line => line.Contains("Optional") && line.EndsWith("[bonus]", StringComparison.Ordinal));
    }

    /// <summary>labs.json is the rule (it is published to the admin dashboard); the comments tell the learner and the CLI: they must agree.</summary>
    [Fact]
    public void The_bonus_scenarios_of_the_catalog_are_the_ones_marked_in_the_exercise_and_the_solution()
    {
        LabCatalog catalog = LabCatalogTests.LoadRealCatalog();
        foreach (LabDefinition lab in catalog.Labs)
        {
            foreach (RunTarget target in Enum.GetValues<RunTarget>())
            {
                IReadOnlyList<int> marked = [.. LabScenarios.ReadFile(catalog.ProgramPath(lab, target)).Where(s => s.Bonus).Select(s => s.Number)];
                Assert.True(lab.BonusScenarios.Order().SequenceEqual(marked), $"{lab.Id} ({target}): labs.json {string.Join(",", lab.BonusScenarios)}, Program.cs {string.Join(",", marked)}");
            }
        }

        Assert.Equal([4, 5], catalog.Find("azureopenai-lab04")!.BonusScenarios);
        Assert.Equal([4, 5], catalog.Find("azureopenai-lab05")!.BonusScenarios);
        Assert.Equal([5, 6], catalog.Find("azureopenai-lab07")!.BonusScenarios);
    }

    [Fact]
    public void The_catalog_refuses_an_unknown_bonus_scenario()
    {
        string root = Directory.CreateTempSubdirectory().FullName;
        Directory.CreateDirectory(Path.Combine(root, "Lab", "Start"));
        Directory.CreateDirectory(Path.Combine(root, "Lab", "Solution"));
        File.WriteAllText(Path.Combine(root, "Lab", "Start", "a.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(root, "Lab", "Solution", "a.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(root, "Lab", "Solution", "Program.cs"), "#region Scenario 1: One\n#endregion\n#region Scenario 2: Two\n#endregion\n");
        string catalogFile = Path.Combine(root, "labs.json");
        File.WriteAllText(catalogFile, """
            { "labs": [ { "id": "x", "number": "99", "track": "t", "title": "t", "summary": "s", "level": "l",
                          "path": "Lab", "startProject": "Start/a.csproj", "solutionProject": "Solution/a.csproj", "bonusScenarios": [3] } ] }
            """);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => LabCatalog.Load(catalogFile, root));
        Assert.Contains("bonus scenario 3", error.Message);
        Directory.Delete(root, recursive: true);
    }
}
