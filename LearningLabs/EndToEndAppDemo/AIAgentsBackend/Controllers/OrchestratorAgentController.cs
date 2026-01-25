using System.Text.Json;
using AIAgentsBackend.Agents.Factory;
using AIAgentsBackend.Agents.Stores;
using AIAgentsBackend.Controllers.Models;
using AIAgentsBackend.Repositories;
using AIAgentsBackend.Services.Conversation;
using AIAgentsBackend.Services.MessageContext;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Mvc;

namespace AIAgentsBackend.Controllers;

/// <summary>
/// Controller for the Orchestrator Agent.
/// Coordinates specialized agents (OrderAgent, PolicyAgent, MessageFormulatorAgent) to handle customer requests.
/// </summary>
[ApiController]
[Route("api/agents/orchestrator")]
public class OrchestratorAgentController : ControllerBase
{
    private readonly IAgentFactory agentFactory;
    private readonly IMessageContextService messageContextService;
    private readonly IConversationService conversationService;
    private readonly IThreadRepository threadRepository;

    public OrchestratorAgentController(
        IAgentFactory agentFactory,
        IMessageContextService messageContextService,
        IConversationService conversationService,
        IThreadRepository threadRepository)
    {
        this.agentFactory = agentFactory;
        this.messageContextService = messageContextService;
        this.conversationService = conversationService;
        this.threadRepository = threadRepository;
    }

    /// <summary>
    /// Streaming chat endpoint with conversation context injection.
    /// </summary>
    [HttpPost("stream")]
    public async Task Stream([FromBody] MessageFormulatorRequest request, CancellationToken cancellationToken)
    {
        // Validate request
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            Response.StatusCode = 400;
            await Response.WriteAsync("{\"error\":\"Message is required\"}");
            return;
        }

        // Get contextId from middleware
        var contextId = HttpContext.Items[MongoVectorChatMessageStore.ContextIdKey] as string 
                        ?? throw new InvalidOperationException("ContextId not found. Ensure AgentContextMiddleware is registered.");

        // Set SSE headers
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        // Get agent
        var (agent, _) = agentFactory.GetOrchestratorAgent();

        try
        {
            var contextResult = await messageContextService.BuildContextualMessageAsync(
                request.Message,
                contextId,
                request.ConversationId,
                request.CustomerName,
                cancellationToken);

            HttpContext.Items["DisableSellerRequirementsTool"] = contextResult.DisableSellerRequirementsTool;

            await WriteEventAsync("start", new { contextId, conversationId = request.ConversationId, agent = "OrchestratorAgent" });

            await foreach (var update in agent.RunStreamingAsync(contextResult.ContextualizedMessage, cancellationToken: cancellationToken))
            {
                if (!string.IsNullOrEmpty(update.Text))
                {
                    await WriteEventAsync("token", new { text = update.Text });
                }
            }

            await WriteEventAsync("end", new { contextId, conversationId = request.ConversationId });
        }
        catch (OperationCanceledException)
        {
            await WriteEventAsync("cancelled", new { message = "Request was cancelled" });
        }
        catch (Exception ex)
        {
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

            return Ok(new
            {
                success = true,
                conversationId = request.ConversationId,
                messageId = message.Id,
                timestamp = message.Timestamp
            });
        }
        catch (Exception)
        {
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
        catch (Exception)
        {
            return StatusCode(500, new { error = "Failed to get conversation" });
        }
    }

    /// <summary>
    /// Clears a customer-seller conversation AND all related AI assistant threads.
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

            // Clear all AI assistant threads that belong to this conversation
            var deletedThreadMessages = await threadRepository.DeleteThreadsByPrefixAsync(conversationId, cancellationToken);

            return Ok(new { 
                success = true, 
                conversationId,
                deletedConversationMessages = true,
                deletedAiThreadMessages = deletedThreadMessages
            });
        }
        catch (Exception)
        {
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
