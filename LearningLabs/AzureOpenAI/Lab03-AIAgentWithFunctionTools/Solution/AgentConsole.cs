using Microsoft.Agents.AI;
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
}
