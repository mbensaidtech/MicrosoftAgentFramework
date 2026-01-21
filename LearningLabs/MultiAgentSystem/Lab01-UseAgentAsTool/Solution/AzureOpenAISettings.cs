namespace UseAgentAsTool;

public class AzureOpenAISettings
{
    public string Endpoint { get; set; } = string.Empty;
    public string DefaultDeploymentName { get; set; } = string.Empty;
    public string? APIKey { get; set; }
}
