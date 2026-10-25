using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CommonUtilities;
using UseAgentAsTool.Models;

namespace UseAgentAsTool;

/// <summary>
/// Console output helpers shared by the scenarios, so that Program.cs focuses on delegation between agents.
/// </summary>
public static class AgentConsole
{
    /// <summary>
    /// Displays the token usage of an agent run, under a heading that says which agent it covers.
    /// </summary>
    public static void WriteTokenUsage(string heading, AgentResponse response)
    {
        ColoredConsole.WritePrimaryLogLine($"{heading}:");
        ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {response.Usage?.InputTokenCount}");
        ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {response.Usage?.OutputTokenCount}");
        ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {response.Usage?.TotalTokenCount}");
    }

    /// <summary>
    /// Displays a review returned by the reviewer agent (scenario 1).
    /// </summary>
    public static void WriteReview(QualityReview review)
    {
        ColoredConsole.WritePrimaryLogLine($"Quality review: {review.Score}/10");
        ColoredConsole.WriteSecondaryLogLine($"  Feedback: {review.Feedback}");
    }

    /// <summary>
    /// Displays what the model of the calling agent knows about each agent tool: its name, its description
    /// and the JSON schema of its parameters (an agent turned into a tool takes a single "query" string).
    /// </summary>
    public static void WriteAgentTools(IEnumerable<AIFunction> tools)
    {
        foreach (AIFunction tool in tools)
        {
            ColoredConsole.WritePrimaryLogLine($"Tool {tool.Name}: {tool.Description}");
            ColoredConsole.WriteSecondaryLogLine($"  Parameters: {tool.JsonSchema.GetRawText()}");
        }
    }

    /// <summary>
    /// Displays the delegations of a run: for each agent tool called by the model, the query the model wrote
    /// (all that the called agent receives) and the first line of the text returned (all that the caller gets back).
    /// The function calls and their results are kept in the messages of the response, linked by their CallId.
    /// </summary>
    public static void WriteDelegations(AgentResponse response)
    {
        List<AIContent> contents = [.. response.Messages.SelectMany(message => message.Contents)];
        Dictionary<string, object?> results = [];
        foreach (FunctionResultContent result in contents.OfType<FunctionResultContent>())
        {
            results.TryAdd(result.CallId, result.Result);
        }

        foreach (FunctionCallContent call in contents.OfType<FunctionCallContent>())
        {
            object? query = null;
            call.Arguments?.TryGetValue("query", out query);
            ColoredConsole.WriteWarningLine($"[Delegation] {call.Name}(query: \"{OneLine(query)}\")");
            ColoredConsole.WriteWarningLine($"[Delegation] {call.Name} returned: {FirstLine(results.GetValueOrDefault(call.CallId))}");
        }
    }

    /// <summary>
    /// Displays the ticket returned by the ticket specialist after the last call (scenario 3),
    /// and whether it still contains the order number given in the first call.
    /// </summary>
    public static void WriteTicket(string heading, string ticket, string orderId)
    {
        ColoredConsole.WritePrimaryLogLine($"{heading}:");
        foreach (string line in ticket.Trim().Split('\n'))
        {
            ColoredConsole.WriteSecondaryLogLine($"  {line.TrimEnd()}");
        }

        ColoredConsole.WriteSecondaryLogLine($"  -> Order number of the first call ({orderId}) kept: {(ticket.Contains(orderId, StringComparison.Ordinal) ? "yes" : "no")}");
    }

    private static string OneLine(object? value) =>
        string.Join(' ', (value?.ToString() ?? string.Empty).Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));

    private static string FirstLine(object? value) =>
        (value?.ToString() ?? string.Empty).Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
}
