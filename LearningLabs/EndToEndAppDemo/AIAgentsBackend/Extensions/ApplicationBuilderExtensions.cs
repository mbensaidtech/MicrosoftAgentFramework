using AIAgentsBackend.Agents.Middleware;

namespace AIAgentsBackend.Extensions;

/// <summary>
/// Configures middleware for the application.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the A2A context middleware. Call this before MapA2A().
    /// </summary>
    public static IApplicationBuilder UseA2AContext(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<A2AContextMiddleware>();
    }
}
