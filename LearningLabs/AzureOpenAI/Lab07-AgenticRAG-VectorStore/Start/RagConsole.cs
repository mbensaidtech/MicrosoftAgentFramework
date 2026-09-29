using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using CommonUtilities;
using AgenticRAG.Models;

namespace AgenticRAG;

/// <summary>
/// Console output helpers shared by the scenarios, so that Program.cs focuses on the vector store and on RAG.
/// </summary>
public static class RagConsole
{
    /// <summary>
    /// Displays one result of a vector search: its rank, the FAQ entry and the relevance score
    /// (cosine similarity: 1 = identical meaning, 0 = unrelated).
    /// </summary>
    public static void WriteSearchResult(int rank, VectorSearchResult<FaqRecord> result)
    {
        ColoredConsole.WritePrimaryLogLine($"{rank}. {result.Record.Id} (score {result.Score:F4}): {result.Record.Question}");
        ColoredConsole.WriteSecondaryLogLine($"   {result.Record.Answer}");
    }

    /// <summary>
    /// Displays the tools called during an agent run, with their arguments
    /// (the function calls of the tool loop are kept in the response messages).
    /// </summary>
    public static void WriteToolCalls(AgentResponse response)
    {
        List<FunctionCallContent> calls = [.. response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>()];
        if (calls.Count == 0)
        {
            ColoredConsole.WriteWarningLine("Tool called: (none)");
            return;
        }

        foreach (FunctionCallContent call in calls)
        {
            string arguments = string.Join(", ", call.Arguments?.Select(argument => $"{argument.Key}: {argument.Value}") ?? []);
            ColoredConsole.WriteWarningLine($"Tool called: {call.Name}({arguments})");
        }
    }

    /// <summary>
    /// Displays the token usage of an agent run.
    /// With RAG, the input tokens include the FAQ entries given to the model (by a tool result or by the context provider);
    /// with a tool, they also add up the several model calls of the run.
    /// </summary>
    public static void WriteTokenUsage(AgentResponse response, string heading = "Token Usage:")
    {
        ColoredConsole.WriteDividerLine();
        ColoredConsole.WritePrimaryLogLine(heading);
        ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {response.Usage?.InputTokenCount}");
        ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {response.Usage?.OutputTokenCount}");
        ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {response.Usage?.TotalTokenCount}");
    }
}
