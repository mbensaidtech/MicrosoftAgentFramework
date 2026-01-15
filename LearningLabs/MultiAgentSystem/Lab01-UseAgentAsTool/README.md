# Lab 01 - Use Agent as Tool: Multi-Agent Customer Service System

## Objective

In this lab, you will create a multi-agent system where specialized AI agents work together as tools within an orchestrator agent using the **Microsoft Agents Framework** with **Azure OpenAI**.

You will build a complete customer service system with quality evaluation, demonstrating the fundamental **Agent as Tool** pattern where specialized agents are wrapped as tools and coordinated by an orchestrator.

## What You Will Learn

- How to create specialized agents with specific responsibilities
- How to use the **Builder Pattern** for flexible agent configuration
- How to wrap agents as AITools using `AsAIFunction`
- How to create an orchestrator agent that coordinates multiple agent-tools
- How to use **Structured Output** to get well-formatted responses
- How to implement quality evaluation with judge agents
- Best practices for multi-agent system architecture

## Prerequisites

- .NET 10 SDK installed
- Azure OpenAI resource deployed with GPT-4o or GPT-4o-mini
- Azure CLI logged in (`az login`) for DefaultAzureCredential

## Project Structure

```
Lab01-UseAgentAsTool/
├── README.md
├── Start/                       <-- Your working folder
│   ├── Program.cs              <-- Complete the TODOs here
│   ├── appsettings.json        <-- Update with your settings
│   ├── AgentCreation/          <-- Provided (Factory & Builder)
│   │   ├── AgentFactory.cs
│   │   └── AgentBuilder.cs
│   ├── Configuration/          <-- Provided (Settings & Types)
│   │   ├── AgentSettings.cs
│   │   └── AgentType.cs
│   ├── Models/                 <-- Provided (Response models)
│   │   └── OrchestratorResponse.cs
│   ├── MockedData/             <-- Provided (SAV rules)
│   │   └── sav-rules.yml
│   ├── ConfigurationHelper.cs
│   ├── AzureOpenAISettings.cs
│   └── UseAgentAsTool.csproj
└── Solution/                    <-- Reference solution
    └── ...
```

## Instructions

### Step 1: Configure your settings

Open `Start/appsettings.json` and update the values:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
    "DefaultDeploymentName": "YOUR-DEPLOYMENT-NAME"
  },
  "Agents": {
    "Reformulator": {
      "DeploymentName": "gpt-4o-mini-deployment"  ← Update if needed
    },
    ...
  }
}
```

**Note**: Each agent can use a different deployment. Update the `DeploymentName` for each agent if you want to use different models.

### Step 2: Complete the Program.cs

Open `Start/Program.cs` and complete the TODOs:

---

#### Setup: Configuration and Client Initialization

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 1** | Create `AzureOpenAIClient` with managed identity authentication | • Constructor: `new AzureOpenAIClient(Uri endpoint, TokenCredential credential)` <br> • Use `new Uri(settings.Endpoint)` for the endpoint <br> • Use `new DefaultAzureCredential()` for authentication |
| **TODO 2** | Create the agent factory | • Constructor: `new AgentFactory(openAIClient)` <br> • The factory encapsulates agent creation logic |

---

#### Scenario 1: Multi-Agent SAV System with Quality Evaluation

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 3** | Create all specialized agents | • Use factory methods: `agentFactory.CreateReformulatorAgent()` <br> • Create 4 agents: reformulator, reformulator judge, SAV, SAV judge <br> • Each agent has specific instructions from `appsettings.json` |
| **TODO 4** | Wrap agents as AITools | • Use extension method: `agent.AsAIFunction(new AIExtensions.AIFunctionFactoryOptions { Name = "...", Description = "..." })` <br> • Create 4 tools: `reformulate_message`, `judge_reformulation`, `answer_sav_question`, `judge_sav_answer` <br> • Good descriptions help the LLM choose the right tool |
| **TODO 5** | Create orchestrator with all tools | • Use factory method: `agentFactory.CreateOrchestratorAgent(tool1, tool2, tool3, tool4)` <br> • The orchestrator coordinates the workflow |
| **TODO 6** | Run the orchestrator with structured output | • Method: `await orchestratorAgent.RunAsync<OrchestratorResponse>(customerQuestion)` <br> • Chain with: `.WithSpinner("Agent is thinking")` <br> • Returns structured `AgentRunResponse<OrchestratorResponse>` |
| **TODO 7** | Display the structured response | • Access: `response.Result.OfficialResponse` <br> • Access judge evaluations: `response.Result.ReformulationJudge`, `response.Result.SavJudge` <br> • Each judge has `Score` (int) and `Feedback` (string) |
| **TODO 8** | Display token usage | • Access: `response.Usage?.InputTokenCount`, `OutputTokenCount`, `TotalTokenCount` <br> • Use null-conditional operator `?.` <br> • Use `ColoredConsole` for formatted output |

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

## Scenario Details

You'll build a **Customer Service System** for an e-commerce website with the following agents:

1. **Reformulator Agent**: Extracts keywords from customer messages
2. **Reformulator Judge Agent**: Evaluates the quality of keyword extraction
3. **SAV Agent**: Answers After-Sales Service questions (returns, refunds, exchanges, warranty, delivery)
4. **SAV Judge Agent**: Evaluates the quality of customer service responses
5. **Orchestrator Agent**: Coordinates all agents and returns structured results

**Workflow:**
```
Customer Question
    ↓
Orchestrator Agent
    ├─→ reformulate_message (extract keywords)
    ├─→ judge_reformulation (validate keywords)
    ├─→ answer_sav_question (get SAV response)
    ├─→ judge_sav_answer (evaluate response)
    └─→ Return OrchestratorResponse (structured output)
```

## Key Concepts

| Concept | Description |
|---------|-------------|
| `AzureOpenAIClient` | Client to connect to Azure OpenAI service |
| `DefaultAzureCredential` | Managed identity authentication (no API keys!) |
| `AgentFactory` | Factory pattern for creating agents with configuration |
| `AgentBuilder` | Builder pattern for flexible agent configuration |
| `ChatClientAgent` | Microsoft Agents wrapper around ChatClient |
| `AsAIFunction` | Extension method to wrap an agent as an AITool |
| `AITool` | Base type for tools that can be registered with an agent |
| `RunAsync<T>` | Execute agent with structured output using a model class |
| `OrchestratorResponse` | Structured response model with typed properties |
| `AgentRunResponse<T>` | Response object containing the structured result |
| `Usage` | Token consumption metrics (Input/Output/Total) |
| `Agent as Tool` | Pattern where agents are wrapped as tools for other agents |

## Optional: Using the Console Spinner

The `CommonUtilities` library provides a `ConsoleSpinner` that shows a loading animation while the agent is processing. This is **optional** but improves the user experience.

**Usage with extension method:**
```csharp
// Simply chain .WithSpinner() to any async Task
AgentRunResponse<OrchestratorResponse> response = await orchestratorAgent
    .RunAsync<OrchestratorResponse>(customerQuestion)
    .WithSpinner("Agent is thinking");
```

**Usage with using statement:**
```csharp
using var spinner = new ConsoleSpinner("Processing request");
AgentRunResponse<OrchestratorResponse> response = await orchestratorAgent
    .RunAsync<OrchestratorResponse>(customerQuestion);
// Spinner automatically stops when disposed
```

The spinner displays an animated indicator with elapsed time: `⠋ Agent is thinking... [00:03]`

## Namespaces Reference

| Namespace | Purpose |
|-----------|---------|
| `Azure.Identity` | Provides `DefaultAzureCredential` for Azure authentication |
| `Azure.AI.OpenAI` | Provides `AzureOpenAIClient` to connect to Azure OpenAI |
| `OpenAI` | Core SDK - extension methods like `CreateAIAgent` |
| `OpenAI.Chat` | Provides `ChatClient` for chat completions |
| `Microsoft.Agents.AI` | Provides `ChatClientAgent`, `AgentRunResponse` |
| `Microsoft.Extensions.AI` | Provides `AIFunctionFactoryOptions`, `AsAIFunction` (aliased as `AIExtensions`) |
| `UseAgentAsTool.AgentCreation` | Provides `AgentFactory`, `AgentBuilder` |
| `UseAgentAsTool.Configuration` | Provides `AgentSettings`, `AgentType` |
| `UseAgentAsTool.Models` | Provides `OrchestratorResponse`, `JudgeEvaluation` |
| `CommonUtilities` | Provides `ColoredConsole` for formatted output |

## Expected Output

```
Endpoint: https://your-resource.openai.azure.com/
Default Deployment: gpt-4o-mini
----------------------------------------
=== Scenario 1: Multi-Agent SAV System with Quality Evaluation ===
Customer Question:
I bought a laptop 2 weeks ago and the screen is broken. What can I do? Can I get a refund or exchange?
----------------------------------------
⠋ Agent is thinking...
----------------------------------------
=== Official Response (for customer) ===
I'm sorry to hear about your broken laptop screen. Since you purchased it 2 weeks ago, you're within our 30-day standard return period. Here are your options:

1. Exchange: You can exchange it for the same model at no extra cost
2. Refund: We can provide a full refund to your original payment method (processing time: 5-10 business days)

Since the screen is damaged, this may be covered under warranty depending on the cause...
----------------------------------------
=== Reformulation Judge Evaluation ===
Score: 9/10
Feedback: Keywords accurately captured: laptop, broken screen, 2 weeks, refund, exchange. Excellent extraction.
----------------------------------------
=== SAV Judge Evaluation ===
Score: 9/10
Feedback: Response is accurate, helpful, and professional. Correctly cites the 30-day return policy and provides clear options. Could mention the need for photos of damage.
----------------------------------------
Token Usage:
  Input tokens: 2547
  Output tokens: 823
  Total tokens: 3370
```

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                     Orchestrator Agent                       │
│  (Coordinates the workflow and returns structured output)   │
└──────────────┬──────────────────────────────────────────────┘
               │
               │ Has access to 4 tools:
               │
      ┌────────┼────────┬───────────┬──────────┐
      │        │        │           │          │
      ▼        ▼        ▼           ▼          ▼
┌──────────┐ ┌──────┐ ┌──────┐ ┌──────────┐
│Reformu-  │ │Judge │ │ SAV  │ │   SAV    │
│lator     │ │Refor-│ │Agent │ │  Judge   │
│Agent     │ │mula- │ │      │ │  Agent   │
│          │ │tor   │ │      │ │          │
└──────────┘ └──────┘ └──────┘ └──────────┘
    │                     │
    │                     │
    ▼                     ▼
Keywords            ┌────────────┐
                    │ SAV Rules  │
                    │  (YAML)    │
                    └────────────┘
```

## Provided Files

The following files are provided and should not be modified:

- `UseAgentAsTool.csproj` - Project file with all required dependencies
- `AgentCreation/AgentFactory.cs` - Factory for creating agents
- `AgentCreation/AgentBuilder.cs` - Fluent builder for agent configuration
- `Configuration/AgentSettings.cs` - Settings model for agents
- `Configuration/AgentType.cs` - Enum of available agent types
- `Models/OrchestratorResponse.cs` - Structured output model
- `MockedData/sav-rules.yml` - E-commerce return/refund policies
- `ConfigurationHelper.cs` - Configuration loading helper
- `AzureOpenAISettings.cs` - Azure OpenAI settings model

The following files should be modified:

- `appsettings.json` - Update with your Azure OpenAI endpoint and deployment names
- `Program.cs` - Complete the TODOs

## Solution

If you get stuck, check the complete solution in the `Solution/` folder.
