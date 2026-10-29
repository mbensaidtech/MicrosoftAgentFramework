# Lab 09 - Control an Agent: Human Approval and Guardrails

## Objective

In this lab, you will build an AI Agent whose **sensitive tools require approval before they run**, with **Microsoft Agent Framework** and **Azure OpenAI**.

This is the **human-in-the-loop** pattern: when the model decides to call a tool marked as requiring approval, the agent does not invoke it. The run completes with an **approval request** instead of an answer. Your code shows the request to a person (or applies a policy), sends the decision back to the agent **in the same session**, and the agent then invokes the approved calls, or tells the model why a call was rejected, and answers.

Then you add **guardrails** around the agent with an **agent run middleware**: code that runs around every run of the agent, without changing the agent. It refuses a forbidden request before the model is even called, and masks personal data in the request and in the answer. Approvals and middleware are the two ways to add runtime controls to a .NET agent: *"Use agent middleware, tool approval, and agent safety to add runtime controls to .NET agents"* ([Agent Hooks](https://learn.microsoft.com/agent-framework/agents/agent-hooks?pivots=programming-language-csharp)).

## What You Will Learn

- How to mark a tool as requiring approval with `ApprovalRequiredAIFunction`
- How an agent run pauses: the `ToolApprovalRequestContent` items in `response.Messages`
- How to show the user what the model wants to do (`ToolCall` is the `FunctionCallContent`: name and arguments)
- How to answer with `CreateResponse(approved, reason)` in a user `ChatMessage`, and continue the run with the same `AgentSession`
- Why the session is mandatory (approval binding) and why only the sensitive tools ask for approval
- How to run the approval loop until the agent no longer asks, how to see what the tools returned, and what a rejection costs in tokens
- How to wrap an agent in an **agent run middleware** (`AsBuilder().Use(runFunc: ..., runStreamingFunc: ...)`), and what it can do: answer without calling the agent, change the request, change the answer

## Prerequisites

- .NET 10 SDK or later
- An Azure OpenAI (or Microsoft Foundry) resource with a chat model deployment that supports function calling (for example `gpt-4o-mini` or `gpt-5.4-mini`)
- One of the following for authentication:
  - the resource **API key**, or
  - Azure CLI logged in (`az login`) with the **Cognitive Services OpenAI User** role on the resource
- Lab 03 completed (function tools) and Lab 05 recommended (sessions)

## Project Structure

```
Lab09-AIAgentWithFunctionToolsHumanApproval/
├── README.md
├── Start/                          <-- Your working folder
│   ├── Program.cs                  <-- Complete the TODOs here
│   ├── Tools/
│   │   └── HrTools.cs              <-- get_employee_info (harmless) and delete_employee_data (sensitive)
│   ├── EmployeeDirectory.cs        <-- The simulated HR data used by the tools and the policy
│   ├── DeletionPolicy.cs           <-- The approval policy of scenario 2 (provided)
│   ├── ApprovalConsole.cs          <-- Display helpers (provided)
│   ├── ConfigurationHelper.cs
│   ├── AzureOpenAISettings.cs
│   ├── appsettings.json
│   └── AIAgentWithFunctionToolsHumanApproval.csproj
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

- `Tools/HrTools.cs` — two static tools with their `[Description]` attributes. `GetEmployeeInfo` reads the HR directory: harmless, it runs without approval. `DeleteEmployeeData` is **sensitive**: it only simulates the deletion, but in a real application it could not be undone. Nothing in this file says that it requires approval: that decision belongs to the code that creates the agent (`Program.cs`).
- `EmployeeDirectory.cs` — three employees: `EMP001` (Alice Martin, left the company), `EMP002` (Bob Lee, still employed) and `EMP003` (Chen Wei, left the company).
- `DeletionPolicy.cs` — the approval policy of scenario 2: `Decide(FunctionCallContent call)` approves the deletion of a **former** employee and rejects everything else, with a reason. It is plain code: it reads the `employeeId` argument chosen by the model and looks it up in the directory.
- `ApprovalConsole.cs` — display helpers, imported with `using static` so you can call them directly:
  - `WriteApprovalRequest(request)` shows the function and the arguments of a `ToolApprovalRequestContent`;
  - `FormatCall(call)` formats a `FunctionCallContent` as `name(arg=value)`;
  - `WriteToolResults(runs)` shows what every tool returned during the runs (or the rejection the model received), by matching each `FunctionResultContent` with its `FunctionCallContent`;
  - `WriteTokenUsage(runs)` adds up the usage of all the runs of the flow.

### Step 3: Complete the Program.cs

Open `Start/Program.cs` and complete the TODOs:

---

#### Setup: Configuration and Client Initialization

Same as Lab 01.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 1** | Create the `OpenAIClientOptions` for the Azure OpenAI v1 endpoint | • `OpenAIClientOptions clientOptions = new() { Endpoint = AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint) };` (`AzureOpenAIEndpoint` comes from `CommonUtilities`) |
| **TODO 2** | Create the `OpenAIClient` | • `OpenAIClient client = !string.IsNullOrWhiteSpace(settings.APIKey) ? new OpenAIClient(new ApiKeyCredential(settings.APIKey), clientOptions) : new OpenAIClient(new BearerTokenPolicy(new DefaultAzureCredential(), "https://ai.azure.com/.default"), clientOptions);` <br> • Keep the `#pragma warning disable/restore OPENAI001` around it |
| **TODO 3** | Get a `ChatClient` for the deployment | • `ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);` |

---

#### Scenario 1: Human approval at the console

The agent has one tool, `delete_employee_data`, marked as requiring approval. You ask it to delete the data of `EMP001`; the run pauses, you answer **Y** (or anything else to reject) at the console, and the agent continues.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 4** | Create the sensitive tool and wrap it | • `AIFunction deleteEmployeeData = AIFunctionFactory.Create(HrTools.DeleteEmployeeData, "delete_employee_data");` <br> • `List<AITool> tools = [new ApprovalRequiredAIFunction(deleteEmployeeData)];` <br> • The wrapper only *marks* the function: the agent is the one that stops and asks |
| **TODO 5** | Create the agent with the tool | • `AIAgent hrAgent = chatClient.AsAIAgent(instructions: AgentInstructions, name: "HrAssistant", tools: tools);` (same as Lab 03) |
| **TODO 6** | Create a session and run the agent in it | • `AgentSession session = await hrAgent.CreateSessionAsync();` <br> • `AgentResponse response = await hrAgent.RunAsync(prompt, session).WithSpinner("Running agent");` <br> • `List<AgentResponse> runs = [response];` — every `RunAsync` is a run; the list is used at the end for the tool results and the token usage <br> • The session is **mandatory**: the framework records in it the approval requests it surfaces, and only an answer sent back in the same session is honored |
| **TODO 7** | Get the approval requests | • `List<ToolApprovalRequestContent> approvalRequests = response.Messages.SelectMany(message => message.Contents).OfType<ToolApprovalRequestContent>().ToList();` <br> • `ColoredConsole.WriteInfoLine($"Agent run paused: {approvalRequests.Count} approval request(s) pending");` <br> • The run completed *without* invoking the tool: `response.Text` is empty, the messages contain one request per call to approve |
| **TODO 8** | Approval loop | • `while (approvalRequests.Count > 0) { ... }` — ask, answer, run again, check again, until the agent no longer asks <br> • **8.1** build one user message per request: <br> `List<ChatMessage> approvalMessages = approvalRequests.ConvertAll(request => { WriteApprovalRequest(request); ColoredConsole.WritePrimaryLogLine("Please reply Y to approve, or anything else to reject:"); bool approved = string.Equals(Console.ReadLine()?.Trim(), "Y", StringComparison.OrdinalIgnoreCase); if (approved) { ColoredConsole.WriteSuccessLine("Function call approved by user."); } else { ColoredConsole.WriteErrorLine("Function call rejected by user."); } return new ChatMessage(ChatRole.User, [request.CreateResponse(approved)]); });` <br> • **8.2** continue the conversation with the decisions: <br> `response = await hrAgent.RunAsync(approvalMessages, session).WithSpinner("Running agent");` <br> `runs.Add(response);` <br> `approvalRequests = response.Messages.SelectMany(message => message.Contents).OfType<ToolApprovalRequestContent>().ToList();` <br> • An approved call is invoked during this second run; a rejected call is not, and the model receives `Tool call invocation rejected.` instead of a result |
| **TODO 9** | Display the tool results and the final answer | • `WriteToolResults(runs);` <br> • `ColoredConsole.WritePrimaryLogLine("Agent answer:");` <br> • `ColoredConsole.WriteSecondaryLogLine(response.Text);` |
| **TODO 10** | Display token usage | • `WriteTokenUsage(runs);` — the usage of the whole flow (at least two runs) |

---

#### Scenario 2: Several approval requests decided by a policy

The agent has two tools: `get_employee_info` (no approval) and `delete_employee_data` (approval required). The request concerns **two employees**: the agent looks them up freely, then asks for approval of **each** deletion. The decision is taken by `DeletionPolicy` (code) instead of a person: `EMP001` left the company, so the deletion is approved; `EMP002` is still employed, so it is rejected **with a reason** that the model receives.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 11** | Create the two tools | • `List<AITool> tools = [AIFunctionFactory.Create(HrTools.GetEmployeeInfo, "get_employee_info"), new ApprovalRequiredAIFunction(AIFunctionFactory.Create(HrTools.DeleteEmployeeData, "delete_employee_data"))];` <br> • Only the wrapped tool requires approval |
| **TODO 12** | Create the agent | • Same as TODO 5: `AIAgent hrAgent = chatClient.AsAIAgent(instructions: AgentInstructions, name: "HrAssistant", tools: tools);` |
| **TODO 13** | Create a session and run the agent | • Same as TODO 6, with the prompt of scenario 2: `AgentSession session = await hrAgent.CreateSessionAsync();` `AgentResponse response = await hrAgent.RunAsync(prompt, session).WithSpinner("Running agent");` `List<AgentResponse> runs = [response];` |
| **TODO 14** | Get the approval requests | • Same as TODO 7 (`List<ToolApprovalRequestContent> approvalRequests = ...;` then the `Agent run paused` line) <br> • Only the `delete_employee_data` calls come back as requests: `get_employee_info` was invoked during the run, even when the model asked for both tools at once |
| **TODO 15** | Approval loop decided by the policy | • `while (approvalRequests.Count > 0) { ... }` <br> • **15.1** `List<ChatMessage> approvalMessages = approvalRequests.ConvertAll(request => { FunctionCallContent call = (FunctionCallContent)request.ToolCall; ApprovalDecision decision = DeletionPolicy.Decide(call); ColoredConsole.WriteWarningLine($"Approval requested for {FormatCall(call)}"); if (decision.Approved) { ColoredConsole.WriteSuccessLine($"Policy decision: approved - {decision.Reason}"); } else { ColoredConsole.WriteErrorLine($"Policy decision: rejected - {decision.Reason}"); } return new ChatMessage(ChatRole.User, [request.CreateResponse(decision.Approved, decision.Reason)]); });` <br> • **15.2** same as TODO 8.2: `response = await hrAgent.RunAsync(approvalMessages, session).WithSpinner("Running agent");` `runs.Add(response);` `approvalRequests = response.Messages.SelectMany(message => message.Contents).OfType<ToolApprovalRequestContent>().ToList();` <br> • The `reason` is sent to the model only for a rejected call (`Tool call invocation rejected. <reason>`) |
| **TODO 16** | Display the tool results and the final answer | • Same as TODO 9: `WriteToolResults(runs);` then `Agent answer:` and `response.Text` |
| **TODO 17** | Display token usage | • `WriteTokenUsage(runs);` |

#### Scenario 3: Guardrails with an agent run middleware

The same HR agent is wrapped in a guardrail: `HrGuardrail.RunAsync`, at the end of `Program.cs`. It is an **agent run middleware**: Agent Framework calls it instead of the agent, with the messages of the run and the inner agent, and it decides what to do (official sample [`Agent_Step11_Middleware`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step11_Middleware), [Termination & Guardrails](https://learn.microsoft.com/agent-framework/concepts/agents/middleware/termination?pivots=programming-language-csharp)):

1. a question about a **forbidden topic** (salaries, passwords): the middleware answers by itself, **the model is never called**;
2. **personal data** in the request (an e-mail, a phone number): masked before the agent and the model see it;
3. the **answer**: checked the same way before the caller sees it.

`MaskPersonalData` and the list of forbidden topics are provided: the patterns are simple on purpose (a real application uses a dedicated detection service). What matters is where the checks run.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 18** | Create the HR agent of scenario 2 | • Same tools and agent as TODO 11 and TODO 12 |
| **TODO 19** | Wrap the agent in the guardrail | • `AIAgent guardedAgent = hrAgent.AsBuilder().Use(runFunc: HrGuardrail.RunAsync, runStreamingFunc: null).Build();` <br> • The agent itself is not changed: `guardedAgent` is a new agent that calls the middleware, which calls `hrAgent` <br> • Only the non-streaming function is given, as in the official sample: a `RunStreamingAsync` of `guardedAgent` then runs in non-streaming mode, so that the guardrail always sees the whole answer. Give a `runStreamingFunc` too when you need real streaming |
| **TODO 20** | Ask a forbidden question | • `const string forbiddenQuestion = "What is the salary of the employee EMP002?";` display it (`User: ...`) <br> • `AgentResponse blockedResponse = await guardedAgent.RunAsync(forbiddenQuestion);` (no spinner: the middleware writes to the console) <br> • `ColoredConsole.WritePrimaryLogLine($"Agent: {blockedResponse.Text}");` <br> • `blockedResponse.Usage` is `null` when the middleware answered by itself: display `Token usage: none, the model was not called` |
| **TODO 21** | Ask a question with personal data | • `const string personalDataQuestion = "My phone number is +33 6 12 34 56 78 and my e-mail is ada.lovelace@example.com. Call me back with the status of the employee EMP001.";` display it (`User: ...`) <br> • `AgentResponse maskedResponse = await guardedAgent.RunAsync(personalDataQuestion);` <br> • `ColoredConsole.WritePrimaryLogLine($"Agent: {maskedResponse.Text}");` then `WriteTokenUsage([maskedResponse]);` |
| **TODO 22** | `HrGuardrail.RunAsync` — block a forbidden topic | • `string? topic = ForbiddenTopics.FirstOrDefault(forbidden => request.Any(message => message.Role == ChatRole.User && message.Text.Contains(forbidden, StringComparison.OrdinalIgnoreCase)));` <br> • `if (topic is not null) { ColoredConsole.WriteWarningLine($"[Guardrail] Request blocked: forbidden topic '{topic}'. The agent was not called."); return new AgentResponse([new ChatMessage(ChatRole.Assistant, "I can't help with salaries or passwords. Please contact the HR department directly.")]); }` <br> • Returning without calling `innerAgent` ends the run: no model call, no tokens |
| **TODO 23** | `HrGuardrail.RunAsync` — mask the personal data | • `int maskedInRequest = MaskPersonalData(request);` then `ColoredConsole.WriteWarningLine($"[Guardrail] Request: {maskedInRequest} personal data item(s) masked before the agent sees them");` <br> • `AgentResponse response = await innerAgent.RunAsync(request, session, options, cancellationToken);` <br> • `int maskedInAnswer = MaskPersonalData(response.Messages);` then `ColoredConsole.WriteWarningLine($"[Guardrail] Answer: {maskedInAnswer} personal data item(s) masked");` and `return response;` <br> • Remove the last line of the method |

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

Scenario 1 is **interactive**: when `Please reply Y to approve, or anything else to reject:` appears, type `Y` and press Enter to approve (anything else, or an empty line, rejects). Scenario 2 needs no input.

In the dashboard, an input box appears under the output when the program waits: type `Y` there. The dashboard checks of scenario 1 expect the deletion to be approved.

To run only one scenario: `dotnet run -- --scenarios 2` ([Choose the scenarios to run](../../../README.md#3-choose-the-scenarios-to-run)).

## How the approval flow works

1. The agent sends your prompt and the tools to the model, which answers with a **function call** (`delete_employee_data`, `employeeId = EMP001`), exactly as in Lab 03.
2. The tool is an `ApprovalRequiredAIFunction`: instead of invoking it, the agent **ends the run**. The response contains no text, only a `ToolApprovalRequestContent` per call, whose `ToolCall` is the `FunctionCallContent` chosen by the model. The agent also records these requests in the **session**.
3. Your code takes the decision (a person at the console in scenario 1, a policy in scenario 2) and creates the answer with `request.CreateResponse(approved, reason)`, inside a user `ChatMessage`.
4. You run the agent again with these messages **and the same session**. The agent binds each answer to the request it recorded, invokes the **approved** calls, and sends the results to the model; for a **rejected** call it sends `Tool call invocation rejected.` followed by the reason. The model then answers with text, or asks for another approval: hence the `while` loop.

Two details matter in practice:

- **The session is mandatory.** The agent only honors an approval that matches a request it surfaced in that session (*approval binding*, enabled by default for `ChatClientAgent`). Without the session, the answer is ignored: the tool is never invoked and the model answers without any result. The requests live in the session: if your application has to wait for a person (a web page, an e-mail), persist the session as in Lab 05 and restore it when the answer arrives.
- **Only the sensitive tools ask.** When the model requests a harmless tool and a sensitive one in the same turn, the agent invokes the harmless one by itself (scenario 2: `get_employee_info` never appears as a request). This is the default behavior of `ChatClientAgent` (`DisableApprovalNotRequiredFunctionBypassing = false`).

Every `RunAsync` is a run with its own token usage: an approval flow costs at least two model calls, and a rejection still costs a round trip (the model has to read the rejection and explain it). `WriteTokenUsage(runs)` adds them up.

## Key Concepts

| Concept | Description |
|---------|-------------|
| Human-in-the-loop | An agent run that needs a human decision completes with a **request** instead of an answer; your code gets the decision and resumes the conversation |
| `ApprovalRequiredAIFunction` | `AIFunction` wrapper (`Microsoft.Extensions.AI`) that marks a tool as requiring approval. It does not enforce anything by itself: the agent's function calling loop does |
| `ToolApprovalRequestContent` | Content found in `response.Messages` when the agent needs an approval. `ToolCall` is the `FunctionCallContent` to approve (name, arguments) |
| `CreateResponse(approved, reason)` | Creates the `ToolApprovalResponseContent` correlated with the request. `reason` is optional; it is sent to the model when the call is rejected |
| `AgentSession` + `CreateSessionAsync()` | The conversation (Lab 05). Mandatory here: the surfaced requests are recorded in it, and the answers must come back in it |
| Approval binding | Default `ChatClientAgent` behavior: an approval only takes effect if it matches a request the agent recorded in the session, with the same tool name and arguments |
| Approval loop | `while (approvalRequests.Count > 0)`: answer, run again, check again, until the agent answers with text |
| `FunctionCallContent` / `FunctionResultContent` | The tool calls and their results in `response.Messages` (same `CallId`): what the tools returned, or `Tool call invocation rejected.` |
| Policy decision | The decision can come from code as well as from a person: same request, same `CreateResponse`. In real applications a policy approves the routine calls and leaves the rest to a person |
| Agent run middleware | `agent.AsBuilder().Use(runFunc: ..., runStreamingFunc: ...).Build()`: a function called around every run, with the messages of the run, the session, the options and the inner agent. The original agent is not modified |
| Guardrail | A check around the run: block a request (return an `AgentResponse` without calling the inner agent), or change the request / the answer before passing them on |
| `runStreamingFunc: null` | Only the non-streaming function is given: a streaming run of the wrapped agent runs in non-streaming mode (the official sample does the same for its guardrails) |

## Namespaces Reference

| Namespace | Purpose |
|-----------|---------|
| `OpenAI` | `OpenAIClient`, `OpenAIClientOptions` |
| `OpenAI.Chat` | `ChatClient` and the `AsAIAgent()` extension method |
| `System.ClientModel` | `ApiKeyCredential` |
| `System.ClientModel.Primitives` | `BearerTokenPolicy` |
| `Azure.Identity` | `DefaultAzureCredential` |
| `Microsoft.Agents.AI` | `AIAgent`, `AgentSession`, `AgentResponse`, `AsBuilder()` and its `Use(runFunc: ..., runStreamingFunc: ...)` |
| `System.Text.RegularExpressions` | `Regex` (personal data patterns of the guardrail) |
| `Microsoft.Extensions.AI` | `AIFunctionFactory`, `AIFunction`, `AITool`, `ApprovalRequiredAIFunction`, `ToolApprovalRequestContent`, `FunctionCallContent`, `FunctionResultContent`, `ChatRole` |
| `using ChatMessage = Microsoft.Extensions.AI.ChatMessage;` | Alias: `OpenAI.Chat` also defines a `ChatMessage` type |
| `CommonUtilities` | `ColoredConsole`, `WithSpinner()`, `AzureOpenAIEndpoint` |

## Expected Output

```
Endpoint: https://your-resource.openai.azure.com/
Deployment: your-deployment-name
Agent instructions: You are an HR assistant. ...
----------------------------------------
=== Scenario 1: Human approval at the console ===
User: Delete all the data of the employee with the ID EMP001.
Agent run paused: 1 approval request(s) pending
APPROVAL REQUIRED
The agent would like to invoke the following sensitive function:
  Function: delete_employee_data
  Arguments: employeeId=EMP001
Please reply Y to approve, or anything else to reject:
Y
Function call approved by user.
Tool results:
Tool result (delete_employee_data): Sensitive operation executed: All data for employee 'EMP001' has been permanently deleted. This action cannot be undone.
Agent answer:
All data for the employee EMP001 has been permanently deleted. This action cannot be undone.
----------------------------------------
Token Usage (2 runs):
  Input tokens: xxx
  Output tokens: xxx
  Total tokens: xxx
----------------------------------------
=== Scenario 2: Several approval requests decided by a policy ===
User: The employees EMP001 and EMP002 asked us to erase their data. Look up both employees, then delete the data of each of them.
Agent run paused: 2 approval request(s) pending
Approval requested for delete_employee_data(employeeId=EMP001)
Policy decision: approved - EMP001 (Alice Martin) left the company on 2025-12-31.
Approval requested for delete_employee_data(employeeId=EMP002)
Policy decision: rejected - EMP002 (Bob Lee) is still employed: only the data of former employees can be deleted.
Tool results:
Tool result (get_employee_info): Employee EMP001: Alice Martin, Engineering, left the company on 2025-12-31.
Tool result (get_employee_info): Employee EMP002: Bob Lee, Finance, currently employed.
Tool result (delete_employee_data): Tool call invocation rejected. EMP002 (Bob Lee) is still employed: only the data of former employees can be deleted.
Tool result (delete_employee_data): Sensitive operation executed: All data for employee 'EMP001' has been permanently deleted. This action cannot be undone.
Agent answer:
- EMP001: Alice Martin, Engineering, left the company on 2025-12-31. (Data has been successfully deleted.)
- EMP002: Bob Lee, Finance, is currently employed. (Deletion was rejected because only former employees can have their data deleted.)
----------------------------------------
Token Usage (2 runs):
  Input tokens: xxx
  Output tokens: xxx
  Total tokens: xxx
----------------------------------------
=== Scenario 3: Guardrails with an agent run middleware ===
User: What is the salary of the employee EMP002?
[Guardrail] Request blocked: forbidden topic 'salary'. The agent was not called.
Agent: I can't help with salaries or passwords. Please contact the HR department directly.
Token usage: none, the model was not called
User: My phone number is +33 6 12 34 56 78 and my e-mail is ada.lovelace@example.com. Call me back with the status of the employee EMP001.
[Guardrail] Request: 2 personal data item(s) masked before the agent sees them
[Guardrail] Answer: 0 personal data item(s) masked
Agent: Employee EMP001, Alice Martin, was part of the Engineering department and left the company on December 31, 2025.
----------------------------------------
Token Usage (1 runs):
  Input tokens: 432
  Output tokens: 44
  Total tokens: 476
```

Answers vary from one run to another. In scenario 2 the model usually looks up both employees, then asks for the two deletions at once (`2 approval request(s) pending`, two runs in total); it may also ask for them one after the other (`1 approval request(s) pending` twice, three runs): the loop handles both. Among the tool results of one run, the rejected calls are listed before the executed ones.

**Color legend:** scenario titles in **cyan**, `APPROVAL REQUIRED` and `Approval requested` in **yellow**, approvals in **green**, rejections in **red**.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `'AzureOpenAI:Endpoint' is not configured` | Replace the `YOUR-...` placeholders in `appsettings.json` (or use user secrets / environment variables) |
| `401 Unauthorized` with an API key | Check the key (user secret `AzureOpenAI:APIKey`) and that it belongs to the resource of the endpoint |
| `401` / `403` with Entra ID | Run `az login` and make sure your identity has the **Cognitive Services OpenAI User** role on the resource |
| `404 DeploymentNotFound` | `ChatDeploymentName` must be the **deployment** name, not the model name |
| `Agent run paused: 0 approval request(s) pending` and the tool ran anyway | The tool was not wrapped in `ApprovalRequiredAIFunction` (TODO 4 / TODO 11) |
| You approved, but `Tool results: none` and the agent answers as if nothing happened | The second `RunAsync` was made without the session, or with another session: the approval could not be bound to the request and was ignored (TODO 6 / TODO 8.2) |
| The agent answers without asking anything | The model did not call the tool. Check the `[Description]` of `DeleteEmployeeData` and the prompt |
| `InvalidCastException` on `(FunctionCallContent)request.ToolCall` | Only function tools raise `ToolApprovalRequestContent` here; the cast is safe for this lab, as in the official sample |
| Scenario 1 never waits for your answer in the dashboard | The lab is declared interactive in the dashboard catalog; type `Y` in the input box under the output when it appears |

## Provided Files

The following files are provided and should not be modified:

- `AIAgentWithFunctionToolsHumanApproval.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs` - Loads and validates the configuration
- `AzureOpenAISettings.cs` - Settings class for Azure OpenAI
- `Tools/HrTools.cs` - The two HR tools, with their `[Description]` attributes
- `EmployeeDirectory.cs` - Simulated HR data
- `DeletionPolicy.cs` - Approval policy of scenario 2
- `ApprovalConsole.cs` - Display helpers

The following files should be modified:

- `appsettings.json` - Update with your Azure OpenAI endpoint and deployment
- `Program.cs` - Complete the TODOs

### NuGet packages

| Package | Why |
|---------|-----|
| `Microsoft.Agents.AI.OpenAI` | Agent Framework + `AsAIAgent()` for OpenAI clients (brings `OpenAI` and `Microsoft.Extensions.AI`, which contains `AIFunctionFactory`, `ApprovalRequiredAIFunction` and `ToolApprovalRequestContent`) |
| `Azure.Identity` | `DefaultAzureCredential` for Entra ID authentication |
| `Microsoft.Extensions.Configuration.*` | `appsettings.json`, user secrets and environment variables, bound to `AzureOpenAISettings` |

## Going Further

- **Streaming**: the approval requests are in the updates too: `updates.SelectMany(u => u.Contents).OfType<ToolApprovalRequestContent>()`, then `RunStreamingAsync(approvalMessages, session)` to continue (shown in comments in the official sample `Agent_Step01_UsingFunctionToolsWithApprovals`).
- **Waiting for a person**: between the request and the answer, persist the session with `SerializeSessionAsync` / `DeserializeSessionAsync` (Lab 05, official sample `Agent_Step03_PersistedConversations`); the recorded requests travel with it.
- **"Don't ask again"**: `agent.AsBuilder().UseToolApproval().Build()` adds the `ToolApprovalAgent` middleware; answering with `request.CreateAlwaysApproveToolResponse()` (or `CreateAlwaysApproveToolWithArgumentsResponse()`) records a standing rule in the session, and later matching calls are approved automatically, one request at a time.
- **All calls as requests**: `ChatClientAgentOptions.DisableApprovalNotRequiredFunctionBypassing = true` surfaces every call of a turn that contains a sensitive one, including the harmless ones (`RequiresConfirmation`, still experimental, then tells them apart).
- **Guardrails with a streaming run**: give a `runStreamingFunc` too (`IAsyncEnumerable<AgentResponseUpdate>`): it can block before the first update, but a check on the whole answer has to collect the updates first.
- **Real personal data detection**: the patterns of `MaskPersonalData` are a demo. Agent Framework integrates Microsoft Purview as a middleware for data security and compliance checks ([Purview](https://learn.microsoft.com/agent-framework/integrations/by-component/middleware/purview?pivots=programming-language-csharp)).
- **The three levels of middleware**: agent run (this lab), function calling (Lab 03, scenario 4) and `IChatClient` (Lab 11). See [Agent Middleware](https://learn.microsoft.com/agent-framework/concepts/agents/middleware/?pivots=programming-language-csharp).
- **Harness agent**: `chatClient.AsHarnessAgent(...)` preconfigures the same approval flow with the "don't ask again" middleware and optional auto-approval rules. See [Using function tools with human in the loop approvals](https://learn.microsoft.com/agent-framework/agents/tools/tool-approval?pivots=programming-language-csharp).

## Coming from an older version of the lab?

| Before (preview) | Now (1.26.0) |
|------------------|--------------|
| `Azure.AI.OpenAI` / `AzureOpenAIClient` | `OpenAI` / `OpenAIClient` + Azure OpenAI v1 endpoint |
| `ChatClientAgent agent = chatClient.CreateAIAgent(instructions, tools)` | `AIAgent agent = chatClient.AsAIAgent(instructions, name, tools)` |
| `var thread = agent.GetNewThread();` (`AgentThread`) | `AgentSession session = await agent.CreateSessionAsync();` |
| `AgentRunResponse` | `AgentResponse` |
| `response.UserInputRequests` (`UserInputRequestContent`) | removed: `response.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>()` |
| `FunctionApprovalRequestContent` with `.FunctionCall` | `ToolApprovalRequestContent` with `.ToolCall` (cast to `FunctionCallContent`) |
| `CreateResponse(approved)` | unchanged, plus an optional `reason` |
| One `if (userInputRequests.Any())` | `while (approvalRequests.Count > 0)`: the agent may ask again |
| — | Approval binding: the answer must come back in the same session; only the sensitive tools ask |
| `response.ToString()` | `response.Text`; tool results and rejections read from `response.Messages` |
| One scenario | Scenario 2: several requests, decision by a policy with a reason |
| `<NoWarn>MEAI001</NoWarn>` | Removed: none of the APIs used is experimental |
| API key in `appsettings.json` | API key in user secrets or environment variables |

`ApprovalRequiredAIFunction` and `AIFunctionFactory` are unchanged: they come from `Microsoft.Extensions.AI`.

## Useful Links

- [Using function tools with human in the loop approvals](https://learn.microsoft.com/agent-framework/agents/tools/tool-approval?pivots=programming-language-csharp)
- [Using function tools with an agent](https://learn.microsoft.com/agent-framework/agents/tools/function-tools?pivots=programming-language-csharp)
- [Microsoft Agent Framework documentation](https://learn.microsoft.com/agent-framework/overview/?pivots=programming-language-csharp)
- [Official sample `Agent_Step01_UsingFunctionToolsWithApprovals`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step01_UsingFunctionToolsWithApprovals)
- [Official sample `Agent_Step03_PersistedConversations`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step03_PersistedConversations)
- [Azure OpenAI function calling](https://learn.microsoft.com/azure/foundry/openai/how-to/function-calling)
- [Azure OpenAI v1 API](https://learn.microsoft.com/azure/foundry/openai/api-version-lifecycle)

## Solution

If you get stuck, check the complete solution in the `Solution/` folder.
