namespace AgenticRAG;

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
    /// Gets or sets the Azure OpenAI chat deployment name.
    /// </summary>
    public string ChatDeploymentName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Azure OpenAI embedding deployment name (for example text-embedding-3-small).
    /// </summary>
    public string EmbeddingDeploymentName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the API key. If not set, DefaultAzureCredential (Microsoft Entra ID) is used.
    /// </summary>
    public string? APIKey { get; set; }
}
