using AIAgentsBackend.Agents.Configuration;
using AIAgentsBackend.Agents.Factory;
using AIAgentsBackend.Configuration;
using AIAgentsBackend.HostedServices;
using AIAgentsBackend.Repositories;
using AIAgentsBackend.Services;
using AIAgentsBackend.Services.Conversation;
using AIAgentsBackend.Services.MessageContext;
using AIAgentsBackend.Services.VectorStore;
using AIAgentsBackend.Services.VectorStore.Interfaces;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Connectors.MongoDB;
using MongoDB.Driver;
using System.ClientModel;

namespace AIAgentsBackend.Extensions;

/// <summary>
/// Registers all application services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Azure OpenAI, MongoDB, agents, vector store, and order services.
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAzureOpenAIServices(configuration);
        services.AddMongoDbServices(configuration);
        services.AddSecurityServices(configuration);
        services.AddAgentServices(configuration);
        services.AddVectorStoreServices(configuration);
        services.AddOrderServices(configuration);

        return services;
    }

    private static IServiceCollection AddAzureOpenAIServices(this IServiceCollection services, IConfiguration configuration)
    {
        var azureSettings = configuration.GetSection("AzureOpenAI").Get<AzureOpenAISettings>() 
            ?? new AzureOpenAISettings();
        services.AddSingleton(azureSettings);

        services.AddSingleton<AzureOpenAIClient>(sp =>
        {
            var settings = sp.GetRequiredService<AzureOpenAISettings>();

            if (!string.IsNullOrEmpty(settings.APIKey))
            {
                return new AzureOpenAIClient(new Uri(settings.Endpoint), new ApiKeyCredential(settings.APIKey));
            }
            else
            {
                return new AzureOpenAIClient(new Uri(settings.Endpoint), new DefaultAzureCredential());
            }
        });

        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var azureSettings = sp.GetRequiredService<AzureOpenAISettings>();
            var client = sp.GetRequiredService<AzureOpenAIClient>();

            return client
                .GetEmbeddingClient(azureSettings.DefaultEmbeddingDeploymentName)
                .AsIEmbeddingGenerator();
        });

        return services;
    }

    private static IServiceCollection AddMongoDbServices(this IServiceCollection services, IConfiguration configuration)
    {
        var mongoSettings = configuration.GetSection("MongoDB").Get<MongoDbSettings>() 
            ?? new MongoDbSettings();
        services.AddSingleton(mongoSettings);
        services.Configure<MongoDbSettings>(configuration.GetSection("MongoDB"));

        services.AddSingleton<IMongoClient>(sp =>
        {
            var settings = sp.GetRequiredService<MongoDbSettings>();
            return new MongoClient(settings.ConnectionString);
        });

        services.AddSingleton<IMongoDatabase>(sp =>
        {
            var settings = sp.GetRequiredService<MongoDbSettings>();
            var client = sp.GetRequiredService<IMongoClient>();
            return client.GetDatabase(settings.DatabaseName);
        });

        services.AddSingleton<MongoVectorStore>(sp =>
        {
            var database = sp.GetRequiredService<IMongoDatabase>();
            return new MongoVectorStore(database);
        });

        services.AddScoped<IThreadRepository, ThreadRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<IMessageContextService, MessageContextService>();

        return services;
    }

    private static IServiceCollection AddSecurityServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SecuritySettings>(configuration.GetSection("Security"));
        services.AddSingleton<IContextIdValidator, ContextIdValidator>();
        services.AddHttpContextAccessor();

        return services;
    }

    private static IServiceCollection AddAgentServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AgentsConfiguration>(configuration.GetSection("AIAgents"));
        services.AddSingleton<IAgentFactory, AgentFactory>();

        return services;
    }

    private static IServiceCollection AddVectorStoreServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<VectorStoreSettings>(configuration.GetSection(VectorStoreSettings.SectionName));

        services.AddScoped<IReturnPolicyVectorStoreService, ReturnPolicyVectorStoreService>();
        services.AddScoped<IRefundPolicyVectorStoreService, RefundPolicyVectorStoreService>();
        services.AddScoped<IOrderCancellationPolicyVectorStoreService, OrderCancellationPolicyVectorStoreService>();
        services.AddScoped<ISellerRequirementsVectorStoreService, SellerRequirementsVectorStoreService>();

        services.AddHostedService<VectorStoreInitializerHostedService>();

        return services;
    }

    private static IServiceCollection AddOrderServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OrderDataSettings>(configuration.GetSection(OrderDataSettings.SectionName));
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddHostedService<OrderDataInitializerHostedService>();

        return services;
    }
}
