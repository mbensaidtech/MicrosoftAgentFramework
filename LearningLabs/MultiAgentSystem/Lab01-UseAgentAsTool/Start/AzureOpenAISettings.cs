namespace UseAgentAsTool;

public class AzureOpenAISettings
{
    public string Endpoint { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
    public string? APIKey { get; set; }
}
