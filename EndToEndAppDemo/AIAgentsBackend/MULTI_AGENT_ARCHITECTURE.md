# Multi-Agent Architecture - Refactoring Summary

## Overview

The AI agent system has been refactored from a single `message-formulator` agent into a **multi-agent architecture** with specialized agents coordinated by an orchestrator.

## Architecture Changes

### Previous Architecture
- **Single Agent**: `message-formulator` handled everything:
  - Order data and status retrieval
  - Policy searches (returns, refunds, cancellations)
  - Message composition for sellers
  - Had conversation memory (message store)

### New Architecture
- **Four Specialized Agents**:
  1. **OrderAgent** - Handles order data and actions (stateless, no message store)
  2. **PolicyAgent** - Handles policy searches (stateless, no message store)
  3. **MessageFormulatorAgent** - Helps compose messages to sellers (stateless, no message store)
  4. **OrchestratorAgent** - Coordinates the three specialized agents (ONLY agent with conversation memory)

### Key Design Principles
- **Single Conversation**: Only the orchestrator maintains conversation history with the user
- **Stateless Sub-Agents**: Specialized agents have no memory - they just process queries from the orchestrator
- **Agent-as-Tool Pattern**: Sub-agents are converted to tools using `AsAIFunction()` method

## Components Created

### 1. Tools Classes

#### `OrderTools.cs` (NEW)
- `GetOrderByIdAsync()` - Retrieves order details by order ID
- `GetOrderStatusAsync()` - Gets current order status
- `SearchOrdersByCustomerAsync()` - Finds orders by customer login

#### `PolicyTools.cs` (NEW)
- `SearchReturnPolicyAsync()` - Searches return policy
- `SearchRefundPolicyAsync()` - Searches refund policy
- `SearchOrderCancellationPolicyAsync()` - Searches cancellation policy

#### `MessageFormulatorTools.cs` (REFACTORED)
- `SearchSellerRequirementsAsync()` - Searches seller requirements for message composition
- Removed order and policy tools (moved to specialized agents)

### 2. Agent Configurations (appsettings.json)

Added four new agent configurations:

```json
{
  "order": {...},          // OrderAgent configuration
  "policy": {...},         // PolicyAgent configuration
  "message-formulator": {...}, // Refactored MessageFormulatorAgent
  "orchestrator": {...}    // OrchestratorAgent configuration
}
```

### 3. Agent Factory Updates

#### `IAgentFactory.cs` (UPDATED)
Added methods:
- `GetOrderAgent()`
- `GetPolicyAgent()`
- `GetMessageFormulatorAgent()` (updated implementation)
- `GetOrchestratorAgent()`

#### `AgentFactory.cs` (UPDATED)
Implemented all four agent creation methods:
- **OrderAgent**: Uses OrderTools, NO message store (stateless)
- **PolicyAgent**: Uses PolicyTools, NO message store (stateless)
- **MessageFormulatorAgent**: Uses MessageFormulatorTools, NO message store (stateless)
- **OrchestratorAgent**: Converts sub-agents to tools using `AsAIFunction()`, HAS message store (only agent with conversation memory)

### 4. Controllers

#### `OrchestratorAgentController.cs` (NEW)
- Main controller exposed to frontend
- Endpoint: `/api/agents/orchestrator/stream`
- Coordinates specialized agents
- Manages conversation context

#### `MessageFormulatorAgentController.cs` (EXISTING)
- Still available if you want to call the message formulator directly
- Not the main entry point anymore

## How It Works

### Request Flow

```
User → OrchestratorAgent (has conversation memory)
           ↓ (calls as tools via AsAIFunction)
   ┌───────┴────────┬──────────────┬────────────────┐
   ↓                ↓              ↓                ↓
OrderAgent     PolicyAgent    MessageFormulatorAgent
(stateless)    (stateless)        (stateless)
```

### Agent Routing Logic

The **OrchestratorAgent** analyzes the customer's request and calls the appropriate specialized agent(s) as tools:

- **Order questions** (status, tracking) → Calls `order_agent` tool
- **Policy questions** (returns, refunds, cancellations) → Calls `policy_agent` tool
- **Message composition** → Calls `message_formulator_agent` tool
- **Complex requests** → Calls multiple agent tools as needed

### Key Points
- **Only ONE conversation**: The orchestrator maintains the entire conversation history
- **Sub-agents are stateless**: They don't know about the conversation, they just answer specific queries
- **Agent-as-Tool pattern**: Sub-agents are registered as tools using `agent.AsAIFunction()`

## Frontend Integration

### To Use the New Orchestrator

Update your frontend to call the orchestrator endpoint instead of the message-formulator:

**Before:**
```typescript
const response = await fetch('/api/agents/message-formulator/stream', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    message: userMessage,
    contextId: contextId,
    conversationId: conversationId,
    customerName: customerName
  })
});
```

**After:**
```typescript
const response = await fetch('/api/agents/orchestrator/stream', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    message: userMessage,
    contextId: contextId,
    conversationId: conversationId,
    customerName: customerName
  })
});
```

### Frontend Changes Required

1. **Update API endpoint** in your chat interface:
   - Change from `/api/agents/message-formulator/stream` 
   - To `/api/agents/orchestrator/stream`

2. **Same request/response format**: No changes needed to the request body or response handling

3. **Same conversation management**: All conversation endpoints remain the same:
   - `POST /api/agents/orchestrator/conversation/message`
   - `GET /api/agents/orchestrator/conversation/{conversationId}`
   - `DELETE /api/agents/orchestrator/conversation/{conversationId}`

## Benefits of Multi-Agent Architecture

### 1. **Separation of Concerns**
Each agent has a single, well-defined responsibility:
- OrderAgent → Orders only
- PolicyAgent → Policies only
- MessageFormulatorAgent → Message composition only

### 2. **Single Conversation Context**
- Only the orchestrator maintains conversation history
- Cleaner conversation management
- No confusion between different conversation contexts

### 3. **Stateless Sub-Agents**
- Sub-agents are simple, focused query processors
- No memory overhead for sub-agents
- Easier to test and debug

### 4. **Scalability**
- Easy to add new specialized agents
- Can optimize each agent independently
- Can scale specific agents based on load

### 5. **Maintainability**
- Smaller, focused codebases
- Easier to test individual agents
- Clear boundaries between functionalities

### 6. **Flexibility**
- Can call individual agents directly if needed (though they're stateless)
- Can compose new orchestration patterns
- Can reuse agents in different contexts

### 7. **Better Prompts**
- Each agent has specialized, focused instructions
- Less confusion about responsibilities
- More accurate responses

## Testing the New Architecture

### Test OrderAgent Directly
```bash
curl -X POST http://localhost:5016/api/agents/orchestrator/stream \
  -H "Content-Type: application/json" \
  -d '{"message": "Où en est ma commande ORD-2026-001 ?", "customerName": "Client"}'
```

### Test PolicyAgent Directly
```bash
curl -X POST http://localhost:5016/api/agents/orchestrator/stream \
  -H "Content-Type: application/json" \
  -d '{"message": "Quelle est votre politique de retour ?", "customerName": "Client"}'
```

### Test MessageFormulatorAgent Directly
```bash
curl -X POST http://localhost:5016/api/agents/orchestrator/stream \
  -H "Content-Type: application/json" \
  -d '{"message": "Je veux écrire au vendeur pour un produit cassé", "customerName": "Client"}'
```

## Migration Path

### Option 1: Direct Migration (Recommended)
1. Update frontend to use `/api/agents/orchestrator/stream`
2. Test all functionalities
3. Remove old message-formulator controller if desired

### Option 2: Gradual Migration
1. Keep both endpoints active
2. Gradually migrate users to orchestrator
3. Monitor and compare performance
4. Deprecate message-formulator endpoint later

## Rollback Plan

If issues arise, the original `MessageFormulatorAgentController` is still available:
- Frontend can revert to `/api/agents/message-formulator/stream`
- All original functionality remains intact
- No data loss or migration issues

## Files Modified

### Created
- `Agents/Tools/OrderTools.cs`
- `Agents/Tools/PolicyTools.cs`
- `Controllers/OrchestratorAgentController.cs`
- `MULTI_AGENT_ARCHITECTURE.md` (this file)

### Modified
- `Agents/Tools/MessageFormulatorTools.cs` (refactored)
- `Agents/Factory/IAgentFactory.cs` (added methods)
- `Agents/Factory/AgentFactory.cs` (added implementations)
- `appsettings.json` (added agent configurations)

### Unchanged
- All repositories
- All services
- All models
- Database schemas

## Next Steps

1. **Update Frontend**: Change API endpoint to use orchestrator
2. **Test Thoroughly**: Verify all scenarios work correctly
3. **Monitor Performance**: Ensure response times are acceptable
4. **Gather Feedback**: Collect user feedback on the new system
5. **Optimize**: Fine-tune agent instructions based on usage patterns

## Questions?

If you have any questions or need help with the migration, please refer to:
- Agent configurations in `appsettings.json`
- Agent factory implementation in `AgentFactory.cs`
- Controller endpoints in `OrchestratorAgentController.cs`
