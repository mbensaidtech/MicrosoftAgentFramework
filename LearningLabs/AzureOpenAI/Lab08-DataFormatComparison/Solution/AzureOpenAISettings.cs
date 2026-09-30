namespace DataFormatComparison;

public class AzureOpenAISettings
{
    /// <summary>Azure OpenAI resource endpoint, e.g. https://my-resource.openai.azure.com/</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Name of the chat model deployment (used as the "model" by the OpenAI SDK).</summary>
    public string ChatDeploymentName { get; set; } = string.Empty;

    /// <summary>Optional API key. Keep it in user secrets or an environment variable, never in appsettings.json.
    /// When empty, Microsoft Entra ID (DefaultAzureCredential) is used.</summary>
    public string? APIKey { get; set; }
}
