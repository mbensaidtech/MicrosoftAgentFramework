using System.Text.Json;
using Microsoft.Agents.AI;
using CommonUtilities;

namespace AIAgentWithThreads;

/// <summary>
/// Console output helpers shared by the scenarios, so that Program.cs focuses on sessions.
/// </summary>
public static class SessionConsole
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    /// <summary>
    /// Displays a serialized session, as it would be saved to a file or a database.
    /// </summary>
    public static void WriteSerializedSession(JsonElement serializedSession)
    {
        ColoredConsole.WritePrimaryLogLine("Serialized session:");
        ColoredConsole.WriteSecondaryLogLine(JsonSerializer.Serialize(serializedSession, IndentedJson));
    }

    /// <summary>
    /// Displays the token usage of an agent run.
    /// With a session, the input tokens include the chat history sent back to the model.
    /// </summary>
    public static void WriteTokenUsage(AgentResponse response)
    {
        ColoredConsole.WriteDividerLine();
        ColoredConsole.WritePrimaryLogLine("Token Usage (follow-up):");
        ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {response.Usage?.InputTokenCount}");
        ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {response.Usage?.OutputTokenCount}");
        ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {response.Usage?.TotalTokenCount}");
    }

    /// <summary>
    /// Displays one turn of a conversation: the question, the answer, how many messages the session keeps after the run,
    /// and the input tokens of the run (instructions + chat history sent back to the model + question).
    /// </summary>
    public static void WriteTurn(string question, AgentResponse response, int storedMessages)
    {
        ColoredConsole.WritePrimaryLogLine($"User: {question}");
        ColoredConsole.WriteSecondaryLogLine($"Agent: {response.Text}");
        ColoredConsole.WriteInfoLine($"Messages kept in the session: {storedMessages} (input tokens of this run: {response.Usage?.InputTokenCount})");
    }
}
