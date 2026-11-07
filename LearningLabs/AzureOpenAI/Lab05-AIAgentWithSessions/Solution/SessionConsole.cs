using System.ClientModel;
using System.Text.Json;
using Microsoft.Agents.AI;
using OpenAI.Responses;
using CommonUtilities;

namespace AIAgentWithSessions;

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

    /// <summary>
    /// Scenarios 4 and 5: asks the Responses API whether it kept a response (<c>GET /responses/{id}</c>), and displays the answer.
    /// A response created with <c>store = false</c> is not kept: the service answers 404.
    /// </summary>
    // ResponsesClient is still flagged [Experimental] (OPENAI001) by the OpenAI SDK.
#pragma warning disable OPENAI001
    public static async Task WriteServiceStorageAsync(ResponsesClient responsesClient, string? responseId)
    {
        if (string.IsNullOrWhiteSpace(responseId))
        {
            ColoredConsole.WriteWarningLine("No response id: nothing to look for in the service");
            return;
        }

        bool stored;
        try
        {
            await responsesClient.GetResponseAsync(responseId);
            stored = true;
        }
        catch (ClientResultException exception) when (exception.Status == 404)
        {
            stored = false;
        }

        ColoredConsole.WriteInfoLine($"Response {responseId} stored by the service: {(stored ? "yes" : "no (404)")}");
    }
#pragma warning restore OPENAI001

    /// <summary>
    /// Scenario 5: displays the four identifiers of a conversation, which must not be confused:
    /// the id of your application, the session of Agent Framework (with the key of its chat history), and the ids of the provider.
    /// </summary>
    public static void WriteIdentifiers(string businessConversationId, string chatHistoryKey, string? lastResponseId, string? conversationId)
    {
        ColoredConsole.WriteDividerLine();
        ColoredConsole.WritePrimaryLogLine("Identifiers of this conversation:");
        ColoredConsole.WriteSecondaryLogLine($"  Business conversation id (your application): {businessConversationId}");
        ColoredConsole.WriteSecondaryLogLine($"  Agent Framework session: the serialized AgentSession, saved under {businessConversationId} (a session has no id of its own)");
        ColoredConsole.WriteSecondaryLogLine($"  Chat history key (PersistentChatHistoryProvider, inside the session): {chatHistoryKey}");
        ColoredConsole.WriteSecondaryLogLine($"  Last response id (Responses API, not stored): {lastResponseId ?? "(none)"}");
        ColoredConsole.WriteSecondaryLogLine($"  Service conversation id (ChatClientAgentSession.ConversationId): {conversationId ?? "(none)"}");
    }
}
