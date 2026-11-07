using System.Globalization;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using OpenAI.Responses;
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
        // Invariant culture: the score is always written with a dot (0.4521), whatever the culture of the computer
        ColoredConsole.WritePrimaryLogLine(string.Create(CultureInfo.InvariantCulture,
            $"{rank}. {result.Record.Id} (score {result.Score:F4}): {result.Record.Question}"));
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

    /// <summary>
    /// Scenario 6: displays a vector store of the service once its files are processed: its id, its files and its size.
    /// </summary>
    // VectorStore of OpenAI.VectorStores is still flagged [Experimental] (OPENAI001) by the OpenAI SDK.
#pragma warning disable OPENAI001
    public static void WriteServiceVectorStore(OpenAI.VectorStores.VectorStore vectorStore) =>
        ColoredConsole.WriteInfoLine(
            $"Vector store of the service ready: {vectorStore.Id}, {vectorStore.FileCounts.Completed} files, {vectorStore.UsageBytes} bytes");
#pragma warning restore OPENAI001

    /// <summary>
    /// Scenario 6: displays what the hosted file search did during a run: the queries written by the model, the chunks found
    /// (file name and score, returned when the run asks for <c>file_search_call.results</c>), and the files cited in the answer.
    /// The search runs in the service: it is not a function call of the agent, it comes back as a <c>file_search_call</c> item.
    /// </summary>
    public static void WriteFileSearch(AgentResponse response)
    {
        List<AIContent> contents = [.. response.Messages.SelectMany(message => message.Contents)];

        // The file_search_call item has no Microsoft.Extensions.AI type of its own: read the OpenAI item.
        // FileSearchCallResponseItem is still flagged [Experimental] (OPENAI001) by the OpenAI SDK.
#pragma warning disable OPENAI001
        List<FileSearchCallResponseItem> searches = [.. contents.Select(content => content.RawRepresentation).OfType<FileSearchCallResponseItem>()];
        if (searches.Count == 0)
        {
            ColoredConsole.WriteWarningLine("File search: (not called)");
        }

        foreach (FileSearchCallResponseItem search in searches)
        {
            ColoredConsole.WriteWarningLine($"File search queries (written by the model): {string.Join(" | ", search.Queries)}");
            int rank = 0;
            foreach (FileSearchCallResult result in search.Results)
            {
                ColoredConsole.WritePrimaryLogLine(string.Create(CultureInfo.InvariantCulture,
                    $"{++rank}. {result.Filename} (score {result.Score:F4})"));
            }
        }
#pragma warning restore OPENAI001

        // The answer cites the files it used: CitationAnnotation (FileId, Title = the file name)
        List<string> citedFiles = [.. contents
            .SelectMany(content => content.Annotations ?? [])
            .OfType<CitationAnnotation>()
            .Select(citation => citation.Title ?? citation.FileId ?? "?")
            .Distinct()];
        ColoredConsole.WriteWarningLine($"Files cited in the answer: {(citedFiles.Count > 0 ? string.Join(", ", citedFiles) : "(none)")}");
    }
}
