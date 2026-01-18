using AIAgentsBackend.Configuration;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

namespace AIAgentsBackend.Agents.Builder;

/// <summary>
/// Abstract base builder containing shared configuration for AI agents.
/// Use FluentAIAgentBuilder or FluentChatClientAgentBuilder to create agents.
/// </summary>
/// <typeparam name="TBuilder">The concrete builder type for fluent API.</typeparam>
/// <typeparam name="TAgent">The agent type to build.</typeparam>
public abstract class FluentAgentBuilderBase<TBuilder, TAgent>
    where TBuilder : FluentAgentBuilderBase<TBuilder, TAgent>
    where TAgent : class
{
    protected readonly AzureOpenAIClient azureClient;
    protected readonly AzureOpenAISettings settings;
    protected string? deploymentName;

    protected string? name;
    protected string? description;
    protected string? instructions;
    protected readonly List<AITool> tools = [];
    protected int? maxOutputTokens;
    protected float? temperature;
    protected float? topP;

    // Shared options
    protected Func<AIContextProvider>? contextProviderFactory;
    protected Func<ChatClientAgentOptions.ChatMessageStoreFactoryContext, ChatMessageStore>? chatMessageStoreFactory;

    /// <summary>
    /// Creates a new builder using a shared AzureOpenAIClient singleton.
    /// </summary>
    protected FluentAgentBuilderBase(AzureOpenAIClient azureClient, AzureOpenAISettings settings)
    {
        this.azureClient = azureClient ?? throw new ArgumentNullException(nameof(azureClient));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Sets the deployment name to use. If not called, the default from settings is used.
    /// </summary>
    public TBuilder WithDeployment(string deploymentName)
    {
        this.deploymentName = deploymentName;
        return (TBuilder)this;
    }

    /// <summary>
    /// Sets the name of the agent.
    /// </summary>
    public TBuilder WithName(string name)
    {
        this.name = name;
        return (TBuilder)this;
    }

    /// <summary>
    /// Sets the description of the agent.
    /// </summary>
    public TBuilder WithDescription(string description)
    {
        this.description = description;
        return (TBuilder)this;
    }

    /// <summary>
    /// Sets the instructions (system prompt) for the agent.
    /// </summary>
    public TBuilder WithInstructions(string instructions)
    {
        this.instructions = instructions;
        return (TBuilder)this;
    }

    /// <summary>
    /// Adds a single tool to the agent.
    /// </summary>
    public TBuilder WithTool(AITool tool)
    {
        tools.Add(tool);
        return (TBuilder)this;
    }

    /// <summary>
    /// Adds multiple tools to the agent.
    /// </summary>
    public TBuilder WithTools(IEnumerable<AITool> tools)
    {
        this.tools.AddRange(tools);
        return (TBuilder)this;
    }

    /// <summary>
    /// Adds a function as a tool to the agent.
    /// </summary>
    public TBuilder WithToolFromMethod(Delegate method, string? name = null)
    {
        var tool = name is not null
            ? AIFunctionFactory.Create(method, name)
            : AIFunctionFactory.Create(method);
        tools.Add(tool);
        return (TBuilder)this;
    }

    /// <summary>
    /// Sets the maximum output tokens for responses.
    /// </summary>
    public TBuilder WithMaxOutputTokens(int maxTokens)
    {
        maxOutputTokens = maxTokens;
        return (TBuilder)this;
    }

    /// <summary>
    /// Sets the temperature for response generation (0.0 - 2.0).
    /// </summary>
    public TBuilder WithTemperature(float temperature)
    {
        this.temperature = temperature;
        return (TBuilder)this;
    }

    /// <summary>
    /// Sets the top-p (nucleus sampling) value.
    /// </summary>
    public TBuilder WithTopP(float topP)
    {
        this.topP = topP;
        return (TBuilder)this;
    }

    /// <summary>
    /// Sets an AI context provider factory for memory/context management.
    /// </summary>
    public TBuilder WithContextProvider(Func<AIContextProvider> factory)
    {
        contextProviderFactory = factory;
        return (TBuilder)this;
    }

    /// <summary>
    /// Sets a chat message store factory for thread/conversation persistence.
    /// </summary>
    public TBuilder WithChatMessageStoreFactory(
        Func<ChatClientAgentOptions.ChatMessageStoreFactoryContext, ChatMessageStore> factory)
    {
        chatMessageStoreFactory = factory;
        return (TBuilder)this;
    }

    /// <summary>
    /// Builds and returns the configured agent.
    /// </summary>
    public abstract TAgent Build();

    /// <summary>
    /// Gets the ChatClient using the specified deployment or the default.
    /// </summary>
    protected ChatClient GetChatClient()
    {
        var deployment = deploymentName ?? settings.DefaultChatDeploymentName;

        if (string.IsNullOrWhiteSpace(deployment))
        {
            throw new InvalidOperationException(
                "No deployment specified. Use WithDeployment() or set DefaultChatDeploymentName in settings.");
        }

        return azureClient.GetChatClient(deployment);
    }

    /// <summary>
    /// Builds the ChatOptions from the configured values.
    /// </summary>
    protected ChatOptions BuildChatOptions()
    {
        var options = new ChatOptions
        {
            Instructions = instructions
        };

        if (tools.Count > 0)
        {
            options.Tools = tools;
        }

        if (maxOutputTokens.HasValue)
        {
            options.MaxOutputTokens = maxOutputTokens.Value;
        }

        if (temperature.HasValue)
        {
            options.Temperature = temperature.Value;
        }

        if (topP.HasValue)
        {
            options.TopP = topP.Value;
        }

        return options;
    }

    /// <summary>
    /// Creates the agent options with common configuration.
    /// </summary>
    protected ChatClientAgentOptions CreateAgentOptions(ChatOptions chatOptions)
    {
        var agentOptions = new ChatClientAgentOptions
        {
            Name = name,
            Description = description,
            ChatOptions = chatOptions
        };

        if (contextProviderFactory is not null)
        {
            agentOptions.AIContextProviderFactory = _ => contextProviderFactory();
        }

        if (chatMessageStoreFactory is not null)
        {
            agentOptions.ChatMessageStoreFactory = chatMessageStoreFactory;
        }

        return agentOptions;
    }
}
