# Lab 14 - Evaluate an Agent

## Objective

In this lab, you will **evaluate** an AI agent built with **Microsoft Agent Framework** and **Azure OpenAI**: run it on a dataset of questions, check each answer automatically, and catch a regression before a change ships.

An agent is code whose behavior depends on a prompt, tools and a model: a change of instructions, of tool or of model version can break it without any compiler error. Evaluation is to an agent what a test suite is to code. You will evaluate a customer support agent with a function tool in three ways:

- **Scenario 1 — deterministic checks**: for each question, the answer must contain an expected fact and the agent must have called the expected tool with the expected arguments. The checks run in your program, with no model call.
- **Scenario 2 — LLM as a judge**: a model scores each answer for **relevance** and **coherence**, from 1 to 5, with its reasons.
- **Scenario 3 — non-regression**: the same dataset on the reference instructions and on a new version of them; the evaluation shows which questions break. Then a question is repeated to measure the consistency of the agent.

## What You Will Learn

- How to build an evaluation dataset: questions, expected outputs, expected tool calls
- How to evaluate an agent with `agent.EvaluateAsync(...)` and a `LocalEvaluator`
- The built-in checks (`EvalChecks.NonEmpty`, `ContainsExpected`, `ToolCallArgsMatch`, `KeywordCheck`, `ToolCalledCheck`) and how to write your own with `FunctionEvaluator.Create`
- How to read the results: one metric per check, with a reason, and the summary `Passed` / `Total`
- How to score answers with LLM-as-judge evaluators of `Microsoft.Extensions.AI.Evaluation` (`RelevanceEvaluator`, `CoherenceEvaluator`) and a `ChatConfiguration`
- How to evaluate responses you already have, without running the agent again
- How to compare two versions of an agent and detect a regression; how to measure consistency with `numRepetitions`

## Prerequisites

- .NET 10 SDK or later
- An Azure OpenAI (or Microsoft Foundry) resource with a chat model deployment that supports function calling (for example `gpt-4o-mini` or `gpt-5.4-mini`)
- One of the following for authentication:
  - the resource **API key**, or
  - Azure CLI logged in (`az login`) with the **Cognitive Services OpenAI User** role on the resource
- Lab 01 (client and agent), Lab 03 (function tools); Lab 11 (observability) is useful

## Project Structure

```
Lab14-AIAgentEvaluation/
├── README.md
├── Start/                          <-- Your working folder
│   ├── Program.cs                  <-- Complete the TODOs here
│   ├── EvaluationDataset.cs        <-- Provided: questions, expected outputs, expected tool calls
│   ├── Tools/OrderTools.cs         <-- Provided: the tool of the agent (get_order_status)
│   ├── EvaluationConsole.cs        <-- Provided: display of the results and of the comparison
│   ├── ConfigurationHelper.cs
│   ├── AzureOpenAISettings.cs
│   ├── appsettings.json
│   └── AIAgentEvaluation.csproj
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

- `Tools/OrderTools.cs` — `GetOrderStatus(orderId)`, the order system of the store. It returns facts the model cannot guess: tracking number `TRK-48213`, carrier `ParcelExpress`, locker `LCK-305`. An answer that contains them proves that the agent called the tool.
- `EvaluationDataset.cs` — the dataset, as three lists paired by position:
  - `Questions`: four customer questions;
  - `ExpectedOutputs`: for each question, a text the answer must contain (`TRK-48213`, `ParcelExpress`, `LCK-305`, `30`);
  - `ExpectedToolCalls`: for each question, the tool calls it needs (`get_order_status` with the right `orderId`), none for the return policy.
- `EvaluationConsole.cs` — display helpers, imported with `using static`:
  - `WriteResults(results)` — for each question: the answer, then each metric (`[pass]`, `[fail]` or `[score n]`, name, reason), then `Summary: passed/total`;
  - `WriteComparison(referenceName, reference, candidateName, candidate)` — two runs side by side, with the checks that failed;
  - `Passed(item)` — `true` when no metric of a result item is interpreted as failed.
- `Program.cs` (provided part) — `ReferenceInstructions`, the instructions of the agent under test, printed at startup.

### Step 3: Complete the Program.cs

Open `Start/Program.cs` and complete the TODOs.

---

#### Setup: Configuration and Client Initialization

Same as Lab 01.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 1** | Create the `OpenAIClientOptions` for the Azure OpenAI v1 endpoint | • `new OpenAIClientOptions { Endpoint = ... }` <br> • `AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint)` (from `CommonUtilities`) returns the `.../openai/v1/` URI |
| **TODO 2** | Create the `OpenAIClient` | • API key: `new OpenAIClient(new ApiKeyCredential(settings.APIKey), clientOptions)` <br> • Entra ID: `new OpenAIClient(new BearerTokenPolicy(new DefaultAzureCredential(), "https://ai.azure.com/.default"), clientOptions)` <br> • Use a conditional on `string.IsNullOrWhiteSpace(settings.APIKey)` to support both |
| **TODO 3** | Get a `ChatClient` for the deployment | • `ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);` |

---

#### Scenario 1: Evaluate an agent with deterministic checks

`EvaluateAsync` runs the agent once per question, turns each run into an evaluation item (question, answer, whole conversation including the tool calls), and gives the items to the evaluator. A `LocalEvaluator` runs **checks** in your program: no model call, the same result every time for the same answer.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 4** | Create the agent under test | • `AIAgent supportAgent = chatClient.AsAIAgent(instructions: ReferenceInstructions, name: "SupportAgent", tools: [AIFunctionFactory.Create(OrderTools.GetOrderStatus, "get_order_status")]);` |
| **TODO 5** | Create the local evaluator | • `LocalEvaluator checks = new(EvalChecks.NonEmpty(), EvalChecks.ContainsExpected(), EvalChecks.ToolCallArgsMatch(), FunctionEvaluator.Create("is_concise", (string response) => response.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 80));` <br> • `ContainsExpected` compares the answer with the expected output of its question (case-insensitive); `ToolCallArgsMatch` checks the expected tool calls: name, and arguments when given (a subset: extra arguments are fine) |
| **TODO 6** | Evaluate the agent on the dataset | • `AgentEvaluationResults results = await supportAgent.EvaluateAsync(EvaluationDataset.Questions, checks, expectedOutput: EvaluationDataset.ExpectedOutputs, expectedToolCalls: EvaluationDataset.ExpectedToolCalls).WithSpinner("Evaluating the agent");` |
| **TODO 7** | Display the results | • `WriteResults(results);` |

---

#### Scenario 2: Score the answers with an LLM judge

Some qualities cannot be checked with a rule: is the answer relevant to the question, is it coherent? An **LLM-as-judge** evaluator asks a model to read the question and the answer and to give a score from 1 to 5 with its reason. The evaluators of `Microsoft.Extensions.AI.Evaluation.Quality` work directly with `EvaluateAsync`: give them a `ChatConfiguration` with the chat client of the judge.

This scenario also shows how to evaluate **responses you already have** (from logs, or from a previous run): the agent runs once per question, and the responses are evaluated without running it again.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 8** | Run the agent once per question and keep the responses | • The same agent as TODO 4 <br> • `List<AgentResponse> responses = [];` then, for each question of `EvaluationDataset.Questions`: `responses.Add(await supportAgent.RunAsync(question).WithSpinner("Running the agent"));` |
| **TODO 9** | Create the judge | • `ChatConfiguration judge = new(chatClient.AsIChatClient());` <br> • `ColoredConsole.WritePrimaryLogLine("Judge: the chat client of the deployment (the same model as the agent)");` |
| **TODO 10** | Score the responses and display the scores | • `AgentEvaluationResults scores = await supportAgent.EvaluateAsync(responses, EvaluationDataset.Questions, new CompositeEvaluator(new RelevanceEvaluator(), new CoherenceEvaluator()), chatConfiguration: judge).WithSpinner("Scoring the answers");` <br> • `WriteResults(scores);` <br> • `CompositeEvaluator` runs several MEAI evaluators on each item |

---

#### Scenario 3: Catch a regression of the instructions

A new version of the instructions adds a security requirement: ask the customer for the email address of the order before looking it up. It looks harmless — the evaluation shows what it breaks. The rule is the one of a test suite: **a question that passed with the reference and fails with the candidate is a regression**, and the decision is taken by your code, on deterministic checks (never on a judge score alone, which varies from run to run).

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 11** | Create the reference and the candidate agents | • `const string CandidateInstructions = """...""";` with: *"You are the customer support assistant of an online store. Security first: before looking up an order, ask the customer for the email address used for the order, and do not call get_order_status until the customer has given it. Returns are accepted within 30 days of delivery. Answer in at most three sentences."* <br> • `referenceAgent` with `ReferenceInstructions`, `candidateAgent` with `CandidateInstructions`: same name `"SupportAgent"`, same tool |
| **TODO 12** | Evaluate both agents with the same checks | • `LocalEvaluator checks = new(EvalChecks.NonEmpty(), EvalChecks.ContainsExpected(), EvalChecks.ToolCallArgsMatch());` <br> • `AgentEvaluationResults reference = await referenceAgent.EvaluateAsync(EvaluationDataset.Questions, checks, expectedOutput: EvaluationDataset.ExpectedOutputs, expectedToolCalls: EvaluationDataset.ExpectedToolCalls).WithSpinner("Evaluating the reference");` <br> • The same for `candidate` with `candidateAgent` |
| **TODO 13** | Compare and report the regressions | • `WriteComparison("reference", reference, "candidate", candidate);` <br> • `List<EvaluationResult> referenceItems = [.. reference.Items]; List<EvaluationResult> candidateItems = [.. candidate.Items];` <br> • `int regressions = Enumerable.Range(0, referenceItems.Count).Count(i => Passed(referenceItems[i]) && !Passed(candidateItems[i]));` <br> • If `regressions > 0`: `ColoredConsole.WriteErrorLine($"Regression detected: {regressions} question(s) pass with the reference instructions and fail with the candidate");` otherwise `ColoredConsole.WriteSuccessLine("No regression: every question that passed with the reference still passes");` |
| **TODO 14** | Measure the consistency of one question | • `AgentEvaluationResults repeated = await referenceAgent.EvaluateAsync([EvaluationDataset.Questions[0]], checks, expectedOutput: [EvaluationDataset.ExpectedOutputs[0]], expectedToolCalls: [EvaluationDataset.ExpectedToolCalls[0]], numRepetitions: 3).WithSpinner("Repeating a question");` <br> • `ColoredConsole.WritePrimaryLogLine($"Repetitions of Q1: {repeated.Passed}/{repeated.Total} passed");` |

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

To run only some scenarios: `dotnet run -- --scenarios 3` ([Choose the scenarios to run](../../../README.md#3-choose-the-scenarios-to-run)).

## How evaluation works

```
EvaluationDataset (questions, expected outputs, expected tool calls)
        │
        ▼
agent.EvaluateAsync(...)  ── runs the agent once per question (or uses responses you already have)
        │                     and builds one EvalItem per run: question, answer, whole conversation
        ▼                     (tool calls included), expected output, expected tool calls
evaluator                 ── LocalEvaluator: checks in your program (EvalChecks, FunctionEvaluator)
        │                    MEAI evaluators: a judge model scores the answer (ChatConfiguration)
        ▼
AgentEvaluationResults    ── Items[i]: one metric per check / evaluator (value, reason, interpretation)
                             InputItems[i]: the question and answer that produced Items[i]
                             Passed / Total / AllPassed / AssertAllPassed()
```

- **Deterministic checks first.** They are free, fast and repeatable: use them for everything a rule can verify (a fact, a tool call, a format, a length). Keep LLM judges for what a rule cannot verify (relevance, coherence, tone), and read their reasons.
- **Expected outputs are facts, not sentences.** `ContainsExpected` looks for a text in the answer: choose facts the model must use (a tracking number), not a wording it may change.
- **The judge is a model too.** Scores vary from one run to another, and judging with the same model as the agent is a simplification: in production, use a strong model as the judge, ideally different from the agent's.
- **Evaluation costs tokens.** Each question is a full agent run, and each judge evaluator is one more model call per answer. Keep the dataset focused, and run the judges less often than the checks.
- **In a test pipeline**, `results.AssertAllPassed()` throws when a question fails: an evaluation becomes a gate before a deployment.

## Key Concepts

| Concept | Description |
|---------|-------------|
| Evaluation dataset | Questions with their expected outputs and expected tool calls; it grows with each bug found |
| `agent.EvaluateAsync(queries, evaluator, ...)` | Runs the agent on each query and evaluates the answers; `expectedOutput`, `expectedToolCalls`, `numRepetitions`, `splitter` are optional |
| `agent.EvaluateAsync(responses, queries, ...)` | Evaluates responses you already have, without running the agent |
| `EvalItem` | One evaluated interaction: `Query`, `Response`, `Conversation` (tool calls included), `ExpectedOutput`, `ExpectedToolCalls` |
| `LocalEvaluator` | Runs checks locally, no model call |
| `EvalChecks` | Built-in checks: `NonEmpty`, `ContainsExpected`, `KeywordCheck`, `ToolCalledCheck`, `ToolCallsPresent`, `ToolCallArgsMatch` |
| `FunctionEvaluator.Create(name, func)` | Turns a function into a check: on the answer, on the answer and the expected output, or on the whole `EvalItem` |
| `ExpectedToolCall(name, arguments)` | A tool call the agent should make; arguments are compared as a subset |
| `RelevanceEvaluator` / `CoherenceEvaluator` | LLM-as-judge evaluators of `Microsoft.Extensions.AI.Evaluation.Quality`: a score from 1 to 5 with a reason |
| `ChatConfiguration` | The chat client the judge evaluators use |
| `AgentEvaluationResults` | `Items` (one result per item, one metric per check), `InputItems`, `Passed`, `Total`, `AllPassed`, `AssertAllPassed()` |
| Regression | A question that passes with the reference version and fails with the candidate |

## Namespaces Reference

| Namespace | Purpose |
|-----------|---------|
| `Microsoft.Agents.AI` | `AIAgent`, `AgentResponse`, `EvaluateAsync()`, `LocalEvaluator`, `EvalChecks`, `FunctionEvaluator`, `ExpectedToolCall`, `EvalItem`, `AgentEvaluationResults` |
| `Microsoft.Extensions.AI` | `AIFunctionFactory`, `AsIChatClient()` |
| `Microsoft.Extensions.AI.Evaluation` | `ChatConfiguration`, `CompositeEvaluator`, `EvaluationResult`, `EvaluationMetric`, `BooleanMetric`, `NumericMetric` |
| `Microsoft.Extensions.AI.Evaluation.Quality` | `RelevanceEvaluator`, `CoherenceEvaluator` |
| `OpenAI`, `OpenAI.Chat`, `System.ClientModel`, `System.ClientModel.Primitives`, `Azure.Identity` | The Azure OpenAI client, as in Lab 01 |
| `CommonUtilities` | `ColoredConsole`, `WithSpinner()`, `AzureOpenAIEndpoint` |

## Expected Output

```
Endpoint: https://your-resource.openai.azure.com/
Deployment: your-deployment-name
Instructions of the agent under test:
You are the customer support assistant of an online store.
...
-------------------------------------------------------------------------------
=== Scenario 1: Evaluate an agent with deterministic checks ===
Evaluator: LocalEvaluator
Q1: Where is my order ORD-1042?  (expected: TRK-48213)
  Answer: Your order ORD-1042 was shipped on October 24, 2026, with the carrier ParcelExpress. The tracking number is TRK-48213, and it is expected to be delivered on October 27, 2026.
  [pass] non_empty: Response length 174 meets minimum 1
  [pass] contains_expected: Response contains expected output: "TRK-48213"
  [pass] tool_call_args_match: Tool call args match: 1/1 get_order_status: args match
  [pass] is_concise: Passed
...
Q4: How many days do I have to return an item?  (expected: 30)
  Answer: Returns are accepted within 30 days of delivery.
  [pass] non_empty: Response length 48 meets minimum 1
  [pass] contains_expected: Response contains expected output: "30"
  [pass] tool_call_args_match: No expected tool calls specified.
  [pass] is_concise: Passed
Summary: 4/4 questions passed
-------------------------------------------------------------------------------
=== Scenario 2: Score the answers with an LLM judge ===
Judge: the chat client of the deployment (the same model as the agent)
Evaluator: CompositeEvaluator
Q1: Where is my order ORD-1042?
  Answer: Your order ORD-1042 was shipped on October 24, 2026, with the carrier ParcelExpress. ...
  [score 4] Relevance: The RESPONSE fully addresses the QUERY by providing accurate and complete information about the order's shipping status, ...
  [score 5] Coherence: The RESPONSE is coherent as it directly answers the QUERY with relevant details in a logical sequence, ...
...
Summary: 4/4 questions passed
-------------------------------------------------------------------------------
=== Scenario 3: Catch a regression of the instructions ===
Question                                         reference                    candidate
Q1: Where is my order ORD-1042?                  pass                         FAIL (contains_expected, tool_call_args_match)
Q2: Which carrier delivers my order ORD-1042?    pass                         FAIL (contains_expected, tool_call_args_match)
Q3: Where can I pick up my order ORD-1043?       pass                         FAIL (contains_expected, tool_call_args_match)
Q4: How many days do I have to return an item?   pass                         pass
Regression detected: 3 question(s) pass with the reference instructions and fail with the candidate
Repetitions of Q1: 3/3 passed
```

Answers and judge scores vary from one run to another; the checks of scenarios 1 and 3 do not. With the candidate instructions, the agent asks for an email address instead of looking up the order: the expected fact is missing and the expected tool call was not made.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `'AzureOpenAI:Endpoint' is not configured` | Replace the `YOUR-...` placeholders in `appsettings.json` (or use user secrets / environment variables) |
| `401` / `403` | API key: check the user secret `AzureOpenAI:APIKey`. Entra ID: run `az login` and check the **Cognitive Services OpenAI User** role |
| `[fail] tool_call_args_match: ... get_order_status: not called` | The agent answered without the tool: check `tools: [AIFunctionFactory.Create(OrderTools.GetOrderStatus, "get_order_status")]` (TODO 4) |
| `[fail] contains_expected` in scenario 1 | Read the answer: the expected fact is missing. Check the instructions of the agent (`ReferenceInstructions`) |
| `ArgumentException: Found 4 queries but 3 expectedOutput values. Counts must match.` | `expectedOutput` and `expectedToolCalls` must have one entry per question, in the same order |
| Scenario 2: `[no score]` or an error message as reason | The judge call failed (quota, content filter): the other answers are still scored. Run again |
| Scenario 3: `No regression` | The candidate agent uses `ReferenceInstructions`, or the two runs use different checks (TODO 11–12) |

## Provided Files

The following files are provided and should not be modified:

- `AIAgentEvaluation.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs`, `AzureOpenAISettings.cs` - Configuration
- `Tools/OrderTools.cs` - The tool of the agent
- `EvaluationDataset.cs` - The evaluation dataset
- `EvaluationConsole.cs` - Display helpers

The following files should be modified:

- `appsettings.json` - Update with your Azure OpenAI endpoint and deployment
- `Program.cs` - Complete the TODOs

### NuGet packages

| Package | Why |
|---------|-----|
| `Microsoft.Agents.AI.OpenAI` | Agent Framework + `AsAIAgent()` (brings `Microsoft.Agents.AI`, which contains the evaluation API, and `Microsoft.Extensions.AI.Evaluation`) |
| `Microsoft.Extensions.AI.Evaluation.Quality` | LLM-as-judge evaluators: `RelevanceEvaluator`, `CoherenceEvaluator`, `GroundednessEvaluator`... |
| `Azure.Identity` | `DefaultAzureCredential` for Entra ID authentication |
| `Microsoft.Extensions.Configuration.*` | `appsettings.json`, user secrets and environment variables, bound to `AzureOpenAISettings` |

Every package is stable.

## Going Further

- **More checks**: `EvalChecks.KeywordCheck("tracking", "carrier")`, `EvalChecks.ToolCalledCheck(ToolCalledMode.Any, "a", "b")`, or a `FunctionEvaluator` on the whole `EvalItem` (for example: no internal identifier in the answer).
- **Several evaluators in one run**: `agent.EvaluateAsync(queries, evaluators: [new LocalEvaluator(...), ...])` returns one `AgentEvaluationResults` per evaluator.
- **Multi-turn conversations**: `splitter: ConversationSplitters.Full` evaluates the whole conversation instead of the last turn; `EvalItem.PerTurnItems(conversation)` scores each exchange.
- **Microsoft Foundry evaluators** (`FoundryEvals`, package `Microsoft.Agents.AI.Foundry` in preview): cloud evaluation (task adherence, tool call accuracy, safety), with results in the Foundry portal.
- **Safety evaluators** (`Microsoft.Extensions.AI.Evaluation.Safety`): content harm, protected material — they need an Azure AI Foundry project.
- **Workflows**: `run.EvaluateAsync(...)` evaluates a workflow run with a breakdown per agent (with the workflow labs).
- **Reports**: `Microsoft.Extensions.AI.Evaluation.Reporting` stores the results of each run and generates an HTML report to follow the quality over time.

## Useful Links

- [Evaluation in Agent Framework](https://learn.microsoft.com/agent-framework/agents/evaluation?pivots=programming-language-csharp)
- [Official samples `02-agents/Evaluation`](https://github.com/microsoft/agent-framework/tree/dotnet-1.26.0/dotnet/samples/02-agents/Evaluation) (`Evaluation_ExpectedOutputs`, `Evaluation_CustomEvals`, `Evaluation_SimpleEval`)
- [Microsoft.Extensions.AI.Evaluation libraries](https://learn.microsoft.com/dotnet/ai/evaluation/libraries)
- [Azure OpenAI v1 API](https://learn.microsoft.com/azure/foundry/openai/api-version-lifecycle)

## Solution

If you get stuck, check the complete solution in the `Solution/` folder.
