using System.ComponentModel;
using System.Text;
using AIAgentsBackend.Models.VectorStore;
using AIAgentsBackend.Services.VectorStore.Interfaces;
using Microsoft.Extensions.VectorData;

namespace AIAgentsBackend.Agents.Tools;

/// <summary>
/// Tools for customer support agent to search policy documents.
/// </summary>
public class CustomerSupportTools
{
    private readonly IServiceProvider serviceProvider;

    public CustomerSupportTools(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Searches the return policy for information about returning products.
    /// Use this when customers ask about returns, return windows, return conditions, or how to return items.
    /// </summary>
    /// <param name="query">The customer's question about return policy</param>
    /// <returns>Relevant return policy information with sources</returns>
    [Description("Searches the return policy for information about returning products. Use this when customers ask about returns, return windows, return conditions, or how to return items.")]
    public async Task<string> SearchReturnPolicyAsync(
        [Description("The customer's question about return policy")] string query)
    {
        using var scope = serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IReturnPolicyVectorStoreService>();
        
        var results = await service.SearchAsync(query, topK: 3);
        
        if (results.Count == 0)
        {
            return "No relevant return policy information found in the knowledge base.";
        }

        return FormatResultsWithSources(results, "Return Policy");
    }

    /// <summary>
    /// Searches the refund policy for information about getting refunds.
    /// Use this when customers ask about refunds, refund timelines, refund methods, or refund eligibility.
    /// </summary>
    /// <param name="query">The customer's question about refund policy</param>
    /// <returns>Relevant refund policy information with sources</returns>
    [Description("Searches the refund policy for information about getting refunds. Use this when customers ask about refunds, refund timelines, refund methods, or refund eligibility.")]
    public async Task<string> SearchRefundPolicyAsync(
        [Description("The customer's question about refund policy")] string query)
    {
        using var scope = serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IRefundPolicyVectorStoreService>();
        
        var results = await service.SearchAsync(query, topK: 3);
        
        if (results.Count == 0)
        {
            return "No relevant refund policy information found in the knowledge base.";
        }

        return FormatResultsWithSources(results, "Refund Policy");
    }

    /// <summary>
    /// Searches the order cancellation policy for information about cancelling orders.
    /// Use this when customers ask about cancelling orders, cancellation windows, or cancellation fees.
    /// </summary>
    /// <param name="query">The customer's question about order cancellation</param>
    /// <returns>Relevant order cancellation policy information with sources</returns>
    [Description("Searches the order cancellation policy for information about cancelling orders. Use this when customers ask about cancelling orders, cancellation windows, or cancellation fees.")]
    public async Task<string> SearchOrderCancellationPolicyAsync(
        [Description("The customer's question about order cancellation")] string query)
    {
        using var scope = serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IOrderCancellationPolicyVectorStoreService>();
        
        var results = await service.SearchAsync(query, topK: 3);
        
        if (results.Count == 0)
        {
            return "No relevant order cancellation policy information found in the knowledge base.";
        }

        return FormatResultsWithSources(results, "Order Cancellation Policy");
    }

    /// <summary>
    /// Formats search results with clear source attribution.
    /// </summary>
    private static string FormatResultsWithSources(
        IReadOnlyList<VectorSearchResult<PolicySectionRecord>> results,
        string policyName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[SOURCE: {policyName} - Knowledge Base]");
        sb.AppendLine();

        foreach (var result in results)
        {
            var record = result.Record;
            var score = result.Score;
            
            sb.AppendLine($"--- Section: {record.Title} (Relevance: {score:P0}) ---");
            sb.AppendLine(record.Content);
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
