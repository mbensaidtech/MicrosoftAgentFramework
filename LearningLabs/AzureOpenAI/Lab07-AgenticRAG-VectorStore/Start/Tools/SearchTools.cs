using System.ComponentModel;
using AgenticRAG.Services;

namespace AgenticRAG.Tools;

/// <summary>
/// Tools for searching FAQ data in the vector store.
/// </summary>
public class SearchTools
{
    private readonly FaqVectorStoreService _faqService;

    public SearchTools(FaqVectorStoreService faqService)
    {
        _faqService = faqService;
    }

    /// <summary>
    /// Searches for FAQ entries similar to the given question.
    /// </summary>
    /// <param name="question">The question to search for.</param>
    /// <param name="topK">The number of results to return (default: 3).</param>
    /// <returns>A list of formatted FAQ results with scores.</returns>
    [Description("Searches for FAQ entries similar to the given question and returns the top matching results with their relevance scores.")]
    public async Task<List<string>> SearchFaqAsync(string question, int topK = 3)
    {
        // TODO: Implement the SearchFaqAsync method
        // Step 1: Call _faqService.SearchAsync with the question and topK
        // Step 2: Transform results to formatted strings using r.Record.ToString(r.Score)
        // Step 3: Return the list of formatted strings
        throw new NotImplementedException("Implement the SearchFaqAsync method");
    }
}
