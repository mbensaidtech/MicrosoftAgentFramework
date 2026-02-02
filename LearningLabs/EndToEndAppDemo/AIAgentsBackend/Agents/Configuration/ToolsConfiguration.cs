namespace AIAgentsBackend.Agents.Configuration;

/// <summary>
/// Configuration for a collection of agent tools.
/// Can be used for any set of tools (orchestrator tools, or other agent tools).
/// </summary>
public class ToolsConfiguration
{
    /// <summary>
    /// Gets or sets the dictionary of tool configurations keyed by tool name.
    /// </summary>
    public Dictionary<string, ToolConfiguration> Tools { get; set; } = new();
}

