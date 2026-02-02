using AIAgentsBackend.Agents.Configuration;
using AIAgentsBackend.Agents.Factory;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AIAgentsBackend.Agents.Tools;

/// <summary>
/// Provides tools for the Orchestrator Agent by converting specialized agents into callable tools.
/// </summary>
public static class OrchestratorTools
{
    /// <summary>
    /// Converts the OrderAgent to a tool with detailed description for the orchestrator.
    /// </summary>
    public static AITool CreateOrderAgentTool(
        ChatClientAgent orderAgent,
        ToolConfiguration? toolConfig = null)
    {
        var config = toolConfig ?? new ToolConfiguration
        {
            Name = "order_agent",
            Description = "Agent for ORDERS and STATUS."
        };

        return orderAgent.AsAIFunction(new AIFunctionFactoryOptions
        {
            Name = config.Name,
            Description = config.Description
        });
    }

    /// <summary>
    /// Converts the PolicyAgent to a tool with detailed description for the orchestrator.
    /// </summary>
    public static AITool CreatePolicyAgentTool(
        ChatClientAgent policyAgent,
        ToolConfiguration? toolConfig = null)
    {
        var config = toolConfig ?? new ToolConfiguration
        {
            Name = "policy_agent",
            Description = "Agent specialized in POLICIES and CONDITIONS."
        };

        return policyAgent.AsAIFunction(new AIFunctionFactoryOptions
        {
            Name = config.Name,
            Description = config.Description
        });
    }

    /// <summary>
    /// Converts the MessageFormulatorAgent to a tool with detailed description for the orchestrator.
    /// </summary>
    public static AITool CreateMessageFormulatorAgentTool(
        ChatClientAgent messageFormulatorAgent,
        ToolConfiguration? toolConfig = null)
    {
        var config = toolConfig ?? new ToolConfiguration
        {
            Name = "message_formulator_agent",
            Description = "Agent to HELP WRITE a message to the SELLER."
        };

        return messageFormulatorAgent.AsAIFunction(new AIFunctionFactoryOptions
        {
            Name = config.Name,
            Description = config.Description
        });
    }
}
