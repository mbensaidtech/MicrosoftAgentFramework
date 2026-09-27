# Lab 03 - AI Agent With Function Tools

## Objective

In this lab, you will create an AI Agent that uses **Function Tools** (also known as *function calling*) with **Microsoft Agent Framework** and **Azure OpenAI**.

A function tool is a .NET method that the agent can decide to call while it answers: it gives the agent access to data and systems the model cannot know (employees, meeting rooms, notifications...). You will register tools one by one, discover them with reflection, give tools access to services through dependency injection, and trace every tool call with a function calling middleware.

## What You Will Learn

- How to turn a .NET method into a tool with `AIFunctionFactory.Create()`, and how `[Description]` attributes describe the tool to the model
- How to give tools to an agent with `AsAIAgent(..., tools: ...)`
- How to discover tools with reflection (`MethodInfo` + `AIFunctionFactory.Create(method, target)`)
- How to let tools resolve services: `IServiceProvider` parameter + `AsAIAgent(..., services: ...)`
- How to intercept every tool call with a function calling middleware (`AsBuilder().Use(...)`)
- Why the token usage of a run with tools covers several model calls

## Prerequisites

- .NET 10 SDK or later
- An Azure OpenAI (or Microsoft Foundry) resource with a chat model deployment that supports function calling (for example `gpt-4o-mini` or `gpt-5.4-mini`)
- One of the following for authentication:
  - the resource **API key**, or
  - Azure CLI logged in (`az login`) with the **Cognitive Services OpenAI User** role on the resource
- Lab 01 completed (client creation, `AsAIAgent()`, `AgentResponse`)

## Project Structure

```
Lab03-AIAgentWithFunctionTools/
├── README.md
├── Start/                          <-- Your working folder
│   ├── Program.cs                  <-- Complete the TODOs here
│   ├── Tools/
│   │   ├── CompanyTools.cs         <-- Employee & meeting room tools (instance methods)
│   │   └── NotificationTools.cs    <-- Notification tools (static methods using IServiceProvider)
│   ├── Repositories/
│   │   ├── INotificationRepository.cs
│   │   └── InMemoryNotificationRepository.cs
│   ├── AgentConsole.cs             <-- Display helper (provided)
│   ├── ConfigurationHelper.cs
│   ├── AzureOpenAISettings.cs
│   ├── appsettings.json
│   └── AIAgentWithFunctionTools.csproj
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

- `Tools/CompanyTools.cs` — the company "internal systems". Each public method is a tool candidate: `GetEmployeeInfo`, `GetMeetingRooms`, `BookMeetingRoom`. The `[Description]` attributes on the methods and on their parameters are sent to the model with the tool: the method description helps the model decide **when** to call it, the parameter descriptions tell it **what** to pass (for example the `yyyy-MM-dd` date format). The private methods are there to show that reflection only exposes what you ask for (scenario 2).
- `Tools/NotificationTools.cs` — **static** tools whose first parameter is an `IServiceProvider`: they resolve the `INotificationRepository` from it (scenario 3).
- `Repositories/` — the notification store used by `NotificationTools`.
- `AgentConsole.cs` — `WriteTokenUsage(response)` displays the token usage of a run. `Program.cs` imports it with `using static`, so you can call it directly.

### Step 3: Complete the Program.cs

Open `Start/Program.cs` and complete the TODOs:

---

#### Setup: Configuration and Client Initialization

Same as Lab 01.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 1** | Create the `OpenAIClientOptions` for the Azure OpenAI v1 endpoint | • `new OpenAIClientOptions { Endpoint = ... }` <br> • `AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint)` (from `CommonUtilities`) returns the `.../openai/v1/` URI |
| **TODO 2** | Create the `OpenAIClient` | • API key: `new OpenAIClient(new ApiKeyCredential(settings.APIKey), clientOptions)` <br> • Entra ID: `new OpenAIClient(new BearerTokenPolicy(new DefaultAzureCredential(), "https://ai.azure.com/.default"), clientOptions)` <br> • Use a conditional on `string.IsNullOrWhiteSpace(settings.APIKey)` to support both |
| **TODO 3** | Get a `ChatClient` for the deployment | • `client.GetChatClient(settings.ChatDeploymentName)` |

---

#### Scenario 1: Function tools calling - basic

You create each tool yourself with `AIFunctionFactory.Create()`, from a method of a `CompanyTools` instance, and give it the name the model will see.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 4** | Create the tools | • `var companyTools = new CompanyTools();` <br> • `List<AITool> tools = [ ... ];` with `AIFunctionFactory.Create(companyTools.GetEmployeeInfo, "get_employee_info")`, `AIFunctionFactory.Create(companyTools.GetMeetingRooms, "get_meeting_rooms")` and `AIFunctionFactory.Create(companyTools.BookMeetingRoom, "book_meeting_room")` |
| **TODO 5** | Display what the model receives for each tool | • `ColoredConsole.WritePrimaryLogLine("Tools available to the agent:");` <br> • `foreach (AITool tool in tools) { ColoredConsole.WriteSecondaryLogLine($"- {tool.Name}: {tool.Description}"); }` <br> • The description is the `[Description]` of the method |
| **TODO 6** | Create the agent with its tools | • `AIAgent companyAgent = chatClient.AsAIAgent(instructions: "You are a helpful assistant that can help with company tasks.", name: "CompanyAssistant", tools: tools);` |
| **TODO 7** | Run the agent and display the answer | • `AgentResponse response = await companyAgent.RunAsync("Get the information of the employee with the ID EMP001").WithSpinner("Running agent");` <br> • `ColoredConsole.WriteSecondaryLogLine(response.Text);` <br> • The agent calls `get_employee_info` by itself: you don't call the tool, the framework does |
| **TODO 8** | Display token usage | • `WriteTokenUsage(response)` |

---

#### Scenario 2: Function tools calling - using reflection

Instead of listing the methods one by one, you discover all the public instance methods of `CompanyTools` and register them as tools. Without an explicit name, a tool is named after its method (`GetEmployeeInfo`).

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 9** | Get the public instance methods | • `var companyTools = new CompanyTools();` <br> • `MethodInfo[] methods = typeof(CompanyTools).GetMethods(BindingFlags.Public \| BindingFlags.Instance \| BindingFlags.DeclaredOnly);` <br> • `DeclaredOnly` excludes the methods inherited from `object` (`ToString`, `GetHashCode`...) |
| **TODO 10** | Create one tool per method | • `List<AITool> tools = [.. methods.Select(method => AIFunctionFactory.Create(method, companyTools))];` <br> • The second argument is the instance the method is invoked on |
| **TODO 11** | Display the names of the tools | • `ColoredConsole.WriteSecondaryLogLine($"Tools that will be available to the agent: {string.Join(", ", tools.Select(tool => tool.Name))}");` |
| **TODO 12** | Create the agent | • Same as TODO 6 with the discovered `tools` |
| **TODO 13** | Run the agent and display the answer | • Prompt: `"What are the available meeting rooms? Then book the room ROOM-A for the employee with the ID EMP001 on 2025-12-16 from 10:00 to 11:00 for a 'Sprint Review'."` <br> • Same pattern as TODO 7 (`RunAsync(...).WithSpinner("Running agent")`, then `response.Text`) |
| **TODO 14** | Display token usage | • `WriteTokenUsage(response)` |

---

#### Scenario 3: Function tools calling - static tools with dependency injection

Tools often need services (a repository, an HTTP client...). The agent can receive an `IServiceProvider`: when a tool has an `IServiceProvider` parameter, the framework fills it with this provider. This parameter is **not** part of the schema sent to the model, which only sees `recipient` and `message`.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 15** | Register the services | • `ServiceCollection services = new();` <br> • `services.AddSingleton<INotificationRepository, InMemoryNotificationRepository>();` |
| **TODO 16** | Build the service provider | • `IServiceProvider serviceProvider = services.BuildServiceProvider();` |
| **TODO 17** | Create the agent with the static tools and the service provider | • `AIAgent notificationAgent = chatClient.AsAIAgent(instructions: "You are a helpful assistant that can send and retrieve notifications.", name: "NotificationAssistant", tools: [ ... ], services: serviceProvider);` <br> • Tools: `AIFunctionFactory.Create(NotificationTools.SendNotification, "send_notification")` and `AIFunctionFactory.Create(NotificationTools.GetNotificationsForRecipient, "get_notifications_for_recipient")` |
| **TODO 18** | Run the agent and display the answer | • Prompt: `"Send a notification to 'Mohammed' with the message 'Meeting at 3pm tomorrow'. Then show me all notifications for Mohammed."` <br> • Same pattern as TODO 7 |
| **TODO 19** | Display token usage | • `WriteTokenUsage(response)` |

---

#### Scenario 4: Function calling middleware - trace every tool call

A **function calling middleware** is called for every tool call made by the agent: it sees the function and the arguments chosen by the model, calls the next step (in the end, the tool itself), and can inspect or replace the result. It is added **around an existing agent**, without changing the agent or the tools: useful for logging, validation, or overriding a result.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 20** | Write the middleware (local function at the end of `Program.cs`) | • Signature (already in the skeleton): `async ValueTask<object?> FunctionCallMiddleware(AIAgent agent, FunctionInvocationContext context, Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next, CancellationToken cancellationToken)` <br> • `string arguments = string.Join(", ", context.Arguments.Select(argument => $"{argument.Key}: {argument.Value}"));` <br> • `ColoredConsole.WriteWarningLine($"[Middleware] Calling {context.Function.Name}({arguments})");` <br> • `object? result = await next(context, cancellationToken);` <br> • `ColoredConsole.WriteWarningLine($"[Middleware] {context.Function.Name} returned: {result?.ToString()?.Split('\n')[0]}");` <br> • `return result;` |
| **TODO 21** | Create the agent of scenario 1 and wrap it with the middleware | • `var companyTools = new CompanyTools();` then the same `AsAIAgent(...)` call as TODO 6, with the three tools of TODO 4 written directly in `tools: [ ... ]` <br> • `AIAgent tracedAgent = companyAgent.AsBuilder().Use(FunctionCallMiddleware).Build();` |
| **TODO 22** | Run the wrapped agent and display the answer | • `AgentResponse response = await tracedAgent.RunAsync("Get the information of the employee EMP003, then book the room ROOM-B for this employee on 2026-10-15 from 14:00 to 15:00 for a 'Design Review'.");` <br> • **No** `.WithSpinner()` here: the spinner would overwrite the lines written by the middleware <br> • `ColoredConsole.WriteSecondaryLogLine(response.Text);` |
| **TODO 23** | Display token usage | • `WriteTokenUsage(response)` |

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

To run only some scenarios, edit `scenariosToRun` at the top of `Program.cs` (for example `[4]`).

## How function calling works

1. The agent sends your prompt to the model **with the list of tools**: for each tool, its name, its description and the JSON schema of its parameters (generated from the method signature and its `[Description]` attributes).
2. The model does not run anything: it answers with one or more **function calls** (tool name + arguments as JSON).
3. The agent (through the `FunctionInvokingChatClient` that `AsAIAgent()` adds for you) invokes the matching .NET methods, then sends the results back to the model.
4. Steps 2–3 repeat until the model answers with text. `RunAsync` returns this final answer.

That is why a run with tools uses more tokens than a simple question: its `Usage` is the **total** of all the model calls of the loop (scenario 4 makes two or three tool calls, hence several round trips).

## Key Concepts

| Concept | Description |
|---------|-------------|
| Function tool | A .NET method the agent can decide to call while it answers |
| `AIFunctionFactory.Create()` | Creates an `AIFunction` from a delegate (instance or static method) or from a `MethodInfo` + target instance; the optional name overrides the method name |
| `AITool` / `AIFunction` | Base type of the tools given to an agent / a tool implemented by .NET code. `Name` and `Description` are what the model sees |
| `[Description]` | `System.ComponentModel` attribute: description of the tool (on the method) and of its parameters, copied into the tool's JSON schema |
| `AsAIAgent(..., tools: ...)` | Creates an agent that can call the tools. The function calling loop is handled for you |
| `AsAIAgent(..., services: ...)` | Gives an `IServiceProvider` to the agent; tools that declare an `IServiceProvider` parameter receive it (hidden from the model) |
| `ServiceCollection` / `IServiceProvider` | The `Microsoft.Extensions.DependencyInjection` container and the object that resolves the services |
| `BindingFlags` | Reflection filter: `Public \| Instance \| DeclaredOnly` = public instance methods declared by the class itself |
| Function calling middleware | `agent.AsBuilder().Use(callback).Build()`: a callback called around every tool call |
| `FunctionInvocationContext` | What the middleware sees: `Function`, `Arguments`, `Iteration`, `Terminate` (stop the function calling loop) |

## Namespaces Reference

| Namespace | Purpose |
|-----------|---------|
| `OpenAI` | `OpenAIClient`, `OpenAIClientOptions` |
| `OpenAI.Chat` | `ChatClient` and the `AsAIAgent()` extension method |
| `System.ClientModel` | `ApiKeyCredential` |
| `System.ClientModel.Primitives` | `BearerTokenPolicy` |
| `Azure.Identity` | `DefaultAzureCredential` |
| `Microsoft.Agents.AI` | `AIAgent`, `AgentResponse`, `AsBuilder()`, and the `Use(...)` overload for function calling middleware |
| `Microsoft.Extensions.AI` | `AIFunctionFactory`, `AITool`, `FunctionInvocationContext` |
| `Microsoft.Extensions.DependencyInjection` | `ServiceCollection`, `AddSingleton()`, `BuildServiceProvider()`, `GetRequiredService()` |
| `System.Reflection` | `MethodInfo`, `BindingFlags` |
| `System.ComponentModel` | `[Description]` (in `Tools/`) |
| `CommonUtilities` | `ColoredConsole`, `WithSpinner()`, `AzureOpenAIEndpoint` |

## Expected Output

```
Endpoint: https://your-resource.openai.azure.com/
Deployment: your-deployment-name
----------------------------------------
=== Scenario 1: Function tools calling - basic ===
Tools available to the agent:
- get_employee_info: Retrieves detailed information about an employee using their employee ID (e.g., EMP001, EMP002).
- get_meeting_rooms: Lists all available meeting rooms with their capacity and features.
- book_meeting_room: Books a meeting room for a specific date, time, and subject.
Here is the information for the employee with ID EMP001:
- Name: Mohammed BEN SAID
- Department: Engineering
...
----------------------------------------
Token Usage:
  Input tokens: 507
  Output tokens: 75
  Total tokens: 582
----------------------------------------
=== Scenario 2: Function tools calling - using reflection ===
Tools that will be available to the agent: GetEmployeeInfo, GetMeetingRooms, BookMeetingRoom
Here are the available meeting rooms:
...
The Innovation Lab (ROOM-A) has been successfully booked for the employee with ID EMP001 on 2025-12-16 from 10:00 to 11:00 ...
----------------------------------------
Token Usage:
  Input tokens: 1110
  ...
----------------------------------------
=== Scenario 3: Function tools calling - static tools with DI ===
The notification has been sent to Mohammed with the message: "Meeting at 3pm tomorrow".
Here are all notifications for Mohammed:
- [97576bb9] 14:17:57: Meeting at 3pm tomorrow
...
----------------------------------------
=== Scenario 4: Function calling middleware ===
[Middleware] Calling get_employee_info(employeeId: EMP003)
[Middleware] get_employee_info returned: Employee Found:
[Middleware] Calling book_meeting_room(roomId: ROOM-B, employeeId: EMP003, date: 2026-10-15, startTime: 14:00, endTime: 15:00, subject: Design Review)
[Middleware] book_meeting_room returned: Booking confirmed!
Employee Information:
- Name: Charlie Brown
...
----------------------------------------
Token Usage:
  Input tokens: 1276
  ...
```

Answers vary from one run to another. In scenario 4, the model sometimes also calls `get_meeting_rooms` before booking: the middleware shows it.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `'AzureOpenAI:Endpoint' is not configured` | Replace the `YOUR-...` placeholders in `appsettings.json` (or use user secrets / environment variables) |
| `401 Unauthorized` with an API key | Check the key (user secret `AzureOpenAI:APIKey`) and that it belongs to the resource of the endpoint |
| `401` / `403` with Entra ID | Run `az login` and make sure your identity has the **Cognitive Services OpenAI User** role on the resource |
| `404 DeploymentNotFound` | `ChatDeploymentName` must be the **deployment** name, not the model name |
| The agent answers without calling a tool | Check the `[Description]` of the method and that the tool is in `tools`. The model decides from the name and the description |
| Scenario 3: the agent says it *encountered an error* while sending the notification | A tool threw an exception: the run does not fail, the error is sent back to the model, which reports it. Check that the repository is registered (TODO 15) and that `services: serviceProvider` is passed to `AsAIAgent` (TODO 17) |
| `InvalidOperationException` mentioning `FunctionInvokingChatClient` | Function calling middleware only works on an agent that runs tools itself, such as the agents created with `AsAIAgent()` |
| The middleware lines are garbled | Remove `.WithSpinner()` from the run of scenario 4 |

## Provided Files

The following files are provided and should not be modified:

- `AIAgentWithFunctionTools.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs` - Loads and validates the configuration
- `AzureOpenAISettings.cs` - Settings class for Azure OpenAI
- `Tools/CompanyTools.cs` - Employee and meeting room tools, with their `[Description]` attributes
- `Tools/NotificationTools.cs` - Notification tools (static methods with an `IServiceProvider` parameter)
- `Repositories/INotificationRepository.cs` - Repository interface
- `Repositories/InMemoryNotificationRepository.cs` - In-memory implementation
- `AgentConsole.cs` - Display helper

The following files should be modified:

- `appsettings.json` - Update with your Azure OpenAI endpoint and deployment
- `Program.cs` - Complete the TODOs

### NuGet packages

| Package | Why |
|---------|-----|
| `Microsoft.Agents.AI.OpenAI` | Agent Framework + `AsAIAgent()` for OpenAI clients (brings `OpenAI` and `Microsoft.Extensions.AI`, which contains `AIFunctionFactory`) |
| `Azure.Identity` | `DefaultAzureCredential` for Entra ID authentication |
| `Microsoft.Extensions.Configuration.*` | `appsettings.json`, user secrets and environment variables, bound to `AzureOpenAISettings` |
| `Microsoft.Extensions.DependencyInjection` | `ServiceCollection` (scenario 3) |

## Going Further

- **Choose explicitly which methods are tools**: instead of reflection, a class can expose its tools itself (a method returning `IEnumerable<AITool>`), and the class can be resolved from the service provider with its own dependencies. See the official sample `Agent_Step12_Plugins`.
- **Tools for a single run**: `new ChatClientAgentRunOptions(new ChatOptions { Tools = [...] })` passed to `RunAsync` adds tools for that run only (official sample `Agent_Step11_Middleware`).
- **Other middleware types**: agent run middleware (`Use(runFunc: ..., runStreamingFunc: ...)`, inspect or change the messages of a run) and `IChatClient` middleware (`clientFactory:` in `AsAIAgent`). See [Agent Middleware](https://learn.microsoft.com/agent-framework/agents/middleware?pivots=programming-language-csharp).
- **Human approval before a tool runs**: `ApprovalRequiredAIFunction` — this is the topic of Lab 09.
- **An agent as a tool**: `agent.AsAIFunction()` turns an agent into a function tool for another agent (official sample `Agent_Step09_AsFunctionTool`).

## Coming from an older version of the lab?

| Before (preview) | Now (1.22.0) |
|------------------|--------------|
| `Azure.AI.OpenAI` / `AzureOpenAIClient` | `OpenAI` / `OpenAIClient` + Azure OpenAI v1 endpoint |
| `ChatClientAgent agent = chatClient.CreateAIAgent(..., tools: ..., services: ...)` | `AIAgent agent = chatClient.AsAIAgent(..., tools: ..., services: ...)` |
| `AgentRunResponse` | `AgentResponse` |
| `response.ToString()` | `response.Text` |
| `Microsoft.Extensions.Hosting` (brought `ServiceCollection` transitively) | `Microsoft.Extensions.DependencyInjection` referenced explicitly |
| Step 0: add `[Description]` attributes to `GetEmployeeInfo` | The attributes are provided in `CompanyTools.cs`; scenario 1 displays the description the model receives (TODO 5) |
| — | Scenario 4: function calling middleware |
| API key in `appsettings.json` | API key in user secrets or environment variables |

`AIFunctionFactory`, `AITool` and the `IServiceProvider` parameter of tools are unchanged: they come from `Microsoft.Extensions.AI`.

## Useful Links

- [Using function tools with an agent](https://learn.microsoft.com/agent-framework/agents/tools/function-tools?pivots=programming-language-csharp)
- [Agent Middleware](https://learn.microsoft.com/agent-framework/agents/middleware?pivots=programming-language-csharp)
- [Microsoft Agent Framework documentation](https://learn.microsoft.com/agent-framework/overview/?pivots=programming-language-csharp)
- [Official sample `02_add_tools`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/01-get-started/02_add_tools)
- [Official sample `Agent_Step12_Plugins`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step12_Plugins) (tools with dependency injection)
- [Official sample `Agent_Step11_Middleware`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step11_Middleware)
- [Azure OpenAI function calling](https://learn.microsoft.com/azure/foundry/openai/how-to/function-calling)
- [Azure OpenAI v1 API](https://learn.microsoft.com/azure/foundry/openai/api-version-lifecycle)

## Solution

If you get stuck, check the complete solution in the `Solution/` folder.
