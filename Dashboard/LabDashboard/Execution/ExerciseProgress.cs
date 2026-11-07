using LabDashboard.Catalog;

namespace LabDashboard.Execution;

/// <summary>
/// When an exercise is completed, from its Start runs: the rule of the lab list, the journey and the progress sent to the trainer.
/// A run with a scenario selection validates the scenarios that passed in it, and the exercise is completed once every scenario
/// passed (in one run or across several): a learner never has to run again the scenarios already validated.
/// A run without scenario results (made before the selection existed, or of a lab without scenarios) completes it when it passed.
/// <para>
/// <b>Bonus scenarios</b> (<see cref="LabDefinition.BonusScenarios"/>) are optional: the exercise is completed once every <i>other</i>
/// scenario passed, and completed with its bonus ("Done+") once every scenario, bonus included, passed. A bonus scenario that fails does
/// not undo the exercise: an attempt whose only failures are bonus scenarios passed the exercise (<see cref="PassedExercise"/>).
/// A run without scenario results never completes a bonus (nothing tells which scenarios it ran).
/// </para>
/// </summary>
public static class ExerciseProgress
{
    /// <summary>The attempts, oldest first: the Start runs that went to the end (a cancelled run proves nothing either way).</summary>
    public static RunRecord[] Attempts(IEnumerable<RunRecord> runsNewestFirst) =>
        runsNewestFirst.Where(r => r.Target == RunTarget.Start && r.Status is RunStatus.Passed or RunStatus.Failed or RunStatus.TimedOut)
            .Reverse()
            .ToArray();

    /// <summary>The index of the attempt that completed the exercise (every scenario but the bonus ones passed), or -1.</summary>
    public static int CompletingAttempt(IReadOnlyList<RunRecord> attemptsOldestFirst, IReadOnlyCollection<int>? bonus = null) =>
        FirstAttemptValidating(attemptsOldestFirst, number => bonus?.Contains(number) != true, legacyRunCounts: true);

    /// <summary>
    /// The index of the attempt that completed the exercise with its bonus (every scenario passed, bonus included), or -1;
    /// always -1 for a lab without bonus scenarios.
    /// </summary>
    public static int BonusCompletingAttempt(IReadOnlyList<RunRecord> attemptsOldestFirst, IReadOnlyCollection<int>? bonus)
    {
        if (bonus is not { Count: > 0 })
        {
            return -1;
        }

        int completing = CompletingAttempt(attemptsOldestFirst, bonus);
        int allScenarios = FirstAttemptValidating(attemptsOldestFirst, _ => true, legacyRunCounts: false);
        return completing < 0 || allScenarios < 0 ? -1 : Math.Max(completing, allScenarios);
    }

    /// <summary>The run that completed the exercise, or null.</summary>
    public static RunRecord? CompletingRun(IEnumerable<RunRecord> runsNewestFirst, IReadOnlyCollection<int>? bonus = null)
    {
        RunRecord[] attempts = Attempts(runsNewestFirst);
        int index = CompletingAttempt(attempts, bonus);
        return index < 0 ? null : attempts[index];
    }

    /// <summary>The run that completed the exercise with its bonus scenarios ("Done+"), or null.</summary>
    public static RunRecord? BonusCompletingRun(IEnumerable<RunRecord> runsNewestFirst, IReadOnlyCollection<int>? bonus)
    {
        RunRecord[] attempts = Attempts(runsNewestFirst);
        int index = BonusCompletingAttempt(attempts, bonus);
        return index < 0 ? null : attempts[index];
    }

    /// <summary>
    /// Whether an attempt passed the exercise: it passed, or its only failures are bonus scenarios (a failed or not run bonus scenario,
    /// every other selected scenario passed). A failure outside the scenarios (build, configuration check) fails every scenario.
    /// </summary>
    public static bool PassedExercise(RunRecord run, IReadOnlyCollection<int>? bonus = null)
    {
        if (run.Status == RunStatus.Passed)
        {
            return true;
        }

        if (bonus is not { Count: > 0 } || run.Scenarios is not { Count: > 0 } scenarios)
        {
            return false;
        }

        List<ScenarioResult> selected = scenarios.Where(s => s.Status != ScenarioStatus.Skipped).ToList();
        return selected.Any(s => s.Status == ScenarioStatus.Passed && !bonus.Contains(s.Number))
            && selected.All(s => s.Status == ScenarioStatus.Passed || bonus.Contains(s.Number));
    }

    /// <summary>
    /// The first attempt after which every scenario that <paramref name="counts"/> passed (in that attempt or in earlier ones).
    /// A run without scenario results validates everything when it passed, if <paramref name="legacyRunCounts"/>.
    /// </summary>
    private static int FirstAttemptValidating(IReadOnlyList<RunRecord> attemptsOldestFirst, Func<int, bool> counts, bool legacyRunCounts)
    {
        HashSet<int> validated = [];
        for (int i = 0; i < attemptsOldestFirst.Count; i++)
        {
            RunRecord run = attemptsOldestFirst[i];
            if (run.Scenarios is not { Count: > 0 } scenarios)
            {
                if (legacyRunCounts && run.Status == RunStatus.Passed)
                {
                    return i;
                }

                continue;
            }

            validated.UnionWith(scenarios.Where(s => s.Status == ScenarioStatus.Passed).Select(s => s.Number));
            if (scenarios.Where(s => counts(s.Number)).All(s => validated.Contains(s.Number)))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// The last result of each scenario on a target (passed or failed), from the newest run where it ran: what the learner already
    /// tested. Cancelled runs and the scenarios that did not run are not results.
    /// </summary>
    public static IReadOnlyDictionary<int, (ScenarioStatus Status, RunRecord Run)> LastScenarioResults(IEnumerable<RunRecord> runsNewestFirst, RunTarget target)
    {
        Dictionary<int, (ScenarioStatus, RunRecord)> last = [];
        foreach (RunRecord run in runsNewestFirst.Where(r => r.Target == target && r.Status != RunStatus.Cancelled))
        {
            foreach (ScenarioResult scenario in run.Scenarios ?? [])
            {
                if (scenario.Status is ScenarioStatus.Passed or ScenarioStatus.Failed)
                {
                    last.TryAdd(scenario.Number, (scenario.Status, run));
                }
            }
        }

        return last;
    }
}
