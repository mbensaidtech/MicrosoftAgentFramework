using AIAgentsBackend.Repositories;
using AIAgentsBackend.Services.Conversation;

namespace AIAgentsBackend.Services.MessageContext;

/// <summary>
/// Service that builds contextualized messages for AI agents by injecting customer info, interaction state, and conversation history.
/// </summary>
public class MessageContextService : IMessageContextService
{
    private readonly IThreadRepository threadRepository;
    private readonly IConversationService conversationService;

    public MessageContextService(
        IThreadRepository threadRepository,
        IConversationService conversationService)
    {
        this.threadRepository = threadRepository;
        this.conversationService = conversationService;
    }

    public async Task<MessageContextResult> BuildContextualMessageAsync(
        string message,
        string contextId,
        string? conversationId,
        string? customerName,
        CancellationToken cancellationToken)
    {
        var isFirstInteraction = await IsFirstInteractionAsync(contextId, cancellationToken);
        var customerDisplayName = customerName ?? "Client";
        
        var contextualizedMessage = BuildCustomerHeader(customerDisplayName);
        contextualizedMessage += BuildInteractionMarker(isFirstInteraction);
        
        var (conversationHistory, hasHistory) = await LoadConversationHistoryAsync(conversationId, cancellationToken);
        contextualizedMessage += conversationHistory;
        contextualizedMessage += BuildMessageFooter(message);

        return new MessageContextResult
        {
            ContextualizedMessage = contextualizedMessage,
            DisableSellerRequirementsTool = hasHistory
        };
    }

    private async Task<bool> IsFirstInteractionAsync(string contextId, CancellationToken cancellationToken)
    {
        var existingMessages = await threadRepository.GetThreadMessagesAsync(contextId, cancellationToken);
        return !existingMessages.Any();
    }

    private static string BuildCustomerHeader(string customerName)
    {
        return $"[NOM DU CLIENT: {customerName}]\n\n";
    }

    private static string BuildInteractionMarker(bool isFirstInteraction)
    {
        return isFirstInteraction
            ? "=== PREMIÈRE INTERACTION AVEC L'ORCHESTRATEUR (NE SE PRÉSENTER QU'UNE FOIS) ===\n\n"
            : "=== INTERACTION ORCHESTRATEUR EN COURS (NE PAS SE REPRÉSENTER, NE PAS RÉPÉTER LES QUESTIONS DÉJÀ RÉPONDUES) ===\n\n";
    }

    private async Task<(string history, bool hasHistory)> LoadConversationHistoryAsync(
        string? conversationId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            return (string.Empty, false);
        }

        var conversationContext = await conversationService.GetConversationContextAsync(
            conversationId,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(conversationContext))
        {
            return ($"{conversationContext}\n=== MESSAGE DE SUIVI (CONTEXTE DE CONVERSATION DISPONIBLE) ===\n\n", true);
        }

        return ("=== AUCUN HISTORIQUE DE CONVERSATION (PREMIER MESSAGE) ===\n\n", false);
    }

    private static string BuildMessageFooter(string message)
    {
        return $"--- NOUVEAU MESSAGE DU CLIENT ---\n{message}";
    }
}
