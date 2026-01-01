# Lab 02 - AI Agent With Structured Output

## Objective

In this lab, you will create an AI Agent that returns **Structured Output** using the **Microsoft Agents Framework** with **Azure OpenAI**.

You will learn how to get structured JSON responses from an agent using three approaches: manual JSON parsing, automatic structured output with `RunAsync<T>`, and using `AIAgent` with `ChatOptions`.

## What You Will Learn

- How to configure an Azure OpenAI client with managed identity authentication
- How to create a ChatClient and wrap it as an AI Agent
- How to define response models for structured output (Restaurant example)
- How to use manual JSON parsing for structured responses
- How to use automatic structured output with `RunAsync<T>` (recommended approach with `ChatClientAgent`)
- How to use `AIAgent` with `ChatOptions` and `ChatResponseFormat` for structured output
- How to parse and work with strongly-typed responses from the agent

## Prerequisites

- .NET 8 SDK or later installed
- Azure OpenAI resource deployed
- Azure CLI logged in (`az login`) for DefaultAzureCredential

## Project Structure

```
Lab02-AIAgentWithSO/
├── README.md
├── Start/                      <-- Your working folder
│   ├── Program.cs              <-- Complete the TODOs here
│   ├── Models/
│   │   └── Restaurant.cs
│   ├── ConfigurationHelper.cs
│   ├── AzureOpenAISettings.cs
│   ├── appsettings.json
│   └── AIAgentWithSO.csproj
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

#### Scenario 1: Manual Structured Output - Restaurant Information

In this scenario, you manually specify the JSON format in the agent's instructions and parse the response yourself.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 3** | Create an agent with JSON format instructions | • Extension method: `chatClient.CreateAIAgent(instructions: "...", name: "...")` <br> • **Instructions to use:** <br> `"You are a culinary expert assistant. When asked about a restaurant, always respond with valid JSON in this exact format: { \"Name\": \"restaurant name\", \"ChefName\": \"head chef name\", \"Cuisine\": \"French\|Italian\|Japanese\|...\", \"MichelinStars\": number (0-3), \"AveragePricePerPerson\": number (in euros), \"City\": \"city name\", \"Country\": \"country name\", \"YearEstablished\": number } Only respond with the JSON, no other text."` <br> • **Name:** `"RestaurantInfoAgent"` |
| **TODO 4** | Run the agent with a restaurant question | • Method: `await agent.RunAsync(string prompt)` <br> • **Question to use:** `"Tell me about the restaurant 'Le Bernardin' in New York. Respond only with JSON."` <br> • Returns: `AgentRunResponse` |
| **TODO 5** | Parse the JSON response and display restaurant information | • Create `JsonSerializerOptions` with: <br> &nbsp;&nbsp;- `PropertyNameCaseInsensitive = true` <br> &nbsp;&nbsp;- `Converters = { new JsonStringEnumConverter() }` <br> • Use `JsonSerializer.Deserialize<Restaurant>(response.ToString(), options)` <br> • Wrap in try/catch for `JsonException` <br> • Display: Name, ChefName, Cuisine, MichelinStars, AveragePricePerPerson, City, Country, YearEstablished |
| **TODO 6** | Display token usage | • Access via `response.Usage` property <br> • Properties: `InputTokenCount`, `OutputTokenCount`, `TotalTokenCount` <br> • Use null-conditional: `response.Usage?.InputTokenCount` |

---

#### Scenario 2: Automatic Structured Output (Recommended) - Restaurant Information

In this scenario, you use the generic `RunAsync<T>` method which automatically generates a JSON schema from your type - no manual format specification needed!

> **Important:** The generic method `RunAsync<T>()` is only available with `ChatClientAgent`, not with the base `AIAgent` interface.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 7** | Create an agent with simple instructions (no JSON format needed) | • Extension method: `chatClient.CreateAIAgent(instructions: "...", name: "...")` <br> • **Instructions to use:** `"You are a culinary expert assistant. When asked about a restaurant."` <br> • **Name:** `"RestaurantInfoAgent"` <br> • Note: No JSON format in instructions - the framework handles it! |
| **TODO 8** | Run the agent with `RunAsync<Restaurant>` for automatic structured output | • Method: `await agent.RunAsync<Restaurant>(string prompt)` <br> • **Question to use:** `"Tell me about the restaurant 'Le Bernardin' in New York."` <br> • Returns: `AgentRunResponse<Restaurant>` (strongly-typed!) |
| **TODO 9** | Display the structured response | • Access properties via `response.Result.PropertyName` <br> • No JSON parsing needed - it's already a `Restaurant` object! <br> • Display: Name, ChefName, Cuisine, MichelinStars, AveragePricePerPerson, City, Country, YearEstablished |
| **TODO 10** | Display token usage | • Same as TODO 6: `response.Usage?.InputTokenCount`, etc. |

---

#### Scenario 3: Structured Output using AIAgent and ChatOptions - Restaurant Information

In this scenario, you use the base `AIAgent` interface with `ChatOptions` and `ChatResponseFormat.ForJsonSchema()`. This approach gives you more control and works with any agent type.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 11** | Create a JSON schema for the Restaurant type | • Method: `AIExtensions.AIJsonUtilities.CreateJsonSchema(typeof(Restaurant))` <br> • Returns: `JsonElement` containing the schema |
| **TODO 12** | Create `ChatOptions` with Instructions and ResponseFormat | • Create: `new AIExtensions.ChatOptions()` <br> • Set `Instructions`: `"You are a helpful assistant that can answer questions about restaurants."` <br> • Set `ResponseFormat`: `AIExtensions.ChatResponseFormat.ForJsonSchema(schema, schemaName, schemaDescription)` <br> • **schemaName:** `"RestaurantInfo"` <br> • **schemaDescription:** `"Information about a restaurant including its name, chef, cuisine, Michelin stars, average price per person, city, country, and year established"` |
| **TODO 13** | Create an `AIAgent` using `ChatClientAgentOptions` | • Method: `chatClient.CreateAIAgent(new ChatClientAgentOptions { ... })` <br> • Set `Name`: `"RestaurantInfoAgent"` <br> • Set `ChatOptions`: the chatOptions you created |
| **TODO 14** | Run the agent and deserialize the response | • Run: `await agent.RunAsync("Tell me about the restaurant 'Le Bernardin' in New York.")` <br> • Deserialize: `response.Deserialize<Restaurant>(JsonSerializerOptions.WebDefaults)` |
| **TODO 15** | Display the structured response | • Access properties via `restaurantInfo.PropertyName` <br> • Display: Name, ChefName, Cuisine, MichelinStars, AveragePricePerPerson, City, Country, YearEstablished |
| **TODO 16** | Display token usage | • Same pattern: `response.Usage?.InputTokenCount`, etc. |

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
| `ChatClientAgent` | Microsoft Agents wrapper around ChatClient (supports `RunAsync<T>`) |
| `AIAgent` | Base agent interface (use with `ChatOptions` for structured output) |
| `Structured Output` | JSON response with a defined schema |
| `RunAsync(prompt)` | Run agent and get text response (manual parsing needed) |
| `RunAsync<T>(prompt)` | Run agent with automatic structured output (only `ChatClientAgent`!) |
| `AgentRunResponse` | Response object containing the agent's reply as text |
| `AgentRunResponse<T>` | Response with strongly-typed `Result` property |
| `ChatOptions` | Configuration for chat including instructions and response format |
| `ChatResponseFormat.ForJsonSchema()` | Specifies JSON schema for structured output |
| `AIJsonUtilities.CreateJsonSchema()` | Creates JSON schema from a .NET type |
| `ChatClientAgentOptions` | Options for creating agent with ChatOptions |
| `JsonSerializer` | System.Text.Json class for manual JSON parsing |
| `JsonSerializerOptions` | Configuration for JSON serialization (case insensitivity, converters) |

## Namespaces Reference

| Namespace | Purpose |
|-----------|---------|
| `Azure.Identity` | Provides `DefaultAzureCredential` for Azure authentication |
| `Azure.AI.OpenAI` | Provides `AzureOpenAIClient` to connect to Azure OpenAI |
| `OpenAI` | Core SDK - extension methods like `CreateAIAgent` |
| `OpenAI.Chat` | Provides `ChatClient` for chat completions |
| `Microsoft.Agents.AI` | Provides `ChatClientAgent`, `AIAgent`, `AgentRunResponse`, `AgentRunResponse<T>`, and `ChatClientAgentOptions` |
| `Microsoft.Extensions.AI` | Provides `ChatOptions`, `ChatResponseFormat`, and `AIJsonUtilities` (aliased as `AIExtensions`) |
| `System.Text.Json` | Provides `JsonSerializer` and `JsonElement` for JSON operations |
| `System.Text.Json.Serialization` | Provides `JsonStringEnumConverter` for enum handling |
| `CommonUtilities` | Provides `ColoredConsole` for formatted output |

## Expected Output

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
----------------------------------------
=== Scenario 3: Structured output using AIAgent and ChatOptions ===
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
  Input tokens: 130
  Output tokens: 85
  Total tokens: 215
```

## Provided Files

The following files are provided and should not be modified:

- `AIAgentWithSO.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs` - Helper to read configuration
- `AzureOpenAISettings.cs` - Settings class for Azure OpenAI
- `Models/Restaurant.cs` - Restaurant model with all properties

The following files should be modified:

- `appsettings.json` - Update with your Azure OpenAI settings
- `Program.cs` - Complete the TODOs

## Useful Links

- [Producing Structured Output with Agents](https://learn.microsoft.com/en-us/agent-framework/tutorials/agents/structured-output?pivots=programming-language-csharp)

## Solution

If you get stuck, check the complete solution in the `Solution/` folder.
