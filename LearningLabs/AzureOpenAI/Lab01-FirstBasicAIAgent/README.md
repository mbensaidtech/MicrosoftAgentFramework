# Lab 01 - First Basic AI Agent

## Objective

In this lab, you will create your first AI Agent using the **Microsoft Agents Framework** with **Azure OpenAI**.

You will learn how to configure a client, create agents with different configurations, and monitor token usage from responses.

## What You Will Learn

- How to configure an Azure OpenAI client with managed identity authentication
- How to create a ChatClient and wrap it as an AI Agent
- How to send a prompt and receive a response from the agent
- How to create agents with custom instructions and names
- How to use ChatMessages for fine-grained control over conversations
- How to monitor token usage from agent responses

## Prerequisites

- .NET 10 SDK installed
- Azure OpenAI resource deployed
- Azure CLI logged in (`az login`) for DefaultAzureCredential

## Project Structure

```
Lab01-FirstBasicAIAgent/
├── README.md
├── Start/                      <-- Your working folder
│   ├── Program.cs              <-- Complete the TODOs here
│   ├── ConfigurationHelper.cs
│   ├── AzureOpenAISettings.cs
│   ├── appsettings.json
│   └── FirstBasicAIAgent.csproj
└── Solution/                   <-- Reference solution
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

Open `Start/Program.cs` and complete the TODOs:

---

#### Setup: Configuration and Client Initialization

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 1** | Create `AzureOpenAIClient` with managed identity authentication | • Constructor: `new AzureOpenAIClient(Uri endpoint, TokenCredential credential)` <br> • Use `new Uri(settings.Endpoint)` for the endpoint <br> • Use `new DefaultAzureCredential()` for authentication |
| **TODO 2** | Get a `ChatClient` for the deployment | • Method: `client.GetChatClient(string deploymentName)` <br> • Use `settings.ChatDeploymentName` |

---

#### Scenario 1: Basic Agent - Simple prompt with default settings

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 3** | Create a basic AI Agent from the ChatClient | • Extension method: `chatClient.CreateAIAgent()` <br> • No parameters needed for a basic agent <br> • Returns: `ChatClientAgent` |
| **TODO 4** | Run the agent with a simple string prompt | • Method: `await agent.RunAsync(string prompt)` <br> • Example prompt: "Hello, what is the capital of France?" <br> • Returns: `AgentRunResponse` |
| **TODO 5** | Display the response | • Use `ColoredConsole.WritePrimaryLogLine(response.ToString())` |

---

#### Scenario 2: Agent with Instructions - Custom behavior and identity

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 6** | Create an agent with instructions and a name | • Extension method: `chatClient.CreateAIAgent(instructions: "...", name: "...")` <br> • `instructions`: Define the agent's behavior (e.g., "You are a helpful geography assistant...") <br> • `name`: Give it a meaningful name (e.g., "GeographyAgent") |
| **TODO 7** | Run the agent with a geography question | • Method: `await agent.RunAsync(string prompt)` <br> • Ask a geography-related question (e.g., "What is the surface area of France?") |
| **TODO 8** | Display the response | • Use `ColoredConsole.WritePrimaryLogLine(response.ToString())` |

---

#### Scenario 3: Using ChatMessages - Fine-grained control with message roles

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 9** | Create an AI Agent | • Same as TODO 6: `chatClient.CreateAIAgent(instructions: "...", name: "...")` |
| **TODO 10** | Create a system message | • Constructor: `new AIExtensions.ChatMessage(AIExtensions.ChatRole.System, "content")` <br> • System messages define the agent's persona/behavior <br> • Example: "You are a geography expert. Provide detailed and accurate information." |
| **TODO 11** | Create a user message | • Constructor: `new AIExtensions.ChatMessage(AIExtensions.ChatRole.User, "content")` <br> • User messages contain the question/request <br> • Example: "What are the neighboring countries of France?" |
| **TODO 12** | Run the agent with an array of messages | • Method: `await agent.RunAsync([systemMessage, userMessage])` <br> • Pass messages as an array `[msg1, msg2]` |
| **TODO 13** | Display the response | • Use `ColoredConsole.WritePrimaryLogLine(response.ToString())` |

---

#### Scenario 4: Token Usage Monitoring

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 14** | Create an AI Agent | • Same pattern: `chatClient.CreateAIAgent(instructions: "...", name: "...")` <br> • Example: Create a "ColorDecoratorAgent" |
| **TODO 15** | Run the agent with a question | • Method: `await agent.RunAsync(string prompt)` <br> • Example: "What colors match with blue?" |
| **TODO 16** | Display the response | • Use `ColoredConsole.WritePrimaryLogLine(response.ToString())` |
| **TODO 17** | Display token usage | • Access via `response.Usage` property <br> • Properties: `InputTokenCount`, `OutputTokenCount`, `TotalTokenCount` <br> • Use null-conditional: `response.Usage?.InputTokenCount` <br> • Display with `ColoredConsole.WriteSecondaryLogLine(...)` |

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

## Key Concepts

| Concept | Description |
|---------|-------------|
| `AzureOpenAIClient` | Client to connect to Azure OpenAI service |
| `DefaultAzureCredential` | Managed identity authentication (no API keys!) |
| `ChatClient` | Client for chat completions with a specific deployment |
| `ChatClientAgent` | Microsoft Agents wrapper around ChatClient |
| `AgentRunResponse` | Response object containing the agent's reply |
| `CreateAIAgent()` | Extension method to create an agent from ChatClient |
| `RunAsync()` | Execute the agent with a prompt or messages |
| `ChatMessage` | Message object with role (System/User/Assistant) and content |
| `Usage` | Token consumption metrics (Input/Output/Total) |

## Namespaces Reference

| Namespace | Purpose |
|-----------|---------|
| `Azure.Identity` | Provides `DefaultAzureCredential` for Azure authentication |
| `Azure.AI.OpenAI` | Provides `AzureOpenAIClient` to connect to Azure OpenAI |
| `OpenAI` | Core SDK - extension methods like `CreateAIAgent` |
| `OpenAI.Chat` | Provides `ChatClient` for chat completions |
| `Microsoft.Agents.AI` | Provides `ChatClientAgent` and `AgentRunResponse` |
| `Microsoft.Extensions.AI` | Provides `ChatMessage` and `ChatRole` (aliased as `AIExtensions`) |
| `CommonUtilities` | Provides `ColoredConsole` for formatted output |

## Expected Output

```
Endpoint: https://your-resource.openai.azure.com/
Deployment: your-deployment-name
----------------------------------------
=== Scenario 1: Basic Agent ===
The capital of France is Paris.
----------------------------------------
=== Scenario 2: Agent with Instructions ===
The surface area of France is approximately 643,801 square kilometers.
----------------------------------------
=== Scenario 3: Using ChatMessages ===
- Belgium
- Luxembourg
- Germany
- Switzerland
- Italy
- Monaco
- Spain
- Andorra
----------------------------------------
=== Scenario 4: Get consumed tokens from the agent run response ===
- Light blue
- Navy blue
- White
- Gray
- Silver
----------------------------------------
Token Usage: 
  Input tokens: 45
  Output tokens: 25
  Total tokens: 70
```

## Provided Files

The following files are provided and should not be modified:

- `FirstBasicAIAgent.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs` - Helper to read configuration
- `AzureOpenAISettings.cs` - Settings class for Azure OpenAI

The following files should be modified:

- `appsettings.json` - Update with your Azure OpenAI settings
- `Program.cs` - Complete the TODOs

## Useful Links

- [Microsoft Agents Framework](https://github.com/microsoft/agents)
- [Azure OpenAI Documentation](https://learn.microsoft.com/azure/ai-services/openai/)
- [Azure.Identity Documentation](https://learn.microsoft.com/dotnet/api/azure.identity)

## Solution

If you get stuck, check the complete solution in the `Solution/` folder.
