# Lab 06 - A2A Server (Agent-to-Agent Communication)

## Objective

In this lab, you will learn how to create an **A2A (Agent-to-Agent) Server** using the Microsoft Agents Framework with Azure OpenAI.

A2A enables agents to communicate with each other, allowing you to build distributed agent systems where specialized agents can collaborate to solve complex tasks.

## What You Will Learn

- How to create an A2A server that hosts an AI agent
- How to expose agent capabilities as A2A endpoints
- How to configure agent-to-agent communication
- How to handle incoming requests from other agents

## Prerequisites

- .NET 10 SDK installed
- Azure OpenAI resource deployed
- Azure CLI logged in (`az login`) for DefaultAzureCredential

## Project Structure

```
Lab06_A2AServer/
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

Open `Start/Program.cs` and add your A2A Server implementation.

### Step 3: Run and Test

```bash
cd Start
dotnet run
```

## Key Concepts

| Concept | Description |
|---------|-------------|
| `A2A` | Agent-to-Agent protocol for agent communication |
| `AzureOpenAIClient` | Client to connect to Azure OpenAI |
| `ChatClient` | Client for chat completions |
| `DefaultAzureCredential` | Managed identity authentication |

## Provided Files

The following files are provided and should not be modified:

- `A2AServer.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs` - Helper to read configuration
- `AzureOpenAISettings.cs` - Settings class for Azure OpenAI

The following file should be modified with your values:

- `appsettings.json` - Update with your Azure OpenAI endpoint and deployment name

## Solution

If you get stuck, you can check the complete solution in the `Solution/` folder.

