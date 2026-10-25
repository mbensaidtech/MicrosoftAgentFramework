# Lab 05 - AI Agent with Sessions (Conversation Persistence)

## Objective

In this lab, you will build multi-turn conversations with **Microsoft Agent Framework** and **Azure OpenAI**, save them, and resume them later with their full context.

A conversation lives in an **`AgentSession`**. The messages of the conversation (the *chat history*) are kept by a **`ChatHistoryProvider`** attached to the agent. You will run the same conversation three times, and change where the chat history is kept, and how much of it:

1. **In the session itself** — the default `InMemoryChatHistoryProvider`: the serialized session contains the whole conversation. You **serialize** the session, save it as text, **restore** it, and ask a follow-up question that only makes sense with the history ("that city", "my name").
2. **Bounded** — the same provider with a **chat reducer** that keeps only the last messages: the cost of each run stops growing, and the agent forgets the beginning of the conversation.
3. **In MongoDB** — a custom `ChatHistoryProvider` on a real database: the serialized session only contains a key, the conversation survives the application, a new agent resumes it.

## What You Will Learn

- How to create a session with `CreateSessionAsync()` and run an agent in it
- How to save a session with `SerializeSessionAsync()` and resume it with `DeserializeSessionAsync()`
- Where the chat history lives with the default `InMemoryChatHistoryProvider`, and how to read it (`GetService<InMemoryChatHistoryProvider>()`, `GetMessages(session)`)
- How to plug a custom `ChatHistoryProvider` with `ChatClientAgentOptions.ChatHistoryProvider`
- How a provider keeps its per-session state (a storage key) in the session: `AgentSession.StateBag` and `ProviderSessionState<T>`
- How to store the chat history in MongoDB with your own `ChatHistoryProvider`
- How to **bound** the chat history with a chat reducer (`MessageCountingChatReducer`), and what it costs: the agent forgets
- What changes when the **service** keeps the conversation (Responses API), and how to protect stored sessions

## Prerequisites

- .NET 10 SDK or later
- An Azure OpenAI (or Microsoft Foundry) resource with a chat model deployment (for example `gpt-4o-mini` or `gpt-5.4-mini`)
- One of the following for authentication:
  - the resource **API key**, or
  - Azure CLI logged in (`az login`) with the **Cognitive Services OpenAI User** role on the resource
- **Docker** (Docker Desktop or any Docker engine with Compose) for scenario 3 (MongoDB)
- Lab 01 completed (client creation, `AsAIAgent()`, `AgentResponse`)

## Project Structure

```
Lab05-AIAgentWithThreads/
├── README.md
├── Start/                                  <-- Your working folder
│   ├── Program.cs                          <-- Complete the TODOs here
│   ├── Stores/
│   │   └── MongoChatHistoryProvider.cs     <-- ChatHistoryProvider on MongoDB (scenario 3)
│   ├── MongoDB/
│   │   └── docker-compose.yml              <-- Local MongoDB for scenario 3
│   ├── SessionConsole.cs                   <-- Display helpers (provided)
│   ├── ConfigurationHelper.cs
│   ├── AzureOpenAISettings.cs
│   ├── MongoDbSettings.cs
│   ├── appsettings.json
│   └── AIAgentWithThreads.csproj
└── Solution/                               <-- Reference solution
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
       "ConnectionString": "mongodb://localhost:27017",
       "DatabaseName": "AIAgentSessions"
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

### Step 2: Start MongoDB (scenario 3)

```bash
cd Start/MongoDB
docker compose up -d
```

This starts a MongoDB 8.0 container named `lab05-mongodb`, reachable from your computer only (`127.0.0.1:27017`), **without authentication**: it is a local development database. Its data is kept in a Docker volume, so the conversations survive a restart of the container.

- See what the agent stored: `docker exec lab05-mongodb mongosh AIAgentSessions --quiet --eval "db.chat_history.find({}, {_id: 0, SessionId: 1, Role: 1, MessageText: 1})"`
- Stop MongoDB: `docker compose down` (add `-v` to also delete the stored conversations)

Scenarios 1 and 2 do not need MongoDB. To skip scenario 3, set `scenariosToRun` to `[1, 2]` at the top of `Program.cs`.

### Step 3: Look at the provided files

- `Stores/MongoChatHistoryProvider.cs` — a `ChatHistoryProvider` on a MongoDB collection (`chat_history`), with the official MongoDB driver, adapted from the official sample `Agent_Step04_3rdPartyChatHistoryStorage`. It overrides two methods:
  - `ProvideChatHistoryAsync` — called **before** each run: loads the last 10 messages of the session from MongoDB. The agent sends them to the model before the new message.
  - `StoreChatHistoryAsync` — called **after** each successful run: stores the new messages (`RequestMessages` + `ResponseMessages`). Each message is a document: `SessionId`, `Timestamp`, `Order`, `Role`, `MessageText` and the serialized `ChatMessage`.

  The provider instance is shared by all the sessions of the agent, so it keeps **no session data in its fields**. The key of the conversation (`SessionDbKey`) is stored **in the session**, through `ProviderSessionState<State>`: `GetOrInitializeState(session)` reads it from `AgentSession.StateBag`, or creates a new key the first time. That is why the key is saved and restored with the session.
- `ConfigurationHelper.cs` — `ConnectToMongoDbAsync()` connects to the database of the configuration and checks that MongoDB answers (clear error message when it is not started).
- `SessionConsole.cs` — `WriteSerializedSession(json)` displays a serialized session, `WriteTurn(question, response, storedMessages)` one turn of a conversation (scenario 2) and `WriteTokenUsage(response)` the token usage of a run. `Program.cs` imports them with `using static`, so you can call them directly.
- `Program.cs` — the constants `AgentName`, `AgentInstructions`, `FirstQuestion`, `IntermediateQuestion` (scenario 2) and `FollowUpQuestion` are shared by the three scenarios, and displayed at the start.

### Step 4: Complete the Program.cs

Open `Start/Program.cs` and complete the TODOs:

---

#### Setup: Configuration and Client Initialization

Same as Lab 01.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 1** | Create the `OpenAIClientOptions` for the Azure OpenAI v1 endpoint | • `new OpenAIClientOptions { Endpoint = ... }` <br> • `AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint)` (from `CommonUtilities`) returns the `.../openai/v1/` URI |
| **TODO 2** | Create the `OpenAIClient` | • API key: `new OpenAIClient(new ApiKeyCredential(settings.APIKey), clientOptions)` <br> • Entra ID: `new OpenAIClient(new BearerTokenPolicy(new DefaultAzureCredential(), "https://ai.azure.com/.default"), clientOptions)` <br> • Use a conditional on `string.IsNullOrWhiteSpace(settings.APIKey)` to support both |
| **TODO 3** | Get a `ChatClient` for the deployment | • `client.GetChatClient(settings.ChatDeploymentName)` <br> • With Chat Completions, the service keeps no history: the agent sends it again at each run, from the session |

---

#### Scenario 1: Session with the default in-memory chat history

An agent created without a `ChatHistoryProvider` uses an `InMemoryChatHistoryProvider`: the messages are kept **inside the session** (in its `StateBag`). Serializing the session therefore serializes the whole conversation.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 4** | Create the agent | • `AIAgent agent = chatClient.AsAIAgent(instructions: AgentInstructions, name: AgentName);` |
| **TODO 5** | Create a session and ask the first question in it | • `AgentSession session = await agent.CreateSessionAsync();` <br> • `ColoredConsole.WritePrimaryLogLine($"User: {FirstQuestion}");` <br> • `AgentResponse firstResponse = await agent.RunAsync(FirstQuestion, session).WithSpinner("Running agent");` <br> • `ColoredConsole.WriteSecondaryLogLine($"Agent: {firstResponse.Text}");` |
| **TODO 6** | Display the chat history kept in the session | • `List<AIExtensions.ChatMessage> messages = agent.GetService<InMemoryChatHistoryProvider>()!.GetMessages(session);` <br> • `ColoredConsole.WriteInfoLine($"Messages in the session: {messages.Count} ({string.Join(", ", messages.Select(message => message.Role))})");` <br> • Expected: `2 (user, assistant)` |
| **TODO 7** | Serialize the session | • `JsonElement serializedSession = await agent.SerializeSessionAsync(session);` <br> • `WriteSerializedSession(serializedSession);` <br> • The JSON contains the two messages, under `stateBag` → `InMemoryChatHistoryProvider` |
| **TODO 8** | Save the session as text, then restore it | • `string savedSession = JsonSerializer.Serialize(serializedSession);` (this is what you would write to a file or a database) <br> • `ColoredConsole.WriteDividerLine();` then `ColoredConsole.WriteInfoLine("Restoring the session from the saved JSON...");` <br> • `AgentSession resumedSession = await agent.DeserializeSessionAsync(JsonSerializer.Deserialize<JsonElement>(savedSession));` |
| **TODO 9** | Ask the follow-up question in the restored session | • `ColoredConsole.WritePrimaryLogLine($"User: {FollowUpQuestion}");` <br> • `AgentResponse followUpResponse = await agent.RunAsync(FollowUpQuestion, resumedSession).WithSpinner("Running agent");` <br> • `ColoredConsole.WriteSecondaryLogLine($"Agent: {followUpResponse.Text}");` <br> • `WriteTokenUsage(followUpResponse);` <br> • The agent answers with the population of **Paris** and the name **Ada**: both come from the history |

---

#### Scenario 2: Bound the chat history with a chat reducer

Without a limit, every run sends the **whole** history back to the model: the cost of a conversation grows with each turn, until it no longer fits in the context window. A **chat reducer** keeps the history bounded. `MessageCountingChatReducer(2)` keeps the last 2 messages (the official sample [`Agent_Step13_ChatReduction`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step13_ChatReduction) does the same). The conversation has three turns: the last question needs "that city" (the previous turn, kept) and "my name" (the first turn, dropped).

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 10** | Create the agent with a bounded history | • `AIAgent agent = chatClient.AsAIAgent(new ChatClientAgentOptions { Name = AgentName, ChatOptions = new() { Instructions = AgentInstructions }, ChatHistoryProvider = new InMemoryChatHistoryProvider(new InMemoryChatHistoryProviderOptions { ChatReducer = new AIExtensions.MessageCountingChatReducer(2) }) });` <br> • With `ChatClientAgentOptions`, the instructions go to `ChatOptions.Instructions` <br> • The chat reducers of `Microsoft.Extensions.AI` are still **experimental** (`MEAI001`): keep the `#pragma` around the code |
| **TODO 11** | Get the provider and create a session | • `InMemoryChatHistoryProvider chatHistory = agent.GetService<InMemoryChatHistoryProvider>()!;` <br> • `AgentSession session = await agent.CreateSessionAsync();` |
| **TODO 12** | First turn | • `AgentResponse response = await agent.RunAsync(FirstQuestion, session).WithSpinner("Running agent");` <br> • `WriteTurn(FirstQuestion, response, chatHistory.GetMessages(session).Count);` |
| **TODO 13** | Second turn | • Same with `IntermediateQuestion` (`response = ...`): the session now holds 4 messages |
| **TODO 14** | Third turn | • Same with `FollowUpQuestion`: before the run, the reducer keeps the last 2 messages of the history. The agent knows "that city" (Rome) but **not** the name given in the first turn |
| **TODO 15** | Display token usage | • `WriteTokenUsage(response);` the input tokens stay flat from the second turn on |

---

#### Scenario 3: Custom ChatHistoryProvider - chat history in MongoDB

Same pattern with `MongoChatHistoryProvider`. This time, you simulate a **restart of the application**: the follow-up question is asked to a **new** agent, with a new provider and a new MongoDB connection. The only things kept are the saved JSON and the documents in MongoDB.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 16** | Connect to MongoDB | • `IMongoDatabase database = await ConfigurationHelper.ConnectToMongoDbAsync();` |
| **TODO 17** | Write a function that creates the agent on a database | • `Func<IMongoDatabase, AIAgent> createAgent = mongoDatabase => chatClient.AsAIAgent(new ChatClientAgentOptions { Name = AgentName, ChatOptions = new() { Instructions = AgentInstructions }, ChatHistoryProvider = new MongoChatHistoryProvider(mongoDatabase) });` |
| **TODO 18** | Create the agent and a session, and ask the first question | • `AIAgent agent = createAgent(database);` <br> • Then the same code as TODO 5 |
| **TODO 19** | Serialize the session and save it as text | • `JsonElement serializedSession = await agent.SerializeSessionAsync(session);` <br> • `WriteSerializedSession(serializedSession);` <br> • `string savedSession = JsonSerializer.Serialize(serializedSession);` |
| **TODO 20** | Simulate a restart and restore the session with a new agent | • `ColoredConsole.WriteDividerLine();` then `ColoredConsole.WriteInfoLine("Simulating a restart: new agent, new MongoDB connection, session restored from the saved JSON...");` <br> • `AIAgent restartedAgent = createAgent(await ConfigurationHelper.ConnectToMongoDbAsync());` <br> • `AgentSession resumedSession = await restartedAgent.DeserializeSessionAsync(JsonSerializer.Deserialize<JsonElement>(savedSession));` |
| **TODO 21** | Display the key of the restored session | • `MongoChatHistoryProvider chatHistoryProvider = restartedAgent.GetService<MongoChatHistoryProvider>()!;` <br> • `ColoredConsole.WriteInfoLine($"Restored session - chat history stored in MongoDB under the key: {chatHistoryProvider.GetSessionDbKey(resumedSession)}");` <br> • Same key as in the serialized session: it is the `SessionId` of the documents |
| **TODO 22** | Ask the follow-up question to the new agent | • Same code as TODO 9, with `restartedAgent` and `resumedSession` |

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

To run only some scenarios, edit `scenariosToRun` at the top of `Program.cs` (for example `[1, 2]` without MongoDB).

## How sessions and chat history work

```
                 RunAsync(message, session)
                            │
   ┌────────────────────────▼─────────────────────────┐
   │ ChatHistoryProvider.ProvideChatHistoryAsync      │  loads the history of the session
   │   (key read from session.StateBag)               │
   └────────────────────────┬─────────────────────────┘
                            │ history + new message
                            ▼
                     Chat Completions  ──►  response
                            │
   ┌────────────────────────▼─────────────────────────┐
   │ ChatHistoryProvider.StoreChatHistoryAsync        │  stores the new request + response messages
   └──────────────────────────────────────────────────┘

   SerializeSessionAsync(session)  → JSON of session.StateBag   → save it (file, database...)
   DeserializeSessionAsync(json)   → AgentSession               → RunAsync(..., resumedSession)
```

| | Scenario 1 — `InMemoryChatHistoryProvider` | Scenario 2 — with a chat reducer | Scenario 3 — MongoDB |
|---|---|---|---|
| Where are the messages? | In the session (`StateBag`) | In the session, the last ones only | In the `chat_history` collection |
| What does the serialized session contain? | The whole conversation | The last messages | The key (`sessionDbKey`) |
| Survives the application? | Yes, if you save the JSON | Yes, if you save the JSON | Yes: JSON + MongoDB |
| Cost of a run | Grows with the conversation | Bounded | Bounded (last 10 messages) |

The token usage shows the cost of a conversation: the input tokens of a run include the history sent back to the model. Scenario 2 bounds it with a reducer; the MongoDB provider of scenario 3 keeps every message but only sends the **10 most recent ones**. Both choices trade memory for cost: what is not sent is forgotten (Lab 12 shows how to keep **facts** about the user beyond the conversation).

### When the service keeps the conversation (Responses API)

With Chat Completions (this lab), the history is kept by Agent Framework: in the session, or in your own store. With the **Responses API** (Lab 01, scenario 6), the service can keep the conversation itself: the session then holds a conversation id, not the messages (`ChatClientAgentSession.ConversationId`), and each run only sends the new message ([Storage](https://learn.microsoft.com/agent-framework/concepts/agents/conversations/storage?pivots=programming-language-csharp)). Consequences:

- no chat history provider and no reducer: the service manages the size of the conversation; Agent Framework compaction has no effect on it;
- the conversation lives in the service, tied to its id (and to your Azure resource): you cannot move it to another model provider;
- saving and restoring a session still works the same way (`SerializeSessionAsync` / `DeserializeSessionAsync`), it just saves the id.

To keep the Responses API but manage the history yourself, ask the service not to store it: `GetResponsesClient().AsIChatClientWithStoredOutputDisabled(model: ...)` (official sample `Agent_With_AzureOpenAIResponses`).

### Security of stored sessions

A saved session is a conversation: treat it as personal data, and as **untrusted input** when you restore it ([Agent Safety](https://learn.microsoft.com/agent-framework/concepts/agents/safety?pivots=programming-language-csharp)).

- **Bind each session to its user**: *"Treat AgentSession as an opaque state object and restore it with the same agent/provider configuration … bind each stored session to the authenticated user or tenant"* ([Storage](https://learn.microsoft.com/agent-framework/concepts/agents/conversations/storage?pivots=programming-language-csharp)). A session id or a conversation id is **not** a proof of ownership: check the user before restoring.
- **Never restore a session sent by a client as is**: a modified session can inject messages (and roles) into the history.
- **Protect the store**: authentication and encryption at rest for MongoDB, a retention policy, and a way for users to delete their conversations.

## Key Concepts

| Concept | Description |
|---------|-------------|
| `AgentSession` | The state of one conversation, passed to every `RunAsync` of the conversation. Treat it as an opaque object: save it whole, restore it with the same agent configuration |
| `CreateSessionAsync()` | Creates a new, empty session for an agent |
| `SerializeSessionAsync(session)` / `DeserializeSessionAsync(json)` | Methods of the **agent**: turn a session into a `JsonElement` and back. The agent knows which providers put what in the session |
| `AgentSession.StateBag` | The key/value state of the session, serialized with it. Each provider has its own entry (its state key) |
| `ChatHistoryProvider` | Loads the chat history before a run (`ProvideChatHistoryAsync`) and stores the new messages after it (`StoreChatHistoryAsync`). One instance per agent, shared by all its sessions |
| `InMemoryChatHistoryProvider` | The default provider of an agent created with `AsAIAgent()`: the messages are kept in the session. `GetMessages(session)` returns them |
| `ProviderSessionState<T>` | Helper that stores the typed state of a provider in `StateBag` (`GetOrInitializeState`, `SaveState`) |
| `ChatClientAgentOptions` | Advanced agent options: `Name`, `ChatOptions` (`Instructions`, tools...), `ChatHistoryProvider` |
| `agent.GetService<T>()` | Finds a service of the agent, such as its chat history provider |
| Chat reducer (`IChatReducer`) | Reduces a chat history; set it with `InMemoryChatHistoryProviderOptions.ChatReducer`. `MessageCountingChatReducer(n)` keeps the last `n` messages (and the first system message); experimental (`MEAI001`) |
| `ChatReducerTriggerEvent` | When the reducer runs: before the history is given to a run (default, `BeforeMessagesRetrieval`) or after messages are added (`AfterMessageAdded`) |

## Namespaces Reference

| Namespace | Purpose |
|-----------|---------|
| `OpenAI` | `OpenAIClient`, `OpenAIClientOptions` |
| `OpenAI.Chat` | `ChatClient` and the `AsAIAgent()` extension methods |
| `System.ClientModel` | `ApiKeyCredential` |
| `System.ClientModel.Primitives` | `BearerTokenPolicy` |
| `Azure.Identity` | `DefaultAzureCredential` |
| `Microsoft.Agents.AI` | `AIAgent`, `AgentSession`, `AgentResponse`, `ChatClientAgentOptions`, `ChatHistoryProvider`, `InMemoryChatHistoryProvider`, `ProviderSessionState<T>` |
| `Microsoft.Extensions.AI` (aliased as `AIExtensions`, because `OpenAI.Chat` also has a `ChatMessage`) | `ChatMessage`, `MessageCountingChatReducer` (scenario 2) |
| `MongoDB.Driver` | `IMongoDatabase` |
| `System.Text.Json` | `JsonElement`, `JsonSerializer` |
| `CommonUtilities` | `ColoredConsole`, `WithSpinner()`, `AzureOpenAIEndpoint` |

## Expected Output

```
Endpoint: https://your-resource.openai.azure.com/
Deployment: your-deployment-name
GlobalAgent instructions: You are a global agent that can answer questions about any topic. The response should be very short and concise.
Questions: "Hello, my name is Ada. What is the capital of France?", then "What is the population of that city? And what is my name?" (scenario 2 asks "And what is the capital of Italy?" in between)
----------------------------------------
=== Scenario 1: Session with the default in-memory chat history ===
User: Hello, my name is Ada. What is the capital of France?
Agent: The capital of France is Paris.
Messages in the session: 2 (user, assistant)
Serialized session:
{
  "stateBag": {
    "InMemoryChatHistoryProvider": {
      "messages": [
        { "role": "user", "contents": [ { "$type": "text", "text": "Hello, my name is Ada. What is the capital of France?" } ] },
        { "authorName": "GlobalAgent", "role": "assistant", "contents": [ { "$type": "text", "text": "The capital of France is Paris." } ], ... }
      ]
    }
  }
}
----------------------------------------
Restoring the session from the saved JSON...
User: What is the population of that city? And what is my name?
Agent: The population of Paris is approximately 2.1 million. Your name is Ada.
----------------------------------------
Token Usage (follow-up):
  Input tokens: 79
  Output tokens: 17
  Total tokens: 96
----------------------------------------
=== Scenario 2: Bound the chat history with a chat reducer ===
User: Hello, my name is Ada. What is the capital of France?
Agent: The capital of France is Paris.
Messages kept in the session: 2 (input tokens of this run: 47)
User: And what is the capital of Italy?
Agent: The capital of Italy is Rome.
Messages kept in the session: 4 (input tokens of this run: 73)
User: What is the population of that city? And what is my name?
Agent: As of 2023, Rome's population is approximately 2.8 million. I don't know your name.
Messages kept in the session: 4 (input tokens of this run: 73)
----------------------------------------
Token Usage (follow-up):
  Input tokens: 73
  Output tokens: 23
  Total tokens: 96
----------------------------------------
=== Scenario 3: Custom ChatHistoryProvider - chat history in MongoDB ===
...
Simulating a restart: new agent, new MongoDB connection, session restored from the saved JSON...
Restored session - chat history stored in MongoDB under the key: f3b863cd58334e6aa5edfcc72d376ff9
User: What is the population of that city? And what is my name?
Agent: As of 2023, the population of Paris is approximately 2.1 million. Your name is Ada.
...
```

Answers vary from one run to another; the keys are new at each run.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `'AzureOpenAI:Endpoint' is not configured` | Replace the `YOUR-...` placeholders in `appsettings.json` (or use user secrets / environment variables) |
| `401 Unauthorized` with an API key | Check the key (user secret `AzureOpenAI:APIKey`) and that it belongs to the resource of the endpoint |
| `401` / `403` with Entra ID | Run `az login` and make sure your identity has the **Cognitive Services OpenAI User** role on the resource |
| `404 DeploymentNotFound` | `ChatDeploymentName` must be the **deployment** name, not the model name |
| `MongoDB is not reachable ...` | Start MongoDB (`docker compose up -d` in `MongoDB/`), check `docker ps`, or skip scenario 3 with `scenariosToRun = [1, 2]` |
| `docker compose up` fails with *port is already allocated* | Another MongoDB already uses port 27017: stop it, or point `MongoDb:ConnectionString` to it |
| The follow-up answer does not know the city or the name | Pass the **restored** session to `RunAsync` (TODO 9, 22), and restore it with the agent that has the same provider |
| `NullReferenceException` on `GetService<...>()!` | The agent has no provider of this type: check `ChatHistoryProvider = ...` in `ChatClientAgentOptions` (TODO 10, 17) |
| `Only ConversationId or ChatHistoryProvider may be used, but not both` | The service stores the history itself (Responses API with stored responses): use the Chat Completions `ChatClient` of TODO 3 |

## Provided Files

The following files are provided and should not be modified:

- `AIAgentWithThreads.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs` - Loads and validates the configuration, connects to MongoDB
- `AzureOpenAISettings.cs` - Settings class for Azure OpenAI
- `MongoDbSettings.cs` - Settings class for MongoDB
- `Stores/MongoChatHistoryProvider.cs` - `ChatHistoryProvider` on MongoDB
- `SessionConsole.cs` - Display helpers
- `MongoDB/docker-compose.yml` - Local MongoDB

The following files should be modified:

- `appsettings.json` - Update with your Azure OpenAI endpoint and deployment
- `Program.cs` - Complete the TODOs

### NuGet packages

| Package | Why |
|---------|-----|
| `Microsoft.Agents.AI.OpenAI` | Agent Framework + `AsAIAgent()` for OpenAI clients (brings `OpenAI` and `Microsoft.Extensions.AI`) |
| `Azure.Identity` | `DefaultAzureCredential` for Entra ID authentication |
| `Microsoft.Extensions.Configuration.*` | `appsettings.json`, user secrets and environment variables, bound to the settings classes |
| `MongoDB.Driver` | Official MongoDB driver (scenario 3) |

## Going Further

- **Summarize instead of dropping**: the compaction framework of Agent Framework can summarize the old messages with a smaller model, or collapse old tool results, instead of dropping them (`CompactionProvider`, `SummarizationCompactionStrategy`...). It is still experimental (`MAAI001`), see [Compaction](https://learn.microsoft.com/agent-framework/concepts/agents/conversations/compaction?pivots=programming-language-csharp) and the official sample `Agent_Step18_CompactionPipeline`.
- **Other stores**: the official samples also store the chat history in a vector store (`Agent_Step04_3rdPartyChatHistoryStorage`) or in Azure Cosmos DB (`CosmosChatHistoryProvider`, package `Microsoft.Agents.AI.CosmosNoSql`, preview).
- **Production**: index the `SessionId` field of the MongoDB collection, enable authentication on MongoDB, and bind each stored session to its user (see [Security of stored sessions](#security-of-stored-sessions)).
- **Memory beyond the conversation**: context providers can add long-term memories or documents to each run — this is the topic of Lab 12.

## Coming from an older version of the lab?

| Before (preview) | Now (1.26.0) |
|------------------|--------------|
| `Azure.AI.OpenAI` / `AzureOpenAIClient` | `OpenAI` / `OpenAIClient` + Azure OpenAI v1 endpoint |
| `chatClient.CreateAIAgent(agentOptions)` | `chatClient.AsAIAgent(agentOptions)`, variables typed `AIAgent` |
| `AgentRunResponse` | `AgentResponse` |
| `AgentThread`, `agent.GetNewThread()` | `AgentSession`, `await agent.CreateSessionAsync()` |
| `thread.Serialize()` / `agent.DeserializeThread(json)` | `await agent.SerializeSessionAsync(session)` / `await agent.DeserializeSessionAsync(json)` |
| `ChatMessageStore` + `ChatMessageStoreFactory = ctx => new ...Store(..., ctx.SerializedState, ...)` (one store per thread, key in a field) | `ChatHistoryProvider` + `ChatClientAgentOptions.ChatHistoryProvider = new ...Provider(...)` (one provider per agent, key in `AgentSession.StateBag`) |
| `AddMessagesAsync` / `GetMessagesAsync` / `Serialize` of the store | `StoreChatHistoryAsync` / `ProvideChatHistoryAsync`; the state is serialized with the session |
| `ChatClientAgentOptions.Instructions` | `ChatClientAgentOptions.ChatOptions.Instructions` |
| Extract the thread id from the serialized state, rebuild an `AgentThreadState` JSON (`models/AgentThreadState.cs`, `ExtractThreadIdFromState`) | Save the whole serialized session and restore it as is; read the key with the provider (`GetSessionDbKey(session)`) |
| `Microsoft.SemanticKernel.Connectors.InMemory` / `.MongoDB` (preview) | Official `MongoDB.Driver` 3.x (the vector store scenario became the chat reducer scenario) |
| Agent name and instructions in `appsettings.json` (`AzureOpenAI:Agents:GlobalAgent`) | Constants at the top of `Program.cs` |
| MongoDB with a password in `appsettings.json` + Mongo Express | MongoDB without authentication bound to `127.0.0.1`, no secret in `appsettings.json`; `mongosh` to look at the data |
| API key in `appsettings.json` | API key in user secrets or environment variables |
| — | Scenario 1: the default in-memory chat history, inside the session |

## Useful Links

- [Session](https://learn.microsoft.com/agent-framework/concepts/agents/conversations/session?pivots=programming-language-csharp)
- [Storage (chat history providers)](https://learn.microsoft.com/agent-framework/concepts/agents/conversations/storage?pivots=programming-language-csharp)
- [Microsoft Agent Framework documentation](https://learn.microsoft.com/agent-framework/overview/?pivots=programming-language-csharp)
- [Official sample `Agent_Step03_PersistedConversations`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step03_PersistedConversations)
- [Official sample `Agent_Step04_3rdPartyChatHistoryStorage`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step04_3rdPartyChatHistoryStorage)
- [Azure OpenAI v1 API](https://learn.microsoft.com/azure/foundry/openai/api-version-lifecycle)

## Solution

If you get stuck, check the complete solution in the `Solution/` folder.
