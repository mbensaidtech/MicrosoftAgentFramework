namespace AIAgentWithMCPClient;

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
}
