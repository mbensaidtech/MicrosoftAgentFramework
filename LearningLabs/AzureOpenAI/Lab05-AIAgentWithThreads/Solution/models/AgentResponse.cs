namespace AIAgentWithThreads.Models;

/// <summary>
/// Represents the response from an agent run.
/// </summary>
public sealed class AgentResponse
{
    /// <summary>
    /// Gets or sets the response from the agent.
    /// </summary>
    public string Response { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the thread id.
    /// </summary>
    public string? ThreadId { get; set; }
}
