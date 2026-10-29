using CommonUtilities;
using LabDashboard.Catalog;
using LabDashboard.Execution;
using LabDashboard.History;
using LabDashboard.Settings;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;

namespace LabDashboard.Tests;

/// <summary>
/// End-to-end: a throwaway lab chooses its scenarios with the real <see cref="ScenarioSelection"/> of CommonUtilities, and the dashboard
/// runs it with a selection: only the selected scenarios run, only their checks are evaluated, and the error of a scenario that throws
/// is attached to it.
/// </summary>
public sealed class ScenarioRunTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("labbench-scenarios-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task The_dashboard_runs_the_selected_scenarios_only_and_attaches_the_results_and_errors_to_each_one()
    {
        LabCatalog catalog = CreateLab();
        LabDefinition lab = catalog.Labs[0];
        IReadOnlyList<LabScenario> scenarios = catalog.Scenarios(lab, RunTarget.Solution);
        Assert.Equal([1, 2, 3], scenarios.Select(s => s.Number));

        DashboardOptions options = new() { DataDirectory = "data" };
        RunHistoryStore history = new(options, new HostingEnvironment { ContentRootPath = _root }, NullLogger<RunHistoryStore>.Instance);
        AzureOpenAISettingsStore settings = new(Path.Combine(_root, "secrets.json"), "test", [], () => new Dictionary<string, string>());
        LabRunner runner = new(catalog, history, settings, options, NullLogger<LabRunner>.Instance);

        // Scenario 2 only: the program receives "-- --scenarios 2", scenarios 1 and 3 do not run, their checks are not evaluated.
        RunRecord only2 = await RunAsync(runner, lab, new ScenarioRun(scenarios, [2]));
        Assert.Equal(RunStatus.Passed, only2.Status);
        Assert.Contains(only2.Log, line => line.Stream == "system" && line.Text.EndsWith("--no-build -- --scenarios 2", StringComparison.Ordinal));
        Assert.Contains(only2.Log, line => line.Text == ">>> Running scenario 2: Second");
        Assert.DoesNotContain(only2.Log, line => line.Text.StartsWith("=== Scenario 1", StringComparison.Ordinal));
        Assert.Equal(["config", "scenario2"], only2.Checks.Select(c => c.Id));
        Assert.Equal([ScenarioStatus.Skipped, ScenarioStatus.Passed, ScenarioStatus.Skipped], only2.Scenarios!.Select(s => s.Status));

        // Scenarios 1 and 3, and scenario 3 throws: scenario 1 passed, scenario 3 failed with the exception, scenario 2 was not selected.
        RunRecord oneAndThree = await RunAsync(runner, lab, new ScenarioRun(scenarios, [1, 3]));
        Assert.Equal((RunStatus.Failed, "run"), (oneAndThree.Status, oneAndThree.FailureStage));
        Assert.Equal([ScenarioStatus.Passed, ScenarioStatus.Skipped, ScenarioStatus.Failed], oneAndThree.Scenarios!.Select(s => s.Status));
        Assert.Empty(oneAndThree.Scenarios![0].Errors);
        Assert.Contains(oneAndThree.Scenarios![2].Errors, error => error.Contains("scenario three failed", StringComparison.Ordinal));

        // Scenarios 1 and 2 passed across the two runs, scenario 3 never did: the exercise is not completed yet.
        Assert.Null(ExerciseProgress.CompletingRun(history.ForLab(lab.Id).Select(r => r with { Target = RunTarget.Start })));
    }

    private static async Task<RunRecord> RunAsync(LabRunner runner, LabDefinition lab, ScenarioRun selection)
    {
        Assert.True(runner.TryStart(lab, RunTarget.Solution, out LabRun run, selection));
        using CancellationTokenSource timeout = new(TimeSpan.FromMinutes(3));
        await foreach (RunEvent _ in run.ReadEventsAsync(timeout.Token))
        {
        }

        return Assert.IsType<RunRecord>(run.Result);
    }

    private LabCatalog CreateLab()
    {
        // The selection code of the labs, compiled from its sources (not the CommonUtilities project: its obj/ is shared by the labs).
        string commonUtilities = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "CommonUtilities"));
        string project = Path.Combine(_root, "ScenarioLab", "Solution");
        Directory.CreateDirectory(project);
        File.WriteAllText(Path.Combine(project, "ScenarioLab.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="{commonUtilities}/ColoredConsole.cs;{commonUtilities}/LabScenarios.cs;{commonUtilities}/ScenarioSelection.cs" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(project, "Program.cs"), """
            using CommonUtilities;

            ScenarioSelection scenarioSelection = ScenarioSelection.FromArgs(args);
            bool ShouldRunScenario(int scenario) => scenarioSelection.ShouldRun(scenario);

            Console.WriteLine("Endpoint: test");

            #region Scenario 1: First
            if (ShouldRunScenario(1))
            {
                Console.WriteLine("=== Scenario 1: First ===");
                Console.WriteLine("answer one");
            }
            #endregion

            #region Scenario 2: Second
            if (ShouldRunScenario(2))
            {
                Console.WriteLine("=== Scenario 2: Second ===");
                Console.WriteLine("answer two");
            }
            #endregion

            #region Scenario 3: Third
            if (ShouldRunScenario(3))
            {
                Console.WriteLine("=== Scenario 3: Third ===");
                throw new InvalidOperationException("scenario three failed");
            }
            #endregion
            """);

        string catalogFile = Path.Combine(_root, "labs.json");
        File.WriteAllText(catalogFile, """
            { "labs": [ { "id": "scenarios", "number": "98", "track": "t", "title": "Scenarios", "summary": "s", "level": "l",
                          "path": "ScenarioLab", "startProject": "Solution/ScenarioLab.csproj", "solutionProject": "Solution/ScenarioLab.csproj",
                          "timeoutSeconds": 170,
                          "expectations": [
                            { "id": "config", "description": "Configuration", "pattern": "^Endpoint: \\S+" },
                            { "id": "scenario1", "description": "Scenario 1", "pattern": "^=== Scenario 1:[^\\n]*===\\n\\S" },
                            { "id": "scenario2", "description": "Scenario 2", "pattern": "^=== Scenario 2:[^\\n]*===\\n\\S" },
                            { "id": "scenario3", "description": "Scenario 3", "pattern": "^=== Scenario 3:[^\\n]*===\\n\\S" } ] } ] }
            """);

        return LabCatalog.Load(catalogFile, _root);
    }
}
