namespace AIAgentsBackend.Agents.Configuration;

/// <summary>
/// Configuration for an AI agent loaded from appsettings.json.
/// Includes both agent settings and A2A card configuration.
/// </summary>
public class AgentConfiguration
{
    /// <summary>
    /// Gets or sets the unique identifier for this agent.
    /// </summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the chat deployment name for this agent.
    /// If not specified, the default deployment from AzureOpenAI settings will be used.
    /// </summary>
    public string? ChatDeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the display name of the agent.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of what this agent does.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the system instructions (prompt) for the agent.
    /// </summary>
    public string Instructions { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the temperature setting for response generation (0.0 - 2.0).
    /// </summary>
    public float? Temperature { get; set; }

    /// <summary>
    /// Gets or sets the maximum output tokens for responses.
    /// </summary>
    public int? MaxOutputTokens { get; set; }

    /// <summary>
    /// Gets or sets the top-p (nucleus sampling) value.
    /// </summary>
    public float? TopP { get; set; }

    #region A2A Card Configuration

    /// <summary>
    /// Gets or sets the version of the agent card.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets whether the agent supports streaming.
    /// </summary>
    public bool Streaming { get; set; } = false;

    /// <summary>
    /// Gets or sets the A2A endpoint URL for this agent.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the skill ID for the agent card.
    /// </summary>
    public string SkillId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the skill name for the agent card.
    /// </summary>
    public string SkillName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the skill description for the agent card.
    /// </summary>
    public string SkillDescription { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tags for the agent skill.
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Gets or sets the example prompts for the agent skill.
    /// </summary>
    public List<string> Examples { get; set; } = [];

    #endregion
}
