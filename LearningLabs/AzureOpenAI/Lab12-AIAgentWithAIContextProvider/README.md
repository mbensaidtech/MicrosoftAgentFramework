# Lab 12 - AI Agent with an AI Context Provider (Long-Term Memory)

## Objective

In this lab, you will give an agent a **long-term memory of its user** with **Microsoft Agent Framework** and **Azure OpenAI**.

A chat history (Lab 05) belongs to **one conversation**. A memory belongs to **the user**: what the agent learned about you in one conversation is still known in the next one, even after a restart of the application.
You will write an **`AIContextProvider`** — the extension point of the framework for memories, RAG and any other context — that runs around each agent run:

1. **Before** the model call (`ProvideAIContextAsync`): it loads the memories of the user from **MongoDB** and adds them to the instructions of the run.
2. **After** a successful run (`StoreAIContextAsync`): it asks a second agent to **extract** the new facts from the user message (**structured output**) and stores them in MongoDB.

The provider is attached once to the agent and shared by all its sessions: the id of the user lives **in the session**, not in the provider.

- **Scenario 1** — the user introduces herself in a first session; the memories are extracted and stored; in a **new session** (empty chat history), the agent still knows her name and her preferences.
- **Scenario 2** — chat with the agent at the console: it starts the conversation with what it remembers, and stores what you tell it.

## What You Will Learn

- How to write a custom `AIContextProvider`: `ProvideAIContextAsync` (before the model call) and `StoreAIContextAsync` (after a successful run)
- How to return additional context with `AIContext` (`Instructions`, and also `Messages` and `Tools`)
- How to attach providers to an agent with `ChatClientAgentOptions.AIContextProviders`
- Why a provider keeps **no session data in its fields**, and how it keeps its per-session state in the session: `ProviderSessionState<T>`, a `stateInitializer`, `StateKeys`
- What the provider receives: the messages passed to `RunAsync` (not the chat history), the answer of the agent
- How to extract data with **structured output** (`RunAsync<T>`) from another agent
- How to persist memories per user in MongoDB, so that they survive sessions and restarts

## Prerequisites

- .NET 10 SDK or later
- An Azure OpenAI (or Microsoft Foundry) resource with a chat model deployment (for example `gpt-4o-mini` or `gpt-5.4-mini`)
- One of the following for authentication:
  - the resource **API key**, or
  - Azure CLI logged in (`az login`) with the **Cognitive Services OpenAI User** role on the resource
- **Docker** (Docker Desktop or any Docker engine with Compose) for MongoDB
- Lab 02 (structured output) and Lab 05 (sessions, `ChatHistoryProvider`, `ProviderSessionState<T>`) completed

## Project Structure

```
Lab12-AIAgentWithAIContextProvider/
├── README.md
├── Start/                              <-- Your working folder
│   ├── Program.cs                      <-- Complete the TODOs here (the context provider is at the bottom)
│   ├── UserMemoriesRepository.cs       <-- MongoDB storage of the memories (provided)
│   ├── ExtractedMemories.cs            <-- Structured output of the memory extractor (provided)
│   ├── MemoryConsole.cs                <-- Display helpers (provided)
│   ├── MongoDB/
│   │   └── docker-compose.yml          <-- Local MongoDB
│   ├── ConfigurationHelper.cs
│   ├── AzureOpenAISettings.cs
│   ├── MongoDbSettings.cs
│   ├── appsettings.json
│   └── AIAgentWithAIContextProvider.csproj
└── Solution/                           <-- Reference solution
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
     },
     "MongoDb": {
       "ConnectionString": "mongodb://localhost:27018",
       "DatabaseName": "AIAgentContext"
     }
   }
   ```

   Use the resource endpoint shown in the Azure portal. The `/openai/v1/` suffix required by the v1 API is added for you.
   `*.openai.azure.com`, `*.cognitiveservices.azure.com` and `*.services.ai.azure.com` endpoints all work.
   The `MongoDb` values match the provided `docker-compose.yml`: keep them unless you use another MongoDB server.

2. Choose an authentication method:

   - **API key** (simplest for local development) — store it as a user secret, **never** in `appsettings.json`:

     ```bash
     cd Start
     dotnet user-secrets set "AzureOpenAI:APIKey" "<your-api-key>"
     ```

     The user secrets id is shared by all the labs: if you already did it for a previous lab, there is nothing to do.
     You can also use the environment variable `AzureOpenAI__APIKey`.

   - **Microsoft Entra ID** — leave `APIKey` unset and run `az login`. `DefaultAzureCredential` picks up your Azure CLI identity.

3. **Or use the dashboard**: the *Azure OpenAI settings* of the optional [Lab Bench dashboard](../../../Dashboard/README.md#azure-openai-settings) write the endpoint, the deployment and the API key to the same user secrets, for every migrated lab (no file to edit, no command to type).

### Step 2: Start MongoDB

```bash
cd Start/MongoDB
docker compose up -d
```

This starts a MongoDB 8.0 container named `lab12-mongodb`, reachable from your computer only (`127.0.0.1:27018`), **without authentication**: it is a local development database.
It uses port **27018**, so that it can run next to the MongoDB of Lab 05 (27017). Its data is kept in a Docker volume: the memories survive a restart of the container.

- See the memories stored by the agent: `docker exec lab12-mongodb mongosh AIAgentContext --quiet --eval "db.user_memories.find({}, {_id: 0, UserId: 1, Memory: 1})"`
- Stop MongoDB: `docker compose down` (add `-v` to also delete the stored memories)

### Step 3: Look at the provided files

- `UserMemoriesRepository.cs` — the memories of the users in the MongoDB collection `user_memories`, one document per memory (`UserId`, `Memory`, `CreatedAt`), with the official MongoDB driver: `GetMemoriesAsync(userId)`, `AddMemoriesAsync(userId, memories)`, `DeleteMemoriesAsync(userId)`. The memories belong to a **user**, not to a session.
- `ExtractedMemories.cs` — the type returned by the memory extractor with structured output: a list of facts (`Memories`), each one a short sentence such as *"The user is vegetarian."*. Its `[Description]` texts are sent to the model in the JSON schema.
- `MemoryConsole.cs` — display helpers. The provider writes `[Memory]` lines while the agent runs (`WriteMemoriesProvided`, `WriteMemoriesExtracted`, `WriteNothingToExtract`); `Program.cs` uses `WriteStoredMemories` and `WriteTokenUsage`. They are imported with `using static`, so you can call them directly.
- `ConfigurationHelper.cs` — `ConnectToMongoDbAsync()` connects to the database of the configuration and checks that MongoDB answers (clear error message when it is not started).
- `Program.cs`:
  - the constants `UserId`, `AgentName`, `AgentInstructions`, `ExtractorInstructions`, `IntroductionMessage` and `RecallQuestion`;
  - at the bottom, the class `UserMemoryAIContextProvider`. Its skeleton is provided:
    - the constructor receives the memory extractor agent, the repository and a **`stateInitializer`**;
    - `_sessionState` is a `ProviderSessionState<State>`: `GetOrInitializeState(session)` reads the state of the provider from the session (`AgentSession.StateBag`) or, the first time, creates it with the `stateInitializer` and saves it in the session;
    - `State` holds the id of the user: it is serialized with the session;
    - `StateKeys` returns the key of this state in the session: each provider of an agent must use its own keys.

  Why not keep the user id, or the memories loaded before the run, in fields of the provider? Because **one provider instance serves every session of the agent**, possibly at the same time: a field would mix the users and the conversations. Session data goes in the session; the provider only keeps its dependencies (the extractor agent and the repository).

### Step 4: Complete the Program.cs

Open `Start/Program.cs` and complete the TODOs:

---

#### Setup: Configuration and Client Initialization

Same as Lab 01.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 1** | Create the `OpenAIClientOptions` for the Azure OpenAI v1 endpoint | • `OpenAIClientOptions clientOptions = new() { Endpoint = AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint) };` <br> • `AzureOpenAIEndpoint.ToV1Uri(...)` (from `CommonUtilities`) returns the `.../openai/v1/` URI |
| **TODO 2** | Create the `OpenAIClient` | • `OpenAIClient client = !string.IsNullOrWhiteSpace(settings.APIKey) ? new OpenAIClient(new ApiKeyCredential(settings.APIKey), clientOptions) : new OpenAIClient(new BearerTokenPolicy(new DefaultAzureCredential(), "https://ai.azure.com/.default"), clientOptions);` <br> • API key when one is configured, otherwise Microsoft Entra ID; keep the `#pragma` lines around it |
| **TODO 3** | Get a `ChatClient` for the deployment | • `ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);` <br> • With Chat Completions, the service keeps no history: the agent sends it again at each run, from the session |

---

#### Agent with a long-term memory (shared by both scenarios)

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 4** | Connect to MongoDB and create the repository | • `IMongoDatabase database = await ConfigurationHelper.ConnectToMongoDbAsync();` <br> • `UserMemoriesRepository memoriesRepository = new(database);` |
| **TODO 5** | Create the memory extractor agent | • `AIAgent memoryExtractor = chatClient.AsAIAgent(instructions: ExtractorInstructions, name: "MemoryExtractor");` <br> • A plain agent: the provider will run it without a session, as a one-shot call, with structured output |
| **TODO 6** | Create the agent with the context provider | • `AIAgent memoryAgent = chatClient.AsAIAgent(new ChatClientAgentOptions { Name = AgentName, ChatOptions = new() { Instructions = AgentInstructions }, AIContextProviders = [new UserMemoryAIContextProvider(memoryExtractor, memoriesRepository, stateInitializer: _ => new UserMemoryAIContextProvider.State(UserId))] });` <br> • `AIContextProviders` is a list: an agent can have several providers, called in order <br> • The `stateInitializer` runs the first time the provider sees a session: every session of this agent is scoped to the user `UserId` |

---

#### Scenario 1: Remember the user from one session to another

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 7** | Start from an empty memory | • `long deletedMemories = await memoriesRepository.DeleteMemoriesAsync(UserId);` <br> • `ColoredConsole.WriteInfoLine($"Memories of user '{UserId}' cleared ({deletedMemories} deleted)");` <br> • So that the scenario gives the same result at each run |
| **TODO 8** | Run the first session | • `AgentSession firstSession = await memoryAgent.CreateSessionAsync();` <br> • `AgentResponse introductionResponse = await memoryAgent.RunAsync(IntroductionMessage, firstSession);` <br> • `ColoredConsole.WriteAssistantLine($"Agent: {introductionResponse.Text}");` <br> • Once the provider is written (TODO 13–16), the `[Memory]` lines appear **before** the answer: the provider runs inside `RunAsync` |
| **TODO 9** | Display the memories stored in MongoDB | • `WriteStoredMemories(UserId, await memoriesRepository.GetMemoriesAsync(UserId));` |
| **TODO 10** | Ask a question in a **new** session | • `AgentSession newSession = await memoryAgent.CreateSessionAsync();` <br> • `AgentResponse recallResponse = await memoryAgent.RunAsync(RecallQuestion, newSession);` <br> • `ColoredConsole.WriteAssistantLine($"Agent: {recallResponse.Text}");` <br> • `WriteTokenUsage(recallResponse);` <br> • The new session has no history: the name **Ada** and her preferences (vegetarian, hiking) come from the memories |

---

#### Scenario 2: Chat with the agent - it greets you with what it remembers

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 11** | Let the agent start the conversation | • `AgentSession chatSession = await memoryAgent.CreateSessionAsync();` <br> • `AgentResponse greeting = await memoryAgent.RunAsync(chatSession);` <br> • `ColoredConsole.WriteAssistantLine($"Agent: {greeting.Text}");` <br> • `RunAsync(session)` runs the agent without a new message: the provider is still called, so the greeting can use your name |
| **TODO 12** | Chat until an empty message or `exit` | • See the code below. `Console.ReadLine()` returns `null` when the input is closed |

TODO 12 — the chat loop:

```csharp
while (true)
{
    Console.Write("You: ");
    string? userInput = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(userInput) || userInput.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine();
        break;
    }

    AgentResponse response = await memoryAgent.RunAsync(userInput, chatSession);
    ColoredConsole.WriteAssistantLine($"Agent: {response.Text}");
}
```

---

#### The context provider: `UserMemoryAIContextProvider` (bottom of `Program.cs`)

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 13** | `ProvideAIContextAsync` — give the memories to the model | • `string userId = _sessionState.GetOrInitializeState(context.Session).UserId;` <br> • `List<string> memories = await _memoriesRepository.GetMemoriesAsync(userId, cancellationToken);` <br> • `WriteMemoriesProvided(userId, memories);` <br> • Return a `new AIContext { Instructions = ... }` that lists the memories, or `new AIContext()` when there is none. The framework appends these instructions to the instructions of the agent, for this run only |
| **TODO 14** | `StoreAIContextAsync` — get the user messages and the known memories | • `List<ChatMessage> userMessages = context.RequestMessages.Where(message => message.Role == ChatRole.User).ToList();` <br> • No user message (the agent spoke first): `WriteNothingToExtract();` then `return;` <br> • Then the user id (same as TODO 13) and `List<string> knownMemories = await _memoriesRepository.GetMemoriesAsync(userId, cancellationToken);` |
| **TODO 15** | Extract the new facts with structured output | • A list of messages: a system message with the facts already known, then the user messages (`.. userMessages`) <br> • `AgentResponse<ExtractedMemories> extraction = await _memoryExtractor.RunAsync<ExtractedMemories>(extractionMessages, cancellationToken: cancellationToken);` <br> • Write `(none)` when nothing is known yet: `knownMemories.DefaultIfEmpty("(none)")` |
| **TODO 16** | Store the new memories | • Keep the non-empty memories of `extraction.Result.Memories` that are not already known <br> • `await _memoriesRepository.AddMemoriesAsync(userId, newMemories, cancellationToken);` <br> • `WriteMemoriesExtracted(userId, newMemories, extraction.Usage);` <br> • The user id comes from the session, **never** from the model |

The two methods, complete (replace the methods of the skeleton):

```csharp
protected override async ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default)
{
    // Step 1: Get the id of the user from the state of the session
    string userId = _sessionState.GetOrInitializeState(context.Session).UserId;

    // Step 2: Load the memories of the user from MongoDB
    List<string> memories = await _memoriesRepository.GetMemoriesAsync(userId, cancellationToken);
    WriteMemoriesProvided(userId, memories);

    // Step 3: Return them as additional instructions (added after the instructions of the agent), or no context at all
    return memories.Count == 0
        ? new AIContext()
        : new AIContext { Instructions = "What you remember about the user from previous conversations:\n" + string.Join("\n", memories.Select(memory => $"- {memory}")) };
}
```

```csharp
protected override async ValueTask StoreAIContextAsync(InvokedContext context, CancellationToken cancellationToken = default)
{
    // Step 1: Get the user messages of the run (none when the agent spoke first) and the memories already known
    List<ChatMessage> userMessages = context.RequestMessages.Where(message => message.Role == ChatRole.User).ToList();
    if (userMessages.Count == 0)
    {
        WriteNothingToExtract();
        return;
    }

    string userId = _sessionState.GetOrInitializeState(context.Session).UserId;
    List<string> knownMemories = await _memoriesRepository.GetMemoriesAsync(userId, cancellationToken);

    // Step 2: Ask the extractor agent for the new facts, as structured output (no session: a one-shot run)
    List<ChatMessage> extractionMessages =
    [
        new(ChatRole.System, $"Facts already known about the user (do not extract them again):\n{string.Join("\n", knownMemories.DefaultIfEmpty("(none)"))}"),
        .. userMessages
    ];
    AgentResponse<ExtractedMemories> extraction = await _memoryExtractor.RunAsync<ExtractedMemories>(extractionMessages, cancellationToken: cancellationToken);

    // Step 3: Store the new memories in MongoDB (the user id comes from the session, never from the model)
    List<string> newMemories = extraction.Result.Memories
        .Where(memory => !string.IsNullOrWhiteSpace(memory) && !knownMemories.Contains(memory))
        .ToList();
    await _memoriesRepository.AddMemoriesAsync(userId, newMemories, cancellationToken);
    WriteMemoriesExtracted(userId, newMemories, extraction.Usage);
}
```

---

### Step 5: Run and Test

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

Scenario 2 waits for your messages: tell the agent something new about you (*"I play the piano every evening"*), then an empty line or `exit`.
Run the lab again with `scenariosToRun = [2]` at the top of `Program.cs`: the agent still knows it — the memories are in MongoDB, not in the process. (Scenario 1 clears the memories of the user at each run.)

## How an AIContextProvider works

```
                    RunAsync(message, session)
                               │
   ChatHistoryProvider ────────┤  loads the chat history of the session (empty in a new session)
                               │
   ┌───────────────────────────▼──────────────────────────────┐
   │ AIContextProvider.ProvideAIContextAsync   (your code)    │  memories of the user (MongoDB) → AIContext.Instructions
   │   receives: the messages passed to RunAsync, the session │
   └───────────────────────────┬──────────────────────────────┘
                               │ the framework merges the AIContext: agent instructions + memories
                               ▼
                       Chat Completions  ──►  answer
                               │
   ChatHistoryProvider ────────┤  stores the new messages in the session
   ┌───────────────────────────▼──────────────────────────────┐
   │ AIContextProvider.StoreAIContextAsync     (your code)    │  extractor agent (RunAsync<ExtractedMemories>) → MongoDB
   │   only after a successful run                            │
   └───────────────────────────┬──────────────────────────────┘
                               ▼
                   AgentResponse returned to the caller
```

| | `ChatHistoryProvider` (Lab 05) | `AIContextProvider` (this lab) |
|---|---|---|
| Role | Keeps the messages of **one conversation** | Adds **context** to each run: instructions, messages, tools |
| Per agent | One (`ChatClientAgentOptions.ChatHistoryProvider`) | Any number (`ChatClientAgentOptions.AIContextProviders`, called in order) |
| Before the model call | `ProvideChatHistoryAsync` | `ProvideAIContextAsync` → `AIContext` |
| After a successful run | `StoreChatHistoryAsync` | `StoreAIContextAsync` |
| Per-session state | In `AgentSession.StateBag` (e.g. the key of the conversation) | In `AgentSession.StateBag` (here: the id of the user) |
| Examples | in memory, vector store, MongoDB | memories (this lab), RAG (`TextSearchProvider`, Lab 07), a todo list, calendar events |

What the provider receives (base class `AIContextProvider`):

- `ProvideAIContextAsync`: `context.Session`, `context.Agent` and `context.AIContext` — its `Messages` are **only the messages passed to `RunAsync`** (not the chat history, not the messages of other providers). This lab does not use them: it loads all the memories of the user. A search-based memory would use them as the query.
- `StoreAIContextAsync`: `context.RequestMessages` (again only the messages passed to `RunAsync`) and `context.ResponseMessages` (the answer). It is **not called when the run failed**.
- The framework appends the `Instructions`, `Messages` and `Tools` you return to those of the run, and marks the messages as coming from your provider.

The cost of a memory, from a real run (`gpt-4o-mini`): the question of the new session costs **119 input tokens** with the 5 memories in the instructions, **71** without them; and each user message costs an **extraction run** (about 250 tokens, shown in the `[Memory]` line), a second model call that also adds latency to `RunAsync`.

## Key Concepts

| Concept | Description |
|---------|-------------|
| `AIContextProvider` | Base class of the components that add context to the runs of an agent (memories, RAG, tools...) and learn from them. Override `ProvideAIContextAsync` and/or `StoreAIContextAsync` |
| `ProvideAIContextAsync(InvokingContext)` | Called before each model call. Returns an `AIContext` merged with the instructions, messages and tools of the run |
| `StoreAIContextAsync(InvokedContext)` | Called after each successful run, with the request messages (those passed to `RunAsync`) and the response messages |
| `AIContext` | What a provider adds to a run: `Instructions`, `Messages`, `Tools` |
| `ChatClientAgentOptions.AIContextProviders` | The providers of the agent: one instance each, shared by all the sessions of the agent |
| `ProviderSessionState<T>` | Stores the typed state of a provider in the session (`GetOrInitializeState`, `SaveState`); its `stateInitializer` creates the state the first time |
| `StateKeys` | The keys of the provider in `AgentSession.StateBag`. Two providers of the same agent with the same key: `InvalidOperationException` when the agent is created |
| `RunAsync(session)` | Runs the agent without a new message (the agent speaks first) |
| `RunAsync<T>(messages)` / `AgentResponse<T>.Result` | Structured output (Lab 02): the answer deserialized as `T` |
| `agent.GetService<T>()` | Finds a provider of the agent, e.g. `memoryAgent.GetService<UserMemoryAIContextProvider>()` |

## Namespaces Reference

| Namespace | Purpose |
|-----------|---------|
| `OpenAI` | `OpenAIClient`, `OpenAIClientOptions` |
| `OpenAI.Chat` | `ChatClient` and the `AsAIAgent()` extension methods |
| `System.ClientModel` | `ApiKeyCredential` |
| `System.ClientModel.Primitives` | `BearerTokenPolicy` |
| `Azure.Identity` | `DefaultAzureCredential` |
| `Microsoft.Agents.AI` | `AIAgent`, `AgentSession`, `AgentResponse`, `AgentResponse<T>`, `ChatClientAgentOptions`, `AIContextProvider`, `AIContext`, `ProviderSessionState<T>` |
| `Microsoft.Extensions.AI` | `ChatMessage` (aliased, because `OpenAI.Chat` also has a `ChatMessage`), `ChatRole`, `UsageDetails` |
| `MongoDB.Driver` | `IMongoDatabase` |
| `CommonUtilities` | `ColoredConsole`, `AzureOpenAIEndpoint` |

## Expected Output

```
Endpoint: https://your-resource.openai.azure.com/
Deployment: your-deployment-name
----------------------------------------
Agent with a long-term memory - the memories of the user are stored in MongoDB
User id: ada
MemoryAssistant instructions: You are a friendly personal assistant. ...
Memory extractor instructions: You extract facts about the user from the user message: ...
----------------------------------------
=== Scenario 1: Remember the user from one session to another ===
Memories of user 'ada' cleared (6 deleted)
First session
User: Hi! I'm Ada, a software developer from Lyon. I love hiking in the Alps and I'm vegetarian.
[Memory] No memory of user 'ada' yet: nothing added to the instructions
[Memory] 5 memories extracted and stored for user 'ada' (extraction run: 265 tokens):
  + The user's name is Ada.
  + The user works as a software developer.
  + The user is from Lyon.
  + The user loves hiking in the Alps.
  + The user is vegetarian.
Agent: Hi Ada! It's great to meet you. Welcome! As a software developer and a vegetarian who enjoys hiking in the Alps, ...
Memories stored in MongoDB for user 'ada': 5
  - The user's name is Ada.
  - ...
----------------------------------------
New session (empty chat history)
User: What is my name? Suggest a dinner idea and a weekend activity for me.
[Memory] 5 memories of user 'ada' added to the instructions
[Memory] No new memory for user 'ada' (extraction run: 256 tokens)
Agent: Hello, Ada! It's great to hear from you. For dinner, how about a delicious vegetarian stir-fry with tofu, colorful veggies, and rice? ...
As for a weekend activity, since you love hiking in the Alps, how about planning a day hike on one of the beautiful trails near Lyon? ...
----------------------------------------
Token Usage (new session, with memories):
  Input tokens: 119
  Output tokens: 90
  Total tokens: 209
----------------------------------------
=== Scenario 2: Chat with the agent - it greets you with what it remembers ===
[Memory] 5 memories of user 'ada' added to the instructions
[Memory] No user message in this run: nothing to extract
Agent: Hello, Ada! I'm your friendly personal assistant, and I'm here to help you with anything you need. How can I assist you today?
Type a message, or an empty line / 'exit' to end the chat.
You: I work remotely on Fridays.
[Memory] 5 memories of user 'ada' added to the instructions
[Memory] 1 memory extracted and stored for user 'ada' (extraction run: 254 tokens):
  + The user works remotely on Fridays.
Agent: That's great to hear, Ada! Working remotely on Fridays can be a nice way to wrap up the week. ...
You: exit

Chat ended. Run scenario 2 again: the agent still remembers what you told it (the memories are in MongoDB).
```

Answers and token counts vary from one run to another; the extracted facts may be worded differently.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `'AzureOpenAI:Endpoint' is not configured` | Replace the `YOUR-...` placeholders in `appsettings.json` (or use user secrets / environment variables) |
| `401 Unauthorized` with an API key | Check the key (user secret `AzureOpenAI:APIKey`) and that it belongs to the resource of the endpoint |
| `401` / `403` with Entra ID | Run `az login` and make sure your identity has the **Cognitive Services OpenAI User** role on the resource |
| `404 DeploymentNotFound` | `ChatDeploymentName` must be the **deployment** name, not the model name |
| `MongoDB is not reachable ...` | Start MongoDB (`docker compose up -d` in `MongoDB/`) and check `docker ps` |
| `docker compose up` fails with *port is already allocated* | Another program uses port 27018: stop it, or change the published port in `docker-compose.yml` and `MongoDb:ConnectionString` |
| No `[Memory]` line at all | The provider is not attached: check `AIContextProviders = [...]` in TODO 6, and that the scenarios use `memoryAgent` |
| The new session does not know the name | `ProvideAIContextAsync` must return the memories in `AIContext.Instructions` (TODO 13), and `StoreAIContextAsync` must store them (TODO 16): check the `Memories stored in MongoDB` list |
| The first message gives `No new memory` | Keep `(none)` in the system message when nothing is known yet (`DefaultIfEmpty("(none)")`): with nothing after *"Facts already known about the user...:"*, `gpt-4o-mini` was observed to extract nothing |
| The same fact is stored twice | Pass the known memories to the extractor (TODO 15) and skip the known ones (TODO 16); a fact worded differently is still a new string |
| `InvalidOperationException: Multiple providers use the same state key ...` | Two providers of the agent use the same `StateKeys`: give each one its own key |
| Scenario 2 never ends | It waits for your messages: type an empty line or `exit` (in the dashboard, **End input**) |

## Security

- **What a provider returns is trusted by the model.** Instructions and messages from a provider are merged into the request as is — instructions have the highest trust. The memories come from a database, and were written by a model from user messages: a stored "fact" such as *"Ignore your instructions..."* would be an **indirect prompt injection**. The framework documentation asks to validate and sanitize data from external sources before returning it; frame it as data (*"What you remember about the user:"*), keep the extraction instructions strict, and only attach trusted providers.
- **The scope of the memories comes from the application, never from the model.** The user id is set by the `stateInitializer` and kept in the session; the extractor only returns the text of the facts. (The previous version of this lab asked the model to write the user id in its output.) In a real application, the `stateInitializer` uses the id of the **authenticated** user, and a restored session must be checked against the current user.
- **Memories are personal data.** Store them encrypted at rest with access control, keep a retention policy, and let users see and delete them (`DeleteMemoriesAsync` is the *"forget me"* of this lab).
- The MongoDB of the lab has **no authentication** and is bound to `127.0.0.1`: for local development only.

## Provided Files

The following files are provided and should not be modified:

- `AIAgentWithAIContextProvider.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs` - Loads and validates the configuration, connects to MongoDB
- `AzureOpenAISettings.cs` - Settings class for Azure OpenAI
- `MongoDbSettings.cs` - Settings class for MongoDB
- `UserMemoriesRepository.cs` - MongoDB storage of the memories of the users
- `ExtractedMemories.cs` - Structured output of the memory extractor
- `MemoryConsole.cs` - Display helpers
- `MongoDB/docker-compose.yml` - Local MongoDB

The following files should be modified:

- `appsettings.json` - Update with your Azure OpenAI endpoint and deployment
- `Program.cs` - Complete the TODOs (scenarios and context provider)

### NuGet packages

| Package | Why |
|---------|-----|
| `Microsoft.Agents.AI.OpenAI` | Agent Framework + `AsAIAgent()` for OpenAI clients (brings `OpenAI` and `Microsoft.Extensions.AI`) |
| `Azure.Identity` | `DefaultAzureCredential` for Entra ID authentication |
| `Microsoft.Extensions.Configuration.*` | `appsettings.json`, user secrets and environment variables, bound to the settings classes |
| `MongoDB.Driver` | Official MongoDB driver, for the memories |

## Going Further

- **Messages and tools instead of instructions**: an `AIContext` can also carry `Messages` (for example search results) and `Tools` (functions for this run only). The official sample `Agent_Step17_AdditionalAIContext` attaches two providers: a todo list that adds tools and its items, and a `MessageAIContextProvider` (a base class for providers that only add messages) for upcoming calendar events. When a provider adds messages, filter them out of the chat history (`InMemoryChatHistoryProviderOptions.StorageInputRequestMessageFilter`, as in the sample and in Lab 07).
- **Search instead of loading everything**: with many memories, search the relevant ones with the messages of the run (`context.AIContext.Messages`) — for example in a vector store. The framework provides `ChatHistoryMemoryProvider` (stores the messages in a `Microsoft.Extensions.VectorData` vector store and searches them, with a storage scope and a search scope such as the user id) and `TextSearchProvider` (RAG, Lab 07). See [Context provider integrations](https://learn.microsoft.com/agent-framework/integrations/by-component/context-providers/?pivots=programming-language-csharp) for the providers of other services (Azure Cosmos DB, Redis, Microsoft Foundry...).
- **Cheaper extraction**: extract every N messages, in the background, or from the answer of the agent too; keep the last facts only when a fact changes (*"I moved to Paris"*).
- **Set the state of a session yourself**: the official sample `01-get-started/04_memory` adds methods to its provider (`GetUserInfo(session)`, `SetUserInfo(session, ...)`) built on `ProviderSessionState<T>.GetOrInitializeState` / `SaveState`, and finds the provider with `agent.GetService<T>()`.
- **Full control**: override `InvokingCoreAsync` / `InvokedCoreAsync` (instead of `ProvideAIContextAsync` / `StoreAIContextAsync`) to change the filtering of the messages or the merging of the context, or pass message filters to the constructor of `AIContextProvider`.
- **Production**: index the `UserId` field of the collection, enable authentication on MongoDB, and decide what happens when the memory store is down (here the exception reaches `RunAsync`; the built-in providers log the error and continue without memories).

## Coming from an older version of the lab?

| Before (preview) | Now (1.22.0) |
|------------------|--------------|
| `Azure.AI.OpenAI` / `AzureOpenAIClient` | `OpenAI` / `OpenAIClient` + Azure OpenAI v1 endpoint |
| `chatClient.CreateAIAgent(...)`, variables typed `ChatClientAgent` | `chatClient.AsAIAgent(...)`, variables typed `AIAgent` |
| `AgentRunResponse` / `AgentRunResponse<T>` | `AgentResponse` / `AgentResponse<T>` |
| `AgentThread`, `agent.GetNewThread()`, `RunAsync(thread)` | `AgentSession`, `await agent.CreateSessionAsync()`, `RunAsync(session)` |
| `AIContextProviderFactory = _ => new UserMemoryAIContextProvider(...)` (one provider per thread) | `AIContextProviders = [new UserMemoryAIContextProvider(...)]` (one provider per agent, shared by its sessions; several providers possible) |
| `public override ValueTask<AIContext> InvokingAsync(InvokingContext context, ...)` | `protected override ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, ...)` — `InvokingAsync` is no longer overridable: it filters the messages, calls `ProvideAIContextAsync` and merges the result |
| `public override ValueTask InvokedAsync(InvokedContext context, ...)` | `protected override ValueTask StoreAIContextAsync(InvokedContext context, ...)` — only called after a successful run |
| User id and memories kept in fields of the provider (`_userId`, `_userMemories` filled before the run, read after it) | No session data in the provider: the user id in the session (`ProviderSessionState<State>` + `stateInitializer`), the memories reloaded from MongoDB |
| The extractor returned `List<UserMemory>` with a `UserId` written by the model | The extractor returns `ExtractedMemories` (text only); the user id comes from the session |
| `IUserMemoriesRepository` + `UserMemoriesRepository`, `Configuration/`, `Models/` | `UserMemoriesRepository` only (one implementation), `MongoDbSettings.cs`, `ExtractedMemories.cs` |
| `MongoDbHealthCheck` (`CommonUtilities`, `MongoDB.Driver` 2.30) | `ConfigurationHelper.ConnectToMongoDbAsync()` (`MongoDB.Driver` 3.12) |
| MongoDB with a password in `appsettings.json` + Mongo Express, project name from the folder | MongoDB without authentication bound to `127.0.0.1:27018`, own compose project `lab12-aiagent-memory`; `mongosh` to look at the data |
| API key in `appsettings.json` | API key in user secrets or environment variables |
| A chat loop only | Scenario 1 (scripted, two sessions) + scenario 2 (the chat loop) |

## Useful Links

- [Context Providers](https://learn.microsoft.com/agent-framework/concepts/agents/conversations/context-providers?pivots=programming-language-csharp)
- [Context provider integrations](https://learn.microsoft.com/agent-framework/integrations/by-component/context-providers/?pivots=programming-language-csharp)
- [Session](https://learn.microsoft.com/agent-framework/concepts/agents/conversations/session?pivots=programming-language-csharp)
- [Producing Structured Outputs with agents](https://learn.microsoft.com/agent-framework/agents/structured-output?pivots=programming-language-csharp)
- [Microsoft Agent Framework documentation](https://learn.microsoft.com/agent-framework/overview/?pivots=programming-language-csharp)
- [Official sample `04_memory`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/01-get-started/04_memory)
- [Official sample `Agent_Step17_AdditionalAIContext`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step17_AdditionalAIContext)
- [Official samples `AgentWithMemory`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/AgentWithMemory)
- [Azure OpenAI v1 API](https://learn.microsoft.com/azure/foundry/openai/api-version-lifecycle)

## Solution

If you get stuck, check the complete solution in the `Solution/` folder.
