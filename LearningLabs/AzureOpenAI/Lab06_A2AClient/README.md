# Lab 06 - A2A Client (Agent-to-Agent Communication)

## Objective

In this lab, you will call remote agents with the **Agent-to-Agent (A2A) protocol v1**, with **Microsoft Agent Framework** and **Azure OpenAI**.

The remote agents run on the A2A server of **Lab06_A2AServer**: the **AuthAgent** (generates and validates API keys with its own tools) and the **CustomerToneAgent** (detects the tone of a customer message). Your program never sees their tools, their model or their secrets: it discovers them, sends them messages in natural language, and receives their answers. For your code, a remote agent is just another `AIAgent`.

You will implement four scenarios:

- **Scenario 1**: Discover the AuthAgent from its **agent card**, then generate and validate an API key
- **Scenario 2**: Connect to the CustomerToneAgent **by URL** (direct configuration, no agent card)
- **Scenario 3**: Give the remote AuthAgent to a **local** Azure OpenAI agent as a **function tool**
- **Scenario 4**: Have a **conversation** with the remote AuthAgent: a session keeps the same A2A `contextId`, and the server keeps the history

> **Preview packages**: the A2A package of Agent Framework (`Microsoft.Agents.AI.A2A`) and the A2A SDK it uses (`A2A` 1.0.0-preview3) have **no stable version yet**. The lab uses the preview aligned on Agent Framework 1.26.0 (`1.26.0-preview.261023.1`). Every other package is stable.

## What You Will Learn

- How to read an agent card with `A2ACardResolver.GetAgentCardAsync()`: skills and supported interfaces (URL, protocol binding, protocol version)
- How to create an `AIAgent` for a remote agent: `AgentCard.AsAIAgent()`, `A2ACardResolver.GetAIAgentAsync()` or `A2AClient.AsAIAgent()`
- How to call a remote agent with `RunAsync()`, exactly like a local agent
- The difference between **discovery** (agent card) and **direct configuration** (URL known in advance)
- How to use a remote agent as a tool of a local agent with `AsAIFunction()`
- How to have a conversation with a remote agent: a session is an A2A `contextId`, and the conversation only exists if the **server** keeps it
- When to call a remote agent directly or as a tool, and what a network adds: failures, timeouts, authentication of the calling agent

## Prerequisites

- .NET 10 SDK or later
- **Lab06_A2AServer running** on `http://localhost:5000` (Step 0)
- For scenario 3: an Azure OpenAI (or Microsoft Foundry) resource with a chat model deployment that supports function calling (for example `gpt-4o-mini` or `gpt-5.4-mini`), and one of:
  - the resource **API key**, or
  - Azure CLI logged in (`az login`) with the **Cognitive Services OpenAI User** role on the resource
- Lab 03 (function tools) completed

## Project Structure

```
Lab06_A2AClient/
├── README.md
├── Start/                          <-- Your working folder
│   ├── Program.cs                  <-- Complete the TODOs here
│   ├── AgentConsole.cs             <-- Display helpers (provided)
│   ├── ConfigurationHelper.cs
│   ├── AzureOpenAISettings.cs
│   ├── RemoteAgentSettings.cs
│   ├── appsettings.json
│   └── A2AClient.csproj
└── Solution/                       <-- Reference solution
    └── ...
```

## Instructions

### Step 0: Start the A2A server

In another terminal, start the server of **Lab06_A2AServer** (the `Solution`, or your `Start` once completed) and keep it running:

```bash
cd ../Lab06_A2AServer/Solution
dotnet run
```

Wait for `Now listening on: http://localhost:5000`. You can check it with:

```bash
curl http://localhost:5000/a2a/authAgent/.well-known/agent-card.json
```

### Step 1: Configure your settings

Settings are read, in this order (last wins): `appsettings.json` → **user secrets** → **environment variables**.

1. Open `Start/appsettings.json` and set the non-secret values:

   ```json
   {
     "AzureOpenAI": {
       "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
       "ChatDeploymentName": "YOUR-DEPLOYMENT-NAME"
     },
     "RemoteAgents": {
       "AuthAgent": {
         "Url": "http://localhost:5000/a2a/authAgent"
       },
       "CustomerToneAgent": {
         "Name": "CustomerToneAgent",
         "Description": "A customer tone assistant that can detect the tone of a customer's message.",
         "Url": "http://localhost:5000/a2a/customerToneAgent"
       }
     }
   }
   ```

   The Azure OpenAI values are used by scenario 3 only, but they are checked at startup. The `/openai/v1/` suffix required by the v1 API is added for you.
   The `Url` of each remote agent must match the server (environment variables `RemoteAgents__AuthAgent__Url`, `RemoteAgents__CustomerToneAgent__Url` to change them). The `Name` and `Description` of the CustomerToneAgent are used by scenario 2, where no agent card is read.

2. Choose an authentication method for Azure OpenAI:

   - **API key** (simplest for local development) — store it as a user secret, **never** in `appsettings.json`:

     ```bash
     cd Start
     dotnet user-secrets set "AzureOpenAI:APIKey" "<your-api-key>"
     ```

     The user secrets id is shared by all the labs: if you already did it for a previous lab, there is nothing to do.
     You can also use the environment variable `AzureOpenAI__APIKey`.

   - **Microsoft Entra ID** — leave `APIKey` unset and run `az login`. `DefaultAzureCredential` picks up your Azure CLI identity.

3. **Or use the dashboard**: the *Azure OpenAI settings* of the optional [Lab Bench dashboard](../../../Dashboard/README.md#azure-openai-settings) write the endpoint, the deployment and the API key to the same user secrets, for every migrated lab. The dashboard also starts the A2A server for you when you run this lab.

### Step 2: Look at the provided files

- `RemoteAgentSettings.cs` — `Url`, `Name` and `Description` of a remote agent. `ConfigurationHelper.GetRemoteAgentSettings("AuthAgent")` reads the `RemoteAgents:AuthAgent` section and checks that the URL is an absolute `http(s)://` URL.
- `AgentConsole.cs` — display helpers, imported with `using static`:
  - `WriteAgentCard(card)` — the name, version, description, skills and supported interfaces of an agent card;
  - `FindApiKey(text)` — the remote agent answers in natural language: this finds the key (`Meknes<random>.<signature>`) in its answer;
  - `WriteToolCalls(response)` and `WriteTokenUsage(response)` — the tools called during a run and its token usage (as in Lab 03 and Lab 04);
  - `WriteContextId(turn, session)` — the A2A `contextId` of a session (scenario 4).

### Step 3: Complete the Program.cs

Open `Start/Program.cs` and complete the TODOs.

---

#### Setup: Configuration and Azure OpenAI client

TODO 1 to 3 are the same as in Lab 01. The settings of the remote agents are loaded for you (`authAgentSettings`, `customerToneAgentSettings`).

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 1** | Create the `OpenAIClientOptions` for the Azure OpenAI v1 endpoint | • `OpenAIClientOptions clientOptions = new() { Endpoint = AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint) };` |
| **TODO 2** | Create the `OpenAIClient` | • API key: `new OpenAIClient(new ApiKeyCredential(settings.APIKey), clientOptions)` <br> • Entra ID: `new OpenAIClient(new BearerTokenPolicy(new DefaultAzureCredential(), "https://ai.azure.com/.default"), clientOptions)` <br> • `OpenAIClient client = !string.IsNullOrWhiteSpace(settings.APIKey) ? ... : ...;` between the two `#pragma` lines |
| **TODO 3** | Get a `ChatClient` for the deployment | • `ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);` |

---

#### Scenario 1: Discover a remote agent from its agent card

An **agent card** is the discovery document of an A2A agent. The server publishes it at `<agent URL>/.well-known/agent-card.json` (the *well-known URI* discovery of the A2A specification). It tells the client who the agent is, what it can do (skills) and where and how to call it (supported interfaces).

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 4** | Read and display the agent card | • `A2ACardResolver cardResolver = new(new Uri($"{authAgentSettings.Url}/"));` <br> • `AgentCard authAgentCard = await cardResolver.GetAgentCardAsync().WithSpinner("Reading the agent card");` <br> • `WriteAgentCard(authAgentCard);` <br> • The trailing `/` matters: the resolver appends `.well-known/agent-card.json` to the URL |
| **TODO 5** | Create the remote agent from its card | • `AIAgent authAgent = authAgentCard.AsAIAgent();` <br> • Its `Name` and `Description` come from the card; the A2A client is created for one of its `SupportedInterfaces` |
| **TODO 6** | Generate an API key | • `AgentResponse generateResponse = await authAgent.RunAsync("Generate a new API key").WithSpinner("Calling the remote agent");` <br> • `string apiKey = FindApiKey(generateResponse.Text);` <br> • `ColoredConsole.WriteAssistantLine($"Generated API key: {apiKey}");` |
| **TODO 7** | Validate the key, then a tampered copy | • `AgentResponse validResponse = await authAgent.RunAsync($"Validate this API key: {apiKey}").WithSpinner("Calling the remote agent");` <br> • `ColoredConsole.WriteAssistantLine($"Validation of the generated key: {validResponse.Text}");` <br> • `string tamperedKey = apiKey[..^1] + (apiKey[^1] == 'A' ? 'B' : 'A');` <br> • Same `RunAsync` with `tamperedKey`, displayed with `$"Validation of a tampered key: {tamperedResponse.Text}"` <br> • Only the tool of the server can tell: the signature of the tampered key no longer matches |

---

#### Scenario 2: Connect to a remote agent by URL (direct configuration)

When you already know the URL of an agent (configuration, private registry), you do not need its card: create an `A2AClient` for the URL. It uses the **JSON-RPC** binding, and the agent gets the name and description **you** give it.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 8** | Create the A2A client and the remote agent | • `using A2A.A2AClient toneClient = new(new Uri(customerToneAgentSettings.Url));` — written in full because the namespace of this project is also called `A2AClient` <br> • `AIAgent customerToneAgent = toneClient.AsAIAgent(name: customerToneAgentSettings.Name, description: customerToneAgentSettings.Description);` <br> • `ColoredConsole.WritePrimaryLogLine($"Remote agent: {customerToneAgent.Name} at {customerToneAgentSettings.Url}");` |
| **TODO 9** | Ask for the tone of a customer message | • `const string CustomerMessage = "I have been waiting for my order for two weeks and nobody answers my emails!";` <br> • `ColoredConsole.WriteUserLine($"Customer message: {CustomerMessage}");` <br> • `AgentResponse toneResponse = await customerToneAgent.RunAsync($"What is the tone of this customer message: \"{CustomerMessage}\"").WithSpinner("Calling the remote agent");` <br> • `ColoredConsole.WriteAssistantLine($"Tone: {toneResponse.Text}");` |

---

#### Scenario 3: A remote agent as a function tool of a local agent

`AsAIFunction()` turns any `AIAgent` into an `AIFunction`: a local agent can then call the remote agent like the function tools of Lab 03. The **local** model (your Azure OpenAI deployment) decides when to call it; the remote agent runs on the server with **its** model and tools.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 10** | Create the remote AuthAgent in one call | • `A2ACardResolver cardResolver = new(new Uri($"{authAgentSettings.Url}/"));` <br> • `AIAgent authAgent = await cardResolver.GetAIAgentAsync().WithSpinner("Reading the agent card");` — reads the card and calls `AsAIAgent()` for you |
| **TODO 11** | Create the local agent with the remote agent as a tool | • `AIAgent assistant = chatClient.AsAIAgent(instructions: "You are a helpful assistant. Use the available tools to answer the user's questions.", name: "Assistant", tools: [authAgent.AsAIFunction()]);` <br> • The tool is named after the remote agent (`AuthAgent`) and described by its card description |
| **TODO 12** | Run the local agent and display the result | • `AgentResponse response = await assistant.RunAsync("I need a new API key. Generate one, check that it is valid, and give me the key and the result of the check.").WithSpinner("Running the local agent");` <br> • `WriteToolCalls(response);` <br> • `ColoredConsole.WriteAssistantLine($"Assistant: {response.Text}");` <br> • `WriteTokenUsage(response);` |

---

#### Scenario 4: A conversation with a remote agent

Until now, each `RunAsync` without a session started a new conversation with the remote agent. To continue a conversation, create a **session**: the first answer of the server carries a `contextId`, and every following request of the session sends it back. On the server, the history of each `contextId` lives in a **session store**: Lab06_A2AServer registers one for each agent (its TODO 8). Without it, the server keeps nothing — same `contextId` or not ([official sample `A2AClientServer`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/05-end-to-end/A2AClientServer): *"Create a session so requests share the same A2A protocol context"*).

The second turn asks for "the key you just generated" without giving it. The code checks that the answer contains **exactly** the key of turn 1: a remote agent without the conversation can generate a new key and say "it is valid", which proves nothing.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 13** | Create the remote AuthAgent and a session | • Same as TODO 10: `A2ACardResolver cardResolver = new(new Uri($"{authAgentSettings.Url}/"));` then `AIAgent authAgent = await cardResolver.GetAIAgentAsync().WithSpinner("Reading the agent card");` <br> • `AgentSession session = await authAgent.CreateSessionAsync();` |
| **TODO 14** | First turn: generate a key | • `AgentResponse generateResponse = await authAgent.RunAsync("Generate a new API key", session).WithSpinner("Calling the remote agent");` <br> • `string apiKey = FindApiKey(generateResponse.Text);` <br> • `ColoredConsole.WriteAssistantLine($"Generated API key: {apiKey}");` <br> • `WriteContextId("turn 1", session);` |
| **TODO 15** | Second turn, same session: ask for the key again and check it | • `const string FollowUpQuestion = "Give me again the API key you just generated, exactly as it is, and check that it is valid.";` <br> • `AgentResponse followUpResponse = await authAgent.RunAsync(FollowUpQuestion, session).WithSpinner("Calling the remote agent");` <br> • `ColoredConsole.WriteAssistantLine($"Same session: {followUpResponse.Text}");` <br> • `ColoredConsole.WriteSecondaryLogLine($"  -> Answer contains the key of turn 1: {(followUpResponse.Text.Contains(apiKey, StringComparison.Ordinal) ? "yes" : "no")}");` <br> • `WriteContextId("turn 2", session);` — the same id as turn 1 |
| **TODO 16** | The same question without the session | • `AgentResponse newConversationResponse = await authAgent.RunAsync(FollowUpQuestion).WithSpinner("Calling the remote agent");` <br> • `ColoredConsole.WriteAssistantLine($"New conversation: {newConversationResponse.Text}");` <br> • The same `-> Answer contains the key of turn 1` line with `newConversationResponse` |

---

### Step 4: Run and Test

With the server running (Step 0):

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

To run only some scenarios, edit `scenariosToRun` at the top of `Program.cs` (for example `[1]`).

## How A2A communication works

```
Lab06_A2AClient (this lab)                               Lab06_A2AServer
──────────────────────────                               ───────────────
Scenario 1  A2ACardResolver.GetAgentCardAsync() ──GET──▶  /a2a/authAgent/.well-known/agent-card.json
            AgentCard.AsAIAgent()
            authAgent.RunAsync("Generate a new API key") ──POST (A2A message)──▶ AuthAgent
                                                                                 ├─ model (server)
                                                         ◀── A2A message ─────── └─ generate_api_key
Scenario 2  new A2AClient(url).AsAIAgent(name, ...) ──POST (JSON-RPC)──▶ /a2a/customerToneAgent
Scenario 3  local agent (Azure OpenAI, your model)
            └─ tool "AuthAgent" = authAgent.AsAIFunction() ──POST──▶ AuthAgent (server model + tools)
Scenario 4  session = authAgent.CreateSessionAsync()
            RunAsync(..., session) ──POST (no contextId)──▶ AuthAgent ──▶ new contextId, history saved
                                   ◀── answer + contextId ──             in the session store
            RunAsync(..., session) ──POST (same contextId)──▶ AuthAgent ──▶ history loaded from the store
```

1. **Discovery** — the client reads the agent card: identity, skills, and `SupportedInterfaces` (URL + binding: `JSONRPC` or `HTTP+JSON` + protocol version `1.0`).
2. **Client** — `AsAIAgent()` creates an `A2AAgent` (an `AIAgent`) on top of an A2A client for one of these interfaces.
3. **Message** — `RunAsync()` sends your text as an A2A message. The server runs its agent (model call, tools) and answers with an A2A message, which becomes an `AgentResponse`.
4. **Nothing is shared but messages**: the client never sees the tools, the signing secret or the Azure OpenAI credentials of the server. That is why only the server can tell a real key from a tampered one.
5. **Conversation** — a session carries the `contextId` chosen by the server on the first answer. The history stays on the server: *"The remote agent manages its own conversation state (keyed by context ID). [...] If the remote agent restarts and loses state, your conversation context may be lost."* ([Agent-to-Agent](https://learn.microsoft.com/agent-framework/journey/agent-to-agent?pivots=programming-language-csharp)).

## Direct call or agent as a tool?

A remote agent is an `AIAgent`: the two ways to delegate of the MultiAgentSystem lab [Lab01-UseAgentAsTool](../../MultiAgentSystem/Lab01-UseAgentAsTool/README.md) apply as they are.

| | Direct call (scenarios 1, 2, 4) | Agent as a tool (scenario 3) |
|---|---|---|
| Who decides to call the remote agent | Your code | The model of your local agent |
| What comes back | The `AgentResponse` of the remote agent | Only the text of its answer, inside the run of the local agent |
| Conversation | A session (`contextId`) that your code keeps | A new conversation for each call, unless the tool is created with `AsAIFunction(session: ...)` |
| Use it for | A remote step that your code must run and check | A remote specialist that the local agent consults when needed |

When all the agents live in the same process and are maintained by the same team, agents as tools are simpler: *"A2A adds value when you cross a process, service, or organizational boundary."*

## What a network changes

- **Latency and failures**: *"Every A2A call is an HTTP request."* A remote agent can be down, slow or replaced by a new version: *"You need to handle network failures, timeouts, retries, and versioning"* ([Agent-to-Agent](https://learn.microsoft.com/agent-framework/journey/agent-to-agent?pivots=programming-language-csharp)). An unreachable server is an exception in your code (see *Troubleshooting*); an `HttpClient` gives up after its `Timeout` (100 seconds by default). For tasks that take minutes, see *Long-running tasks* in *Going further*.
- **Conversation state**: it lives on the server, in the session store of the agent. With the in-memory store of Lab06_A2AServer, a restart of the server loses every conversation.
- **Authentication of the calling agent**: in production, the server requires a token. Give the client an `HttpClient` that sends it, for the card and for the messages ([authentication guide of the official sample](https://github.com/microsoft/agent-framework/blob/main/dotnet/samples/05-end-to-end/A2AClientServer/AUTHENTICATION.md)):

  ```csharp
  using var handler = new HttpClientHandler { AllowAutoRedirect = false };
  using var httpClient = new HttpClient(handler);
  httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

  A2ACardResolver cardResolver = new(agentUri, httpClient);
  AgentCard card = await cardResolver.GetAgentCardAsync();
  AIAgent remoteAgent = card.AsAIAgent(httpClient);
  ```

  The server then isolates the conversations by caller: a `contextId` is not a proof of identity (see *Security* in Lab06_A2AServer).
- **The answer is untrusted data**: a remote agent belongs to another team or organization. Check what your code uses (scenario 1 checks the key with the server; scenario 4 compares it with the key of turn 1).

In scenarios 1 and 2, your program makes **no** model call: the models run on the server. Only scenario 3 uses your Azure OpenAI deployment (and prints a token usage): the remote calls it makes are not included in that usage.

## Key Concepts

| Concept | Description |
|---------|-------------|
| A2A (Agent-to-Agent) | Open protocol for agents to discover and call each other over HTTP, independently of their framework |
| `AgentCard` | Discovery document of an agent: `Name`, `Description`, `Skills`, `Capabilities`, `SupportedInterfaces` |
| `A2ACardResolver` | Reads the card at `<base URL>/.well-known/agent-card.json` (`GetAgentCardAsync()`); `GetAIAgentAsync()` also creates the agent |
| `AgentCard.AsAIAgent()` | Creates an `AIAgent` for the agent of a card (name, description and interface from the card) |
| `A2AClient` | A2A client for one URL, with the JSON-RPC binding; `AsAIAgent(name, description)` creates an `AIAgent` from it (direct configuration) |
| `A2AAgent` | The `AIAgent` returned by these methods: `RunAsync()`, `RunStreamingAsync()`, `CreateSessionAsync()` work as for any agent |
| `AgentSession` / `A2AAgentSession` | The session of a remote agent: its `ContextId` is the A2A conversation id, chosen by the server and sent back with every request of the session |
| `AsAIFunction()` | Turns an `AIAgent` (local or remote) into an `AIFunction` that another agent can call |
| `A2AClientOptions.PreferredBindings` | Chooses the protocol binding among the interfaces of the card (see *Going further*) |

## Namespaces Reference

| Namespace | Purpose |
|-----------|---------|
| `OpenAI` | `OpenAIClient`, `OpenAIClientOptions` |
| `OpenAI.Chat` | `ChatClient` and the `AsAIAgent()` extension method |
| `System.ClientModel` | `ApiKeyCredential` |
| `System.ClientModel.Primitives` | `BearerTokenPolicy` |
| `Azure.Identity` | `DefaultAzureCredential` |
| `Microsoft.Agents.AI` | `AIAgent`, `AgentResponse`, `AgentSession`, `AsAIFunction()` |
| `Microsoft.Agents.AI.A2A` | `A2AAgentSession` (in `AgentConsole.cs`) |
| `A2A` | `A2ACardResolver`, `AgentCard`, `A2AClient`, and the `AsAIAgent()` / `GetAIAgentAsync()` extension methods of Agent Framework |
| `Microsoft.Extensions.AI` | `FunctionCallContent` (in `AgentConsole.cs`) |
| `CommonUtilities` | `ColoredConsole`, `WithSpinner()`, `AzureOpenAIEndpoint` |

## Expected Output

```
Endpoint: https://your-resource.openai.azure.com/
Deployment: your-deployment-name
----------------------------------------
=== Scenario 1: Discover a remote agent from its agent card ===
Agent card: AuthAgent (version 1.0.0)
  Description: An authentication agent specialized in generating and validating API keys. Only handles authentication-related tasks.
  Skill: GenerateAPIKey - Generates a new random API key that starts with 'Meknes'. The key includes a cryptographic signature for validation.
  Skill: ValidateAPIKey - Validates an API key by checking if it starts with 'Meknes' and verifying its cryptographic signature.
  Interface: JSONRPC (A2A 1.0) at http://localhost:5000/a2a/authAgent
  Interface: HTTP+JSON (A2A 1.0) at http://localhost:5000/a2a/authAgent
Generated API key: MeknesZeasqW4ORZ_UzJQIovBHN9fe7SPfg8jadUTZzK1Bea4.qAwjnjO9fPNFBLRsM7od_0ceFnKpIEdKWxjS1u3ZE7I
Validation of the generated key: The API key is valid.
Validation of a tampered key: The API key you provided is not valid.
----------------------------------------
=== Scenario 2: Connect to a remote agent by URL (direct configuration) ===
Remote agent: CustomerToneAgent at http://localhost:5000/a2a/customerToneAgent
Customer message: I have been waiting for my order for two weeks and nobody answers my emails!
Tone: Frustrated. The customer expresses dissatisfaction with the delay in their order and the lack of communication...
----------------------------------------
=== Scenario 3: A remote agent as a function tool of a local agent ===
Tool called: AuthAgent
Tool called: AuthAgent
Assistant: Your new API key is: MekneswfgBqwoTo8xR_kcg0pdT3hGQZkZw1WfFDyfHCsLtLfc.msIHprlvcWq4Rgm_xb0oUCk9JQv64ZJBKLEaEnfQLvI
The API key has been successfully validated and is valid.
----------------------------------------
Token Usage:
  Input tokens: 689
  Output tokens: 184
  Total tokens: 873
----------------------------------------
=== Scenario 4: A conversation with a remote agent ===
Generated API key: Meknes89JCH9LZV6dxG5fBipadJb-YT2WR5cx3zTlZhYQTyvk._hCm1ngpLmSMPRkNhyyzMR09QBO88BoH0N0dsLAkumI
Context id after turn 1: 3aec061b267945b48faaa1fac1c27a91
Same session: Here is the API key you requested, exactly as it is:
**Meknes89JCH9LZV6dxG5fBipadJb-YT2WR5cx3zTlZhYQTyvk._hCm1ngpLmSMPRkNhyyzMR09QBO88BoH0N0dsLAkumI**
The key is valid.
  -> Answer contains the key of turn 1: yes
Context id after turn 2: 3aec061b267945b48faaa1fac1c27a91
New conversation: The generated API key is: **Meknesl_w9qApEb0EBkvgPF9J3oLkrGlYGswIpAvCE16O0B90.MiszCP8Y7Au0_zuYRgoH13JSW6dttyeWN-6vWG4DQBM**
It is valid.
  -> Answer contains the key of turn 1: no
```

The keys are random and the wording of the answers varies. The explanation given for the tampered key may be wrong (the tool only returns `false`): what matters is *not valid*. In scenario 4, the new conversation often generates **another** key (and validates it, with either result): the remote agent did not refuse, it answered a different question.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `HttpRequestException: Connection refused` / `A2AException: HTTP request failed` | The A2A server is not running, or not on this URL: start Lab06_A2AServer (Step 0) and check `RemoteAgents:*:Url` |
| `A2AException ... 403 (Forbidden)` on `localhost:5000` | On **macOS**, the *AirPlay Receiver* also listens on port 5000: when the lab server is not running, AirPlay answers. Start the server (or turn AirPlay Receiver off in System Settings → General → AirDrop & Handoff) |
| `404 (Not Found)` while reading the agent card | Wrong agent URL, or the trailing `/` is missing in `new Uri($"{...Url}/")` |
| *"'method' field is not a valid A2A method"* | The server speaks A2A **v0.3** (old version of Lab06_A2AServer): A2A v1 clients and v0.3 servers are not compatible. Use the migrated server |
| *"Agent handler did not produce any response events"* | The agent failed on the server: look at the server console (see the troubleshooting of Lab06_A2AServer) |
| `No API key found in the answer of the remote agent` | The AuthAgent did not return a key: look at the server console, and check that its tools are registered |
| `'AzureOpenAI:Endpoint' is not configured` | Replace the `YOUR-...` placeholders in `appsettings.json` (or use user secrets / environment variables) |
| `'RemoteAgents:AuthAgent:Url' must be an absolute http:// or https:// URL` | Use for example `http://localhost:5000/a2a/authAgent` |
| Scenario 3: `401 Unauthorized` / `403` from Azure OpenAI | API key: check the user secret `AzureOpenAI:APIKey`. Entra ID: run `az login` and check the **Cognitive Services OpenAI User** role |
| Scenario 3: no `Tool called:` line | The local model answered without the tool: check `tools: [authAgent.AsAIFunction()]` |
| Scenario 4: `-> Answer contains the key of turn 1: no` for the same session | The server keeps no conversation: check the session store of Lab06_A2AServer (its TODO 8), and that the server was not restarted between the two turns. Check also that both turns pass `session` |

## Provided Files

The following files are provided and should not be modified:

- `A2AClient.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs` - Loads and validates the configuration
- `AzureOpenAISettings.cs`, `RemoteAgentSettings.cs` - Settings classes
- `AgentConsole.cs` - Display helpers

The following files should be modified:

- `appsettings.json` - Update with your Azure OpenAI endpoint and deployment
- `Program.cs` - Complete the TODOs

### NuGet packages

| Package | Why |
|---------|-----|
| `Microsoft.Agents.AI.OpenAI` | Agent Framework + `AsAIAgent()` for the local agent of scenario 3 (brings `OpenAI` and `Microsoft.Extensions.AI`) |
| `Microsoft.Agents.AI.A2A` (preview) | `A2AAgent` and the `AsAIAgent()` / `GetAIAgentAsync()` extension methods; brings the A2A SDK v1 (`A2A`: `A2ACardResolver`, `A2AClient`, `AgentCard`) |
| `Azure.Identity` | `DefaultAzureCredential` for Entra ID authentication |
| `Microsoft.Extensions.Configuration.*` | `appsettings.json`, user secrets and environment variables, bound to the settings classes |

## Going Further

- **Choose the protocol binding**: `authAgentCard.AsAIAgent(options: new A2AClientOptions { PreferredBindings = [ProtocolBindingNames.HttpJson] })` (official sample `A2AAgent_ProtocolSelection`). With the cards of this server, the lab observed JSON-RPC by default.
- **One tool per skill**: instead of one tool for the whole agent, create one `AIFunction` per `AgentSkill` of the card (official sample `A2AAgent_Skills`).
- **Continue a conversation later**: keep the `contextId` of the session, then `A2AAgent.CreateSessionAsync(contextId)` creates a session that continues it (as long as the server still has it).
- **Long-running tasks**: polling with continuation tokens (`A2AAgent_PollingForTaskCompletion`) and stream reconnection (`A2AAgent_StreamReconnection`).

## Coming from an older version of the lab?

| Before (preview) | Now (1.26.0 / A2A v1) |
|------------------|------------------|
| A2A protocol **v0.3** (`A2A` 0.3.x) | A2A protocol **v1** (`A2A` 1.0.0-preview3): the client needs the migrated Lab06_A2AServer |
| `new A2A.A2AClient(uri).GetAIAgent()` | `new A2A.A2AClient(uri).AsAIAgent(name: ..., description: ...)` |
| `A2ACardResolver.GetAIAgentAsync()` (unused helper) | Scenario 1: `GetAgentCardAsync()` + `AgentCard.AsAIAgent()`; scenario 3: `GetAIAgentAsync()` |
| Remote agent without a name (direct URL) | Name and description from the card, or given by the client |
| `Azure.AI.OpenAI` / `AzureOpenAIClient` created but never used | `OpenAIClient` + Azure OpenAI v1 endpoint, used by scenario 3 |
| Whole answer used as the API key | `FindApiKey()` extracts the key from the answer |
| `RemoteAuthAgentSettings` (`RemoteAuthAgentSettings` section) | `RemoteAgentSettings` (`RemoteAgents:AuthAgent`, `RemoteAgents:CustomerToneAgent`) |
| `Microsoft.Extensions.Hosting` (configuration only), `<NoWarn>MEAI001</NoWarn>` | `Microsoft.Extensions.Configuration.*`, no global `NoWarn` |
| API key in `appsettings.json` | API key in user secrets or environment variables |
| One scenario (AuthAgent only) | Four scenarios: discovery, direct configuration (CustomerToneAgent), remote agent as a tool, conversation |

## Useful Links

- [A2A integration of Agent Framework](https://learn.microsoft.com/agent-framework/integrations/by-component/agent-services/a2a?pivots=programming-language-csharp) and [A2A SDK v1 migration guide](https://learn.microsoft.com/agent-framework/migration-guide/agent-to-agent-sdk-v1?pivots=programming-language-csharp)
- [Microsoft Agent Framework documentation](https://learn.microsoft.com/agent-framework/overview/?pivots=programming-language-csharp)
- Official samples: [`Agent_With_A2A`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/AgentProviders/a2a/Agent_With_A2A), [A2A samples](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/A2A) (`A2AAgent_AsFunctionTool`, `A2AAgent_ProtocolSelection`, `A2AAgent_Skills`), [`A2AClientServer`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/05-end-to-end/A2AClientServer)
- [A2A protocol](https://a2a-protocol.org/latest/) and [agent discovery](https://a2a-protocol.org/latest/topics/agent-discovery/)
- [Azure OpenAI v1 API](https://learn.microsoft.com/azure/foundry/openai/api-version-lifecycle)

## Solution

If you get stuck, check the complete solution in the `Solution/` folder.
