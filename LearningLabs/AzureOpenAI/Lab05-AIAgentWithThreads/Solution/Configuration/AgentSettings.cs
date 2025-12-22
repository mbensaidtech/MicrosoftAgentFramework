namespace AIAgentWithThreads.Configuration;

/// <summary>
/// Configuration settings for an AI agent.
/// </summary>
public class AgentSettings
{
    /// <summary>
    /// Gets or sets the agent name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the agent instructions (system prompt).
    /// </summary>
    public string Instructions { get; set; } = string.Empty;
}
