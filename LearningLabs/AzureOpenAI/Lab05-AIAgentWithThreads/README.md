# Lab 05 - AI Agent with Threads

## Objective

In this lab, you will learn how to use **Threads** with AI Agents using the Microsoft Agents Framework with Azure OpenAI.

Threads allow you to maintain conversation context across multiple interactions, enabling multi-turn conversations with memory.

## What You Will Learn

- How to create and manage conversation threads
- How to maintain context across multiple agent interactions
- How to implement multi-turn conversations
- How to manage thread history and state

## Prerequisites

- .NET 10 SDK installed
- Azure OpenAI resource deployed
- Azure CLI logged in (`az login`) for DefaultAzureCredential

## Project Structure

```
Lab05-AIAgentWithThreads/
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

Open `Start/Program.cs` and implement the scenarios as instructed.

### Step 3: Run and Test

```bash
cd Start
dotnet run
```

## Key Concepts

| Concept | Description |
|---------|-------------|
| `Thread` | A conversation context that maintains history |
| `ChatClientAgent` | AI Agent that can participate in threaded conversations |
| `AgentRunResponse` | Response from an agent run |

## Provided Files

The following files are provided and should not be modified:

- `AIAgentWithThreads.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs` - Helper to read configuration
- `AzureOpenAISettings.cs` - Settings class for Azure OpenAI

The following file should be modified with your values:

- `appsettings.json` - Update with your Azure OpenAI endpoint and deployment name

## Solution

If you get stuck, you can check the complete solution in the `Solution/` folder.


