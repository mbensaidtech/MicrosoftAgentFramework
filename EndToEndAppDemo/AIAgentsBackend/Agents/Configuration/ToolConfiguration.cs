namespace AIAgentsBackend.Agents.Configuration;

/// <summary>
/// Configuration for an agent tool (name and description).
/// Can be used for any agent that is exposed as a tool.
/// </summary>
public class ToolConfiguration
{
    /// <summary>
    /// Tool name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Tool description.
    /// </summary>
    public string Description { get; set; } = string.Empty;
}

