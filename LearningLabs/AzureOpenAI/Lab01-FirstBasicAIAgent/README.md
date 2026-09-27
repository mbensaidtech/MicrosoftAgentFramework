# Lab 01 - First Basic AI Agent

## Objective

In this lab, you will create your first AI Agent using **Microsoft Agent Framework** with **Azure OpenAI**.

You will learn how to connect to Azure OpenAI, create agents with different configurations, read token usage, and stream a response.

## What You Will Learn

- How to connect to Azure OpenAI through its **v1 API** with the official `OpenAI` SDK (API key or Microsoft Entra ID)
- How to turn a `ChatClient` into an `AIAgent` with `AsAIAgent()`
- How to send a prompt and read the `AgentResponse`
- How to give an agent instructions and a name
- How to use `ChatMessage`s for fine-grained control over a run
- How to read token usage from a response (including reasoning tokens)
- How to stream a response with `RunStreamingAsync()` and still get its token usage

## Prerequisites

- .NET 10 SDK or later
- An Azure OpenAI (or Microsoft Foundry) resource with a chat model deployment (for example `gpt-4o-mini` or `gpt-5.4-mini`)
- One of the following for authentication:
  - the resource **API key**, or
  - Azure CLI logged in (`az login`) with the **Cognitive Services OpenAI User** role on the resource

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

     The user secrets id is shared by all the labs: you only need to do this once.
     You can also use the environment variable `AzureOpenAI__APIKey`.

   - **Microsoft Entra ID** — leave `APIKey` unset and run `az login`. `DefaultAzureCredential` picks up your Azure CLI identity.

3. **Or use the dashboard**: the *Azure OpenAI settings* of the optional [Lab Bench dashboard](../../../Dashboard/README.md#azure-openai-settings) write the endpoint, the deployment and the API key to the same user secrets, for every migrated lab (no file to edit, no command to type).

### Step 2: Complete the Program.cs

Open `Start/Program.cs` and complete the TODOs:

---

#### Setup: Configuration and Client Initialization

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 1** | Create the `OpenAIClientOptions` for the Azure OpenAI v1 endpoint | • `new OpenAIClientOptions { Endpoint = ... }` <br> • `AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint)` (from `CommonUtilities`) returns the `.../openai/v1/` URI |
| **TODO 2** | Create the `OpenAIClient` | • API key: `new OpenAIClient(new ApiKeyCredential(settings.APIKey), clientOptions)` <br> • Entra ID: `new OpenAIClient(new BearerTokenPolicy(new DefaultAzureCredential(), "https://ai.azure.com/.default"), clientOptions)` <br> • Use a conditional on `string.IsNullOrWhiteSpace(settings.APIKey)` to support both |
| **TODO 3** | Get a `ChatClient` for the deployment | • Method: `client.GetChatClient(string deploymentName)` <br> • Use `settings.ChatDeploymentName` (with Azure, the deployment name plays the role of the model name) |

---

#### Scenario 1: Basic Agent - Simple prompt with default settings

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 4** | Create a basic AI Agent from the ChatClient | • Extension method: `chatClient.AsAIAgent()` <br> • No parameters needed for a basic agent <br> • Store it in an `AIAgent` variable |
| **TODO 5** | Run the agent with a simple string prompt | • Method: `await agent.RunAsync(string message)` <br> • Example prompt: "Hello, what is the capital of France?" <br> • Returns: `AgentResponse` <br> • Optional: chain `.WithSpinner("Running agent")` |
| **TODO 6** | Display the response | • `response.Text` contains the text of the answer <br> • `ColoredConsole.WritePrimaryLogLine(response.Text)` |

---

#### Scenario 2: Agent with Instructions - Custom behavior and identity

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 7** | Create an agent with instructions and a name | • `chatClient.AsAIAgent(instructions: "...", name: "...")` <br> • `instructions`: define the agent's behavior (e.g., "You are a helpful geography assistant...") <br> • `name`: give it a meaningful name (e.g., "GeographyAgent") |
| **TODO 8** | Run the agent with a geography question | • `await agent.RunAsync(string message)` <br> • e.g., "What is the surface area of France?" |
| **TODO 9** | Display the response | • `ColoredConsole.WritePrimaryLogLine(response.Text)` <br> • Optional: show `agent.Name` in the scenario title |

---

#### Scenario 3: Using ChatMessages - Fine-grained control with message roles

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 10** | Create an AI Agent | • Same as TODO 7: `chatClient.AsAIAgent(instructions: "...", name: "...")` |
| **TODO 11** | Create a system message | • `new AIExtensions.ChatMessage(AIExtensions.ChatRole.System, "content")` <br> • It is added to the agent instructions for this run only <br> • System messages must always be written by the developer, never built from end-user input |
| **TODO 12** | Create a user message | • `new AIExtensions.ChatMessage(AIExtensions.ChatRole.User, "content")` <br> • e.g., "What are the neighboring countries of France?" |
| **TODO 13** | Run the agent with the messages | • `await agent.RunAsync([systemMessage, userMessage])` <br> • `RunAsync` accepts any `IEnumerable<ChatMessage>` |
| **TODO 14** | Display the response | • `ColoredConsole.WritePrimaryLogLine(response.Text)` |

---

#### Scenario 4: Token Usage Monitoring

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 15** | Create an AI Agent | • Same pattern: `chatClient.AsAIAgent(instructions: "...", name: "...")` <br> • e.g., a "ColorDecoratorAgent" |
| **TODO 16** | Run the agent with a question | • `await agent.RunAsync(string message)` <br> • e.g., "What colors match with blue?" |
| **TODO 17** | Display the response | • `ColoredConsole.WritePrimaryLogLine(response.Text)` |
| **TODO 18** | Display token usage | • `response.Usage` (type `UsageDetails`, may be `null`) <br> • Properties: `InputTokenCount`, `OutputTokenCount`, `TotalTokenCount` <br> • `ReasoningTokenCount`: part of the output tokens spent on hidden reasoning by reasoning models (`null` or `0` otherwise) <br> • Use null-conditional: `response.Usage?.InputTokenCount` <br> • Display with `ColoredConsole.WriteSecondaryLogLine(...)` |

---

#### Scenario 5: Streaming

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 19** | Create an AI Agent | • e.g., a "StorytellerAgent" whose instructions ask for short stories |
| **TODO 20** | Stream the answer | • `agent.RunStreamingAsync(string message)` returns `IAsyncEnumerable<AgentResponseUpdate>` <br> • `await foreach (AgentResponseUpdate update in agent.RunStreamingAsync("...")) { Console.Write(update.Text); }` <br> • Keep each update in a `List<AgentResponseUpdate> updates` <br> • Don't use `.WithSpinner()` here: the spinner would overwrite the streamed text |
| **TODO 21** | Display the token usage of the streamed run | • `AgentResponse streamedResponse = updates.ToAgentResponse();` combines all the updates into one response <br> • Then read `streamedResponse.Usage` as in TODO 18 |

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

To run only some scenarios, edit `scenariosToRun` at the top of `Program.cs` (for example `[5]`).

## Key Concepts

| Concept | Description |
|---------|-------------|
| Azure OpenAI **v1 API** | OpenAI-compatible endpoint (`https://<resource>.openai.azure.com/openai/v1/`), no `api-version` needed, usable with the official `OpenAI` SDK |
| `OpenAIClient` | Entry point of the `OpenAI` SDK; `OpenAIClientOptions.Endpoint` points it to Azure |
| `ApiKeyCredential` | API key authentication |
| `BearerTokenPolicy` + `DefaultAzureCredential` | Microsoft Entra ID authentication with automatic token refresh (scope `https://ai.azure.com/.default`) |
| `ChatClient` | Client for the Chat Completions API of one deployment |
| `AsAIAgent()` | Extension method that wraps a `ChatClient` into an agent (a `ChatClientAgent`) |
| `AIAgent` | Common abstraction of every agent in Agent Framework, whatever the provider |
| `RunAsync()` | Runs the agent with a string, a `ChatMessage` or a collection of messages |
| `AgentResponse` | Result of a run: `Text`, `Messages`, `Usage`, `FinishReason`... |
| `RunStreamingAsync()` / `AgentResponseUpdate` | Streaming run: the answer arrives as a sequence of updates |
| `ToAgentResponse()` | Combines streamed updates into a single `AgentResponse` (text, messages, usage) |
| `ChatMessage` | Message with a role (System/User/Assistant) and content (from `Microsoft.Extensions.AI`) |
| `Usage` | Token consumption metrics (Input/Output/Reasoning/Total) |

## Optional: Using the Console Spinner

The `CommonUtilities` library provides a `ConsoleSpinner` that shows a loading animation while the agent is processing. This is **optional** but improves the user experience of non-streaming runs.

**Usage with extension method:**
```csharp
// Simply chain .WithSpinner() to any async Task
AgentResponse response = await agent.RunAsync("your prompt")
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
| `Microsoft.Agents.AI` | `AIAgent`, `AgentResponse`, `AgentResponseUpdate`, `ToAgentResponse()` |
| `Microsoft.Extensions.AI` | `ChatMessage` and `ChatRole` (aliased as `AIExtensions` because `OpenAI.Chat` also defines a `ChatMessage`) |
| `CommonUtilities` | `ColoredConsole`, `WithSpinner()`, `AzureOpenAIEndpoint` |

## Expected Output

```
Endpoint: https://your-resource.openai.azure.com/
Deployment: your-deployment-name
----------------------------------------
=== Scenario 1: Basic Agent ===
The capital of France is Paris.
----------------------------------------
=== Scenario 2: Agent with Instructions (GeographyAgent) ===
The surface area of France is approximately 551,695 square kilometers...
----------------------------------------
=== Scenario 3: Using ChatMessages ===
- Belgium
- Luxembourg
- Germany
- Switzerland
- Italy
- Spain
- Andorra
- Monaco
----------------------------------------
=== Scenario 4: Get consumed tokens from the agent response ===
- White
- Gray
- Navy
- ...
----------------------------------------
Token Usage:
  Input tokens: 56
  Output tokens: 29
  Reasoning tokens (included in output): 0
  Total tokens: 85
----------------------------------------
=== Scenario 5: Streaming ===
Under a lavender sky, Sophie wandered through the cobblestone streets of Montmartre...
----------------------------------------
Token Usage (streaming):
  Input tokens: 38
  Output tokens: 120
  Total tokens: 158
```

Answers vary from one run to another.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `'AzureOpenAI:Endpoint' is not configured` | Replace the `YOUR-...` placeholders in `appsettings.json` (or use user secrets / environment variables) |
| `401 Unauthorized` with an API key | Check the key (user secret `AzureOpenAI:APIKey`) and that it belongs to the resource of the endpoint |
| `401` / `403` with Entra ID | Run `az login` and make sure your identity has the **Cognitive Services OpenAI User** role on the resource |
| `404 DeploymentNotFound` | `ChatDeploymentName` must be the **deployment** name, not the model name |

## Provided Files

The following files are provided and should not be modified:

- `FirstBasicAIAgent.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs` - Loads and validates the configuration
- `AzureOpenAISettings.cs` - Settings class for Azure OpenAI

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

- **Responses API**: the same client can create an agent on the Responses API, which Microsoft recommends for new Azure OpenAI apps:
  `client.GetResponsesClient().AsAIAgent(model: settings.ChatDeploymentName, instructions: "...")`.
  By default the conversation history is then stored by the service; the next labs use the Chat Completions API so that Agent Framework manages the history (sessions, persistence).
- **Microsoft Foundry Agent Service**: the official getting-started samples use `Microsoft.Agents.AI.Foundry` (`AIProjectClient.AsAIAgent(...)`).

## Coming from an older version of the lab?

| Before (preview) | Now (1.22.0) |
|------------------|--------------|
| `Azure.AI.OpenAI` / `AzureOpenAIClient` | `OpenAI` / `OpenAIClient` + Azure OpenAI v1 endpoint |
| `chatClient.CreateAIAgent(...)` | `chatClient.AsAIAgent(...)` |
| `ChatClientAgent` variables | `AIAgent` variables (`AsAIAgent` still returns a `ChatClientAgent`) |
| `AgentRunResponse` / `AgentRunResponseUpdate` | `AgentResponse` / `AgentResponseUpdate` |
| API key in `appsettings.json` | API key in user secrets or environment variables |

## Useful Links

- [Microsoft Agent Framework documentation](https://learn.microsoft.com/agent-framework/overview/?pivots=programming-language-csharp)
- [Microsoft Agent Framework on GitHub](https://github.com/microsoft/agent-framework) — see `dotnet/samples/02-agents/AgentProviders/azure`
- [Azure OpenAI v1 API](https://learn.microsoft.com/azure/foundry/openai/api-version-lifecycle)
- [Safe storage of app secrets (user secrets)](https://learn.microsoft.com/aspnet/core/security/app-secrets)
- [Azure.Identity documentation](https://learn.microsoft.com/dotnet/api/azure.identity)

## Solution

If you get stuck, check the complete solution in the `Solution/` folder.
