# Lab 03 - Concurrent Orchestration: Agents in Parallel

## Objective

In this lab, you will run AI agents **in parallel** on the same input with a **concurrent workflow** of **Microsoft Agent Framework** and **Azure OpenAI**, then combine their answers.

> *"Concurrent orchestration enables multiple agents to work on the same task in parallel. Each agent processes the input independently, and their results are collected and aggregated. This approach is well-suited for scenarios where diverse perspectives or solutions are valuable, such as brainstorming, ensemble reasoning, or voting systems."* — [Concurrent orchestration](https://learn.microsoft.com/agent-framework/workflows/orchestrations/concurrent?pivots=programming-language-csharp)

A customer writes to the after-sales service of an online store: a charger is missing from the order, and a laptop sleeve has the wrong color. Three specialists answer at the same time, each on its own topic — returns and exchanges, refunds, deliveries:

- **Scenario 1** — build the concurrent workflow, run it, and read the aggregated answers;
- **Scenario 2** — follow the run with its events: see that the agents work at the same time, that each one receives only the customer message, and what each one costs;
- **Scenario 3** — combine the answers: your own aggregator merges them in a fixed order, then a reply writer turns them into one reply to the customer.

Lab 02 of this folder chains agents one after the other (sequential orchestration). Here, the agents do not wait for each other: the run takes about the time of the slowest agent instead of the sum of all of them.

## What You Will Learn

- What a concurrent orchestration is, how it works under the hood (fan-out, one executor per agent, fan-in, aggregator), and when to use it
- How to build it with `AgentWorkflowBuilder.BuildConcurrent(...)` and run it with `InProcessExecution.RunStreamingAsync(...)` and a `TurnToken`
- What the default aggregator returns: the last message of each agent, in the order they finished — not the input, not the order of the list
- How to handle the errors of a run (`ExecutorFailedEvent`, `WorkflowErrorEvent`): one failing agent stops the whole workflow
- How to see with the events (`ExecutorInvokedEvent`, `ExecutorCompletedEvent`, `AgentResponseUpdateEvent`) that the agents work at the same time and independently, and what each one costs
- How to write a custom aggregator, and how to combine independent answers into one reply
- The limits of a concurrent orchestration, and how it compares with the sequential one

## Prerequisites

- .NET 10 SDK or later
- An Azure OpenAI (or Microsoft Foundry) resource with a chat model deployment (for example `gpt-4o-mini` or `gpt-5.4-mini`)
- One of the following for authentication:
  - the resource **API key**, or
  - Azure CLI logged in (`az login`) with the **Cognitive Services OpenAI User** role on the resource
- Lab 01 of the Azure OpenAI track (agents, streaming) and **Lab 02 of this folder** (*Sequential Orchestration*): workflows, the `TurnToken`, the events of a run

## Concurrent orchestration in a nutshell

```
                                     ┌───────────────────┐   ┌───────────┐
                               ┌───► │ ReturnsSpecialist │──►│ Batcher   │───┐
                               │     └───────────────────┘   └───────────┘   │
 customer      ┌───────┐       │     ┌───────────────────┐   ┌───────────┐   │   ┌───────────────┐
 message  ───► │ Start │───────┼───► │ RefundSpecialist  │──►│ Batcher   │───┼──►│ ConcurrentEnd │──► output
               └───────┘       │     └───────────────────┘   └───────────┘   │   │  (aggregator) │    (List<ChatMessage>)
                fan-out        │     ┌───────────────────┐   ┌───────────┐   │   └───────────────┘
                               └───► │ ShippingSpecialist│──►│ Batcher   │───┘     fan-in: waits
                                     └───────────────────┘   └───────────┘         for all of them
```

- **Fan-out.** `BuildConcurrent` adds a `Start` executor that sends the same input, then the `TurnToken`, to every agent executor at once. In a run, the executors that receive messages in the same step (a *superstep*) run at the same time: the three model calls are in flight together.
- **Independent agents.** Each agent receives **only the input**: it does not see the answers of the others, and the others do not wait for it. Each one gets its own session in each run.
- **Fan-in.** After each agent, a *batcher* collects its messages (the input it received and its answer). `ConcurrentEnd` waits for **all** the batchers (a fan-in *barrier*), then calls the **aggregator** and yields its result as the output of the workflow.
- **The default aggregator** keeps the **last message of each agent** — its answer — in the order the agents **finished**. The output does not contain the input, and its order changes from one run to another.
- **A custom aggregator** is a function that receives one list of messages per agent (in the order they finished) and returns the output you want: `BuildConcurrent(name, agents, aggregator)`. It is plain code, run once: no model call.
- **The run** works as in Lab 02: `RunStreamingAsync` delivers the input, a `TurnToken` starts the agents (`emitEvents: true` streams them), and `WatchStreamAsync()` reports what happens until the run has nothing left to do.

### When to use it

Use a concurrent orchestration when **several independent tasks apply to the same input**, and you need all their results:

| Use case | Agents in parallel |
|----------|--------------------|
| Several topics in one request | returns, refunds, deliveries (this lab) |
| Several points of view | research, marketing, legal review of a product launch |
| Ensemble reasoning, voting | the same question to several agents (or models), then a vote or a comparison |
| Independent analyses of a document | summary, sentiment, entities, compliance checks |
| Translations | the same text into several languages |

Do **not** use it when a step needs the result of another (chain them: sequential orchestration, Lab 02), when only one of the agents should answer (route the request: handoff, conditional edges), or when the agents must discuss (group chat).

## Project Structure

```
Lab03_OrchestrationConcurrent/
├── README.md
├── Start/                          <-- Your working folder
│   ├── Program.cs                  <-- Complete the TODOs here
│   ├── LabPrompts.cs               <-- Provided: names, instructions and messages of the agents
│   ├── WorkflowConsole.cs          <-- Display helpers and ParallelTrace (provided)
│   ├── MockedData/
│   │   └── sav-rules.yml           <-- The after-sales rules of the store
│   ├── ConfigurationHelper.cs
│   ├── AzureOpenAISettings.cs
│   ├── appsettings.json
│   └── OrchestrationConcurrent.csproj
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

- `MockedData/sav-rules.yml` — the after-sales rules of the store (returns, refunds, exchanges, warranty, delivery problems). Each specialist answers with them.
- `LabPrompts.cs` — the names and the instructions of the four agents, and the customer message (see Step 3). The instructions of the specialists ask each one to answer only on its topic, and to start with `Return/exchange:`, `Refund:` or `Delivery:`.
- `WorkflowConsole.cs` — display helpers, imported with `using static`:
  - `WritePipeline(workflow)` — the graph of a workflow, level by level; the executors of a level are written between brackets: they receive their messages at the same time;
  - `WriteWorkflowOutput(messages)` — the output of a workflow: each message with its role and its author, in the order of the list;
  - `WriteWorkflowOutputAuthors(messages)` — the same in one line: the authors, in the order the answers arrived;
  - `WriteAgentAnswer(name, response)` — the answer of an agent run by the code, then its token usage;
  - `WriteExecutorFailed(failed)`, `WriteWorkflowError(error)` — the errors of a run;
  - `ParallelTrace` — follows a run from its events (scenario 2): `OnExecutorInvoked` records what each agent receives and when its turn starts (it receives its `TurnToken`), `OnExecutorCompleted` when it ends, `OnAgentUpdate` keeps the streamed updates; `WriteReport()` displays for each agent the messages it received, its turn and its token usage, then the time of the run compared with the sum of the turns, and whether all the turns overlapped.

### Step 3: Complete the Program.cs

Open `Start/Program.cs` and complete the TODOs.

The names, the instructions and the messages are **provided** in `Start/LabPrompts.cs`: use its constants (`Agents.ReturnsSpecialist.Name`, `Agents.ReturnsSpecialist.Instructions`, `CustomerMessages.MissingChargerAndWrongSleeve`...) instead of writing your own texts. Everyone works with the same agents and gets comparable answers, and you can focus on the Agent Framework code. The instructions of the specialists end with the after-sales rules, read from `MockedData/sav-rules.yml` at run time: they are format strings, whose `{0}` you fill with `string.Format`. The names of the workflows (`after-sales`, `after-sales-merged`) are identifiers that you write in the code.

---

#### Setup: Configuration and Client Initialization

TODO 1 to 3 are the same as in Lab 01 of the Azure OpenAI track.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 1** | Create the `OpenAIClientOptions` for the Azure OpenAI v1 endpoint | • `OpenAIClientOptions clientOptions = new() { Endpoint = AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint) };` |
| **TODO 2** | Create the `OpenAIClient` | • API key: `new OpenAIClient(new ApiKeyCredential(settings.APIKey), clientOptions)` <br> • Entra ID: `new OpenAIClient(new BearerTokenPolicy(new DefaultAzureCredential(), "https://ai.azure.com/.default"), clientOptions)` <br> • Use a conditional on `string.IsNullOrWhiteSpace(settings.APIKey)` to support both, between the two `#pragma` lines |
| **TODO 3** | Get a `ChatClient` for the deployment | • `ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);` |
| **TODO 4** | Load the rules and create the three specialists | • `string savRules = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "MockedData", "sav-rules.yml"));` <br> • `AIAgent[] specialists = [chatClient.AsAIAgent(instructions: string.Format(CultureInfo.InvariantCulture, Agents.ReturnsSpecialist.Instructions, savRules), name: Agents.ReturnsSpecialist.Name), ...];` with `Agents.RefundSpecialist` and `Agents.ShippingSpecialist`, in this order <br> • The **name** of each agent becomes the author of its messages and the start of the id of its executor |
| **TODO 5** | The input of every workflow | • `List<ChatMessage> customerMessage = [new(ChatRole.User, CustomerMessages.MissingChargerAndWrongSleeve)];` <br> • `ChatMessage` is the one of `Microsoft.Extensions.AI` (an alias at the top of the file avoids the conflict with `OpenAI.Chat.ChatMessage`) |

---

#### Scenario 1: A concurrent workflow - fan out, then aggregate

The three specialists receive the customer message at the same time; the workflow yields their answers once all of them have answered.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 6** | Build the workflow and display its graph | • `Workflow workflow = AgentWorkflowBuilder.BuildConcurrent("after-sales", specialists);` <br> • `WritePipeline(workflow);` — `Start`, the three agents between brackets, their batchers, then `ConcurrentEnd` |
| **TODO 7** | Start the run, then send the `TurnToken` | • `await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, customerMessage);` <br> • `await run.TrySendMessageAsync(new TurnToken(emitEvents: false));` — `Start` passes it to every agent |
| **TODO 8** | Watch the events: the output and the errors | • `await foreach (WorkflowEvent evt in run.WatchStreamAsync())` with a `switch (evt)`: <br> &nbsp;&nbsp;`case WorkflowOutputEvent output: WriteWorkflowOutput(output.As<List<ChatMessage>>() ?? []); break;` <br> &nbsp;&nbsp;`case ExecutorFailedEvent failed: WriteExecutorFailed(failed); break;` <br> &nbsp;&nbsp;`case WorkflowErrorEvent error: WriteWorkflowError(error); break;` <br> • Look at the output: one answer per specialist, without the customer message, and not in the order of the array |

---

#### Scenario 2: Follow the parallel run - time, context and cost of each agent

The same workflow, run with the streaming events. The `ParallelTrace` records when each agent starts its turn (it receives its `TurnToken`: `ExecutorInvokedEvent`) and when it ends (`ExecutorCompletedEvent`), what it received, and its streamed updates (`AgentResponseUpdateEvent`), from which it computes its token usage.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 9** | Build the workflow, start the run with the streaming events | • `Workflow workflow = AgentWorkflowBuilder.BuildConcurrent("after-sales", specialists);` <br> • `await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, customerMessage);` <br> • `await run.TrySendMessageAsync(new TurnToken(emitEvents: true));` |
| **TODO 10** | Follow the events with a `ParallelTrace` | • `ParallelTrace trace = new();` and `List<ChatMessage> output = [];` before the loop <br> • `await foreach (WorkflowEvent evt in run.WatchStreamAsync())` with a `switch (evt)`: <br> &nbsp;&nbsp;`case ExecutorInvokedEvent invoked: trace.OnExecutorInvoked(invoked); break;` <br> &nbsp;&nbsp;`case ExecutorCompletedEvent completed: trace.OnExecutorCompleted(completed); break;` <br> &nbsp;&nbsp;`case AgentResponseUpdateEvent update: trace.OnAgentUpdate(update); break;` <br> &nbsp;&nbsp;`case WorkflowOutputEvent workflowOutput: output = workflowOutput.As<List<ChatMessage>>() ?? []; break;` <br> &nbsp;&nbsp;`ExecutorFailedEvent` and `WorkflowErrorEvent` as in TODO 8 <br> • `AgentResponseUpdateEvent` derives from `WorkflowOutputEvent`: put it first (the compiler refuses the reverse order) |
| **TODO 11** | Display the report, then the order of the output | • `trace.WriteReport();` <br> • `WriteWorkflowOutputAuthors(output);` |

Read the report: the three agents start together; each one received **1 message** (the customer message); the run lasts about as long as the slowest agent, not the sum of the turns; and the order of the output is the order in which they finished. Run the scenario again: the order may change.

---

#### Scenario 3: Combine the answers - a custom aggregator, then one reply

Three answers are not a reply to a customer: they repeat each other, and may even disagree (the delivery specialist may treat the sleeve as a *wrong item received*, with a free return within 14 days, while the returns specialist applies the 30-day exchange rule). The specialists cannot fix it: they do not see each other. Two steps combine their work:

1. **A custom aggregator** — plain code, run once by `ConcurrentEnd` — keeps the answer of each agent, puts them in the order of the specialists (the output no longer depends on which agent was faster), and merges them into a single message;
2. **A reply writer**, run by your code once the workflow has ended, writes one reply from the customer message and the merged answers.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 12** | Write the aggregator `MergeAnswers` | • A local function: `List<ChatMessage> MergeAnswers(IList<List<ChatMessage>> results) { ... }` <br> • `results` holds one list per agent, in the order they finished; each list is the message the agent received, then its answer: `messages[^1]` is the answer <br> • `IEnumerable<ChatMessage> answers = results.Where(messages => messages.Count > 0).Select(messages => messages[^1]).OrderBy(answer => Array.FindIndex(specialists, agent => agent.Name == answer.AuthorName));` <br> • `string merged = string.Join("\n\n", answers.Select(answer => $"{answer.AuthorName}: {answer.Text.Trim()}"));` <br> • `return [new ChatMessage(ChatRole.User, $"Answers of the specialists:\n\n{merged}") { AuthorName = "Specialists" }];` — a `user` message: it is an input for the writer, not something it said |
| **TODO 13** | Build the workflow with the aggregator, display it, start the run | • `Workflow workflow = AgentWorkflowBuilder.BuildConcurrent("after-sales-merged", specialists, MergeAnswers);` <br> • `WritePipeline(workflow);` — the same graph: only the function of `ConcurrentEnd` changed <br> • Then `RunStreamingAsync` and `TrySendMessageAsync(new TurnToken(emitEvents: false))` as in TODO 7 |
| **TODO 14** | Watch the events and keep the output | • `List<ChatMessage> merged = [];` before the loop <br> • `case WorkflowOutputEvent output: merged = output.As<List<ChatMessage>>() ?? []; WriteWorkflowOutput(merged); break;` <br> • `ExecutorFailedEvent` and `WorkflowErrorEvent` as in TODO 8 |
| **TODO 15** | Run the reply writer and display its reply | • `if (merged.Count > 0) { ... }` — nothing to write when the workflow failed <br> • `AIAgent writerAgent = chatClient.AsAIAgent(instructions: Agents.ReplyWriter.Instructions, name: Agents.ReplyWriter.Name);` <br> • `AgentResponse reply = await writerAgent.RunAsync([.. customerMessage, .. merged]).WithSpinner("Running the reply writer");` <br> • `WriteAgentAnswer(Agents.ReplyWriter.Name, reply);` |

The writer runs after the fan-in, in your code, as the delegation by code of Lab 01 of this folder. The aggregator cannot call it: in C#, an aggregator is a synchronous function (`Func<IList<List<ChatMessage>>, List<ChatMessage>>`).

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

## Concurrent or sequential?

The same three specialists, in the two orchestrations (measured with `gpt-4o-mini`):

| | Sequential (Lab 02) | Concurrent (this lab) |
|---|---|---|
| Built with | `AgentWorkflowBuilder.BuildSequential(...)` | `AgentWorkflowBuilder.BuildConcurrent(...)` |
| Order of the turns | One after the other, in the order of the list | All at the same time |
| What an agent receives | The conversation so far (or the previous answer) | The input only |
| Time of the run | The sum of the turns | About the slowest turn (scenario 2: ~1.0–1.4 s instead of ~2.4–3.4 s) |
| Output by default | The conversation: input, then the messages of every agent | The last message of each agent, in the order they finished |
| Steps that need each other | Yes: each agent builds on the previous ones | No: combine the results after the fan-in (scenario 3) |

Both can be combined: a concurrent analysis, then a sequential step that uses all the results (scenario 3 does it with code; see *Going Further* for a workflow of workflows).

## Limits of a concurrent orchestration

- **One failing agent stops the whole workflow**: the fan-in waits for every agent; when one fails, the run reports `ExecutorFailedEvent` and `WorkflowErrorEvent`, ends, and yields **no output** — the answers of the other agents are lost. Try it below.
- **The agents do not coordinate**: they may repeat each other or disagree (scenario 3). Plan a step that reconciles the answers.
- **The output order is not stable** with the default aggregator: never rely on the position of an answer; use the `AuthorName`, or a custom aggregator that sorts them.
- **The cost is multiplied**: each agent is a full model call on the same input (scenario 2: three times ~800 input tokens, the rules included). Parallel calls also hit the rate limits of the deployment sooner (`429 Too Many Requests`).
- **Latency is that of the slowest agent**: one slow agent (a larger model, a long answer, a tool) delays the output.
- **The input is untrusted** for every agent at once: an instruction hidden in a customer message reaches all of them. Keep permissions in the tools and a person in the loop for the actions that matter (Lab 02, scenario 3).

## Try it

- **Make one agent fail**: in TODO 4, create one specialist with `client.GetChatClient("does-not-exist").AsAIAgent(...)` and run scenario 1. The other two answer, but the workflow reports the error and yields no output.
- **Run it several times**: the order of the answers in scenarios 1 and 2 changes; the order of scenario 3 does not.
- **Remove the `OrderBy`** from `MergeAnswers` and compare the merged message over a few runs.
- **Use the specialists one after the other**: build `AgentWorkflowBuilder.BuildSequential("after-sales-chained", specialists)` instead (Lab 02) and compare the time and what each agent receives.

## The events of a run

The events are those of Lab 02 of this folder. In a concurrent workflow, the events of the agents of the same superstep are **interleaved**: `ExecutorInvokedEvent` (the input, then the `TurnToken`), `AgentResponseUpdateEvent` (one stream per agent, mixed), `ExecutorCompletedEvent` (each agent when it is done). The executors that are not agents (`Start`, the batchers, `ConcurrentEnd`) raise the same `ExecutorInvokedEvent` / `ExecutorCompletedEvent`; the `ParallelTrace` keeps only the agents. The `WorkflowOutputEvent` comes once, from `ConcurrentEnd`.

## Key Concepts

| Concept | Description |
|---------|-------------|
| Concurrent orchestration | Several agents work on the same input at the same time; their results are collected and aggregated |
| `AgentWorkflowBuilder.BuildConcurrent(name, agents, aggregator?)` | Builds a concurrent workflow: `Start`, one executor and one batcher per agent, `ConcurrentEnd` |
| Fan-out / fan-in | One message sent to several executors / one executor that waits for several (a barrier) |
| Superstep | A step of a run: the executors that received messages run at the same time, then the messages they send are delivered at the next step |
| Default aggregator | The last message of each agent, in the order they finished |
| Custom aggregator | `Func<IList<List<ChatMessage>>, List<ChatMessage>>`: one list per agent (the input it received, then its answer) → the output of the workflow |
| `TurnToken(emitEvents)` | Starts the agents; `emitEvents: true` streams them (`AgentResponseUpdateEvent`) |
| `ExecutorInvokedEvent` / `ExecutorCompletedEvent` | An executor receives a message / has handled it: the start and the end of the turn of an agent |
| `ExecutorFailedEvent` / `WorkflowErrorEvent` | The errors of a run: one failing agent ends the run without output |

## Namespaces Reference

| Namespace | Purpose |
|-----------|---------|
| `OpenAI` | `OpenAIClient`, `OpenAIClientOptions` |
| `OpenAI.Chat` | `ChatClient` and the `AsAIAgent()` extension method |
| `System.ClientModel` | `ApiKeyCredential` |
| `System.ClientModel.Primitives` | `BearerTokenPolicy` |
| `System.Globalization` | `CultureInfo` for `string.Format` |
| `Azure.Identity` | `DefaultAzureCredential` |
| `Microsoft.Agents.AI` | `AIAgent`, `AgentResponse` |
| `Microsoft.Agents.AI.Workflows` | `AgentWorkflowBuilder`, `Workflow`, `InProcessExecution`, `StreamingRun`, `TurnToken`, `WorkflowEvent` and the events |
| `Microsoft.Extensions.AI` | `ChatMessage`, `ChatRole` |
| `CommonUtilities` | `ColoredConsole`, `WithSpinner()`, `AzureOpenAIEndpoint` |

## Expected Output

```
Endpoint: https://your-resource.openai.azure.com/
Deployment: your-deployment-name
Customer: Hello, I received my order ORD-2087 yesterday, but the charger of the laptop is missing. I also want to return the laptop sleeve: it is the wrong color.
----------------------------------------
=== Scenario 1: A concurrent workflow - fan out, then aggregate ===
Workflow after-sales: Start -> [ReturnsSpecialist | RefundSpecialist | ShippingSpecialist] -> [Batcher/ReturnsSpecialist | Batcher/RefundSpecialist | Batcher/ShippingSpecialist] -> ConcurrentEnd
Workflow output (3 messages):
  [assistant] ShippingSpecialist: Delivery: We can ship the missing charger or provide a partial refund; please report it within 7 days. ...
  [assistant] RefundSpecialist: Refund: For the missing charger, you can receive a partial refund or have the item shipped to you; ...
  [assistant] ReturnsSpecialist: Return/exchange: You can return the laptop sleeve within 30 days as long as it is unused, in original packaging, ...
----------------------------------------
=== Scenario 2: Follow the parallel run - time, context and cost of each agent ===
[0.0 s] ShippingSpecialist starts its turn
[0.0 s] RefundSpecialist starts its turn
[0.0 s] ReturnsSpecialist starts its turn
[0.6 s] ShippingSpecialist has answered
[0.8 s] RefundSpecialist has answered
[1.0 s] ReturnsSpecialist has answered
[Context] ShippingSpecialist received 1 message: user
  Turn: from 0.0 s to 0.6 s (0.6 s)
  Token Usage (ShippingSpecialist):
    Input tokens: 809
    Output tokens: 39
    Total tokens: 848
[Context] RefundSpecialist received 1 message: user
  ...
[Context] ReturnsSpecialist received 1 message: user
  ...
[Timing] Run: 1.0 s; the same turns one after the other: about 2.4 s
[Timing] All the agents worked at the same time: yes
Workflow output (3 messages, in the order they arrived): ShippingSpecialist, RefundSpecialist, ReturnsSpecialist
----------------------------------------
=== Scenario 3: Combine the answers - a custom aggregator, then one reply ===
Workflow after-sales-merged: Start -> [ReturnsSpecialist | RefundSpecialist | ShippingSpecialist] -> [...] -> ConcurrentEnd
Workflow output (1 message):
  [user] Specialists: Answers of the specialists:

      ReturnsSpecialist: Return/exchange: You can return the laptop sleeve within 30 days, ensuring it is unused, in its original packaging, and has the tags attached. ...

      RefundSpecialist: Refund: You can receive a partial refund for the missing charger, and you can request a refund for the laptop sleeve within 30 days if it is unused ...

      ShippingSpecialist: Delivery: ...
ReplyWriter: Hello,

Thank you for reaching out. I'm sorry to hear that your charger is missing. Please contact us within 7 days so we can either ship the missing charger to you or issue a partial refund. Regarding the laptop sleeve, you can return it within 30 days as long as it is unused, in its original packaging, and has the tags attached. A refund will be processed within 5-10 days once we receive the item.

If you have any further questions, feel free to ask.

The customer service
Token Usage (ReplyWriter):
  Input tokens: 286
  Output tokens: 104
  Total tokens: 390
```

Answers, times and token counts vary from one run to another, and so does the order of the answers in scenarios 1 and 2.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `'AzureOpenAI:Endpoint' is not configured` | Replace the `YOUR-...` placeholders in `appsettings.json` (or use user secrets / environment variables) |
| `401 Unauthorized` with an API key | Check the key (user secret `AzureOpenAI:APIKey`) and that it belongs to the resource of the endpoint |
| `401` / `403` with Entra ID | Run `az login` and make sure your identity has the **Cognitive Services OpenAI User** role on the resource |
| `[Error] Executor ... failed: HTTP 404 (... DeploymentNotFound)`, no output | `ChatDeploymentName` must be the **deployment** name, not the model name. One failing agent is enough to stop the workflow |
| `429 Too Many Requests` | The parallel calls exceed the quota of the deployment: raise the tokens-per-minute limit, or run again |
| The run ends at once, with no output | The `TurnToken` was not sent: the agents wait for it |
| The specialists answer as if they had received nothing | Pass the input with a type the agents accept — `List<ChatMessage>`, `ChatMessage` or `string`; an input typed `object` is dropped without an error (Lab 02) |
| Scenario 2: `All the agents worked at the same time: no` | Check that the three agents are in one `BuildConcurrent` workflow; a very slow deployment can also make one agent finish before another starts |
| Scenario 3: the answers are not in the order of the specialists | Keep the `OrderBy` of `MergeAnswers`, and the `name:` of each agent (the order uses `AuthorName`) |
| `FileNotFoundException` for `sav-rules.yml` | The file is copied next to the program by the `.csproj`: run from the `Start` or `Solution` folder with `dotnet run`, and keep `AppContext.BaseDirectory` in the path |

## Provided Files

The following files are provided and should not be modified:

- `LabPrompts.cs` - Names and instructions of the agents, and the customer message, the same for everyone
- `OrchestrationConcurrent.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs` - Loads and validates the configuration
- `AzureOpenAISettings.cs` - Settings class for Azure OpenAI
- `WorkflowConsole.cs` - Display helpers and `ParallelTrace`
- `MockedData/sav-rules.yml` - The after-sales rules of the store

The following files should be modified:

- `appsettings.json` - Update with your Azure OpenAI endpoint and deployment
- `Program.cs` - Complete the TODOs

### NuGet packages

| Package | Why |
|---------|-----|
| `Microsoft.Agents.AI.OpenAI` | Agent Framework + `AsAIAgent()` for OpenAI clients (brings `Microsoft.Agents.AI`, `OpenAI` and `Microsoft.Extensions.AI`) |
| `Microsoft.Agents.AI.Workflows` | Workflows: `AgentWorkflowBuilder`, `InProcessExecution`, the events |
| `Microsoft.Extensions.AI.OpenAI` (10.11.0) | Fixes the streaming of the Azure OpenAI deployments that use the asynchronous content filter; the agents of a workflow stream when the `TurnToken` asks for events. Agent Framework 1.26.0 brings only 10.10.x |
| `Azure.Identity` | `DefaultAzureCredential` for Entra ID authentication |
| `Microsoft.Extensions.Configuration.*` | `appsettings.json`, user secrets and environment variables, bound to `AzureOpenAISettings` |

## Going Further

- **Your own fan-out and fan-in**: `WorkflowBuilder` builds the same shape with your own executors: `AddFanOutEdge(start, [physicist, chemist])`, `AddFanInBarrierEdge([physicist, chemist], aggregator)`, `WithOutputFrom(aggregator)`; an agent becomes an executor with `agent.BindAsExecutor(new AIAgentHostOptions { ForwardIncomingMessages = false })` (official sample [`Concurrent`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/03-workflows/Concurrent/Concurrent)). Map-reduce over a large input: sample [`MapReduce`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/03-workflows/Concurrent/MapReduce).
- **A workflow of workflows**: a workflow can become an agent (`workflow.AsAIAgent(name: ...)`) and be a step of another workflow, for example a concurrent analysis followed by a writer in a sequential workflow. The agent returns the messages of the agents it runs; read the options of `AsAIAgent` before relying on the output of a custom aggregator (official sample [`WorkflowAsAnAgent`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/03-workflows/Agents/WorkflowAsAnAgent)); sub-workflows: sample [`05_SubWorkflows`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/03-workflows/_StartHere/05_SubWorkflows).
- **Route instead of broadcasting**: when only one specialist should answer, conditional edges or a handoff orchestration send the request to the right agent ([Orchestrations](https://learn.microsoft.com/agent-framework/workflows/orchestrations/?pivots=programming-language-csharp)).
- **Several models**: each specialist can use its own deployment (`client.GetChatClient("<another deployment>").AsAIAgent(...)`), for example to compare the answers of several models (official sample [`04_MultiModelService`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/03-workflows/_StartHere/04_MultiModelService)).

## Coming from an older version of the lab?

| Before (preview) | Now (1.26.0) |
|------------------|--------------|
| `Azure.AI.OpenAI` / `AzureOpenAIClient` | `OpenAI` / `OpenAIClient` + Azure OpenAI v1 endpoint |
| `chatClient.CreateAIAgent(name: ..., instructions: ...)`, names with spaces (`"Refund Agent"`) | `chatClient.AsAIAgent(instructions: ..., name: ...)`, identifiers (`RefundSpecialist`), texts in `LabPrompts.cs`, grounded in `MockedData/sav-rules.yml` |
| `AgentWorkflowBuilder.BuildConcurrent(workflowName: ..., agents: ...)` | Unchanged, plus a custom `aggregator` (scenario 3) |
| `InProcessExecution.StreamAsync(workflow, messages)` | `InProcessExecution.RunStreamingAsync(workflow, messages)`, disposed with `await using` |
| `(List<ChatMessage>)outputEvt.Data!`, then the messages filtered on `ChatRole.User` | `output.As<List<ChatMessage>>()`: the default aggregator returns no input message |
| A follow-up agent writing the reply **in parallel** with the specialists (it could not know their answers) | A reply writer that runs **after** the fan-in, with the merged answers (scenario 3) |
| `Workflow Schema.md` | The diagram of *Concurrent orchestration in a nutshell* |
| Errors not handled | `ExecutorFailedEvent` and `WorkflowErrorEvent` handled in every scenario |
| `Microsoft.Extensions.Hosting` to read the configuration, API key in `appsettings.json` | `Microsoft.Extensions.Configuration.*`, API key in user secrets or environment variables |

## Useful Links

- [Concurrent orchestration](https://learn.microsoft.com/agent-framework/workflows/orchestrations/concurrent?pivots=programming-language-csharp)
- [Sequential orchestration](https://learn.microsoft.com/agent-framework/workflows/orchestrations/sequential?pivots=programming-language-csharp)
- [Workflows overview](https://learn.microsoft.com/agent-framework/workflows/?pivots=programming-language-csharp)
- [Orchestrations](https://learn.microsoft.com/agent-framework/workflows/orchestrations/?pivots=programming-language-csharp)
- [Official sample `03_AgentWorkflowPatterns`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/03-workflows/_StartHere/03_AgentWorkflowPatterns) (sequential, concurrent, handoff, group chat)
- [Azure OpenAI v1 API](https://learn.microsoft.com/azure/foundry/openai/api-version-lifecycle)

## Solution

If you get stuck, check the complete solution in the `Solution/` folder.
