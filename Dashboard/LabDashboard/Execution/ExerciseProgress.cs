using LabDashboard.Catalog;

namespace LabDashboard.Execution;

/// <summary>
/// When an exercise is completed, from its Start runs: the rule of the lab list, the journey and the progress sent to the trainer.
/// A run with a scenario selection validates the scenarios that passed in it, and the exercise is completed once every scenario
/// passed (in one run or across several): a learner never has to run again the scenarios already validated.
/// A run without scenario results (made before the selection existed, or of a lab without scenarios) completes it when it passed.
/// </summary>
public static class ExerciseProgress
{
    /// <summary>The attempts, oldest first: the Start runs that went to the end (a cancelled run proves nothing either way).</summary>
    public static RunRecord[] Attempts(IEnumerable<RunRecord> runsNewestFirst) =>
        runsNewestFirst.Where(r => r.Target == RunTarget.Start && r.Status is RunStatus.Passed or RunStatus.Failed or RunStatus.TimedOut)
            .Reverse()
            .ToArray();

    /// <summary>The index of the attempt that completed the exercise, or -1.</summary>
    public static int CompletingAttempt(IReadOnlyList<RunRecord> attemptsOldestFirst)
    {
        HashSet<int> validated = [];
        for (int i = 0; i < attemptsOldestFirst.Count; i++)
        {
            RunRecord run = attemptsOldestFirst[i];
            if (run.Scenarios is not { Count: > 0 } scenarios)
            {
                if (run.Status == RunStatus.Passed)
                {
                    return i;
                }

                continue;
            }

            validated.UnionWith(scenarios.Where(s => s.Status == ScenarioStatus.Passed).Select(s => s.Number));
            if (scenarios.All(s => validated.Contains(s.Number)))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The run that completed the exercise, or null.</summary>
    public static RunRecord? CompletingRun(IEnumerable<RunRecord> runsNewestFirst)
    {
        RunRecord[] attempts = Attempts(runsNewestFirst);
        int index = CompletingAttempt(attempts);
        return index < 0 ? null : attempts[index];
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
