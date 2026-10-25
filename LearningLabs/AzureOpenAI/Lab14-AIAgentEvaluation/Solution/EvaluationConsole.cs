using System.Globalization;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI.Evaluation;
using CommonUtilities;

namespace AIAgentEvaluation;

/// <summary>
/// Console output helpers for evaluation results, so that Program.cs focuses on the evaluation API.
/// Each result item holds one metric per check or evaluator: a BooleanMetric for a local check, a NumericMetric
/// (score) for an LLM judge, each with a reason and an interpretation (failed or not).
/// </summary>
public static class EvaluationConsole
{
    /// <summary>
    /// Displays the results of an evaluation run: for each question, each metric with its value and its reason,
    /// then the summary (questions where every metric passed / total).
    /// </summary>
    public static void WriteResults(AgentEvaluationResults results)
    {
        ColoredConsole.WritePrimaryLogLine($"Evaluator: {results.ProviderName}");
        List<EvaluationResult> items = [.. results.Items];
        for (int i = 0; i < items.Count; i++)
        {
            // InputItems[i] is the question and the answer that produced Items[i]
            EvalItem? input = results.InputItems?.ElementAtOrDefault(i);
            ColoredConsole.WriteUserLine($"Q{i + 1}: {input?.Query}" + (input?.ExpectedOutput is null ? string.Empty : $"  (expected: {input.ExpectedOutput})"));
            ColoredConsole.WriteAssistantLine($"  Answer: {OneLine(input?.Response)}");
            foreach (EvaluationMetric metric in items[i].Metrics.Values)
            {
                WriteMetric(metric);
            }
        }

        ColoredConsole.WritePrimaryLogLine($"Summary: {results.Passed}/{results.Total} questions passed");
    }

    /// <summary>
    /// Displays the comparison of two evaluation runs of the same dataset, question by question,
    /// with the checks that failed.
    /// </summary>
    public static void WriteComparison(string referenceName, AgentEvaluationResults reference, string candidateName, AgentEvaluationResults candidate)
    {
        ColoredConsole.WritePrimaryLogLine($"{"Question",-48} {referenceName,-28} {candidateName}");
        List<EvaluationResult> referenceItems = [.. reference.Items];
        List<EvaluationResult> candidateItems = [.. candidate.Items];
        for (int i = 0; i < referenceItems.Count; i++)
        {
            string question = Truncate($"Q{i + 1}: {reference.InputItems?.ElementAtOrDefault(i)?.Query}", 48);
            ColoredConsole.WriteSecondaryLogLine($"{question,-48} {Status(referenceItems[i]),-28} {Status(candidateItems[i])}");
        }
    }

    /// <summary>True when no metric of the item is interpreted as failed.</summary>
    public static bool Passed(EvaluationResult item) => item.Metrics.Values.All(metric => metric.Interpretation?.Failed != true);

    private static void WriteMetric(EvaluationMetric metric)
    {
        string value = metric switch
        {
            BooleanMetric boolean => boolean.Value == true ? "pass" : "fail",
            NumericMetric numeric => numeric.Value is double score ? $"score {score.ToString("0.#", CultureInfo.InvariantCulture)}" : "no score",
            _ => metric.Interpretation?.Failed == true ? "fail" : "pass"
        };
        string line = $"  [{value}] {metric.Name}: {OneLine(metric.Reason)}";
        if (metric.Interpretation?.Failed == true)
        {
            ColoredConsole.WriteWarningLine(line);
        }
        else
        {
            ColoredConsole.WriteSecondaryLogLine(line);
        }
    }

    private static string Status(EvaluationResult item) => Passed(item)
        ? "pass"
        : $"FAIL ({string.Join(", ", item.Metrics.Values.Where(metric => metric.Interpretation?.Failed == true).Select(metric => metric.Name))})";

    private static string OneLine(string? text) =>
        string.Join(' ', (text ?? string.Empty).Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));

    private static string Truncate(string text, int length) => text.Length <= length ? text : text[..(length - 3)] + "...";
}
