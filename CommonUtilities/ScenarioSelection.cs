using System.Runtime.CompilerServices;

namespace CommonUtilities;

/// <summary>
/// The scenarios a lab runs, chosen on the command line (the rules are in <see cref="LabScenarios"/>):
/// <code>
/// dotnet run                          the implemented scenarios (a scenario whose code is still the delivered one is left out)
/// dotnet run -- --scenarios 2         scenario 2 (and the scenarios it requires)
/// dotnet run -- --scenarios 1,3       scenarios 1 and 3
/// dotnet run -- --scenarios all       every scenario, implemented or not
/// dotnet run -- --list-scenarios      the scenarios of the lab and their state, then exit
/// </code>
/// The Lab Bench dashboard runs the lab with the same option.
/// </summary>
public sealed class ScenarioSelection
{
    private readonly Dictionary<int, LabScenario> _scenarios;
    private readonly HashSet<int> _selected;
    private readonly bool _runEverything;
    private readonly IReadOnlyList<string> _planLines;
    private bool _announced;

    private ScenarioSelection(IReadOnlyList<LabScenario> scenarios, ScenarioRequest request, ScenarioPlan plan)
    {
        _scenarios = scenarios.ToDictionary(s => s.Number);
        _selected = [.. plan.Selected];
        // Program.cs not found (the program was moved away from its sources): nothing to decide from, run what is asked or everything.
        _runEverything = scenarios.Count == 0 && request.Numbers is null;
        _planLines = LabScenarios.DescribePlan(scenarios, request, plan);
    }

    /// <summary>Reads the selection from the arguments of the program. <paramref name="programFile"/> is filled by the compiler: leave it out.</summary>
    public static ScenarioSelection FromArgs(string[] args, [CallerFilePath] string programFile = "")
    {
        IReadOnlyList<LabScenario> scenarios = LabScenarios.ReadFile(programFile);
        ScenarioRequest request = LabScenarios.ParseArguments(args, out bool list, out string? error);
        ScenarioPlan plan = LabScenarios.Plan(scenarios, request);
        if (error is null && plan.Unknown.Count > 0)
        {
            error = $"Unknown scenario {string.Join(", ", plan.Unknown)}. The scenarios of this lab are {string.Join(", ", scenarios.Select(s => s.Number))} (--list-scenarios).";
        }

        if (error is not null)
        {
            ColoredConsole.WriteErrorLine(error);
            Environment.Exit(2);
        }

        if (list)
        {
            foreach (string line in LabScenarios.Describe(scenarios))
            {
                Console.WriteLine(line);
            }

            Environment.Exit(0);
        }

        return new ScenarioSelection(scenarios, request, plan);
    }

    /// <summary>
    /// Whether the scenario runs. The first call prints what runs; a scenario that runs prints a line when it starts,
    /// so that its output, its results and its errors can be told apart from those of the other scenarios.
    /// </summary>
    public bool ShouldRun(int scenario)
    {
        if (!_announced)
        {
            _announced = true;
            foreach (string line in _planLines)
            {
                ColoredConsole.WriteSecondaryLogLine(line);
            }
        }

        if (!_runEverything && !_selected.Contains(scenario))
        {
            return false;
        }

        if (_scenarios.TryGetValue(scenario, out LabScenario? declared))
        {
            ColoredConsole.WriteSecondaryLogLine(LabScenarios.FormatMarker(declared));
        }

        return true;
    }
}
