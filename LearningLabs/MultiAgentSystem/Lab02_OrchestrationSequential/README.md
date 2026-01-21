# Lab 02 - Sequential Orchestration

## Objective

In this lab, you will learn how to implement **Sequential Orchestration** in a multi-agent system using the **Microsoft Agents Framework** with **Azure OpenAI**.

You will create multiple specialized agents and orchestrate them in a sequential pipeline where each agent's output feeds into the next agent.

## What You Will Learn

- How to create multiple specialized agents
- How to orchestrate agents in a sequential pipeline using `AgentWorkflowBuilder.BuildSequential`
- How to execute workflows using `InProcessExecution.StreamAsync`
- How to handle workflow events (`WorkflowOutputEvent`)

## Prerequisites

- .NET 10 SDK installed
- Azure OpenAI resource deployed
- Azure CLI logged in (`az login`) for DefaultAzureCredential
- Familiarity with Lab01 concepts

## Project Structure

```
Lab02_OrchestrationSequential/
├── README.md
├── Start/                          <-- Your working folder
│   ├── Program.cs                  <-- Complete the TODOs here
│   ├── Data/
│   │   └── meeting-transcript.txt  <-- Sample meeting transcript
│   ├── ConfigurationHelper.cs
│   ├── AzureOpenAISettings.cs
│   ├── appsettings.json
│   └── OrchestrationSequential.csproj
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
    "ChatDeploymentName": "YOUR-DEPLOYMENT-NAME",
    "APIKey": ""
  }
}
```

#### Authentication Options

The labs support two authentication methods:

1. **API Key Authentication** (recommended for local development):
   - Set `APIKey` in `appsettings.json` with your Azure OpenAI API key

2. **DefaultAzureCredential** (for Azure-hosted apps):
   - Leave `APIKey` empty
   - Requires Azure CLI login (`az login`) or managed identity

The Solution folder includes the conditional logic to handle both methods.

### Step 2: Complete the Program.cs

Open `Start/Program.cs` and complete the TODOs in the **Sequential Workflow** region:

---

#### Sequential Workflow

| Step | Description | Hints |
|------|-------------|-------|
| **Step 2** | Create specialized agents | • Use `chatClient.CreateAIAgent(name: "...", instructions: "...")` <br> • **SummaryAgent**: Instructions should ask it to summarize the meeting transcript <br> • **ActionsExtractorAgent**: Instructions should ask it to extract action items <br> • Include in instructions: "You must use the same language as the meeting transcript" |
| **Step 3** | Build sequential workflow with agents | • Method: `AgentWorkflowBuilder.BuildSequential(workflowName: "...", agents: [...])` <br> • Pass both agents in an array |
| **Step 4** | Prepare input messages | • Create a `List<ChatMessage>` <br> • Add one message: `new ChatMessage(ChatRole.User, meetingTranscript)` |
| **Step 5** | Execute the workflow | • Get a streaming run: `InProcessExecution.StreamAsync(workflow, messages)` <br> • Send a turn token: `await run.TrySendMessageAsync(new TurnToken(emitEvents: true))` |
| **Step 6** | Collect results from workflow events | • Create an empty `List<ChatMessage>` for results <br> • Iterate with: `await foreach (WorkflowEvent evt in run.WatchStreamAsync())` <br> • Check for `WorkflowOutputEvent` and cast `evt.Data` to `List<ChatMessage>` |
| **Step 7** | Display final results | • Filter messages: `result.Where(x => x.Role != ChatRole.User)` <br> • Display: `message.Role`, `message.AuthorName`, `message.Text` |

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
| Sequential Orchestration | A pattern where agents are executed in a specific order, with each agent's output feeding into the next |
| `AgentWorkflowBuilder.BuildSequential` | Creates a workflow that executes agents in sequence |
| `InProcessExecution.StreamAsync` | Executes a workflow and returns a streaming run |
| `StreamingRun` | Represents an ongoing workflow execution that can be monitored |
| `TurnToken` | Signals the workflow to proceed with execution |
| `WorkflowEvent` | Base class for workflow events (start, update, output) |
| `WorkflowOutputEvent` | Event containing the final output of the workflow |

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

=== Sequential Workflow ===
Loaded transcript: 2145 characters
Results:
Assistant - SummaryAgent: 
The Project Alpha Weekly Sync meeting covered status updates from team members...

Assistant - ActionsExtractorAgent: 
**Action Items:**
1. Mohammed: Write unit tests for authentication module (by Friday)
2. Mohammed: Send API docs to Emma (by end of day today)
3. Mohammed: Fix PAY-201 security issue (this afternoon)
...
```

## Provided Files

The following files are provided and should not be modified:

- `OrchestrationSequential.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs` - Helper to read configuration
- `AzureOpenAISettings.cs` - Settings class for Azure OpenAI
- `Data/meeting-transcript.txt` - Sample meeting transcript

The following files should be modified:

- `appsettings.json` - Update with your Azure OpenAI settings
- `Program.cs` - Complete the TODOs

## Useful Links

- [Microsoft Agent Framework Workflows Orchestrations - Sequential](https://learn.microsoft.com/en-us/agent-framework/user-guide/workflows/orchestrations/sequential?pivots=programming-language-csharp)

## Solution

If you get stuck, check the complete solution in the `Solution/` folder.
