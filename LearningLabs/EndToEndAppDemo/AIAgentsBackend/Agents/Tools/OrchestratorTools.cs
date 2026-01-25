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
    public static AITool CreateOrderAgentTool(ChatClientAgent orderAgent)
    {
        return orderAgent.AsAIFunction(new AIFunctionFactoryOptions
        {
            Name = "order_agent",
            Description = @"Agent for ORDERS and STATUS.

Use this agent when you see the words:
- 'statut' + 'commande'
- 'où en est' + 'commande'  
- 'suivi' + 'livraison'
- Order number (ORD-XXXX-XXX)

Examples YES (use this agent):
- 'Où en est ma commande ORD-2026-001 ?' ✅
- 'Je veux connaître le statut de ma commande ORD-2026-001' ✅
- 'Quel est le statut de ma livraison ?' ✅
- 'Suivi de ma commande' ✅
- 'Détails de ma commande' ✅

Examples NO:
- 'Comment être remboursé ?' ❌ → policy_agent
- 'Je veux écrire au vendeur' ❌ → message_formulator_agent"
        });
    }

    /// <summary>
    /// Converts the PolicyAgent to a tool with detailed description for the orchestrator.
    /// </summary>
    public static AITool CreatePolicyAgentTool(ChatClientAgent policyAgent)
    {
        return policyAgent.AsAIFunction(new AIFunctionFactoryOptions
        {
            Name = "policy_agent",
            Description = @"Agent specialized in POLICIES and CONDITIONS.

Use this agent when the customer asks for INFORMATION about:
- RETURN policy (how to return, return deadline, conditions)
- REFUND policy (how to get refunded, timeline, conditions)
- CANCELLATION policy (how to cancel, when can you cancel, fees)
- General RIGHTS and CONDITIONS

Question examples:
- 'Comment faire pour retourner un produit ?' ✅
- 'Quelles sont les conditions pour être remboursé ?' ✅
- 'Quel est le délai de remboursement ?' ✅
- 'Puis-je annuler ma commande ?' ✅
- 'Quelle est votre politique de retour ?' ✅

THIS AGENT RESPONDS DIRECTLY with official information.
DO NOT USE for: writing a message to the seller, getting order status."
        });
    }

    /// <summary>
    /// Converts the MessageFormulatorAgent to a tool with detailed description for the orchestrator.
    /// </summary>
    public static AITool CreateMessageFormulatorAgentTool(ChatClientAgent messageFormulatorAgent)
    {
        return messageFormulatorAgent.AsAIFunction(new AIFunctionFactoryOptions
        {
            Name = "message_formulator_agent",
            Description = @"Agent to HELP WRITE a message to the SELLER.

Use this agent when the customer has a PROBLEM that requires CONTACTING THE SELLER:
- Broken/defective/damaged product
- Quality issue
- Product doesn't match description
- Wrong product received
- Missing parts
- Specific question about the product
- Special request to the seller

Examples YES (contact the seller):
- 'Mon produit est cassé' ✅
- 'Le produit ne fonctionne pas' ✅
- 'Ce n'est pas ce que j'ai commandé' ✅
- 'Il manque des pièces' ✅
- 'Je veux écrire au vendeur' ✅
- 'Le produit ne correspond pas à la description' ✅

Examples NO (answer directly, no need for seller):
- 'Où en est ma commande ?' ❌ → order_agent
- 'Comment être remboursé ?' ❌ → policy_agent
- 'Puis-je annuler ma commande ?' ❌ → policy_agent
- 'Quel est le statut de ma livraison ?' ❌ → order_agent

RULE: If the question can be answered with order_agent or policy_agent, DO NOT use this agent."
        });
    }
}
