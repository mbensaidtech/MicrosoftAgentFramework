using System.Text.RegularExpressions;
using LabDashboard.Catalog;

namespace LabDashboard.Execution;

/// <summary>Pure functions that interpret the output of a lab run.</summary>
public static partial class OutputAnalyzer
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    [GeneratedRegex(@"^\s*(?<kind>input|output|reasoning|total)\s+tokens\b[^:\n]*:\s*(?<value>\d+)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex TokenLine();

    [GeneratedRegex(@"^=== (?<scenario>Scenario \d+)\b")]
    private static partial Regex ScenarioHeader();

    [GeneratedRegex(@"\b(?<severity>warning|error)\s+(?<code>[A-Z]+\d+)\s*:\s*(?<message>.*?)(\s+\[[^\]]+\])?$")]
    private static partial Regex BuildDiagnostic();

    /// <summary>
    /// Parses the token usage blocks printed by the exercise; returns null when there are none.
    /// A block printed after a "=== Scenario N: ... ===" header is labelled "Scenario N · heading".
    /// </summary>
    public static TokenUsageSummary? ParseTokenUsage(IEnumerable<string> stdoutLines)
    {
        List<TokenUsageReport> reports = [];
        string? heading = null;
        string? scenario = null;
        TokenUsageReport? current = null;

        foreach (string line in stdoutLines)
        {
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

    public static IReadOnlyList<ExpectationResult> EvaluateExpectations(IEnumerable<LabExpectation> expectations, IEnumerable<string> stdoutLines)
    {
        string output = string.Join('\n', stdoutLines);
        return expectations
            .Select(e => new ExpectationResult(e.Id, e.Description, IsMatch(e.Pattern, output)))
            .ToList();
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
