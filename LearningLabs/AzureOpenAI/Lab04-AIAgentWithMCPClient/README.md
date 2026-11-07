# Lab 04 - AI Agent With MCP Client

## Objective

In this lab, you will give an AI Agent the tools of a remote **Model Context Protocol (MCP)** server, with **Microsoft Agent Framework**, the official **MCP C# SDK** and **Azure OpenAI**.

MCP is an open standard that defines how applications expose tools and data to language models. An MCP server publishes tools; an MCP **client** (your program) connects to it, lists its tools and calls them. Here, the server is the public **Hugging Face MCP server** (`https://huggingface.co/mcp`): you connect to it, discover its tools, let an agent search the Hugging Face Hub with them and return a typed result, then give the agent only the tool it needs.

The lab has two parts, on the same server and with the same request:

| Part | Scenarios | API of the model | Who connects to the MCP server and runs its tools? |
|------|-----------|------------------|-----------------------------------------------------|
| **A — Chat Completions** | 1 to 3 | `GetChatClient()` | Your application (its `McpClient`) |
| **B — Responses API (bonus)** | 4 | `GetResponsesClient()` | Your application, **still** (the same `McpClient`): local MCP works with both APIs |
| | 5 | `GetResponsesClient()` | **The service** (Azure OpenAI): *hosted MCP*, a capability of the Responses API only |

Part B is a **bonus**, and it is not "the same code with another client": scenario 4 shows what does **not** change (MCP stays orchestrated by your application), scenario 5 shows what really changes when the provider orchestrates MCP for you.

> **Bonus — the Responses API scenarios (4 and 5) are optional.** The lab is **completed** once scenarios 1 to 3 pass. Pass the bonus scenarios too and it is **completed with its bonus** (*Completed+* in the Lab Bench dashboard and for the trainer, +50 XP). A failed bonus scenario never undoes a completed lab, and `dotnet run` leaves a bonus scenario out until you write it. `dotnet run -- --list-scenarios` marks them `[bonus]`.

## What You Will Learn

- How to connect to a remote MCP server with `McpClient.CreateAsync()` and the Streamable HTTP transport (`HttpClientTransport`)
- How to send an access token to an MCP server (`AdditionalHeaders`), and why it is optional here
- How to list the tools of an MCP server (`ListToolsAsync()`) and give them to an agent: an `McpClientTool` **is** an `AIFunction`
- How to configure an agent with `ChatClientAgentOptions` (instructions, tools, `MaxOutputTokens`, `Temperature`)
- How to combine MCP tools and structured output with `RunAsync<T>()`
- How to see which tools the agent called (`FunctionCallContent` in `response.Messages`)
- Why giving an agent fewer tools reduces the token usage
- That the same MCP client and the same tools work on the **Responses API** (`GetResponsesClient().AsAIAgent(options, model: ...)`)
- How to let the **service** connect to an MCP server and run its tools with a hosted tool (`HostedMcpServerTool`, `AllowedTools`, `ApprovalMode`)
- How to tell, in a response, who ran a tool: `FunctionCallContent` (your application) or `McpServerToolCallContent` (the service)
- How to choose between MCP orchestrated by your application and hosted MCP orchestrated by the provider

## Prerequisites

- .NET 10 SDK or later
- An Azure OpenAI (or Microsoft Foundry) resource with a chat model deployment that supports function calling and structured output (for example `gpt-4o-mini` or `gpt-5.4-mini`). Scenarios 4 and 5 also need the **Responses API** for this deployment (the model and the region must support it, see Lab 01 scenario 6)
- One of the following for authentication:
  - the resource **API key**, or
  - Azure CLI logged in (`az login`) with the **Cognitive Services OpenAI User** role on the resource
- Internet access to `https://huggingface.co/mcp`. A Hugging Face account is **optional** (see Step 1)
- Lab 02 (structured output) and Lab 03 (function tools) completed

## Project Structure

```
Lab04-AIAgentWithMCPClient/
├── README.md
├── Start/                          <-- Your working folder
│   ├── Program.cs                  <-- Complete the TODOs here
│   ├── LabPrompts.cs               <-- Provided: name, instructions and question of the agent
│   ├── Models/
│   │   └── HuggingFaceModel.cs     <-- Structured output types (provided)
│   ├── AgentConsole.cs             <-- Display helper (provided)
│   ├── ConfigurationHelper.cs
│   ├── AzureOpenAISettings.cs
│   ├── MCPServerSettings.cs
│   ├── appsettings.json
│   └── AIAgentWithMCPClient.csproj
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
     "MCPServers": {
       "HuggingFace": {
         "Endpoint": "https://huggingface.co/mcp"
       }
     }
   }
   ```

   Use the resource endpoint shown in the Azure portal. The `/openai/v1/` suffix required by the v1 API is added for you.
   `*.openai.azure.com`, `*.cognitiveservices.azure.com` and `*.services.ai.azure.com` endpoints all work.

2. Choose an authentication method for Azure OpenAI:

   - **API key** (simplest for local development) — store it as a user secret, **never** in `appsettings.json`:

     ```bash
     cd Start
     dotnet user-secrets set "AzureOpenAI:APIKey" "<your-api-key>"
     ```

     The user secrets id is shared by all the labs: if you already did it for a previous lab, there is nothing to do.
     You can also use the environment variable `AzureOpenAI__APIKey`.

   - **Microsoft Entra ID** — leave `APIKey` unset and run `az login`. `DefaultAzureCredential` picks up your Azure CLI identity.

3. **Hugging Face access token (optional)**. The Hugging Face MCP server also works **anonymously**, with lower rate limits: the lab runs without a token.
   To use your account (higher limits), create a token at <https://huggingface.co/settings/tokens> and store it as a user secret — it is a secret too:

   ```bash
   cd Start
   dotnet user-secrets set "MCPServers:HuggingFace:BearerToken" "<your-hf-token>"
   ```

   or use the environment variable `MCPServers__HuggingFace__BearerToken`. The program shows `(access token)` or `(anonymous)` next to the server URL.

4. **Or use the dashboard**: the *Azure OpenAI settings* of the optional [Lab Bench dashboard](../../../Dashboard/README.md#azure-openai-settings) write the endpoint, the deployment and the API key to the same user secrets, for every migrated lab. The Hugging Face token is not part of these settings: set it with the command above if you want one.

### Step 2: Look at the provided files

- `LabPrompts.cs` — the name, the instructions and the question of the agent (see Step 3).
- `Models/HuggingFaceModel.cs` — `HuggingFaceSearchResult` (a list of `HuggingFaceModel`: `Name`, `Task`, `Library`, `Link`), the structured output of scenarios 2 and 3. As in Lab 02, the `[Description]` attributes are copied into the JSON schema sent to the model.
- `MCPServerSettings.cs` — the `Endpoint` of an MCP server and its optional `BearerToken`. `ConfigurationHelper.GetMCPServerSettings("HuggingFace")` reads the `MCPServers:HuggingFace` section and checks that the endpoint is an `https://` URL.
- `AgentConsole.cs` — `WriteTokenUsage(response)` displays the token usage of a run (same helper as Lab 03). `WriteToolCalls(response)` (scenarios 4 and 5) displays the tool calls of a run and **who ran them**: a `FunctionCallContent` was run by your application, a `McpServerToolCallContent` by the service; it also shows the tools listed by the service for a hosted MCP server (`mcp_list_tools` item). `Program.cs` imports them with `using static`.

### Step 3: Complete the Program.cs

Open `Start/Program.cs` and complete the TODOs.

The name, the instructions and the question of the agent are **provided** in `Start/LabPrompts.cs`: use its constants (`Agents.HuggingFaceAssistant.Name`, `Agents.HuggingFaceAssistant.Instructions`, `Questions.TextEmbeddingModels`) instead of writing your own texts. Everyone works with the same agent and gets comparable answers, and you can focus on the Agent Framework code.

---

#### Setup: Configuration, Azure OpenAI client and MCP client

TODO 1 to 3 are the same as in Lab 01. The MCP settings are loaded for you (Step 4); TODO 4 opens the connection to the MCP server, used by the three scenarios.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 1** | Create the `OpenAIClientOptions` for the Azure OpenAI v1 endpoint | • `new OpenAIClientOptions { Endpoint = ... }` <br> • `AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint)` (from `CommonUtilities`) returns the `.../openai/v1/` URI |
| **TODO 2** | Create the `OpenAIClient` | • API key: `new OpenAIClient(new ApiKeyCredential(settings.APIKey), clientOptions)` <br> • Entra ID: `new OpenAIClient(new BearerTokenPolicy(new DefaultAzureCredential(), "https://ai.azure.com/.default"), clientOptions)` <br> • Use a conditional on `string.IsNullOrWhiteSpace(settings.APIKey)` to support both |
| **TODO 3** | Get a `ChatClient` for the deployment | • `ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);` |
| **TODO 4** | Connect to the MCP server | • `await using McpClient huggingFaceMcpClient = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions { ... })).WithSpinner("Connecting to the MCP server");` <br> • Options: `Name = "Hugging Face"`, `Endpoint = new Uri(huggingFaceMcpSettings.Endpoint)`, `TransportMode = HttpTransportMode.StreamableHttp` <br> • Token: `AdditionalHeaders = hasToken ? new Dictionary<string, string> { ["Authorization"] = $"Bearer {huggingFaceMcpSettings.BearerToken}" } : null` <br> • `CreateAsync` connects and runs the MCP initialization: a wrong URL or token fails here |

---

#### Scenario 1: Discover the tools of the MCP server

No model is involved here: you talk to the MCP server only.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 5** | Display the server you are connected to | • `ColoredConsole.WritePrimaryLogLine($"Connected to: {huggingFaceMcpClient.ServerInfo.Name} {huggingFaceMcpClient.ServerInfo.Version}");` <br> • `ServerInfo` is sent by the server during the initialization |
| **TODO 6** | List the tools of the server | • `IList<McpClientTool> mcpTools = await huggingFaceMcpClient.ListToolsAsync();` |
| **TODO 7** | Display the number of tools and their names | • `ColoredConsole.WriteSecondaryLogLine($"MCP tools available ({mcpTools.Count}): {string.Join(", ", mcpTools.Select(tool => tool.Name))}");` |

---

#### Scenario 2: Agent with MCP tools and structured output

`McpClientTool` derives from `AIFunction`: an MCP tool is given to an agent exactly like the function tools of Lab 03. When the model asks for it, the agent sends a `tools/call` request to the MCP server and returns the result to the model. This time the agent is configured with `ChatClientAgentOptions`, where the instructions, the tools and the chat options (`MaxOutputTokens`, `Temperature`) live in `ChatOptions`.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 8** | Get the tools of the server | • Same call as TODO 6 |
| **TODO 9** | Create the agent with `ChatClientAgentOptions` | • `AIAgent agent = chatClient.AsAIAgent(new ChatClientAgentOptions { Name = Agents.HuggingFaceAssistant.Name, ChatOptions = new ChatOptions { ... } });` <br> • `Instructions = Agents.HuggingFaceAssistant.Instructions` (search the Hub with the MCP tools, and only return models found by the tools) <br> • `Tools = [.. mcpTools.Cast<AITool>()]` <br> • `MaxOutputTokens = 1000`, `Temperature = 0.2f` (a low temperature makes the tool calls more predictable) |
| **TODO 10** | Run the agent with structured output | • `AgentResponse<HuggingFaceSearchResult> response = await agent.RunAsync<HuggingFaceSearchResult>(Questions.TextEmbeddingModels).WithSpinner("Running agent with MCP tools");` <br> • The agent calls the MCP tools first, then answers with JSON that matches the schema of `HuggingFaceSearchResult` |
| **TODO 11** | Display the structured result | • `ColoredConsole.WritePrimaryLogLine($"Found {response.Result.Models.Count} models:");` <br> • `foreach (HuggingFaceModel model in response.Result.Models)`: `ColoredConsole.WriteSecondaryLogLine($"  Name: {model.Name}");`, same for `Task`, `Library` and `Link`, then `ColoredConsole.WriteEmptyLine();` |
| **TODO 12** | Display the MCP tools the agent called | • `ColoredConsole.WritePrimaryLogLine("MCP tools called by the agent:");` <br> • `foreach (FunctionCallContent call in response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>())` <br> • `string arguments = string.Join(", ", call.Arguments?.Select(argument => $"{argument.Key}: {argument.Value}") ?? []);` <br> • `ColoredConsole.WriteSecondaryLogLine($"- {call.Name}({arguments})");` <br> • `response.Messages` keeps every message of the run: the function calls requested by the model and their results, then the final answer |
| **TODO 13** | Display token usage | • `WriteTokenUsage(response)` |

---

#### Scenario 3: Give the agent only the MCP tools it needs

The definition of **every** tool given to an agent (name, description, JSON schema) is sent to the model with **each** request. An MCP server can expose many tools with long descriptions: you pay for them even when they are not used, and each tool is something the agent is allowed to do. Here you keep only the search tool and run the same request as in scenario 2.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 14** | Keep only `hub_repo_search` and display the tools kept | • `IList<McpClientTool> mcpTools = await huggingFaceMcpClient.ListToolsAsync();` <br> • `List<AITool> selectedTools = [.. mcpTools.Where(tool => tool.Name == "hub_repo_search")];` <br> • `ColoredConsole.WriteSecondaryLogLine($"Tools given to the agent: {string.Join(", ", selectedTools.Select(tool => tool.Name))}");` |
| **TODO 15** | Create the same agent with the selected tools | • Same code as TODO 9, with `Tools = selectedTools` |
| **TODO 16** | Run the same request | • Same code as TODO 10, with `.WithSpinner("Running agent with the selected MCP tool")` |
| **TODO 17** | Display the names of the models found | • `ColoredConsole.WritePrimaryLogLine($"Found {response.Result.Models.Count} models: {string.Join(", ", response.Result.Models.Select(model => model.Name))}");` |
| **TODO 18** | Display token usage | • `WriteTokenUsage(response)` <br> • Compare the input tokens with scenario 2 |

---

#### Scenario 4 (bonus): Responses API — the same MCP client, the tools still run in your application

The agent of scenario 3, on the Responses API. Nothing changes on the MCP side: the `McpClient`, `ListToolsAsync()` and the `McpClientTool` objects are the same, because MCP tools are given to the agent as `AIFunction`s, whatever the API of the model. What changes is the protocol between the agent and the model: the model asks for the tool with a `function_call` **item**, the agent runs it from your process and sends a `function_call_output` item. With the default `store = true`, the service keeps the response, and the second request of the tool loop only sends the tool result and the id of the previous response (`previous_response_id`) instead of the whole conversation.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 19** | Get the tools and keep `hub_repo_search` | • Same code as TODO 14 <br> • `ColoredConsole.WriteSecondaryLogLine($"Tools given to the agent: {string.Join(", ", selectedTools.Select(tool => tool.Name))} (MCP client of this application)");` |
| **TODO 20** | Create the agent of scenario 3 on the Responses API | • `AIAgent agent = client.GetResponsesClient().AsAIAgent(new ChatClientAgentOptions { ... }, model: settings.ChatDeploymentName);` with the same `Name` and `ChatOptions` as TODO 15 <br> • With Responses, the deployment is the `model` of each request (with Chat Completions, it is chosen by `GetChatClient(...)`) <br> • `GetResponsesClient()` is flagged experimental (`OPENAI001`): keep the `#pragma` |
| **TODO 21** | Run the same request | • `AgentResponse<HuggingFaceSearchResult> response = await agent.RunAsync<HuggingFaceSearchResult>(Questions.TextEmbeddingModels).WithSpinner("Running agent on the Responses API with the local MCP tool");` <br> • Structured output and tools work together on Responses too |
| **TODO 22** | Display the models and the tool calls | • Same code as TODO 17 <br> • `WriteToolCalls(response);` shows `hub_repo_search(...) - run by this application, through its MCP client` |
| **TODO 23** | Display the response id and the usage | • `ColoredConsole.WriteSecondaryLogLine($"Response id: {response.ResponseId}");` (`resp_...`) <br> • `WriteTokenUsage(response);` — close to scenario 3: the same tool definition and the same tool result reach the model |

---

#### Scenario 5 (bonus): Hosted MCP — the service connects to the MCP server and runs its tools

With a **hosted tool**, you do not run the tool: you describe it, and the service runs it. `HostedMcpServerTool` gives the address of the MCP server to the Responses API: during the request, the service lists the tools of the server (`mcp_list_tools`), the model chooses one, **the service** calls it (`mcp_call`) and gives the result to the model, then the model answers. Your application makes **one** request and never talks to Hugging Face: there is no `McpClient` in this scenario.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 24** | Describe the MCP server as a hosted tool | • `HostedMcpServerTool hostedMcpServer = new(serverName: "huggingface", serverAddress: huggingFaceMcpSettings.Endpoint) { ... };` <br> • `AllowedTools = ["hub_repo_search"]`: the tools the model may use (the same choice as scenario 3, and the same token savings) <br> • `ApprovalMode = HostedMcpServerToolApprovalMode.NeverRequire`: the service calls the tool without asking you first. **The default asks for an approval** (see [Approve the calls of a hosted MCP server](#approve-the-calls-of-a-hosted-mcp-server)) <br> • `Headers = hasToken ? new Dictionary<string, string> { ["Authorization"] = $"Bearer {huggingFaceMcpSettings.BearerToken}" } : null`: the service sends them to the MCP server, so the token is given to the service <br> • Display it: `ColoredConsole.WriteSecondaryLogLine($"Hosted MCP server given to the agent: {hostedMcpServer.ServerName} ({hostedMcpServer.ServerAddress}), allowed tools: {string.Join(", ", hostedMcpServer.AllowedTools)}");` |
| **TODO 25** | Create the agent with the hosted tool | • Same code as TODO 20, with `Tools = [hostedMcpServer]` |
| **TODO 26** | Run the same request | • `.WithSpinner("Running agent with the hosted MCP server")` |
| **TODO 27** | Display the models, the tool calls and the usage | • `WriteToolCalls(response);` now shows `MCP tools listed by the service (huggingface): hub_repo_search` and `huggingface/hub_repo_search(...) - run by the service (hosted MCP)` <br> • The tool results are in `McpServerToolResultContent`, the calls in `McpServerToolCallContent`: they are not `FunctionCallContent`, because your agent did not run them <br> • `WriteTokenUsage(response);` |

> **Beware with Chat Completions:** an agent on `GetChatClient()` does not refuse a hosted tool, it **silently leaves it out** of the request. The model then answers from its own knowledge (with models that may not exist), and no error tells you why. Hosted tools need the Responses API.

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

To run only some scenarios: `dotnet run -- --scenarios 1` ([Choose the scenarios to run](../../../README.md#3-choose-the-scenarios-to-run)).

## How an agent uses MCP tools

1. **Connection** — `McpClient.CreateAsync()` opens an MCP session with the server (Streamable HTTP: JSON-RPC messages over HTTP `POST`) and exchanges the capabilities and the server information (`initialize`).
2. **Discovery** — `ListToolsAsync()` sends `tools/list`: the server returns the name, the description and the JSON schema of the parameters of each tool. The SDK wraps each tool in an `McpClientTool`, which is an `AIFunction`.
3. **Function calling** — the agent sends these definitions to the model with your prompt, like the function tools of Lab 03. The model answers with function calls.
4. **Execution** — for each call, the `McpClientTool` sends `tools/call` to the MCP server **from your program** and gives the result back to the model. Steps 3–4 repeat until the model answers.
5. **Answer** — with `RunAsync<T>()`, the final answer is JSON that matches the schema of `T`, deserialized in `response.Result`.

The model never talks to the MCP server: your agent does. This is a **local** MCP client (the tools run from your process), and it works the same on Chat Completions (scenarios 2, 3) and on the Responses API (scenario 4).

## Local MCP or hosted MCP?

With **hosted MCP** (scenario 5), steps 1 to 4 move to the service: it connects to the server, lists the tools, calls them and gives the results to the model, inside **one** request of your application.

```
Local MCP (scenarios 2 to 4)                         Hosted MCP (scenario 5)

 your app ──tools/list, tools/call──► MCP server      your app ──1 request──► Azure OpenAI ──tools/list, tools/call──► MCP server
    │  ▲                                                   ▲                       │
    ▼  │ function_call / function_call_output              └────── answer ─────────┘
 Azure OpenAI (Chat Completions or Responses)
```

| | MCP orchestrated by your application (`McpClient`) | Hosted MCP (`HostedMcpServerTool`) |
|---|---|---|
| APIs | Chat Completions **and** Responses | Responses only (silently ignored by Chat Completions) |
| Who calls the MCP server | Your process | The service (Azure OpenAI / OpenAI) |
| Where the server must be | Anywhere your application reaches: localhost, private network, a local process (`stdio`) | **Publicly reachable from the service**, over HTTPS (TLS 1.2+; on Azure, no mTLS and no service tags for this traffic) |
| Credentials of the server | Stay in your application | Sent to the service with each request (`Headers`), which forwards them |
| Control of each call | Full: the function calling middleware (Lab 03), approval (Lab 09), logs, retries, timeouts, filtering of the results | What the service offers: `AllowedTools`, `ApprovalMode` (approve or reject before the call) |
| Round trips of your application | One model call per step of the tool loop | One request for the whole run |
| Tool results | In your process before going to the model: you can check, trim or redact them | Go straight from the server to the model; you see them afterwards (`McpServerToolResultContent`) |
| Observability | Your traces (OpenTelemetry, Lab 11) | The items of the response (`mcp_list_tools`, `mcp_call`) |
| Dependency on the provider | None: change model provider, keep your MCP code | Tied to the providers that host MCP calls |

When to use which:

- **Local MCP** when the server is internal or local, when the credentials must not leave your infrastructure, or when you need to control, log or transform each call. It is the default choice, and it does not depend on the API of the model.
- **Hosted MCP** for public MCP servers when you want fewer round trips and less code (no MCP client, no tool loop in your process), and accept that the provider sees the calls and their results.

Token usage: in a run of this lab, scenario 5 used fewer input tokens than scenario 4 (about 1,350 against 1,850) for the same answer, because the service formats the tool list and the result itself. Measure it for your own server: it depends on the size of the tool definitions and of the results, not on the kind of MCP.

### Approve the calls of a hosted MCP server

By default (`ApprovalMode` not set, or `HostedMcpServerToolApprovalMode.AlwaysRequire`), the service does **not** call the tool: it stops and returns a `ToolApprovalRequestContent` whose `ToolCall` is the `McpServerToolCallContent` it wants to run. You answer in the same session:

```csharp
AgentSession session = await agent.CreateSessionAsync();
AgentResponse response = await agent.RunAsync(Questions.TextEmbeddingModels, session);
foreach (ToolApprovalRequestContent request in response.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>())
{
    var call = (McpServerToolCallContent)request.ToolCall;   // ServerName, Name, Arguments
    bool approved = call.Name == "hub_repo_search";            // your policy, or ask a human (Lab 09)
    // Microsoft.Extensions.AI.ChatMessage: OpenAI.Chat (imported by this lab) has a ChatMessage too
    response = await agent.RunAsync(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, [request.CreateResponse(approved)]), session);
}
```

`HostedMcpServerToolApprovalMode.RequireSpecific(alwaysRequireApprovalToolNames, neverRequireApprovalToolNames)` sets it tool by tool. Same flow as the official sample `ResponseAgent_Hosted_MCP`.

## Key Concepts

| Concept | Description |
|---------|-------------|
| MCP server | A program or service that exposes tools (and resources, prompts) with the Model Context Protocol |
| `McpClient` | The MCP client of the official C# SDK. `McpClient.CreateAsync(transport)` connects and initializes the session; dispose it (`await using`) to close the session |
| `HttpClientTransport` / `HttpClientTransportOptions` | Transport to a remote server: `Endpoint`, `TransportMode`, `Name` (for logs), `AdditionalHeaders` (sent with every request) |
| `HttpTransportMode.StreamableHttp` | The HTTP transport of the current MCP specification. `AutoDetect` (default) tries it first, then falls back to the older SSE transport |
| `ServerInfo` | Name and version of the server, received during the initialization |
| `ListToolsAsync()` | Returns the tools of the server as `IList<McpClientTool>` |
| `McpClientTool` | An MCP tool, as an `AIFunction`: give it to an agent like any function tool |
| `ChatClientAgentOptions` | Complete agent configuration: `Name`, `Description`, `ChatOptions` (`Instructions`, `Tools`, `MaxOutputTokens`, `Temperature`...) |
| `RunAsync<T>()` / `AgentResponse<T>` | Run with structured output; `Result` is the deserialized `T` (Lab 02) |
| `FunctionCallContent` | A function call requested by the model (`Name`, `Arguments`), kept in `response.Messages`; run by your application |
| `GetResponsesClient()` / `ResponsesClient` | Client of the **Responses API**; `AsAIAgent(options, model: deployment)` creates the same kind of `ChatClientAgent` on it |
| Hosted tool | A tool described to the service and **run by the service** (Responses API only): hosted MCP, file search (Lab 07), code interpreter, web search |
| `HostedMcpServerTool` | Hosted MCP server: `ServerName`, `ServerAddress`, `AllowedTools`, `ApprovalMode`, `Headers` |
| `HostedMcpServerToolApprovalMode` | `AlwaysRequire` (the default of the service), `NeverRequire`, `RequireSpecific(...)` |
| `McpServerToolCallContent` / `McpServerToolResultContent` | A call to a hosted MCP server (`ServerName`, `Name`, `Arguments`) and its result (`Outputs`), run by the service |
| `ToolApprovalRequestContent` | The service asks your approval before calling a hosted MCP tool; answer with `CreateResponse(approved)` |

## Namespaces Reference

| Namespace | Purpose |
|-----------|---------|
| `OpenAI` | `OpenAIClient`, `OpenAIClientOptions` |
| `OpenAI.Chat` | `ChatClient` and the `AsAIAgent()` extension method |
| `OpenAI.Responses` | `ResponsesClient` and its `AsAIAgent(options, model: ...)` extension method (scenarios 4 and 5) |
| `System.ClientModel` | `ApiKeyCredential` |
| `System.ClientModel.Primitives` | `BearerTokenPolicy` |
| `Azure.Identity` | `DefaultAzureCredential` |
| `Microsoft.Agents.AI` | `AIAgent`, `ChatClientAgentOptions`, `AgentResponse`, `AgentResponse<T>` |
| `Microsoft.Extensions.AI` | `AITool`, `ChatOptions`, `FunctionCallContent`, `HostedMcpServerTool`, `HostedMcpServerToolApprovalMode`, `McpServerToolCallContent` |
| `ModelContextProtocol.Client` | `McpClient`, `HttpClientTransport`, `HttpClientTransportOptions`, `HttpTransportMode`, `McpClientTool` |
| `System.ComponentModel` | `[Description]` (in `Models/`) |
| `CommonUtilities` | `ColoredConsole`, `WithSpinner()`, `AzureOpenAIEndpoint` |

## Expected Output

```
Endpoint: https://your-resource.openai.azure.com/
Deployment: your-deployment-name
MCP Server: https://huggingface.co/mcp (anonymous)
----------------------------------------
=== Scenario 1: Discover the tools of the MCP server ===
Connected to: huggingface.co/mcp 0.4.23
MCP tools available (4): hf_whoami, hub_repo_search, hub_repo_details, hf_fs
----------------------------------------
=== Scenario 2: Agent with MCP tools and structured output ===
Found 4 models:
  Name: jinaai/jina-embeddings-v5-text-nano
  Task: feature-extraction
  Library: transformers
  Link: https://hf.co/jinaai/jina-embeddings-v5-text-nano
  ...

MCP tools called by the agent:
- hub_repo_search(query: text embedding, repo_types: ["model"], limit: 4)
----------------------------------------
Token Usage:
  Input tokens: 4435
  Output tokens: 240
  Total tokens: 4675
----------------------------------------
=== Scenario 3: Give the agent only the MCP tools it needs ===
Tools given to the agent: hub_repo_search
Found 4 models: jinaai/jina-embeddings-v5-text-nano-classification, trapoom555/MiniCPM-2B-Text-Embedding-cft, ...
----------------------------------------
Token Usage:
  Input tokens: 1927
  Output tokens: 240
  Total tokens: 2167
----------------------------------------
=== Scenario 4: Responses API - the same MCP client, the tools still run in your application ===
Tools given to the agent: hub_repo_search (MCP client of this application)
Found 4 models: jinaai/jina-embeddings-v5-text-nano, jinaai/jina-embeddings-v5-text-small, ...
Tool calls of the run:
- hub_repo_search(query: text embedding, repo_types: ["model"], limit: 4) - run by this application, through its MCP client
Response id: resp_0b7f4dde665b923f006aef3744b20c8195a4529aaddfe327ac
----------------------------------------
Token Usage:
  Input tokens: 1847
  Output tokens: 262
  Total tokens: 2109
----------------------------------------
=== Scenario 5: Hosted MCP - the service connects to the MCP server and runs its tools ===
Hosted MCP server given to the agent: huggingface (https://huggingface.co/mcp), allowed tools: hub_repo_search
Found 4 models: jinaai/jina-embeddings-v5-text-nano, jinaai/jina-embeddings-v5-text-small, ...
Tool calls of the run:
- MCP tools listed by the service (huggingface): hub_repo_search
- huggingface/hub_repo_search(query: text embedding, repo_types: ["model"], author: , filters: [], sort: trendingScore, limit: 4) - run by the service (hosted MCP)
----------------------------------------
Token Usage:
  Input tokens: 1344
  Output tokens: 277
  Total tokens: 1621
```

The models found, the server version and the list of tools depend on the Hugging Face Hub and server at the time you run the lab (a token can unlock more tools). The input tokens of scenario 3 are much lower than in scenario 2 for the same answer: the difference is the definitions of the tools that were not needed.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `'AzureOpenAI:Endpoint' is not configured` | Replace the `YOUR-...` placeholders in `appsettings.json` (or use user secrets / environment variables) |
| `401 Unauthorized` / `403` from Azure OpenAI | API key: check the user secret `AzureOpenAI:APIKey`. Entra ID: run `az login` and check the **Cognitive Services OpenAI User** role |
| `404 DeploymentNotFound` | `ChatDeploymentName` must be the **deployment** name, not the model name |
| `'MCPServers:HuggingFace:Endpoint' must be an absolute https:// URL` | Use `https://huggingface.co/mcp` |
| `'MCPServers:HuggingFace:BearerToken' still contains a placeholder` | Remove the value to connect anonymously, or set a real token in user secrets |
| `HttpRequestException ... 401 (Unauthorized)` while connecting to the MCP server | The Hugging Face token is invalid or expired: fix it, or remove it to connect anonymously |
| `HttpRequestException ... 404 (Not Found)` while connecting | Wrong MCP endpoint URL |
| `429 Too Many Requests` from the MCP server | Anonymous rate limit reached: wait, or use a Hugging Face token |
| Scenario 3: `Tools given to the agent:` is empty | The server renamed its tools: run scenario 1 and use the name of the search tool |
| `JsonException` when reading `response.Result` | The answer was cut: increase `MaxOutputTokens`, or ask for fewer models |
| The agent answers from its own knowledge (no line under `MCP tools called by the agent:`) | Check `Tools` in `ChatOptions` and the instructions; the model decides from the tool names and descriptions |
| Scenarios 4 and 5 fail with `404` or *"unsupported"* | The deployment (model or region) does not support the Responses API: use one that does, or run `dotnet run -- --scenarios 1,2,3` |
| Scenario 5: no model found, `(no tool called)`, or an answer with models that do not exist | The agent runs on Chat Completions (`GetChatClient()`): a hosted tool is silently left out. Use `GetResponsesClient()` |
| Scenario 5: no answer, the response only holds a `ToolApprovalRequestContent` | `ApprovalMode` is not set: the service waits for your approval. Set `NeverRequire`, or approve the call ([Approve the calls](#approve-the-calls-of-a-hosted-mcp-server)) |
| Scenario 5: an error from the service about the MCP server (it could not list or call its tools) | The **service** could not reach the server: it must be public, over HTTPS. A `localhost` or private server works with a local MCP client only (scenario 4) |

## Security: third-party MCP servers

Remote MCP servers are third-party services: your prompts and the tool arguments chosen by the model are sent to them, and their answers go back to the model. As recommended in [Using MCP tools](https://learn.microsoft.com/agent-framework/agents/tools/local-mcp-tools?pivots=programming-language-csharp):

- use servers hosted by the service provider itself (here, Hugging Face), not proxies, and review what you connect to;
- keep tokens out of `appsettings.json` and of the logs; send them only to the server they belong to;
- give an agent only the tools it needs (scenario 3, and `AllowedTools` in scenario 5).

With **hosted MCP**, add the provider to this list: the service sees your tool arguments and their results, and receives the credentials of the MCP server in `Headers`. Approve the calls (`AlwaysRequire` / `RequireSpecific`) for servers that can act on your data, and use a token with the smallest scope.

## Provided Files

The following files are provided and should not be modified:

- `LabPrompts.cs` - Name, instructions and question of the agent, the same for everyone
- `AIAgentWithMCPClient.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs` - Loads and validates the configuration
- `AzureOpenAISettings.cs` - Settings class for Azure OpenAI
- `MCPServerSettings.cs` - Settings class for MCP servers
- `Models/HuggingFaceModel.cs` - Structured output types, with their `[Description]` attributes
- `AgentConsole.cs` - Display helper

The following files should be modified:

- `appsettings.json` - Update with your Azure OpenAI endpoint and deployment
- `Program.cs` - Complete the TODOs

### NuGet packages

| Package | Why |
|---------|-----|
| `Microsoft.Agents.AI.OpenAI` | Agent Framework + `AsAIAgent()` for OpenAI clients (brings `OpenAI` and `Microsoft.Extensions.AI`) |
| `ModelContextProtocol` | The official MCP C# SDK (`McpClient`, `HttpClientTransport`, `McpClientTool`) |
| `Azure.Identity` | `DefaultAzureCredential` for Entra ID authentication |
| `Microsoft.Extensions.Configuration.*` | `appsettings.json`, user secrets and environment variables, bound to the settings classes |

## Going Further

- **Local MCP servers (stdio)**: `new StdioClientTransport(new() { Name = ..., Command = "npx", Arguments = [...] })` starts a server as a child process. See the official sample `Agent_MCP_Server`.
- **Servers protected with OAuth**: `HttpClientTransportOptions.OAuth` runs the authorization flow for you (official sample `Agent_MCP_Server_Auth`).
- **Per-run tokens**: a custom `HttpClient` with a `DelegatingHandler` can attach a fresh token to each request, only for the server's origin (official sample `Agent_MCP_PerRun_AuthHeaders`).
- **Trace or wrap MCP tool calls**: the function calling middleware of Lab 03 works with MCP tools too; a `DelegatingAIFunction` can also wrap each `McpClientTool` (official sample `Agent_Step23_LocalMCP`, Foundry).
- **Hosted MCP with approvals**: scenario 5 with `ApprovalMode` left to its default, and the approval loop of [Approve the calls](#approve-the-calls-of-a-hosted-mcp-server) (official sample `ResponseAgent_Hosted_MCP`).
- **Chat Completions or Responses for the whole course**: [Chat Completions and Responses API](../ChatCompletions-vs-Responses.md) compares the two APIs in Labs 04, 05 and 07.
- **Long-running MCP tools** (MCP Tasks extension): official sample `Agent_MCP_LongRunningTask_Client`.
- **Expose your own agent as an MCP server**: this is the topic of Lab 10.

## Coming from an older version of the lab?

| Before (preview) | Now (1.27.0) |
|------------------|--------------|
| `ModelContextProtocol` 0.5.0-preview.1 | `ModelContextProtocol` 2.2.0 (stable). `McpClient.CreateAsync`, `HttpClientTransport` and `ListToolsAsync` are unchanged |
| `Azure.AI.OpenAI` / `AzureOpenAIClient` | `OpenAI` / `OpenAIClient` + Azure OpenAI v1 endpoint |
| `ChatClientAgent agent = chatClient.CreateAIAgent(instructions, tools, clientFactory: c => new ConfigureOptionsChatClient(c, o => { o.MaxOutputTokens = ...; o.Temperature = ...; }))` | `AIAgent agent = chatClient.AsAIAgent(new ChatClientAgentOptions { ChatOptions = new ChatOptions { Instructions, Tools, MaxOutputTokens, Temperature } })` |
| `AgentRunResponse<T>` | `AgentResponse<T>` |
| Hugging Face token in `appsettings.json` (required, `Bearer ` header always sent) | Optional token in user secrets or environment variables; header sent only when a token is set |
| API key in `appsettings.json` | API key in user secrets or environment variables |
| One scenario | Scenario 1: discover the tools; scenario 2: the original agent + the list of tools called; scenario 3: only the tools needed |
| — | Scenarios 4 and 5 (2026-11): the same MCP client on the Responses API, then hosted MCP (`HostedMcpServerTool`) |

## Useful Links

- [Using MCP tools with Agent Framework](https://learn.microsoft.com/agent-framework/agents/tools/local-mcp-tools?pivots=programming-language-csharp) and [hosted MCP tools](https://learn.microsoft.com/agent-framework/agents/tools/hosted-mcp-tools)
- [Official sample `ResponseAgent_Hosted_MCP`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/ModelContextProtocol/ResponseAgent_Hosted_MCP)
- [Azure OpenAI Responses API — remote MCP servers](https://learn.microsoft.com/azure/ai-foundry/openai/how-to/responses)
- [Azure OpenAI provider in Agent Framework (Responses or Chat Completions, hosted tools)](https://learn.microsoft.com/agent-framework/agents/providers/azure-openai)
- [Microsoft Agent Framework documentation](https://learn.microsoft.com/agent-framework/overview/?pivots=programming-language-csharp)
- [Official sample `Agent_MCP_Server`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/ModelContextProtocol/Agent_MCP_Server) and the other [MCP samples](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/ModelContextProtocol)
- [Official MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
- [Model Context Protocol](https://modelcontextprotocol.io/introduction) and its [security best practices](https://modelcontextprotocol.io/specification/draft/basic/security_best_practices)
- [Hugging Face MCP server](https://huggingface.co/settings/mcp)
- [Azure OpenAI v1 API](https://learn.microsoft.com/azure/foundry/openai/api-version-lifecycle)

## Solution

If you get stuck, check the complete solution in the `Solution/` folder.
