using A2A;
using Microsoft.Agents.AI;

namespace AIAgentsBackend.Agents.Factory;

/// <summary>
/// Factory for creating AI agent instances with their agent cards.
/// Each agent has a dedicated method that returns a configured agent and its A2A card.
/// </summary>
public interface IAgentFactory
{
    /// <summary>
    /// Creates a Translation Agent instance with its agent card.
    /// Uses structured output (JSON schema).
    /// </summary>
    /// <returns>A tuple containing the configured AIAgent and its AgentCard.</returns>
    (AIAgent Agent, AgentCard Card) GetTranslationAgent();

    /// <summary>
    /// Creates a Customer Support Agent instance with its agent card.
    /// </summary>
    /// <returns>A tuple containing the configured ChatClientAgent and its AgentCard.</returns>
    (ChatClientAgent Agent, AgentCard Card) GetCustomerSupportAgent();

    /// <summary>
    /// Creates a History Agent instance with its agent card.
    /// This agent has MongoDB conversation persistence.
    /// </summary>
    /// <returns>A tuple containing the configured ChatClientAgent and its AgentCard.</returns>
    (ChatClientAgent Agent, AgentCard Card) GetHistoryAgent();
}
