# Lab 01 - First Basic AI Agent

## 🎯 Objective

In this lab, you will learn how to create your first AI Agent using the **Microsoft Agents Framework** with **Azure OpenAI**.

By the end of this lab, you will be able to:
- Configure an Azure OpenAI client with managed identity authentication
- Create a ChatClient and wrap it as an AI Agent
- Send a prompt and receive a response from the agent
- Create agents with custom instructions and names
- Use ChatMessages for fine-grained control over conversations
- Monitor token usage from agent responses

## 📋 Prerequisites

- .NET 10 SDK installed
- Azure OpenAI resource deployed
- Azure CLI logged in (`az login`) for DefaultAzureCredential

## 📁 Project Structure

```
Lab01-FirstBasicAIAgent/
├── README.md          # This file
├── Start/             # 👈 Your working folder (incomplete)
└── Solution/          # ✅ Reference solution
```

## 🚀 Scenarios Overview

| Scenario | Description |
|----------|-------------|
| **Setup** | Configuration and Client Initialization |
| **Scenario 1** | Basic Agent - Simple prompt with default settings |
| **Scenario 2** | Agent with Instructions - Custom behavior and identity |
| **Scenario 3** | Using ChatMessages - Fine-grained control with message roles |
| **Scenario 4** | Get consumed tokens from the agent run response |

---

## 🔧 Setup: Configuration and Client Initialization

### Step 1: Configure your Azure OpenAI settings

Open `Start/appsettings.json` and update the values with your Azure OpenAI resource:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
    "DeploymentName": "YOUR-DEPLOYMENT-NAME"
  }
}
```

### Step 2: Complete the Setup TODOs in Program.cs

| TODO | Description |
|------|-------------|
| **TODO 1** | Create an `AzureOpenAIClient` using the endpoint and `DefaultAzureCredential` |
| **TODO 2** | Get a `ChatClient` from the AzureOpenAIClient using the deployment name |

---

## 📝 Scenario 1: Basic Agent

Create a simple agent with no custom instructions.

| TODO | Description |
|------|-------------|
| **TODO 3** | Create a basic AI Agent using `CreateAIAgent()` |
| **TODO 4** | Run the agent with `RunAsync("Hello, what is the capital of France?")` |
| **TODO 5** | Display the response |

---

## 📝 Scenario 2: Agent with Instructions

Create an agent with custom instructions and a name.

| TODO | Description |
|------|-------------|
| **TODO 6** | Create an agent with `CreateAIAgent(instructions: "...", name: "GeographyAgent")` |
| **TODO 7** | Run the agent with a geography question |
| **TODO 8** | Display the response |

---

## 📝 Scenario 3: Using ChatMessages

Use `ChatMessage` objects for fine-grained control over the conversation.

| TODO | Description |
|------|-------------|
| **TODO 9** | Create a system message with `AIExtensions.ChatMessage` and `ChatRole.System` |
| **TODO 10** | Create a user message with `AIExtensions.ChatMessage` and `ChatRole.User` |
| **TODO 11** | Run the agent with `RunAsync([systemMessage, userMessage])` |
| **TODO 12** | Display the response |

---

## 📝 Scenario 4: Token Usage Monitoring

Monitor the token consumption from agent responses.

| TODO | Description |
|------|-------------|
| **TODO 13** | Create a new agent (e.g., ColorDecoratorAgent) |
| **TODO 14** | Run the agent with a question |
| **TODO 15** | Display the response |
| **TODO 16** | Display token usage: `InputTokenCount`, `OutputTokenCount`, `TotalTokenCount` |

---

## ▶️ Run and Test

```bash
cd Start
dotnet run
```

---

## 💡 Hints

<details>
<summary>Hint: Setup - Creating AzureOpenAIClient and ChatClient</summary>

```csharp
AzureOpenAIClient client = new AzureOpenAIClient(
    new Uri(settings.Endpoint), 
    new DefaultAzureCredential()
);
ChatClient chatClient = client.GetChatClient(settings.DeploymentName);
```
</details>

<details>
<summary>Hint: Scenario 1 - Basic Agent</summary>

```csharp
ChatClientAgent basicAgent = chatClient.CreateAIAgent();
AgentRunResponse basicResponse = await basicAgent.RunAsync("Hello, what is the capital of France?");
Console.WriteLine(basicResponse);
```
</details>

<details>
<summary>Hint: Scenario 2 - Agent with Instructions</summary>

```csharp
ChatClientAgent geographyAgent = chatClient.CreateAIAgent(
    instructions: "You are a helpful geography assistant.",
    name: "GeographyAgent");
AgentRunResponse geographyResponse = await geographyAgent.RunAsync("What is the surface area of France?");
Console.WriteLine(geographyResponse);
```
</details>

<details>
<summary>Hint: Scenario 3 - Using ChatMessages</summary>

```csharp
AIExtensions.ChatMessage systemMessage = new(
    AIExtensions.ChatRole.System,
    "You are a geography expert.");

AIExtensions.ChatMessage userMessage = new(
    AIExtensions.ChatRole.User,
    "What are the neighboring countries of France?");

AgentRunResponse chatMessageResponse = await geographyAgent.RunAsync([systemMessage, userMessage]);
Console.WriteLine(chatMessageResponse);
```
</details>

<details>
<summary>Hint: Scenario 4 - Token Usage</summary>

```csharp
ColoredConsole.WritePrimaryLogLine("Token Usage: ");
ColoredConsole.WriteSecondaryLogLine($"Input tokens: {colorResponse.Usage?.InputTokenCount}");
ColoredConsole.WriteSecondaryLogLine($"Output tokens: {colorResponse.Usage?.OutputTokenCount}");
ColoredConsole.WriteSecondaryLogLine($"Total tokens: {colorResponse.Usage?.TotalTokenCount}");
```
</details>

---

## ✅ Expected Output

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
Consumed tokens: 45
Output tokens: 25
Total tokens: 70
```

---

## 📚 Key Concepts

| Concept | Description |
|---------|-------------|
| `AzureOpenAIClient` | Client to connect to Azure OpenAI service |
| `DefaultAzureCredential` | Managed identity authentication (no API keys!) |
| `ChatClient` | Client for chat completions with a specific deployment |
| `ChatClientAgent` | Microsoft Agents wrapper around ChatClient |
| `AgentRunResponse` | Response object containing the agent's reply |
| `CreateAIAgent()` | Extension method to create an agent from ChatClient |
| `ChatMessage` | Message object with role (System/User/Assistant) and content |
| `Usage` | Token consumption metrics (Input/Output/Total) |

---

## 🔗 Resources

- [Microsoft Agents Framework Documentation](https://github.com/microsoft/agents)
- [Azure OpenAI Documentation](https://learn.microsoft.com/azure/ai-services/openai/)
- [Azure.Identity Documentation](https://learn.microsoft.com/dotnet/api/azure.identity)
