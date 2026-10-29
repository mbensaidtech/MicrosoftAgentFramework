using CommonUtilities;
using LabDashboard.Catalog;
using LabDashboard.Execution;

namespace LabDashboard.Tests;

/// <summary>
/// The scenario selection rules shared by the labs (CommonUtilities/LabScenarios.cs, compiled by both): the reading of Program.cs,
/// what is implemented (code no longer the delivered one), the command-line option, the default, the evaluation per scenario,
/// and the completion of an exercise across partial runs.
/// </summary>
public class LabScenariosTests
{
    // The exercise as delivered: the setup and scenarios 2 and 3 to write, scenario 1 provided (the same code in the solution).
    private const string Start = """
        ScenarioSelection scenarioSelection = ScenarioSelection.FromArgs(args);

        #region Setup: Configuration
        var settings = Load();
        // TODO 1: create the client
        #endregion

        #region Scenario 1: Fill the store
        if (ShouldRunScenario(1)) { Fill(); }
        #endregion

        #region Scenario 2: Search - without an agent
        // Requires scenario 1: the store is in memory.
        if (ShouldRunScenario(2))
        {
            Console.WriteLine("=== Scenario 2: Search ===");
            // TODO 2: search
            #region Nested
            // TODO 3: still scenario 2
            #endregion
        }
        #endregion

        #region Scenario 3: Agent
        if (ShouldRunScenario(3)) { }
        #endregion
        """;

    private static readonly string Solution = Start
        .Replace("// TODO 1: create the client", "var client = Create(settings);")
        .Replace("// TODO 2: search", "Search();")
        .Replace("if (ShouldRunScenario(3)) { }", "if (ShouldRunScenario(3)) { Run(); }");

    private static readonly DeliveredExercise Delivered = LabScenarios.CreateDelivered(Start, Solution);

    // A learner who wrote the setup and scenario 3, and only removed the TODO comments of scenario 2 (and re-indented it).
    private static readonly string Learner = Start
        .Replace("// TODO 1: create the client", "var client = new MyClient(settings);")
        .Replace("if (ShouldRunScenario(3)) { }", "if (ShouldRunScenario(3)) { MyAgent(); }")
        .Replace("    // TODO 2: search\n", "")
        .Replace("    Console.WriteLine", "        Console.WriteLine");

    private static readonly IReadOnlyList<LabScenario> Scenarios = LabScenarios.Parse(Learner, Delivered);

    [Fact]
    public void Reads_the_scenario_regions_and_what_they_require()
    {
        IReadOnlyList<LabScenario> scenarios = LabScenarios.Parse(Start);

        Assert.Equal([(1, "Fill the store", ""), (2, "Search - without an agent", "1"), (3, "Agent", "")],
            scenarios.Select(s => (s.Number, s.Title, string.Join(',', s.Requires))));
        // Without the delivered exercise to compare to, nothing tells a delivered scenario: every scenario counts as implemented.
        Assert.All(scenarios, s => Assert.True(s.Implemented));
    }

    [Fact]
    public void The_delivered_exercise_has_no_scenario_implemented_and_the_solution_has_them_all()
    {
        Assert.Equal((false, false, true), (Delivered.Setup.Provided, Delivered.Scenarios[2].Provided, Delivered.Scenarios[1].Provided));
        Assert.All(LabScenarios.Parse(Start, Delivered), s => Assert.False(s.Implemented));
        Assert.All(LabScenarios.Parse(Solution, Delivered), s => Assert.True(s.Implemented));
    }

    [Fact]
    public void A_scenario_is_implemented_when_its_code_changes_never_when_only_its_comments_or_layout_do()
    {
        Assert.Equal([true, false, true], Scenarios.Select(s => s.Implemented));

        // The setup still delivered: no scenario is implemented, even the one whose code was written.
        IReadOnlyList<LabScenario> withoutSetup = LabScenarios.Parse(Learner.Replace("var client = new MyClient(settings);", "// TODO 1: create the client"), Delivered);
        Assert.All(withoutSetup, s => Assert.Equal((false, s.Number != 2), (s.SetupImplemented, s.CodeImplemented)));
    }

    [Fact]
    public void The_delivered_exercise_round_trips_through_its_file()
    {
        DeliveredExercise? read = LabScenarios.ReadDelivered(LabScenarios.WriteDelivered(Delivered));

        Assert.NotNull(read);
        Assert.Equal(Delivered.Setup, read.Setup);
        Assert.Equal(Delivered.Scenarios.OrderBy(s => s.Key), read.Scenarios.OrderBy(s => s.Key));
    }

    [Theory]
    [InlineData(new string[0], "1,3")] // the implemented scenarios
    [InlineData(new[] { "--scenarios", "implemented" }, "1,3")]
    [InlineData(new[] { "--scenarios", "3" }, "3")]
    [InlineData(new[] { "--scenario=2" }, "1,2")] // with the scenario it requires; not implemented but asked for
    [InlineData(new[] { "--serve", "--scenarios", "3, 1" }, "1,3")] // the arguments of the lab itself are left alone
    [InlineData(new[] { "--scenarios", "all" }, "1,2,3")]
    public void Plans_what_runs_from_the_command_line(string[] args, string expected)
    {
        ScenarioRequest request = LabScenarios.ParseArguments(args, out bool list, out string? error);
        ScenarioPlan plan = LabScenarios.Plan(Scenarios, request);

        Assert.Null(error);
        Assert.False(list);
        Assert.Equal(expected, string.Join(',', plan.Selected));
        // The dashboard passes the plan with the same option: the program reads back exactly the same scenarios.
        ScenarioRequest roundTrip = LabScenarios.ParseArguments([.. LabScenarios.FormatArguments(plan.Selected)], out _, out _);
        Assert.Equal(plan.Selected, LabScenarios.Plan(Scenarios, roundTrip).Selected);
    }

    [Fact]
    public void A_not_implemented_scenario_never_runs_by_default_nor_a_scenario_that_requires_it()
    {
        IReadOnlyList<LabScenario> scenarios =
            [new(1, "Fill", []) { CodeImplemented = false }, new(2, "Search", [1]), new(3, "Agent", [])];

        ScenarioPlan byDefault = LabScenarios.Plan(scenarios, ScenarioRequest.Implemented);
        ScenarioPlan asked = LabScenarios.Plan(scenarios, new ScenarioRequest([2]));

        Assert.Equal([3], byDefault.Selected);
        Assert.Equal([1, 2], byDefault.LeftOut);
        Assert.Equal([1, 2], asked.Selected);
        Assert.Equal([1], asked.AddedAsRequired);
        Assert.Equal([1], asked.NotImplemented);
    }

    [Theory]
    [InlineData(new[] { "--scenarios" }, "needs a value")]
    [InlineData(new[] { "--scenarios", "1,x" }, "not a scenario number")]
    [InlineData(new[] { "--scenarios", "0" }, "not a scenario number")]
    public void Refuses_a_malformed_selection(string[] args, string error)
    {
        LabScenarios.ParseArguments(args, out _, out string? message);

        Assert.Contains(error, message);
    }

    [Fact]
    public void Reports_the_numbers_the_lab_does_not_have()
    {
        Assert.Equal([7], LabScenarios.Plan(Scenarios, new ScenarioRequest([3, 7])).Unknown);
    }

    [Fact]
    public void Splits_the_output_on_the_lines_of_the_selection_and_leaves_them_out()
    {
        string marker = LabScenarios.FormatMarker(Scenarios[1]);
        Assert.True(LabScenarios.TryParseMarker(marker, out int number));
        Assert.Equal(2, number);
        Assert.False(LabScenarios.TryParseMarker("=== Scenario 2: Search ===", out _));
        Assert.All(LabScenarios.DescribePlan(Scenarios, ScenarioRequest.Implemented, LabScenarios.Plan(Scenarios, ScenarioRequest.Implemented)),
            line => Assert.True(LabScenarios.IsSelectionLine(line)));

        RunOutput output = OutputAnalyzer.Split([">>> Scenarios to run: 1, 2", "Endpoint: x", ">>> Running scenario 1: Fill", "filled", marker, "found"]);

        Assert.Equal("Endpoint: x\nfilled\nfound", output.Whole);
        Assert.Equal(("filled", "found"), (output.Scenarios[1], output.Scenarios[2]));
        Assert.Equal([1, 2], output.Started);
    }

    [Fact]
    public void A_check_of_a_scenario_reads_its_output_only_and_needs_no_header()
    {
        LabExpectation[] expectations = [new("scenario1", "Scenario 1 answered", @"(?i)\bParis\b"), new("scenario2", "Scenario 2 answered", @"(?i)\bParis\b")];
        // No "=== Scenario N" header at all; the answer of scenario 1 only.
        string[] stdout = [">>> Running scenario 1: Capital", "The capital of France is Paris.", ">>> Running scenario 2: Surface", "I don't know."];

        Assert.Equal([true, false], OutputAnalyzer.EvaluateExpectations(expectations, stdout).Select(c => c.Passed));
    }

    [Fact]
    public void Each_scenario_gets_its_checks_and_the_error_of_the_scenario_the_program_stopped_in()
    {
        LabExpectation[] expectations =
        [
            new("config", "Configuration loaded", "^Endpoint: "),
            new("scenario1", "Scenario 1 answered", "^filled"),
            new("scenario2-usage", "Scenario 2 tokens", "Input tokens"),
            new("comparison", "1 vs 2", "^Comparison") { Scenarios = [1, 2] },
            new("scenario3", "Scenario 3 answered", "^answered"),
        ];
        string[] stdout = ["Endpoint: x", ">>> Running scenario 1: Fill the store", "filled", ">>> Running scenario 3: Agent"];

        (IReadOnlyList<ExpectationResult> checks, IReadOnlyList<ScenarioResult> scenarios) = OutputAnalyzer.EvaluateScenarios(
            expectations, new ScenarioRun(Scenarios, [1, 3]), OutputAnalyzer.Split(stdout), ScenarioEnd.Stopped,
            ["Unhandled exception. System.InvalidOperationException: boom"]);

        // Scenario 2 was not selected: neither its checks nor the comparison of 1 and 2 are evaluated.
        Assert.Equal(["config", "scenario1", "scenario3"], checks.Select(c => c.Id));
        Assert.Equal([ScenarioStatus.Passed, ScenarioStatus.Skipped, ScenarioStatus.Failed], scenarios.Select(s => s.Status));
        Assert.Equal(["scenario1"], scenarios[0].Checks);
        Assert.Empty(scenarios[0].Errors);
        Assert.Equal(["Unhandled exception. System.InvalidOperationException: boom"], scenarios[2].Errors);
    }

    [Fact]
    public void Scenarios_passed_in_several_runs_complete_the_exercise()
    {
        DateTimeOffset t0 = DateTimeOffset.Now;
        RunRecord Run(int minute, RunStatus status, params (int Number, ScenarioStatus Status)[] scenarios) => new()
        {
            RunId = $"r{minute}", LabId = "lab", Target = RunTarget.Start, Status = status, Summary = "", StartedAt = t0.AddMinutes(minute),
            DurationMs = 1, Scenarios = scenarios.Select(s => new ScenarioResult(s.Number, $"S{s.Number}", s.Status)).ToList(),
        };

        // Newest first, as the history keeps them.
        RunRecord[] partial =
        [
            Run(3, RunStatus.Passed, (1, ScenarioStatus.Skipped), (2, ScenarioStatus.Passed)),
            Run(2, RunStatus.Cancelled, (1, ScenarioStatus.Skipped), (2, ScenarioStatus.Passed)),
            Run(1, RunStatus.Failed, (1, ScenarioStatus.Passed), (2, ScenarioStatus.Failed)),
        ];

        Assert.Equal("r3", ExerciseProgress.CompletingRun(partial)?.RunId);
        Assert.Null(ExerciseProgress.CompletingRun(partial[1..]));
        Assert.Equal(ScenarioStatus.Passed, ExerciseProgress.LastScenarioResults(partial, RunTarget.Start)[2].Status);
        Assert.Equal("r1", ExerciseProgress.LastScenarioResults(partial, RunTarget.Start)[1].Run.RunId);
    }

    [Fact]
    public void Every_lab_of_the_catalog_has_up_to_date_fingerprints_and_the_same_scenarios_in_its_exercise_and_its_solution()
    {
        LabCatalog catalog = LabCatalogTests.LoadRealCatalog();
        foreach (LabDefinition lab in catalog.Labs)
        {
            // Regenerate with `dotnet run -- exercise-fingerprints` (Dashboard/LabDashboard) after changing a Start or a Solution.
            string fingerprints = ExerciseFingerprints.FilePath(catalog, lab);
            Assert.True(File.Exists(fingerprints), $"{lab.Id}: {LabScenarios.FingerprintFile} is missing");
            Assert.True(File.ReadAllText(fingerprints) == ExerciseFingerprints.Generate(catalog, lab), $"{lab.Id}: {LabScenarios.FingerprintFile} is out of date");

            IReadOnlyList<LabScenario> start = catalog.Scenarios(lab, RunTarget.Start);
            IReadOnlyList<LabScenario> solution = catalog.Scenarios(lab, RunTarget.Solution);
            Assert.True(solution.Count > 0, $"{lab.Id}: no scenario region");
            Assert.Equal(solution.Select(s => (s.Number, s.Title, string.Join(',', s.Requires))), start.Select(s => (s.Number, s.Title, string.Join(',', s.Requires))));
            Assert.All(solution, s => Assert.True(s.Implemented, $"{lab.Id}: scenario {s.Number} of the solution counts as not implemented"));
            // The delivered exercise: no scenario is implemented, so none runs by default.
            Assert.All(start, s => Assert.False(s.Implemented, $"{lab.Id}: scenario {s.Number} of the exercise counts as implemented"));
            // A check of a scenario the program does not have would never be evaluated.
            Assert.All(lab.Expectations, e => Assert.All(e.ScenarioNumbers(), n => Assert.Contains(n, solution.Select(s => s.Number))));
        }
    }
}
