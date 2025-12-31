# Lab 02 - AI Agent With Structured Output

## 🎯 Objective

In this lab, you will learn how to create an AI Agent that returns **Structured Output** using the **Microsoft Agents Framework** with **Azure OpenAI**.

By the end of this lab, you will be able to:
- Configure an Azure OpenAI client with managed identity authentication
- Create a ChatClient and wrap it as an AI Agent
- Define response models for structured output (Restaurant example)
- Use manual JSON parsing for structured responses
- Use automatic structured output with `RunAsync<T>` (recommended approach)
- Parse and work with strongly-typed responses from the agent

## 📋 Prerequisites

- .NET 10 SDK installed
- Azure OpenAI resource deployed
- Azure CLI logged in (`az login`) for DefaultAzureCredential

## 📁 Project Structure

```
Lab02-AIAgentWithSO/
├── README.md          # This file
├── Start/             # 👈 Your working folder (incomplete)
│   ├── Models/
│   │   └── Restaurant.cs
│   └── Program.cs
└── Solution/          # ✅ Reference solution
    ├── Models/
    │   └── Restaurant.cs
    └── Program.cs
```

## 🚀 Scenarios Overview

| Scenario | Description |
|----------|-------------|
| **Setup** | Configuration and Client Initialization |
| **Scenario 1** | Manual Structured Output - Define JSON format in instructions and parse manually |
| **Scenario 2** | Automatic Structured Output (Recommended) - Use `RunAsync<T>` for automatic schema generation |

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

## 📝 Scenario 1: Manual Structured Output

Create an agent that returns structured JSON by specifying the format in instructions, then manually parse the response.

| TODO | Description |
|------|-------------|
| **TODO 3** | Create an agent with detailed JSON format instructions |
| **TODO 4** | Run the agent with a restaurant question |
| **TODO 5** | Display the scenario title |
| **TODO 6** | Parse the JSON response using `JsonSerializer.Deserialize<Restaurant>()` |
| **TODO 7** | Display the parsed restaurant information |
| **TODO 8** | Display token usage |

---

## 📝 Scenario 2: Automatic Structured Output (Recommended)

Create an agent that automatically generates structured output using the generic `RunAsync<T>` method.

| TODO | Description |
|------|-------------|
| **TODO 9** | Create an agent with simple instructions (no JSON format needed) |
| **TODO 10** | Display the scenario title |
| **TODO 11** | Run the agent with `RunAsync<Restaurant>()` for automatic schema generation |
| **TODO 12** | Display the structured response directly from `Result` property |
| **TODO 13** | Display token usage |

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

<details>
<summary>Hint: Scenario 1 - Manual Structured Output</summary>

```csharp
ChatClientAgent restaurantAgent = chatClient.CreateAIAgent(
    instructions: @"You are a culinary expert assistant. When asked about a restaurant, always respond with valid JSON in this exact format:
{
    ""Name"": ""restaurant name"",
    ""ChefName"": ""head chef name"",
    ""Cuisine"": ""French|Italian|Japanese|..."",
    ""MichelinStars"": number (0-3),
    ""AveragePricePerPerson"": number (in euros),
    ""City"": ""city name"",
    ""Country"": ""country name"",
    ""YearEstablished"": number
}
Only respond with the JSON, no other text.",
    name: "RestaurantInfoAgent");

AgentRunResponse response = await restaurantAgent.RunAsync("Tell me about 'Le Bernardin'...");

var options = new JsonSerializerOptions 
{ 
    PropertyNameCaseInsensitive = true,
    Converters = { new JsonStringEnumConverter() }
};
var restaurant = JsonSerializer.Deserialize<Restaurant>(response.ToString(), options);
```
</details>

<details>
<summary>Hint: Scenario 2 - Automatic Structured Output (Recommended)</summary>

```csharp
ChatClientAgent agent = chatClient.CreateAIAgent(
    instructions: @"You are a culinary expert assistant.",
    name: "RestaurantInfoAgent");

// The generic RunAsync<T> automatically generates JSON schema from the type!
AgentRunResponse<Restaurant> response = await agent.RunAsync<Restaurant>(
    "Tell me about the restaurant 'Le Bernardin' in New York.");

// Direct access to strongly-typed result - no manual parsing needed!
Console.WriteLine($"Name: {response.Result.Name}");
Console.WriteLine($"Chef: {response.Result.ChefName}");
```
</details>

---

## ✅ Expected Output

```
Endpoint: https://your-resource.openai.azure.com/
Deployment: your-deployment-name
----------------------------------------
=== Scenario 1: Manually defined structured output - Restaurant Information ===
Successfully parsed structured response:
Name: Le Bernardin
Chef: Eric Ripert
Cuisine: French
Michelin Stars: 3
Average Price: €150
Location: New York, United States
Established: 1986
----------------------------------------
Token Usage: 
  Input tokens: 150
  Output tokens: 85
  Total tokens: 235
----------------------------------------
=== Scenario 2: Automatically generated structured output - Restaurant Information ===
Structured response: 
Name: Le Bernardin
Chef: Eric Ripert
Cuisine: French
Michelin Stars: 3
Average Price: €150
Location: New York, United States
Established: 1986
----------------------------------------
Token Usage: 
  Input tokens: 120
  Output tokens: 85
  Total tokens: 205
```

---

## 📚 Key Concepts

| Concept | Description |
|---------|-------------|
| `AzureOpenAIClient` | Client to connect to Azure OpenAI service |
| `DefaultAzureCredential` | Managed identity authentication (no API keys!) |
| `ChatClient` | Client for chat completions with a specific deployment |
| `Structured Output` | JSON response with a defined schema |
| `RunAsync(prompt)` | Run agent and get text response (manual parsing needed) |
| `RunAsync<T>(prompt)` | Run agent with automatic structured output (recommended!) |
| `AgentRunResponse<T>` | Response with strongly-typed `Result` property |
| `JsonSerializer` | Manual JSON parsing for Scenario 1 |

---

## 🔗 Resources

- [Microsoft Agents Framework Documentation](https://github.com/microsoft/agents)
- [Azure OpenAI Documentation](https://learn.microsoft.com/azure/ai-services/openai/)
- [Azure OpenAI Structured Outputs](https://learn.microsoft.com/azure/ai-services/openai/how-to/structured-outputs)
