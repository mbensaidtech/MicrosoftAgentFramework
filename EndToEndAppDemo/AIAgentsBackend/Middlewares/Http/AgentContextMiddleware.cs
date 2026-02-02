using System.Text.Json;
using AIAgentsBackend.Agents.Stores;
using AIAgentsBackend.Services;
using CommonUtilities;

namespace AIAgentsBackend.Middlewares.Http;

/// <summary>
/// Unified middleware that extracts contextId from all agent requests (A2A and Frontend).
/// Supports both A2A format (params.contextId) and Frontend format (direct contextId).
/// Validates HMAC-SHA256 signatures when provided for security.
/// </summary>
public class AgentContextMiddleware
{
    private readonly RequestDelegate next;
    private readonly IContextIdValidator contextIdValidator;
    private readonly ILogger<AgentContextMiddleware> logger;

    public AgentContextMiddleware(
        RequestDelegate next,
        IContextIdValidator contextIdValidator,
        ILogger<AgentContextMiddleware> logger)
    {
        this.next = next;
        this.contextIdValidator = contextIdValidator;
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
                    string? signature = null;
                    string? requestType = null;

                    // Try A2A format first (params.contextId and params.signature)
                    if (doc.RootElement.TryGetProperty("params", out var paramsElement))
                    {
                        if (paramsElement.TryGetProperty("contextId", out var a2aContextId) &&
                            a2aContextId.ValueKind == JsonValueKind.String)
                        {
                            contextId = a2aContextId.GetString();
                            requestType = "A2A";
                        }

                        if (paramsElement.TryGetProperty("signature", out var a2aSignature) &&
                            a2aSignature.ValueKind == JsonValueKind.String)
                        {
                            signature = a2aSignature.GetString();
                        }
                    }

                    // Try Frontend format (direct contextId and signature)
                    if (contextId == null && 
                        doc.RootElement.TryGetProperty("contextId", out var frontendContextId) &&
                        frontendContextId.ValueKind == JsonValueKind.String)
                    {
                        contextId = frontendContextId.GetString();
                        requestType = "Frontend";

                        if (doc.RootElement.TryGetProperty("signature", out var frontendSignature) &&
                            frontendSignature.ValueKind == JsonValueKind.String)
                        {
                            signature = frontendSignature.GetString();
                        }
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
                        // Validate signature if provided
                        if (!string.IsNullOrWhiteSpace(signature) && !string.IsNullOrWhiteSpace(contextId))
                        {
                            if (contextIdValidator.ValidateSignature(contextId, signature))
                            {
                                var username = contextIdValidator.ExtractUsername(contextId);
                                ColoredConsole.WriteSuccessLine($"[AgentContextMiddleware] ✅ Valid signature from {requestType} for user: {username}");
                                logger.LogInformation("[AgentContextMiddleware] Valid signed contextId from {Type} for user: {Username}", requestType, username);
                            }
                            else
                            {
                                ColoredConsole.WriteErrorLine($"[AgentContextMiddleware] ❌ Invalid signature for contextId: {contextId}");
                                logger.LogWarning("[AgentContextMiddleware] Invalid signature for contextId: {ContextId}", contextId);
                                
                                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                                await context.Response.WriteAsJsonAsync(new { error = "Invalid contextId signature" });
                                return;
                            }
                        }
                        else
                        {
                            ColoredConsole.WriteSecondaryLogLine($"[AgentContextMiddleware] Extracted contextId from {requestType}: {contextId} (no signature)");
                            logger.LogDebug("[AgentContextMiddleware] No signature provided for contextId: {ContextId}", contextId);
                        }
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
