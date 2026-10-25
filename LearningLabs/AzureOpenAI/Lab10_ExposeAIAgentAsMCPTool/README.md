# Lab 10 - Expose an AI Agent as an MCP Tool

## Objective

In this lab, you will expose an AI agent built with **Microsoft Agent Framework** and **Azure OpenAI** as a tool of an **MCP (Model Context Protocol) server**, so that other applications and other agents can use it through MCP.

The agent, `CompanyAssistant`, has its own function tools (employees and meeting rooms, as in Lab 03). You turn it into a function, wrap the function in an MCP tool, and serve it with the two transports of MCP:

- **Scenario 1 — remote server, Streamable HTTP**: an ASP.NET Core application serves the tool on `http://localhost:5100/mcp/companyassistant`; the program connects to it as an MCP client would (MCP Inspector, VS Code), lists the tools and calls the agent.
- **Scenario 2 — local server, stdio**: an MCP host starts the server as a child process and talks to it on stdin/stdout. The program plays the host: it starts **itself** with `--stdio`.
- **Scenario 3 — an agent uses your agent**: another agent, the `OfficeAssistant`, receives the MCP tool and delegates a booking to the `CompanyAssistant` through MCP.

## What You Will Learn

- How to turn an agent into a function with `AsAIFunction()`: its name, its description and its `query` parameter
- How to wrap the function in an MCP tool with `McpServerTool.Create()`
- How to serve the tool over **Streamable HTTP** with ASP.NET Core: `AddMcpServer()`, `WithHttpTransport()`, `WithTools()`, `MapMcp(path)`
- How to serve the tool over **stdio** with `Host.CreateEmptyApplicationBuilder()` and `WithStdioServerTransport()`, and why nothing else may be written to stdout
- How an MCP client sees the agent (`ListToolsAsync`, `CallToolAsync`), and how another agent uses it as a tool
- Why each call runs the agent in a new session, and whose tokens a call consumes

## Prerequisites

- .NET 10 SDK or later
- An Azure OpenAI (or Microsoft Foundry) resource with a chat model deployment that supports function calling (for example `gpt-4o-mini` or `gpt-5.4-mini`)
- One of the following for authentication:
  - the resource **API key**, or
  - Azure CLI logged in (`az login`) with the **Cognitive Services OpenAI User** role on the resource
- Lab 03 completed (function tools) and Lab 04 recommended (MCP client)
- Optional, to try the server from other MCP hosts (Step 5): [Node.js](https://nodejs.org/) for MCP Inspector, or VS Code with GitHub Copilot

## Project Structure

```
Lab10_ExposeAIAgentAsMCPTool/
├── README.md
├── Start/                          <-- Your working folder
│   ├── Program.cs                  <-- Complete the TODOs here
│   ├── Tools/
│   │   └── CompanyTools.cs         <-- The tools of the CompanyAssistant agent (provided)
│   ├── McpConsole.cs               <-- Display helpers (provided)
│   ├── McpServerSettings.cs        <-- Address of the HTTP MCP server
│   ├── ConfigurationHelper.cs
│   ├── AzureOpenAISettings.cs
│   ├── appsettings.json
│   └── ExposeAIAgentAsMCPTool.csproj
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
     "McpServer": {
       "BaseUrl": "http://localhost:5100"
     },
     "Logging": {
       "LogLevel": {
         "Default": "Warning"
       }
     }
   }
   ```

   Use the resource endpoint shown in the Azure portal. The `/openai/v1/` suffix required by the v1 API is added for you.
   `*.openai.azure.com`, `*.cognitiveservices.azure.com` and `*.services.ai.azure.com` endpoints all work.
   `McpServer:BaseUrl` is the address of the HTTP MCP server of scenario 1 (change the port if `5100` is already used; avoid `5000`, used by the AirPlay Receiver on macOS).
   `Logging` keeps the logs of ASP.NET Core and of the MCP server quiet (warnings and errors only).

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

The stdio server of scenarios 2 and 3 is a second process of the same program: it reads the same `appsettings.json`, user secrets and environment variables.

### Step 2: Look at the provided files

- `Tools/CompanyTools.cs` — the company data (5 employees, 4 meeting rooms, 2 bookings) and the tools of the `CompanyAssistant` agent, with their `[Description]` attributes: `GetEmployeeInfo`, `GetMeetingRooms` and `BookMeetingRoom` (public), plus private helpers that are not given to the agent. Same file as Lab 03.
- `McpConsole.cs` — display helpers, imported with `using static` so you can call them directly:
  - `WriteTools(tools)` shows what an MCP client sees for each tool: its name, its description and the JSON schema of its input;
  - `WriteToolResult(result)` shows the text returned by an MCP `tools/call` (or the error the tool reported);
  - `WriteToolCalls(response)` shows the tools an agent called during a run, with what they returned (`FunctionCallContent` / `FunctionResultContent` with the same `CallId`);
  - `WriteTokenUsage(response)` shows the token usage of a run.
- `McpServerSettings.cs` — `BaseUrl`, read and validated by `ConfigurationHelper.GetMcpServerSettings()`.

### Step 3: Complete the Program.cs

Open `Start/Program.cs` and complete the TODOs.

The program reads two optional command-line switches, already in place at the top of `Program.cs`:

- `--stdio`: the program is **only** the stdio MCP server (TODO 7 and 8). Scenarios 2 and 3 start it that way, and so do MCP Inspector or VS Code. In this mode, `Console.SetOut(Console.Error)` sends everything the lab prints to stderr: **stdout carries the MCP messages and nothing else**.
- `--http`: the program runs scenario 1, then keeps the HTTP server running until `Ctrl+C` (TODO 12).

Without switch, it runs the scenarios selected in `scenariosToRun`.

---

#### Setup: Configuration and Client Initialization

Same as Lab 01.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 1** | Create the `OpenAIClientOptions` for the Azure OpenAI v1 endpoint | • `OpenAIClientOptions clientOptions = new() { Endpoint = AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint) };` (`AzureOpenAIEndpoint` comes from `CommonUtilities`) |
| **TODO 2** | Create the `OpenAIClient` | • `OpenAIClient client = !string.IsNullOrWhiteSpace(settings.APIKey) ? new OpenAIClient(new ApiKeyCredential(settings.APIKey), clientOptions) : new OpenAIClient(new BearerTokenPolicy(new DefaultAzureCredential(), "https://ai.azure.com/.default"), clientOptions);` <br> • Keep the `#pragma warning disable/restore OPENAI001` around it |
| **TODO 3** | Get a `ChatClient` for the deployment | • `ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);` |

---

#### The CompanyAssistant agent and its MCP tool

Used by every scenario and by the stdio server.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 4** | Create the `CompanyAssistant` agent with the company tools | • `var companyTools = new CompanyTools();` <br> • `AIAgent companyAssistant = chatClient.AsAIAgent(instructions: "You are a helpful assistant that can help with company tasks. Answer with the facts returned by the tools.", name: "CompanyAssistant", description: "Answers questions about the employees and the meeting rooms of the company, and books meeting rooms.", tools: [AIFunctionFactory.Create(companyTools.GetEmployeeInfo, "get_employee_info"), AIFunctionFactory.Create(companyTools.GetMeetingRooms, "get_meeting_rooms"), AIFunctionFactory.Create(companyTools.BookMeetingRoom, "book_meeting_room")]);` <br> • Same as Lab 03, plus a **description**: the name and the description of the agent become those of the MCP tool |
| **TODO 5** | Turn the agent into a function | • `AIFunction companyAssistantFunction = companyAssistant.AsAIFunction();` <br> • The function has one string parameter, `query`; invoking it runs the agent and returns the text of its answer <br> • No session is given: every call runs the agent in a **new session** |
| **TODO 6** | Wrap the function in an MCP server tool | • `McpServerTool companyAssistantTool = McpServerTool.Create(companyAssistantFunction);` |

---

#### Stdio server mode (`--stdio`)

The server side of scenarios 2 and 3: an MCP host starts the program with `--stdio` and talks to it on stdin/stdout.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 7** | Create an empty host and register the MCP server with the stdio transport | • `HostApplicationBuilder stdioBuilder = Host.CreateEmptyApplicationBuilder(settings: null);` <br> • `stdioBuilder.Services.AddMcpServer().WithStdioServerTransport().WithTools([companyAssistantTool]);` <br> • An *empty* builder has no configuration source and no logging provider: nothing it does reaches stdout |
| **TODO 8** | Run the server until the host closes stdin | • `ColoredConsole.WriteSecondaryLogLine("CompanyAssistant MCP server ready on stdio.");` (it goes to stderr, see `--stdio` above) <br> • `await stdioBuilder.Build().RunAsync();` |

---

#### Scenario 1: Expose the agent as a remote MCP tool (Streamable HTTP)

The program starts an ASP.NET Core MCP server, then calls it as an MCP client: it lists the tools and asks the agent for the employee `EMP003`.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 9** | Create an ASP.NET Core application and register the MCP server with the HTTP transport | • `WebApplicationBuilder builder = WebApplication.CreateBuilder(args);` <br> • `builder.Services.AddMcpServer().WithHttpTransport().WithTools([companyAssistantTool]);` |
| **TODO 10** | Build the application, map the MCP endpoint and start the server without blocking | • `WebApplication app = builder.Build();` <br> • `app.MapMcp(McpPath);` — the MCP endpoint is `http://localhost:5100/mcp/companyassistant` <br> • `app.Urls.Add(serverSettings.BaseUrl);` <br> • `await app.StartAsync();` — unlike `Run()`, it returns once the server listens, so the program can call it <br> • `ColoredConsole.WritePrimaryLogLine($"MCP server listening on {mcpUrl}");` |
| **TODO 11** | Be an MCP client of the server: connect, list the tools, call the tool | • **11.1** connect with the Streamable HTTP transport (same as Lab 04): <br> `await using (McpClient mcpClient = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions { Name = "CompanyAssistant over HTTP", Endpoint = new Uri(mcpUrl), TransportMode = HttpTransportMode.StreamableHttp }))) { ... }` <br> • **11.2** in the block, list the tools and call the agent: <br> `IList<McpClientTool> mcpTools = await mcpClient.ListToolsAsync();` <br> `WriteTools(mcpTools);` <br> `CallToolResult result = await mcpClient.CallToolAsync("CompanyAssistant", new Dictionary<string, object?> { ["query"] = query }).AsTask().WithSpinner("Waiting for the MCP tool");` <br> `WriteToolResult(result);` <br> • The block closes the client before the server stops |
| **TODO 12** | Keep the server running with `--http`, otherwise stop it | • `if (httpServerMode) { ColoredConsole.WriteSecondaryLogLine("Server still running for other MCP clients (--http). Press Ctrl+C to stop it."); await app.WaitForShutdownAsync(); return; }` <br> • `await app.StopAsync();` |

---

#### Scenario 2: Expose the agent as a local MCP tool (stdio)

The program is the MCP host: it starts the stdio server (this same program with `--stdio`), lists the tools and asks for the meeting rooms that can host 10 people.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 13** | Describe how to start the server | • `StdioClientTransport transport = new(new StdioClientTransportOptions { Name = "CompanyAssistant over stdio", Command = "dotnet", Arguments = [Assembly.GetEntryAssembly()!.Location, "--stdio"], StandardErrorLines = line => ColoredConsole.WriteSecondaryLogLine($"[server] {line}") });` <br> • `Assembly.GetEntryAssembly()!.Location` is the path of the built program (`bin/Debug/net10.0/ExposeAIAgentAsMCPTool.dll`) <br> • `StandardErrorLines` receives the log of the server (its stderr), shown with a `[server]` prefix |
| **TODO 14** | Connect | • `await using McpClient mcpClient = await McpClient.CreateAsync(transport);` <br> • The client starts the server process and opens the MCP session; when it is disposed (end of the scenario), it closes stdin and the server exits |
| **TODO 15** | List the tools and call the tool | • Same as TODO 11.2: `IList<McpClientTool> mcpTools = await mcpClient.ListToolsAsync();` `WriteTools(mcpTools);` `CallToolResult result = await mcpClient.CallToolAsync("CompanyAssistant", new Dictionary<string, object?> { ["query"] = query }).AsTask().WithSpinner("Waiting for the MCP tool");` `WriteToolResult(result);` |

---

#### Scenario 3: Another agent uses the CompanyAssistant through MCP

The `OfficeAssistant` agent has a single tool: the `CompanyAssistant`, received through MCP. You ask it to book a room for `EMP002` and to tell the names of the room and of the employee, which only the company tools know.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 16** | Start the stdio server and connect to it | • Same as TODO 13 and TODO 14, in one statement: `await using McpClient mcpClient = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions { Name = "CompanyAssistant over stdio", Command = "dotnet", Arguments = [Assembly.GetEntryAssembly()!.Location, "--stdio"], StandardErrorLines = line => ColoredConsole.WriteSecondaryLogLine($"[server] {line}") }));` |
| **TODO 17** | Get the MCP tools and create the `OfficeAssistant` | • `IList<McpClientTool> mcpTools = await mcpClient.ListToolsAsync();` <br> • `AIAgent officeAssistant = chatClient.AsAIAgent(instructions: "You are the personal assistant of the employees. Delegate every question or task about the company to the CompanyAssistant tool, then report its answer briefly.", name: "OfficeAssistant", tools: [.. mcpTools.Cast<AITool>()]);` <br> • An `McpClientTool` is an `AIFunction` (Lab 04): the agent calls it like a .NET tool, and the call goes to the MCP server |
| **TODO 18** | Run the `OfficeAssistant` and display what happened | • `AgentResponse response = await officeAssistant.RunAsync(prompt).WithSpinner("Running the OfficeAssistant");` <br> • `WriteToolCalls(response);` <br> • `ColoredConsole.WritePrimaryLogLine("Agent answer:");` `ColoredConsole.WriteSecondaryLogLine(response.Text);` <br> • `WriteTokenUsage(response);` |

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

Run from the project folder: ASP.NET Core reads the `Logging` settings from the `appsettings.json` of the current folder (from another folder, you see its information logs, which is harmless).

To run only one scenario, edit `scenariosToRun` at the top of `Program.cs` (for example `[2]`).

### Step 5 (optional): Use the server from MCP Inspector or VS Code

Any MCP host can use the `CompanyAssistant`, exactly like scenarios 1 and 2 do.

**Build first**, then always start the stdio server with `--no-build` (or with the `.dll`): when `dotnet run` builds, it writes the build warnings **on stdout**, which breaks the stdio protocol.

```bash
cd Solution            # or Start, once your TODOs are done
dotnet build
```

| Host | Stdio server | HTTP server |
|------|--------------|-------------|
| [MCP Inspector](https://modelcontextprotocol.io/docs/tools/inspector): `npx @modelcontextprotocol/inspector`, then open the URL it prints | Transport **STDIO**, command `dotnet`, arguments `run --project /ABSOLUTE/PATH/TO/Lab10_ExposeAIAgentAsMCPTool/Solution --no-build -- --stdio` | Start the server with `dotnet run --no-build -- --http`, then transport **Streamable HTTP**, URL `http://localhost:5100/mcp/companyassistant` |
| VS Code (GitHub Copilot, agent mode): `.vscode/mcp.json` | `{ "servers": { "companyassistant": { "type": "stdio", "command": "dotnet", "args": ["run", "--project", "/ABSOLUTE/PATH/TO/Lab10_ExposeAIAgentAsMCPTool/Solution", "--no-build", "--", "--stdio"] } } }` | `{ "servers": { "companyassistant": { "type": "http", "url": "http://localhost:5100/mcp/companyassistant" } } }` (server started with `--http`) |

Then list the tools (`CompanyAssistant`), and call it with a `query` such as `Get the information of the employee with the ID EMP001`.
The stdio server inherits the environment of the host: if your settings come from `AzureOpenAI__*` environment variables, set them in the host too (MCP Inspector and `mcp.json` both accept environment variables); user secrets need nothing.
Legacy **SSE** clients cannot connect: see [How it works](#how-it-works).

## How it works

```
MCP client / host                     MCP server (this program)
(scenarios 1-3, MCP Inspector,        ┌──────────────────────────────────────────────────┐
 VS Code, another agent)              │ McpServerTool  "CompanyAssistant" (query: string) │
        │  tools/list, tools/call     │   └ AIFunction  (AsAIFunction)                   │
        ├────────────────────────────►│       └ AIAgent CompanyAssistant ── Azure OpenAI │
        │  Streamable HTTP or stdio   │           └ get_employee_info, get_meeting_rooms,│
        │◄────────────────────────────┤             book_meeting_room                    │
        │  text of the agent's answer └──────────────────────────────────────────────────┘
```

1. **Agent → function.** `AsAIFunction()` creates an `AIFunction` named after the agent (characters other than letters and digits become `_`), described by the agent's description, with a single string parameter `query`. Invoking it runs the agent with the query and returns `response.Text`. Without a session, **every call runs the agent in a new session**: the tool is stateless, and two callers never share a conversation. In scenario 3, the `OfficeAssistant` often calls the tool several times; each call starts from scratch. (`AsAIFunction` also accepts an `AgentSession`, but then every call of every client shares one conversation: avoid it on a server.)
2. **Function → MCP tool.** `McpServerTool.Create()` exposes the function: `tools/list` returns its name, description and input schema (what `WriteTools` displays), `tools/call` invokes it. `AIFunctionFactory` serializes the return value to JSON, so the answer travels as a **JSON string** (`"Here is...\n..."`); `McpConsole` decodes it for display, MCP hosts give it to their model as is.
3. **Streamable HTTP** (`WithHttpTransport()` + `MapMcp(path)`): one HTTP endpoint at the path. With the MCP C# SDK 2.2, the server is **stateless** by default (no MCP session: every request stands alone), and the legacy **SSE** endpoints (`/sse`, `/message`) are **not mapped** (`HttpServerTransportOptions.EnableLegacySse` is `false`). Clients use Streamable HTTP; `HttpTransportMode.AutoDetect`, the client default, tries it first.
4. **stdio** (`WithStdioServerTransport()`): the host starts the server process; requests arrive on stdin and answers leave on stdout, one JSON-RPC message per line. Any other line on stdout is an invalid message: logs go to **stderr**, which hosts show as the server log (`[server]` lines in scenarios 2 and 3). The server stops when the host closes stdin.
5. **Whose tokens?** The `CompanyAssistant` calls the model **in the server process**, with the Azure OpenAI settings of the server. A client only sends a query and receives text. In scenario 3, the usage displayed is the `OfficeAssistant`'s; the `CompanyAssistant`'s model calls are made by the server and are not included.

## Expose an agent: MCP or A2A?

Lab 10 exposes an agent with **MCP**; Lab06_A2AServer exposes agents with **A2A**. Both make an agent usable by other programs, but they do not give the caller the same thing:

| | MCP (this lab) | A2A (Lab06_A2AServer and Lab06_A2AClient) |
|---|---|---|
| What the agent becomes | A **tool** (`tools/list`, `tools/call`): a name, a description, one `query` parameter | An **agent**: an agent card (skills, interfaces), messages, a conversation |
| Who can use it | Any **MCP host**: IDEs (VS Code), MCP Inspector, assistants, other agents | Other **agents** that speak A2A, whatever their framework |
| Conversation | None: every call runs the agent in a new session (point 1 above) | A `contextId` per conversation, kept by the server in a session store |
| Discovery | The client knows the server (URL or command), then lists its tools | An agent card at `.well-known/agent-card.json`, or a registry |
| Long-running work | One call, one answer | A2A tasks (polling, stream reconnection) |
| Status in .NET | MCP C# SDK `ModelContextProtocol` **stable** (2.2.0) | A2A packages of Agent Framework and A2A SDK v1 in **preview** |

Choose **MCP** when the agent is one more capability among the tools of an assistant or an IDE, called with a single question. Choose **A2A** when another agent must hold a conversation with yours, discover what it can do, or follow long tasks across a service, team or organization boundary. Within one process, neither is needed: give the agent to the other agent as a tool with `AsAIFunction()` (MultiAgentSystem lab *Delegate a Task to Another Agent*).

## Key Concepts

| Concept | Description |
|---------|-------------|
| MCP server | A program that exposes tools (and resources, prompts) to MCP clients. Here, its only tool is an agent |
| `AsAIFunction()` | Turns an `AIAgent` into an `AIFunction`: name and description of the agent, one `query` parameter, the agent's answer as result; a new session per call when no session is given |
| `McpServerTool.Create(function)` | Wraps an `AIFunction` as an MCP server tool |
| `AddMcpServer()` / `WithTools([...])` | Registers the MCP server and its tools in the dependency injection container |
| `WithHttpTransport()` + `MapMcp(path)` | Streamable HTTP transport (package `ModelContextProtocol.AspNetCore`), served by ASP.NET Core at `path`; stateless by default, no legacy SSE |
| `WithStdioServerTransport()` | stdio transport: the server is a child process of the host; stdout is reserved for MCP messages |
| `Host.CreateEmptyApplicationBuilder(settings: null)` | A host without configuration source nor logging provider, as in the official sample: nothing written to stdout |
| `McpClient`, `HttpClientTransport`, `StdioClientTransport` | The client side (Lab 04): connect to a server, or start one as a child process |
| `ListToolsAsync()` / `CallToolAsync(name, arguments)` | MCP `tools/list` and `tools/call`; `CallToolResult.Content` holds the text blocks, `IsError` a failure of the tool |
| `McpClientTool` | A tool of an MCP server seen as an `AIFunction`: give it to an agent, and its calls go to the server |

## Namespaces Reference

| Namespace | Purpose |
|-----------|---------|
| `OpenAI` | `OpenAIClient`, `OpenAIClientOptions` |
| `OpenAI.Chat` | `ChatClient` and the `AsAIAgent()` extension method |
| `System.ClientModel` | `ApiKeyCredential` |
| `System.ClientModel.Primitives` | `BearerTokenPolicy` |
| `Azure.Identity` | `DefaultAzureCredential` |
| `Microsoft.Agents.AI` | `AIAgent`, `AgentResponse`, the `AsAIFunction()` extension method |
| `Microsoft.Extensions.AI` | `AIFunctionFactory`, `AIFunction`, `AITool` |
| `ModelContextProtocol.Server` | `McpServerTool` |
| `Microsoft.Extensions.DependencyInjection` | `AddMcpServer()`, `WithHttpTransport()`, `WithStdioServerTransport()`, `WithTools()` |
| `Microsoft.AspNetCore.Builder` | `WebApplication`, `WebApplicationBuilder`, `MapMcp()` |
| `Microsoft.Extensions.Hosting` | `Host`, `HostApplicationBuilder` |
| `ModelContextProtocol.Client` | `McpClient`, `HttpClientTransport`, `StdioClientTransport`, `McpClientTool` |
| `ModelContextProtocol.Protocol` | `CallToolResult` |
| `System.Reflection` | `Assembly.GetEntryAssembly()` (path of the program started with `--stdio`) |
| `CommonUtilities` | `ColoredConsole`, `WithSpinner()`, `AzureOpenAIEndpoint` |

## Expected Output

```
Endpoint: https://your-resource.openai.azure.com/
Deployment: your-deployment-name
----------------------------------------
=== Scenario 1: Expose the agent as a remote MCP tool (Streamable HTTP) ===
MCP server listening on http://localhost:5100/mcp/companyassistant
Query: Get the information of the employee with the ID EMP003.
MCP tools available (1):
- CompanyAssistant: Answers questions about the employees and the meeting rooms of the company, and books meeting rooms.
  Input schema: {"type":"object","properties":{"query":{"description":"Input query to invoke the agent.","type":"string"}},"required":["query"]}
Tool result:
Here is the information for the employee with ID EMP003:

- **Name:** Charlie Brown
- **Department:** Engineering
- **Position:** Tech Lead
- **Email:** charlie.brown@company.com
- **Vacation Days Remaining:** 12
----------------------------------------
=== Scenario 2: Expose the agent as a local MCP tool (stdio) ===
Query: Which meeting rooms can host at least 10 people?
[server] Endpoint: https://your-resource.openai.azure.com/
[server] Deployment: your-deployment-name
[server] CompanyAssistant MCP server ready on stdio.
MCP tools available (1):
- CompanyAssistant: Answers questions about the employees and the meeting rooms of the company, and books meeting rooms.
  Input schema: {"type":"object","properties":{"query":{"description":"Input query to invoke the agent.","type":"string"}},"required":["query"]}
Tool result:
The meeting rooms that can host at least 10 people are:

1. **Innovation Lab (ROOM-A)**
   - Capacity: 10 people
   - Features: Projector, Whiteboard, Video Conference

2. **Executive Suite (ROOM-C)**
   - Capacity: 20 people
   - Features: Projector, Video Conference, Catering Service
----------------------------------------
=== Scenario 3: Another agent uses the CompanyAssistant through MCP ===
User: Book the meeting room ROOM-B for the employee EMP002 on 2026-10-26 from 10:00 to 11:00, subject: Campaign review. Tell me the name of the room and the name of the employee.
[server] Endpoint: https://your-resource.openai.azure.com/
[server] Deployment: your-deployment-name
[server] CompanyAssistant MCP server ready on stdio.
Tool called: CompanyAssistant(query: Book the meeting room ROOM-B for the employee EMP002 on 2026-10-26 from 10:00 to 11:00, subject: Campaign review.)
Tool result: The meeting room ROOM-B has been successfully booked for the employee EMP002. Here are the details:

- **Room:** Brainstorm Corner (ROOM-B)
- **Date:** 2026-10-26
- **Time:** 10:00 - 11:00
- **Subject:** Campaign review
- **Booked by:** Bob Johnson
Tool called: CompanyAssistant(query: Get the name of the employee with ID EMP002.)
Tool result: The name of the employee with ID EMP002 is Bob Johnson.
Tool called: CompanyAssistant(query: Get the name of the room with ID ROOM-B.)
Tool result: The name of the room with ID ROOM-B is "Brainstorm Corner."
Agent answer:
The meeting room ROOM-B has been successfully booked for the employee Bob Johnson on 2026-10-26 from 10:00 to 11:00 for the subject "Campaign review."
- **Room Name:** Brainstorm Corner
- **Employee Name:** Bob Johnson
----------------------------------------
Token Usage:
  Input tokens: xxx
  Output tokens: xxx
  Total tokens: xxx
```

Answers vary from one run to another. In scenario 3 the `OfficeAssistant` calls the tool once or several times (each call is a new conversation for the `CompanyAssistant`, which does not remember the previous one); the date is tomorrow's. The `[server]` lines may come a little earlier or later: they are read from another process.

**Color legend:** scenario titles in **cyan**, server addresses, queries and section titles in **blue**, results and `[server]` lines in **gray**.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `'AzureOpenAI:Endpoint' is not configured` | Replace the `YOUR-...` placeholders in `appsettings.json` (or use user secrets / environment variables) |
| `401 Unauthorized` with an API key | Check the key (user secret `AzureOpenAI:APIKey`) and that it belongs to the resource of the endpoint |
| `401` / `403` with Entra ID | Run `az login` and make sure your identity has the **Cognitive Services OpenAI User** role on the resource |
| `The tool reported an error: An error occurred invoking 'CompanyAssistant'.` | The agent failed **inside the server** (wrong deployment, credentials...). The MCP server does not send the details to the client: in scenario 1 the server logs them on the console (`DeploymentNotFound`, `401`...); check the settings, then scenario 3 shows the same error directly |
| `Failed to bind to address http://127.0.0.1:5100: address already in use` | Another program (or a server started with `--http`) uses the port: stop it, or change `McpServer:BaseUrl` (environment variable `McpServer__BaseUrl`) |
| `'McpServer:BaseUrl' must be an absolute http:// or https:// URL` | Use a full URL, for example `http://localhost:5100` |
| An MCP host cannot connect to the stdio server, or reports invalid messages | Start it with `--no-build` or with the `.dll` (Step 5): `dotnet run` writes the build output on stdout. And keep `Console.SetOut(Console.Error)`: in `--stdio` mode, any `Console.WriteLine` would also break the protocol |
| An SSE client gets `404` / `405` | The legacy SSE transport is disabled by default: use Streamable HTTP (MCP Inspector: transport **Streamable HTTP**, URL without `/sse`) |
| Scenario 2 or 3 fails with `ClientTransportClosedException: MCP server process exited unexpectedly` | The stdio server process stopped before answering. `exit code: 0` without the `[server] CompanyAssistant MCP server ready on stdio.` line: TODO 7 and 8 are not done. Otherwise its error is in the `Server's stderr tail` of the message (for example a configuration error). The server is the `.dll` of `bin/Debug/net10.0`: build the project first |
| Scenario 3: `Tool calls: none` | The `OfficeAssistant` answered by itself: check that it received the MCP tools (TODO 17) and its instructions |

## Security

- The HTTP server of this lab has **no authentication**: anyone who can reach it can run your agent, spend your tokens and book rooms. It listens on `localhost` only. Before exposing an MCP server on a network, add authentication and authorization (`MapMcp()` returns an endpoint builder on which ASP.NET Core authorization policies apply, e.g. `RequireAuthorization()`), HTTPS and rate limiting.
- An MCP tool backed by an agent can do whatever the agent's tools do. `book_meeting_room` **writes**: some MCP hosts (GitHub Copilot for example) ask the user before running a tool, but your server should not rely on it.
- A stdio server runs with the rights of the user who starts it, with the configuration it finds (user secrets, environment variables): only configure servers you trust in your MCP hosts.

## Provided Files

The following files are provided and should not be modified:

- `ExposeAIAgentAsMCPTool.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs` - Loads and validates the configuration
- `AzureOpenAISettings.cs` - Settings class for Azure OpenAI
- `McpServerSettings.cs` - Settings class for the HTTP MCP server
- `Tools/CompanyTools.cs` - The company tools, with their `[Description]` attributes
- `McpConsole.cs` - Display helpers

The following files should be modified:

- `appsettings.json` - Update with your Azure OpenAI endpoint and deployment
- `Program.cs` - Complete the TODOs

### NuGet packages

| Package | Why |
|---------|-----|
| `Microsoft.Agents.AI.OpenAI` | Agent Framework + `AsAIAgent()` for OpenAI clients (brings `OpenAI` and `Microsoft.Extensions.AI`); `AsAIFunction()` |
| `Azure.Identity` | `DefaultAzureCredential` for Entra ID authentication |
| `ModelContextProtocol.AspNetCore` | Official MCP C# SDK for ASP.NET Core: `WithHttpTransport()`, `MapMcp()`. It brings `ModelContextProtocol` (`McpServerTool`, stdio transport, `McpClient`) and the ASP.NET Core shared framework (`WebApplication`, `Host`, and the configuration providers used by `ConfigurationHelper`) |

## Going Further

- **Plain text results**: `AsAIFunction(new AIFunctionFactoryOptions { MarshalResult = (result, _, _) => new ValueTask<object?>(result) })` returns the answer of the agent as is: MCP clients then receive plain text instead of a JSON string.
- **Name and description of the tool**: `AsAIFunction(new AIFunctionFactoryOptions { Name = "...", Description = "..." })` overrides the values taken from the agent.
- **Watch the MCP requests**: set `"ModelContextProtocol": "Information"` under `Logging:LogLevel` in `appsettings.json` to see each `tools/list` and `tools/call` handled by the HTTP server.
- **Several agents on one server**: `.WithTools([toolA, toolB])` exposes several agents; each one is a tool, chosen by the client (or its model) by name and description.
- **Publish the stdio server as a package**: the `Microsoft.McpServer.ProjectTemplates` templates (`dotnet new mcpserver`, in preview) create an MCP server project that can be published to NuGet, see [Build a minimal MCP server](https://learn.microsoft.com/dotnet/ai/quickstarts/build-mcp-server).

## Coming from an older version of the lab?

| Before (preview) | Now (1.26.0, MCP C# SDK 2.2.0) |
|------------------|--------------------------------|
| `Azure.AI.OpenAI` / `AzureOpenAIClient` | `OpenAI` / `OpenAIClient` + Azure OpenAI v1 endpoint |
| `ChatClientAgent agent = chatClient.CreateAIAgent(agentOptions)` | `AIAgent agent = chatClient.AsAIAgent(instructions, name, description, tools)` |
| Tools found by reflection (`GetMethods` + `AIFunctionFactory.Create(method, target)`) | The three tools listed explicitly, with snake_case names (Lab 03, scenario 1) |
| `ModelContextProtocol.AspNetCore` 0.5.0-preview.1 (Solution only: the Start did not compile) | `ModelContextProtocol.AspNetCore` 2.2.0 in Start **and** Solution |
| `Microsoft.NET.Sdk.Web` (Solution) / `Microsoft.Extensions.Hosting` (Start) | `Microsoft.NET.Sdk` + `ModelContextProtocol.AspNetCore`, which brings ASP.NET Core |
| `enum Scenario` and one scenario per run, each one blocking (`app.Run()`) | `scenariosToRun`; each scenario starts its server, calls it and stops it; `--http` / `--stdio` to keep a server for other hosts |
| `MapMcp` served Streamable HTTP **and** SSE (`.../sse`) | Streamable HTTP only, stateless by default; legacy SSE disabled (`EnableLegacySse`) |
| `app.UseHttpsRedirection()` | Removed: the server listens on `http://localhost` only, there is no HTTPS endpoint to redirect to |
| The stdio server printed its configuration and titles on **stdout** (8 lines before the first MCP message) | `Console.SetOut(Console.Error)` in `--stdio` mode: stdout carries MCP messages only |
| Tested with MCP Inspector only (Node.js) | Tested by the program itself (MCP client + host, `dotnet` only); MCP Inspector and VS Code remain optional (Step 5) |
| — | Scenario 3: another agent uses the agent through MCP |
| API key in `appsettings.json` | API key in user secrets or environment variables |

`AsAIFunction()` and `McpServerTool.Create()` are unchanged.

## Useful Links

- [Official sample `Agent_Step07_AsMcpTool`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step07_AsMcpTool) (stdio server, MCP Inspector)
- [Official sample `Agent_MCP_Server`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/ModelContextProtocol/Agent_MCP_Server) (an agent using the tools of a stdio MCP server)
- [Using MCP tools with an agent](https://learn.microsoft.com/agent-framework/agents/tools/local-mcp-tools?pivots=programming-language-csharp)
- [Get started with .NET AI and MCP](https://learn.microsoft.com/dotnet/ai/get-started-mcp)
- [Build a minimal MCP server (stdio and HTTP)](https://learn.microsoft.com/dotnet/ai/quickstarts/build-mcp-server)
- [Build a minimal MCP client](https://learn.microsoft.com/dotnet/ai/quickstarts/build-mcp-client)
- [Microsoft Agent Framework documentation](https://learn.microsoft.com/agent-framework/overview/?pivots=programming-language-csharp)
- [Azure OpenAI v1 API](https://learn.microsoft.com/azure/foundry/openai/api-version-lifecycle)

## Solution

If you get stuck, check the complete solution in the `Solution/` folder.
