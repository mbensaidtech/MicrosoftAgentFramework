using System.Text.Json;
using AIAgentsBackend.Agents.Stores;
using AIAgentsBackend.Controllers.Models;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Mvc;

namespace AIAgentsBackend.Controllers.Base;

/// <summary>
/// Base controller for ChatClientAgent-based agents.
/// Contains common logic for chat and streaming endpoints.
/// </summary>
public abstract class ChatClientAgentControllerBase : ControllerBase
{
    /// <summary>
    /// Gets the ChatClientAgent instance for this controller.
    /// </summary>
    protected abstract ChatClientAgent GetAgent();

    /// <summary>
    /// Gets the agent name for logging/responses.
    /// </summary>
    protected abstract string AgentName { get; }

    /// <summary>
    /// Standard (non-streaming) chat endpoint.
    /// </summary>
    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Message is required" });
        }

        // Set contextId in HttpContext.Items for the store to use
        var contextId = request.ContextId ?? Guid.NewGuid().ToString("N");
        HttpContext.Items[MongoVectorChatMessageStore.ContextIdKey] = contextId;

        var agent = GetAgent();
        var thread = agent.GetNewThread();

        var response = await agent.RunAsync(request.Message, thread, cancellationToken: cancellationToken);

        return Ok(new ChatResponse
        {
            Message = response.ToString(),
            ContextId = contextId,
            Agent = AgentName
        });
    }

    /// <summary>
    /// Streaming chat endpoint using Server-Sent Events (SSE).
    /// Tokens are sent as they are generated.
    /// </summary>
    [HttpPost("stream")]
    public async Task Stream([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            Response.StatusCode = 400;
            await Response.WriteAsync("{\"error\":\"Message is required\"}");
            return;
        }

        // Set contextId in HttpContext.Items for the store to use
        var contextId = request.ContextId ?? Guid.NewGuid().ToString("N");
        HttpContext.Items[MongoVectorChatMessageStore.ContextIdKey] = contextId;

        // Set SSE headers
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        var agent = GetAgent();
        var thread = agent.GetNewThread();

        try
        {
            // Send start event
            await WriteEventAsync("start", new { contextId, agent = AgentName });

            // Stream tokens
            await foreach (var update in agent.RunStreamingAsync(request.Message, thread, cancellationToken: cancellationToken))
            {
                if (!string.IsNullOrEmpty(update.Text))
                {
                    await WriteEventAsync("token", new { text = update.Text });
                }
            }

            // Send end event
            await WriteEventAsync("end", new { contextId });
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

    private async Task WriteEventAsync(string eventType, object data)
    {
        var json = JsonSerializer.Serialize(data);
        await Response.WriteAsync($"event: {eventType}\n");
        await Response.WriteAsync($"data: {json}\n\n");
        await Response.Body.FlushAsync();
    }
}

/// <summary>
/// Base controller for AIAgent-based agents (with structured output).
/// Contains common logic for chat and streaming endpoints.
/// </summary>
public abstract class AIAgentControllerBase : ControllerBase
{
    /// <summary>
    /// Gets the AIAgent instance for this controller.
    /// </summary>
    protected abstract AIAgent GetAgent();

    /// <summary>
    /// Gets the agent name for logging/responses.
    /// </summary>
    protected abstract string AgentName { get; }

    /// <summary>
    /// Standard (non-streaming) chat endpoint.
    /// </summary>
    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Message is required" });
        }

        var contextId = request.ContextId ?? Guid.NewGuid().ToString("N");
        HttpContext.Items[MongoVectorChatMessageStore.ContextIdKey] = contextId;

        var agent = GetAgent();
        var thread = agent.GetNewThread();

        var response = await agent.RunAsync(request.Message, thread, cancellationToken: cancellationToken);

        return Ok(new ChatResponse
        {
            Message = response.ToString(),
            ContextId = contextId,
            Agent = AgentName
        });
    }

    /// <summary>
    /// Streaming chat endpoint using Server-Sent Events (SSE).
    /// </summary>
    [HttpPost("stream")]
    public async Task Stream([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            Response.StatusCode = 400;
            await Response.WriteAsync("{\"error\":\"Message is required\"}");
            return;
        }

        var contextId = request.ContextId ?? Guid.NewGuid().ToString("N");
        HttpContext.Items[MongoVectorChatMessageStore.ContextIdKey] = contextId;

        // Set SSE headers
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        var agent = GetAgent();
        var thread = agent.GetNewThread();

        try
        {
            await WriteEventAsync("start", new { contextId, agent = AgentName });

            await foreach (var update in agent.RunStreamingAsync(request.Message, thread, cancellationToken: cancellationToken))
            {
                if (!string.IsNullOrEmpty(update.Text))
                {
                    await WriteEventAsync("token", new { text = update.Text });
                }
            }

            await WriteEventAsync("end", new { contextId });
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

    private async Task WriteEventAsync(string eventType, object data)
    {
        var json = JsonSerializer.Serialize(data);
        await Response.WriteAsync($"event: {eventType}\n");
        await Response.WriteAsync($"data: {json}\n\n");
        await Response.Body.FlushAsync();
    }
}
