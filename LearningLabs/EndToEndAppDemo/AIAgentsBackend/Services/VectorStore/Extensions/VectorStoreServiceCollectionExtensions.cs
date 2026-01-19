using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using MongoDB.Driver;
using AIAgentsBackend.Configuration;
using AIAgentsBackend.HostedServices;
using AIAgentsBackend.Services.VectorStore.Interfaces;

namespace AIAgentsBackend.Services.VectorStore.Extensions;

/// <summary>
/// Extension methods for registering vector store services.
/// </summary>
public static class VectorStoreServiceCollectionExtensions
{
    /// <summary>
    /// Adds vector store services to the service collection.
    /// Registers the embedding generator, MongoDB database, and all policy vector store services.
    /// </summary>
    public static IServiceCollection AddVectorStoreServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        services.Configure<VectorStoreSettings>(
            configuration.GetSection(VectorStoreSettings.SectionName));

        // Register embedding generator as singleton
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var azureSettings = sp.GetRequiredService<AzureOpenAISettings>();

            // Check if API key is provided, otherwise use DefaultAzureCredential
            if (!string.IsNullOrEmpty(azureSettings.APIKey))
            {
                var client = new AzureOpenAIClient(
                    new Uri(azureSettings.Endpoint),
                    new System.ClientModel.ApiKeyCredential(azureSettings.APIKey));

                return client
                    .GetEmbeddingClient(azureSettings.DefaultEmbeddingDeploymentName)
                    .AsIEmbeddingGenerator();
            }
            else
            {
                var client = new AzureOpenAIClient(
                    new Uri(azureSettings.Endpoint),
                    new DefaultAzureCredential());

                return client
                    .GetEmbeddingClient(azureSettings.DefaultEmbeddingDeploymentName)
                    .AsIEmbeddingGenerator();
            }
        });

        // Register MongoDB database (if not already registered)
        services.AddSingleton<IMongoDatabase>(sp =>
        {
            var mongoSettings = configuration.GetSection("MongoDB").Get<MongoDbSettings>()
                ?? throw new InvalidOperationException("MongoDB settings not found in configuration.");

            var mongoClient = new MongoClient(mongoSettings.ConnectionString);
            return mongoClient.GetDatabase(mongoSettings.DatabaseName);
        });

        // Register policy vector store services
        services.AddScoped<IReturnPolicyVectorStoreService, ReturnPolicyVectorStoreService>();
        services.AddScoped<IRefundPolicyVectorStoreService, RefundPolicyVectorStoreService>();
        services.AddScoped<IOrderCancellationPolicyVectorStoreService, OrderCancellationPolicyVectorStoreService>();

        // Register the hosted service for initialization
        services.AddHostedService<VectorStoreInitializerHostedService>();

        return services;
    }
}
