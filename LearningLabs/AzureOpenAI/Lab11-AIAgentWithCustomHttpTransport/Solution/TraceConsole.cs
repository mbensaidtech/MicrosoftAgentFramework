using System.Diagnostics;
using CommonUtilities;

namespace AIAgentWithCustomHttpTransport;

/// <summary>
/// Console output helper for the spans recorded by OpenTelemetry (scenario 3), so that Program.cs focuses on the
/// instrumentation. It never prints a prompt or an answer: only span names, durations and token counts.
/// </summary>
public static class TraceConsole
{
    /// <summary>
    /// Writes the spans as a tree: each span under its parent (ParentSpanId), in the order they started.
    /// The token counts come from the gen_ai.usage.* attributes of the OpenTelemetry GenAI semantic conventions.
    /// </summary>
    public static void WriteSpanTree(IReadOnlyCollection<Activity> spans)
    {
        ColoredConsole.WritePrimaryLogLine($"Spans recorded by OpenTelemetry: {spans.Count}");
        HashSet<ActivitySpanId> spanIds = [.. spans.Select(span => span.SpanId)];
        foreach (Activity root in spans.Where(span => !spanIds.Contains(span.ParentSpanId)).OrderBy(span => span.StartTimeUtc))
        {
            WriteSpan(root, spans, depth: 0);
        }
    }

    private static void WriteSpan(Activity span, IReadOnlyCollection<Activity> spans, int depth)
    {
        string tokens = span.GetTagItem("gen_ai.usage.input_tokens") is { } inputTokens
            ? $" - input tokens {inputTokens}, output tokens {span.GetTagItem("gen_ai.usage.output_tokens")}"
            : string.Empty;
        ColoredConsole.WriteSecondaryLogLine($"{new string(' ', 2 + depth * 4)}{span.DisplayName} ({span.Duration.TotalMilliseconds:F0} ms){tokens}");

        foreach (Activity child in spans.Where(other => other.ParentSpanId == span.SpanId).OrderBy(other => other.StartTimeUtc))
        {
            WriteSpan(child, spans, depth + 1);
        }
    }
}
