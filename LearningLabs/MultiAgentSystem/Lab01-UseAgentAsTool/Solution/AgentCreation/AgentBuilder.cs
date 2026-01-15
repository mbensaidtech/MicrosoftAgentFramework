using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using OpenAI;
using UseAgentAsTool.Configuration;

namespace UseAgentAsTool.AgentCreation;

/// <summary>
/// Fluent builder for creating AI agents with customizable configuration.
/// </summary>
public class AgentBuilder
{
    private readonly ChatClient chatClient;
    private readonly AgentSettings settings;
    private readonly List<AITool> tools = new();
    private Func<string, string>? instructionModifier;

    internal AgentBuilder(ChatClient chatClient, AgentSettings settings)
    {
        this.chatClient = chatClient;
        this.settings = settings;
    }

    /// <summary>
    /// Adds tools to the agent.
    /// </summary>
    public AgentBuilder WithTools(params AITool[] tools)
    {
        this.tools.AddRange(tools);
        return this;
    }

    /// <summary>
    /// Adds a custom instruction modifier to transform the base instructions.
    /// </summary>
    public AgentBuilder WithInstructionModifier(Func<string, string> modifier)
    {
        if (instructionModifier == null)
        {
            instructionModifier = modifier;
        }
        else
        {
            // Chain modifiers if multiple are added
            var existingModifier = instructionModifier;
            instructionModifier = instructions => modifier(existingModifier(instructions));
        }
        return this;
    }

    /// <summary>
    /// Builds and returns the configured agent.
    /// </summary>
    public ChatClientAgent Build()
    {
        // Apply instruction modifier if provided
        var instructions = instructionModifier != null
            ? instructionModifier(settings.Instructions)
            : settings.Instructions;

        // Create agent with or without tools
        if (tools.Count > 0)
        {
            return chatClient.CreateAIAgent(
                instructions: instructions,
                name: settings.Name,
                tools: tools);
        }

        return chatClient.CreateAIAgent(
            instructions: instructions,
            name: settings.Name);
    }
}

