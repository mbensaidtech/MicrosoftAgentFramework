using A2A;
using AIAgentsBackend.Agents.Builder;
using AIAgentsBackend.Agents.Configuration;
using AIAgentsBackend.Agents.Models;
using AIAgentsBackend.Agents.Stores;
using AIAgentsBackend.Configuration;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.Connectors.MongoDB;

namespace AIAgentsBackend.Agents.Factory;

/// <summary>
/// Implementation of the agent factory.
/// Each method creates a specific agent with its agent card using the appropriate builder.
/// Agent cards are populated from configuration.
/// </summary>
public class AgentFactory : IAgentFactory
{
    private readonly AzureOpenAIClient azureClient;
    private readonly AzureOpenAISettings settings;
    private readonly AgentsConfiguration agentsConfig;
    private readonly MongoVectorStore mongoVectorStore;
    private readonly MongoDbSettings mongoDbSettings;
    private readonly IHttpContextAccessor httpContextAccessor;

    public AgentFactory(
        AzureOpenAIClient azureClient,
        AzureOpenAISettings settings,
        IOptions<AgentsConfiguration> agentsConfig,
        MongoVectorStore mongoVectorStore,
        MongoDbSettings mongoDbSettings,
        IHttpContextAccessor httpContextAccessor)
    {
        this.azureClient = azureClient;
        this.settings = settings;
        this.agentsConfig = agentsConfig.Value;
        this.mongoVectorStore = mongoVectorStore;
        this.mongoDbSettings = mongoDbSettings;
        this.httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public (AIAgent Agent, AgentCard Card) GetTranslationAgent()
    {
        var config = GetConfig("translation");

        var builder = new FluentAIAgentBuilder(azureClient, settings)
            .WithName(config.Name)
            .WithDescription(config.Description)
            .WithInstructions(config.Instructions)
            .WithTemperature(config.Temperature ?? 0.5f)
            .WithMaxOutputTokens(config.MaxOutputTokens ?? 1000)
            .WithStructuredOutput<TranslationResult>(
                schemaName: "TranslationResult",
                schemaDescription: "Translation output containing the translated text, source language, and target language");

        if (!string.IsNullOrWhiteSpace(config.ChatDeploymentName))
        {
            builder.WithDeployment(config.ChatDeploymentName);
        }

        var agent = builder.Build();
        var card = CreateAgentCard(config);

        return (agent, card);
    }

    /// <inheritdoc />
    public (ChatClientAgent Agent, AgentCard Card) GetCustomerSupportAgent()
    {
        var config = GetConfig("customer-support");

        var builder = new FluentChatClientAgentBuilder(azureClient, settings)
            .WithName(config.Name)
            .WithDescription(config.Description)
            .WithInstructions(config.Instructions)
            .WithTemperature(config.Temperature ?? 0.7f)
            .WithMaxOutputTokens(config.MaxOutputTokens ?? 800);

        if (!string.IsNullOrWhiteSpace(config.ChatDeploymentName))
        {
            builder.WithDeployment(config.ChatDeploymentName);
        }

        var agent = builder.Build();
        var card = CreateAgentCard(config);

        return (agent, card);
    }

    /// <inheritdoc />
    public (ChatClientAgent Agent, AgentCard Card) GetHistoryAgent()
    {
        var config = GetConfig("history");

        var builder = new FluentChatClientAgentBuilder(azureClient, settings)
            .WithName(config.Name)
            .WithDescription(config.Description)
            .WithInstructions(config.Instructions)
            .WithTemperature(config.Temperature ?? 0.7f)
            .WithMaxOutputTokens(config.MaxOutputTokens ?? 1500)
            .WithChatMessageStoreFactory(ctx => new MongoVectorChatMessageStore(
                mongoVectorStore,
                httpContextAccessor,
                mongoDbSettings.ChatMessageStoreCollectionName,
                ctx.JsonSerializerOptions));

        if (!string.IsNullOrWhiteSpace(config.ChatDeploymentName))
        {
            builder.WithDeployment(config.ChatDeploymentName);
        }

        var agent = builder.Build();
        var card = CreateAgentCard(config);

        return (agent, card);
    }

    private AgentConfiguration GetConfig(string agentId)
    {
        if (!agentsConfig.Agents.TryGetValue(agentId, out var config))
        {
            throw new KeyNotFoundException(
                $"Agent configuration '{agentId}' not found in appsettings.json. " +
                $"Available agents: {string.Join(", ", agentsConfig.Agents.Keys)}");
        }
        return config;
    }

    /// <summary>
    /// Creates an AgentCard from the agent configuration.
    /// </summary>
    private static AgentCard CreateAgentCard(AgentConfiguration config)
    {
        var skill = new AgentSkill
        {
            Id = config.SkillId,
            Name = config.SkillName,
            Description = config.SkillDescription,
            Tags = config.Tags,
            Examples = config.Examples
        };

        return new AgentCard
        {
            Name = config.Name.Replace(" ", ""),
            Description = config.Description,
            Version = config.Version,
            DefaultInputModes = ["text"],
            DefaultOutputModes = ["text"],
            Capabilities = new AgentCapabilities
            {
                Streaming = config.Streaming,
                PushNotifications = false
            },
            Skills = [skill],
            Url = config.Url
        };
    }
}
