# Lab 13 - Host an Agent in an ASP.NET Core API

## Objective

In this lab, you will turn an agent built with **Microsoft Agent Framework** and **Azure OpenAI** into a **web API** that an application can call on behalf of its users.

Until now, every agent ran in a console program, for a single user. A real application needs more: the agent is created once and shared by all the requests, each answer is **streamed** to the browser as it is generated, each user has **their own conversations**, kept between two HTTP requests, and only **authenticated** users can call the agent. You will build this API with ASP.NET Core minimal APIs and **stable** packages only:

- the agent registered in **dependency injection**, with a tool that knows the signed-in user from the token, never from the model;
- an endpoint protected by **JWT bearer** authentication, that checks its input and streams the answer as **Server-Sent Events**;
- **per-user conversations**: the session of the agent is serialized after each request and stored under a key that combines the user and the conversation id.

The program starts the API, then plays four scenarios as a front end would: requests refused, a streamed answer for Alice, the same conversation continued in a second request, and Bob trying Alice's conversation id.

## What You Will Learn

- How to register an `AIAgent` in the ASP.NET Core dependency injection container, and why a singleton is right (the conversation is in the session, not in the agent)
- How a tool can use the services of the request (`IServiceProvider` parameter, `IHttpContextAccessor`) to act for the authenticated user
- How to stream an answer with `RunStreamingAsync` and `TypedResults.ServerSentEvents`
- How to keep a conversation between HTTP requests: `SerializeSessionAsync` after the run, `DeserializeSessionAsync` at the next request
- Why a conversation id is not a proof of ownership, and how to bind each stored conversation to the authenticated user
- How to protect the endpoint: `AddAuthentication().AddJwtBearer()`, `RequireAuthorization()`, input checks, problem details

## Prerequisites

- .NET 10 SDK or later
- An Azure OpenAI (or Microsoft Foundry) resource with a chat model deployment that supports function calling (for example `gpt-4o-mini` or `gpt-5.4-mini`)
- One of the following for authentication:
  - the resource **API key**, or
  - Azure CLI logged in (`az login`) with the **Cognitive Services OpenAI User** role on the resource
- Port **5200** free on your machine (or change `WebApi:BaseUrl`)
- Lab 01 (client and agent), Lab 03 (function tools, services in tools) and Lab 05 (sessions and their serialization); Lab 09 is useful

## Project Structure

```
Lab13-AIAgentInAspNetCoreApi/
├── README.md
├── Start/                              <-- Your working folder
│   ├── Program.cs                      <-- Complete the TODOs here (the API; the scenarios are provided)
│   ├── Tools/EmployeeDirectory.cs      <-- Provided: the tool get_my_profile (user from the token)
│   ├── Storage/ConversationStore.cs    <-- Provided: where the serialized sessions are kept (in memory)
│   ├── Auth/DevTokenIssuer.cs          <-- Provided: development tokens for the lab users (stands in for Entra ID)
│   ├── ApiConsole.cs                   <-- Provided: the client of the scenarios (HTTP + Server-Sent Events)
│   ├── ChatRequest.cs                  <-- Provided: the body of a message, { "message": "..." }
│   ├── ConfigurationHelper.cs
│   ├── AzureOpenAISettings.cs
│   ├── WebApiSettings.cs
│   ├── appsettings.json
│   └── AIAgentWebApi.csproj
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
     "WebApi": {
       "BaseUrl": "http://localhost:5200"
     }
   }
   ```

   Use the resource endpoint shown in the Azure portal. The `/openai/v1/` suffix required by the v1 API is added for you.

2. Choose an authentication method:

   - **API key** (simplest for local development) — store it as a user secret, **never** in `appsettings.json`:

     ```bash
     cd Start
     dotnet user-secrets set "AzureOpenAI:APIKey" "<your-api-key>"
     ```

     The user secrets id is shared by all the labs: if you already did it for a previous lab, there is nothing to do.
     You can also use the environment variable `AzureOpenAI__APIKey`.

   - **Microsoft Entra ID** — leave `APIKey` unset and run `az login`. `DefaultAzureCredential` picks up your Azure CLI identity.

3. **Or use the dashboard**: the *Azure OpenAI settings* of the optional [Lab Bench dashboard](../../../Dashboard/README.md#azure-openai-settings) write the endpoint, the deployment and the API key to the same user secrets, for every migrated lab.

### Step 2: Look at the provided files

- `Tools/EmployeeDirectory.cs` — `GetMyProfile(IServiceProvider services)`, the tool `get_my_profile`. It has **no user parameter**: it reads the user id (`sub` claim) of the HTTP request from `IHttpContextAccessor`, so the model cannot ask for someone else's profile. The `IServiceProvider` parameter is filled by the framework and hidden from the model (Lab 03, scenario 3).
- `Storage/ConversationStore.cs` — `TryGet(key, out serializedSession)` and `Save(key, serializedSession)`: the serialized sessions (`JsonElement`) in a `ConcurrentDictionary`. In production, the same two operations go to a database.
- `Auth/DevTokenIssuer.cs` — creates signed JWT tokens (`CreateToken("alice")`) with a random key, and gives the API the matching `TokenValidationParameters`. It stands in for an identity provider so that the lab runs without setup: **never issue tokens yourself in production**.
- `ApiConsole.cs` — `SendMessageAsync(httpClient, token, conversationId, message)`: the client of the scenarios. It posts the message with the bearer token, displays the HTTP status, then reads the Server-Sent Events as they arrive (`SseParser`) and displays the answer. It never prints a token.

### Step 3: Complete the Program.cs

Open `Start/Program.cs` and complete the TODOs. The program already starts the API and plays the scenarios: until TODO 9 the API has no endpoint, so every scenario gets `404 NotFound`.

---

#### Setup: Configuration and Client Initialization

Same as Lab 01.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 1** | Create the `OpenAIClientOptions` for the Azure OpenAI v1 endpoint | • `new OpenAIClientOptions { Endpoint = ... }` <br> • `AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint)` (from `CommonUtilities`) returns the `.../openai/v1/` URI |
| **TODO 2** | Create the `OpenAIClient` | • API key: `new OpenAIClient(new ApiKeyCredential(settings.APIKey), clientOptions)` <br> • Entra ID: `new OpenAIClient(new BearerTokenPolicy(new DefaultAzureCredential(), "https://ai.azure.com/.default"), clientOptions)` <br> • Use a conditional on `string.IsNullOrWhiteSpace(settings.APIKey)` to support both |
| **TODO 3** | Get a `ChatClient` for the deployment | • `ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);` |

---

#### The API: services

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 4** | Register the agent in dependency injection | • `builder.Services.AddHttpContextAccessor();` (used by the tool) <br> • `builder.Services.AddSingleton<AIAgent>(serviceProvider => chatClient.AsAIAgent(instructions: "You are the assistant of the company intranet. Answer in one or two sentences. Use get_my_profile for any question about the user's team or office.", name: "IntranetAssistant", tools: [AIFunctionFactory.Create(EmployeeDirectory.GetMyProfile, "get_my_profile")], services: serviceProvider));` <br> • A **singleton**: the agent holds no conversation, the sessions do (official sample `Agent_Step06_DependencyInjection` registers it the same way) |
| **TODO 5** | Register the conversation store | • `builder.Services.AddSingleton<ConversationStore>();` |
| **TODO 6** | Register the authentication and the authorization | • `builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => { options.MapInboundClaims = false; options.TokenValidationParameters = tokenIssuer.ValidationParameters; });` <br> • `builder.Services.AddAuthorization();` <br> • `MapInboundClaims = false` keeps the claim names of the token: the user id stays `sub` |
| **TODO 7** | Return errors as problem details | • `builder.Services.AddProblemDetails();` |

---

#### The API: middleware and endpoint

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 8** | Add the error handling middleware | • `app.UseExceptionHandler();` then `app.UseStatusCodePages();` (needs TODO 7) <br> • An unhandled exception becomes a `500` problem details, without the exception message |
| **TODO 9** | Map the endpoint | See the code below. `{conversationId:guid}`: an id that is not a GUID never reaches the endpoint (`404`) |
| **TODO 10** | `AgentEndpoint.StreamAnswerAsync` (bottom of `Program.cs`) — load, stream, save | See the code below. Remove the `yield break;` line |

**TODO 9:**

```csharp
app.MapPost("/api/conversations/{conversationId:guid}/messages",
    IResult (Guid conversationId, ChatRequest request, ClaimsPrincipal user, AIAgent agent, ConversationStore store, CancellationToken cancellationToken) =>
    {
        // The message comes from the caller: check it before it reaches the model (and your token budget)
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > 2000)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["message"] = ["The message must contain 1 to 2000 characters."] });
        }

        // The conversation belongs to the authenticated user: user id of the token + conversation id.
        // Never fall back to an empty user id: all the users would share the same conversations
        string userId = user.FindFirstValue("sub") ?? throw new InvalidOperationException("The token has no 'sub' claim.");
        string storageKey = $"{userId}/{conversationId}";

        return TypedResults.ServerSentEvents(AgentEndpoint.StreamAnswerAsync(agent, store, storageKey, request.Message, cancellationToken));
    })
    .RequireAuthorization();
```

The parameters are bound by ASP.NET Core: `conversationId` from the route, `request` from the JSON body, `user` from the validated token, `agent` and `store` from dependency injection, `cancellationToken` from the request (it is cancelled when the client disconnects, which stops the model call).

**TODO 10:**

```csharp
AgentSession session = store.TryGet(storageKey, out JsonElement serializedSession)
    ? await agent.DeserializeSessionAsync(serializedSession, cancellationToken: cancellationToken)
    : await agent.CreateSessionAsync(cancellationToken);

await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(message, session, cancellationToken: cancellationToken))
{
    if (!string.IsNullOrEmpty(update.Text))
    {
        yield return update.Text;
    }
}

store.Save(storageKey, await agent.SerializeSessionAsync(session, cancellationToken: cancellationToken));
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

To run only some scenarios, edit `scenariosToRun` at the top of `Program.cs`. Scenarios 3 and 4 continue the conversation of scenario 2: run them with it.

### Step 5 (optional): Call the API yourself

```bash
cd Solution
dotnet run -- --serve
```

The API keeps running and prints a development token for `alice` and one for `bob` (valid 30 minutes, signed with a key that disappears when the program stops). In another terminal:

```bash
TOKEN="<token of alice>"
CONVERSATION=$(uuidgen)
curl -N -X POST "http://localhost:5200/api/conversations/$CONVERSATION/messages" \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"message":"Which team am I in?"}'
```

`-N` turns off the buffering of curl: the `data:` lines appear one by one, as the model writes. Send a second message with the same `$CONVERSATION`, then with the token of `bob`.

## How it works

```
Front end (ApiConsole)                      ASP.NET Core API (this program)
──────────────────────                      ───────────────────────────────────────────────────────────────
POST /api/conversations/{id}/messages  ──▶  JWT bearer authentication ── no or invalid token ──▶ 401
Authorization: Bearer <token>               RequireAuthorization()
{ "message": "..." }                        endpoint: check the message ─────── invalid ───────▶ 400
                                            storage key = "<sub of the token>/<id>"
                                            AgentEndpoint.StreamAnswerAsync
                                              ├─ ConversationStore.TryGet(key) → DeserializeSessionAsync
                                              │  (or CreateSessionAsync for a new conversation)
                                              ├─ agent.RunStreamingAsync(message, session)
                                              │    └─ get_my_profile → IHttpContextAccessor → "sub"
                         ◀── data: ...  ───   │  each text update = one Server-Sent Event
                         ◀── data: ...  ───   │
                                              └─ SerializeSessionAsync → ConversationStore.Save(key)
```

- **One agent, many users.** The `AIAgent` is a singleton: it holds the instructions, the tools and the chat client, never a conversation. Each request works on **its own session**, loaded from the store and saved back.
- **The session travels as JSON.** `SerializeSessionAsync` turns the session (here, the chat history kept by the default `InMemoryChatHistoryProvider`) into a `JsonElement`; the next request rebuilds it with `DeserializeSessionAsync`. *"Sessions are agent/service-specific"*: restore a session with the same agent configuration.
- **Streaming.** `TypedResults.ServerSentEvents(...)` answers with `Content-Type: text/event-stream` and writes one `data:` event per string of the `IAsyncEnumerable`, as soon as it is produced. The session is saved after the last update: if the client disconnects before the end, the request is cancelled and that turn is not saved.
- **The tool acts for the token, not for the model.** `get_my_profile` has no parameter: whatever the prompt says, it returns the profile of the user of the token. Alice cannot get Bob's profile by asking for it.

## Security

- **Authenticate every request.** `RequireAuthorization()` refuses requests without a valid token **before** the agent runs: no model call, no cost. In production, validate the tokens of your identity provider (for Microsoft Entra ID: `AddMicrosoftIdentityWebApi` of `Microsoft.Identity.Web`, or `AddJwtBearer` with the authority of your tenant); the development issuer of the lab is only a stand-in.
- **A conversation id is not an ownership proof.** It comes from the client: anyone can send any id. The storage key includes the user id of the token, so another user who sends the same id gets a different conversation (scenario 4). The official guidance is the same for service-side ids: *"verify the authenticated user or tenant before resuming a conversation"* ([Session](https://learn.microsoft.com/agent-framework/concepts/agents/conversations/session?pivots=programming-language-csharp)).
- **Authorize in the tools.** A tool that acts for a user takes the user from the request context, never from an argument chosen by the model (Lab 03, *Designing safe tools*).
- **Check the input.** Limit the size of the message (cost, denial of service), and add a rate limit per user in production (`builder.Services.AddRateLimiter(...)` and `RequireRateLimiting(...)`).
- **Do not leak errors.** Problem details without exception messages; log the details on the server, not in the response.
- **HTTPS.** Bearer tokens must only travel over HTTPS: the lab uses `http://localhost` for simplicity.
- **Stored conversations are personal data.** They hold everything the users wrote: protect the database, set a retention period, and delete a user's conversations when they ask (Lab 05, *Security of stored sessions*).

## Key Concepts

| Concept | Description |
|---------|-------------|
| `WebApplication` | The ASP.NET Core application: services (`builder.Services`), middleware (`app.Use...`), endpoints (`app.Map...`) |
| Singleton `AIAgent` | One agent for all the requests: it is stateless, the conversation lives in the session |
| `services:` of `AsAIAgent` | The `IServiceProvider` given to the tools that declare an `IServiceProvider` parameter (hidden from the model) |
| `IHttpContextAccessor` | Gives the current HTTP request (and its `User`) to code that is not an endpoint, such as a tool |
| JWT bearer authentication | `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)`: the `Authorization: Bearer <token>` header is validated (issuer, audience, signature, expiry) |
| `RequireAuthorization()` | The endpoint needs an authenticated user; otherwise `401` |
| `ClaimsPrincipal` / `sub` | The user of the request and the claim that identifies them |
| `TypedResults.ServerSentEvents` | .NET 10 result that streams an `IAsyncEnumerable<T>` as Server-Sent Events (`text/event-stream`) |
| `RunStreamingAsync` | Runs the agent and returns its answer as a stream of `AgentResponseUpdate` |
| `SerializeSessionAsync` / `DeserializeSessionAsync` | Turn a session into JSON and back, to keep a conversation between requests |
| Problem details | Standard error body (`application/problem+json`, RFC 9457): `AddProblemDetails()`, `UseExceptionHandler()`, `ValidationProblem` |
| `SseParser` | Client side: reads the events of a `text/event-stream` response (`System.Net.ServerSentEvents`) |

## Namespaces Reference

| Namespace | Purpose |
|-----------|---------|
| `Microsoft.AspNetCore.Builder` | `WebApplication`, `MapPost()`, `UseExceptionHandler()`, `UseStatusCodePages()` |
| `Microsoft.AspNetCore.Http` | `TypedResults`, `IResult`, `IHttpContextAccessor` |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | `JwtBearerDefaults`, `AddJwtBearer()` |
| `Microsoft.Extensions.DependencyInjection` | `AddSingleton()`, `AddHttpContextAccessor()`, `AddAuthentication()`, `AddAuthorization()`, `AddProblemDetails()` |
| `Microsoft.Extensions.Hosting` | `WaitForShutdownAsync()` (`--serve` mode) |
| `System.Security.Claims` | `ClaimsPrincipal`, `FindFirstValue()` |
| `System.Runtime.CompilerServices` | `[EnumeratorCancellation]` |
| `System.Text.Json` | `JsonElement` (a serialized session) |
| `Microsoft.Agents.AI` | `AIAgent`, `AgentSession`, `AgentResponseUpdate`, `SerializeSessionAsync()`, `DeserializeSessionAsync()` |
| `Microsoft.Extensions.AI` | `AIFunctionFactory` |
| `OpenAI`, `OpenAI.Chat`, `System.ClientModel`, `System.ClientModel.Primitives`, `Azure.Identity` | The Azure OpenAI client, as in Lab 01 |
| `CommonUtilities` | `ColoredConsole`, `AzureOpenAIEndpoint` |

## Expected Output

```
Endpoint: https://your-resource.openai.azure.com/
Deployment: your-deployment-name
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5200
...
API: POST http://localhost:5200/api/conversations/{conversationId}/messages (bearer token required)
-------------------------------------------------------------------------------
=== Scenario 1: Requests refused before the agent runs ===
POST /api/conversations/7045ff93-cda8-4630-834f-2b687ceac0ce/messages: Hello, which team am I in?
HTTP 401 Unauthorized (no content)
POST /api/conversations/4eaebd6c-e281-4f45-9b2e-4eeb2f140ea6/messages:
HTTP 400 BadRequest (application/problem+json)
-------------------------------------------------------------------------------
=== Scenario 2: Alice - a streamed answer, with a tool that knows who she is ===
POST /api/conversations/3000ec4f-8bd8-44b6-8be3-316183fb873c/messages: Which team am I in? Please also remember that my favorite meeting room is ROOM-B.
HTTP 200 OK (text/event-stream)
Agent: You are in the Engineering team, and your office is in Paris.
Server-sent events received: 14
-------------------------------------------------------------------------------
=== Scenario 3: Alice - the conversation continues in another request ===
POST /api/conversations/3000ec4f-8bd8-44b6-8be3-316183fb873c/messages: Which meeting room do I prefer?
HTTP 200 OK (text/event-stream)
Agent: Your favorite meeting room is ROOM-B.
Server-sent events received: 8
-------------------------------------------------------------------------------
=== Scenario 4: Bob - the same conversation id, another user ===
POST /api/conversations/3000ec4f-8bd8-44b6-8be3-316183fb873c/messages: Which meeting room do I prefer, and which team am I in?
HTTP 200 OK (text/event-stream)
Agent: You are in the Sales team and your office is in Lyon. Please let me know if you need anything else!
Server-sent events received: 23
info: Microsoft.Hosting.Lifetime[0]
      Application is shutting down...
```

The ids are random; the wording and the number of events vary. In scenario 4, Bob gets **his** team (Sales) and no trace of ROOM-B: the conversation of Alice is not his.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `'AzureOpenAI:Endpoint' is not configured` | Replace the `YOUR-...` placeholders in `appsettings.json` (or use user secrets / environment variables) |
| `Failed to bind to address http://127.0.0.1:5200: address already in use` | Another program uses the port: stop it, or set `WebApi__BaseUrl=http://localhost:5250` |
| Every scenario gets `404 NotFound` | The endpoint is not mapped yet (TODO 9) |
| Scenario 1: the request without a token is not refused with `401` | `.RequireAuthorization()` is missing on the endpoint (TODO 9) |
| `401` with a token (scenarios 2–4) | The authentication does not use the keys of the issuer: check `options.TokenValidationParameters = tokenIssuer.ValidationParameters` (TODO 6) |
| `InvalidOperationException` about the exception handler at startup | `UseExceptionHandler()` needs `AddProblemDetails()` (TODO 7 before TODO 8) |
| `500` with a valid token, and *"The token has no 'sub' claim"* in the server log | `options.MapInboundClaims = false` is missing: by default the JWT handler renames `sub` to the long `nameidentifier` claim type (TODO 6) |
| Scenario 2 answers that it cannot know the team | The tool is not given to the agent, or `services: serviceProvider` is missing: the tool cannot resolve `IHttpContextAccessor` (TODO 4) |
| Scenario 3 does not remember ROOM-B | The session is not saved after the run, or not loaded at the next request (TODO 10) |
| Scenario 4: Bob knows ROOM-B | The storage key does not contain the user id (TODO 9): this is the vulnerability the lab is about |
| The answer arrives all at once | Something buffers the response (a proxy, or curl without `-N`); the API itself writes each event as soon as it is produced |

## Provided Files

The following files are provided and should not be modified:

- `AIAgentWebApi.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs`, `AzureOpenAISettings.cs`, `WebApiSettings.cs` - Configuration
- `Tools/EmployeeDirectory.cs` - The tool of the agent
- `Storage/ConversationStore.cs` - The store of the serialized sessions
- `Auth/DevTokenIssuer.cs` - Development tokens
- `ChatRequest.cs` - The body of a message
- `ApiConsole.cs` - The client of the scenarios

The following files should be modified:

- `appsettings.json` - Update with your Azure OpenAI endpoint and deployment
- `Program.cs` - Complete the TODOs

### NuGet packages

| Package | Why |
|---------|-----|
| `Microsoft.AspNetCore.App` (framework reference) | ASP.NET Core: `WebApplication`, minimal APIs, `TypedResults.ServerSentEvents`, problem details |
| `Microsoft.Agents.AI.OpenAI` | Agent Framework + `AsAIAgent()` (brings `OpenAI` and `Microsoft.Extensions.AI`) |
| `Microsoft.Extensions.AI.OpenAI` 10.11.0 | Streaming fix: Agent Framework 1.26.0 still brings 10.10.x, which fails on the annotation chunks without `delta` that Azure sends with the asynchronous content filter ([dotnet/extensions#7790](https://github.com/dotnet/extensions/issues/7790)). The API streams every answer |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | JWT bearer authentication (brings `Microsoft.IdentityModel.JsonWebTokens`, used by `DevTokenIssuer`) |
| `Azure.Identity` | `DefaultAzureCredential` for Entra ID authentication |

Every package is stable.

## Going Further

- **The hosting library of Agent Framework** (preview): `builder.AddAIAgent("name", instructions: ...)` registers a named agent in dependency injection, with `.WithAITool(...)` and a session store; protocol adapters then expose it, for example `app.MapOpenAIResponses(agent)` (package `Microsoft.Agents.AI.Hosting.OpenAI`, **alpha**) or A2A (Lab06_A2AServer). See [Step 7: Host your agent](https://learn.microsoft.com/agent-framework/get-started/hosting?pivots=programming-language-csharp).
- **A persistent store**: replace `ConversationStore` with a database (Cosmos DB, SQL, Redis), or keep the chat history in a `ChatHistoryProvider` that writes to a database, so that the serialized session only holds a key (Lab 05, scenario 3).
- **Microsoft Entra ID**: protect the API with `Microsoft.Identity.Web` (`AddMicrosoftIdentityWebApi`), and use the `oid` or `sub` claim of the Entra ID token as the user id.
- **Rate limiting**: `builder.Services.AddRateLimiter(...)` with a partition per user, and `.RequireRateLimiting("per-user")` on the endpoint.
- **Observe the API**: add the OpenTelemetry instrumentation of Lab 11 (`UseOpenTelemetry`) and `AddAspNetCoreInstrumentation()`: each HTTP request becomes a trace that contains the spans of the agent.
- **Human approval**: an agent with `ApprovalRequiredAIFunction` tools (Lab 09) returns approval requests; over HTTP, the front end shows them and sends the decisions in the next request of the same conversation.

## Useful Links

- [Step 7: Host your agent](https://learn.microsoft.com/agent-framework/get-started/hosting?pivots=programming-language-csharp)
- [Session](https://learn.microsoft.com/agent-framework/concepts/agents/conversations/session?pivots=programming-language-csharp) and [Storage](https://learn.microsoft.com/agent-framework/concepts/agents/conversations/storage?pivots=programming-language-csharp)
- Official samples [`Agent_Step06_DependencyInjection`](https://github.com/microsoft/agent-framework/tree/dotnet-1.26.0/dotnet/samples/02-agents/Agents/Agent_Step06_DependencyInjection) and [`Agent_Step03_PersistedConversations`](https://github.com/microsoft/agent-framework/tree/dotnet-1.26.0/dotnet/samples/02-agents/Agents/Agent_Step03_PersistedConversations)
- [Minimal APIs](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis) and [Server-Sent Events in minimal APIs](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/responses#server-sent-events-serversentevents)
- [Configure JWT bearer authentication in ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/authentication/configure-jwt-bearer-authentication)
- [Azure OpenAI v1 API](https://learn.microsoft.com/azure/foundry/openai/api-version-lifecycle)

## Solution

If you get stuck, check the complete solution in the `Solution/` folder.
