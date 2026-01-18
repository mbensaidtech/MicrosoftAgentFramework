using AIAgentsBackend.Agents.Configuration;
using AIAgentsBackend.Agents.Factory;
using AIAgentsBackend.Configuration;
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

        services.AddSingleton<IMongoDatabase>(sp =>
        {
            var settings = sp.GetRequiredService<MongoDbSettings>();
            var client = new MongoClient(settings.ConnectionString);
            return client.GetDatabase(settings.DatabaseName);
        });

        services.AddSingleton<MongoVectorStore>(sp =>
        {
            var database = sp.GetRequiredService<IMongoDatabase>();
            return new MongoVectorStore(database);
        });

        // Register HttpContextAccessor to access request context
        services.AddHttpContextAccessor();

        // Register agent configurations from appsettings.json
        services.Configure<AgentsConfiguration>(configuration.GetSection("AIAgents"));

        // Register the agent factory
        services.AddSingleton<IAgentFactory, AgentFactory>();

        return services;
    }
}
