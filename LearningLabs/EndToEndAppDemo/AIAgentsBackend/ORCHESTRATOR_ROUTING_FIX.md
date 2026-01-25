# Orchestrator Routing Fix

## Problem
The orchestrator was incorrectly routing policy questions to the message formulator agent.

**Example:**
- User asks: "Quelles sont les conditions pour être remboursé ?"
- Expected: Call `policy_agent` to get refund policy information
- Actual (before fix): Try to gather info to write a message to seller ❌

## Root Cause
1. **Vague tool descriptions**: The orchestrator didn't have enough information about what each agent does
2. **Ambiguous instructions**: Not clear enough about when to use which agent
3. **High temperature**: Temperature of 0.7 made routing less deterministic

## Solutions Applied (FINAL VERSION)

### 1. Ultra-Explicit Tool Descriptions (AgentFactory.cs)

Added **detailed, explicit descriptions** for each agent tool:

```csharp
var policyAgentTool = policyAgent.AsAIFunction(new Microsoft.Extensions.AI.AIFunctionFactoryOptions
{
    Name = "policy_agent",
    Description = @"Agent spécialisé dans les POLITIQUES et CONDITIONS.
    
Utilise cet agent quand le client demande des INFORMATIONS sur:
- Politique de RETOUR (comment retourner, délai de retour, conditions)
- Politique de REMBOURSEMENT (comment être remboursé, délai, conditions)
- Politique d'ANNULATION (comment annuler, frais)

Exemples:
- 'Comment faire pour retourner un produit ?'
- 'Quelles sont les conditions pour être remboursé ?'
- 'Quel est le délai de remboursement ?'

CET AGENT RÉPOND DIRECTEMENT avec les informations officielles.
NE PAS UTILISER pour: rédiger un message au vendeur"
});
```

### 2. Priority-Based Routing Rules (appsettings.json)

**Key Innovation: Ordered Priority Rules**

```
RÈGLE #1 - CHERCHE D'ABORD "statut" OU "commande" OU "ORD-":
SI tu vois un de ces mots → order_agent

RÈGLE #2 - CHERCHE "comment", "conditions", "politique":  
SI tu vois ces mots → policy_agent

RÈGLE #3 - CHERCHE EXPLICITEMENT "écrire" OU "contacter":
SEULEMENT SI tu vois ces mots → message_formulator_agent

⚠️ PIÈGES À ÉVITER:
❌ "Je veux connaître le statut" ≠ "Je veux écrire" → order_agent
❌ "Je veux savoir si je peux annuler" ≠ "Je veux demander" → policy_agent
```

**Why This Works:**
- **Priority order**: Check for order/status keywords FIRST
- **Explicit negative examples**: Shows what NOT to do
- **Keyword matching**: Simple pattern matching instead of intent interpretation

### 3. Lower Temperature

Changed orchestrator temperature from **0.7 → 0.2** for more deterministic routing.

### 4. Negative Examples in Tool Descriptions

Each tool now explicitly shows what **NOT** to use it for with specific examples:

```csharp
// message_formulator_agent
Exemples NON (NE PAS utiliser cet agent):
- 'Je veux connaître le statut de ma commande' ❌ → order_agent
- 'Je veux savoir les conditions de remboursement' ❌ → policy_agent
```

This prevents the model from incorrectly choosing the message formulator when the user just wants information.

## How to Test

### Test Case 1: Policy Question
**Input:** "Quelles sont les conditions pour être remboursé ?"

**Expected Flow:**
1. Orchestrator analyzes: sees "conditions" + "remboursé"
2. Calls `policy_agent` tool
3. Policy agent searches refund policy in knowledge base
4. Returns policy information directly

**Expected Output:** Information about refund conditions from the knowledge base

### Test Case 2: Order Status
**Input:** "Où en est ma commande ORD-2026-001 ?"

**Expected Flow:**
1. Orchestrator sees "où en est" + "commande"
2. Calls `order_agent` tool
3. Order agent retrieves order status
4. Returns order information

### Test Case 3: Message to Seller
**Input:** "Je veux écrire au vendeur pour un produit cassé"

**Expected Flow:**
1. Orchestrator sees "écrire" + "vendeur"
2. Calls `message_formulator_agent` tool
3. Message formulator gathers info (product, issue, order number, desired action)
4. Generates message draft

## Key Differences in Routing Logic

### Policy Questions vs Message Writing

| User Intent | Keywords | Correct Agent |
|------------|----------|---------------|
| "How do I get a refund?" | "comment", "remboursement" | `policy_agent` |
| "What are the conditions?" | "conditions", "quelles sont" | `policy_agent` |
| "Can I cancel?" | "puis-je", "annuler" | `policy_agent` |
| "I want to ask seller for refund" | "je veux", "demander", "vendeur" | `message_formulator_agent` |
| "Write to seller about broken item" | "écrire", "vendeur", "cassé" | `message_formulator_agent` |

## Critical Rules Added

1. **⚠️ ABSOLUTE RULE**: Orchestrator NEVER responds directly - ALWAYS calls a tool
2. **🚫 NO INFO GATHERING**: If user asks a question about policies, call `policy_agent` immediately
3. **📋 KEYWORD MATCHING**: Clear keyword patterns for each agent
4. **🎯 EXPLICIT EXAMPLES**: Exact question → agent mappings

## Files Modified

1. `Agents/Factory/AgentFactory.cs` - Enhanced tool descriptions
2. `appsettings.json` - Simplified orchestrator instructions, lower temperature

## Next Steps if Still Not Working

If the orchestrator still routes incorrectly:

1. **Check logs**: See which tool is being called
2. **Test individual agents**: Call policy_agent directly to verify it works
3. **Increase tool descriptions**: Add even more examples
4. **Force tool usage**: Investigate if there's a way to force tool calls in ChatOptions
5. **Consider tool choice**: Look into setting tool_choice to "required" if available

## Benefits of This Approach

✅ **Clearer routing**: Keyword-based decision making  
✅ **Lower temperature**: More deterministic tool selection  
✅ **Explicit examples**: The exact question from user is in the examples  
✅ **Better tool descriptions**: Each tool clearly states what it does and doesn't do  
