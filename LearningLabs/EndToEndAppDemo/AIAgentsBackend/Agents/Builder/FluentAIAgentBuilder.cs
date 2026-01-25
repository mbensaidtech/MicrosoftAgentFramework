using AIAgentsBackend.Configuration;
using Azure.AI.OpenAI;
using OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using AIExtensions = Microsoft.Extensions.AI;

namespace AIAgentsBackend.Agents.Builder;

/// <summary>
/// Fluent API builder for creating AIAgent instances.
/// Use this builder when you need structured output with ChatResponseFormat or middleware support.
/// </summary>
public sealed class FluentAIAgentBuilder : FluentAgentBuilderBase<FluentAIAgentBuilder, AIAgent>
{
    private AIExtensions.ChatResponseFormat? responseFormat;
    private readonly List<Func<AIAgent, FunctionInvocationContext, Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>>, CancellationToken, ValueTask<object?>>> middlewares = [];

    /// <summary>
    /// Creates a new FluentAIAgentBuilder using a shared AzureOpenAIClient singleton.
    /// </summary>
    public FluentAIAgentBuilder(AzureOpenAIClient azureClient, AzureOpenAISettings settings)
        : base(azureClient, settings)
    {
    }

    /// <summary>
    /// Sets the response format for structured output.
    /// Use ChatResponseFormat.ForJsonSchema() for JSON schema output.
    /// </summary>
    public FluentAIAgentBuilder WithResponseFormat(AIExtensions.ChatResponseFormat responseFormat)
    {
        this.responseFormat = responseFormat;
        return this;
    }

    /// <summary>
    /// Sets a JSON schema response format for structured output.
    /// Use response.Deserialize&lt;T&gt;() to get the typed result.
    /// </summary>
    public FluentAIAgentBuilder WithStructuredOutput<T>(string schemaName, string? schemaDescription = null)
    {
        var schema = AIJsonUtilities.CreateJsonSchema(typeof(T));
        responseFormat = AIExtensions.ChatResponseFormat.ForJsonSchema(schema, schemaName, schemaDescription);
        return this;
    }

    /// <summary>
    /// Adds a middleware to the agent for intercepting function calls.
    /// Middleware is useful for logging, monitoring, or modifying function invocations.
    /// </summary>
    public FluentAIAgentBuilder WithMiddleware(
        Func<AIAgent, FunctionInvocationContext, Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>>, CancellationToken, ValueTask<object?>> middleware)
    {
        middlewares.Add(middleware);
        return this;
    }

    /// <summary>
    /// Builds and returns the configured AIAgent with all middlewares applied.
    /// Creates agent with full options, then wraps with middleware using AsBuilder().
    /// </summary>
    public override AIAgent Build()
    {
        var chatClient = GetChatClient();
        var chatOptions = BuildChatOptions();

        // Add AIAgent-specific response format
        if (responseFormat is not null)
        {
            chatOptions.ResponseFormat = responseFormat;
        }

        var agentOptions = CreateAgentOptions(chatOptions);
        
        // Create the base agent with full options (including message store)
        var agentBuilder = chatClient.CreateAIAgent(agentOptions).AsBuilder();

        // Apply middlewares using the AsBuilder pattern
        // The agent must be created first with all options, then middleware is applied
        if (middlewares.Count > 0)
        {
            foreach (var middleware in middlewares)
            {
                agentBuilder = agentBuilder.Use(middleware);
            }
        }
        return agentBuilder.Build();
    }
}
