using System.ComponentModel;
using System.Text;
using AIAgentsBackend.Models.VectorStore;
using AIAgentsBackend.Services.VectorStore.Interfaces;
using Microsoft.Extensions.VectorData;

namespace AIAgentsBackend.Agents.Tools;

/// <summary>
/// Tools for the Policy Agent to search company policies.
/// </summary>
public class PolicyTools
{
    private readonly IServiceProvider serviceProvider;

    public PolicyTools(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Searches the return policy for information about returning products.
    /// Use this when customers ask about returns, return windows, return conditions, or how to return items.
    /// </summary>
    /// <param name="query">The customer's question about return policy</param>
    /// <returns>Relevant return policy information</returns>
    [Description("Recherche dans la politique de retour. Utilise cet outil quand le client pose des questions sur les retours, délais de retour, conditions de retour, ou comment retourner un produit.")]
    public async Task<string> SearchReturnPolicyAsync(
        [Description("La question du client sur la politique de retour")] string query)
    {
        using var scope = serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IReturnPolicyVectorStoreService>();
        
        var results = await service.SearchAsync(query, topK: 3);
        
        if (results.Count == 0)
        {
            return "Aucune information sur la politique de retour trouvée.";
        }

        return FormatPolicyResults(results, "Politique de Retour");
    }

    /// <summary>
    /// Searches the refund policy for information about getting refunds.
    /// Use this when customers ask about refunds, refund timelines, refund methods, or refund eligibility.
    /// </summary>
    /// <param name="query">The customer's question about refund policy</param>
    /// <returns>Relevant refund policy information</returns>
    [Description("Recherche dans la politique de remboursement. Utilise cet outil quand le client pose des questions sur les remboursements, délais de remboursement, méthodes de remboursement, ou éligibilité au remboursement.")]
    public async Task<string> SearchRefundPolicyAsync(
        [Description("La question du client sur la politique de remboursement")] string query)
    {
        using var scope = serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IRefundPolicyVectorStoreService>();
        
        var results = await service.SearchAsync(query, topK: 3);
        
        if (results.Count == 0)
        {
            return "Aucune information sur la politique de remboursement trouvée.";
        }

        return FormatPolicyResults(results, "Politique de Remboursement");
    }

    /// <summary>
    /// Searches the order cancellation policy for information about cancelling orders.
    /// Use this when customers ask about cancelling orders, cancellation windows, or cancellation fees.
    /// </summary>
    /// <param name="query">The customer's question about order cancellation</param>
    /// <returns>Relevant order cancellation policy information</returns>
    [Description("Recherche dans la politique d'annulation de commande. Utilise cet outil quand le client pose des questions sur l'annulation de commandes, délais d'annulation, ou frais d'annulation.")]
    public async Task<string> SearchOrderCancellationPolicyAsync(
        [Description("La question du client sur l'annulation de commande")] string query)
    {
        using var scope = serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IOrderCancellationPolicyVectorStoreService>();
        
        var results = await service.SearchAsync(query, topK: 3);
        
        if (results.Count == 0)
        {
            return "Aucune information sur la politique d'annulation trouvée.";
        }

        return FormatPolicyResults(results, "Politique d'Annulation");
    }

    /// <summary>
    /// Formats policy search results with elegant markdown styling.
    /// </summary>
    private static string FormatPolicyResults(
        IReadOnlyList<VectorSearchResult<PolicySectionRecord>> results,
        string policyName)
    {
        var sb = new StringBuilder();
        
        // Header with emoji based on policy type
        var emoji = policyName switch
        {
            "Politique de Retour" => "📦",
            "Politique de Remboursement" => "💰",
            "Politique d'Annulation" => "❌",
            _ => "📋"
        };
        
        sb.AppendLine($"{emoji} **{policyName}**");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        foreach (var result in results)
        {
            var record = result.Record;
            
            // Section header with ### for elegance
            sb.AppendLine($"### {record.Title}");
            sb.AppendLine();
            
            // Format content - split into sentences for better readability
            var content = record.Content;
            var sentences = content.Split(new[] { ". " }, StringSplitOptions.RemoveEmptyEntries);
            
            if (sentences.Length > 1)
            {
                // Multiple sentences - format as bullet points
                foreach (var sentence in sentences)
                {
                    var cleanSentence = sentence.Trim();
                    if (!string.IsNullOrWhiteSpace(cleanSentence))
                    {
                        // Add period if missing
                        if (!cleanSentence.EndsWith(".") && !cleanSentence.EndsWith("!") && !cleanSentence.EndsWith("?"))
                        {
                            cleanSentence += ".";
                        }
                        sb.AppendLine($"• {cleanSentence}");
                    }
                }
            }
            else
            {
                // Single sentence - just display it
                sb.AppendLine(content);
            }
            
            sb.AppendLine();
        }

        // Footer with helpful note
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("💡 *Si vous avez d'autres questions, n'hésitez pas à me demander !*");

        return sb.ToString();
    }
}
