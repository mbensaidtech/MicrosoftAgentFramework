using System.Text.RegularExpressions;
using CommonUtilities;
using LabDashboard.Catalog;

namespace LabDashboard.Execution;

/// <summary>How the program of a run ended, for the scenario it was running.</summary>
public enum ScenarioEnd
{
    /// <summary>The program exited with code 0.</summary>
    Exited,

    /// <summary>It failed (exit code, timeout, companion): the scenario it was running failed.</summary>
    Stopped,

    /// <summary>The learner cancelled the run: the scenario it was running did not finish.</summary>
    Cancelled
}

/// <summary>
/// The standard output of a run: whole, and split by scenario. A scenario's output goes from the line the scenario selection prints
/// when it starts (<see cref="LabScenarios.MarkerPrefix"/>, printed by CommonUtilities, never by the learner) to the next one; an output
/// without these lines (a program written before the selection existed) is split on the "=== Scenario N: ... ===" headers instead.
/// The lines of the selection itself are in neither.
/// </summary>
public sealed record RunOutput(string Whole, IReadOnlyDictionary<int, string> Scenarios, IReadOnlyList<int> Started);

/// <summary>Pure functions that interpret the output of a lab run.</summary>
public static partial class OutputAnalyzer
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    [GeneratedRegex(@"^\s*(?<kind>input|output|reasoning|total)\s+tokens\b[^:\n]*:\s*(?<value>\d+)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex TokenLine();

    [GeneratedRegex(@"^=== (?<scenario>Scenario (?<number>\d+))\b")]
    private static partial Regex ScenarioHeader();

    [GeneratedRegex(@"\b(?<severity>warning|error)\s+(?<code>[A-Z]+\d+)\s*:\s*(?<message>.*?)(\s+\[[^\]]+\])?$")]
    private static partial Regex BuildDiagnostic();

    /// <summary>
    /// Parses the token usage blocks printed by the exercise; returns null when there are none.
    /// A block printed in a scenario (after the line of its start, or after its "=== Scenario N: ... ===" header) is labelled "Scenario N · heading".
    /// </summary>
    public static TokenUsageSummary? ParseTokenUsage(IEnumerable<string> stdoutLines)
    {
        List<TokenUsageReport> reports = [];
        string? heading = null;
        string? scenario = null;
        TokenUsageReport? current = null;

        foreach (string line in stdoutLines)
        {
            if (LabScenarios.TryParseMarker(line, out int started))
            {
                scenario = $"Scenario {started}";
                continue;
            }

            Match match = TokenLine().Match(line);
            if (!match.Success)
            {
                if (ScenarioHeader().Match(line) is { Success: true } header)
                {
                    scenario = header.Groups["scenario"].Value;
                }
                else if (line.Contains("token usage", StringComparison.OrdinalIgnoreCase))
                {
                    heading = line.Trim().TrimEnd(':').Trim();
                }

                continue;
            }

            long value = long.Parse(match.Groups["value"].Value);
            string kind = match.Groups["kind"].Value.ToLowerInvariant();

            if (kind == "input" || current is null)
            {
                if (current is not null)
                {
                    reports.Add(current);
                }

                string label = heading ?? $"Report {reports.Count + 1}";
                current = new TokenUsageReport(scenario is null ? label : $"{scenario} · {label}", null, null, null, null);
                heading = null;
            }

            current = kind switch
            {
                "input" => current with { Input = value },
                "output" => current with { Output = value },
                "reasoning" => current with { Reasoning = value },
                _ => current with { Total = value },
            };
        }

        if (current is not null)
        {
            reports.Add(current);
        }

        if (reports.Count == 0)
        {
            return null;
        }

        return new TokenUsageSummary(
            reports,
            Sum(reports.Select(r => r.Input)),
            Sum(reports.Select(r => r.Output)),
            Sum(reports.Select(r => r.Reasoning)),
            Sum(reports.Select(r => r.Total)));
    }

    /// <summary>
    /// Splits the output of the lab by scenario (see <see cref="RunOutput"/>). The output of a companion client (always a reference
    /// solution) is only part of the whole output: its own scenario lines are not those of the lab.
    /// </summary>
    public static RunOutput Split(IReadOnlyList<string> labStdout, IReadOnlyList<string>? companionStdout = null)
    {
        bool markers = labStdout.Any(line => LabScenarios.TryParseMarker(line, out _));
        Dictionary<int, List<string>> scenarios = [];
        List<int> started = [];
        List<string>? current = null;
        foreach (string line in labStdout)
        {
            if (markers && LabScenarios.TryParseMarker(line, out int number))
            {
                current = scenarios.TryGetValue(number, out List<string>? lines) ? lines : scenarios[number] = [];
                if (!started.Contains(number))
                {
                    started.Add(number);
                }

                continue;
            }

            if (LabScenarios.IsSelectionLine(line))
            {
                continue;
            }

            if (!markers && ScenarioHeader().Match(line) is { Success: true } header)
            {
                int headerNumber = int.Parse(header.Groups["number"].Value);
                current = scenarios.TryGetValue(headerNumber, out List<string>? lines) ? lines : scenarios[headerNumber] = [];
            }

            current?.Add(line);
        }

        string whole = string.Join('\n', labStdout.Concat(companionStdout ?? []).Where(line => !LabScenarios.IsSelectionLine(line)));
        return new RunOutput(whole, scenarios.ToDictionary(s => s.Key, s => string.Join('\n', s.Value)), started);
    }

    public static IReadOnlyList<ExpectationResult> EvaluateExpectations(IEnumerable<LabExpectation> expectations, IEnumerable<string> stdoutLines) =>
        EvaluateExpectations(expectations, Split(stdoutLines.ToList()));

    /// <summary>
    /// Evaluates each check on its part of the output: a check whose id names its scenario (<c>scenario2-usage</c>) on the output of
    /// that scenario only, so that it needs neither a header nor anything else around the result; a check that lists its scenarios
    /// (it compares several of them, or reads a companion) or names none (the configuration) on the whole output.
    /// </summary>
    public static IReadOnlyList<ExpectationResult> EvaluateExpectations(IEnumerable<LabExpectation> expectations, RunOutput output) =>
        expectations
            .Select(e => new ExpectationResult(e.Id, e.Description, IsMatch(e.Pattern, Scope(e, output))))
            .ToList();

    private static string Scope(LabExpectation expectation, RunOutput output) =>
        expectation.Scenarios is null && expectation.ScenarioNumbers() is [int number]
            ? output.Scenarios.GetValueOrDefault(number, "")
            : output.Whole;

    /// <summary>
    /// The checks and the scenario results of a run with a scenario selection. The program prints a line when a scenario starts
    /// (<see cref="RunOutput.Started"/>): a check is evaluated when every scenario it needs ran, on its part of the output
    /// (<see cref="EvaluateExpectations(IEnumerable{LabExpectation}, RunOutput)"/>), and belongs to the last of them
    /// (a check of no scenario belongs to the whole run).
    /// A selected scenario passes when it ran to its end, its checks passed and the checks of the whole run passed; when the program
    /// stopped (<paramref name="end"/>), the scenario it was running gets the <paramref name="errors"/>, and the next ones did not run.
    /// </summary>
    public static (IReadOnlyList<ExpectationResult> Checks, IReadOnlyList<ScenarioResult> Scenarios) EvaluateScenarios(
        IReadOnlyList<LabExpectation> expectations, ScenarioRun selection, RunOutput output, ScenarioEnd end, IReadOnlyList<string> errors)
    {
        HashSet<int> selected = [.. selection.Selected];
        List<int> started = output.Started.Where(selected.Contains).ToList();
        // A program that prints no scenario line (written before the selection existed) and exits normally ran what it ran: judge it by the checks.
        HashSet<int> ran = started.Count == 0 && end == ScenarioEnd.Exited ? selected : [.. started];
        int? stoppedIn = end != ScenarioEnd.Exited && started.Count > 0 ? started[^1] : null;

        List<(LabExpectation Expectation, int? Owner)> evaluated = expectations
            .Select(e => (Expectation: e, Numbers: e.ScenarioNumbers()))
            .Where(e => e.Numbers.All(ran.Contains))
            .Select(e => (e.Expectation, e.Numbers.Count == 0 ? (int?)null : e.Numbers.Max()))
            .ToList();
        IReadOnlyList<ExpectationResult> checks = EvaluateExpectations(evaluated.Select(e => e.Expectation), output);
        bool runChecksPassed = checks.Where((_, i) => evaluated[i].Owner is null).All(c => c.Passed);

        List<ScenarioResult> scenarios = selection.Scenarios.Select(scenario =>
        {
            int number = scenario.Number;
            if (!selected.Contains(number))
            {
                return new ScenarioResult(number, scenario.Title, ScenarioStatus.Skipped);
            }

            List<ExpectationResult> own = checks.Where((_, i) => evaluated[i].Owner == number).ToList();
            ScenarioStatus status = !ran.Contains(number) || (stoppedIn == number && end == ScenarioEnd.Cancelled) ? ScenarioStatus.NotRun
                : stoppedIn == number || !runChecksPassed || own.Any(c => !c.Passed) ? ScenarioStatus.Failed
                : ScenarioStatus.Passed;
            return new ScenarioResult(number, scenario.Title, status)
            {
                Checks = own.Select(c => c.Id).ToList(),
                Errors = stoppedIn == number && end == ScenarioEnd.Stopped ? errors : [],
            };
        }).ToList();

        return (checks, scenarios);
    }

    /// <summary>Distinct build diagnostics of the given severity, without the trailing "[project]" suffix.</summary>
    public static IReadOnlyList<string> BuildDiagnostics(IEnumerable<string> buildLines, string severity, int max = 20) =>
        buildLines
            .Select(line => BuildDiagnostic().Match(line))
            .Where(m => m.Success && m.Groups["severity"].Value == severity)
            .Select(m => $"{m.Groups["code"].Value}: {m.Groups["message"].Value}")
            .Distinct(StringComparer.Ordinal)
            .Take(max)
            .ToList();

    /// <summary>The stderr lines worth showing, or the exception line when there is one.</summary>
    public static IReadOnlyList<string> RuntimeErrors(IEnumerable<string> stderrLines, int max = 20) =>
        stderrLines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Take(max)
            .ToList();

    private static bool IsMatch(string pattern, string input)
    {
        try
        {
            return Regex.IsMatch(input, pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant, MatchTimeout);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static long? Sum(IEnumerable<long?> values)
    {
        long?[] known = values.Where(v => v.HasValue).ToArray();
        return known.Length == 0 ? null : known.Sum();
    }
}
