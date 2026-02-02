using AIAgentsBackend.Middlewares.Http;

namespace AIAgentsBackend.Extensions;

/// <summary>
/// Configures middleware for the application.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the unified agent context middleware for all agent endpoints (A2A and Frontend).
    /// Call this before MapA2A() and before routing.
    /// </summary>
    public static IApplicationBuilder UseAgentContext(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AgentContextMiddleware>();
    }
}
