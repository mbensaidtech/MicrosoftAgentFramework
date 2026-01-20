namespace AIAgentWithFunctionTools;

public class AzureOpenAISettings
{
    public string Endpoint { get; set; } = string.Empty;
    public string ChatDeploymentName { get; set; } = string.Empty;
    public string? APIKey { get; set; }
}
