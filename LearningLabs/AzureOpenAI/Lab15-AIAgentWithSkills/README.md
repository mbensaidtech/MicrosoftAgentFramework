# Lab 15 - Give an Agent Skills

## Objective

In this lab, you will give an AI agent built with **Microsoft Agent Framework** and **Azure OpenAI** **Agent Skills**: portable packages of instructions, reference documents and scripts that the agent discovers, and loads only when a task needs them.

A skill packages domain expertise — an expense policy, an IT procedure, a pricing method — outside the instructions of the agent. The agent only sees the **name and the description** of each skill; when a question matches one, it **loads** the skill's instructions, **reads** the resources it needs and **runs** its scripts (*progressive disclosure*). You will:

- **Scenario 1** — give an agent two skills written as `SKILL.md` files, and watch it load the right one and read its reference document — or no skill at all when the question is unrelated;
- **Scenario 2** — define a skill in C#, with a static resource, a resource computed when it is read, and a script that runs in your program;
- **Scenario 3** — control the skill tools with **approvals**: by default every skill tool waits for an approval; you auto-approve the read-only tools and let a policy decide when a script may run.

## What You Will Learn

- What an Agent Skill is: the `SKILL.md` format (frontmatter + instructions), resources and scripts
- How progressive disclosure works: advertise → `load_skill` → `read_skill_resource` → `run_skill_script`
- How to give skills to an agent with `AgentSkillsProvider` (a context provider), from a folder or from code (`AgentInlineSkill`, `AddResource`, `AddScript`)
- Why skill tools require approval by default, and how to auto-approve with `UseToolApproval` and `AgentSkillsProvider.ReadOnlyToolsAutoApprovalRule` / `AllToolsAutoApprovalRule`
- How to approve or reject a script call with your own policy (as in Lab 09)
- When to use a skill, a tool or a workflow, and how to treat skills you did not write

## Prerequisites

- .NET 10 SDK or later
- An Azure OpenAI (or Microsoft Foundry) resource with a chat model deployment that supports function calling (for example `gpt-4o-mini` or `gpt-5.4-mini`)
- One of the following for authentication:
  - the resource **API key**, or
  - Azure CLI logged in (`az login`) with the **Cognitive Services OpenAI User** role on the resource
- Lab 03 (function tools and function calling middleware), Lab 09 (approvals), Lab 12 (context providers)

## Project Structure

```
Lab15-AIAgentWithSkills/
├── README.md
├── Start/                                  <-- Your working folder
│   ├── Program.cs                          <-- Complete the TODOs here
│   ├── skills/                             <-- Provided: the file-based skills (scenario 1)
│   │   ├── expense-policy/
│   │   │   ├── SKILL.md
│   │   │   └── references/limits.md
│   │   └── it-support/
│   │       └── SKILL.md
│   ├── ShippingRates.cs                    <-- Provided: resources and script of the code-defined skill (scenario 2)
│   ├── ScriptApprovalPolicy.cs             <-- Provided: the approval policy of the scripts (scenario 3)
│   ├── SkillsConsole.cs                    <-- Provided: skills display and call tracing
│   ├── ConfigurationHelper.cs
│   ├── AzureOpenAISettings.cs
│   ├── appsettings.json
│   └── AIAgentWithSkills.csproj
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

- `skills/` — two file-based skills, copied next to the program by the `.csproj`. Each skill is a folder with a `SKILL.md`:

  ```markdown
  ---
  name: expense-policy
  description: The expense policy of Contoso. Use for questions about business expenses, reimbursement rules, spending limits for hotels and meals, and how to file an expense report.
  metadata:
    owner: contoso-finance
    version: "2026.1"
  ---

  # Expense policy
  1. Read the `references/limits.md` resource: it holds the current spending limits. Never guess an amount.
  ...
  ```

  The frontmatter is what the agent sees before loading the skill: `name` (lowercase letters, digits and hyphens, the name of the folder) and `description` (what the skill does **and when to use it**). The body holds the instructions, loaded on demand. `references/limits.md` is a **resource**: read only when the instructions need it. `expense-policy` holds the hotel limits (145 EUR a night in Paris — a value the model cannot guess); `it-support` has no resource.
- `ShippingRates.cs` — the content of the code-defined skill of scenario 2: `RatesTable` (a static text), `Validity()` (computed at each read: today + 7 days) and `Quote(weightKg, zone)` (the script: a price and a quote id the model cannot guess, such as `QT-B250-096`).
- `ScriptApprovalPolicy.cs` — `Decide(call)` returns an `ApprovalDecision(Approved, Reason)` for a call of `run_skill_script`: only the `quote` script of `shipping-quote` is allowed, for parcels up to 30 kg.
- `SkillsConsole.cs` — imported with `using static`:
  - `WriteFileSkillsAsync(skillsPath, agent)` — the skills found in the folder, as advertised to the model (name, description);
  - `TraceSkillCall` — a **function calling middleware** (Lab 03) that writes every skill tool that runs: `[Skill] load_skill(skillName: ...) -> ...`; `SkillCallCount` counts them;
  - `FormatCall(call)` — a tool call as `name(argument: value, ...)`.

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

#### Scenario 1: Skills from SKILL.md files, loaded on demand

`AgentSkillsProvider` is a **context provider** (Lab 12): at each run, it adds the list of the skills (name and description) to the instructions, and gives the agent the tools `load_skill` and `read_skill_resource` (and `run_skill_script` when a skill has scripts). **Every skill tool requires approval by default**: here, the read-only tools are auto-approved (scenario 3 explains it).

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 4** | Create the skills provider | • `AgentSkillsProvider skillsProvider = new(skillsPath);` — it discovers each sub-folder that contains a `SKILL.md` |
| **TODO 5** | Create the agent and display the skills | • `AIAgent agent = chatClient.AsAIAgent(new ChatClientAgentOptions { Name = "EmployeeAssistant", ChatOptions = new() { Instructions = "You are the assistant of Contoso employees. Use the available skills when they match the question." }, AIContextProviders = [skillsProvider] }).AsBuilder().Use(TraceSkillCall).UseToolApproval(new ToolApprovalAgentOptions { AutoApprovalRules = [AgentSkillsProvider.ReadOnlyToolsAutoApprovalRule] }).Build();` <br> • `await WriteFileSkillsAsync(skillsPath, agent);` |
| **TODO 6** | Ask a question that matches a skill | • `const string ExpenseQuestion = "What is the maximum I can spend on a hotel night in Paris for a business trip?";` and `ColoredConsole.WriteUserLine($"User: {ExpenseQuestion}");` <br> • `int callsBefore = SkillCallCount;` <br> • `AgentResponse expenseResponse = await agent.RunAsync(ExpenseQuestion, await agent.CreateSessionAsync());` — **no** `.WithSpinner()`: `TraceSkillCall` writes during the run <br> • `ColoredConsole.WriteAssistantLine($"Agent: {expenseResponse.Text}");` <br> • `ColoredConsole.WriteSecondaryLogLine($"Skill tools run for this question: {SkillCallCount - callsBefore}");` |
| **TODO 7** | Ask a question that no skill covers | • `"What is the capital of Italy?"`, the same steps as TODO 6 (`callsBefore = SkillCallCount;`) <br> • Expect **0** skill tools: the instructions of the skills never reach the model |

---

#### Scenario 2: A skill defined in code, with a dynamic resource and a script

A skill can be built in code: useful when its content comes from your application (a database, the configuration) or must be computed at read time. A **script** of a code-defined skill is a C# delegate run in your program: its parameters become the JSON schema the model fills, exactly like a function tool.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 8** | Define the skill in code | • `AgentInlineSkill shippingSkill = new AgentInlineSkill(name: "shipping-quote", description: "Quotes the cost of shipping a parcel. Use when asked how much it costs to send a parcel, or for a shipping quote.", instructions: """...""")` with these instructions: *"Use this skill to quote the shipping cost of a parcel. 1. Read the rates resource to find the zone of the destination. 2. Read the validity resource to know until when the quote is valid. 3. Run the quote script with the weight in kg and the zone, and give the quote id and the price it returns."* <br> • `.AddResource("rates", ShippingRates.RatesTable)` (static), `.AddResource("validity", ShippingRates.Validity)` (a delegate: computed at each read), `.AddScript("quote", ShippingRates.Quote);` |
| **TODO 9** | Create the agent | • `AgentSkillsProvider skillsProvider = new(shippingSkill);` <br> • As TODO 5, with `Name = "ShippingAssistant"`, `Instructions = "You are the shipping assistant of an online store. Use the available skills."`, and `AutoApprovalRules = [AgentSkillsProvider.AllToolsAutoApprovalRule]` — this skill is your own code: the script is auto-approved too |
| **TODO 10** | Ask for a quote | • `const string QuoteQuestion = "How much does it cost to ship a 2.5 kg parcel to Germany? Give me the quote id and until when it is valid.";` <br> • `ColoredConsole.WriteUserLine(...)`, `await agent.RunAsync(QuoteQuestion, await agent.CreateSessionAsync());`, `ColoredConsole.WriteAssistantLine($"Agent: {response.Text}");` |

---

#### Scenario 3: Approve the skill tools

Skills are instructions and code that run in your agent: **the three skill tools require approval by default**. The tool approval middleware (`UseToolApproval`) lets you auto-approve some calls with rules, and returns the others to your program as `ToolApprovalRequestContent`, as in Lab 09. A rejected call never runs: the model receives the reason instead of the result.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 11** | Prepare the provider and the agent options | • A provider for the same skill as scenario 2: `AgentSkillsProvider skillsProvider = new(new AgentInlineSkill(...)...);` <br> • `ChatClientAgentOptions agentOptions = new() { Name = "ShippingAssistant", ChatOptions = new() { Instructions = "You are the shipping assistant of an online store. Use the available skills." }, AIContextProviders = [skillsProvider] };` |
| **TODO 12** | See the default: every skill tool waits for an approval | • `AIAgent strictAgent = chatClient.AsAIAgent(agentOptions).AsBuilder().Use(TraceSkillCall).UseToolApproval().Build();` <br> • `AgentResponse pending = await strictAgent.RunAsync("How much does it cost to ship a 2.5 kg parcel to Germany?", await strictAgent.CreateSessionAsync());` <br> • `foreach (ToolApprovalRequestContent request in pending.Messages.SelectMany(message => message.Contents).OfType<ToolApprovalRequestContent>())` → `ColoredConsole.WriteWarningLine($"Without auto-approval, approval requested for {FormatCall((FunctionCallContent)request.ToolCall)}");` |
| **TODO 13** | Auto-approve the read-only tools only | • `AIAgent agent = chatClient.AsAIAgent(agentOptions).AsBuilder().Use(TraceSkillCall).UseToolApproval(new ToolApprovalAgentOptions { AutoApprovalRules = [AgentSkillsProvider.ReadOnlyToolsAutoApprovalRule] }).Build();` <br> • `load_skill` and `read_skill_resource` now run without a request; `run_skill_script` still asks |
| **TODO 14** | Let the policy decide for two parcels | See the code below |

**TODO 14:**

```csharp
foreach (string question in new[] { "How much does it cost to ship a 2.5 kg parcel to Germany? Give me the quote id.", "How much does it cost to ship a 120 kg parcel to Germany? Give me the quote id." })
{
    ColoredConsole.WriteUserLine($"User: {question}");
    AgentSession session = await agent.CreateSessionAsync();
    AgentResponse response = await agent.RunAsync(question, session);

    List<ToolApprovalRequestContent> requests = [.. response.Messages.SelectMany(message => message.Contents).OfType<ToolApprovalRequestContent>()];
    while (requests.Count > 0)
    {
        List<ChatMessage> decisions = requests.ConvertAll(request =>
        {
            FunctionCallContent call = (FunctionCallContent)request.ToolCall;
            ApprovalDecision decision = ScriptApprovalPolicy.Decide(call);
            ColoredConsole.WriteWarningLine($"Approval requested for {FormatCall(call)}");
            if (decision.Approved) { ColoredConsole.WriteSuccessLine($"Policy decision: approved - {decision.Reason}"); }
            else { ColoredConsole.WriteErrorLine($"Policy decision: rejected - {decision.Reason}"); }
            return new ChatMessage(ChatRole.User, [request.CreateResponse(decision.Approved, decision.Reason)]);
        });

        response = await agent.RunAsync(decisions, session);
        requests = [.. response.Messages.SelectMany(message => message.Contents).OfType<ToolApprovalRequestContent>()];
    }

    ColoredConsole.WriteAssistantLine($"Agent: {response.Text}");
}
```

The decisions must be sent **in the same session**: the middleware keeps there the requests it surfaced, and binds each response to its request.

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

## How skills work

```
At each run, AgentSkillsProvider adds to the instructions:   "Available skills: expense-policy - <description>, it-support - <description>"
                                                              (~100 tokens per skill: name and description only)
                │
Question matches a skill?  ── no ──▶ the agent answers alone, no skill content in the context
                │ yes
                ▼
load_skill(skillName)                         ── the body of SKILL.md (or the instructions of a code-defined skill)
read_skill_resource(skillName, resourceName)  ── a reference document, a table, a template... when the instructions need it
run_skill_script(skillName, scriptName, arguments) ── a script: in-process for a code-defined skill
                │
Each of these tools requires approval by default (ToolApprovalRequestContent) — UseToolApproval + rules decide which ones run alone
```

- **Progressive disclosure keeps the context small.** Ten skills cost about ten short descriptions per run; the instructions and resources of a skill are only loaded for the questions that need them (scenario 1: zero skill tools for the unrelated question).
- **The description is the trigger.** The model decides to load a skill from its description: say what it does **and when to use it**, with the words users will use.
- **Resources are read on demand.** Put the long or changing data (limits, tables, templates) in resources, and tell the instructions when to read them.
- **Scripts run code.** A code-defined script is a C# delegate in your process. Scripts of file-based skills (Python, shell...) run outside it, through a script runner you provide; the official `SubprocessScriptRunner` is *"provided for demonstration purposes only"* — production needs sandboxing, limits and allow-lists.

## Skill, tool or workflow?

| | Function tool (Lab 03) | Agent Skill (this lab) | Workflow (later labs) |
|---|---|---|---|
| What it is | One operation the model can call | Packaged expertise: instructions, resources, scripts | A process whose steps are fixed by code |
| Loaded | Its schema is sent with every request | Name and description only; the rest on demand | Not a tool: it runs the agents |
| Who decides how | The model calls it | The model follows the instructions of the skill | The code |
| Use it for | Read or act on a system | Domain knowledge and procedures, reusable across agents | Steps that must happen in order, with side effects or approvals |

The official rule of thumb: *"if you want the AI to figure out how to accomplish a task, use a skill. If you need to guarantee what steps execute and in what order, use a workflow."* ([Agent Skills](https://learn.microsoft.com/agent-framework/agents/skills?pivots=programming-language-csharp))

Not to be confused with the `AgentSkill` of an **A2A agent card** (Lab 06): that one only describes what a remote agent can do; it has no instructions, resources or scripts.

## Security

Skills are instructions and code injected into your agent: *"Agent Skills should be treated like any third-party code you bring into your project."* The official guidance:

- **Review before use** — read `SKILL.md`, the resources and the scripts; look for instructions that try to bypass your rules or exfiltrate data.
- **Source trust** — install skills only from trusted authors or reviewed internal contributors; beware of look-alike names.
- **Sandboxing** — run file-based scripts in an isolated environment, with limited file, network and system access.
- **Audit and logging** — record which skills are loaded, which resources are read and which scripts run (`TraceSkillCall` shows the idea).
- **Keep approvals for what runs code** — auto-approve the read-only tools of trusted skills (`ReadOnlyToolsAutoApprovalRule`); keep `run_skill_script` behind an approval or a policy, and use `AllToolsAutoApprovalRule` only for skills you own (scenario 2).

## Key Concepts

| Concept | Description |
|---------|-------------|
| Agent Skill | A package of instructions, resources and scripts, following the open [Agent Skills](https://agentskills.io/) specification |
| `SKILL.md` | YAML frontmatter (`name`, `description`, optional `license`, `compatibility`, `metadata`) + markdown instructions |
| Progressive disclosure | Advertise (name, description) → load (instructions) → read resources → run scripts, only when needed |
| `AgentSkillsProvider` | Context provider that advertises the skills and registers `load_skill`, `read_skill_resource`, `run_skill_script` |
| `AgentInlineSkill` | A skill defined in code: `AddResource(name, value or delegate)`, `AddScript(name, delegate)` |
| `AgentFileSkillsSource` | Discovers file-based skills in a folder (up to two levels deep) |
| Tool approval | Every skill tool requires approval by default; `UseToolApproval(new ToolApprovalAgentOptions { AutoApprovalRules = [...] })` auto-approves some calls |
| `ReadOnlyToolsAutoApprovalRule` / `AllToolsAutoApprovalRule` | Auto-approve `load_skill` and `read_skill_resource` / all three skill tools |
| `ToolApprovalRequestContent` / `CreateResponse(approved, reason)` | A pending approval, and the decision sent back in the same session (Lab 09) |

## Namespaces Reference

| Namespace | Purpose |
|-----------|---------|
| `Microsoft.Agents.AI` | `AIAgent`, `ChatClientAgentOptions`, `AgentSkillsProvider`, `AgentInlineSkill`, `AgentFileSkillsSource`, `ToolApprovalAgentOptions`, `UseToolApproval()` |
| `Microsoft.Extensions.AI` | `ChatMessage`, `ChatRole`, `FunctionCallContent`, `ToolApprovalRequestContent`, `FunctionInvocationContext` |
| `OpenAI`, `OpenAI.Chat`, `System.ClientModel`, `System.ClientModel.Primitives`, `Azure.Identity` | The Azure OpenAI client, as in Lab 01 |
| `CommonUtilities` | `ColoredConsole`, `AzureOpenAIEndpoint` |

`Program.cs` declares `using ChatMessage = Microsoft.Extensions.AI.ChatMessage;`: `OpenAI.Chat` also has a `ChatMessage` type.

## Expected Output

```
Endpoint: https://your-resource.openai.azure.com/
Deployment: your-deployment-name
Skills folder: skills/ (2 skills)
-------------------------------------------------------------------------------
=== Scenario 1: Skills from SKILL.md files, loaded on demand ===
Skill advertised: expense-policy - The expense policy of Contoso. Use for questions about business expenses, reimbursement rules, spending limits for hotels and meals, and how to file an expense report.
Skill advertised: it-support - IT support procedures of Contoso. Use when an employee has a problem with the VPN, a password, a laptop or a phone.
User: What is the maximum I can spend on a hotel night in Paris for a business trip?
[Skill] load_skill(skillName: expense-policy) -> --- name: expense-policy description: The expense policy of Contoso. Use for questions about business expen...
[Skill] read_skill_resource(skillName: expense-policy, resourceName: references/limits.md) -> # Spending limits (2026) | Expense | Limit | |---|---| | Hotel night, Paris | 145 EUR | | Hotel night, othe...
Agent: The maximum you can spend on a hotel night in Paris for a business trip is **145 EUR**.
Skill tools run for this question: 2
User: What is the capital of Italy?
Agent: The capital of Italy is Rome.
Skill tools run for this question: 0
-------------------------------------------------------------------------------
=== Scenario 2: A skill defined in code, with a dynamic resource and a script ===
User: How much does it cost to ship a 2.5 kg parcel to Germany? Give me the quote id and until when it is valid.
[Skill] load_skill(skillName: shipping-quote) -> <name>shipping-quote</name> <description>Quotes the cost of shipping a parcel. Use when asked how much it c...
[Skill] read_skill_resource(skillName: shipping-quote, resourceName: rates) -> # Shipping rates Price = base price of the zone + price per kg x weight (kg). Parcels up to 30 kg. | Zone |...
[Skill] read_skill_resource(skillName: shipping-quote, resourceName: validity) -> Quotes made today are valid until 2026-11-01.
[Skill] run_skill_script(skillName: shipping-quote, scriptName: quote, arguments: {"weightKg":2.5,"zone":"B"}) -> {"quoteId":"QT-B250-096","priceEur":"13.75"}
Agent: The cost to ship a 2.5 kg parcel to Germany is **€13.75**. The quote ID is **QT-B250-096**, and it is valid until **November 1, 2026**.
-------------------------------------------------------------------------------
=== Scenario 3: Approve the skill tools ===
Without auto-approval, approval requested for load_skill(skillName: shipping-quote)
User: How much does it cost to ship a 2.5 kg parcel to Germany? Give me the quote id.
[Skill] load_skill(skillName: shipping-quote) -> <name>shipping-quote</name> <description>Quotes the cost of shipping a parcel. Use when asked how much it c...
[Skill] read_skill_resource(skillName: shipping-quote, resourceName: rates) -> # Shipping rates Price = base price of the zone + price per kg x weight (kg). Parcels up to 30 kg. | Zone |...
[Skill] read_skill_resource(skillName: shipping-quote, resourceName: validity) -> Quotes made today are valid until 2026-11-01.
Approval requested for run_skill_script(skillName: shipping-quote, scriptName: quote, arguments: {"weightKg":2.5,"zone":"B"})
Policy decision: approved - Parcel of 2.5 kg: within the 30 kg limit.
[Skill] run_skill_script(skillName: shipping-quote, scriptName: quote, arguments: {"weightKg":2.5,"zone":"B"}) -> {"quoteId":"QT-B250-096","priceEur":"13.75"}
Agent: The cost to ship a 2.5 kg parcel to Germany is **13.75 EUR**, and the quote ID is **QT-B250-096**.
User: How much does it cost to ship a 120 kg parcel to Germany? Give me the quote id.
[Skill] load_skill(skillName: shipping-quote) -> <name>shipping-quote</name> <description>Quotes the cost of shipping a parcel. Use when asked how much it c...
[Skill] read_skill_resource(skillName: shipping-quote, resourceName: rates) -> # Shipping rates Price = base price of the zone + price per kg x weight (kg). Parcels up to 30 kg. | Zone |...
[Skill] read_skill_resource(skillName: shipping-quote, resourceName: validity) -> Quotes made today are valid until 2026-11-01.
Approval requested for run_skill_script(skillName: shipping-quote, scriptName: quote, arguments: {"weightKg":120,"zone":"B"})
Policy decision: rejected - Parcel of 120 kg: over the 30 kg limit, a freight quote is needed from the logistics team.
Agent: The parcel of 120 kg exceeds the 30 kg limit for standard shipping quotes. A freight quote is required for such heavy parcels, and you'll need to contact the logistics team to obtain that information.
```

The wording varies; the order of the skill tools and the decisions do not. The model may skip `read_skill_resource` for `validity` when the question does not ask for it, or choose another zone for an unusual parcel: the policy only looks at the weight.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `'AzureOpenAI:Endpoint' is not configured` | Replace the `YOUR-...` placeholders in `appsettings.json` (or use user secrets / environment variables) |
| `401` / `403` | API key: check the user secret `AzureOpenAI:APIKey`. Entra ID: run `az login` and check the **Cognitive Services OpenAI User** role |
| `DirectoryNotFoundException` for `skills` | The skills are copied next to the program by the `.csproj` (`skills\**\*`): run with `dotnet run` from `Start` or `Solution` |
| No `Skill advertised` line for a skill | Its `SKILL.md` is invalid: the frontmatter must be valid YAML between `---` lines, `name` must match the folder name (lowercase, digits, hyphens) |
| Scenario 1 answers without `[Skill]` lines and guesses the limit | The provider is not in `AIContextProviders`, or the tools wait for an approval: check the auto-approval rule (TODO 5) |
| The `[Skill]` lines are garbled | Remove `.WithSpinner()`: `TraceSkillCall` writes during the run |
| Scenario 3: the decisions have no effect, or the same request comes back | Send the decisions in the **same session** as the run that produced the requests: the middleware binds each decision to a request it surfaced in that session (TODO 14) |
| Scenario 3: the script runs for 120 kg | The decision of the policy is not used: `request.CreateResponse(decision.Approved, decision.Reason)` (TODO 14) |

## Provided Files

The following files are provided and should not be modified:

- `AIAgentWithSkills.csproj` - Project file with all required dependencies (and the copy of `skills/`)
- `ConfigurationHelper.cs`, `AzureOpenAISettings.cs` - Configuration
- `skills/` - The file-based skills
- `ShippingRates.cs` - Resources and script of the code-defined skill
- `ScriptApprovalPolicy.cs` - The approval policy of the scripts
- `SkillsConsole.cs` - Display and call tracing helpers

The following files should be modified:

- `appsettings.json` - Update with your Azure OpenAI endpoint and deployment
- `Program.cs` - Complete the TODOs

### NuGet packages

| Package | Why |
|---------|-----|
| `Microsoft.Agents.AI.OpenAI` | Agent Framework + `AsAIAgent()` (brings `Microsoft.Agents.AI`, which contains Agent Skills and the tool approval middleware) |
| `Azure.Identity` | `DefaultAzureCredential` for Entra ID authentication |
| `Microsoft.Extensions.Configuration.*` | `appsettings.json`, user secrets and environment variables, bound to `AzureOpenAISettings` |

Every package is stable.

## Going Further

- **Class-based skills**: derive from `AgentClassSkill<T>`, mark resources with `[AgentSkillResource]` and scripts with `[AgentSkillScript]` — easy to ship as a NuGet package.
- **Several sources in one provider**: `new AgentSkillsProviderBuilder().UseFileSkill(path).UseSkill(shippingSkill).UseFilter(...).Build()`.
- **File-based scripts**: give the provider a script runner (`new AgentSkillsProvider(path, runner)`); run the scripts in a sandbox (container), with time and resource limits.
- **Disable an approval in the provider**: `new AgentSkillsProviderOptions { DisableLoadSkillApproval = true, DisableReadSkillResourceApproval = true }` removes the tools from the approval flow.
- **Skills from an MCP server** (`UseMcpSkills`, package `Microsoft.Agents.AI.Mcp`): **experimental**.
- **Harness Agent**: includes an `AgentSkillsProvider` by default (some of its features are still experimental).

## Useful Links

- [Agent Skills in Agent Framework](https://learn.microsoft.com/agent-framework/agents/skills?pivots=programming-language-csharp) and the [Agent Skills specification](https://agentskills.io/)
- [Official samples `02-agents/AgentSkills`](https://github.com/microsoft/agent-framework/tree/dotnet-1.26.0/dotnet/samples/02-agents/AgentSkills)
- [Tool approval](https://learn.microsoft.com/agent-framework/agents/tools/tool-approval?pivots=programming-language-csharp)
- [Azure OpenAI v1 API](https://learn.microsoft.com/azure/foundry/openai/api-version-lifecycle)

## Solution

If you get stuck, check the complete solution in the `Solution/` folder.
