using AIAgentsBackend.Agents.Middleware;

namespace AIAgentsBackend.Agents.Extensions;

/// <summary>
/// Extension methods for configuring middleware in the application pipeline.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the A2A context middleware to the pipeline.
    /// This middleware extracts contextId from A2A request bodies and stores it in HttpContext.Items.
    /// Must be called BEFORE MapA2A().
    /// </summary>
    public static IApplicationBuilder UseA2AContext(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<A2AContextMiddleware>();
    }
}
