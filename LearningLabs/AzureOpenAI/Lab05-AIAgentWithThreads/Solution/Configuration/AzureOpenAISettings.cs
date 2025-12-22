namespace AIAgentWithThreads.Configuration;

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
    public string DeploymentName { get; set; } = string.Empty;
}
