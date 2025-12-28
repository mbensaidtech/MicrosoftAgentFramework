# Lab 01 - Use Agent as Tool

## Objective

In this lab, you will learn how to use an AI Agent as a Tool within another AI Agent using the Microsoft Agents Framework with Azure OpenAI.

This is a fundamental multi-agent pattern where an orchestrator agent can delegate tasks to specialized agents by calling them as tools.

## What You Will Learn

- How to create a specialized agent with specific capabilities
- How to wrap an agent as an AITool using `AIFunctionFactory`
- How to create an orchestrator agent that uses other agents as tools
- How to coordinate multiple agents in a multi-agent system
- Best practices for agent-to-agent communication

## Prerequisites

- .NET 10 SDK installed
- Azure OpenAI resource deployed
- Azure CLI logged in (`az login`) for DefaultAzureCredential

## Project Structure

```
Lab01-UseAgentAsTool/
├── README.md
├── Start/             <-- Your working folder
│   ├── Program.cs     <-- Complete the TODOs here
│   └── ...
└── Solution/          <-- Reference solution (check if needed)
    └── ...
```

## Instructions

### Step 1: Configure your settings

Open `Start/appsettings.json` and update the values:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
    "DeploymentName": "YOUR-DEPLOYMENT-NAME"
  }
}
```

### Step 2: Complete the Program.cs

Open `Start/Program.cs` and complete the TODOs in Scenario 1:

| TODO | Description |
|------|-------------|
| TODO 1 | Create a specialized translator agent |
| TODO 2 | Create a function that wraps the translator agent |
| TODO 3 | Create an AITool from the wrapper function |
| TODO 4 | Create an orchestrator agent with the translator tool |
| TODO 5 | Run the orchestrator agent |
| TODO 6 | Display the response and token usage |

### Step 3: Run and Test

```bash
cd Start
dotnet run
```

## Key Concepts

| Concept | Description |
|---------|-------------|
| `ChatClientAgent` | An AI agent that can process prompts and use tools |
| `AIFunctionFactory.Create` | Creates an AITool from a delegate/function |
| `AITool` | Abstraction for tools that agents can use |
| Orchestrator Agent | An agent that coordinates other agents |
| Specialized Agent | An agent with specific capabilities (e.g., translation) |
| Agent as Tool | Pattern where an agent is wrapped as a tool for another agent |

## Expected Output

```
Endpoint: https://your-resource.openai.azure.com/
Deployment: your-deployment-name
----------------------------------------
=== Scenario 1: Use Agent as Tool ===
Orchestrator Response: [Response with translated content]
----------------------------------------
Token Usage: 
  Input tokens: 1234
  Output tokens: 456
  Total tokens: 1690
```

## Provided Files

The following files are provided and should not be modified:

- `UseAgentAsTool.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs` - Helper to read configuration
- `AzureOpenAISettings.cs` - Settings class for Azure OpenAI

The following file should be modified with your values:

- `appsettings.json` - Update with your Azure OpenAI endpoint and deployment name

## Solution

If you get stuck, you can check the complete solution in the `Solution/` folder.
