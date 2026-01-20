using System.Text.Json;
using AIAgentsBackend.Agents.Stores;
using AIAgentsBackend.Services;

namespace AIAgentsBackend.Agents.Middleware;

/// <summary>
/// Middleware that extracts and validates the signed contextId from A2A request body
/// and stores it in HttpContext.Items for use by MongoVectorChatMessageStore.
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
        // Only process A2A endpoints (POST requests to /a2a/)
        if (context.Request.Method == "POST" && 
            context.Request.Path.StartsWithSegments("/a2a"))
        {
            // Enable buffering so we can read the body multiple times
            context.Request.EnableBuffering();

            try
            {
                // Read the request body
                using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                var body = await reader.ReadToEndAsync();
                
                // Reset the stream position for the next middleware
                context.Request.Body.Position = 0;

                // Parse JSON and extract contextId and signature
                if (!string.IsNullOrEmpty(body))
                {
                    using var doc = JsonDocument.Parse(body);
                    
                    // A2A format: { "params": { "contextId": "xxx", "signature": "yyy" } }
                    if (doc.RootElement.TryGetProperty("params", out var paramsElement))
                    {
                        string? contextId = null;
                        string? signature = null;

                        // Extract contextId
                        if (paramsElement.TryGetProperty("contextId", out var contextIdElement) &&
                            contextIdElement.ValueKind == JsonValueKind.String)
                        {
                            contextId = contextIdElement.GetString();
                        }

                        // Extract signature
                        if (paramsElement.TryGetProperty("signature", out var signatureElement) &&
                            signatureElement.ValueKind == JsonValueKind.String)
                        {
                            signature = signatureElement.GetString();
                        }

                        // Validate contextId and signature
                        if (!string.IsNullOrWhiteSpace(contextId))
                        {
                            // If signature is provided, validate it
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
                                // No signature provided - log warning but allow (for backward compatibility)
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
