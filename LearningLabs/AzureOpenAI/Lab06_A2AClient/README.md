# Lab 06 - A2A Client (Agent-to-Agent Communication)

## Objective

In this lab, you will learn how to create an **A2A (Agent-to-Agent) Client** using the Microsoft Agents Framework with Azure OpenAI.

The A2A Client connects to an A2A Server and sends requests to remote agents, enabling distributed agent systems where specialized agents can collaborate to solve complex tasks.

## What You Will Learn

- How to create an A2A client that connects to remote agents
- How to discover agent capabilities via agent cards
- How to send tasks to remote agents and receive responses
- How to integrate A2A client with Azure OpenAI

## Prerequisites

- .NET 10 SDK installed
- Azure OpenAI resource deployed
- Azure CLI logged in (`az login`) for DefaultAzureCredential
- **Lab06_A2AServer running** on http://localhost:5000

## Project Structure

```
Lab06_A2AClient/
├── README.md
├── Start/             <-- Your working folder
│   ├── Program.cs     <-- Complete the TODOs here
│   └── ...
└── Solution/          <-- Reference solution (check if needed)
    └── ...
```

## Instructions

### Step 1: Start the A2A Server

First, make sure the A2A Server from Lab06_A2AServer is running:

```bash
cd ../Lab06_A2AServer/Solution
dotnet run
```

The server should be running on http://localhost:5000

### Step 2: Configure your settings

Open `Start/appsettings.json` and update the values:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
    "DeploymentName": "YOUR-DEPLOYMENT-NAME"
  },
  "A2AServer": {
    "Url": "http://localhost:5000"
  }
}
```

### Step 3: Complete the Program.cs

Open `Start/Program.cs` and implement the A2A Client logic.

### Step 4: Run and Test

```bash
cd Start
dotnet run
```

## Key Concepts

| Concept | Description |
|---------|-------------|
| `A2AClient` | Client for connecting to A2A servers |
| `AgentCard` | Metadata describing agent capabilities |
| `A2ATask` | A task to be executed by a remote agent |
| `A2AClientAgent` | Agent that wraps A2A client for local use |

## Provided Files

The following files are provided and should not be modified:

- `A2AClient.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs` - Helper to read configuration
- `AzureOpenAISettings.cs` - Settings class for Azure OpenAI
- `A2AServerSettings.cs` - Settings class for A2A Server connection

The following file should be modified with your values:

- `appsettings.json` - Update with your Azure OpenAI endpoint and A2A server URL

## Solution

If you get stuck, you can check the complete solution in the `Solution/` folder.

