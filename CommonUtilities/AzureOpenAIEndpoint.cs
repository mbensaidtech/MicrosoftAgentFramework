namespace CommonUtilities;

/// <summary>
/// Helper for building the Azure OpenAI v1 endpoint used by the official OpenAI SDK.
/// </summary>
public static class AzureOpenAIEndpoint
{
    private const string V1Path = "/openai/v1";

    /// <summary>
    /// Converts an Azure OpenAI resource endpoint (as shown in the Azure portal, e.g.
    /// <c>https://my-resource.openai.azure.com/</c>) into its v1 API endpoint
    /// (<c>https://my-resource.openai.azure.com/openai/v1/</c>).
    /// Endpoints that already end with <c>/openai/v1</c> are returned unchanged.
    /// </summary>
    /// <param name="endpoint">The Azure OpenAI resource endpoint or v1 endpoint.</param>
    /// <returns>The v1 endpoint to pass to <c>OpenAIClientOptions.Endpoint</c>.</returns>
    public static Uri ToV1Uri(string endpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        Uri endpointUri = new(endpoint, UriKind.Absolute);
        string path = endpointUri.AbsolutePath.TrimEnd('/');

        if (path.EndsWith(V1Path, StringComparison.OrdinalIgnoreCase))
        {
            return endpointUri;
        }

        return new UriBuilder(endpointUri) { Path = $"{path}{V1Path}/" }.Uri;
    }
}
