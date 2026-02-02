namespace AIAgentsBackend.Agents.Configuration;

/// <summary>
/// Root configuration section containing all agent configurations.
/// Maps to the "AIAgents" section in appsettings.json.
/// </summary>
public class AgentsConfiguration
{
    /// <summary>
    /// Gets or sets the dictionary of agent configurations keyed by agent ID.
    /// </summary>
    public Dictionary<string, AgentConfiguration> Agents { get; set; } = new();

    /// <summary>
    /// Gets or sets the orchestrator tools configuration.
    /// </summary>
    public ToolsConfiguration OrchestratorTools { get; set; } = new();
}
