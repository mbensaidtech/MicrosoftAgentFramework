using A2A;
using AIAgentsBackend.Agents.Builder;
using AIAgentsBackend.Agents.Configuration;
using AIAgentsBackend.Middlewares.Agent;
using AIAgentsBackend.Agents.Models;
using AIAgentsBackend.Agents.Stores;
using AIAgentsBackend.Agents.Tools;
using AIAgentsBackend.Configuration;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.Connectors.MongoDB;
using OpenAI.Chat;

namespace AIAgentsBackend.Agents.Factory;

/// <summary>
/// Creates and configures AI agents with their A2A cards.
/// </summary>
public class AgentFactory : IAgentFactory
{
    private readonly AzureOpenAIClient azureClient;
    private readonly AzureOpenAISettings settings;
    private readonly AgentsConfiguration agentsConfig;
    private readonly MongoVectorStore mongoVectorStore;
    private readonly MongoDbSettings mongoDbSettings;
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly IServiceProvider serviceProvider;

    public AgentFactory(
        AzureOpenAIClient azureClient,
        AzureOpenAISettings settings,
        IOptions<AgentsConfiguration> agentsConfig,
        MongoVectorStore mongoVectorStore,
        MongoDbSettings mongoDbSettings,
        IHttpContextAccessor httpContextAccessor,
        IServiceProvider serviceProvider)
    {
        this.azureClient = azureClient;
        this.settings = settings;
        this.agentsConfig = agentsConfig.Value;
        this.mongoVectorStore = mongoVectorStore;
        this.mongoDbSettings = mongoDbSettings;
        this.httpContextAccessor = httpContextAccessor;
        this.serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Creates a translation agent that outputs structured JSON with source/target languages.
    /// </summary>
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

    /// <summary>
    /// Creates a customer support agent for handling user inquiries.
    /// Includes tools for searching return, refund, and order cancellation policies.
    /// </summary>
    public (ChatClientAgent Agent, AgentCard Card) GetCustomerSupportAgent()
    {
        var config = GetConfig("customer-support");
        var tools = new CustomerSupportTools(serviceProvider);

        var builder = new FluentChatClientAgentBuilder(azureClient, settings)
            .WithName(config.Name)
            .WithDescription(config.Description)
            .WithInstructions(config.Instructions)
            .WithTemperature(config.Temperature ?? 0.7f)
            .WithMaxOutputTokens(config.MaxOutputTokens ?? 800)
            .WithToolFromMethod(tools.SearchReturnPolicyAsync, "SearchReturnPolicy")
            .WithToolFromMethod(tools.SearchRefundPolicyAsync, "SearchRefundPolicy")
            .WithToolFromMethod(tools.SearchOrderCancellationPolicyAsync, "SearchOrderCancellationPolicy");

        if (!string.IsNullOrWhiteSpace(config.ChatDeploymentName))
        {
            builder.WithDeployment(config.ChatDeploymentName);
        }

        var agent = builder.Build();
        var card = CreateAgentCard(config);

        return (agent, card);
    }

    /// <summary>
    /// Creates a history expert agent with MongoDB conversation memory.
    /// </summary>
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

    /// <summary>
    /// Creates an order agent that handles order data and order actions.
    /// This agent is stateless (no message store) and is used as a tool by the orchestrator.
    /// </summary>
    public (ChatClientAgent Agent, AgentCard Card) GetOrderAgent()
    {
        var config = GetConfig("order");
        var tools = new OrderTools(serviceProvider);

        var builder = new FluentChatClientAgentBuilder(azureClient, settings)
            .WithName(config.Name)
            .WithDescription(config.Description)
            .WithInstructions(config.Instructions)
            .WithTemperature(config.Temperature ?? 0.5f)
            .WithMaxOutputTokens(config.MaxOutputTokens ?? 1000)
            .WithToolFromMethod(tools.GetOrderByIdAsync, "GetOrderById")
            .WithToolFromMethod(tools.GetOrderStatusAsync, "GetOrderStatus")
            .WithToolFromMethod(tools.SearchOrdersByCustomerAsync, "SearchOrdersByCustomer");

        if (!string.IsNullOrWhiteSpace(config.ChatDeploymentName))
        {
            builder.WithDeployment(config.ChatDeploymentName);
        }

        var agent = builder.Build();
        var card = CreateAgentCard(config);

        return (agent, card);
    }

    /// <summary>
    /// Creates a policy agent that handles return, refund, and cancellation policies.
    /// This agent is stateless (no message store) and is used as a tool by the orchestrator.
    /// </summary>
    public (ChatClientAgent Agent, AgentCard Card) GetPolicyAgent()
    {
        var config = GetConfig("policy");
        var tools = new PolicyTools(serviceProvider);

        var builder = new FluentChatClientAgentBuilder(azureClient, settings)
            .WithName(config.Name)
            .WithDescription(config.Description)
            .WithInstructions(config.Instructions)
            .WithTemperature(config.Temperature ?? 0.5f)
            .WithMaxOutputTokens(config.MaxOutputTokens ?? 1200)
            .WithToolFromMethod(tools.SearchReturnPolicyAsync, "SearchReturnPolicy")
            .WithToolFromMethod(tools.SearchRefundPolicyAsync, "SearchRefundPolicy")
            .WithToolFromMethod(tools.SearchOrderCancellationPolicyAsync, "SearchOrderCancellationPolicy");

        if (!string.IsNullOrWhiteSpace(config.ChatDeploymentName))
        {
            builder.WithDeployment(config.ChatDeploymentName);
        }

        var agent = builder.Build();
        var card = CreateAgentCard(config);

        return (agent, card);
    }

    /// <summary>
    /// Creates a message formulator agent that helps customers compose messages to sellers.
    /// This agent is stateless (no message store) and is used as a tool by the orchestrator.
    /// </summary>
    public (ChatClientAgent Agent, AgentCard Card) GetMessageFormulatorAgent()
    {
        var config = GetConfig("message-formulator");
        var tools = new MessageFormulatorTools(serviceProvider);

        var builder = new FluentChatClientAgentBuilder(azureClient, settings)
            .WithName(config.Name)
            .WithDescription(config.Description)
            .WithInstructions(config.Instructions)
            .WithTemperature(config.Temperature ?? 0.7f)
            .WithMaxOutputTokens(config.MaxOutputTokens ?? 1500)
            .WithToolFromMethod(tools.SearchSellerRequirementsAsync, "SearchSellerRequirements");

        if (!string.IsNullOrWhiteSpace(config.ChatDeploymentName))
        {
            builder.WithDeployment(config.ChatDeploymentName);
        }

        var agent = builder.Build();
        var card = CreateAgentCard(config);

        return (agent, card);
    }

    /// <summary>
    /// Creates an orchestrator agent that coordinates the specialized agents.
    /// This is the ONLY agent with a message store for conversation memory.
    /// Uses AIAgent (not ChatClientAgent) to support middleware for function call logging.
    /// </summary>
    public (AIAgent Agent, AgentCard Card) GetOrchestratorAgent()
    {
        var config = GetConfig("orchestrator");

        var (orderAgent, _) = GetOrderAgent();
        var (policyAgent, _) = GetPolicyAgent();
        var (messageFormulatorAgent, _) = GetMessageFormulatorAgent();

        // Get tool configurations from appsettings.json
        var orderToolConfig = agentsConfig.OrchestratorTools.Tools.TryGetValue("order_agent", out var orderConfig) ? orderConfig : null;
        var policyToolConfig = agentsConfig.OrchestratorTools.Tools.TryGetValue("policy_agent", out var policyConfig) ? policyConfig : null;
        var messageFormulatorToolConfig = agentsConfig.OrchestratorTools.Tools.TryGetValue("message_formulator_agent", out var msgConfig) ? msgConfig : null;

        var orderAgentTool = OrchestratorTools.CreateOrderAgentTool(orderAgent, orderToolConfig);
        var policyAgentTool = OrchestratorTools.CreatePolicyAgentTool(policyAgent, policyToolConfig);
        var messageFormulatorAgentTool = OrchestratorTools.CreateMessageFormulatorAgentTool(messageFormulatorAgent, messageFormulatorToolConfig);

        var builder = new FluentAIAgentBuilder(azureClient, settings)
            .WithName(config.Name)
            .WithDescription(config.Description)
            .WithInstructions(config.Instructions)
            .WithTemperature(config.Temperature ?? 0.3f)
            .WithMaxOutputTokens(config.MaxOutputTokens ?? 2000)
            .WithChatMessageStoreFactory(ctx => new MongoVectorChatMessageStore(
                mongoVectorStore,
                httpContextAccessor,
                mongoDbSettings.ChatMessageStoreCollectionName,
                ctx.JsonSerializerOptions))
            .WithTool(orderAgentTool)
            .WithTool(policyAgentTool)
            .WithTool(messageFormulatorAgentTool)
            .WithMiddleware(FunctionCallLoggingMiddleware.LogFunctionCallAsync);

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
    /// Builds an A2A agent card from the configuration.
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
