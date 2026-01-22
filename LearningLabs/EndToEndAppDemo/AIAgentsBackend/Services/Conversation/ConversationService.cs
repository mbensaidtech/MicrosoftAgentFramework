using System.Text;
using AIAgentsBackend.Models.CustomerSellerConversation;
using AIAgentsBackend.Repositories;

namespace AIAgentsBackend.Services.Conversation;

/// <summary>
/// Service for managing customer-seller conversations.
/// </summary>
public class ConversationService : IConversationService
{
    private readonly IConversationRepository conversationRepository;

    public ConversationService(IConversationRepository conversationRepository)
    {
        this.conversationRepository = conversationRepository ?? throw new ArgumentNullException(nameof(conversationRepository));
    }

    /// <inheritdoc />
    public async Task<ConversationMessage> AddCustomerMessageAsync(
        string conversationId, 
        string content, 
        string? customerName = null, 
        CancellationToken cancellationToken = default)
    {
        var message = new ConversationMessage
        {
            ConversationId = conversationId,
            From = "customer",
            Content = content,
            CustomerName = customerName,
            Timestamp = DateTime.UtcNow
        };

        return await conversationRepository.AddMessageAsync(message, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ConversationMessage> AddSellerMessageAsync(
        string conversationId, 
        string content, 
        CancellationToken cancellationToken = default)
    {
        var message = new ConversationMessage
        {
            ConversationId = conversationId,
            From = "seller",
            Content = content,
            Timestamp = DateTime.UtcNow
        };

        return await conversationRepository.AddMessageAsync(message, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ConversationMessage>> GetConversationAsync(
        string conversationId, 
        CancellationToken cancellationToken = default)
    {
        return await conversationRepository.GetConversationAsync(conversationId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> GetConversationContextAsync(
        string conversationId, 
        CancellationToken cancellationToken = default)
    {
        var messages = await conversationRepository.GetConversationAsync(conversationId, cancellationToken);
        var messageList = messages.ToList();

        if (!messageList.Any())
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine("=== HISTORIQUE DE LA CONVERSATION AVEC LE VENDEUR ===");
        sb.AppendLine("(Messages précédemment envoyés au vendeur par ce client)");
        sb.AppendLine();

        foreach (var msg in messageList)
        {
            var sender = msg.From == "customer" 
                ? $"CLIENT ({msg.CustomerName ?? "Client"})" 
                : "VENDEUR";
            var timestamp = msg.Timestamp.ToString("dd/MM/yyyy HH:mm");
            
            sb.AppendLine($"[{timestamp}] {sender}:");
            sb.AppendLine(msg.Content);
            sb.AppendLine();
        }

        sb.AppendLine("=== FIN DE L'HISTORIQUE ===");
        sb.AppendLine();

        return sb.ToString();
    }

    /// <inheritdoc />
    public async Task<bool> HasExistingConversationAsync(
        string conversationId, 
        CancellationToken cancellationToken = default)
    {
        return await conversationRepository.ConversationExistsAsync(conversationId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ClearConversationAsync(
        string conversationId, 
        CancellationToken cancellationToken = default)
    {
        await conversationRepository.DeleteConversationAsync(conversationId, cancellationToken);
    }
}
