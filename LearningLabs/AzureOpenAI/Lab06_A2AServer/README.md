# Lab 06 - A2A Server (Agent-to-Agent Communication)

## Objective

In this lab, you will expose two AI agents with the **Agent-to-Agent (A2A) protocol v1**, with **Microsoft Agent Framework**, ASP.NET Core and **Azure OpenAI**.

A2A is an open protocol that lets agents call each other over HTTP, whatever framework they are built with. An A2A **server** hosts agents; for each one it publishes an **agent card** (who the agent is, what it can do, where and how to call it) and answers the messages that A2A **clients** send to it. You will build a server with two agents:

1. **AuthAgent** - generates and validates API keys with two function tools (HMAC-SHA256 signatures)
2. **CustomerToneAgent** - detects the tone of a customer's message

The next lab, **Lab06_A2AClient**, connects to this server.

> **Preview packages**: the A2A packages of Agent Framework (`Microsoft.Agents.AI.Hosting.A2A.AspNetCore`) and the A2A SDK they use (`A2A` 1.0.0-preview2) have **no stable version yet**. The lab uses the preview aligned on Agent Framework 1.22.0 (`1.22.0-preview.260918.1`). Every other package is stable.

## What You Will Learn

- How to host agents in an ASP.NET Core application with `builder.AddA2AServer(agent)`
- How to expose an agent with the two protocol bindings of A2A v1: **JSON-RPC** (`MapA2AJsonRpc`) and **HTTP+JSON** (`MapA2AHttpJson`)
- How to describe an agent with an `AgentCard` (skills, `SupportedInterfaces`) and publish it at `.well-known/agent-card.json` (`MapWellKnownAgentCard`)
- How to give a hosted agent function tools, exactly as in Lab 03
- Why the model and the tools run on the **server**: a client only sends messages

## Prerequisites

- .NET 10 SDK or later
- An Azure OpenAI (or Microsoft Foundry) resource with a chat model deployment that supports function calling (for example `gpt-4o-mini` or `gpt-5.4-mini`)
- One of the following for authentication:
  - the resource **API key**, or
  - Azure CLI logged in (`az login`) with the **Cognitive Services OpenAI User** role on the resource
- Port **5000** free on your machine (see *Troubleshooting* on macOS)
- Lab 03 (function tools) completed

## Project Structure

```
Lab06_A2AServer/
├── README.md
├── Start/                          <-- Your working folder
│   ├── Program.cs                  <-- Complete the TODOs here
│   ├── AgentCards.cs               <-- Agent cards of the two agents (provided)
│   ├── Tools/
│   │   └── APIKeyTools.cs          <-- Tools of the AuthAgent (provided)
│   ├── StreamingWorkaround.cs      <-- Temporary workaround (provided, see Step 2)
│   ├── ConfigurationHelper.cs
│   ├── AzureOpenAISettings.cs
│   ├── A2AServerSettings.cs
│   ├── APIKeySettings.cs
│   ├── appsettings.json
│   └── A2AServer.csproj
└── Solution/                       <-- Reference solution
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
     "A2AServer": {
       "BaseUrl": "http://localhost:5000"
     },
     "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } }
   }
   ```

   Use the resource endpoint shown in the Azure portal. The `/openai/v1/` suffix required by the v1 API is added for you.
   `A2AServer:BaseUrl` is the address the server listens on **and** the address written in the agent cards: keep them identical (environment variable `A2AServer__BaseUrl` to change it).

2. Choose an authentication method for Azure OpenAI:

   - **API key** (simplest for local development) — store it as a user secret, **never** in `appsettings.json`:

     ```bash
     cd Start
     dotnet user-secrets set "AzureOpenAI:APIKey" "<your-api-key>"
     ```

     The user secrets id is shared by all the labs: if you already did it for a previous lab, there is nothing to do.
     You can also use the environment variable `AzureOpenAI__APIKey`.

   - **Microsoft Entra ID** — leave `APIKey` unset and run `az login`. `DefaultAzureCredential` picks up your Azure CLI identity.

3. **Signing secret of the API keys (optional)**. The AuthAgent signs the keys it generates with HMAC-SHA256. Without configuration, the server creates a **random secret at startup**: the keys stay valid until the server stops. To keep them valid across restarts, set your own secret — it is a secret, keep it out of `appsettings.json`:

   ```bash
   cd Start
   dotnet user-secrets set "APIKeySettings:SecretKey" "$(openssl rand -base64 32)"
   ```

   or use the environment variable `APIKeySettings__SecretKey`.

4. **Or use the dashboard**: the *Azure OpenAI settings* of the optional [Lab Bench dashboard](../../../Dashboard/README.md#azure-openai-settings) write the endpoint, the deployment and the API key to the same user secrets, for every migrated lab.

### Step 2: Look at the provided files

- `AgentCards.cs` — the `AgentCard` of each agent: `Name`, `Description`, `Version`, `Skills` (`AgentSkill`: id, name, description, tags, examples) and `SupportedInterfaces`. Each interface is a URL + a protocol binding (`ProtocolBindingNames.JsonRpc`, `ProtocolBindingNames.HttpJson`) + a protocol version (`"1.0"`). The URL of the agent is given by `Program.cs` (`BaseUrl` + path of the agent).
- `Tools/APIKeyTools.cs` — `GenerateAPIKey()` returns `Meknes<random>.<signature>`; `ValidateAPIKey(apiKey)` checks the prefix and the signature (constant-time comparison). `IsSecretConfigured` tells whether the secret comes from the configuration.
- `A2AServerSettings.cs`, `APIKeySettings.cs` — the settings above. `ConfigurationHelper.GetA2AServerSettings()` checks that `BaseUrl` is an absolute `http(s)://` URL.
- `StreamingWorkaround.cs` — **temporary workaround**. The A2A hosting always runs the agents in **streaming** mode. Since the end of September 2026, Azure OpenAI streams content-filter annotations that `Microsoft.Extensions.AI.OpenAI` 10.10.x cannot read ([dotnet/extensions#7790](https://github.com/dotnet/extensions/issues/7790)): every A2A request then fails with *"Agent handler did not produce any response events"*. `WithNonStreamingResponses()` is a chat client middleware (built with the official `ChatClientBuilder.Use`) that answers the streaming requests with one non-streaming call. When the bug is fixed, delete TODO 4 and create the agents with `chatClient.AsAIAgent(...)`.

### Step 3: Complete the Program.cs

Open `Start/Program.cs` and complete the TODOs.

---

#### Setup: Configuration and Azure OpenAI client

TODO 1 to 3 are the same as in Lab 01.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 1** | Create the `OpenAIClientOptions` for the Azure OpenAI v1 endpoint | • `OpenAIClientOptions clientOptions = new() { Endpoint = AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint) };` |
| **TODO 2** | Create the `OpenAIClient` | • API key: `new OpenAIClient(new ApiKeyCredential(settings.APIKey), clientOptions)` <br> • Entra ID: `new OpenAIClient(new BearerTokenPolicy(new DefaultAzureCredential(), "https://ai.azure.com/.default"), clientOptions)` <br> • `OpenAIClient client = !string.IsNullOrWhiteSpace(settings.APIKey) ? ... : ...;` between the two `#pragma` lines |
| **TODO 3** | Get a `ChatClient` for the deployment | • `ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);` |
| **TODO 4** | Create the chat client of the agents (temporary workaround, Step 2) | • `IChatClient agentChatClient = chatClient.AsIChatClient().WithNonStreamingResponses();` |

---

#### Scenario 1: A2A server with two agents

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 5** | Create the ASP.NET Core application builder | • `var builder = WebApplication.CreateBuilder(args);` |
| **TODO 6** | Create the tools of the AuthAgent | • `APIKeySettings apiKeySettings = ConfigurationHelper.GetAPIKeySettings();` <br> • `APIKeyTools apiKeyTools = new(apiKeySettings);` <br> • `if (!apiKeyTools.IsSecretConfigured) { ColoredConsole.WriteSecondaryLogLine("APIKeySettings:SecretKey is not set: API keys are signed with a random secret, valid until the server stops."); }` |
| **TODO 7** | Wrap the two methods as AI tools (Lab 03) | • `IList<AITool> tools = [AIFunctionFactory.Create(apiKeyTools.GenerateAPIKey, "generate_api_key"), AIFunctionFactory.Create(apiKeyTools.ValidateAPIKey, "validate_api_key")];` |
| **TODO 8** | Create the two agents | • `AIAgent authAgent = agentChatClient.AsAIAgent(instructions: "You are a helpful API key assistant. You are able to generate and validate API keys. When you generate a key, return the key exactly as produced by the tool.", name: "AuthAgent", tools: tools);` <br> • `AIAgent customerToneAgent = agentChatClient.AsAIAgent(instructions: "You are a helpful customer tone assistant. You are able to detect the tone of a customer's message. Start your answer with the tone in one word (for example: Angry, Neutral, Satisfied), then explain briefly.", name: "CustomerToneAgent");` |
| **TODO 9** | Register an A2A server for each agent, then build the app | • `builder.AddA2AServer(authAgent);` <br> • `builder.AddA2AServer(customerToneAgent);` <br> • `var app = builder.Build();` <br> • `AddA2AServer` registers, for the agent **name**, the A2A request handler and a task store |
| **TODO 10** | Map both protocol bindings of each agent | • `app.MapA2AJsonRpc(authAgent, AuthAgentPath);` <br> • `app.MapA2AHttpJson(authAgent, AuthAgentPath);` <br> • Same two lines for `customerToneAgent` and `CustomerToneAgentPath` |
| **TODO 11** | Publish the agent cards | • `app.MapWellKnownAgentCard(AgentCards.CreateAuthAgentCard($"{serverSettings.BaseUrl}{AuthAgentPath}"), AuthAgentPath);` <br> • `app.MapWellKnownAgentCard(AgentCards.CreateCustomerToneAgentCard($"{serverSettings.BaseUrl}{CustomerToneAgentPath}"), CustomerToneAgentPath);` <br> • With a path, the card is served at `<path>/.well-known/agent-card.json` |
| **TODO 12** | Run the server | • `await app.RunAsync(serverSettings.BaseUrl);` — it runs until `Ctrl+C` |

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

Keep the server running, then, in another terminal:

```bash
# Agent cards (discovery documents)
curl http://localhost:5000/a2a/authAgent/.well-known/agent-card.json
curl http://localhost:5000/a2a/customerToneAgent/.well-known/agent-card.json

# Send a message with the JSON-RPC binding (method SendMessage)
curl -X POST http://localhost:5000/a2a/customerToneAgent -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"SendMessage","params":{"message":{"messageId":"1","role":"ROLE_USER","parts":[{"text":"Where is my order? I am very disappointed."}]}}}'

# The same with the HTTP+JSON binding
curl -X POST http://localhost:5000/a2a/customerToneAgent/message:send -H "Content-Type: application/json" \
  -d '{"message":{"messageId":"2","role":"ROLE_USER","parts":[{"text":"Thank you, everything arrived on time!"}]}}'
```

Then run **Lab06_A2AClient**, which uses both agents.

## How an A2A server works

```
Lab06_A2AClient                          Lab06_A2AServer (ASP.NET Core, http://localhost:5000)
───────────────                          ─────────────────────────────────────────────────────
GET  /a2a/authAgent/.well-known/  ─────▶  MapWellKnownAgentCard  → AgentCard (skills, interfaces)
     agent-card.json

POST /a2a/authAgent               ─────▶  MapA2AJsonRpc  ┐
     (JSON-RPC: SendMessage)              MapA2AHttpJson ┴─▶ A2A server of "AuthAgent" (AddA2AServer)
POST /a2a/authAgent/message:send                              └─▶ AIAgent "AuthAgent" (Azure OpenAI)
     (HTTP+JSON)                                                    └─▶ generate_api_key / validate_api_key
                                  ◀─────  A2A Message (text parts)
```

1. **Discovery** — the client reads the agent card. `SupportedInterfaces` tells it where the agent is and which bindings it speaks.
2. **Message** — the client sends an A2A message (`role`, `parts`) with one of the bindings. `MapA2AJsonRpc` and `MapA2AHttpJson` translate both into the same request for the A2A server of the agent.
3. **Run** — the A2A server runs the `AIAgent` on the **server**: the model call, the tool loop and the tools (`GenerateAPIKey`, `ValidateAPIKey`) all happen here. The client never sees the tools, the signing secret or the Azure OpenAI credentials.
4. **Answer** — by default (`AgentRunMode.ReturnMessage`), the answer is one A2A `Message`. Long-running agents can return an A2A **task** instead (see *Going further*).

## Key Concepts

| Concept | Description |
|---------|-------------|
| A2A (Agent-to-Agent) | Open protocol for agents to discover and call each other over HTTP, independently of their framework |
| `AgentCard` | Discovery document of an agent: identity, `Skills`, `Capabilities`, `SupportedInterfaces` |
| `AgentSkill` | One capability advertised by the agent (id, name, description, tags, examples). It helps clients and models choose the agent; it is not a function schema |
| `AgentInterface` | Where and how to call the agent: `Url`, `ProtocolBinding`, `ProtocolVersion` |
| Protocol bindings | `JSONRPC` (JSON-RPC 2.0, method `SendMessage`) and `HTTP+JSON` (REST, `POST .../message:send`); A2A v1 also defines gRPC |
| `AddA2AServer(agent)` | Registers the A2A request handler and the task store of an agent, keyed by the agent name |
| `MapA2AJsonRpc` / `MapA2AHttpJson` | Map the endpoints of one binding for an agent at a path |
| `MapWellKnownAgentCard(card, path)` | Serves the card at `<path>/.well-known/agent-card.json` (from the `A2A.AspNetCore` package) |

## Namespaces Reference

| Namespace | Purpose |
|-----------|---------|
| `OpenAI` | `OpenAIClient`, `OpenAIClientOptions` |
| `OpenAI.Chat` | `ChatClient` |
| `System.ClientModel` | `ApiKeyCredential` |
| `System.ClientModel.Primitives` | `BearerTokenPolicy` |
| `Azure.Identity` | `DefaultAzureCredential` |
| `Microsoft.Agents.AI` | `AIAgent`, `AsAIAgent()` |
| `Microsoft.Extensions.AI` | `IChatClient`, `AITool`, `AIFunctionFactory`, `AsIChatClient()` |
| `Microsoft.Extensions.DependencyInjection` | `AddA2AServer()` |
| `Microsoft.AspNetCore.Builder` | `WebApplication`, `MapA2AJsonRpc()`, `MapA2AHttpJson()` |
| `A2A` | `AgentCard`, `AgentSkill`, `AgentInterface`, `ProtocolBindingNames` |
| `A2A.AspNetCore` | `MapWellKnownAgentCard()` |
| `CommonUtilities` | `ColoredConsole`, `AzureOpenAIEndpoint` |

## Expected Output

```
Endpoint: https://your-resource.openai.azure.com/
Deployment: your-deployment-name
----------------------------------------
=== Scenario 1: A2A server with two agents ===
APIKeySettings:SecretKey is not set: API keys are signed with a random secret, valid until the server stops.
AuthAgent:         http://localhost:5000/a2a/authAgent
  Agent card:      http://localhost:5000/a2a/authAgent/.well-known/agent-card.json
CustomerToneAgent: http://localhost:5000/a2a/customerToneAgent
  Agent card:      http://localhost:5000/a2a/customerToneAgent/.well-known/agent-card.json
Press Ctrl+C to stop the server.
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

The delivered Start project prints the header and the endpoints (without the `APIKeySettings` notice of TODO 6), then exits: the server only listens once TODO 12 is done.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `'AzureOpenAI:Endpoint' is not configured` | Replace the `YOUR-...` placeholders in `appsettings.json` (or use user secrets / environment variables) |
| `'A2AServer:BaseUrl' must be an absolute http:// or https:// URL` | Use for example `http://localhost:5000` |
| `'APIKeySettings:SecretKey' still contains a placeholder` | Remove the value (random secret) or set a real one in user secrets |
| `Failed to bind to address http://127.0.0.1:5000: address already in use` | Another program uses the port (`lsof -i :5000`). Stop it, or run on another port with `A2AServer__BaseUrl=http://localhost:5050 dotnet run` and change the URLs of the client accordingly (`RemoteAgents__AuthAgent__Url`, `RemoteAgents__CustomerToneAgent__Url`) |
| The client gets `403 Forbidden` on port 5000 | On **macOS**, the *AirPlay Receiver* also listens on port 5000 (System Settings → General → AirDrop & Handoff): when the lab server is not running, AirPlay answers instead. Start the server (or turn AirPlay Receiver off) |
| The client gets *"Agent handler did not produce any response events"* | The agent failed on the server: look at the server console. With Azure OpenAI streaming, check that TODO 4 (workaround) is done and that the agents are created from `agentChatClient` |
| `401 Unauthorized` / `403` from Azure OpenAI | API key: check the user secret `AzureOpenAI:APIKey`. Entra ID: run `az login` and check the **Cognitive Services OpenAI User** role |
| `404 DeploymentNotFound` | `ChatDeploymentName` must be the **deployment** name, not the model name |
| A key generated before a restart is "not valid" | Expected with the random secret: set `APIKeySettings:SecretKey` (Step 1) |

## Security

This lab server is for local development:

- it has **no authentication**: anyone who can reach the port can use the agents (and your Azure OpenAI quota). Keep it on `localhost`;
- by default, the A2A tasks and sessions are looked up by `contextId` / `taskId` only. In production, register an agent isolation key provider (for example `builder.Services.UseClaimsBasedAgentIsolation(...)`), as the official sample recommends;
- the signing secret and the Azure OpenAI key stay on the server; the agent card must never contain secrets.

## Provided Files

The following files are provided and should not be modified:

- `A2AServer.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs` - Loads and validates the configuration
- `AzureOpenAISettings.cs`, `A2AServerSettings.cs`, `APIKeySettings.cs` - Settings classes
- `AgentCards.cs` - Agent cards of the two agents
- `Tools/APIKeyTools.cs` - Tools of the AuthAgent
- `StreamingWorkaround.cs` - Temporary workaround (Step 2)

The following files should be modified:

- `appsettings.json` - Update with your Azure OpenAI endpoint and deployment
- `Program.cs` - Complete the TODOs

### NuGet packages

| Package | Why |
|---------|-----|
| `Microsoft.Agents.AI.OpenAI` | Agent Framework + `AsAIAgent()` (brings `OpenAI` and `Microsoft.Extensions.AI`) |
| `Microsoft.Agents.AI.Hosting.A2A.AspNetCore` (preview) | `AddA2AServer`, `MapA2AJsonRpc`, `MapA2AHttpJson`; brings the A2A SDK v1 (`A2A`, `A2A.AspNetCore`) and `Microsoft.Extensions.Configuration.*` |
| `Azure.Identity` | `DefaultAzureCredential` for Entra ID authentication |

## Going Further

- **Multi-turn conversations**: by default the server does not keep the sessions of the agents between requests. Register a session store for an agent, for example `builder.Services.AddKeyedSingleton<AgentSessionStore>("AuthAgent", new InMemoryAgentSessionStore());`, so that the messages that share a `contextId` share the history (see the official sample `A2AClientServer`).
- **Long-running tasks**: `builder.AddA2AServer(agent, options => options.AgentRunMode = ...)` lets the server answer with A2A tasks that the client polls or streams (`A2AServerRegistrationOptions` is still experimental). Client side: samples `A2AAgent_PollingForTaskCompletion`, `A2AAgent_StreamReconnection`.
- **Agents registered with the hosting builder**: `builder.AddAIAgent("name", instructions: ...)` then `agentBuilder.AddA2AServer()` and `app.MapA2AJsonRpc(agentBuilder, path)`.
- **A2A Inspector**: a web tool of the A2A project to browse a card and chat with the agent ([a2a-inspector](https://github.com/a2aproject/a2a-inspector)).

## Coming from an older version of the lab?

| Before (preview) | Now (1.22.0 / A2A v1) |
|------------------|------------------|
| A2A protocol **v0.3** (`A2A` 0.3.3-preview) | A2A protocol **v1** (`A2A` 1.0.0-preview2). **Not compatible**: a v1 client cannot call a v0.3 server (*"'method' field is not a valid A2A method"*). Migrate the client (Lab06_A2AClient) at the same time |
| `app.MapA2A(agent, path, agentCard, taskManager => app.MapWellKnownAgentCard(taskManager, path))` | `builder.AddA2AServer(agent)` + `app.MapA2AJsonRpc(agent, path)` / `app.MapA2AHttpJson(agent, path)` + `app.MapWellKnownAgentCard(card, path)` |
| `AgentCard.Url` | `AgentCard.SupportedInterfaces` (URL + binding + version) |
| Cards at `/a2a/authAgent/v1/card` (and a broken `.well-known` route) | Cards at `/a2a/<agent>/.well-known/agent-card.json` |
| `Microsoft.Agents.Hosting.AspNetCore` 1.4.9-beta, `Microsoft.Extensions.Hosting` | Removed (not needed) |
| `Azure.AI.OpenAI` / `AzureOpenAIClient` | `OpenAI` / `OpenAIClient` + Azure OpenAI v1 endpoint |
| `ChatClientAgent agent = chatClient.CreateAIAgent(...)` | `AIAgent agent = agentChatClient.AsAIAgent(...)` |
| Agent named `APIKeyAgent` behind the `AuthAgent` card | Agent named `AuthAgent`, like its card |
| Signing secret and API key in `appsettings.json` | Optional signing secret and API key in user secrets or environment variables |
| URL `http://localhost:5000` hard-coded in the cards | `A2AServer:BaseUrl` in the configuration |

## Useful Links

- [A2A hosting with Agent Framework](https://learn.microsoft.com/agent-framework/hosting/self-hosting/a2a/dotnet) and [A2A SDK v1 migration guide](https://learn.microsoft.com/agent-framework/migration-guide/agent-to-agent-sdk-v1?pivots=programming-language-csharp)
- [Microsoft Agent Framework documentation](https://learn.microsoft.com/agent-framework/overview/?pivots=programming-language-csharp)
- [Official sample `A2AClientServer`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/05-end-to-end/A2AClientServer) and the [A2A samples](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/A2A)
- [A2A protocol](https://a2a-protocol.org/latest/) and [agent discovery](https://a2a-protocol.org/latest/topics/agent-discovery/)
- [Azure OpenAI v1 API](https://learn.microsoft.com/azure/foundry/openai/api-version-lifecycle)

## Next Step

Continue with **[Lab06_A2AClient](../Lab06_A2AClient/README.md)**: discover these agents and call them.

## Solution

If you get stuck, check the complete solution in the `Solution/` folder.
