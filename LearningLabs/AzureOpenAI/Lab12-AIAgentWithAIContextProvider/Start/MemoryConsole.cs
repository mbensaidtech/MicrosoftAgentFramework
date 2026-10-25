using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CommonUtilities;

namespace AIAgentWithAIContextProvider;

/// <summary>
/// Console output helpers, so that Program.cs focuses on the agent and its context provider.
/// The <c>[Memory]</c> lines are written by the context provider, while the agent runs.
/// </summary>
public static class MemoryConsole
{
    /// <summary>
    /// Written by the provider before the model call: the memories added to the instructions of the run.
    /// </summary>
    public static void WriteMemoriesProvided(string userId, IReadOnlyCollection<string> memories)
    {
        ColoredConsole.WriteSystemLine(memories.Count == 0
            ? $"[Memory] No memory of user '{userId}' yet: nothing added to the instructions"
            : $"[Memory] {Count(memories)} of user '{userId}' added to the instructions");
    }

    /// <summary>
    /// Written by the provider after the run: the new memories extracted from the user message, and the cost of the extraction.
    /// </summary>
    public static void WriteMemoriesExtracted(string userId, IReadOnlyCollection<string> newMemories, UsageDetails? extractionUsage)
    {
        string cost = $"(extraction run: {extractionUsage?.TotalTokenCount} tokens)";
        if (newMemories.Count == 0)
        {
            ColoredConsole.WriteSystemLine($"[Memory] No new memory for user '{userId}' {cost}");
            return;
        }

        ColoredConsole.WriteSystemLine($"[Memory] {Count(newMemories)} extracted and stored for user '{userId}' {cost}:");
        foreach (string memory in newMemories)
        {
            ColoredConsole.WriteSecondaryLogLine($"  + {memory}");
        }
    }

    /// <summary>
    /// Written by the provider after a run without user message (the agent spoke first): nothing to extract.
    /// </summary>
    public static void WriteNothingToExtract()
    {
        ColoredConsole.WriteSystemLine("[Memory] No user message in this run: nothing to extract");
    }

    /// <summary>
    /// Displays the memories of a user, as stored in MongoDB.
    /// </summary>
    public static void WriteStoredMemories(string userId, IReadOnlyCollection<string> memories)
    {
        ColoredConsole.WritePrimaryLogLine($"Memories stored in MongoDB for user '{userId}': {memories.Count}");
        foreach (string memory in memories)
        {
            ColoredConsole.WriteSecondaryLogLine($"  - {memory}");
        }
    }

    private static string Count(IReadOnlyCollection<string> memories) =>
        memories.Count == 1 ? "1 memory" : $"{memories.Count} memories";

    /// <summary>
    /// Displays the token usage of an agent run.
    /// The input tokens include the instructions, so the memories added by the provider.
    /// </summary>
    public static void WriteTokenUsage(AgentResponse response)
    {
        ColoredConsole.WriteDividerLine();
        ColoredConsole.WritePrimaryLogLine("Token Usage (new session, with memories):");
        ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {response.Usage?.InputTokenCount}");
        ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {response.Usage?.OutputTokenCount}");
        ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {response.Usage?.TotalTokenCount}");
    }
}
