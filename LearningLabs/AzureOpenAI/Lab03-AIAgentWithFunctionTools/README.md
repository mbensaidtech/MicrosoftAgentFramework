# Lab 03 - AI Agent With Function Tools

## 🎯 Objective

In this lab, you will learn how to create an AI Agent that uses **Function Tools** (also known as Function Calling) using the **Microsoft Agents Framework** with **Azure OpenAI**.

By the end of this lab, you will be able to:
- Configure an Azure OpenAI client with managed identity authentication
- Create a ChatClient and wrap it as an AI Agent
- Define and register function tools for the agent
- Handle function tool invocations
- Build agents that can interact with external systems and APIs

## 📋 Prerequisites

- .NET 10 SDK installed
- Azure OpenAI resource deployed
- Azure CLI logged in (`az login`) for DefaultAzureCredential

## 📁 Project Structure

```
Lab03-AIAgentWithFunctionTools/
├── README.md          # This file
├── Start/             # 👈 Your working folder (incomplete)
│   └── Program.cs
└── Solution/          # ✅ Reference solution
    └── Program.cs
```

## 🚀 Scenarios Overview

| Scenario | Description |
|----------|-------------|
| **Setup** | Configuration and Client Initialization |
| **Scenario 1** | TBD |
| **Scenario 2** | TBD |

---

## 🔧 Setup: Configuration and Client Initialization

### Step 1: Configure your Azure OpenAI settings

Open `Start/appsettings.json` and update the values with your Azure OpenAI resource:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
    "ChatDeploymentName": "YOUR-DEPLOYMENT-NAME"
  }
}
```

### Step 2: Complete the Setup TODOs in Program.cs

| TODO | Description |
|------|-------------|
| **TODO 1** | Create an `AzureOpenAIClient` using the endpoint and `DefaultAzureCredential` |
| **TODO 2** | Get a `ChatClient` from the AzureOpenAIClient using the deployment name |

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
ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);
```
</details>

---

## 📚 Key Concepts

| Concept | Description |
|---------|-------------|
| `AzureOpenAIClient` | Client to connect to Azure OpenAI service |
| `DefaultAzureCredential` | Managed identity authentication (no API keys!) |
| `ChatClient` | Client for chat completions with a specific deployment |
| `Function Tools` | Functions that the agent can call to perform actions |
| `Tool Invocation` | The process of the agent calling a registered function |

---

## 🔗 Resources

- [Microsoft Agents Framework Documentation](https://github.com/microsoft/agents)
- [Azure OpenAI Documentation](https://learn.microsoft.com/azure/ai-services/openai/)
- [Azure OpenAI Function Calling](https://learn.microsoft.com/azure/ai-services/openai/how-to/function-calling)
