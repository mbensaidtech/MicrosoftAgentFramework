using System.Text.Json;
using AIAgentsBackend.Agents.Stores;

namespace AIAgentsBackend.Agents.Middleware;

/// <summary>
/// Middleware that extracts the contextId from A2A request body
/// and stores it in HttpContext.Items for use by MongoVectorChatMessageStore.
/// </summary>
public class A2AContextMiddleware
{
    private readonly RequestDelegate next;

    public A2AContextMiddleware(RequestDelegate next)
    {
        this.next = next;
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

                // Parse JSON and extract contextId
                if (!string.IsNullOrEmpty(body))
                {
                    using var doc = JsonDocument.Parse(body);
                    
                    // A2A format: { "params": { "contextId": "xxx" } }
                    if (doc.RootElement.TryGetProperty("params", out var paramsElement) &&
                        paramsElement.TryGetProperty("contextId", out var contextIdElement) &&
                        contextIdElement.ValueKind == JsonValueKind.String)
                    {
                        var contextId = contextIdElement.GetString();
                        context.Items[MongoVectorChatMessageStore.ContextIdKey] = contextId;
                        Console.WriteLine($"[A2AMiddleware] Extracted contextId: {contextId}");
                    }
                    else
                    {
                        Console.WriteLine("[A2AMiddleware] No contextId in request params");
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
