using System.Text.RegularExpressions;
using A2A;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CommonUtilities;

namespace A2AClient;

/// <summary>
/// Console output helpers shared by the scenarios, so that Program.cs focuses on A2A.
/// </summary>
public static partial class AgentConsole
{
    /// <summary>
    /// Displays what an agent card tells about a remote agent: identity, skills and supported interfaces.
    /// (<c>A2A.AgentSkill</c> is written in full: Microsoft.Agents.AI also has an <c>AgentSkill</c> type, unrelated to A2A.)
    /// </summary>
    public static void WriteAgentCard(AgentCard card)
    {
        ColoredConsole.WritePrimaryLogLine($"Agent card: {card.Name} (version {card.Version})");
        ColoredConsole.WriteSecondaryLogLine($"  Description: {card.Description}");
        foreach (A2A.AgentSkill skill in card.Skills ?? [])
        {
            ColoredConsole.WriteSecondaryLogLine($"  Skill: {skill.Name} - {skill.Description}");
        }

        foreach (AgentInterface agentInterface in card.SupportedInterfaces ?? [])
        {
            ColoredConsole.WriteSecondaryLogLine(
                $"  Interface: {agentInterface.ProtocolBinding} (A2A {agentInterface.ProtocolVersion}) at {agentInterface.Url}");
        }
    }

    /// <summary>
    /// Displays the tools called during an agent run (the function calls of the tool loop are in the response messages).
    /// </summary>
    public static void WriteToolCalls(AgentResponse response)
    {
        foreach (FunctionCallContent call in response.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>())
        {
            ColoredConsole.WriteSecondaryLogLine($"Tool called: {call.Name}");
        }
    }

    /// <summary>
    /// Displays the token usage of an agent run.
    /// With tools, one run makes several model calls (the model asks for the tools, then answers
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
    /// Finds the API key in the text answered by the AuthAgent (format: <c>Meknes&lt;random&gt;.&lt;signature&gt;</c>).
    /// The remote agent answers in natural language: the key may be surrounded by other words.
    /// </summary>
    public static string FindApiKey(string text) =>
        ApiKeyRegex().Match(text) is { Success: true } match
            ? match.Value
            : throw new InvalidOperationException($"No API key found in the answer of the remote agent: {text}");

    [GeneratedRegex(@"Meknes[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+")]
    private static partial Regex ApiKeyRegex();
}
