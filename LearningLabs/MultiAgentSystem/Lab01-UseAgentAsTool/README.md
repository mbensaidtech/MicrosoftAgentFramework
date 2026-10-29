# Lab 01 - Delegate a Task to Another Agent

## Objective

In this lab, you will make AI agents work together with **Microsoft Agent Framework** and **Azure OpenAI**: one agent hands a task to another agent and uses its result.

There are two ways to delegate, and you will use both on a customer service case:

- **by code**: your program calls the other agent directly, gets its complete (and typed) response, and decides what happens next — a reviewer agent checks an answer before it is sent to a customer;
- **by the model**: the other agent becomes a **tool** (`AsAIFunction()`), and the model of the calling agent decides when to call it and what to ask — a front desk agent consults an order specialist and an after-sales expert.

Finally, you will see how a specialist turned into a tool can keep the context of the previous calls with a **session**.

All the agents run in the same process. To call an agent that runs in another service, see Lab 06 (A2A). Coordinating several agents with workflows (sequential, concurrent, handoff...) is not covered here.

## What You Will Learn

- Why every agent is an `AIAgent`, and why an agent keeps no conversation by itself (a session does)
- How to **delegate by code**: a direct call with `RunAsync<T>`, a typed result (`AgentResponse<T>`), and a decision taken by the code, not by a model
- Why a review returned by a model is checked before it is used
- How to **delegate by the model**: `AsAIFunction(new AIFunctionFactoryOptions { Name, Description })`
- The contract of an agent turned into a tool: a single `query` parameter, only the text of the answer comes back, the called agent does not see the conversation
- How to read the delegations of a run (`FunctionCallContent` and `FunctionResultContent`)
- How to keep the context of a specialist with `AsAIFunction(session: ...)`, and why this tool must not be called concurrently
- The limits of an agent used as a tool: hidden token usage, approvals that do not come back, latency

## Prerequisites

- .NET 10 SDK or later
- An Azure OpenAI (or Microsoft Foundry) resource with a chat model deployment that supports function calling and structured output (for example `gpt-4o-mini` or `gpt-5.4-mini`)
- One of the following for authentication:
  - the resource **API key**, or
  - Azure CLI logged in (`az login`) with the **Cognitive Services OpenAI User** role on the resource
- Labs 01 to 03 and 05 of the Azure OpenAI track: agents (`AsAIAgent()`), structured output (`RunAsync<T>`), function tools, sessions

## Project Structure

```
Lab01-UseAgentAsTool/
├── README.md
├── Start/                          <-- Your working folder
│   ├── Program.cs                  <-- Complete the TODOs here
│   ├── Models/
│   │   └── QualityReview.cs        <-- The typed review returned by the reviewer (scenario 1)
│   ├── Tools/
│   │   └── OrderTools.cs           <-- The order system, tool of the order specialist (scenario 2)
│   ├── MockedData/
│   │   └── sav-rules.yml           <-- The after-sales rules of the store
│   ├── AgentConsole.cs             <-- Display helpers (provided)
│   ├── ConfigurationHelper.cs
│   ├── AzureOpenAISettings.cs
│   ├── appsettings.json
│   └── UseAgentAsTool.csproj
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

- `MockedData/sav-rules.yml` — the after-sales rules of the store (returns, refunds, exchanges, warranty, delivery problems). The after-sales expert answers with them, the reviewer checks with them.
- `Models/QualityReview.cs` — the review the code asks the reviewer for: a `Score` from 1 to 10 and a `Feedback`. The `[Description]` attributes go into the JSON schema sent to the model.
- `Tools/OrderTools.cs` — `GetOrder(orderId)`, the order system of the store (mocked; dates are relative to today). Only the order specialist has this tool.
- `AgentConsole.cs` — display helpers, imported with `using static`:
  - `WriteTokenUsage(heading, response)` — the token usage of a run, under a heading that names the agent;
  - `WriteReview(review)` — a review of scenario 1;
  - `WriteAgentTools(tools)` — what the calling model knows about each agent tool: name, description and parameters;
  - `WriteDelegations(response)` — for each agent tool called during a run: the `query` written by the model and the first line of the text returned;
  - `WriteTicket(heading, ticket, orderId)` — a ticket of scenario 3, and whether it still contains the order number.

### Step 3: Complete the Program.cs

Open `Start/Program.cs` and complete the TODOs:

---

#### Setup: Configuration and Client Initialization

TODO 1 to 3 are the same as in Lab 01 of the Azure OpenAI track.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 1** | Create the `OpenAIClientOptions` for the Azure OpenAI v1 endpoint | • `new OpenAIClientOptions { Endpoint = ... }` <br> • `AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint)` (from `CommonUtilities`) returns the `.../openai/v1/` URI |
| **TODO 2** | Create the `OpenAIClient` | • API key: `new OpenAIClient(new ApiKeyCredential(settings.APIKey), clientOptions)` <br> • Entra ID: `new OpenAIClient(new BearerTokenPolicy(new DefaultAzureCredential(), "https://ai.azure.com/.default"), clientOptions)` <br> • Use a conditional on `string.IsNullOrWhiteSpace(settings.APIKey)` to support both |
| **TODO 3** | Get a `ChatClient` for the deployment | • `ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);` |
| **TODO 4** | Load the after-sales rules and create the after-sales expert | • `string savRules = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "MockedData", "sav-rules.yml"));` <br> • `AIAgent afterSalesAgent = chatClient.AsAIAgent(instructions: $"""...""", name: "AfterSalesExpert");` with these instructions, followed by an empty line and `{savRules}`: *"You are the after-sales expert of an online store. Answer in a few sentences, using only the rules below. If the rules do not cover the question, say so."* <br> • The agent is created once and used by scenarios 1 and 2: it keeps no conversation by itself |

---

#### Scenario 1: Delegation by code - a reviewer agent checks the answer

The after-sales expert answers a customer. Before the answer is sent, **your code** asks a reviewer agent to check it, gets a typed review back (`RunAsync<QualityReview>`, as in Lab 02), and applies a fixed rule: a score of 7 or more, the answer is sent; otherwise it is held back for a person. Then the code submits a wrong answer written by hand, to check that the rule holds it back.

The agent being checked has no say: it does not decide whether it is reviewed, and it does not read the verdict. With an agent used as a tool, the model of the calling agent would choose whether to call the reviewer, and the code would never see the score.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 5** | Create the reviewer agent | • `AIAgent reviewerAgent = chatClient.AsAIAgent(instructions: $"""...""", name: "QualityReviewer");` <br> • Instructions, followed by an empty line and `{savRules}`: *"You review the answers of the after-sales service of an online store before they are sent to a customer. Score each answer with the rules below:* <br> *- 9 to 10: correct according to the rules and answers the question* <br> *- 7 to 8: correct, but incomplete or unclear* <br> *- 4 to 6: partly wrong, or misses an important condition* <br> *- 1 to 3: contradicts the rules, or promises something the store does not offer"* |
| **TODO 6** | The after-sales expert answers the customer | • `const string CustomerQuestion = "I bought headphones 10 days ago. They are unused and still in their original packaging. Can I return them and get a refund?";` <br> • `ColoredConsole.WriteUserLine($"Customer: {CustomerQuestion}");` <br> • `AgentResponse answer = await afterSalesAgent.RunAsync(CustomerQuestion).WithSpinner("Running the after-sales expert");` <br> • `ColoredConsole.WriteAssistantLine($"{afterSalesAgent.Name}: {answer.Text}");` <br> • `WriteTokenUsage($"Token Usage ({afterSalesAgent.Name})", answer);` |
| **TODO 7** | Write the decision rule of the code | • `const int MinimumScore = 7;` <br> • `string Decide(QualityReview review) => review.Score is >= MinimumScore and <= 10 ? "Decision: sent to the customer" : $"Decision: held back for a human agent (score below {MinimumScore})";` <br> • The review comes from a model: a score outside 1–10 is not trusted |
| **TODO 8** | Review the answer and decide | • `AgentResponse<QualityReview> review = await reviewerAgent.RunAsync<QualityReview>($"Customer question: {CustomerQuestion}\nAnswer to review: {answer.Text}").WithSpinner("Running the reviewer");` <br> • `WriteReview(review.Result);` <br> • `ColoredConsole.WritePrimaryLogLine(Decide(review.Result));` <br> • `WriteTokenUsage($"Token Usage ({reviewerAgent.Name})", review);` |
| **TODO 9** | Review a wrong answer written by hand | • `const string WrongAnswer = "Of course! You can return any product within 90 days, even used, and we refund you in cash within 24 hours.";` <br> • `ColoredConsole.WriteAssistantLine($"Answer written by hand (wrong on purpose): {WrongAnswer}");` <br> • Then the same steps as TODO 8 with `WrongAnswer`, in a `wrongReview` variable |

---

#### Scenario 2: Delegation by the model - specialist agents as tools

A front desk agent answers customers, but it knows neither the orders nor the after-sales rules. It gets two specialists as tools: an **order specialist** (which has its own tool, `get_order`) and the **after-sales expert**. Its model decides which specialist to call and **writes the query** each one receives. This is the *agents as tools* step of the official learning journey: *"the lightest multi-agent pattern"*, to use when *"the delegation is model-driven, not hard-coded"* ([Agents as tools](https://learn.microsoft.com/agent-framework/journey/agents-as-tools?pivots=programming-language-csharp)).

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 10** | Create the order specialist | • `AIAgent orderAgent = chatClient.AsAIAgent(instructions: "You are the order specialist of an online store. Look up the orders with get_order and answer only with the facts of the order data.", name: "OrderSpecialist", tools: [AIFunctionFactory.Create(OrderTools.GetOrder, "get_order")]);` |
| **TODO 11** | Turn the two specialists into tools and display them | • `AIFunction orderTool = orderAgent.AsAIFunction(new AIFunctionFactoryOptions { Name = "ask_order_specialist", Description = "Asks the order specialist about an order: product, status, purchase and delivery dates. Include the order number in the query." });` <br> • `AIFunction afterSalesTool = afterSalesAgent.AsAIFunction(new AIFunctionFactoryOptions { Name = "ask_after_sales_expert", Description = "Asks the after-sales expert about the rules of the store: returns, refunds, exchanges, warranty and delivery problems. The expert does not see the conversation: give it all the facts it needs." });` <br> • `WriteAgentTools([orderTool, afterSalesTool]);` — each tool has a single `query` parameter |
| **TODO 12** | Create the front desk agent | • `AIAgent frontDeskAgent = chatClient.AsAIAgent(instructions: "You are the front desk of the customer service of an online store. You know neither the orders nor the after-sales rules: ask the specialists with your tools, then answer the customer in a few sentences.", name: "FrontDesk", tools: [orderTool, afterSalesTool]);` |
| **TODO 13** | Run the front desk | • `const string CustomerMessage = "Hello, my order ORD-1042 arrived yesterday and the laptop screen is broken. What can I do?";` <br> • `ColoredConsole.WriteUserLine($"Customer: {CustomerMessage}");` <br> • `AgentResponse response = await frontDeskAgent.RunAsync(CustomerMessage).WithSpinner("Running the front desk");` |
| **TODO 14** | Display the delegations, then the answer | • `WriteDelegations(response);` <br> • `ColoredConsole.WriteAssistantLine($"{frontDeskAgent.Name}: {response.Text}");` <br> • Compare each `query` with the customer message: it is all the specialist received |
| **TODO 15** | Display the token usage | • `WriteTokenUsage($"Token Usage ({frontDeskAgent.Name} only, without the specialists)", response);` |

---

#### Scenario 3: Keep the context of a specialist with a session

By default, `AsAIFunction()` creates **a new session for each call**: the specialist starts from nothing every time. When a specialist must build something over several calls, give the tool a session: `AsAIFunction(session: ...)`. The ticket specialist keeps the support ticket of a case up to date; it receives two facts, one per call, through each kind of tool.

Here, **the code calls the tools itself** with `InvokeAsync` and the `query` argument, exactly as the function calling loop does when a model calls them: the result does not depend on how a model would split or rephrase the facts.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 16** | Create the ticket specialist | • `AIAgent ticketAgent = chatClient.AsAIAgent(instructions: """...""", name: "TicketSpecialist", description: "Keeps the support ticket of the current customer case up to date. Give it each new fact about the case.");` <br> • Instructions: *"You keep the support ticket of ONE customer case up to date. Each message gives new facts about the case. Answer only with the complete ticket, with every fact you know, in this format:* <br> *Order: <order number, or unknown>* <br> *Problem: <problem, or unknown>* <br> *Customer request: <request, or unknown>"* |
| **TODO 17** | Turn it into two tools, without and with a session | • `AIFunction toolWithoutSession = ticketAgent.AsAIFunction();` <br> • `AgentSession ticketSession = await ticketAgent.CreateSessionAsync();` <br> • `AIFunction toolWithSession = ticketAgent.AsAIFunction(session: ticketSession);` <br> • `ColoredConsole.WritePrimaryLogLine($"Tool {toolWithoutSession.Name}: {toolWithoutSession.Description}");` — without options, the name and the description come from the agent |
| **TODO 18** | Give the two facts to each tool, then display the last ticket | • `string[] caseFacts = ["Order ORD-1042: the laptop screen arrived broken.", "The customer wants a replacement."];` <br> • `(string Heading, AIFunction Tool)[] variants = [("Ticket after the second call - a new session for each call", toolWithoutSession), ("Ticket after the second call - one session for every call", toolWithSession)];` <br> • `foreach ((string heading, AIFunction tool) in variants)`: for each fact, `ColoredConsole.WriteWarningLine($"[Call] {tool.Name}(query: \"{fact}\")");` then `ticket = await tool.InvokeAsync(new AIFunctionArguments { ["query"] = fact }).AsTask().WithSpinner("Calling the specialist");` (with `object? ticket = null;` before the loop on the facts) <br> • After the facts: `WriteTicket(heading, ticket?.ToString() ?? string.Empty, "ORD-1042");` |
| **TODO 19** | Say why there is no token usage | • `ColoredConsole.WriteSecondaryLogLine("Token usage: not available, an agent tool returns only the text of the answer.");` |

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

## Two ways to delegate

Every agent is an `AIAgent`: a local `ChatClientAgent`, a remote A2A agent (Lab 06) or an agent of a service. The same mechanisms apply to all of them.

| | Direct call (scenario 1) | Agent as a tool (scenario 2) | Agent as a tool with a session (scenario 3) |
|---|---|---|---|
| API | `other.RunAsync(...)`, `RunAsync<T>(...)` | `other.AsAIFunction(new AIFunctionFactoryOptions { Name, Description })` | `other.AsAIFunction(options, session)` |
| Who decides to delegate | **The code** | **The model** of the calling agent | The model (or the code) |
| What the called agent receives | The messages built by the code | A single `query` string written by the model | The `query`, plus the previous calls of the session |
| What comes back | The complete `AgentResponse`: text, messages, `Usage`, typed `Result` | **Only the text** of the answer | Only the text of the answer |
| Use it for | A mandatory, deterministic step whose result the code uses: review, translation, classification, extraction | A specialist consulted when needed; splitting an agent that has too many tools or instructions | A specialist that builds something over several calls |
| Limits | The code fixes the sequence; beyond a few steps, it rebuilds by hand what a workflow does | Routing depends on the description; see *Limits* below | Must not be called concurrently; the session lives as long as the tool |

## The contract of an agent turned into a tool

`AsAIFunction()` (stable, `Microsoft.Agents.AI`) creates an `AIFunction` that runs the agent:

- **Name and description**: from `AIFunctionFactoryOptions`, or else from the agent's `Name` and `Description` (scenario 3). They are **all** the calling model knows about the specialist: it uses them to decide when to call it and what to ask.
- **Input**: a single `query` string (*"Input query to invoke the agent."*, see the parameters displayed in scenario 2). The called agent does not see the conversation of the calling agent: *"It doesn't automatically inherit the outer agent's conversation history or context"* ([Agents as tools](https://learn.microsoft.com/agent-framework/journey/agents-as-tools?pivots=programming-language-csharp)). Say in the description what the query must contain.
- **Output**: only `response.Text`. The messages, the tool calls and the usage of the called agent stay inside it.
- **Session**: without the `session` parameter, *"a new session will be created for each function call, which may not preserve conversation context"*. With a session, *"avoid using the resulting function concurrently"* (XML documentation of `AsAIFunction`).

## Limits of an agent used as a tool

- **Hidden token usage**: the `Usage` of the calling agent only counts its own model calls (scenario 2), and the tool returns no usage (scenario 3). To see the cost of the whole delegation, use tracing (OpenTelemetry, Lab 11 of the Azure OpenAI track): *"If you need observability into inner agent behavior, use tracing"*.
- **Approvals do not come back**: if the called agent has a tool that requires human approval (`ApprovalRequiredAIFunction`, Lab 09), the approval request stays inside it — only text comes back. Keep the tools that need an approval on the agent that talks with the user (the official page describes the same limit for Python, and points to workflows when an approval must be interactive).
- **Latency**: *"Each delegation is a full agent invocation"* (one or more model calls of the specialist) inside the run of the caller: *"Nested invocations add up."*
- **The answer of an agent is untrusted data** for the agent that receives it, like a document (Lab 07) or the arguments of a tool (Lab 03): check what the code uses (scenario 1), and keep permissions in the tools, not in instructions.
- **Same process, same team**: agents as tools are the simplest choice when the agents live in the same application. When an agent belongs to another service, team or organization, expose it with A2A (Lab 06): the same `AsAIFunction()` then works on the remote agent.

## Try it: a vague description

In scenario 2, replace the `Description` of `ask_after_sales_expert` with `"An agent"`, and remove *"Include the order number in the query."* from the other one. Run the scenario several times and look at the delegations: the queries the front desk writes are poorer, and it may skip a specialist — *"if the tool description is vague, the model may call the wrong agent or skip it entirely"* ([Agents as tools](https://learn.microsoft.com/agent-framework/journey/agents-as-tools?pivots=programming-language-csharp)). The description is the only documentation the model reads.

## Key Concepts

| Concept | Description |
|---------|-------------|
| `AIAgent` | Base type of every agent, local or remote: `RunAsync`, `RunStreamingAsync`, `CreateSessionAsync` |
| Delegation by code | The program calls another agent directly and uses its complete response |
| `RunAsync<T>` / `AgentResponse<T>` | Structured output: the agent answers with JSON matching `T`, available as `response.Result` |
| Delegation by the model | The other agent is a tool: the model of the caller decides when to call it and what to ask |
| `AsAIFunction()` | Turns an agent into an `AIFunction`: one `query` parameter, returns the text of the answer |
| `AIFunctionFactoryOptions` | `Name` and `Description` of the tool, seen by the calling model |
| `FunctionCallContent` / `FunctionResultContent` | A tool call (name, arguments) and its result, in the messages of the response, linked by `CallId` |
| `AsAIFunction(session: ...)` | All the calls of the tool continue the same session of the called agent |
| `AIFunction.InvokeAsync()` | Runs a tool with its arguments (`AIFunctionArguments`): what the function calling loop does for the model |

## Namespaces Reference

| Namespace | Purpose |
|-----------|---------|
| `OpenAI` | `OpenAIClient`, `OpenAIClientOptions` |
| `OpenAI.Chat` | `ChatClient` and the `AsAIAgent()` extension method |
| `System.ClientModel` | `ApiKeyCredential` |
| `System.ClientModel.Primitives` | `BearerTokenPolicy` |
| `Azure.Identity` | `DefaultAzureCredential` |
| `Microsoft.Agents.AI` | `AIAgent`, `AgentResponse`, `AgentResponse<T>`, `AgentSession`, `AsAIFunction()` |
| `Microsoft.Extensions.AI` | `AIFunction`, `AIFunctionFactory`, `AIFunctionFactoryOptions`, `AIFunctionArguments` |
| `CommonUtilities` | `ColoredConsole`, `WithSpinner()`, `AzureOpenAIEndpoint` |

## Expected Output

```
Endpoint: https://your-resource.openai.azure.com/
Deployment: your-deployment-name
----------------------------------------
=== Scenario 1: Delegation by code - a reviewer agent checks the answer ===
Customer: I bought headphones 10 days ago. They are unused and still in their original packaging. Can I return them and get a refund?
AfterSalesExpert: Yes, you can return the headphones for a refund since you are within the standard return period of 30 days, and the product is unused and in its original packaging. Please ensure you have the original receipt or proof of purchase.
Token Usage (AfterSalesExpert):
  Input tokens: 755
  Output tokens: 46
  Total tokens: 801
Quality review: 10/10
  Feedback: The answer correctly states the return eligibility based on the return policy and asks for necessary proof of purchase.
Decision: sent to the customer
Token Usage (QualityReviewer):
  ...
Answer written by hand (wrong on purpose): Of course! You can return any product within 90 days, even used, and we refund you in cash within 24 hours.
Quality review: 1/10
  Feedback: The answer contradicts the rules by stating a 90-day return period, allowing used items, and suggesting a cash refund within 24 hours.
Decision: held back for a human agent (score below 7)
Token Usage (QualityReviewer):
  ...
----------------------------------------
=== Scenario 2: Delegation by the model - specialist agents as tools ===
Tool ask_order_specialist: Asks the order specialist about an order: product, status, purchase and delivery dates. Include the order number in the query.
  Parameters: {"type":"object","properties":{"query":{"description":"Input query to invoke the agent.","type":"string"}},"required":["query"]}
Tool ask_after_sales_expert: Asks the after-sales expert about the rules of the store: ...
  Parameters: {"type":"object","properties":{"query":{"description":"Input query to invoke the agent.","type":"string"}},"required":["query"]}
Customer: Hello, my order ORD-1042 arrived yesterday and the laptop screen is broken. What can I do?
[Delegation] ask_order_specialist(query: "Get details for order number ORD-1042 including product details, status, purchase date, and delivery date.")
[Delegation] ask_order_specialist returned: - **Product**: ZenBook 14 laptop (electronics)
[Delegation] ask_after_sales_expert(query: "What is the process for returning a broken item and getting a refund or exchange? The customer has reported a broken laptop screen upon delivery.")
[Delegation] ask_after_sales_expert returned: For a broken item received, such as a laptop screen, the customer should report the damage within 48 hours. ...
FrontDesk: I'm sorry to hear about the issue with your laptop. ... please report this damage within 48 hours, and ensure you have photos of the damage and the packaging. You can choose between a full refund or a free replacement. ...
Token Usage (FrontDesk only, without the specialists):
  Input tokens: 644
  Output tokens: 194
  Total tokens: 838
----------------------------------------
=== Scenario 3: Keep the context of a specialist with a session ===
Tool TicketSpecialist: Keeps the support ticket of the current customer case up to date. Give it each new fact about the case.
[Call] TicketSpecialist(query: "Order ORD-1042: the laptop screen arrived broken.")
[Call] TicketSpecialist(query: "The customer wants a replacement.")
Ticket after the second call - a new session for each call:
  Order: unknown
  Problem: unknown
  Customer request: replacement
  -> Order number of the first call (ORD-1042) kept: no
[Call] TicketSpecialist(query: "Order ORD-1042: the laptop screen arrived broken.")
[Call] TicketSpecialist(query: "The customer wants a replacement.")
Ticket after the second call - one session for every call:
  Order: ORD-1042
  Problem: The laptop screen arrived broken.
  Customer request: The customer wants a replacement.
  -> Order number of the first call (ORD-1042) kept: yes
Token usage: not available, an agent tool returns only the text of the answer.
```

Answers, scores and queries vary from one run to another. In scenario 2, the front desk may call the two specialists in the same model call (parallel tool calls) or one after the other, and the get_order call of the order specialist never appears: it happened inside the specialist.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `'AzureOpenAI:Endpoint' is not configured` | Replace the `YOUR-...` placeholders in `appsettings.json` (or use user secrets / environment variables) |
| `401 Unauthorized` with an API key | Check the key (user secret `AzureOpenAI:APIKey`) and that it belongs to the resource of the endpoint |
| `401` / `403` with Entra ID | Run `az login` and make sure your identity has the **Cognitive Services OpenAI User** role on the resource |
| `404 DeploymentNotFound` | `ChatDeploymentName` must be the **deployment** name, not the model name |
| `FileNotFoundException` for `sav-rules.yml` | The file is copied next to the program by the `.csproj`: run from the `Start` or `Solution` folder with `dotnet run`, and keep `AppContext.BaseDirectory` in the path |
| Scenario 1: the correct answer is held back | The score comes from a model: run again, or read the feedback. The rubric in the instructions of the reviewer makes the scores more stable |
| Scenario 2: no `[Delegation]` line | The front desk answered without its tools: check that both tools are in `tools:` and that the instructions say it knows neither the orders nor the rules |
| Scenario 3: both tickets contain the order number | Check that the first tool is created **without** `session:` |

## Provided Files

The following files are provided and should not be modified:

- `UseAgentAsTool.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs` - Loads and validates the configuration
- `AzureOpenAISettings.cs` - Settings class for Azure OpenAI
- `Models/QualityReview.cs` - The typed review of scenario 1
- `Tools/OrderTools.cs` - The order system (tool of the order specialist)
- `MockedData/sav-rules.yml` - The after-sales rules of the store
- `AgentConsole.cs` - Display helpers

The following files should be modified:

- `appsettings.json` - Update with your Azure OpenAI endpoint and deployment
- `Program.cs` - Complete the TODOs

### NuGet packages

| Package | Why |
|---------|-----|
| `Microsoft.Agents.AI.OpenAI` | Agent Framework + `AsAIAgent()` for OpenAI clients (brings `Microsoft.Agents.AI`, `OpenAI` and `Microsoft.Extensions.AI`) |
| `Azure.Identity` | `DefaultAzureCredential` for Entra ID authentication |
| `Microsoft.Extensions.Configuration.*` | `appsettings.json`, user secrets and environment variables, bound to `AzureOpenAISettings` |

## Going Further

- **A model per agent**: each agent can use its own deployment — `client.GetChatClient("<another deployment>").AsAIAgent(...)` — for example a larger model for the reviewer and a smaller, faster one for the specialists.
- **A remote agent as a tool**: Lab 06 (A2A client, scenario 3) gives a local agent a remote A2A agent with the same `AsAIFunction()`.
- **An agent for any MCP host**: Lab 10 exposes an agent as an MCP tool (`McpServerTool.Create(agent.AsAIFunction())`).
- **See inside the delegations**: Lab 11 traces the model calls with OpenTelemetry (`UseOpenTelemetry()`), including those of the specialists.
- **Coordinating several agents** (sequential, concurrent, handoff, group chat) is the job of workflows: see [Agent Framework workflows](https://learn.microsoft.com/agent-framework/workflows/overview?pivots=programming-language-csharp). The other labs of this folder use older APIs and are not migrated yet.

## Coming from an older version of the lab?

| Before (preview) | Now (1.26.0) |
|------------------|--------------|
| `Azure.AI.OpenAI` / `AzureOpenAIClient` | `OpenAI` / `OpenAIClient` + Azure OpenAI v1 endpoint |
| `AgentFactory` / `AgentBuilder` and one section per agent in `appsettings.json` | `chatClient.AsAIAgent(instructions: ..., name: ...)` written in `Program.cs` |
| `ChatClientAgent` created with `CreateAIAgent` | `AIAgent` created with `AsAIAgent` |
| `AgentRunResponse<T>` | `AgentResponse<T>` |
| One orchestrator calling four agents as tools, including two "judges" | Scenario 1: the reviewer is called **by the code**, which decides; the agent being checked no longer chooses to be reviewed nor reads the verdict |
| Reformulator agent (keywords) | Removed: the model of the front desk writes the query of each specialist (scenario 2) |
| — | Scenario 3: `AsAIFunction(session: ...)` |
| `DefaultDeploymentName` + a deployment per agent | `ChatDeploymentName`, one deployment for every agent |
| `Microsoft.Extensions.Hosting` to read the configuration | `Microsoft.Extensions.Configuration.*` |
| API key in `appsettings.json` | API key in user secrets or environment variables |

## Useful Links

- [Agents as tools (learning journey)](https://learn.microsoft.com/agent-framework/journey/agents-as-tools?pivots=programming-language-csharp)
- [Agent to agent (learning journey)](https://learn.microsoft.com/agent-framework/journey/agent-to-agent?pivots=programming-language-csharp)
- [Tools: using an agent as a function tool](https://learn.microsoft.com/agent-framework/agents/tools/?pivots=programming-language-csharp)
- [Structured output](https://learn.microsoft.com/agent-framework/agents/structured-output?pivots=programming-language-csharp)
- [Official sample `Agent_Step09_AsFunctionTool`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step09_AsFunctionTool)
- [Azure OpenAI v1 API](https://learn.microsoft.com/azure/foundry/openai/api-version-lifecycle)

## Solution

If you get stuck, check the complete solution in the `Solution/` folder.
