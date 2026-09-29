using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.VectorData;
using AgenticRAG.Models;

namespace AgenticRAG.Tools;

/// <summary>
/// The function tool of the agent of scenario 3: a semantic search in the FAQ collection.
/// The model reads the [Description] attributes to decide when to call it and with what arguments.
/// </summary>
public sealed class FaqSearchTool(VectorStoreCollection<string, FaqRecord> faqCollection)
{
    /// <summary>
    /// Searches the FAQ entries closest to a question and returns them as text for the model.
    /// The question is embedded by the vector store with the same embedding generator as the entries.
    /// </summary>
    [Description("Searches the FAQ of the online shop and returns the entries closest to the question: their id, question, answer and relevance score.")]
    public async Task<string> SearchFaqAsync(
        [Description("The customer question, in natural language.")] string question,
        [Description("The number of FAQ entries to return.")] int top = 3,
        CancellationToken cancellationToken = default)
    {
        StringBuilder results = new();
        await foreach (VectorSearchResult<FaqRecord> result in faqCollection.SearchAsync(question, top, cancellationToken: cancellationToken))
        {
            results.AppendLine($"[{result.Record.Id}] (score {result.Score:F4}) Q: {result.Record.Question}");
            results.AppendLine($"A: {result.Record.Answer}");
        }

        return results.Length > 0 ? results.ToString() : "No FAQ entry matches this question.";
    }
}
