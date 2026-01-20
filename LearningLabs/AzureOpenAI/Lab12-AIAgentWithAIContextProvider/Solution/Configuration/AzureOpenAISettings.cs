namespace AIAgentWithAIContextProvider.Configuration;

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
    /// Gets or sets the Azure OpenAI deployment name.
    /// </summary>
    public string ChatDeploymentName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the API key. If not set, DefaultAzureCredential is used.
    /// </summary>
    public string? APIKey { get; set; }
}
