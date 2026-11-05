# Lab 02 - Sequential Orchestration: a Pipeline of Agents

## Objective

In this lab, you will chain AI agents in a **sequential workflow** with **Microsoft Agent Framework** and **Azure OpenAI**: each agent takes its turn, in a fixed order, and builds on the work of the previous ones.

> *"In sequential orchestration, agents are organized in a pipeline. Each agent processes the task in turn, passing its output to the next agent in the sequence. This is ideal for workflows where each step builds upon the previous one, such as document review, data processing pipelines, or multi-stage reasoning."* — [Sequential orchestration](https://learn.microsoft.com/agent-framework/workflows/orchestrations/sequential?pivots=programming-language-csharp)

You will work on the transcript of a team meeting:

- **Scenario 1** — build a two-step pipeline (summary, then action items), run it, and read its output;
- **Scenario 2** — follow a three-step pipeline (action items, follow-up email, French translation) through its events, see what each agent receives, and compare the two ways to pass the context from one agent to the next;
- **Scenario 3** — make the last agent send the email with a tool that requires an approval: the pipeline pauses until your code answers.

In Lab 01 of this folder, your code called one agent after another. Here, the **workflow** does it: you declare the order, the framework runs the agents, passes the messages, and reports what happens as events.

## What You Will Learn

- What a sequential orchestration is, how it works under the hood (executors, edges, the `TurnToken`), and when to use it
- How to build it with `AgentWorkflowBuilder.BuildSequential(...)`
- How to run it with `InProcessExecution.RunStreamingAsync(...)` and a `TurnToken`, and read its output from the `WorkflowOutputEvent`
- How to handle the errors of a run: `ExecutorFailedEvent` and `WorkflowErrorEvent` (a failing agent does not throw)
- How to follow a run with its events: `ExecutorInvokedEvent`, `AgentResponseUpdateEvent`, and the token usage of each agent
- What context each agent receives, and how to choose it: the whole conversation (default) or the previous agent only (`chainOnlyAgentResponses`)
- How to keep a human in the loop: a tool wrapped in `ApprovalRequiredAIFunction` pauses the workflow (`RequestInfoEvent`), and `SendResponseAsync` resumes it
- The limits of a sequential orchestration, and the other patterns to use instead

## Prerequisites

- .NET 10 SDK or later
- An Azure OpenAI (or Microsoft Foundry) resource with a chat model deployment that supports function calling (for example `gpt-4o-mini` or `gpt-5.4-mini`)
- One of the following for authentication:
  - the resource **API key**, or
  - Azure CLI logged in (`az login`) with the **Cognitive Services OpenAI User** role on the resource
- Labs 01, 03 and 09 of the Azure OpenAI track: agents (`AsAIAgent()`), streaming, function tools, tool approvals (`ApprovalRequiredAIFunction`)
- Lab 01 of this folder (*Delegate a Task to Another Agent*) is recommended: it shows the alternative, delegation written in code

## Sequential orchestration in a nutshell

```
                    ┌──────────────────┐   ┌──────────────────┐   ┌──────────────────┐   ┌────────────────┐
 transcript ──────► │ ActionItems      │──►│ FollowUpWriter   │──►│ Translator       │──►│ OutputMessages │──► output
 (input messages)   │ Extractor        │   │                  │   │                  │   │ (added by the  │    (List<ChatMessage>)
                    └──────────────────┘   └──────────────────┘   └──────────────────┘   │  builder)      │
                          agent 1                agent 2                agent 3          └────────────────┘
```

- **A workflow is a graph.** Its nodes are **executors**, its links are **edges**. `BuildSequential` wraps each agent in an executor, links them in the order of the list, and adds a last executor, `OutputMessages`, which yields the output of the workflow. `WritePipeline` (provided) displays this path.
- **Messages travel along the edges.** An agent executor collects the chat messages it receives, and runs its agent only when it receives a **`TurnToken`**. When the agent has answered, the executor sends the messages on to the next executor, followed by a `TurnToken`: that is how the turn passes from one agent to the next. Your code sends the first `TurnToken`; without it, the first agent keeps the transcript and nothing runs.
- **Each agent receives the conversation so far.** By default, the next agent receives the messages its predecessor received **and** the messages it produced: the third agent sees the transcript and the answers of the first two. The messages written by other agents are given to it as `user` messages (it did not write them). With `chainOnlyAgentResponses: true`, each agent receives only the messages of the previous agent (scenario 2).
- **The output is the conversation.** The workflow yields a `List<ChatMessage>`: by default the input, then the messages of every agent, each with its `AuthorName`; with `chainOnlyAgentResponses: true`, only the messages of the last agent.
- **A run reports what happens with events**, which you read with `WatchStreamAsync()`: an executor is invoked, an agent streams an update, the workflow yields its output, a tool call waits for an approval, an executor fails... The run itself runs in the background.
- **Each run is independent.** An agent keeps no conversation by itself: in each run, the executor of an agent gives it a new session. The same `AIAgent` can be a step of several workflows (the action items extractor is used by the three scenarios).

### When to use it

Use a sequential orchestration when a task is a **fixed series of steps, each building on the previous one**, and each step deserves its own agent (its own instructions, tools or model):

| Use case | Pipeline |
|----------|----------|
| Document processing | extract → summarize → translate (this lab) |
| Content creation | draft → review → edit → format |
| Data processing | extract → validate → enrich → write |
| Multi-stage reasoning | analyze → plan → check |
| An action at the end of a pipeline | prepare → write → act with an approved tool (scenario 3) |

Do **not** use it when the next step depends on the content (a router decides: handoff, conditional edges), when the steps are independent (run them in parallel: concurrent orchestration), when the agents must iterate together (group chat, or a writer-critic loop), or when a single agent with a few tools is enough: every step is a full agent call, which adds latency and tokens.

## Project Structure

```
Lab02_OrchestrationSequential/
├── README.md
├── Start/                          <-- Your working folder
│   ├── Program.cs                  <-- Complete the TODOs here
│   ├── LabPrompts.cs               <-- Provided: names and instructions of the agents
│   ├── WorkflowConsole.cs          <-- Display helpers and PipelineTrace (provided)
│   ├── Tools/
│   │   └── EmailTools.cs           <-- The mail system, tool of the sender (scenario 3)
│   ├── Data/
│   │   └── meeting-transcript.txt  <-- The input of every workflow
│   ├── ConfigurationHelper.cs
│   ├── AzureOpenAISettings.cs
│   ├── appsettings.json
│   └── OrchestrationSequential.csproj
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
   Every agent of the lab uses this deployment.

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

- `Data/meeting-transcript.txt` — the weekly meeting of the *Project Alpha* team (Sarah, Mohammed, Emma, Michael): status updates, decisions, and tasks with owners and due dates. It is the input of every workflow.
- `LabPrompts.cs` — the names and the instructions of the five agents (see Step 3).
- `Tools/EmailTools.cs` — `SendEmail(to, subject, body)`, the mail system of the team (mocked: nothing is sent). Only the sender of scenario 3 has this tool.
- `WorkflowConsole.cs` — display helpers, imported with `using static`:
  - `WritePipeline(workflow)` — the path of a workflow, from its start executor along its edges: `Workflow meeting-notes: MeetingSummarizer -> ActionItemsExtractor -> OutputMessages`;
  - `WriteWorkflowOutput(messages)` — the output of a workflow: each message with its role and its author (the transcript is shortened);
  - `WriteWorkflowOutputAuthors(messages)` — the same in one line: the number of messages and their authors;
  - `WriteExecutorFailed(failed)`, `WriteWorkflowError(error)` — the errors of a run;
  - `WriteRunStatus(status)` — the status of a run (`Running`, `PendingRequests`, `Idle`...);
  - `WriteApprovalRequest(request)`, `WriteApprovalResponse(response)` — a tool call that waits for an approval, and the answer;
  - `PipelineTrace` — follows a run from its events (scenario 2): `OnExecutorInvoked` records the messages each executor receives, `OnAgentUpdate` writes the text streamed by the agents, and `WriteReport()` displays, for each executor, the messages it received and, for each agent, its token usage (computed from its streamed updates, as `updates.ToAgentResponse().Usage` in Lab 01 of the Azure OpenAI track).

### Step 3: Complete the Program.cs

Open `Start/Program.cs` and complete the TODOs.

The names and the instructions of the agents are **provided** in `Start/LabPrompts.cs`: use its constants (`Agents.ActionItemsExtractor.Name`, `Agents.ActionItemsExtractor.Instructions`, `Agents.Translator.Name`...) instead of writing your own texts. Everyone works with the same agents and gets comparable answers, and you can focus on the Agent Framework code. The names of the workflows (`meeting-notes`, `meeting-follow-up`...) and of the tool (`send_email`) are identifiers that you write in the code.

---

#### Setup: Configuration and Client Initialization

TODO 1 to 3 are the same as in Lab 01 of the Azure OpenAI track.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 1** | Create the `OpenAIClientOptions` for the Azure OpenAI v1 endpoint | • `OpenAIClientOptions clientOptions = new() { Endpoint = AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint) };` <br> • `AzureOpenAIEndpoint.ToV1Uri` (from `CommonUtilities`) returns the `.../openai/v1/` URI |
| **TODO 2** | Create the `OpenAIClient` | • API key: `new OpenAIClient(new ApiKeyCredential(settings.APIKey), clientOptions)` <br> • Entra ID: `new OpenAIClient(new BearerTokenPolicy(new DefaultAzureCredential(), "https://ai.azure.com/.default"), clientOptions)` <br> • Use a conditional on `string.IsNullOrWhiteSpace(settings.APIKey)` to support both, between the two `#pragma` lines |
| **TODO 3** | Get a `ChatClient` for the deployment | • `ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);` |
| **TODO 4** | Load the transcript: the input of every workflow | • `string transcript = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "meeting-transcript.txt"));` <br> • `List<ChatMessage> meeting = [new(ChatRole.User, transcript)];` <br> • `ChatMessage` is the one of `Microsoft.Extensions.AI` (an alias at the top of the file avoids the conflict with `OpenAI.Chat.ChatMessage`) |
| **TODO 5** | Create the action items extractor | • `AIAgent actionItemsAgent = chatClient.AsAIAgent(instructions: Agents.ActionItemsExtractor.Instructions, name: Agents.ActionItemsExtractor.Name);` <br> • The **name** matters in a workflow: it becomes the author of the messages of the agent and the start of the id of its executor <br> • The agent is created once and used by the three scenarios: each run gives it a new session |

---

#### Scenario 1: A sequential workflow - build it, run it, read its output

The smallest pipeline: a summarizer, then the action items extractor. You build the workflow, start a run, send the `TurnToken`, and wait for the output. `emitEvents: false` asks for no streaming events: the agents run without streaming, and the run reports only the main events, among them the output.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 6** | Create the summarizer | • `AIAgent summarizerAgent = chatClient.AsAIAgent(instructions: Agents.MeetingSummarizer.Instructions, name: Agents.MeetingSummarizer.Name);` |
| **TODO 7** | Build the workflow and display its path | • `Workflow workflow = AgentWorkflowBuilder.BuildSequential("meeting-notes", [summarizerAgent, actionItemsAgent]);` <br> • The order of the list is the order of the pipeline <br> • `WritePipeline(workflow);` — note the last executor, `OutputMessages`, added by the builder |
| **TODO 8** | Start the run, then send the `TurnToken` | • `await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, meeting);` <br> • `await run.TrySendMessageAsync(new TurnToken(emitEvents: false));` <br> • `RunStreamingAsync` delivers the input to the first agent, which keeps it until the `TurnToken` arrives. `await using` disposes the run at the end of the block <br> • Pass `meeting` with its type (`List<ChatMessage>`): see *Troubleshooting* |
| **TODO 9** | Watch the events: the output and the errors | • `await foreach (WorkflowEvent evt in run.WatchStreamAsync())` with a `switch (evt)`: <br> &nbsp;&nbsp;`case WorkflowOutputEvent output: WriteWorkflowOutput(output.As<List<ChatMessage>>() ?? []); break;` <br> &nbsp;&nbsp;`case ExecutorFailedEvent failed: WriteExecutorFailed(failed); break;` <br> &nbsp;&nbsp;`case WorkflowErrorEvent error: WriteWorkflowError(error); break;` <br> • The loop ends when the run has nothing left to do <br> • A failing agent does **not** throw an exception: it raises these two events, and the output never comes. Without these cases, a run that fails ends silently |
| **TODO 10** | Say why there is no token usage | • `ColoredConsole.WriteSecondaryLogLine("Token usage: not available without the streaming events (see scenario 2).");` <br> • The output messages do not carry the usage of the agents |

---

#### Scenario 2: Follow the pipeline - what each agent receives

A three-step pipeline: the action items extractor, the writer of the follow-up email, and a translator that translates the email into French. You build it **twice** and run both versions:

- by default, each agent receives the **whole conversation**: the translator receives the transcript, the action items and the email;
- with `chainOnlyAgentResponses: true`, each agent receives **only the messages of the previous agent**: the translator receives only the email.

This time, the `TurnToken` asks for the streaming events (`emitEvents: true`): you see each agent write its answer, the messages each executor receives (`ExecutorInvokedEvent`), and the token usage of each agent.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 11** | Create the writer and the translator | • `AIAgent writerAgent = chatClient.AsAIAgent(instructions: Agents.FollowUpWriter.Instructions, name: Agents.FollowUpWriter.Name);` <br> • `AIAgent translatorAgent = chatClient.AsAIAgent(instructions: Agents.Translator.Instructions, name: Agents.Translator.Name);` |
| **TODO 12** | Build the pipeline twice | • `(string Heading, Workflow Workflow)[] variants = [("Context: the whole conversation (default)", AgentWorkflowBuilder.BuildSequential("meeting-follow-up", [actionItemsAgent, writerAgent, translatorAgent])), ("Context: the previous agent only (chainOnlyAgentResponses: true)", AgentWorkflowBuilder.BuildSequential("meeting-follow-up-chained", chainOnlyAgentResponses: true, [actionItemsAgent, writerAgent, translatorAgent]))];` |
| **TODO 13** | For each variant: display it, run it with the streaming events | • `foreach ((string heading, Workflow workflow) in variants) { ... }` — TODO 13 to 15 go in this loop <br> • `ColoredConsole.WriteInfoLine($"--- {heading} ---");` then `WritePipeline(workflow);` <br> • `await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, meeting);` <br> • `await run.TrySendMessageAsync(new TurnToken(emitEvents: true));` |
| **TODO 14** | Follow the events with a `PipelineTrace` | • `PipelineTrace trace = new();` and `List<ChatMessage> output = [];` before the loop <br> • `await foreach (WorkflowEvent evt in run.WatchStreamAsync())` with a `switch (evt)`: <br> &nbsp;&nbsp;`case ExecutorInvokedEvent invoked: trace.OnExecutorInvoked(invoked); break;` <br> &nbsp;&nbsp;`case AgentResponseUpdateEvent update: trace.OnAgentUpdate(update); break;` <br> &nbsp;&nbsp;`case WorkflowOutputEvent workflowOutput: output = workflowOutput.As<List<ChatMessage>>() ?? []; break;` <br> &nbsp;&nbsp;`ExecutorFailedEvent` and `WorkflowErrorEvent` as in TODO 9 <br> • **Order matters**: `AgentResponseUpdateEvent` derives from `WorkflowOutputEvent`; put it first, or every update would be taken for the output (the compiler refuses the reverse order: the case would be unreachable) |
| **TODO 15** | Display what each executor received, then the output | • `trace.WriteReport();` <br> • `WriteWorkflowOutputAuthors(output);` — the texts were already streamed |

Compare the two reports: the `[Context]` lines (what each executor received), the input tokens of each agent, and the end of the email (see [The context between agents](#the-context-between-agents)).

---

#### Scenario 3: Human in the loop - approve a tool call before it runs

The last agent of the pipeline, the **sender**, sends the follow-up email with its tool `send_email`. Sending an email cannot be undone: the tool is wrapped in an `ApprovalRequiredAIFunction` (as in Lab 09 of the Azure OpenAI track). When the sender asks to call it, the workflow **pauses** and emits a `RequestInfoEvent`; the run waits (`PendingRequests`) until your code answers with `SendResponseAsync`; then the sender continues, the tool runs, and the workflow ends. *"`AgentWorkflowBuilder.BuildSequential()` supports tool approval out of the box — no additional configuration is needed."*

Here the code approves every request: it plays the person who reviews the email. In a real application, the request would be shown to a person, and the answer could come much later.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 16** | Create the writer and the sender | • `AIAgent writerAgent = chatClient.AsAIAgent(instructions: Agents.FollowUpWriter.Instructions, name: Agents.FollowUpWriter.Name);` <br> • `AIAgent senderAgent = chatClient.AsAIAgent(instructions: Agents.MinutesSender.Instructions, name: Agents.MinutesSender.Name, tools: [new ApprovalRequiredAIFunction(AIFunctionFactory.Create(EmailTools.SendEmail, "send_email"))]);` <br> • The instructions name the tool `send_email`: keep this name |
| **TODO 17** | Build the workflow and display its path | • `Workflow workflow = AgentWorkflowBuilder.BuildSequential("meeting-minutes", [actionItemsAgent, writerAgent, senderAgent]);` <br> • `WritePipeline(workflow);` |
| **TODO 18** | Start the run | • As in TODO 8: `RunStreamingAsync(workflow, meeting)`, then `TrySendMessageAsync(new TurnToken(emitEvents: false))` |
| **TODO 19** | Watch the events and answer the approval request | • `await foreach (WorkflowEvent evt in run.WatchStreamAsync())` with a `switch (evt)`, whose first case is: <br> &nbsp;&nbsp;`case RequestInfoEvent request when request.Request.TryGetDataAs(out ToolApprovalRequestContent? approvalRequest):` <br> &nbsp;&nbsp;&nbsp;&nbsp;`WriteApprovalRequest(approvalRequest);` <br> &nbsp;&nbsp;&nbsp;&nbsp;`WriteRunStatus(await run.GetStatusAsync());` — `PendingRequests`: the run waits <br> &nbsp;&nbsp;&nbsp;&nbsp;`ToolApprovalResponseContent approval = approvalRequest.CreateResponse(approved: true);` <br> &nbsp;&nbsp;&nbsp;&nbsp;`WriteApprovalResponse(approval);` <br> &nbsp;&nbsp;&nbsp;&nbsp;`await run.SendResponseAsync(request.Request.CreateResponse(approval));` <br> &nbsp;&nbsp;&nbsp;&nbsp;`break;` <br> • Then `WorkflowOutputEvent` (with `WriteWorkflowOutput`, as in TODO 9), `ExecutorFailedEvent` and `WorkflowErrorEvent` <br> • Two answers are nested: `approvalRequest.CreateResponse(...)` is the answer of the agent framework (approved or not), `request.Request.CreateResponse(...)` wraps it into the answer to the request of the workflow |
| **TODO 20** | Display the status at the end | • `WriteRunStatus(await run.GetStatusAsync());` — `Idle`: the run has nothing left to do |

In the output, follow the approval: the sender asks for an approval, the answer of the code is added to the conversation, then the sender calls `send_email`, gets its result, and confirms.

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

To run only some scenarios: `dotnet run -- --scenarios 2` ([Choose the scenarios to run](../../../README.md#3-choose-the-scenarios-to-run)).

## The events of a run

A run is asynchronous: your code sends inputs (`RunStreamingAsync`, `TrySendMessageAsync`, `SendResponseAsync`) and reads what happens with `WatchStreamAsync()`, which ends when the run has nothing left to do. The events used in this lab:

| Event | When | What you read |
|-------|------|---------------|
| `ExecutorInvokedEvent` | An executor receives a message (a list of chat messages, a `TurnToken`) | `ExecutorId`, `Data` (the message) |
| `AgentResponseUpdateEvent` | An agent streams a piece of its answer — only when the `TurnToken` asks for events (`emitEvents: true`) | `ExecutorId`, `Update` (an `AgentResponseUpdate`: `Text`, `AuthorName`, `Contents`) |
| `WorkflowOutputEvent` | The workflow yields its output (the `OutputMessages` executor of a sequential workflow) | `As<List<ChatMessage>>()`, `ExecutorId` |
| `RequestInfoEvent` | The workflow needs an answer from outside: here, the approval of a tool call | `Request.TryGetDataAs(out ToolApprovalRequestContent? ...)`, `Request.CreateResponse(...)` |
| `ExecutorFailedEvent` | An executor threw an exception (for example the model call of an agent failed) | `ExecutorId`, `Data` (the exception) |
| `WorkflowErrorEvent` | The run stopped on an error | `Exception` |

Others exist (`WorkflowStartedEvent`, `ExecutorCompletedEvent`, `SuperStepStartedEvent`, `SuperStepCompletedEvent`...): a run advances in **supersteps**, each delivering the messages sent during the previous one. `AgentResponseUpdateEvent` derives from `WorkflowOutputEvent`: in a `switch`, test it first.

The status of a run (`await run.GetStatusAsync()`): `Running`, `PendingRequests` (it waits for the answer to a request, scenario 3), `Idle` (nothing left to do), `Ended`.

## The context between agents

What each agent receives decides what it can do, and what it costs. Scenario 2 shows both modes on the same pipeline (input tokens of a real run with `gpt-4o-mini`):

| | Whole conversation (default) | Previous agent only (`chainOnlyAgentResponses: true`) |
|---|---|---|
| The writer receives | the transcript and the action items (2 messages) | the action items (1 message) |
| The translator receives | the transcript, the action items and the email (3 messages) | the email (1 message) |
| Input tokens: extractor / writer / translator | ~576 / ~760 / ~980 | ~576 / ~250 / ~300 |
| Output of the workflow | the 4 messages of the conversation | the message of the last agent |
| Typical use | each step may need the original input or earlier results (review, analysis, multi-stage reasoning) | each step transforms the previous output (translation, formatting, progressive refinement) |

The cost of the default grows with each step: the last agent reads everything that came before. The risk of the chained mode is **losing information**: in scenario 2, the writer of the chained pipeline no longer sees the transcript — it does not know that Sarah leads the meeting, and often signs the email with a placeholder (`[Your Name]`). Choose the mode per pipeline: the translator only needs the email, but the writer would benefit from the transcript.

The official documentation says it in the same words: *"By default, each agent in the sequence consumes the previous agent's full conversation — both the input messages provided to the previous agent and its response messages. You can configure agents to consume only the previous agent's response messages instead."* For a finer control (a mode per agent, a filter), build the workflow yourself with `WorkflowBuilder` (see *Going Further*).

## Sequential workflow or code?

| | Delegation by code (Lab 01 of this folder) | Agents as tools (Lab 01 of this folder) | Sequential workflow (this lab) |
|---|---|---|---|
| Who decides the order | Your code | The model of the calling agent | The workflow, from the list given to `BuildSequential` |
| What the next agent receives | What your code builds | A `query` written by the model | The conversation so far, or the previous answer |
| What you get back | Each `AgentResponse` (typed with `RunAsync<T>`) | The text of the answer only | The conversation (`List<ChatMessage>`) and the events of the run |
| Human in the loop | Your code asks between two calls | Approvals stay inside the called agent | `RequestInfoEvent`, the run waits for the answer |
| Use it for | Two or three steps, a decision between them, a typed result | A specialist consulted when needed | A fixed series of agent steps, observed and reusable |

Beyond a few steps, writing the sequence by hand rebuilds what a workflow gives you: the passing of the context, the events, the pauses for a person, and (going further) checkpoints and tracing.

## Limits of a sequential orchestration

- **Fixed order, no way back**: an agent cannot send the task back to a previous one, nor skip a step. For decisions, use conditional edges or handoff; for iterations, a loop (writer-critic) or a group chat.
- **No free-form question to the user between agents**: *"a sequential orchestration does not include a built-in step to pause for free-form user feedback between agents"*; tool approvals are supported (scenario 3). For a back-and-forth with the user, use a handoff orchestration, or a custom workflow with a `RequestPort`.
- **Latency adds up**: every step is a full agent call, one after the other. Independent steps should run in parallel (concurrent orchestration, Lab 03 of this folder).
- **Context grows** with the default mode, and is lost with the chained mode (see above).
- **One failing agent stops the pipeline**: the run reports `ExecutorFailedEvent` and `WorkflowErrorEvent`, and yields no output.
- **The output of an agent is untrusted data** for the next one, like a document: an instruction hidden in the transcript could be followed by any step. Keep permissions in the tools, and keep a person in the loop for the actions that matter (scenario 3).

## Try it

- **Forget the `TurnToken`**: comment out `TrySendMessageAsync(new TurnToken(...))` in scenario 1. The run ends at once, without output and without any model call: the first agent keeps the transcript and waits for its turn.
- **Change the order**: put the action items extractor before the summarizer in scenario 1 and compare the output.
- **Reject the email**: in scenario 3, answer with `approvalRequest.CreateResponse(approved: false, reason: "The email must be reviewed by Sarah first.")`. The tool does not run; the sender says that the email was not sent.
- **Make an agent fail**: run scenario 1 with a deployment that does not exist (`AzureOpenAI__ChatDeploymentName=does-not-exist dotnet run -- --scenarios 1`) and look at the two error events.

## Key Concepts

| Concept | Description |
|---------|-------------|
| Sequential orchestration | Agents organized in a pipeline: each one takes its turn, in a fixed order, and builds on the previous ones |
| `Workflow` | A graph of executors linked by edges; `Name`, `StartExecutorId`, `ReflectEdges()` |
| Executor | A node of a workflow. `BuildSequential` wraps each agent in an executor, and adds `OutputMessages` at the end |
| `AgentWorkflowBuilder.BuildSequential(name, agents)` | Builds a sequential workflow from a list of agents, in the order of the list |
| `chainOnlyAgentResponses` | `true`: each agent receives only the messages of the previous agent, and the output contains only the messages of the last agent |
| `InProcessExecution.RunStreamingAsync(workflow, input)` | Starts a run in the process and returns a `StreamingRun` |
| `TurnToken(emitEvents)` | Tells an agent executor to take its turn with the messages it received; passed on from agent to agent. `emitEvents: true` streams the agents and raises `AgentResponseUpdateEvent` |
| `StreamingRun` | A run: `TrySendMessageAsync`, `WatchStreamAsync`, `SendResponseAsync`, `GetStatusAsync` |
| `WorkflowOutputEvent` | The output of the workflow: `As<List<ChatMessage>>()` |
| `ExecutorFailedEvent` / `WorkflowErrorEvent` | The errors of a run (no exception is thrown to your code) |
| `ExecutorInvokedEvent` / `AgentResponseUpdateEvent` | A message delivered to an executor / a piece of the answer of an agent |
| `ApprovalRequiredAIFunction` + `RequestInfoEvent` | A tool call that waits for an approval pauses the workflow; `SendResponseAsync` resumes it |
| `RunStatus` | `Running`, `PendingRequests`, `Idle`, `Ended` |

## Namespaces Reference

| Namespace | Purpose |
|-----------|---------|
| `OpenAI` | `OpenAIClient`, `OpenAIClientOptions` |
| `OpenAI.Chat` | `ChatClient` and the `AsAIAgent()` extension method |
| `System.ClientModel` | `ApiKeyCredential` |
| `System.ClientModel.Primitives` | `BearerTokenPolicy` |
| `Azure.Identity` | `DefaultAzureCredential` |
| `Microsoft.Agents.AI` | `AIAgent`, `AgentResponseUpdate` |
| `Microsoft.Agents.AI.Workflows` | `AgentWorkflowBuilder`, `Workflow`, `InProcessExecution`, `StreamingRun`, `TurnToken`, `WorkflowEvent` and the events, `RunStatus` |
| `Microsoft.Extensions.AI` | `ChatMessage`, `ChatRole`, `AIFunctionFactory`, `ApprovalRequiredAIFunction`, `ToolApprovalRequestContent`, `ToolApprovalResponseContent` |
| `CommonUtilities` | `ColoredConsole`, `AzureOpenAIEndpoint` |

## Expected Output

```
Endpoint: https://your-resource.openai.azure.com/
Deployment: your-deployment-name
----------------------------------------
=== Scenario 1: A sequential workflow - build it, run it, read its output ===
Workflow meeting-notes: MeetingSummarizer -> ActionItemsExtractor -> OutputMessages
Workflow output (3 messages):
  [user] (input): Meeting Transcript - Project Alpha Weekly Sync ... (2412 characters)
  [assistant] MeetingSummarizer: - Mohammed finished the authentication module and will write unit tests by Friday, ...
      - Emma's dashboard mockups are ready for review; she needs feedback by Wednesday and API specs from Mohammed for mobile layouts.
      ...
  [assistant] ActionItemsExtractor: 1. Mohammed - Write unit tests for the authentication module - Due Friday.
      2. Mohammed - Address database performance issues - Due next week.
      ...
      9. Sarah - Confirm new release date for version 2.0 with stakeholders - Due tomorrow.
Token usage: not available without the streaming events (see scenario 2).
----------------------------------------
=== Scenario 2: Follow the pipeline - what each agent receives ===
--- Context: the whole conversation (default) ---
Workflow meeting-follow-up: ActionItemsExtractor -> FollowUpWriter -> Translator -> OutputMessages
ActionItemsExtractor:
1. Mohammed - Write unit tests for the authentication module - Due date: Friday
...
FollowUpWriter:
Subject: Action Items from Project Alpha Weekly Sync
...
Best,
Sarah
Translator:
Objet : Actions à réaliser suite à la synchronisation hebdomadaire du projet Alpha
...
[Context] ActionItemsExtractor received 1 message: user
  Token Usage (ActionItemsExtractor):
    Input tokens: 576
    Output tokens: 175
    Total tokens: 751
[Context] FollowUpWriter received 2 messages: user, ActionItemsExtractor
  Token Usage (FollowUpWriter):
    Input tokens: 766
    ...
[Context] Translator received 3 messages: user, ActionItemsExtractor, FollowUpWriter
  Token Usage (Translator):
    Input tokens: 980
    ...
[Context] OutputMessages received 4 messages: user, ActionItemsExtractor, FollowUpWriter, Translator
Workflow output (4 messages): user, ActionItemsExtractor, FollowUpWriter, Translator
--- Context: the previous agent only (chainOnlyAgentResponses: true) ---
Workflow meeting-follow-up-chained: ActionItemsExtractor -> FollowUpWriter -> Translator -> OutputMessages
...
FollowUpWriter:
Subject: Follow-Up: Action Items from Our Meeting
...
Best regards,
[Your Name]
...
[Context] FollowUpWriter received 1 message: ActionItemsExtractor
  Token Usage (FollowUpWriter):
    Input tokens: 282
    ...
[Context] Translator received 1 message: FollowUpWriter
  Token Usage (Translator):
    Input tokens: 319
    ...
[Context] OutputMessages received 1 message: Translator
Workflow output (1 message): Translator
----------------------------------------
=== Scenario 3: Human in the loop - approve a tool call before it runs ===
Workflow meeting-minutes: ActionItemsExtractor -> FollowUpWriter -> MinutesSender -> OutputMessages
[Approval] send_email(to: "Sarah, Mohammed, Emma, Michael", subject: "Follow-Up on Action Items from Project Alpha Weekly Sync", body: (1112 characters)) waits for an approval
Run status: PendingRequests
[Approval] send_email: approved
Workflow output (8 messages):
  [user] (input): Meeting Transcript - Project Alpha Weekly Sync ... (2412 characters)
  [assistant] ActionItemsExtractor: 1. Mohammed - Write unit tests for the authentication module - Due date: Friday
      ...
  [assistant] FollowUpWriter: Subject: Follow-Up on Action Items from Project Alpha Weekly Sync
      ...
  [assistant] MinutesSender: (asks for an approval)
  [user] (the answer of the code): (approval: approved)
  [assistant] MinutesSender: (calls send_email)
  [tool] MinutesSender: (result: Email sent to Sarah, Mohammed, Emma, Michael (subject: Follow-Up on Action Items from Project Alpha Weekly Sync, 1112 characters).)
  [assistant] MinutesSender: The follow-up email was sent successfully to Sarah, Mohammed, Emma, and Michael.
Run status: Idle
```

Texts and token counts vary from one run to another. In the chained pipeline, the email is not always signed with a placeholder, but the writer never receives the transcript.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `'AzureOpenAI:Endpoint' is not configured` | Replace the `YOUR-...` placeholders in `appsettings.json` (or use user secrets / environment variables) |
| `401 Unauthorized` with an API key | Check the key (user secret `AzureOpenAI:APIKey`) and that it belongs to the resource of the endpoint |
| `401` / `403` with Entra ID | Run `az login` and make sure your identity has the **Cognitive Services OpenAI User** role on the resource |
| `[Error] Executor ... failed: HTTP 404 (... DeploymentNotFound)` | `ChatDeploymentName` must be the **deployment** name, not the model name |
| The run ends at once, with no output | The `TurnToken` was not sent (TODO 8): the first agent waits for its turn |
| The first agent answers as if it had received nothing ("please provide the transcript") | The input was not delivered: pass it with a type the first agent accepts — `List<ChatMessage>`, `ChatMessage` or `string`. With a variable typed `object`, `RunStreamingAsync` declares the type `object`, which no agent accepts, and the input is dropped without an error |
| Scenario 2: no text streamed, no `Token Usage` | The `TurnToken` of scenario 2 must ask for events: `new TurnToken(emitEvents: true)` |
| Scenario 2: every update is taken for the output | Test `AgentResponseUpdateEvent` before `WorkflowOutputEvent` in the `switch` |
| Scenario 2: both reports are the same | The second workflow must be built with `chainOnlyAgentResponses: true` |
| Scenario 3: the run stays in `PendingRequests` | The approval was not answered: call `run.SendResponseAsync(request.Request.CreateResponse(approval))` |
| `FileNotFoundException` for `meeting-transcript.txt` | The file is copied next to the program by the `.csproj`: run from the `Start` or `Solution` folder with `dotnet run`, and keep `AppContext.BaseDirectory` in the path |

## Provided Files

The following files are provided and should not be modified:

- `LabPrompts.cs` - Names and instructions of the agents, the same for everyone
- `OrchestrationSequential.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs` - Loads and validates the configuration
- `AzureOpenAISettings.cs` - Settings class for Azure OpenAI
- `WorkflowConsole.cs` - Display helpers and `PipelineTrace`
- `Tools/EmailTools.cs` - The mail system (tool of the sender)
- `Data/meeting-transcript.txt` - The meeting transcript

The following files should be modified:

- `appsettings.json` - Update with your Azure OpenAI endpoint and deployment
- `Program.cs` - Complete the TODOs

### NuGet packages

| Package | Why |
|---------|-----|
| `Microsoft.Agents.AI.OpenAI` | Agent Framework + `AsAIAgent()` for OpenAI clients (brings `Microsoft.Agents.AI`, `OpenAI` and `Microsoft.Extensions.AI`) |
| `Microsoft.Agents.AI.Workflows` | Workflows: `AgentWorkflowBuilder`, `InProcessExecution`, the events |
| `Microsoft.Extensions.AI.OpenAI` (10.11.0) | Fixes the streaming of the Azure OpenAI deployments that use the asynchronous content filter; the agents of a workflow stream when the `TurnToken` asks for events. Agent Framework 1.27.0 brings only 10.10.x |
| `Azure.Identity` | `DefaultAzureCredential` for Entra ID authentication |
| `Microsoft.Extensions.Configuration.*` | `appsettings.json`, user secrets and environment variables, bound to `AzureOpenAISettings` |

## Going Further

- **The workflow as an agent**: `workflow.AsAIAgent(name: "MeetingPipeline")` turns the whole pipeline into an `AIAgent`: `RunAsync(meeting)` returns the messages of the agents in an `AgentResponse`. It can then be a step of another workflow, a tool (`AsAIFunction()`, Lab 01 of this folder) or be hosted like any agent (official sample [`WorkflowAsAnAgent`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/03-workflows/Agents/WorkflowAsAnAgent)).
- **Steps without a model**: a validation, a conversion or a formatting step does not need an agent. Build the pipeline with `WorkflowBuilder` (`new WorkflowBuilder(first).AddEdge(first, second)...WithOutputFrom(last).Build()`), mixing agents and your own executors (`Executor<TInput, TOutput>`): official sample [`06_MixedWorkflowAgentsAndExecutors`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/03-workflows/_StartHere/06_MixedWorkflowAgentsAndExecutors).
- **Draw the graph**: `workflow.ToMermaidString()` and `workflow.ToDotString()` describe the workflow in Mermaid or Graphviz (official sample [`Visualization`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/03-workflows/Visualization)).
- **Pause for a long time**: checkpoints save the state of a run (`CheckpointManager`) so that it can resume later, for example after an approval given the next day (samples [`Checkpoint`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/03-workflows/Checkpoint)).
- **Trace the pipeline**: Lab 11 of the Azure OpenAI track traces the model calls with OpenTelemetry; workflows add their own spans (samples [`Observability`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/03-workflows/Observability)).
- **The other orchestrations**: concurrent (`BuildConcurrent`, Lab 03 of this folder, not migrated yet), handoff (`CreateHandoffBuilderWith`), group chat (`CreateGroupChatBuilderWith`) and Magentic: see [Orchestrations](https://learn.microsoft.com/agent-framework/workflows/orchestrations/?pivots=programming-language-csharp).

## Coming from an older version of the lab?

| Before (preview) | Now (1.27.0) |
|------------------|--------------|
| `Azure.AI.OpenAI` / `AzureOpenAIClient` | `OpenAI` / `OpenAIClient` + Azure OpenAI v1 endpoint |
| `chatClient.CreateAIAgent(name: ..., instructions: ...)` | `chatClient.AsAIAgent(instructions: ..., name: ...)`, with the texts provided in `LabPrompts.cs` |
| `InProcessExecution.StreamAsync(workflow, messages)` | `InProcessExecution.RunStreamingAsync(workflow, messages)`, disposed with `await using` |
| `(List<ChatMessage>)outputEvt.Data!` | `output.As<List<ChatMessage>>()` |
| `new TurnToken(emitEvents: true)` to wait only for the output | `emitEvents: false` when only the output is used (scenario 1); `true` to follow the agents (scenario 2) |
| Errors not handled | `ExecutorFailedEvent` and `WorkflowErrorEvent` handled in every scenario |
| One scenario (summary, then action items) | Three scenarios: the basics, the events and the context between agents (`chainOnlyAgentResponses`), a tool approval in the pipeline |
| `Microsoft.Extensions.Hosting` to read the configuration, API key in `appsettings.json` | `Microsoft.Extensions.Configuration.*`, API key in user secrets or environment variables |

## Useful Links

- [Sequential orchestration](https://learn.microsoft.com/agent-framework/workflows/orchestrations/sequential?pivots=programming-language-csharp)
- [Workflows overview](https://learn.microsoft.com/agent-framework/workflows/?pivots=programming-language-csharp)
- [Orchestrations](https://learn.microsoft.com/agent-framework/workflows/orchestrations/?pivots=programming-language-csharp)
- [Human-in-the-loop in workflows](https://learn.microsoft.com/agent-framework/workflows/human-in-the-loop?pivots=programming-language-csharp)
- [Official sample `03_AgentWorkflowPatterns`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/03-workflows/_StartHere/03_AgentWorkflowPatterns) (sequential, sequential with `chainOnlyAgentResponses`, concurrent, handoff, group chat)
- [Azure OpenAI v1 API](https://learn.microsoft.com/azure/foundry/openai/api-version-lifecycle)

## Solution

If you get stuck, check the complete solution in the `Solution/` folder.
