using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CommonUtilities;

namespace AIAgentWithFunctionTools;

/// <summary>
/// Console output helpers shared by the scenarios, so that Program.cs focuses on function tools.
/// </summary>
public static class AgentConsole
{
    /// <summary>
    /// Displays the token usage of an agent run.
    /// With function tools, one run makes several model calls (the model asks for the tools, then answers
    /// with their results): the usage of the response is the total of all these calls.
    /// </summary>
    public static void WriteTokenUsage(AgentResponse response)
    {
        ColoredConsole.WriteDividerLine();
        ColoredConsole.WritePrimaryLogLine("Token Usage:");
        ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {response.Usage?.InputTokenCount}");
        ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {response.Usage?.OutputTokenCount}");
        ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {response.Usage?.TotalTokenCount}");
    }

    /// <summary>
    /// Displays what the tools returned to the model during a run (first line of each result).
    /// The function calls and their results are kept in the messages of the response, linked by their CallId.
    /// </summary>
    public static void WriteToolResults(AgentResponse response)
    {
        List<AIContent> contents = [.. response.Messages.SelectMany(message => message.Contents)];
        Dictionary<string, string> toolNames = [];
        foreach (FunctionCallContent call in contents.OfType<FunctionCallContent>())
        {
            toolNames.TryAdd(call.CallId, call.Name);
        }

        foreach (FunctionResultContent result in contents.OfType<FunctionResultContent>())
        {
            string toolName = toolNames.GetValueOrDefault(result.CallId, "?");
            ColoredConsole.WriteWarningLine($"[Tool] {toolName} returned: {result.Result?.ToString()?.Split('\n')[0]}");
        }
    }
}
