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

    /// <summary>
    /// Creates an order agent that handles order data and order actions.
    /// </summary>
    (ChatClientAgent Agent, AgentCard Card) GetOrderAgent();

    /// <summary>
    /// Creates a policy agent that handles return, refund, and cancellation policies.
    /// </summary>
    (ChatClientAgent Agent, AgentCard Card) GetPolicyAgent();

    /// <summary>
    /// Creates a message formulator agent that helps customers compose messages to sellers.
    /// </summary>
    (ChatClientAgent Agent, AgentCard Card) GetMessageFormulatorAgent();

    /// <summary>
    /// Creates an orchestrator agent that coordinates the specialized agents.
    /// Uses AIAgent to support middleware.
    /// </summary>
    (AIAgent Agent, AgentCard Card) GetOrchestratorAgent();
}
