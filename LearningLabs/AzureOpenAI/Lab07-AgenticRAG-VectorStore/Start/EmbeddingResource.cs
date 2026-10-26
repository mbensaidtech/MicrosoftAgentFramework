using System.ClientModel;
using System.ClientModel.Primitives;
using Azure.Identity;
using OpenAI;
using CommonUtilities;

namespace AgenticRAG;

/// <summary>
/// Chooses the Azure OpenAI resource of the embedding deployment. By default it is the resource of the chat deployment;
/// the optional settings <c>AzureOpenAI:EmbeddingEndpoint</c> and <c>AzureOpenAI:EmbeddingAPIKey</c> move it to another resource.
/// </summary>
public static class EmbeddingResource
{
    /// <summary>
    /// Returns the <see cref="OpenAIClient"/> to get the embedding client from:
    /// <list type="bullet">
    /// <item><c>EmbeddingEndpoint</c> not set: <paramref name="client"/> itself (the endpoint and key of the chat deployment).</item>
    /// <item><c>EmbeddingEndpoint</c> set: a new client for that endpoint, with <c>EmbeddingAPIKey</c> when it is set,
    /// otherwise Microsoft Entra ID. The API key of the chat deployment is never sent to another endpoint.</item>
    /// </list>
    /// </summary>
    public static OpenAIClient ForEmbeddings(this OpenAIClient client, AzureOpenAISettings settings)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrWhiteSpace(settings.EmbeddingEndpoint))
        {
            return client;
        }

        OpenAIClientOptions embeddingClientOptions = new() { Endpoint = AzureOpenAIEndpoint.ToV1Uri(settings.EmbeddingEndpoint) };

        // Same authentication rule as the client of the chat deployment, with the embedding settings only
#pragma warning disable OPENAI001
        return !string.IsNullOrWhiteSpace(settings.EmbeddingAPIKey)
            ? new OpenAIClient(new ApiKeyCredential(settings.EmbeddingAPIKey), embeddingClientOptions)
            : new OpenAIClient(new BearerTokenPolicy(new DefaultAzureCredential(), "https://ai.azure.com/.default"), embeddingClientOptions);
#pragma warning restore OPENAI001
    }
}
