using System.Text.Json;
using AIAgentsBackend.Agents.Stores;
using CommonUtilities;

namespace AIAgentsBackend.Middlewares.Http;

/// <summary>
/// Unified middleware that extracts contextId from all agent requests (A2A and Frontend).
/// Supports both A2A format (params.contextId) and Frontend format (direct contextId).
/// </summary>
public class AgentContextMiddleware
{
    private readonly RequestDelegate next;
    private readonly ILogger<AgentContextMiddleware> logger;

    public AgentContextMiddleware(
        RequestDelegate next,
        ILogger<AgentContextMiddleware> logger)
    {
        this.next = next;
        this.logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Match ALL agent endpoints (A2A and Frontend)
        if (context.Request.Method == "POST" && 
            (context.Request.Path.StartsWithSegments("/a2a") || 
             context.Request.Path.StartsWithSegments("/api/agents")))
        {
            context.Request.EnableBuffering();

            try
            {
                using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                var body = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;

                if (!string.IsNullOrEmpty(body))
                {
                    using var doc = JsonDocument.Parse(body);
                    string? contextId = null;
                    string? requestType = null;

                    // Try A2A format first (params.contextId)
                    if (doc.RootElement.TryGetProperty("params", out var paramsElement))
                    {
                        if (paramsElement.TryGetProperty("contextId", out var a2aContextId) &&
                            a2aContextId.ValueKind == JsonValueKind.String)
                        {
                            contextId = a2aContextId.GetString();
                            requestType = "A2A";
                        }
                    }

                    // Try Frontend format (direct contextId)
                    if (contextId == null && 
                        doc.RootElement.TryGetProperty("contextId", out var frontendContextId) &&
                        frontendContextId.ValueKind == JsonValueKind.String)
                    {
                        contextId = frontendContextId.GetString();
                        requestType = "Frontend";
                    }

                    // Generate new contextId if not provided
                    bool isNewContext = string.IsNullOrWhiteSpace(contextId);
                    if (isNewContext)
                    {
                        contextId = Guid.NewGuid().ToString("N");
                        requestType ??= "Unknown";
                        
                        ColoredConsole.WriteSecondaryLogLine($"[AgentContextMiddleware] Generated new contextId: {contextId}");
                    }
                    else
                    {
                        ColoredConsole.WriteSecondaryLogLine($"[AgentContextMiddleware] Extracted contextId from {requestType}: {contextId}");
                    }

                    // Store in HttpContext for the entire request pipeline
                    context.Items[MongoVectorChatMessageStore.ContextIdKey] = contextId;
                    context.Items["IsNewContext"] = isNewContext;
                    context.Items["RequestType"] = requestType;

                    logger.LogDebug(
                        "[AgentContextMiddleware] Path: {Path}, Type: {Type}, ContextId: {ContextId}, IsNew: {IsNew}",
                        context.Request.Path,
                        requestType,
                        contextId,
                        isNewContext);
                }
                else
                {
                    logger.LogWarning("[AgentContextMiddleware] Empty request body for {Path}", context.Request.Path);
                }
            }
            catch (JsonException ex)
            {
                ColoredConsole.WriteErrorLine($"[AgentContextMiddleware] Failed to parse request body: {ex.Message}");
                logger.LogError(ex, "[AgentContextMiddleware] Failed to parse request body");
            }
        }

        await next(context);
    }
}
