using A2A;
using Microsoft.Agents.AI;

namespace AIAgentsBackend.Agents.Factory;

/// <summary>
/// Creates AI agents with their A2A cards.
/// </summary>
public interface IAgentFactory
{
    /// <summary>
    /// Creates a translation agent that outputs structured JSON.
    /// </summary>
    (AIAgent Agent, AgentCard Card) GetTranslationAgent();

    /// <summary>
    /// Creates a customer support agent for handling inquiries.
    /// </summary>
    (ChatClientAgent Agent, AgentCard Card) GetCustomerSupportAgent();

    /// <summary>
    /// Creates a history expert agent with conversation memory.
    /// </summary>
    (ChatClientAgent Agent, AgentCard Card) GetHistoryAgent();
}
