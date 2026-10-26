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

    /// <summary>
    /// Gets or sets the endpoint of the Azure OpenAI resource of the embedding deployment (optional).
    /// If not set, the embeddings use the resource of the chat deployment (<see cref="Endpoint"/> and <see cref="APIKey"/>).
    /// </summary>
    public string? EmbeddingEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the API key of the embedding resource (optional, used only with <see cref="EmbeddingEndpoint"/>).
    /// If not set, DefaultAzureCredential (Microsoft Entra ID) is used: <see cref="APIKey"/> is never sent to another endpoint.
    /// </summary>
    public string? EmbeddingAPIKey { get; set; }
}
