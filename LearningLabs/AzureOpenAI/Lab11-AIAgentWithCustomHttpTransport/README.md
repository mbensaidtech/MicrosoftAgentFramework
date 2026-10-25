# Lab 11 - AI Agent With Custom HTTP Transport

## Objective

In this lab, you will give the OpenAI SDK used by your agent a **custom HTTP transport**: an `HttpClient` whose handler intercepts every HTTP request sent to Azure OpenAI, and its response.

You will use it to add a header to each request, log the exact traffic (URL, headers, JSON bodies, status, duration) without ever printing a credential, and see what one agent run really sends: one HTTP request for a simple prompt, several when the agent calls a function tool.

## What You Will Learn

- How to write an HTTP message handler with `DelegatingHandler` (and its `InnerHandler`)
- How to plug your own `HttpClient` into the OpenAI SDK with `OpenAIClientOptions.Transport` and `HttpClientPipelineTransport`
- How to add a custom header to every request, measure the duration of each call and count the requests
- How to log requests and responses safely: the credential headers (`Authorization`, `api-key`) are always masked
- What an agent sends on the wire: the Azure OpenAI v1 URL, the deployment in the `model` field, the instructions as a `system` message, the tools and their JSON schema
- Why one `RunAsync` can send several HTTP requests (function calling) and why its token usage is the total of all of them

## Prerequisites

- .NET 10 SDK or later
- An Azure OpenAI (or Microsoft Foundry) resource with a chat model deployment that supports function calling (for example `gpt-4o-mini` or `gpt-5.4-mini`)
- One of the following for authentication:
  - the resource **API key**, or
  - Azure CLI logged in (`az login`) with the **Cognitive Services OpenAI User** role on the resource
- Lab 01 (client and agent) and Lab 03 (function tools) are recommended

## Project Structure

```
Lab11-AIAgentWithCustomHttpTransport/
├── README.md
├── Start/                                  <-- Your working folder
│   ├── Program.cs                          <-- Complete the TODOs here (setup, scenarios and the HTTP handler)
│   ├── HttpConsole.cs                      <-- Provided: display helpers (headers with masked credentials, JSON bodies)
│   ├── Tools/WeatherTools.cs               <-- Provided: the function tool of scenario 2
│   ├── ConfigurationHelper.cs
│   ├── AzureOpenAISettings.cs
│   ├── appsettings.json
│   └── AIAgentWithCustomHttpTransport.csproj
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

     The user secrets id is shared by all the labs: if you already did it for a previous lab, there is nothing to do.
     You can also use the environment variable `AzureOpenAI__APIKey`.

   - **Microsoft Entra ID** — leave `APIKey` unset and run `az login`. `DefaultAzureCredential` picks up your Azure CLI identity.

3. **Or use the dashboard**: the *Azure OpenAI settings* of the optional [Lab Bench dashboard](../../../Dashboard/README.md#azure-openai-settings) write the endpoint, the deployment and the API key to the same user secrets, for every migrated lab (no file to edit, no command to type).

### Step 2: Look at the provided files

- `HttpConsole.cs` — display helpers, imported with `using static` so you can call them directly:
  - `WriteHeaders(headers, content)` writes the headers of a request or a response, then the headers of its content (`Content-Type`, `Content-Length`). The value of `Authorization` and `api-key` is **always masked** (`Bearer [MASKED]`): with the OpenAI SDK, your API key itself travels in `Authorization: Bearer <key>`;
  - `await WriteBodyAsync(content, cancellationToken)` reads the body and writes it (indented when it is JSON). It does not read a streamed response (`text/event-stream`);
  - `StatusLabel(statusCode)` returns `OK`, `REDIRECT`, `CLIENT ERROR` or `SERVER ERROR`;
  - `WriteTokenUsage(response)` writes the token usage of an agent run.
- `Tools/WeatherTools.cs` — `GetWeather(location)`, the tool of the official "add tools" getting-started sample. It always answers *cloudy with a high of 15 degrees Celsius*: a value the model cannot guess.

### Step 3: Complete the Program.cs

Open `Start/Program.cs` and complete the TODOs.

The HTTP handler is the class `HttpLoggingHandler` at the **bottom** of `Program.cs` (TODO 11–14). You can do the TODOs in order: after TODO 1–10 the agent already answers through your transport, but nothing is logged yet (the handler only forwards the requests, and counts them). Complete TODO 11–14 to see the traffic.

---

#### Setup: Configuration and Client Initialization

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 1** | Create the logging handler and an `HttpClient` that uses it | • `HttpLoggingHandler loggingHandler = new() { InnerHandler = new HttpClientHandler() };` <br> • `using HttpClient httpClient = new(loggingHandler);` <br> • A `DelegatingHandler` only intercepts: its `InnerHandler` (`HttpClientHandler`) is the handler that talks to the network <br> • Keep `loggingHandler` in a variable: the scenarios read its `RequestCount` |
| **TODO 2** | Create the `OpenAIClientOptions` with the custom transport | • `OpenAIClientOptions clientOptions = new() { Endpoint = AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint), Transport = new HttpClientPipelineTransport(httpClient), NetworkTimeout = TimeSpan.FromSeconds(30) };` <br> • `AzureOpenAIEndpoint.ToV1Uri` (from `CommonUtilities`) returns the `.../openai/v1/` URI <br> • `HttpClientPipelineTransport` (`System.ClientModel.Primitives`) makes the SDK send its requests with your `HttpClient` <br> • `NetworkTimeout` applies to each attempt |
| **TODO 3** | Create the `OpenAIClient` | • API key: `new OpenAIClient(new ApiKeyCredential(settings.APIKey), clientOptions)` <br> • Entra ID: `new OpenAIClient(new BearerTokenPolicy(new DefaultAzureCredential(), "https://ai.azure.com/.default"), clientOptions)` <br> • `OpenAIClient client = !string.IsNullOrWhiteSpace(settings.APIKey) ? ... : ...;` (keep the `#pragma` `OPENAI001` around it) |
| **TODO 4** | Get a `ChatClient` for the deployment | • `ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);` |

---

#### Scenario 1: See the HTTP traffic of an agent run

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 5** | Create a basic AI Agent | • `AIAgent basicAgent = chatClient.AsAIAgent();` |
| **TODO 6** | Run the agent, counting the HTTP requests of the run | • `int requestsBefore = loggingHandler.RequestCount;` <br> • `AgentResponse basicResponse = await basicAgent.RunAsync(prompt);` <br> • No `.WithSpinner()` in this lab: the spinner would write over the HTTP log printed by the handler |
| **TODO 7** | Display the answer, the number of requests and the token usage | • `ColoredConsole.WritePrimaryLogLine("Agent answer:");` <br> • `ColoredConsole.WriteAssistantLine(basicResponse.Text);` <br> • `ColoredConsole.WritePrimaryLogLine($"HTTP requests sent by this run: {loggingHandler.RequestCount - requestsBefore}");` <br> • `WriteTokenUsage(basicResponse);` |

---

#### Scenario 2: One agent run, several HTTP requests (function calling)

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 8** | Create an agent with a function tool | • `AIAgent weatherAgent = chatClient.AsAIAgent(instructions: "You are a helpful assistant. Use the tools to answer.", name: "WeatherAgent", tools: [AIFunctionFactory.Create(WeatherTools.GetWeather, "get_weather")]);` <br> • The instructions and the tool will appear in the body of the request: a `system` message and the `tools` array |
| **TODO 9** | Run the agent, counting the HTTP requests of the run | • `int requestsBefore = loggingHandler.RequestCount;` <br> • `AgentResponse weatherResponse = await weatherAgent.RunAsync(prompt);` |
| **TODO 10** | Display the answer, the number of requests and the token usage | • Same as TODO 7 with `weatherResponse`: `ColoredConsole.WritePrimaryLogLine("Agent answer:");`, `ColoredConsole.WriteAssistantLine(weatherResponse.Text);`, `ColoredConsole.WritePrimaryLogLine($"HTTP requests sent by this run: {loggingHandler.RequestCount - requestsBefore}");`, `WriteTokenUsage(weatherResponse);` <br> • Expect **2** requests and a usage equal to the total of the two `usage` objects of the responses |

---

#### The HTTP handler: `HttpLoggingHandler.SendAsync` (bottom of `Program.cs`)

`SendAsync` receives each request when it leaves the SDK, and must return the response. The first line (provided) numbers the request: `int number = Interlocked.Increment(ref _requestCount);`.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 11** | Add a custom header to the request | • `request.Headers.Add("X-agent-name", "AIAgentWithCustomHttpTransport");` |
| **TODO 12** | Write the HTTP request | • `ColoredConsole.WriteDividerLine();` <br> • `ColoredConsole.WriteInfoLine($"=== HTTP REQUEST #{number} ===");` <br> • `ColoredConsole.WritePrimaryLogLine($"[URL] {request.Method} {request.RequestUri}");` <br> • `WriteHeaders(request.Headers, request.Content);` <br> • `await WriteBodyAsync(request.Content, cancellationToken);` |
| **TODO 13** | Send the request to the next handler and measure the duration | • **Replace** the last line of the method (`return await base.SendAsync(request, cancellationToken);`) by: <br> • `long start = Stopwatch.GetTimestamp();` <br> • `HttpResponseMessage response = await base.SendAsync(request, cancellationToken);` <br> • `TimeSpan duration = Stopwatch.GetElapsedTime(start);` |
| **TODO 14** | Write the HTTP response, then return it | • `ColoredConsole.WriteEmptyLine();` <br> • `ColoredConsole.WriteSuccessLine($"=== HTTP RESPONSE #{number} ===");` <br> • `ColoredConsole.WritePrimaryLogLine($"[Status] {(int)response.StatusCode} {response.StatusCode} ({StatusLabel(response.StatusCode)}) in {duration.TotalMilliseconds:F0} ms");` <br> • `WriteHeaders(response.Headers, response.Content);` <br> • `await WriteBodyAsync(response.Content, cancellationToken);` <br> • `ColoredConsole.WriteDividerLine();` <br> • `return response;` |

**Complete `SendAsync` after TODO 11–14:**

```csharp
protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
{
    int number = Interlocked.Increment(ref _requestCount);

    // TODO 11
    request.Headers.Add("X-agent-name", "AIAgentWithCustomHttpTransport");

    // TODO 12
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine($"=== HTTP REQUEST #{number} ===");
    ColoredConsole.WritePrimaryLogLine($"[URL] {request.Method} {request.RequestUri}");
    WriteHeaders(request.Headers, request.Content);
    await WriteBodyAsync(request.Content, cancellationToken);

    // TODO 13
    long start = Stopwatch.GetTimestamp();
    HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
    TimeSpan duration = Stopwatch.GetElapsedTime(start);

    // TODO 14
    ColoredConsole.WriteEmptyLine();
    ColoredConsole.WriteSuccessLine($"=== HTTP RESPONSE #{number} ===");
    ColoredConsole.WritePrimaryLogLine($"[Status] {(int)response.StatusCode} {response.StatusCode} ({StatusLabel(response.StatusCode)}) in {duration.TotalMilliseconds:F0} ms");
    WriteHeaders(response.Headers, response.Content);
    await WriteBodyAsync(response.Content, cancellationToken);
    ColoredConsole.WriteDividerLine();

    return response;
}
```

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

To run only one scenario, edit `scenariosToRun` at the top of `Program.cs` (for example `[2]`). The output is long on purpose: every request and every response is printed in full.

## How it works

```
agent.RunAsync("...")
 └─ AIAgent (AsAIAgent)                    Agent Framework: instructions, tools, function-calling loop
     └─ IChatClient                        Microsoft.Extensions.AI adapter over the OpenAI ChatClient
         └─ ChatClient (OpenAI SDK)        builds POST /openai/v1/chat/completions and its JSON body
             └─ ClientPipeline             System.ClientModel: User-Agent and SDK headers → retries → authentication
                 └─ HttpClientPipelineTransport   (OpenAIClientOptions.Transport)
                     └─ HttpClient
                         └─ HttpLoggingHandler    <-- your code: the final request, once per attempt
                             └─ HttpClientHandler → network → Azure OpenAI
```

- **The handler sees the final request.** It runs after the SDK pipeline: the URL is the v1 one (`/openai/v1/chat/completions`), the deployment name is the `model` field of the body, and the `User-Agent` shows every layer (`OpenAI/2.13.0 …, MEAI/10.10.0 agent-framework-dotnet/1.22.0 …`). The `Authorization` header is already there: that is why it must be masked (see [Security](#security)).
- **One run, one or more HTTP requests.** Scenario 1 sends one request. In scenario 2, the first response has `"finish_reason": "tool_calls"`; the agent invokes `get_weather` locally, then sends a second request whose messages end with the model's `tool_calls` and a `"role": "tool"` message containing the tool result. The second response contains the final answer.
- **The token usage of a run is the total of its HTTP responses.** In the run below, scenario 2 shows `Input tokens: 185` = 74 (request #2) + 111 (request #3), and `Output tokens: 31` = 14 + 17. The second request is bigger: it repeats the whole conversation and the tools.
- **Each attempt goes through the handler.** The SDK retries some failures (for example `429` or `503`): every attempt is a new pass in `SendAsync`, so `HTTP requests sent by this run` can be higher than the number of model calls when the service throttles you.
- **Errors are logged before the exception.** With a wrong deployment name, the handler prints `[Status] 404 NotFound (CLIENT ERROR)` and the error body (`DeploymentNotFound`), then the SDK throws a `ClientResultException`.
- **Bodies are buffered.** `ReadAsStringAsync` loads the content in memory: the SDK can still read it after the handler. A streamed response (`text/event-stream`, `RunStreamingAsync`) is not read by `WriteBodyAsync`: reading it in the handler would wait for the end of the stream before the SDK gets the first event.
- **The Entra ID token request is not logged.** `DefaultAzureCredential` gets its tokens by its own means (Azure CLI, managed identity...), not through this `HttpClient`.

Response headers worth reading: `x-ms-region` (where the request was served), `x-ms-served-model` (the model version behind your deployment), `x-ratelimit-remaining-requests` / `x-ratelimit-remaining-tokens` (your remaining quota), `apim-request-id` (give it to support when you report a problem).

## Key Concepts

| Concept | Description |
|---------|-------------|
| `DelegatingHandler` | HTTP message handler that intercepts a request and passes it to its `InnerHandler` with `base.SendAsync`; handlers can be chained |
| `HttpClientHandler` | The handler that actually sends the request on the network (the last one of the chain) |
| `HttpClientPipelineTransport` | `System.ClientModel` transport that sends the SDK requests with the `HttpClient` you give it |
| `OpenAIClientOptions` | Options of the OpenAI client: `Endpoint`, plus the pipeline options inherited from `ClientPipelineOptions`: `Transport`, `NetworkTimeout`, `RetryPolicy`, `AddPolicy`, `ClientLoggingOptions` |
| `NetworkTimeout` | Timeout of one network attempt |
| Azure OpenAI **v1 API** | `POST https://<resource>.openai.azure.com/openai/v1/chat/completions`, no `api-version`, deployment name in `model` |
| `Authorization: Bearer` | Carries the credential: the API key with the OpenAI SDK, or the Entra ID access token. Never log it |
| `Stopwatch.GetTimestamp()` / `GetElapsedTime()` | Measure the duration of a call without allocating a `Stopwatch` |
| `AsAIAgent()` / `AIAgent` | Agent created from the `ChatClient`; its requests go through the custom transport |
| `AgentResponse.Usage` | Token usage of the run: the total of all the HTTP responses of the run |

## Namespaces Reference

| Namespace | Purpose |
|-----------|---------|
| `System.Net.Http` (implicit using) | `HttpClient`, `DelegatingHandler`, `HttpClientHandler`, `HttpRequestMessage`, `HttpResponseMessage` |
| `System.ClientModel.Primitives` | `HttpClientPipelineTransport`, `BearerTokenPolicy` |
| `System.ClientModel` | `ApiKeyCredential` |
| `System.Diagnostics` | `Stopwatch` |
| `OpenAI` | `OpenAIClient`, `OpenAIClientOptions` |
| `OpenAI.Chat` | `ChatClient` and the `AsAIAgent()` extension method |
| `Azure.Identity` | `DefaultAzureCredential` |
| `Microsoft.Agents.AI` | `AIAgent`, `AgentResponse` |
| `Microsoft.Extensions.AI` | `AIFunctionFactory` |
| `CommonUtilities` | `ColoredConsole`, `AzureOpenAIEndpoint` |

## Expected Output

Abbreviated (`...`): the real output prints every header and the whole JSON bodies (about 600 lines).

```
Endpoint: https://your-resource.openai.azure.com/
Deployment: gpt-4o-mini
-------------------------------------------------------------------------------
=== Scenario 1: See the HTTP traffic of an agent run ===
User: Hello, what is the capital of France?
-------------------------------------------------------------------------------
=== HTTP REQUEST #1 ===
[URL] POST https://your-resource.openai.azure.com/openai/v1/chat/completions
[Headers]
   Accept: application/json
   User-Agent: OpenAI/2.13.0 (.NET 10.0.13; macOS 27.0.0),MEAI/10.10.0 agent-framework-dotnet/1.22.0 (...)
   X-Stainless-Lang: csharp
   ...
   Authorization: Bearer [MASKED]
   X-agent-name: AIAgentWithCustomHttpTransport
   Content-Type: application/json

[Body]
   {
     "messages": [
       {
         "role": "user",
         "content": "Hello, what is the capital of France?"
       }
     ],
     "model": "gpt-4o-mini"
   }

=== HTTP RESPONSE #1 ===
[Status] 200 OK (OK) in 1485 ms
[Headers]
   ...
   x-ms-served-model: gpt-4o-mini-2024-07-18
   x-ratelimit-remaining-requests: 999
   x-ratelimit-remaining-tokens: 99984
   apim-request-id: 1524c5f7-...
   x-ms-region: France Central
   Content-Type: application/json
   ...

[Body]
   {
     "choices": [
       {
         ...
         "finish_reason": "stop",
         "message": {
           "content": "The capital of France is Paris.",
           ...
         }
       }
     ],
     ...
     "usage": {
       "completion_tokens": 7,
       ...
       "prompt_tokens": 16,
       ...
       "total_tokens": 23
     }
   }
-------------------------------------------------------------------------------
Agent answer:
The capital of France is Paris.
HTTP requests sent by this run: 1

Token Usage:
  Input tokens: 16
  Output tokens: 7
  Total tokens: 23
-------------------------------------------------------------------------------
=== Scenario 2: One agent run, several HTTP requests (function calling) ===
User: What is the weather like in Amsterdam?
-------------------------------------------------------------------------------
=== HTTP REQUEST #2 ===
...
[Body]
   {
     "messages": [
       { "role": "system", "content": "You are a helpful assistant. Use the tools to answer." },
       { "role": "user", "content": "What is the weather like in Amsterdam?" }
     ],
     "model": "gpt-4o-mini",
     "tools": [ { "type": "function", "function": { "description": "Get the weather for a given location.", "name": "get_weather", "parameters": { ... } } } ],
     "tool_choice": "auto"
   }

=== HTTP RESPONSE #2 ===
[Status] 200 OK (OK) in 656 ms
...
         "finish_reason": "tool_calls",
         ...
           "tool_calls": [ { "function": { "arguments": "{\"location\":\"Amsterdam\"}", "name": "get_weather" }, ... } ]
...
=== HTTP REQUEST #3 ===
...
       {
         "role": "tool",
         "tool_call_id": "call_zQNpb2BQFXHiLMJDlfN5HINS",
         "content": "\"The weather in Amsterdam is cloudy with a high of 15 degrees Celsius.\""
       }
...
=== HTTP RESPONSE #3 ===
[Status] 200 OK (OK) in 772 ms
...
-------------------------------------------------------------------------------
Agent answer:
The weather in Amsterdam is currently cloudy with a high of 15 degrees Celsius.
HTTP requests sent by this run: 2

Token Usage:
  Input tokens: 185
  Output tokens: 31
  Total tokens: 216
```

The JSON bodies above are shortened to one line per message; the real output is indented. Answers, durations, ids and token counts vary from one run to another; the `model` field is your deployment name.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `'AzureOpenAI:Endpoint' is not configured` | Replace the `YOUR-...` placeholders in `appsettings.json` (or use user secrets / environment variables) |
| `[Status] 401 Unauthorized (CLIENT ERROR)` then `ClientResultException: HTTP 401` with an API key | Check the key (user secret `AzureOpenAI:APIKey`) and that it belongs to the resource of the endpoint; the error body says *Access denied due to invalid subscription key or wrong API endpoint* |
| `401` / `403` with Entra ID | Run `az login` and make sure your identity has the **Cognitive Services OpenAI User** role on the resource |
| `[Status] 404 NotFound` with `"code": "DeploymentNotFound"` | `ChatDeploymentName` must be the **deployment** name, not the model name |
| The agent answers but nothing is logged | `SendAsync` still only forwards the request (TODO 12–14), or the `HttpClient` passed to `HttpClientPipelineTransport` does not use `loggingHandler` (TODO 1–2) |
| `InvalidOperationException: The inner handler has not been assigned` | `HttpLoggingHandler` needs `InnerHandler = new HttpClientHandler()` (TODO 1) |
| `HTTP requests sent by this run: 0` | The options do not use your transport: check `Transport = new HttpClientPipelineTransport(httpClient)` (TODO 2) |
| `X-agent-name` is missing from the logged headers | Add the header **before** writing the request (TODO 11 before TODO 12) |
| Scenario 2 sends 1 request and the answer does not mention 15 degrees | The model did not call the tool: check `tools: [AIFunctionFactory.Create(WeatherTools.GetWeather, "get_weather")]` (TODO 8) |
| More requests than expected | A retry: the SDK resends a request after some failures (`429`, `503`) and each attempt goes through the handler |
| `AggregateException: Retry failed after 4 tries. (The operation was cancelled because it exceeded the configured timeout of 0:00:30...)` | Each attempt took longer than `NetworkTimeout` (slow or reasoning model, long answer) and the SDK retried 3 times: raise `NetworkTimeout` (TODO 2). Each attempt appears as a new `HTTP REQUEST` in the log |

## Security

- **Never log a credential.** The handler sees the final request: with an API key, the OpenAI SDK sends the key itself in `Authorization: Bearer <key>`; with Entra ID, an access token. `HttpConsole.MaskIfSensitive` keeps only the scheme (`Bearer [MASKED]`); do not show "the first characters" of a key or a token.
- **Bodies contain the conversation.** Prompts, answers and tool results may hold personal or confidential data: in a real application, do not log bodies by default, and keep such logs out of shared files and dashboards.
- **Logs cost time.** Buffering and printing every body slows each call; this logging is for learning and debugging.

## Provided Files

The following files are provided and should not be modified:

- `AIAgentWithCustomHttpTransport.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs` - Loads and validates the configuration
- `AzureOpenAISettings.cs` - Settings class for Azure OpenAI
- `HttpConsole.cs` - Display helpers for the HTTP traffic (credentials masked) and the token usage
- `Tools/WeatherTools.cs` - The function tool of scenario 2

The following files should be modified:

- `appsettings.json` - Update with your Azure OpenAI endpoint and deployment
- `Program.cs` - Complete the TODOs (setup, scenarios and `HttpLoggingHandler`)

### NuGet packages

| Package | Why |
|---------|-----|
| `Microsoft.Agents.AI.OpenAI` | Agent Framework + `AsAIAgent()` for OpenAI clients (brings `OpenAI`, `System.ClientModel` with `HttpClientPipelineTransport`, and `Microsoft.Extensions.AI` with `AIFunctionFactory`) |
| `Azure.Identity` | `DefaultAzureCredential` for Entra ID authentication |
| `Microsoft.Extensions.Configuration.*` | `appsettings.json`, user secrets and environment variables, bound to `AzureOpenAISettings` |

`DelegatingHandler`, `HttpClientHandler` and `HttpClient` are part of .NET: no package is needed for the transport.

## Going Further

- **Built-in logging of the SDK**: when you only need logs, the pipeline can write them for you through `ILogger`: `clientOptions.ClientLoggingOptions = new ClientLoggingOptions { LoggerFactory = loggerFactory, EnableMessageContentLogging = true };` (`System.ClientModel.Primitives`; for example with `LoggerFactory.Create(b => b.AddConsole())` from the `Microsoft.Extensions.Logging.Console` package). Headers that are not in `AllowedHeaderNames` are written `REDACTED`, `Authorization` included. A custom handler remains the tool to modify, measure or route the traffic, or to plug an `HttpClient` you already configured (proxy, certificates).
- **Pipeline policy instead of a transport**: a `PipelinePolicy` added with `clientOptions.AddPolicy(policy, PipelinePosition.BeforeTransport)` runs inside the SDK pipeline without replacing the `HttpClient` (`PerCall`: once per call, `PerTry`: once per attempt, `BeforeTransport`: right before the transport, after authentication). The official Foundry hosting samples use one to rewrite the request URI.
- **`IHttpClientFactory`**: in an application with dependency injection, register the handler with `services.AddHttpClient("openai").AddHttpMessageHandler<HttpLoggingHandler>()` and give `HttpClientPipelineTransport` the client created by the factory (see [Outgoing request middleware](https://learn.microsoft.com/aspnet/core/fundamentals/http-requests#outgoing-request-middleware)).
- **The same handler for other clients**: an MCP `HttpClientTransport` also accepts your `HttpClient`; the official sample `Agent_MCP_PerRun_AuthHeaders` uses a `DelegatingHandler` to add an `Authorization` header to each MCP request.
- **Observability**: `agent.AsBuilder().UseOpenTelemetry(sourceName: ...).Build()` traces agent runs and model calls with OpenTelemetry instead of raw HTTP (official sample `Agent_Step05_Observability`).
- **Higher-level interception**: the agent and function-calling middleware of Lab 03 (`AsBuilder().Use(...)`) work on messages and function calls instead of HTTP.
- **Streaming**: with `RunStreamingAsync`, the response is `text/event-stream`; the handler sees it as soon as the headers arrive, and `WriteBodyAsync` leaves the stream to the SDK.

## Coming from an older version of the lab?

| Before (preview) | Now (1.22.0) |
|------------------|--------------|
| `Azure.AI.OpenAI` / `AzureOpenAIClient(Uri, credential, AzureOpenAIClientOptions)` | `OpenAI` / `OpenAIClient(credential, OpenAIClientOptions)` + Azure OpenAI v1 endpoint |
| `AzureOpenAIClientOptions { Transport = new HttpClientPipelineTransport(httpClient), NetworkTimeout = ... }` | `OpenAIClientOptions { Endpoint = ..., Transport = new HttpClientPipelineTransport(httpClient), NetworkTimeout = ... }` (same `ClientPipelineOptions` properties) |
| `CustomHttpClientHandler : HttpClientHandler`, in its own file | `HttpLoggingHandler : DelegatingHandler` with `InnerHandler = new HttpClientHandler()` (pattern of the official samples), at the bottom of `Program.cs` |
| URL `.../openai/deployments/<deployment>/chat/completions?api-version=...`, API key in the `api-key` header | URL `.../openai/v1/chat/completions`, deployment in the `model` field of the body, API key in `Authorization: Bearer` |
| `Authorization` masked after its first 20 characters; the `api-key` header printed **in clear** | Credential headers fully masked (only the scheme is shown) by `HttpConsole.MaskIfSensitive` |
| Display helpers in the handler file | `HttpConsole.cs` (provided): headers, bodies, status, token usage; streamed bodies not read |
| `ChatClientAgent agent = chatClient.CreateAIAgent();` | `AIAgent agent = chatClient.AsAIAgent();` |
| `AgentRunResponse`, `response.ToString()` | `AgentResponse`, `response.Text` |
| One scenario, no measure | Scenario 2 (function calling on the wire); request number, duration, request count and token usage of each run |
| `Microsoft.Extensions.Hosting` to read the configuration | `Microsoft.Extensions.Configuration.*` (`ConfigurationHelper`) |
| API key in `appsettings.json` | API key in user secrets or environment variables |

## Useful Links

- [Microsoft Agent Framework documentation](https://learn.microsoft.com/agent-framework/overview/?pivots=programming-language-csharp)
- [Using function tools with an agent](https://learn.microsoft.com/agent-framework/agents/tools/function-tools?pivots=programming-language-csharp)
- [Official sample `02_add_tools`](https://github.com/microsoft/agent-framework/tree/dotnet-1.22.0/dotnet/samples/01-get-started/02_add_tools)
- [Official sample `Agent_MCP_PerRun_AuthHeaders`](https://github.com/microsoft/agent-framework/tree/dotnet-1.22.0/dotnet/samples/02-agents/ModelContextProtocol/Agent_MCP_PerRun_AuthHeaders) (a `DelegatingHandler` in an `HttpClient`)
- [System.ClientModel library for .NET](https://learn.microsoft.com/dotnet/api/overview/azure/system.clientmodel-readme)
- [`HttpClientPipelineTransport`](https://learn.microsoft.com/dotnet/api/system.clientmodel.primitives.httpclientpipelinetransport), [`ClientPipelineOptions`](https://learn.microsoft.com/dotnet/api/system.clientmodel.primitives.clientpipelineoptions), [`ClientLoggingOptions`](https://learn.microsoft.com/dotnet/api/system.clientmodel.primitives.clientloggingoptions), [`PipelinePolicy`](https://learn.microsoft.com/dotnet/api/system.clientmodel.primitives.pipelinepolicy)
- [`DelegatingHandler`](https://learn.microsoft.com/dotnet/api/system.net.http.delegatinghandler)
- [Azure OpenAI v1 API](https://learn.microsoft.com/azure/foundry/openai/api-version-lifecycle)

## Solution

If you get stuck, check the complete solution in the `Solution/` folder.
