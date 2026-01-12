# Lab 03 - Concurrent Orchestration

## Objective

In this lab, you will learn how to implement **Concurrent Orchestration** in a multi-agent system using the **Microsoft Agents Framework** with **Azure OpenAI**.

You will create multiple specialized agents and orchestrate them to run in parallel (concurrently), where all agents process the same input simultaneously and produce independent outputs.

## What You Will Learn

- How to create multiple specialized agents
- How to orchestrate agents in a concurrent pipeline using `AgentWorkflowBuilder.BuildConcurrent`
- How to execute workflows using `InProcessExecution.StreamAsync`
- How to handle workflow events (`WorkflowOutputEvent`)
- Understanding the difference between sequential and concurrent orchestration

## Prerequisites

- .NET 10 SDK installed
- Azure OpenAI resource deployed
- Azure CLI logged in (`az login`) for DefaultAzureCredential
- Familiarity with Lab01 and Lab02 concepts

## Project Structure

```
Lab03_OrchestrationConcurrent/
├── README.md
├── Start/                          <-- Your working folder
│   ├── Program.cs                  <-- Complete the TODOs here
│   ├── ConfigurationHelper.cs
│   ├── AzureOpenAISettings.cs
│   ├── appsettings.json
│   └── OrchestrationConcurrent.csproj
└── Solution/                       <-- Reference solution
    └── ...
```

## Instructions

### Step 1: Configure your settings

Open `Start/appsettings.json` and update the values:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
    "ChatDeploymentName": "YOUR-DEPLOYMENT-NAME"
  }
}
```

### Step 2: Complete the Program.cs

Open `Start/Program.cs` and complete the TODOs in the **Concurrent Workflow** region:

---

#### Concurrent Workflow

| Step | Description | Hints |
|------|-------------|-------|
| **Step 1** | Create specialized agents | • Use `chatClient.CreateAIAgent(name: "...", instructions: "...")` <br> • **Return/Exchange Agent**: Detects if the customer needs a return or exchange <br> • **Refund Agent**: Checks if the customer is eligible for a refund <br> • **Follow-up Agent**: Creates a polite reply summarizing actions for the customer |
| **Step 2** | Build concurrent workflow with agents | • Method: `AgentWorkflowBuilder.BuildConcurrent(workflowName: "...", agents: [...])` <br> • Pass all three agents in an array |
| **Step 3** | Prepare input messages | • Create a `List<ChatMessage>` <br> • Add one message: `new ChatMessage(ChatRole.User, "I received my order, but the charger is missing and I also want to return the old one.")` |
| **Step 4** | Execute the workflow | • Get a streaming run: `InProcessExecution.StreamAsync(workflow, messages)` <br> • Send a turn token: `await run.TrySendMessageAsync(new TurnToken(emitEvents: false))` |
| **Step 5** | Collect results from workflow events | • Create an empty `List<ChatMessage>` for results <br> • Iterate with: `await foreach (WorkflowEvent evt in run.WatchStreamAsync())` <br> • Check for `WorkflowOutputEvent` and cast `evt.Data` to `List<ChatMessage>` |
| **Step 6** | Display final results | • Filter messages: `result.Where(x => x.Role != ChatRole.User)` <br> • Display: `message.Role`, `message.AuthorName`, `message.Text` |

---

### Step 3: Run and Test

**Run the Start project (your implementation):**
```bash
cd Start
dotnet run
```

**Run the Solution (reference):**
```bash
cd Solution
dotnet run
```

---

## Key Concepts

| Concept | Description |
|---------|-------------|
| Concurrent Orchestration | A pattern where multiple agents are executed in parallel, each processing the same input independently |
| Sequential vs Concurrent | Sequential: agents run one after another (output feeds next). Concurrent: agents run simultaneously on the same input |
| `AgentWorkflowBuilder.BuildConcurrent` | Creates a workflow that executes agents in parallel |
| `InProcessExecution.StreamAsync` | Executes a workflow and returns a streaming run |
| `StreamingRun` | Represents an ongoing workflow execution that can be monitored |
| `TurnToken` | Signals the workflow to proceed with execution |
| `WorkflowEvent` | Base class for workflow events (start, update, output) |
| `WorkflowOutputEvent` | Event containing the final output of the workflow |

## When to Use Concurrent vs Sequential

| Use Concurrent When | Use Sequential When |
|---------------------|---------------------|
| Tasks are independent and can run in parallel | Tasks depend on previous outputs |
| You want faster execution time | Output from one agent feeds into the next |
| Each agent analyzes the same input differently | You need a chain of transformations |
| Results can be aggregated at the end | Order of execution matters |

## Workflow Diagram

```
                User Message
                      │
                      │ ["I received my order, but the charger is missing and I also want to return the old one."]
                      ▼
           +----------------------+
           | StreamingRun         |
           | (workflow instance)  |
           +----------------------+
                      │
                      │ TrySendMessageAsync(new TurnToken(emitEvents: false))
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
| Checks if    | | Checks if    | | Crafts polite   |
| return/exchg | | refund ok    | | customer reply  |
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
```

## Namespaces Reference

| Namespace | Purpose |
|-----------|---------|
| `Azure.Identity` | Provides `DefaultAzureCredential` for Azure authentication |
| `Azure.AI.OpenAI` | Provides `AzureOpenAIClient` to connect to Azure OpenAI |
| `OpenAI` | Core SDK - extension methods like `CreateAIAgent` |
| `OpenAI.Chat` | Provides `ChatClient` for chat completions |
| `Microsoft.Agents.AI` | Provides `ChatClientAgent`, `AIAgent` |
| `Microsoft.Agents.AI.Workflows` | Provides `AgentWorkflowBuilder`, `InProcessExecution`, `StreamingRun`, `WorkflowEvent`, `WorkflowOutputEvent` |
| `Microsoft.Extensions.AI` | Provides `ChatMessage`, `ChatRole` |
| `CommonUtilities` | Provides `ColoredConsole` for formatted output |

## Expected Output

```
Azure OpenAI Settings: 
Azure OpenAI Endpoint: https://your-resource.openai.azure.com/
Azure OpenAI Chat Deployment Name: your-deployment-name

────────────────────────────────────────────────────

=== Concurrent Workflow ===
Results:
Assistant - Return/Exchange Agent: 
The customer needs a return for the old item and is missing a charger from their order...

Assistant - Refund Agent: 
The customer may be eligible for a refund or replacement for the missing charger...

Assistant - Follow-up Agent: 
Dear Customer, thank you for reaching out. We apologize for the inconvenience...
```

## Provided Files

The following files are provided and should not be modified:

- `OrchestrationConcurrent.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs` - Helper to read configuration
- `AzureOpenAISettings.cs` - Settings class for Azure OpenAI

The following files should be modified:

- `appsettings.json` - Update with your Azure OpenAI settings
- `Program.cs` - Complete the TODOs

## Useful Links

- [Microsoft Agent Framework Workflows Orchestrations - Concurrent](https://learn.microsoft.com/en-us/agent-framework/user-guide/workflows/orchestrations/concurrent?pivots=programming-language-csharp)

## Solution

If you get stuck, check the complete solution in the `Solution/` folder.
