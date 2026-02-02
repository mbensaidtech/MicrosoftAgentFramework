namespace AIAgentsBackend.Agents.Configuration;

/// <summary>
/// Settings for an AI agent from appsettings.json.
/// </summary>
public class AgentConfiguration
{
    /// <summary>
    /// Unique ID for this agent.
    /// </summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// Azure OpenAI deployment name. Uses default if not set.
    /// </summary>
    public string? ChatDeploymentName { get; set; }

    /// <summary>
    /// Display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// What this agent does.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// System prompt for the agent.
    /// </summary>
    public string Instructions { get; set; } = string.Empty;

    /// <summary>
    /// Response randomness (0.0 - 2.0).
    /// </summary>
    public float? Temperature { get; set; }

    /// <summary>
    /// Maximum response length in tokens.
    /// </summary>
    public int? MaxOutputTokens { get; set; }

    /// <summary>
    /// Top-p sampling value.
    /// </summary>
    public float? TopP { get; set; }

    #region A2A Card Settings

    /// <summary>
    /// Agent card version.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Whether streaming is supported.
    /// </summary>
    public bool Streaming { get; set; } = false;

    /// <summary>
    /// A2A endpoint URL.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Skill ID for the agent card.
    /// </summary>
    public string SkillId { get; set; } = string.Empty;

    /// <summary>
    /// Skill name.
    /// </summary>
    public string SkillName { get; set; } = string.Empty;

    /// <summary>
    /// Skill description.
    /// </summary>
    public string SkillDescription { get; set; } = string.Empty;

    /// <summary>
    /// Tags for categorization.
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Example prompts.
    /// </summary>
    public List<string> Examples { get; set; } = [];

    #endregion
}
