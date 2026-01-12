# E-commerce After-Sales Concurrent Agent Workflow

## Scenario
A customer sends a message after receiving a product:

> "I received my order, but the charger is missing and I also want to return the old one."

We run **three agents concurrently** to handle different aspects of the message:

1. **Return/Exchange Agent** – Checks if a return or exchange is needed.  
2. **Refund Agent** – Determines refund eligibility.  
3. **Follow-up Agent** – Crafts a polite customer reply summarizing actions.  

---

## Workflow Schema

```text
                User Message
                      │
                      │ ["I received my order, but the charger is missing and I also want to return the old one."]
                      ▼
           +----------------------+
           | StreamingRun         |
           | (workflow instance)  |
           +----------------------+
                      │
                      │ TrySendMessageAsync(new TurnToken(emitEvents: true))
                      ▼
           +----------------------+
           | AgentWorkflow Engine |
           +----------------------+
                      │
       ┌──────────────┼──────────────┐
       │              │              │
       ▼              ▼              ▼
+--------------+ +--------------+ +-----------------+
| ReturnAgent  | | RefundAgent  | | FollowUpAgent   |
| Detects if   | | Checks if    | | Crafts polite   |
| return/exchg | | refund is OK | | customer reply  |
+--------------+ +--------------+ +-----------------+
       │              │              │
       │              │              │
       └─────── Events emitted ──────┘
                      │
                      ▼
           +----------------------+
           | WatchStreamAsync()   |
           +----------------------+
                      │
       ┌──────────────┼──────────────┐
       │              │              │
       ▼              ▼              ▼
AgentRunUpdateEvent  AgentRunUpdateEvent  AgentRunUpdateEvent
("ReturnAgent started") ... ("RefundAgent processing") ... ("FollowUpAgent generating response")
                      │
                      ▼
          WorkflowOutputEvent (final combined results)
