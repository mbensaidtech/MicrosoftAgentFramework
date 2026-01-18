using AIAgentsBackend.Configuration;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using OpenAI;
namespace AIAgentsBackend.Agents.Builder;

/// <summary>
/// Fluent API builder for creating ChatClientAgent instances.
/// Use this builder when you need RunAsync&lt;T&gt;() for structured output.
/// </summary>
public sealed class FluentChatClientAgentBuilder : FluentAgentBuilderBase<FluentChatClientAgentBuilder, ChatClientAgent>
{
    /// <summary>
    /// Creates a new FluentChatClientAgentBuilder using a shared AzureOpenAIClient singleton.
    /// </summary>
    public FluentChatClientAgentBuilder(AzureOpenAIClient azureClient, AzureOpenAISettings settings)
        : base(azureClient, settings)
    {
    }

    /// <summary>
    /// Builds and returns the configured ChatClientAgent.
    /// </summary>
    public override ChatClientAgent Build()
    {
        var chatClient = GetChatClient();
        var chatOptions = BuildChatOptions();
        var agentOptions = CreateAgentOptions(chatOptions);
        return (ChatClientAgent)chatClient.CreateAIAgent(agentOptions);
    }
}
