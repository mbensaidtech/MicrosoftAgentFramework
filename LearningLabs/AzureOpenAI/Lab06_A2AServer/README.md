# Lab 06 - A2A Server (Agent-to-Agent Communication)

## Objective

In this lab, you will learn how to create an **A2A (Agent-to-Agent) Server** using the Microsoft Agents Framework with Azure OpenAI.

A2A (Agent-to-Agent) is a protocol that enables agents to communicate with each other, allowing you to build distributed agent systems where specialized agents can collaborate to solve complex tasks. In this lab, you will build a server that exposes two agents:

1. **AuthAgent** - An authentication agent that can generate and validate API keys
2. **CustomerToneAgent** - A customer tone assistant that can detect the tone of customer messages

## What You Will Learn

- How to create an A2A server using ASP.NET Core
- How to define **Agent Cards** that describe agent capabilities
- How to expose AI agents as A2A endpoints using `MapA2A()`
- How to configure agent tools (like API key generation/validation)
- How to set up well-known agent card endpoints for discovery

## Prerequisites

- .NET 10 SDK installed
- Azure OpenAI resource deployed
- Azure CLI logged in (`az login`) for DefaultAzureCredential

## Project Structure

```
Lab06_A2AServer/
├── README.md
├── Start/                          <-- Your working folder
│   ├── Program.cs                  <-- Complete the TODOs here
│   ├── appsettings.json            <-- Update with your settings
│   ├── AgentCards.cs               <-- Provided (do not modify)
│   ├── APIKeySettings.cs           <-- Provided (do not modify)
│   ├── AzureOpenAISettings.cs      <-- Provided (do not modify)
│   ├── ConfigurationHelper.cs      <-- Provided (do not modify)
│   └── Tools/
│       └── APIKeyTools.cs          <-- Provided (do not modify)
└── Solution/                       <-- Reference solution (check if needed)
    └── ...
```

## Instructions

### Step 1: Configure your settings

Open `Start/appsettings.json` and update the Azure OpenAI values:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
    "ChatDeploymentName": "YOUR-DEPLOYMENT-NAME",
    "APIKey": ""
  },
  "APIKeySettings": {
    "SecretKey": "MeknesUfoN2bp5zvU78T/hBJZPm2GboLX68axxSp4p2CbFHsY="
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

> **Note**: The `SecretKey` is used for cryptographic signing of API keys. You can use the provided value for testing or generate your own.

### Step 2: Complete Scenario 1 - Create an A2A Server with Two Agents

Open `Start/Program.cs` and complete the TODOs to create an A2A server that exposes two agents:

| TODO | Description | Hint |
|------|-------------|------|
| TODO 1 | Create a WebApplication builder and build the app | Use `WebApplication.CreateBuilder(args)` and `builder.Build()` |
| TODO 2 | Get API Key settings and create APIKeyTools | Use `ConfigurationHelper.GetAPIKeySettings()` and pass it to `new APIKeyTools(...)` |
| TODO 3 | Create a list of AI tools for the Auth Agent | Use `AIFunctionFactory.Create()` to wrap `apiKeyTools.GenerateAPIKey` and `apiKeyTools.ValidateAPIKey` |
| TODO 4 | Create the AuthAgent (APIKeyAgent) | Use `chatClient.CreateAIAgent()` with name, instructions, and tools |
| TODO 5 | Create the CustomerToneAgent | Use `chatClient.CreateAIAgent()` with name and instructions (no tools needed) |
| TODO 6 | Get the AgentCards for both agents | Use `AgentCards.CreateAuthAgentCard()` and `AgentCards.CreateCustomerToneAgentCard()` |
| TODO 7 | Map the AuthAgent to an A2A endpoint | Use `app.MapA2A()` with the agent, path `/a2a/authAgent`, and agent card |
| TODO 8 | Map the CustomerToneAgent to an A2A endpoint | Use `app.MapA2A()` with the agent, path `/a2a/customerToneAgent`, and agent card |
| TODO 9 | Run the application | Use `await app.RunAsync()` |

### Step-by-Step Guide

#### TODO 1: Create WebApplication

The first step is to create an ASP.NET Core web application that will host our A2A server:

```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
```

#### TODO 2: Get API Key Settings and Create Tools

Load the API key settings from configuration and create the tools instance:

```csharp
var apiKeySettings = ConfigurationHelper.GetAPIKeySettings();
var apiKeyTools = new APIKeyTools(apiKeySettings);
```

#### TODO 3: Create AI Tools List

Create AI tools from the APIKeyTools methods using `AIFunctionFactory`:

```csharp
IList<AITool> tools = [
    AIFunctionFactory.Create(apiKeyTools.GenerateAPIKey, "generate_api_key"), 
    AIFunctionFactory.Create(apiKeyTools.ValidateAPIKey, "validate_api_key")
];
```

#### TODO 4: Create AuthAgent

Create the authentication agent with the API key tools:

```csharp
ChatClientAgent apiKeyAgent = chatClient.CreateAIAgent(
    name: "APIKeyAgent", 
    instructions: "You are a helpful API key assistant. You are able to generate and validate API keys.",
    tools: tools);
```

#### TODO 5: Create CustomerToneAgent

Create the customer tone agent (no tools needed, just analyzes tone):

```csharp
ChatClientAgent customerToneAgent = chatClient.CreateAIAgent(
    name: "CustomerToneAgent", 
    instructions: "You are a helpful customer tone assistant. You are able to detect the tone of a customer's message.");
```

#### TODO 6: Get Agent Cards

Agent cards describe the capabilities of each agent for A2A discovery:

```csharp
AgentCard authAgentCard = AgentCards.CreateAuthAgentCard();
AgentCard customerToneAgentCard = AgentCards.CreateCustomerToneAgentCard();
```

#### TODO 7 & 8: Map A2A Endpoints

Map each agent to an A2A endpoint and set up the well-known agent card endpoint:

```csharp
app.MapA2A(apiKeyAgent, path: "/a2a/authAgent", agentCard: authAgentCard, 
    taskManager => app.MapWellKnownAgentCard(taskManager, "/a2a/authAgent"));

app.MapA2A(customerToneAgent, path: "/a2a/customerToneAgent", agentCard: customerToneAgentCard, 
    taskManager => app.MapWellKnownAgentCard(taskManager, "/a2a/customerToneAgent"));
```

#### TODO 9: Run the Application

Finally, run the web application:

```csharp
await app.RunAsync();
```

### Step 3: Run and Test

```bash
cd Start
dotnet run
```

You should see output like:
```
Endpoint: https://your-resource.openai.azure.com/
Deployment: your-deployment-name
----------------------------------------
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

### Step 4: Test the Endpoints

Once the server is running, you can test the agent card endpoints:

```bash
# Get AuthAgent card
curl http://localhost:5000/.well-known/a2a/authAgent/agent.json

# Get CustomerToneAgent card
curl http://localhost:5000/.well-known/a2a/customerToneAgent/agent.json
```

## Key Concepts

| Concept | Description |
|---------|-------------|
| `A2A` | Agent-to-Agent protocol for inter-agent communication |
| `AgentCard` | Metadata describing an agent's capabilities, skills, and endpoint |
| `AgentSkill` | A specific capability/action that an agent can perform |
| `MapA2A()` | Extension method to expose an agent as an A2A endpoint |
| `MapWellKnownAgentCard()` | Sets up the `.well-known` endpoint for agent discovery |
| `AIFunctionFactory.Create()` | Creates an AI tool from a method |
| `ChatClientAgent` | An AI agent backed by a chat client |

## Understanding Agent Cards

Agent Cards are JSON documents that describe an agent's capabilities. They include:

```json
{
  "name": "AuthAgent",
  "description": "An authentication agent specialized in...",
  "version": "1.0.0",
  "defaultInputModes": ["text"],
  "defaultOutputModes": ["text"],
  "capabilities": {
    "streaming": false,
    "pushNotifications": false
  },
  "skills": [
    {
      "id": "auth_agent_generate_api_key",
      "name": "GenerateAPIKey",
      "description": "Generates a new random API key...",
      "tags": ["api", "key", "security"],
      "examples": ["Generate a new API key"]
    }
  ],
  "url": "http://localhost:5000/a2a/authAgent"
}
```

## A2A Server Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                        A2A SERVER                                    │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │                    ASP.NET Core WebApp                       │   │
│  └─────────────────────────────────────────────────────────────┘   │
│                              │                                      │
│              ┌───────────────┴───────────────┐                     │
│              ▼                               ▼                      │
│  ┌─────────────────────────┐    ┌─────────────────────────┐        │
│  │    /a2a/authAgent       │    │ /a2a/customerToneAgent  │        │
│  ├─────────────────────────┤    ├─────────────────────────┤        │
│  │  AuthAgent              │    │  CustomerToneAgent      │        │
│  │  - GenerateAPIKey()     │    │  - Detect customer tone │        │
│  │  - ValidateAPIKey()     │    │                         │        │
│  └─────────────────────────┘    └─────────────────────────┘        │
│              │                               │                      │
│              ▼                               ▼                      │
│  ┌─────────────────────────┐    ┌─────────────────────────┐        │
│  │ .well-known/a2a/        │    │ .well-known/a2a/        │        │
│  │ authAgent/agent.json    │    │ customerToneAgent/      │        │
│  │                         │    │ agent.json              │        │
│  └─────────────────────────┘    └─────────────────────────┘        │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

## Provided Files

The following files are provided and **should not be modified**:

| File | Description |
|------|-------------|
| `AgentCards.cs` | Contains agent card definitions for AuthAgent and CustomerToneAgent |
| `Tools/APIKeyTools.cs` | API key generation and validation tools with HMAC-SHA256 signing |
| `APIKeySettings.cs` | Settings class for API key secret |
| `AzureOpenAISettings.cs` | Azure OpenAI configuration model |
| `ConfigurationHelper.cs` | Helper to read configuration |
| `A2AServer.csproj` | Project file with all required dependencies |

The following file **should be modified** with your values:

| File | What to modify |
|------|----------------|
| `appsettings.json` | Update `Endpoint` and `ChatDeploymentName` |

## Expected Output

When the server starts successfully:

```
Endpoint: https://your-resource.openai.azure.com/
Deployment: your-deployment-name
----------------------------------------
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

## Troubleshooting

### Port Already in Use
If port 5000 is already in use:
```bash
# Find what's using the port
lsof -i :5000
# Or change the port in your app
app.Urls.Add("http://localhost:5001");
```

### Authentication Error
- Ensure you're logged in with Azure CLI: `az login`
- Verify your Azure OpenAI endpoint is correct
- Check that your deployment name matches

### Missing Package References
If you see errors about missing types, ensure your `.csproj` includes:
- `Microsoft.Agents.AI.A2A`
- `Microsoft.Agents.AI.Hosting.A2A.AspNetCore`

## Next Step

After completing this lab, proceed to **Lab06_A2AClient** to learn how to create a client that connects to and uses these A2A agents!

## Solution

If you get stuck, you can check the complete solution in the `Solution/` folder.
