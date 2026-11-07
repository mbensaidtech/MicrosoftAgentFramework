# Lab 05 - AI Agent with Sessions (Conversation Persistence)

## Objective

In this lab, you will build multi-turn conversations with **Microsoft Agent Framework** and **Azure OpenAI**, save them, and resume them later with their full context.

A conversation lives in an **`AgentSession`**. The messages of the conversation (the *chat history*) are kept by a **`ChatHistoryProvider`** attached to the agent. You will run the same conversation three times, and change where the chat history is kept, and how much of it:

1. **In the session itself** — the default `InMemoryChatHistoryProvider`: the serialized session contains the whole conversation. You **serialize** the session, save it as text, **restore** it, and ask a follow-up question that only makes sense with the history ("that city", "my name").
2. **Bounded** — the same provider with a **chat reducer** that keeps only the last messages: the cost of each run stops growing, and the agent forgets the beginning of the conversation.
3. **Outside of the application** — a custom `ChatHistoryProvider` on a real storage (MongoDB, or a JSON file when MongoDB cannot run on your computer): the serialized session only contains a key, the conversation survives the application, a new agent resumes it.

Then, as a **bonus**, you run the same conversation on the **Responses API**, where the model provider *can* keep the conversation for you, and you decide whether it should:

4. **In the service** — the Responses API stores each response (`store = true`, the default): the session only holds the id of the last response, and each turn sends only the new message (`previous_response_id`). You check that the service keeps the conversation, then delete it.
5. **In your application, on the Responses API** — the same `PersistentChatHistoryProvider` as scenario 3, on a Responses client that asks the service to store nothing (`store = false`): the conversation stays in **your** storage, under **your** business id, and the service keeps nothing.

> This lab was called *Lab05-AIAgentWithThreads*: Agent Framework replaced `AgentThread` with **`AgentSession`**, and the lab follows its terminology.

> **Bonus — the Responses API scenarios (4 and 5) are optional.** The lab is **completed** once scenarios 1 to 3 pass. Pass the bonus scenarios too and it is **completed with its bonus** (*Completed+* in the Lab Bench dashboard and for the trainer, +50 XP). A failed bonus scenario never undoes a completed lab, and `dotnet run` leaves a bonus scenario out until you write it. `dotnet run -- --list-scenarios` marks them `[bonus]`.

## What You Will Learn

- How to create a session with `CreateSessionAsync()` and run an agent in it
- How to save a session with `SerializeSessionAsync()` and resume it with `DeserializeSessionAsync()`
- Where the chat history lives with the default `InMemoryChatHistoryProvider`, and how to read it (`GetService<InMemoryChatHistoryProvider>()`, `GetMessages(session)`)
- How to plug a custom `ChatHistoryProvider` with `ChatClientAgentOptions.ChatHistoryProvider`
- How a provider keeps its per-session state (a storage key) in the session: `AgentSession.StateBag` and `ProviderSessionState<T>`
- How to store the chat history outside of the application with your own `ChatHistoryProvider` (MongoDB or a JSON file)
- How to **bound** the chat history with a chat reducer (`MessageCountingChatReducer`), and what it costs: the agent forgets
- What changes when the **service** keeps the conversation (Responses API): `ChatClientAgentSession.ConversationId`, `previous_response_id`, the 30-day storage
- How to keep the Responses API **and** your own persistence: `AsIChatClientWithStoredOutputDisabled` + your `ChatHistoryProvider`
- How to tell apart your business conversation id, the Agent Framework session, and the response / conversation ids of OpenAI
- How to check where a conversation lives (`GET /responses/{id}`), and how to delete what the service keeps
- How to protect stored sessions

## Prerequisites

- .NET 10 SDK or later
- An Azure OpenAI (or Microsoft Foundry) resource with a chat model deployment (for example `gpt-4o-mini` or `gpt-5.4-mini`). Scenarios 4 and 5 also need the **Responses API** for this deployment (see Lab 01 scenario 6)
- One of the following for authentication:
  - the resource **API key**, or
  - Azure CLI logged in (`az login`) with the **Cognitive Services OpenAI User** role on the resource
- For scenarios 3 and 5, a storage for the chat history: **Docker** (the MongoDB container of the lab), **or nothing at all** — MongoDB started by the lab itself, or a JSON file (see [Step 2](#step-2-choose-the-storage-of-scenarios-3-and-5)). None of them needs administrator rights
- Lab 01 completed (client creation, `AsAIAgent()`, `AgentResponse`)

## Project Structure

```
Lab05-AIAgentWithSessions/
├── README.md
├── Start/                                  <-- Your working folder
│   ├── Program.cs                          <-- Complete the TODOs here
│   ├── LabPrompts.cs                       <-- Provided: name, instructions and questions of the agent
│   ├── Stores/
│   │   ├── PersistentChatHistoryProvider.cs <-- ChatHistoryProvider on a store (scenarios 3 and 5)
│   │   ├── IChatHistoryStore.cs            <-- Where the messages are kept
│   │   ├── MongoChatHistoryStore.cs        <-- ... in MongoDB
│   │   └── JsonFileChatHistoryStore.cs     <-- ... in a JSON file
│   ├── MongoDB/
│   │   └── docker-compose.yml              <-- Local MongoDB in Docker for scenarios 3 and 5
│   ├── SessionConsole.cs                   <-- Display helpers (provided)
│   ├── ConfigurationHelper.cs              <-- Configuration, opens the store chosen by Storage:Mode
│   ├── MongoDbConnection.cs                <-- Connects to MongoDB, starts it without Docker
│   ├── AzureOpenAISettings.cs
│   ├── StorageSettings.cs
│   ├── MongoDbSettings.cs
│   ├── appsettings.json
│   └── AIAgentWithSessions.csproj
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
     "Storage": {
       "Mode": "MongoDb"
     },
     "MongoDb": {
       "ConnectionString": "mongodb://localhost:27017",
       "DatabaseName": "AIAgentSessions"
     }
   }
   ```

   Use the resource endpoint shown in the Azure portal. The `/openai/v1/` suffix required by the v1 API is added for you.
   `*.openai.azure.com`, `*.cognitiveservices.azure.com` and `*.services.ai.azure.com` endpoints all work.
   The `Storage` and `MongoDb` values match the provided `docker-compose.yml`: keep them unless you use another storage (Step 2).

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

### Step 2: Choose the storage of scenarios 3 and 5

Scenarios 3 and 5 keep the chat history outside of the application. Choose where with the `Storage:Mode` setting. None of the three modes needs administrator rights, and the code of the scenario is the same with all of them:

| `Storage:Mode` | What you need | Where the data are kept |
|---|---|---|
| `MongoDb` (default) | **Docker**, to start the MongoDB container of the lab (or any MongoDB server, set in `MongoDb:ConnectionString`) | The Docker volume of the container |
| `EmbeddedMongoDb` | **Nothing**: the lab downloads MongoDB at its first run (about 1 minute, then a few seconds), and starts it itself | `<local app data>/MicrosoftAgentFrameworkLabs/AIAgentSessions/mongodb` |
| `JsonFile` | **Nothing**, not even MongoDB: the fallback when the two others are not possible | `<local app data>/MicrosoftAgentFrameworkLabs/AIAgentSessions/chat_history.json` |

`<local app data>` is `%LOCALAPPDATA%` on Windows, `~/Library/Application Support` on macOS and `~/.local/share` on Linux (`Storage:DataDirectory` changes it). At the start of scenarios 3 and 5, the lab displays the storage it uses.

To change the mode, set it once in the user secrets: they are shared by all the labs, so Lab 12 uses the same mode.

```bash
cd Start
dotnet user-secrets set "Storage:Mode" "EmbeddedMongoDb"    # or "JsonFile"
dotnet user-secrets remove "Storage:Mode"                    # back to Docker (MongoDb)
```

You can also use the environment variable `Storage__Mode`, or change `Storage:Mode` in `appsettings.json`.

#### Option A — MongoDB in Docker (`MongoDb`, default)

```bash
cd Start/MongoDB
docker compose up -d
```

This starts a MongoDB 8.0 container named `lab05-mongodb`, reachable from your computer only (`127.0.0.1:27017`), **without authentication**: it is a local development database. Its data is kept in a Docker volume, so the conversations survive a restart of the container.

- See what the agent stored: `docker exec lab05-mongodb mongosh AIAgentSessions --quiet --eval "db.chat_history.find({}, {_id: 0, SessionId: 1, Role: 1, MessageText: 1})"`
- Stop MongoDB: `docker compose down` (add `-v` to also delete the stored conversations)

#### Option B — MongoDB without Docker (`EmbeddedMongoDb`)

The lab starts MongoDB itself, with [EphemeralMongo](https://github.com/asimmon/ephemeral-mongo): no Docker, no installation. At the first run, it downloads MongoDB Community from **mongodb.org** into your local app data folder (`ephemeral-mongo`), then runs `mongod` as a child process of the lab, on `127.0.0.1` only, without authentication. The server stops with the lab; its data folder is kept, so the conversations survive the lab, like with the container.

- Behind a corporate proxy, the download goes through the proxy of the system, with your Windows credentials. If mongodb.org is blocked, set `MongoDb:BinaryDirectory` to a folder that contains the MongoDB binaries (`mongod`), for example copied from a colleague, or use `JsonFile`.
- If Windows shows a firewall prompt for `mongod`, you can cancel it: the server only listens on `127.0.0.1`.
- If the lab is killed, its `mongod` keeps running on macOS and Linux: the next run of the lab stops it before starting a new one.

#### Option C — a JSON file (`JsonFile`)

No MongoDB at all: `JsonFileChatHistoryStore` keeps the messages in `chat_history.json` (its path is displayed at the start of scenarios 3 and 5). Open the file to see what the agent stored, and delete it to start again from an empty history.

Scenarios 1, 2 and 4 need no storage. To skip scenarios 3 and 5: `dotnet run -- --scenarios 1,2,4`.

### Step 3: Look at the provided files

- `Stores/PersistentChatHistoryProvider.cs` (scenarios 3 and 5) — a `ChatHistoryProvider` that keeps the chat history in a store (`IChatHistoryStore`), adapted from the official sample `Agent_Step04_3rdPartyChatHistoryStorage`. It overrides two methods:
  - `ProvideChatHistoryAsync` — called **before** each run: loads the last 10 messages of the session from the store. The agent sends them to the model before the new message.
  - `StoreChatHistoryAsync` — called **after** each successful run: stores the new messages (`RequestMessages` + `ResponseMessages`).

  The provider instance is shared by all the sessions of the agent, so it keeps **no session data in its fields**. The key of the conversation (`SessionDbKey`) is stored **in the session**, through `ProviderSessionState<State>`: `GetOrInitializeState(session)` reads it from `AgentSession.StateBag`, or creates a new key the first time. That is why the key is saved and restored with the session.
- `Stores/IChatHistoryStore.cs` — where the provider keeps the messages, under the key of each session. Two implementations, chosen by `Storage:Mode`; the provider does not know which one it uses:
  - `Stores/MongoChatHistoryStore.cs` — the `chat_history` collection of MongoDB, with the official MongoDB driver. Each message is a document: `SessionId`, `Timestamp`, `Order`, `Role`, `MessageText` and the serialized `ChatMessage`.
  - `Stores/JsonFileChatHistoryStore.cs` — the `chat_history.json` file: the messages of all the sessions, with their `SessionId`.
- `ConfigurationHelper.cs` — `CreateChatHistoryStoreAsync()` opens the store chosen by `Storage:Mode`, and checks that MongoDB answers (clear error message when it is not started). `MongoDbConnection.cs` connects to MongoDB, and starts it in the `EmbeddedMongoDb` mode.
- `SessionConsole.cs` — `WriteSerializedSession(json)` displays a serialized session, `WriteTurn(question, response, storedMessages)` one turn of a conversation (scenario 2) and `WriteTokenUsage(response)` the token usage of a run. For the Responses API: `WriteServiceStorageAsync(responsesClient, responseId)` asks the service whether it kept a response (`GET /responses/{id}`: found, or 404), and `WriteIdentifiers(...)` displays the identifiers of a conversation (scenario 5). `Program.cs` imports them with `using static`, so you can call them directly.
- `LabPrompts.cs` — the name and the instructions of the agent (`Agents.Global`) and the questions of the conversation (`Questions.First`, `Questions.Intermediate` for scenario 2, and `Questions.FollowUp`), shared by every scenario and displayed at the start of `Program.cs`. `BusinessIds.SupportCase` is the id of the conversation **in your application** (scenario 5).

### Step 4: Complete the Program.cs

Open `Start/Program.cs` and complete the TODOs.

The name, the instructions and the questions of the agent are **provided** in `Start/LabPrompts.cs`: use its constants (`Agents.Global.Name`, `Agents.Global.Instructions`, `Questions.First`, `Questions.FollowUp`...) instead of writing your own texts. Everyone works with the same agent and gets comparable answers, and you can focus on the Agent Framework code.

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
| **TODO 4** | Create the agent | • `AIAgent agent = chatClient.AsAIAgent(instructions: Agents.Global.Instructions, name: Agents.Global.Name);` |
| **TODO 5** | Create a session and ask the first question in it | • `AgentSession session = await agent.CreateSessionAsync();` <br> • `ColoredConsole.WritePrimaryLogLine($"User: {Questions.First}");` <br> • `AgentResponse firstResponse = await agent.RunAsync(Questions.First, session).WithSpinner("Running agent");` <br> • `ColoredConsole.WriteSecondaryLogLine($"Agent: {firstResponse.Text}");` |
| **TODO 6** | Display the chat history kept in the session | • `List<AIExtensions.ChatMessage> messages = agent.GetService<InMemoryChatHistoryProvider>()!.GetMessages(session);` <br> • `ColoredConsole.WriteInfoLine($"Messages in the session: {messages.Count} ({string.Join(", ", messages.Select(message => message.Role))})");` <br> • Expected: `2 (user, assistant)` |
| **TODO 7** | Serialize the session | • `JsonElement serializedSession = await agent.SerializeSessionAsync(session);` <br> • `WriteSerializedSession(serializedSession);` <br> • The JSON contains the two messages, under `stateBag` → `InMemoryChatHistoryProvider` |
| **TODO 8** | Save the session as text, then restore it | • `string savedSession = JsonSerializer.Serialize(serializedSession);` (this is what you would write to a file or a database) <br> • `ColoredConsole.WriteDividerLine();` then `ColoredConsole.WriteInfoLine("Restoring the session from the saved JSON...");` <br> • `AgentSession resumedSession = await agent.DeserializeSessionAsync(JsonSerializer.Deserialize<JsonElement>(savedSession));` |
| **TODO 9** | Ask the follow-up question in the restored session | • `ColoredConsole.WritePrimaryLogLine($"User: {Questions.FollowUp}");` <br> • `AgentResponse followUpResponse = await agent.RunAsync(Questions.FollowUp, resumedSession).WithSpinner("Running agent");` <br> • `ColoredConsole.WriteSecondaryLogLine($"Agent: {followUpResponse.Text}");` <br> • `WriteTokenUsage(followUpResponse);` <br> • The agent answers with the population of **Paris** and the name **Ada**: both come from the history |

---

#### Scenario 2: Bound the chat history with a chat reducer

Without a limit, every run sends the **whole** history back to the model: the cost of a conversation grows with each turn, until it no longer fits in the context window. A **chat reducer** keeps the history bounded. `MessageCountingChatReducer(2)` keeps the last 2 messages (the official sample [`Agent_Step13_ChatReduction`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step13_ChatReduction) does the same). The conversation has three turns: the last question needs "that city" (the previous turn, kept) and "my name" (the first turn, dropped).

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 10** | Create the agent with a bounded history | • `AIAgent agent = chatClient.AsAIAgent(new ChatClientAgentOptions { Name = Agents.Global.Name, ChatOptions = new() { Instructions = Agents.Global.Instructions }, ChatHistoryProvider = new InMemoryChatHistoryProvider(new InMemoryChatHistoryProviderOptions { ChatReducer = new AIExtensions.MessageCountingChatReducer(2) }) });` <br> • With `ChatClientAgentOptions`, the instructions go to `ChatOptions.Instructions` <br> • The chat reducers of `Microsoft.Extensions.AI` are still **experimental** (`MEAI001`): keep the `#pragma` around the code |
| **TODO 11** | Get the provider and create a session | • `InMemoryChatHistoryProvider chatHistory = agent.GetService<InMemoryChatHistoryProvider>()!;` <br> • `AgentSession session = await agent.CreateSessionAsync();` |
| **TODO 12** | First turn | • `AgentResponse response = await agent.RunAsync(Questions.First, session).WithSpinner("Running agent");` <br> • `WriteTurn(Questions.First, response, chatHistory.GetMessages(session).Count);` |
| **TODO 13** | Second turn | • Same with `Questions.Intermediate` (`response = ...`): the session now holds 4 messages |
| **TODO 14** | Third turn | • Same with `Questions.FollowUp`: before the run, the reducer keeps the last 2 messages of the history. The agent knows "that city" (Rome) but **not** the name given in the first turn |
| **TODO 15** | Display token usage | • `WriteTokenUsage(response);` the input tokens stay flat from the second turn on |

---

#### Scenario 3: Custom ChatHistoryProvider - chat history outside of the application

Same pattern with `PersistentChatHistoryProvider`, on the store chosen in [Step 2](#step-2-choose-the-storage-of-scenarios-3-and-5). This time, you simulate a **restart of the application**: the follow-up question is asked to a **new** agent, with a new provider and a new connection to the storage. The only things kept are the saved JSON and the stored messages.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 16** | Open the storage of the chat history | • `IChatHistoryStore store = await ConfigurationHelper.CreateChatHistoryStoreAsync();` <br> • MongoDB (with or without Docker) or a JSON file, according to `Storage:Mode` |
| **TODO 17** | Write a function that creates the agent on a store | • `Func<IChatHistoryStore, AIAgent> createAgent = chatHistoryStore => chatClient.AsAIAgent(new ChatClientAgentOptions { Name = Agents.Global.Name, ChatOptions = new() { Instructions = Agents.Global.Instructions }, ChatHistoryProvider = new PersistentChatHistoryProvider(chatHistoryStore) });` |
| **TODO 18** | Create the agent and a session, and ask the first question | • `AIAgent agent = createAgent(store);` <br> • Then the same code as TODO 5 |
| **TODO 19** | Serialize the session and save it as text | • `JsonElement serializedSession = await agent.SerializeSessionAsync(session);` <br> • `WriteSerializedSession(serializedSession);` <br> • `string savedSession = JsonSerializer.Serialize(serializedSession);` |
| **TODO 20** | Simulate a restart and restore the session with a new agent | • `ColoredConsole.WriteDividerLine();` then `ColoredConsole.WriteInfoLine("Simulating a restart: new agent, new connection to the storage, session restored from the saved JSON...");` <br> • `AIAgent restartedAgent = createAgent(await ConfigurationHelper.CreateChatHistoryStoreAsync());` <br> • `AgentSession resumedSession = await restartedAgent.DeserializeSessionAsync(JsonSerializer.Deserialize<JsonElement>(savedSession));` |
| **TODO 21** | Display the key of the restored session | • `PersistentChatHistoryProvider chatHistoryProvider = restartedAgent.GetService<PersistentChatHistoryProvider>()!;` <br> • `ColoredConsole.WriteInfoLine($"Restored session - chat history stored under the key: {chatHistoryProvider.GetSessionDbKey(resumedSession)}");` <br> • Same key as in the serialized session: it is the `SessionId` of the stored messages |
| **TODO 22** | Ask the follow-up question to the new agent | • Same code as TODO 9, with `restartedAgent` and `resumedSession` |

---

#### Scenario 4 (bonus): Responses API — the service keeps the conversation

The same agent and the same conversation, on the Responses API. By default the service **stores** each response (`store = true`) for 30 days: the session no longer holds the messages, only the id of the last response, in `ChatClientAgentSession.ConversationId`. At the next turn, the agent sends the new message with this id (`previous_response_id`), and the service adds the history.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 23** | Create the agent on the Responses API | • `ResponsesClient responsesClient = client.GetResponsesClient();` <br> • `AIAgent agent = responsesClient.AsAIAgent(model: settings.ChatDeploymentName, instructions: Agents.Global.Instructions, name: Agents.Global.Name);` <br> • Same `OpenAIClient`: only the API changes. `GetResponsesClient()` is flagged experimental (`OPENAI001`): keep the `#pragma` |
| **TODO 24** | Create a session and ask the first question | • Same code as TODO 5 |
| **TODO 25** | Display what the session keeps | • `ColoredConsole.WriteInfoLine($"Conversation id kept in the session: {((ChatClientAgentSession)session).ConversationId}");` <br> • `ChatClientAgentSession` is the session of a `ChatClientAgent`. The id is a **response id** (`resp_...`) |
| **TODO 26** | Serialize, save and restore the session | • Same code as TODO 7 and TODO 8 <br> • The JSON holds `"conversationId": "resp_..."` and an **empty** `InMemoryChatHistoryProvider`: the provider is skipped when the service keeps the conversation |
| **TODO 27** | Ask the follow-up question in the restored session | • Same code as TODO 9 (without the token usage), then `ColoredConsole.WriteInfoLine($"Conversation id kept in the session after the follow-up: {((ChatClientAgentSession)resumedSession).ConversationId}");` <br> • The agent knows Paris and Ada: the service added the first turn. The id **changed**: the session now points to the new response, which points to the previous one |
| **TODO 28** | Check where the conversation lives, display the usage | • `await WriteServiceStorageAsync(responsesClient, followUpResponse.ResponseId);` → `stored by the service: yes` <br> • `WriteTokenUsage(followUpResponse);` the input tokens are close to scenario 1: the history was not sent, but the model read it, and it is billed |
| **TODO 29** | Delete what the service keeps | • `await responsesClient.DeleteResponseAsync(firstResponse.ResponseId);` and the same with `followUpResponse.ResponseId` (inside the `OPENAI001` pragma) <br> • `ColoredConsole.WriteInfoLine("Deleted the 2 responses stored by the service for this conversation");` <br> • The conversation is data held by the provider: to erase it, you ask the provider. After that, the saved session can no longer be resumed |

---

#### Scenario 5 (bonus): Responses API without storage — the conversation stays in your application

The Responses API does **not** force you to store the conversation at the provider. `AsIChatClientWithStoredOutputDisabled(model)` returns a chat client that sends `store = false`: the service returns a response id but keeps nothing, and Agent Framework manages the history, as with Chat Completions. You can then plug the **same** `PersistentChatHistoryProvider` as scenario 3, and keep the conversation in your own MongoDB (or JSON file).

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 30** | Open the storage, get a Responses client that stores nothing | • `IChatHistoryStore store = await ConfigurationHelper.CreateChatHistoryStoreAsync();` <br> • `ResponsesClient responsesClient = client.GetResponsesClient();` <br> • `AIExtensions.IChatClient statelessResponsesClient = responsesClient.AsIChatClientWithStoredOutputDisabled(settings.ChatDeploymentName);` <br> • Flagged experimental by Agent Framework (`MAAI001`): keep the `#pragma` <br> • Without it, the run fails with *Only ConversationId or ChatHistoryProvider may be used, but not both*: the service returned a conversation id **and** you configured a provider |
| **TODO 31** | Write the function that creates the agent | • `Func<IChatHistoryStore, AIAgent> createAgent = chatHistoryStore => new ChatClientAgent(statelessResponsesClient, new ChatClientAgentOptions { ... });` with the options of TODO 17 <br> • `new ChatClientAgent(chatClient, options)` is what `AsAIAgent(options)` does for you on an OpenAI client; here the chat client is an `IChatClient` |
| **TODO 32** | Your business id, the first question, where the conversation lives | • `string businessConversationId = BusinessIds.SupportCase;` <br> • `Dictionary<string, string> savedSessions = [];` stands for **your** table of conversations (in a real application: a table of your database, bound to the authenticated user) <br> • Same code as TODO 18, then `await WriteServiceStorageAsync(responsesClient, firstResponse.ResponseId);` → `stored by the service: no (404)` |
| **TODO 33** | Save the session under the business id, restart, restore | • `WriteSerializedSession(serializedSession);` — the JSON holds the `sessionDbKey` of your storage, and **no** `conversationId` <br> • `savedSessions[businessConversationId] = JsonSerializer.Serialize(serializedSession);` <br> • Then the restart of TODO 20, restoring `savedSessions[businessConversationId]`, with `ColoredConsole.WriteInfoLine($"Simulating a restart: new agent, new connection to the storage, session of '{businessConversationId}' restored...");` |
| **TODO 34** | Follow-up question, identifiers, usage | • Same code as TODO 22 (without the token usage) <br> • `string chatHistoryKey = restartedAgent.GetService<PersistentChatHistoryProvider>()!.GetSessionDbKey(resumedSession);` <br> • `WriteIdentifiers(businessConversationId, chatHistoryKey, followUpResponse.ResponseId, ((ChatClientAgentSession)resumedSession).ConversationId);` <br> • `WriteTokenUsage(followUpResponse);` |

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

To run only some scenarios: `dotnet run -- --scenarios 1,2,4` without storage, `--scenarios 4,5` for the Responses API only ([Choose the scenarios to run](../../../README.md#3-choose-the-scenarios-to-run)).

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

| | Scenario 1 — `InMemoryChatHistoryProvider` | Scenario 2 — with a chat reducer | Scenario 3 — `PersistentChatHistoryProvider` |
|---|---|---|---|
| Where are the messages? | In the session (`StateBag`) | In the session, the last ones only | In the store: the `chat_history` collection (MongoDB) or `chat_history.json` |
| What does the serialized session contain? | The whole conversation | The last messages | The key (`sessionDbKey`) |
| Survives the application? | Yes, if you save the JSON | Yes, if you save the JSON | Yes: JSON + the store |
| Cost of a run | Grows with the conversation | Bounded | Bounded (last 10 messages) |

The token usage shows the cost of a conversation: the input tokens of a run include the history sent back to the model. Scenario 2 bounds it with a reducer; the provider of scenario 3 keeps every message but only sends the **10 most recent ones**. Both choices trade memory for cost: what is not sent is forgotten (Lab 12 shows how to keep **facts** about the user beyond the conversation).

## Where does the conversation live? Chat Completions and Responses

`AgentSession` is the same object in every scenario: your code creates it, passes it to `RunAsync`, serializes and restores it. What changes is **what it holds**, and therefore **who keeps the conversation**.

| | Chat Completions (scenarios 1 to 3) | Responses, stored (scenario 4) | Responses, not stored (scenario 5) |
|---|---|---|---|
| Chat client | `GetChatClient(deployment)` | `GetResponsesClient().AsAIAgent(model: ...)` | `GetResponsesClient().AsIChatClientWithStoredOutputDisabled(deployment)` |
| `store` sent to the service | (no such option: nothing is kept) | `true` (the default) | `false` |
| Who keeps the history | Agent Framework: the session or your `ChatHistoryProvider` | **The service**, 30 days (Azure OpenAI and OpenAI) | Agent Framework: your `ChatHistoryProvider` |
| What the serialized session holds | The messages, or the key of your store | `conversationId`: the id of the last response (`resp_...`) | The key of your store (`sessionDbKey`) |
| What each turn sends | The whole history (or what the provider / reducer keeps) | The new message + `previous_response_id` | The whole history loaded by your provider |
| Input tokens billed | The history sent | The history **read** by the model: not cheaper | The history sent |
| `ChatHistoryProvider`, chat reducer, compaction | Yes | **No** (an explicit provider throws *Only ConversationId or ChatHistoryProvider may be used*) | Yes |
| Change of model provider | Possible: the history is yours | Not possible: the history is in the service, tied to its ids and to your resource | Possible: the history is yours |
| Delete a conversation | In your store | `DeleteResponseAsync(id)` for each response of the chain | In your store |
| Reasoning of reasoning models across turns | — | Kept by the service | Sent back as encrypted content (`includeReasoningEncryptedContent`, the default of `AsIChatClientWithStoredOutputDisabled`) |

The Responses API therefore does **not** mean "the conversation is stored at OpenAI": that is its default, and you can turn it off. Choose **service-side state** (scenario 4) for less code and smaller requests, when a 30-day storage by the provider is acceptable. Choose **application-side state** (scenario 5, or Chat Completions) when the conversation is your data: you control the place, the retention, the encryption, the deletion and the access, you can bound or summarize it, and you can change of model provider without losing it.

### Four identifiers you must not confuse

| Identifier | Example | Owner | Lifetime | Use it for |
|---|---|---|---|---|
| **Business conversation id** | `support-case-1042` | Your application | As long as your application needs it | Find the conversation of a user, show it, bind it to the user. Your table maps it to the saved session |
| **Agent Framework session** | The serialized `AgentSession` (JSON) | Agent Framework, saved by you | As long as you keep the JSON | Resume the conversation with the same agent configuration. It has **no id of its own**: its providers put their state in it (for example the `sessionDbKey` of `PersistentChatHistoryProvider`, the key of the messages in your store) |
| **Response id** | `resp_68f...` | OpenAI / Azure OpenAI | 30 days when stored; never retrievable with `store = false` | Chain the next turn (`previous_response_id`), read or delete a stored response. It changes at **every** turn |
| **Conversation id** | `conv_68f...` | OpenAI (Conversations API) | Until you delete it (no 30-day limit) | A durable conversation object of the service, see below |

`ChatClientAgentSession.ConversationId` holds the id **of the service** (a `resp_...` or a `conv_...` id), never your business id. Do not use a service id as your business id either: a `resp_...` id changes at each turn, disappears after 30 days, and only exists at this provider.

### The Conversations API (`conv_...`)

The OpenAI **Conversations API** creates a durable conversation object in the service: each response is added to it, and it does not expire after 30 days. Agent Framework supports it natively: create the conversation with the OpenAI SDK, then a session on its id (official sample `Agent_OpenAI_Step05_Conversation`):

```csharp
#pragma warning disable OPENAI001
ConversationClient conversationClient = client.GetConversationClient();   // namespace OpenAI.Conversations
ConversationResource conversation = await conversationClient.CreateConversationAsync(new ConversationCreationOptions
{
    Metadata = { ["business_conversation_id"] = BusinessIds.SupportCase }   // your id, as metadata of the object of the service
});
ChatClientAgent agent = client.GetResponsesClient().AsAIAgent(model: settings.ChatDeploymentName, instructions: Agents.Global.Instructions, name: Agents.Global.Name);
AgentSession session = await agent.CreateSessionAsync(conversation.Id);   // ConversationId = conv_..., it does not change between turns
// ... agent.RunAsync(..., session) ...
await conversationClient.DeleteConversationAsync(conversation.Id);
#pragma warning restore OPENAI001
```

> **Not part of the scenarios: the Conversations API is not available on Azure OpenAI.** On the Azure OpenAI v1 endpoint of these labs (`https://<resource>.openai.azure.com/openai/v1/`), `POST /conversations` returns **404** (checked on 2026-11-07 on a resource where `/responses` works; other users report the same). The code above runs on OpenAI (`api.openai.com`). On Azure OpenAI, keep the conversation with `previous_response_id` (scenario 4) or in your application (scenario 5).

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
| `GetResponsesClient()` / `ResponsesClient` | Client of the **Responses API**; `AsAIAgent(model: deployment, ...)` creates a `ChatClientAgent` on it |
| `store` | Option of the Responses API: `true` (default) the service keeps the response 30 days; `false` it keeps nothing |
| `ChatClientAgentSession.ConversationId` | The id of the conversation **kept by the service**: the last `resp_...` (sent as `previous_response_id`) or a `conv_...`. `null` when the history is on your side |
| `previous_response_id` | Responses API: continue from a stored response; the service adds the history. The instructions are not carried over (the agent sends them again at each run) |
| `AsIChatClientWithStoredOutputDisabled(model)` | Agent Framework: an `IChatClient` on the Responses API with `store = false` (experimental, `MAAI001`); the history is then managed by Agent Framework |
| `new ChatClientAgent(chatClient, options)` | Creates an agent on any `IChatClient` (what `AsAIAgent()` does on an OpenAI client) |
| `ChatClientAgent.CreateSessionAsync(conversationId)` | Creates a session on an existing conversation of the service (`conv_...`) |
| `GetResponseAsync(id)` / `DeleteResponseAsync(id)` | Read / delete a response stored by the service (404 when it is not stored) |

## Namespaces Reference

| Namespace | Purpose |
|-----------|---------|
| `OpenAI` | `OpenAIClient`, `OpenAIClientOptions` |
| `OpenAI.Chat` | `ChatClient` and the `AsAIAgent()` extension methods |
| `OpenAI.Responses` | `ResponsesClient`, its `AsAIAgent(model: ...)` and `AsIChatClientWithStoredOutputDisabled(...)` extension methods (scenarios 4 and 5) |
| `System.ClientModel` | `ApiKeyCredential` |
| `System.ClientModel.Primitives` | `BearerTokenPolicy` |
| `Azure.Identity` | `DefaultAzureCredential` |
| `Microsoft.Agents.AI` | `AIAgent`, `AgentSession`, `AgentResponse`, `ChatClientAgent`, `ChatClientAgentSession`, `ChatClientAgentOptions`, `ChatHistoryProvider`, `InMemoryChatHistoryProvider`, `ProviderSessionState<T>` |
| `Microsoft.Extensions.AI` (aliased as `AIExtensions`, because `OpenAI.Chat` also has a `ChatMessage`) | `ChatMessage`, `MessageCountingChatReducer` (scenario 2), `IChatClient` (scenario 5) |
| `AIAgentWithSessions.Stores` | `IChatHistoryStore`, `PersistentChatHistoryProvider` (scenarios 3 and 5) |
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
=== Scenario 3: Custom ChatHistoryProvider - chat history outside of the application ===
Chat history storage: MongoDB, database 'AIAgentSessions' (Storage:Mode = MongoDb)
...
Simulating a restart: new agent, new connection to the storage, session restored from the saved JSON...
Chat history storage: MongoDB, database 'AIAgentSessions' (Storage:Mode = MongoDb)
Restored session - chat history stored under the key: f3b863cd58334e6aa5edfcc72d376ff9
User: What is the population of that city? And what is my name?
Agent: As of 2023, the population of Paris is approximately 2.1 million. Your name is Ada.
...
----------------------------------------
=== Scenario 4: Responses API - the service keeps the conversation ===
User: Hello, my name is Ada. What is the capital of France?
Agent: The capital of France is Paris.
Conversation id kept in the session: resp_03ba8899cedb2790006aef36e802308193a7f1f8bdadd6bb01
Serialized session:
{
  "conversationId": "resp_03ba8899cedb2790006aef36e802308193a7f1f8bdadd6bb01",
  "stateBag": {
    "InMemoryChatHistoryProvider": {
      "messages": []
    }
  }
}
----------------------------------------
Restoring the session from the saved JSON...
User: What is the population of that city? And what is my name?
Agent: The population of Paris is approximately 2.1 million. Your name is Ada.
Conversation id kept in the session after the follow-up: resp_03ba8899cedb2790006aef36e95d8c8193ac552fe6c2296839
Response resp_03ba8899cedb2790006aef36e95d8c8193ac552fe6c2296839 stored by the service: yes
----------------------------------------
Token Usage (follow-up):
  Input tokens: 76
  Output tokens: 18
  Total tokens: 94
Deleted the 2 responses stored by the service for this conversation
----------------------------------------
=== Scenario 5: Responses API without storage - the conversation stays in your application ===
Chat history storage: JSON file .../MicrosoftAgentFrameworkLabs/AIAgentSessions/chat_history.json (Storage:Mode = JsonFile)
User: Hello, my name is Ada. What is the capital of France?
Agent: The capital of France is Paris.
Response resp_09d2fa8241d9b35a016aef36f6f54c8190ab9196d6544774b1 stored by the service: no (404)
Serialized session:
{
  "stateBag": {
    "PersistentChatHistoryProvider": {
      "sessionDbKey": "7dfc90bab1374aaaacea034296f735bf"
    }
  }
}
----------------------------------------
Simulating a restart: new agent, new connection to the storage, session of 'support-case-1042' restored...
Chat history storage: JSON file .../MicrosoftAgentFrameworkLabs/AIAgentSessions/chat_history.json (Storage:Mode = JsonFile)
User: What is the population of that city? And what is my name?
Agent: The population of Paris is approximately 2.1 million. Your name is Ada.
----------------------------------------
Identifiers of this conversation:
  Business conversation id (your application): support-case-1042
  Agent Framework session: the serialized AgentSession, saved under support-case-1042 (a session has no id of its own)
  Chat history key (PersistentChatHistoryProvider, inside the session): 7dfc90bab1374aaaacea034296f735bf
  Last response id (Responses API, not stored): resp_07acc07bb0a6bd04016aef36f872308190b4fc9648b9a90377
  Service conversation id (ChatClientAgentSession.ConversationId): (none)
----------------------------------------
Token Usage (follow-up):
  Input tokens: 76
  Output tokens: 18
  Total tokens: 94
```

Answers vary from one run to another; the keys and the response ids are new at each run.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `'AzureOpenAI:Endpoint' is not configured` | Replace the `YOUR-...` placeholders in `appsettings.json` (or use user secrets / environment variables) |
| `401 Unauthorized` with an API key | Check the key (user secret `AzureOpenAI:APIKey`) and that it belongs to the resource of the endpoint |
| `401` / `403` with Entra ID | Run `az login` and make sure your identity has the **Cognitive Services OpenAI User** role on the resource |
| `404 DeploymentNotFound` | `ChatDeploymentName` must be the **deployment** name, not the model name |
| `MongoDB is not reachable ...` | Start MongoDB (`docker compose up -d` in `MongoDB/`) and check `docker ps`. Without Docker, set `Storage:Mode` to `EmbeddedMongoDb` or `JsonFile` ([Step 2](#step-2-choose-the-storage-of-scenarios-3-and-5)), or skip scenario 3 with `dotnet run -- --scenarios 1,2` |
| `The embedded MongoDB could not start` | The first run downloads MongoDB from mongodb.org: check the proxy / VPN, or set `MongoDb:BinaryDirectory` to a folder that contains `mongod`. Otherwise, set `Storage:Mode` to `JsonFile` |
| `The embedded MongoDB of '...' is already used by another running lab` | Another run of the lab (Start or Solution) uses the same data folder: stop it first |
| `docker compose up` fails with *port is already allocated* | Another MongoDB already uses port 27017: stop it, or point `MongoDb:ConnectionString` to it |
| The follow-up answer does not know the city or the name | Pass the **restored** session to `RunAsync` (TODO 9, 22), and restore it with the agent that has the same provider |
| `NullReferenceException` on `GetService<...>()!` | The agent has no provider of this type: check `ChatHistoryProvider = ...` in `ChatClientAgentOptions` (TODO 10, 17) |
| `Only ConversationId or ChatHistoryProvider may be used, but not both` | The service stores the history itself (Responses API with `store = true`) **and** the agent has a `ChatHistoryProvider`. Scenario 5: create the agent on `AsIChatClientWithStoredOutputDisabled(...)` (TODO 30), not on `GetResponsesClient()` directly |
| Scenarios 4 and 5 fail with `404` or *"unsupported"* when running the agent | The deployment (model or region) does not support the Responses API: use one that does, or run `dotnet run -- --scenarios 1,2,3` |
| Scenario 4: `stored by the service: no (404)` | The response was not stored: the agent was created on a client that disables the storage. Use `responsesClient.AsAIAgent(model: ...)` |
| Scenario 4: the follow-up does not know Paris or Ada | Pass the **restored** session to `RunAsync`: it holds the `resp_...` id the service chains to |
| `HTTP 400 (invalid_request_error: previous_response_not_found)` | The responses of this session were deleted (TODO 29), expired (30 days), or created with `store = false`: a `resp_...` id can only be chained while the service keeps it |
| `POST /conversations` returns 404 | Expected on Azure OpenAI: the Conversations API is not available there (see [The Conversations API](#the-conversations-api-conv_)) |

## Provided Files

The following files are provided and should not be modified:

- `LabPrompts.cs` - Name, instructions and questions of the agent, the same for everyone
- `AIAgentWithSessions.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs` - Loads and validates the configuration, opens the store of the chat history
- `MongoDbConnection.cs` - Connects to MongoDB, starts it without Docker (`EmbeddedMongoDb`)
- `AzureOpenAISettings.cs` - Settings class for Azure OpenAI
- `StorageSettings.cs` - Settings class for the storage (`Storage:Mode`)
- `MongoDbSettings.cs` - Settings class for MongoDB
- `Stores/PersistentChatHistoryProvider.cs` - `ChatHistoryProvider` on a store
- `Stores/IChatHistoryStore.cs`, `Stores/MongoChatHistoryStore.cs`, `Stores/JsonFileChatHistoryStore.cs` - The stores: MongoDB, JSON file
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
| `MongoDB.Driver` | Official MongoDB driver (scenario 3, `MongoDb` and `EmbeddedMongoDb` modes) |
| `EphemeralMongo` | Downloads and starts MongoDB without Docker (`EmbeddedMongoDb` mode) |

## Going Further

- **Summarize instead of dropping**: the compaction framework of Agent Framework can summarize the old messages with a smaller model, or collapse old tool results, instead of dropping them (`CompactionProvider`, `SummarizationCompactionStrategy`...). It is still experimental (`MAAI001`), see [Compaction](https://learn.microsoft.com/agent-framework/concepts/agents/conversations/compaction?pivots=programming-language-csharp) and the official sample `Agent_Step18_CompactionPipeline`.
- **Other stores**: the official samples also store the chat history in a vector store (`Agent_Step04_3rdPartyChatHistoryStorage`) or in Azure Cosmos DB (`CosmosChatHistoryProvider`, package `Microsoft.Agents.AI.CosmosNoSql`, preview).
- **Production**: index the `SessionId` field of the MongoDB collection, enable authentication on MongoDB, and bind each stored session to its user (see [Security of stored sessions](#security-of-stored-sessions)).
- **Memory beyond the conversation**: context providers can add long-term memories or documents to each run — this is the topic of Lab 12.
- **Durable conversations of the service**: the [Conversations API](#the-conversations-api-conv_) on OpenAI (`conv_...`, `ChatClientAgent.CreateSessionAsync(conversationId)`).
- **Background responses**: long runs on the Responses API (they need `store = true`), official sample `Agent_Step14_BackgroundResponses`.
- **Chat Completions or Responses for the whole course**: [Chat Completions and Responses API](../ChatCompletions-vs-Responses.md) compares the two APIs in Labs 04, 05 and 07.

## Coming from an older version of the lab?

| Before (preview) | Now (1.27.0) |
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
| Agent name and instructions in `appsettings.json` (`AzureOpenAI:Agents:GlobalAgent`) | Constants provided in `LabPrompts.cs` (`Agents.Global`) |
| MongoDB with a password in `appsettings.json` + Mongo Express | MongoDB without authentication bound to `127.0.0.1`, no secret in `appsettings.json`; `mongosh` to look at the data |
| API key in `appsettings.json` | API key in user secrets or environment variables |
| — | Scenario 1: the default in-memory chat history, inside the session |
| `MongoChatHistoryProvider` (MongoDB in Docker only) | `PersistentChatHistoryProvider` on a store chosen by `Storage:Mode`: MongoDB in Docker, MongoDB without Docker (EphemeralMongo) or a JSON file |
| `Lab05-AIAgentWithThreads`, `AIAgentWithThreads.csproj`, namespace `AIAgentWithThreads` | `Lab05-AIAgentWithSessions`, `AIAgentWithSessions.csproj`, namespace `AIAgentWithSessions` (the terminology of Agent Framework). The storage (database `AIAgentSessions`, container `lab05-mongodb`, `chat_history.json`) is unchanged |
| — | Scenarios 4 and 5 (2026-11): the same conversation on the Responses API, kept by the service, then in your application with `store = false` |

## Useful Links

- [Session](https://learn.microsoft.com/agent-framework/concepts/agents/conversations/session?pivots=programming-language-csharp)
- [Storage (chat history providers)](https://learn.microsoft.com/agent-framework/concepts/agents/conversations/storage?pivots=programming-language-csharp)
- [Microsoft Agent Framework documentation](https://learn.microsoft.com/agent-framework/overview/?pivots=programming-language-csharp)
- [Official sample `Agent_Step03_PersistedConversations`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step03_PersistedConversations)
- [Official sample `Agent_Step04_3rdPartyChatHistoryStorage`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step04_3rdPartyChatHistoryStorage) (a `ChatHistoryProvider` on a Responses client with `AsIChatClientWithStoredOutputDisabled`)
- [Official sample `Agent_OpenAI_Step05_Conversation`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/AgentProviders/openai/Agent_OpenAI_Step05_Conversation) (Conversations API, OpenAI)
- [Azure OpenAI Responses API](https://learn.microsoft.com/azure/ai-foundry/openai/how-to/responses) (storage and deletion of the responses)
- [OpenAI — conversation state](https://platform.openai.com/docs/guides/conversation-state) and [migrate to the Responses API](https://platform.openai.com/docs/guides/migrate-to-responses)
- [Azure OpenAI v1 API](https://learn.microsoft.com/azure/foundry/openai/api-version-lifecycle)

## Solution

If you get stuck, check the complete solution in the `Solution/` folder.
