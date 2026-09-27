# Lab 02 - AI Agent With Structured Output

## Objective

In this lab, you will create an AI Agent that returns **Structured Output** using **Microsoft Agent Framework** with **Azure OpenAI**.

Instead of free text, the agent answers with JSON that matches a .NET type (`Restaurant`). You will compare four approaches: describing the JSON format yourself in the instructions, the generic `RunAsync<T>` method, a response format configured on the agent, and a response format passed for a single (streamed) run.

## What You Will Learn

- Why describing a JSON format in the instructions is fragile, and how to parse the answer yourself
- How to get a strongly-typed result with `RunAsync<T>()` and `AgentResponse<T>.Result` (recommended when the type is known at compile time)
- How to generate a JSON schema from a .NET type with `ChatResponseFormat.ForJsonSchema<T>()`
- How to configure the response format of every run of an agent with `ChatClientAgentOptions`
- How to set the response format of a single run with `AgentRunOptions`
- How to get structured output from a streamed run with `RunStreamingAsync()` and `ToAgentResponse()`
- How `[Description]` attributes on your type guide the model through the JSON schema

## Prerequisites

- .NET 10 SDK or later
- An Azure OpenAI (or Microsoft Foundry) resource with a chat model deployment that supports structured outputs (for example `gpt-4o-mini` or `gpt-5.4-mini`)
- One of the following for authentication:
  - the resource **API key**, or
  - Azure CLI logged in (`az login`) with the **Cognitive Services OpenAI User** role on the resource
- Lab 01 completed (client creation, `AsAIAgent()`, `AgentResponse`, streaming)

## Project Structure

```
Lab02-AIAgentWithSO/
├── README.md
├── Start/                      <-- Your working folder
│   ├── Program.cs              <-- Complete the TODOs here
│   ├── Models/
│   │   └── Restaurant.cs       <-- The structured output type
│   ├── RestaurantConsole.cs    <-- Display helpers (provided)
│   ├── ConfigurationHelper.cs
│   ├── AzureOpenAISettings.cs
│   ├── appsettings.json
│   └── AIAgentWithSO.csproj
└── Solution/                   <-- Reference solution
    └── ...
```

## Instructions

### Step 1: Configure your settings

Settings are read, in this order (last wins): `appsettings.json` → **user secrets** → **environment variables**.

1. Open `Start/appsettings.json` and set the non-secret values:

   ```json
   {
     "AzureOpenAI": {
       "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
       "ChatDeploymentName": "YOUR-DEPLOYMENT-NAME"
     }
   }
   ```

   Use the resource endpoint shown in the Azure portal. The `/openai/v1/` suffix required by the v1 API is added for you.
   `*.openai.azure.com`, `*.cognitiveservices.azure.com` and `*.services.ai.azure.com` endpoints all work.

2. Choose an authentication method:

   - **API key** (simplest for local development) — store it as a user secret, **never** in `appsettings.json`:

     ```bash
     cd Start
     dotnet user-secrets set "AzureOpenAI:APIKey" "<your-api-key>"
     ```

     The user secrets id is shared by all the labs: if you already did it for Lab 01, there is nothing to do.
     You can also use the environment variable `AzureOpenAI__APIKey`.

   - **Microsoft Entra ID** — leave `APIKey` unset and run `az login`. `DefaultAzureCredential` picks up your Azure CLI identity.

3. **Or use the dashboard**: the *Azure OpenAI settings* of the optional [Lab Bench dashboard](../../../Dashboard/README.md#azure-openai-settings) write the endpoint, the deployment and the API key to the same user secrets, for every migrated lab (no file to edit, no command to type).

### Step 2: Look at the provided files

- `Models/Restaurant.cs` — the type the agent must fill. Each property has a `[Description]` attribute: it is copied into the JSON schema generated from the type and tells the model what to put there (for example, the price is in euros).
- `RestaurantConsole.cs` — `WriteRestaurant(restaurant)` and `WriteTokenUsage(response)` display the results. `Program.cs` imports them with `using static`, so you can call them directly.
- `Program.cs` — the setup region already contains `jsonOptions`, the `JsonSerializerOptions` used to generate the schema (scenarios 3 and 4) and to deserialize the JSON text yourself: web defaults (camelCase, case-insensitive property names) and enums as strings (`"French"`).

### Step 3: Complete the Program.cs

Open `Start/Program.cs` and complete the TODOs:

---

#### Setup: Configuration and Client Initialization

Same as Lab 01.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 1** | Create the `OpenAIClientOptions` for the Azure OpenAI v1 endpoint | • `new OpenAIClientOptions { Endpoint = ... }` <br> • `AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint)` (from `CommonUtilities`) returns the `.../openai/v1/` URI |
| **TODO 2** | Create the `OpenAIClient` | • API key: `new OpenAIClient(new ApiKeyCredential(settings.APIKey), clientOptions)` <br> • Entra ID: `new OpenAIClient(new BearerTokenPolicy(new DefaultAzureCredential(), "https://ai.azure.com/.default"), clientOptions)` <br> • Use a conditional on `string.IsNullOrWhiteSpace(settings.APIKey)` to support both |
| **TODO 3** | Get a `ChatClient` for the deployment | • `client.GetChatClient(settings.ChatDeploymentName)` |

---

#### Scenario 1: Manual Structured Output - JSON format described in the instructions

You describe the JSON format in the instructions and parse the answer yourself. It works most of the time, but nothing forces the model to follow the format: that is why you need a `try/catch`.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 4** | Create an agent with JSON format instructions | • `chatClient.AsAIAgent(instructions: "...", name: "RestaurantInfoAgent")` <br> • Use a raw string literal (`"""..."""`) for the instructions: <br> `You are a culinary expert assistant. When asked about a restaurant, always respond with valid JSON in this exact format: { "Name": "restaurant name", "ChefName": "head chef name", "Cuisine": "French\|Italian\|Japanese\|Chinese\|Mexican\|Indian\|Spanish\|American\|Mediterranean\|Thai\|Korean\|Vietnamese\|Other", "MichelinStars": number (0-3), "AveragePricePerPerson": number (in euros), "City": "city name", "Country": "country name", "YearEstablished": number } Only respond with the JSON, no other text.` |
| **TODO 5** | Run the agent | • `await restaurantAgent.RunAsync("Tell me about the restaurant 'Le Bernardin' in New York. Respond only with JSON.")` <br> • Returns: `AgentResponse` <br> • Optional: chain `.WithSpinner("Running agent")` |
| **TODO 6** | Parse the JSON and display the restaurant | • `Restaurant? restaurant = JsonSerializer.Deserialize<Restaurant>(response.Text, jsonOptions);` <br> • Wrap it in `try { ... } catch (JsonException ex) { ... }` and display `ex.Message` with `ColoredConsole.WriteErrorLine` <br> • If `restaurant is not null`: `WriteRestaurant(restaurant)` |
| **TODO 7** | Display token usage | • `WriteTokenUsage(response)` (it reads `response.Usage?.InputTokenCount`, `OutputTokenCount`, `TotalTokenCount`) |

---

#### Scenario 2: Automatic Structured Output with `RunAsync<T>` (Recommended)

`RunAsync<T>()` is available on **every** `AIAgent`. It generates the JSON schema from `T`, sends it as the response format of the run, and deserializes the answer for you. No JSON format in the instructions!

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 8** | Create an agent with simple instructions | • `chatClient.AsAIAgent(instructions: "You are a culinary expert assistant that answers questions about restaurants.", name: "RestaurantInfoAgent")` |
| **TODO 9** | Run the agent with `RunAsync<Restaurant>` | • `AgentResponse<Restaurant> response = await restaurantAgent.RunAsync<Restaurant>("Tell me about the restaurant 'Le Bernardin' in New York.");` <br> • Optional: chain `.WithSpinner("Running agent")` |
| **TODO 10** | Display the strongly-typed result | • `response.Result` is already a `Restaurant`: `WriteRestaurant(response.Result)` |
| **TODO 11** | Display token usage | • `AgentResponse<T>` derives from `AgentResponse`: `WriteTokenUsage(response)` |

---

#### Scenario 3: Structured Output configured on the agent - `ChatClientAgentOptions` and `ResponseFormat`

The response format is part of the agent configuration: every run returns JSON that follows the schema. The answer stays **JSON text** (`response.Text`): useful to log it, store it or pass it to another agent, and you deserialize it only when you need the object.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 12** | Create an agent with `ChatClientAgentOptions` | • `chatClient.AsAIAgent(new ChatClientAgentOptions { Name = "RestaurantInfoAgent", ChatOptions = new AIExtensions.ChatOptions { ... } })` <br> • `Instructions`: `"You are a culinary expert assistant that answers questions about restaurants."` <br> • `ResponseFormat`: `AIExtensions.ChatResponseFormat.ForJsonSchema<Restaurant>(jsonOptions, schemaName: "RestaurantInfo")` <br> • The schema description comes from the `[Description]` of the `Restaurant` class |
| **TODO 13** | Run the agent | • Non-generic `await restaurantAgent.RunAsync("Tell me about the restaurant 'Le Bernardin' in New York.")` |
| **TODO 14** | Display the JSON, then the deserialized restaurant | • `ColoredConsole.WriteSecondaryLogLine(response.Text)` <br> • `Restaurant restaurant = JsonSerializer.Deserialize<Restaurant>(response.Text, jsonOptions)!;` <br> • `WriteRestaurant(restaurant)` |
| **TODO 15** | Display token usage | • `WriteTokenUsage(response)` |

---

#### Scenario 4: Structured Output per run with `AgentRunOptions` - Streaming

The response format can also be given **for one run only**, with `AgentRunOptions`, to an agent that has none. This scenario streams the answer: you see the JSON arrive piece by piece, but you can only deserialize it once all the updates are received.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 16** | Create an agent with simple instructions | • Same as TODO 8 |
| **TODO 17** | Define the response format for this run | • `AgentRunOptions runOptions = new() { ResponseFormat = AIExtensions.ChatResponseFormat.ForJsonSchema<Restaurant>(jsonOptions, schemaName: "RestaurantInfo") };` |
| **TODO 18** | Stream the answer | • `await foreach (AgentResponseUpdate update in restaurantAgent.RunStreamingAsync("Tell me about the restaurant 'Le Bernardin' in New York.", options: runOptions)) { Console.Write(update.Text); updates.Add(update); }` <br> • Declare `List<AgentResponseUpdate> updates = [];` before the loop, and write a new line after it <br> • Don't use `.WithSpinner()` here: the spinner would overwrite the streamed text |
| **TODO 19** | Deserialize the complete answer | • `AgentResponse response = updates.ToAgentResponse();` combines the updates (text, usage) <br> • `JsonSerializer.Deserialize<Restaurant>(response.Text, jsonOptions)!` then `WriteRestaurant(...)` and `WriteTokenUsage(response)` |

---

### Step 4: Run and Test

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

To run only some scenarios, edit `scenariosToRun` at the top of `Program.cs` (for example `[2]`).

## Which approach should I use?

| Approach | When to use it |
|----------|----------------|
| JSON format in the instructions (scenario 1) | Only when the model or the agent does not support structured outputs. The format is a request, not a guarantee. |
| `RunAsync<T>()` (scenario 2) | **Default choice** when the type is known at compile time and you need the object. Also supports primitives and arrays (`RunAsync<List<Restaurant>>`). |
| `ResponseFormat` on the agent (scenario 3) | Every run of the agent must return this format, or you need the JSON text (logs, storage, inter-agent communication). |
| `ResponseFormat` in `AgentRunOptions` (scenario 4) | The format depends on the run, the type is only known at run time, or the schema is raw JSON (`ChatResponseFormat.ForJsonSchema(JsonElement, ...)`). It overrides the agent's response format for this run. |

When streaming, there is no `RunStreamingAsync<T>`: use a `ResponseFormat` (on the agent or in `AgentRunOptions`) and deserialize the combined response.

`ResponseFormat` (scenarios 3 and 4) only accepts JSON objects: to return a list, use `RunAsync<T>` or a wrapper type (`class RestaurantList { public List<Restaurant> Restaurants { get; set; } }`).

## Key Concepts

| Concept | Description |
|---------|-------------|
| Structured output | The model answers with JSON that follows a JSON schema, instead of free text |
| `RunAsync<T>()` | Available on every `AIAgent`: generates the schema from `T`, runs the agent, returns an `AgentResponse<T>` |
| `AgentResponse<T>` | An `AgentResponse` with a strongly-typed `Result` property (deserialized from `Text`) |
| `ChatResponseFormat.ForJsonSchema<T>()` | Creates a JSON schema response format from a .NET type (name, description and serializer options are optional) |
| `ChatResponseFormat.Json` | JSON without a schema ("JSON mode") |
| `ChatClientAgentOptions` | Advanced agent configuration: `Name`, `Description`, `ChatOptions` (instructions, response format, tools...) |
| `ChatOptions.ResponseFormat` | Response format used by every run of the agent |
| `AgentRunOptions.ResponseFormat` | Response format for a single run (overrides the agent's one) |
| `RunStreamingAsync()` / `ToAgentResponse()` | Stream a run, then combine the `AgentResponseUpdate`s into one `AgentResponse` to deserialize it |
| `[Description]` | `System.ComponentModel` attribute copied into the generated JSON schema |
| `JsonSerializerOptions.Web` + `JsonStringEnumConverter` | camelCase, case-insensitive names and enums as strings: the same options for the schema and for deserialization |

## Optional: Using the Console Spinner

The `CommonUtilities` library provides a `ConsoleSpinner` that shows a loading animation while the agent is processing. This is **optional** but improves the user experience of non-streaming runs.

**Usage with extension method:**
```csharp
// Simply chain .WithSpinner() to any async Task
AgentResponse response = await agent.RunAsync("your prompt")
    .WithSpinner("Running agent");

// Works with the generic RunAsync<T> too
AgentResponse<Restaurant> typedResponse = await agent.RunAsync<Restaurant>("your prompt")
    .WithSpinner("Running agent");
```

**Usage with using statement:**
```csharp
using (new ConsoleSpinner("Processing request"))
{
    response = await agent.RunAsync("your prompt");
} // Spinner stops when disposed
```

The spinner displays an animated indicator with elapsed time: `⠋ Running agent... [00:03]`

## Namespaces Reference

| Namespace | Purpose |
|-----------|---------|
| `OpenAI` | `OpenAIClient`, `OpenAIClientOptions` |
| `OpenAI.Chat` | `ChatClient` and the `AsAIAgent()` extension method |
| `System.ClientModel` | `ApiKeyCredential` |
| `System.ClientModel.Primitives` | `BearerTokenPolicy` |
| `Azure.Identity` | `DefaultAzureCredential` |
| `Microsoft.Agents.AI` | `AIAgent`, `AgentResponse`, `AgentResponse<T>`, `AgentResponseUpdate`, `AgentRunOptions`, `ChatClientAgentOptions`, `ToAgentResponse()` |
| `Microsoft.Extensions.AI` | `ChatOptions` and `ChatResponseFormat` (aliased as `AIExtensions` because `OpenAI.Chat` also defines a `ChatResponseFormat` and a `ChatMessage`) |
| `System.Text.Json` / `System.Text.Json.Serialization` | `JsonSerializer`, `JsonSerializerOptions`, `JsonStringEnumConverter` |
| `System.ComponentModel` | `[Description]` (in `Models/Restaurant.cs`) |
| `CommonUtilities` | `ColoredConsole`, `WithSpinner()`, `AzureOpenAIEndpoint` |

## Expected Output

```
Endpoint: https://your-resource.openai.azure.com/
Deployment: your-deployment-name
----------------------------------------
=== Scenario 1: Manually defined structured output ===
Successfully parsed structured response:
Name: Le Bernardin
Chef: Éric Ripert
Cuisine: French
Michelin Stars: 3
Average Price: €150
Location: New York, USA
Established: 1986
----------------------------------------
Token Usage:
  Input tokens: 166
  Output tokens: 72
  Total tokens: 238
----------------------------------------
=== Scenario 2: Automatically generated structured output with RunAsync<T> ===
Structured response:
Name: Le Bernardin
...
----------------------------------------
=== Scenario 3: Structured output configured on the agent ===
JSON response:
{"name":"Le Bernardin","chefName":"Éric Ripert","cuisine":"French","michelinStars":3,"averagePricePerPerson":150,"city":"New York","country":"USA","yearEstablished":1986}
Deserialized response:
Name: Le Bernardin
...
----------------------------------------
=== Scenario 4: Structured output with AgentRunOptions and streaming ===
{"name":"Le Bernardin","chefName":"Éric Ripert","cuisine":"French",...}
Deserialized response:
Name: Le Bernardin
...
```

Answers vary from one run to another (the average price in particular).

> **Note:** with Azure OpenAI, the schema is sent in *non-strict* mode by default. The model follows it, but may add a property that is not in the schema (with `gpt-4o-mini`, often a `"description"`). Deserialization simply ignores unknown properties. See [Going Further](#going-further) to enable strict mode.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `'AzureOpenAI:Endpoint' is not configured` | Replace the `YOUR-...` placeholders in `appsettings.json` (or use user secrets / environment variables) |
| `401 Unauthorized` with an API key | Check the key (user secret `AzureOpenAI:APIKey`) and that it belongs to the resource of the endpoint |
| `401` / `403` with Entra ID | Run `az login` and make sure your identity has the **Cognitive Services OpenAI User** role on the resource |
| `404 DeploymentNotFound` | `ChatDeploymentName` must be the **deployment** name, not the model name |
| `400` mentioning `response_format` / `json_schema` | The deployed model does not support structured outputs: use a more recent model (for example `gpt-4o-mini`) |
| `Failed to parse JSON` in scenario 1 | Expected from time to time: the format is only described in the instructions. Use scenario 2, 3 or 4 |
| `JsonException` about `Cuisine` | Enums must be (de)serialized as strings: use `jsonOptions` (it contains `JsonStringEnumConverter`) |

## Provided Files

The following files are provided and should not be modified:

- `AIAgentWithSO.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs` - Loads and validates the configuration
- `AzureOpenAISettings.cs` - Settings class for Azure OpenAI
- `Models/Restaurant.cs` - Restaurant model with its `[Description]` attributes
- `RestaurantConsole.cs` - Display helpers

The following files should be modified:

- `appsettings.json` - Update with your Azure OpenAI endpoint and deployment
- `Program.cs` - Complete the TODOs

### NuGet packages

| Package | Why |
|---------|-----|
| `Microsoft.Agents.AI.OpenAI` | Agent Framework + `AsAIAgent()` for OpenAI clients (brings `OpenAI` and `Microsoft.Extensions.AI`) |
| `Azure.Identity` | `DefaultAzureCredential` for Entra ID authentication |
| `Microsoft.Extensions.Configuration.*` | `appsettings.json`, user secrets and environment variables, bound to `AzureOpenAISettings` |

## Going Further

- **Strict schema**: with the OpenAI provider, you can ask the service to strictly enforce the schema (no extra property) by adding the provider-specific option `AdditionalProperties = new() { ["strict"] = true }` to the `ChatOptions` of scenario 3.
- **Raw JSON schema**: when there is no .NET type (schema loaded from configuration, declarative agents), use `ChatResponseFormat.ForJsonSchema(JsonElement.Parse(schemaJson), "RestaurantInfo", "...")` and read the result as a `JsonElement`.
- **Agents without native structured output**: the official sample `Agent_Step02_StructuredOutput` shows a decorator agent that converts a text answer into structured output with an additional LLM call.

## Coming from an older version of the lab?

| Before (preview) | Now (1.22.0) |
|------------------|--------------|
| `Azure.AI.OpenAI` / `AzureOpenAIClient` | `OpenAI` / `OpenAIClient` + Azure OpenAI v1 endpoint |
| `chatClient.CreateAIAgent(...)` | `chatClient.AsAIAgent(...)` |
| `RunAsync<T>()` only on `ChatClientAgent` | `RunAsync<T>()` on every `AIAgent` |
| `AgentRunResponse` / `AgentRunResponse<T>` | `AgentResponse` / `AgentResponse<T>` |
| `AIJsonUtilities.CreateJsonSchema(typeof(T))` + `ChatResponseFormat.ForJsonSchema(schema, name, description)` | `ChatResponseFormat.ForJsonSchema<T>()` |
| `response.Deserialize<T>(options)` | `JsonSerializer.Deserialize<T>(response.Text, options)` (or `RunAsync<T>`) |
| — | `AgentRunOptions.ResponseFormat` for a single run |
| API key in `appsettings.json` | API key in user secrets or environment variables |

## Useful Links

- [Producing Structured Outputs with agents](https://learn.microsoft.com/agent-framework/agents/structured-outputs?pivots=programming-language-csharp)
- [Microsoft Agent Framework documentation](https://learn.microsoft.com/agent-framework/overview/?pivots=programming-language-csharp)
- [Official sample `Agent_Step02_StructuredOutput`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step02_StructuredOutput)
- [Azure OpenAI structured outputs](https://learn.microsoft.com/azure/foundry/openai/how-to/structured-outputs)
- [Azure OpenAI v1 API](https://learn.microsoft.com/azure/foundry/openai/api-version-lifecycle)

## Solution

If you get stuck, check the complete solution in the `Solution/` folder.
