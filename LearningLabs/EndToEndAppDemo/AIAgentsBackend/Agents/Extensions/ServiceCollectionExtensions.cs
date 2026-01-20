using AIAgentsBackend.Agents.Configuration;
using AIAgentsBackend.Agents.Factory;
using AIAgentsBackend.Configuration;
using AIAgentsBackend.Repositories;
using AIAgentsBackend.Services;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.SemanticKernel.Connectors.MongoDB;
using MongoDB.Driver;
using System.ClientModel;

namespace AIAgentsBackend.Agents.Extensions;

/// <summary>
/// Extension methods for registering AI agent services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all AI agent services including configuration and factory.
    /// Agent configurations are loaded from the "AIAgents" section in appsettings.json.
    /// </summary>
    public static IServiceCollection AddAIAgents(this IServiceCollection services, IConfiguration configuration)
    {
        // Register Azure OpenAI settings
        var azureSettings = configuration.GetSection("AzureOpenAI").Get<AzureOpenAISettings>() 
            ?? new AzureOpenAISettings();
        services.AddSingleton(azureSettings);

        // Register AzureOpenAIClient as singleton (shared across all agents)
        services.AddSingleton<AzureOpenAIClient>(sp =>
        {
            var settings = sp.GetRequiredService<AzureOpenAISettings>();
            return new AzureOpenAIClient(new Uri(settings.Endpoint), new ApiKeyCredential(settings.APIKey));
        });

        // Register MongoDB settings and client
        var mongoSettings = configuration.GetSection("MongoDB").Get<MongoDbSettings>() 
            ?? new MongoDbSettings();
        services.AddSingleton(mongoSettings);
        services.Configure<MongoDbSettings>(configuration.GetSection("MongoDB"));

        // Register MongoDB client (single source of truth for MongoDB access)
        services.AddSingleton<IMongoClient>(sp =>
        {
            var settings = sp.GetRequiredService<MongoDbSettings>();
            return new MongoClient(settings.ConnectionString);
        });

        // Register MongoVectorStore for Semantic Kernel (gets database from IMongoClient)
        services.AddSingleton<MongoVectorStore>(sp =>
        {
            var settings = sp.GetRequiredService<MongoDbSettings>();
            var client = sp.GetRequiredService<IMongoClient>();
            var database = client.GetDatabase(settings.DatabaseName);
            return new MongoVectorStore(database);
        });

        // Register Thread Repository for MongoDB data access
        services.AddScoped<IThreadRepository, ThreadRepository>();

        // Register HttpContextAccessor to access request context
        services.AddHttpContextAccessor();

        // Register agent configurations from appsettings.json
        services.Configure<AgentsConfiguration>(configuration.GetSection("AIAgents"));

        // Register security settings for context ID signing
        services.Configure<SecuritySettings>(configuration.GetSection("Security"));

        // Register context ID validator service as Singleton (stateless, can be reused)
        services.AddSingleton<IContextIdValidator, ContextIdValidator>();

        // Register the agent factory
        services.AddSingleton<IAgentFactory, AgentFactory>();

        return services;
    }
}
