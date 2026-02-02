namespace AIAgentsBackend.Configuration;

/// <summary>
/// Configuration settings for Azure OpenAI.
/// </summary>
public class AzureOpenAISettings
{
    /// <summary>
    /// Gets or sets the Azure OpenAI endpoint.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the default Azure OpenAI chat deployment name.
    /// Used when no specific deployment is specified for an agent.
    /// </summary>
    public string DefaultChatDeploymentName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the default Azure OpenAI embedding deployment name.
    /// </summary>
    public string DefaultEmbeddingDeploymentName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Azure OpenAI API key.
    /// </summary>
    public string APIKey { get; set; } = string.Empty;
}