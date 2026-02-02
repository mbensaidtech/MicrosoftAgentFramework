using System.Text.Json;
using AIAgentsBackend.Agents.Factory;
using AIAgentsBackend.Agents.Stores;
using AIAgentsBackend.Controllers.Models;
using AIAgentsBackend.Repositories;
using AIAgentsBackend.Services.Conversation;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Mvc;

namespace AIAgentsBackend.Controllers;

/// <summary>
/// Controller for the Message Formulator Agent.
/// Helps customers write clear, well-structured messages to sellers.
/// Manages both AI assistant thread and customer-seller conversation.
/// </summary>
[ApiController]
[Route("api/agents/message-formulator")]
public class MessageFormulatorAgentController : ControllerBase
{
    private readonly IAgentFactory agentFactory;
    private readonly IConversationService conversationService;
    private readonly IThreadRepository threadRepository;

    public MessageFormulatorAgentController(
        IAgentFactory agentFactory,
        IConversationService conversationService,
        IThreadRepository threadRepository)
    {
        this.agentFactory = agentFactory;
        this.conversationService = conversationService;
        this.threadRepository = threadRepository;
    }

    /// <summary>
    /// Streaming chat endpoint with conversation context injection.
    /// </summary>
    [HttpPost("stream")]
    public async Task Stream([FromBody] MessageFormulatorRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            Response.StatusCode = 400;
            await Response.WriteAsync("{\"error\":\"Message is required\"}");
            return;
        }

        // ContextId is now extracted by AgentContextMiddleware and stored in HttpContext.Items
        var contextId = HttpContext.Items[MongoVectorChatMessageStore.ContextIdKey] as string 
                        ?? throw new InvalidOperationException("ContextId not found in HttpContext. Ensure AgentContextMiddleware is registered.");

        // Set SSE headers
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        var agent = agentFactory.GetMessageFormulatorAgent().Agent;
        var thread = agent.GetNewThread();

        try
        {
            // Detect whether this is the first interaction in this assistant thread (contextId).
            // This prevents the assistant from re-introducing itself or repeating the same questions
            // when the user is simply answering.
            var existingThreadMessages = await threadRepository.GetThreadMessagesAsync(contextId, cancellationToken);
            var isFirstAssistantTurn = !existingThreadMessages.Any();

            // Build the message with customer name and conversation context
            var customerName = request.CustomerName ?? "Client";
            string messageWithContext = $"[NOM DU CLIENT: {customerName}]\n\n";

            messageWithContext += isFirstAssistantTurn
                ? "=== PREMIÈRE INTERACTION AVEC L'ASSISTANT (NE SE PRÉSENTER QU'UNE FOIS) ===\n\n"
                : "=== INTERACTION ASSISTANT EN COURS (NE PAS SE REPRÉSENTER, NE PAS RÉPÉTER LES QUESTIONS DÉJÀ RÉPONDUES) ===\n\n";
            
            // Get conversation context if conversationId is provided
            if (!string.IsNullOrWhiteSpace(request.ConversationId))
            {
                var conversationContext = await conversationService.GetConversationContextAsync(
                    request.ConversationId, 
                    cancellationToken);
                
                if (!string.IsNullOrWhiteSpace(conversationContext))
                {
                    // Follow-up message: disable seller knowledge tool.
                    HttpContext.Items["DisableSellerRequirementsTool"] = true;
                    messageWithContext += $"{conversationContext}\n";
                    messageWithContext += "=== MESSAGE DE SUIVI (NE PAS UTILISER SearchSellerRequirements, NE PAS AFFICHER LE BLOC 💡) ===\n\n";
                    Console.WriteLine($"[MessageFormulatorAgent] Injected conversation context for {request.ConversationId}");
                }
                else
                {
                    // Make it explicit to the model that this is the first message to the seller conversation.
                    // This helps the agent decide to use seller knowledge only for the first message.
                    HttpContext.Items["DisableSellerRequirementsTool"] = false;
                    messageWithContext += "=== AUCUN HISTORIQUE AVEC LE VENDEUR (PREMIER MESSAGE) ===\n\n";
                    Console.WriteLine($"[MessageFormulatorAgent] No conversation history found for {request.ConversationId}");
                }
            }
            else
            {
                // No conversationId: treat as first message, but we won't have seller history anyway.
                HttpContext.Items["DisableSellerRequirementsTool"] = false;
                Console.WriteLine($"[MessageFormulatorAgent] No conversationId provided in request");
            }
            
            messageWithContext += $"--- NOUVEAU MESSAGE DU CLIENT ---\n{request.Message}";
            Console.WriteLine($"[MessageFormulatorAgent] Customer name: {customerName}");

            // Send start event
            await WriteEventAsync("start", new { contextId, conversationId = request.ConversationId, agent = "MessageFormulatorAgent" });

            // Stream tokens
            await foreach (var update in agent.RunStreamingAsync(messageWithContext, thread, cancellationToken: cancellationToken))
            {
                if (!string.IsNullOrEmpty(update.Text))
                {
                    await WriteEventAsync("token", new { text = update.Text });
                }
            }

            // Send end event
            await WriteEventAsync("end", new { contextId, conversationId = request.ConversationId });
        }
        catch (OperationCanceledException)
        {
            await WriteEventAsync("cancelled", new { message = "Request was cancelled" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MessageFormulatorAgent] Error: {ex.Message}");
            await WriteEventAsync("error", new { message = ex.Message });
        }
    }

    /// <summary>
    /// Saves an approved customer message to the customer-seller conversation.
    /// </summary>
    [HttpPost("conversation/message")]
    public async Task<IActionResult> SaveMessage([FromBody] SaveMessageRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ConversationId))
        {
            return BadRequest(new { error = "ConversationId is required" });
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { error = "Content is required" });
        }

        try
        {
            var message = await conversationService.AddCustomerMessageAsync(
                request.ConversationId,
                request.Content,
                request.CustomerName,
                cancellationToken);

            Console.WriteLine($"[MessageFormulatorAgent] Saved customer message to conversation {request.ConversationId}");

            return Ok(new
            {
                success = true,
                conversationId = request.ConversationId,
                messageId = message.Id,
                timestamp = message.Timestamp
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MessageFormulatorAgent] Error saving message: {ex.Message}");
            return StatusCode(500, new { error = "Failed to save message" });
        }
    }

    /// <summary>
    /// Gets the customer-seller conversation history.
    /// </summary>
    [HttpGet("conversation/{conversationId}")]
    public async Task<IActionResult> GetConversation(string conversationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            return BadRequest(new { error = "ConversationId is required" });
        }

        try
        {
            var messages = await conversationService.GetConversationAsync(conversationId, cancellationToken);

            var response = new ConversationResponse
            {
                ConversationId = conversationId,
                Messages = messages.Select(m => new ConversationMessageDto
                {
                    From = m.From,
                    Content = m.Content,
                    Timestamp = m.Timestamp,
                    CustomerName = m.CustomerName
                }).ToList()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MessageFormulatorAgent] Error getting conversation: {ex.Message}");
            return StatusCode(500, new { error = "Failed to get conversation" });
        }
    }

    /// <summary>
    /// Clears a customer-seller conversation AND all related AI assistant threads.
    /// AI assistant threads are identified by having the conversationId as a prefix.
    /// </summary>
    [HttpDelete("conversation/{conversationId}")]
    public async Task<IActionResult> ClearConversation(string conversationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            return BadRequest(new { error = "ConversationId is required" });
        }

        try
        {
            // Clear customer-seller conversation
            await conversationService.ClearConversationAsync(conversationId, cancellationToken);
            Console.WriteLine($"[MessageFormulatorAgent] Cleared customer-seller conversation {conversationId}");

            // Clear all AI assistant threads that belong to this conversation
            // AI threads have contextIds that start with the conversationId (e.g., "conv-xxx-ai-timestamp-random")
            var deletedThreadMessages = await threadRepository.DeleteThreadsByPrefixAsync(conversationId, cancellationToken);
            Console.WriteLine($"[MessageFormulatorAgent] Cleared {deletedThreadMessages} AI assistant messages for conversation {conversationId}");

            return Ok(new { 
                success = true, 
                conversationId,
                deletedConversationMessages = true,
                deletedAiThreadMessages = deletedThreadMessages
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MessageFormulatorAgent] Error clearing conversation: {ex.Message}");
            return StatusCode(500, new { error = "Failed to clear conversation" });
        }
    }

    private async Task WriteEventAsync(string eventType, object data)
    {
        var json = JsonSerializer.Serialize(data);
        await Response.WriteAsync($"event: {eventType}\n");
        await Response.WriteAsync($"data: {json}\n\n");
        await Response.Body.FlushAsync();
    }
}
