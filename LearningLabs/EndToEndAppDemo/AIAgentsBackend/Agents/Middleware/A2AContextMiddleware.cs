using System.Text.Json;
using AIAgentsBackend.Agents.Stores;
using AIAgentsBackend.Services;

namespace AIAgentsBackend.Agents.Middleware;

/// <summary>
/// Extracts and validates the signed context ID from A2A requests.
/// </summary>
public class A2AContextMiddleware
{
    private readonly RequestDelegate next;
    private readonly IContextIdValidator contextIdValidator;
    private readonly ILogger<A2AContextMiddleware> logger;

    public A2AContextMiddleware(
        RequestDelegate next,
        IContextIdValidator contextIdValidator,
        ILogger<A2AContextMiddleware> logger)
    {
        this.next = next;
        this.contextIdValidator = contextIdValidator;
        this.logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Method == "POST" && 
            context.Request.Path.StartsWithSegments("/a2a"))
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
                    
                    if (doc.RootElement.TryGetProperty("params", out var paramsElement))
                    {
                        string? contextId = null;
                        string? signature = null;

                        if (paramsElement.TryGetProperty("contextId", out var contextIdElement) &&
                            contextIdElement.ValueKind == JsonValueKind.String)
                        {
                            contextId = contextIdElement.GetString();
                        }

                        if (paramsElement.TryGetProperty("signature", out var signatureElement) &&
                            signatureElement.ValueKind == JsonValueKind.String)
                        {
                            signature = signatureElement.GetString();
                        }

                        if (!string.IsNullOrWhiteSpace(contextId))
                        {
                            if (!string.IsNullOrWhiteSpace(signature))
                            {
                                if (contextIdValidator.ValidateSignature(contextId, signature))
                                {
                                    context.Items[MongoVectorChatMessageStore.ContextIdKey] = contextId;
                                    var username = contextIdValidator.ExtractUsername(contextId);
                                    logger.LogInformation("[A2AMiddleware] Valid signed contextId for user: {Username}", username);
                                }
                                else
                                {
                                    logger.LogWarning("[A2AMiddleware] Invalid signature for contextId: {ContextId}", contextId);
                                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                                    await context.Response.WriteAsJsonAsync(new { error = "Invalid contextId signature" });
                                    return;
                                }
                            }
                            else
                            {
                                logger.LogWarning("[A2AMiddleware] No signature provided for contextId: {ContextId}", contextId);
                                context.Items[MongoVectorChatMessageStore.ContextIdKey] = contextId;
                            }
                        }
                        else
                        {
                            logger.LogDebug("[A2AMiddleware] No contextId in request params");
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[A2AMiddleware] Failed to parse request body: {ex.Message}");
            }
        }

        await next(context);
    }
}
