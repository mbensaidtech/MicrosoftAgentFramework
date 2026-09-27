# Microsoft Agent Framework - Learning Labs

## Introduction

Welcome to the **Microsoft Agent Framework Learning Labs**! This repository provides a hands-on approach to learning how to build AI Agents using the **Microsoft Agents Framework** with **Azure OpenAI**.

### Objective

The objective of these labs is to guide developers through the process of building intelligent AI agents, from basic chat interactions to advanced multi-agent systems. Each lab focuses on a specific concept and includes practical scenarios that progressively build your knowledge and skills.

### How the Labs are Structured

Each lab follows a consistent structure designed for effective learning:

```
LabXX-LabName/
├── README.md       <-- Detailed instructions, hints, and explanations
├── Start/          <-- Your working folder with TODOs to complete
│   └── Program.cs  <-- Contains step-by-step TODOs with hints
└── Solution/       <-- Complete reference implementation
    └── Program.cs  <-- Fully working code to compare with your solution
```

#### How to Work with Each Lab

1. **Read the README.md** - Understand the objective and key concepts
2. **Open the `Start/` folder** - This is your working directory
3. **Complete the TODOs** - Follow the comments in `Program.cs` with basic hints
4. **Need more help?** - Check the README.md for detailed hints and explanations
5. **Stuck?** - Compare your code with the `Solution/` folder

Each lab contains one or more **scenarios** that demonstrate different aspects of the topic being covered.

---

## Prerequisites

| Requirement | Needed for | Notes |
|-------------|-----------|-------|
| **.NET 10 SDK** or newer | CLI and Dashboard | All labs and the dashboard target `net10.0`. Check with `dotnet --version`. |
| **Azure OpenAI resource** with a chat deployment | Every lab | You need the endpoint URL and the **deployment name** (not the model name). |
| **Authentication** | Every lab | Either the resource **API key**, or **Microsoft Entra ID**: `az login` + the *Cognitive Services OpenAI User* role on the resource (used automatically when no key is set). |
| Embedding deployment | Lab07+ | e.g. `text-embedding-ada-002` |
| MongoDB | Lab05, Lab12 | e.g. `mongodb://localhost:27017`. Lab05 provides a local container: `docker compose up -d` in its `MongoDB/` folder (Docker required). |
| Internet access to `https://huggingface.co/mcp` | Lab04 | Works anonymously; an optional Hugging Face token (user secret `MCPServers:HuggingFace:BearerToken`) raises the rate limits. |
| Visual Studio Code or Visual Studio 2022+ | Editing the exercises | Any editor works. |

> **Migration in progress:** the labs are being migrated to the stable release of Microsoft Agent Framework (1.22.0).
> Migrated so far: **AzureOpenAI/Lab01**, **AzureOpenAI/Lab02**, **AzureOpenAI/Lab03**, **AzureOpenAI/Lab04**, **AzureOpenAI/Lab05**. See [Migration/Migration-Plan.md](Migration/Migration-Plan.md).
> Only the migrated labs are available in the dashboard; every lab runs with the CLI.

---

## Running the Labs: CLI or Dashboard

There are two ways to run a lab. Both run **the same projects, with the same `dotnet` commands and the same configuration**, so you can switch at any time.

| | Option A — `dotnet` CLI | Option B — Lab Bench Dashboard |
|---|---|---|
| Labs available | **All labs** | Migrated labs only (AzureOpenAI Lab01, Lab02, Lab03, Lab04, Lab05) |
| How you run a lab | `dotnet run` in a terminal | **Run** button in a local web page |
| Configuration | `appsettings.json`, user secrets or environment variables | The same, plus an **Azure OpenAI settings** form |
| Result | You read the console output | **Passed / Failed** verdict, automatic checks, token usage, run history |
| Interactive labs (`Console.ReadLine`) | Supported (keyboard) | Supported for labs declared interactive: an input box asks for each value |
| Best for | Every lab, debugging, IDE workflow | Getting started, quick feedback on the migrated labs |

Whichever you choose, **configure Azure OpenAI once first**: the configuration is shared by every lab and by both options.

### Configure Azure OpenAI

Each lab reads its settings from these sources (the last one wins):

1. `appsettings.json` of the lab (ships with `YOUR-…` placeholders)
2. **User secrets** shared by all migrated labs (id `microsoft-agent-framework-learninglabs`, stored outside the repository)
3. **Environment variables** `AzureOpenAI__*`

Pick one method:

- **Dashboard form**, no terminal needed — see [step 4 of Option B](#4-configure-azure-openai-from-the-dashboard).
- **User secrets** (recommended for the API key), from any migrated lab project folder:
  ```bash
  cd LearningLabs/AzureOpenAI/Lab01-FirstBasicAIAgent/Start
  dotnet user-secrets set "AzureOpenAI:Endpoint" "https://my-resource.openai.azure.com/"
  dotnet user-secrets set "AzureOpenAI:ChatDeploymentName" "gpt-4o"
  dotnet user-secrets set "AzureOpenAI:APIKey" "<your-api-key>"   # optional: omit it to use Microsoft Entra ID
  dotnet user-secrets list
  ```
- **Environment variables**, required for the labs that are **not migrated yet** (they don't read user secrets) — see [Environment Variables Configuration](#environment-variables-configuration).
- **`appsettings.json`** of a single lab — works, but never commit a real key.

If the endpoint or the deployment is missing, a migrated lab stops with *"'AzureOpenAI:Endpoint' is not configured"*. No API version is needed: the labs use the Azure OpenAI **v1** API (`…/openai/v1/`).

---

### Option A — Run a lab with the `dotnet` CLI

#### 1. Find the lab

Labs live in `LearningLabs/<Part>/<LabFolder>/`, and each lab has two projects:

| Folder | Content | When to run it |
|--------|---------|----------------|
| `Start/` | The exercise, with `TODO` comments to complete | While you work on the lab |
| `Solution/` | The complete reference implementation | To see the expected behavior, or to compare |

```bash
ls LearningLabs/AzureOpenAI        # Part 1: Lab01-FirstBasicAIAgent, Lab02-AIAgentWithSO, ...
ls LearningLabs/MultiAgentSystem   # Part 2: Lab01-UseAgentAsTool, Lab02_OrchestrationSequential, ...
```

What each lab covers is listed in [Labs Overview](#labs-overview).

#### 2. Run it

From the lab folder:

```bash
cd LearningLabs/AzureOpenAI/Lab01-FirstBasicAIAgent/Start
dotnet run
```

Or from the repository root:

```bash
# My exercise
dotnet run --project LearningLabs/AzureOpenAI/Lab01-FirstBasicAIAgent/Start

# Reference solution
dotnet run --project LearningLabs/AzureOpenAI/Lab01-FirstBasicAIAgent/Solution
```

The first `dotnet run` restores the NuGet packages and builds the project (including the shared `CommonUtilities` project).
To only check that your code compiles: `dotnet build LearningLabs/AzureOpenAI/Lab01-FirstBasicAIAgent/Start`.

> **Lab06 (A2A)** has two separate labs: start `Lab06_A2AServer` in one terminal, then `Lab06_A2AClient` in another.

#### 3. Read the result

The program prints each scenario (`=== Scenario 1: … ===`), the agent answers and, in most labs, the token usage (`Input tokens: …`).
Compare it with the output of `Solution/`. Stop a running lab with `Ctrl+C`.

---

### Option B — Run a lab with the Lab Bench Dashboard

The dashboard is a local web UI (listening on `127.0.0.1` only) that builds and runs the labs for you and shows the live output, a verdict, the token usage and the run history. It is optional: no lab depends on it.

#### 1. Start the dashboard

Run it from its own folder (it locates the labs relative to this folder):

```bash
cd Dashboard/LabDashboard
dotnet run
```

The terminal prints `Lab dashboard running on http://127.0.0.1:5057 (Ctrl+C to stop)`. Keep it open; stop the dashboard with `Ctrl+C`.

> Port 5057 already in use? `Dashboard__Port=5058 dotnet run` (macOS/Linux) or `$env:Dashboard__Port=5058; dotnet run` (PowerShell).

#### 2. Open the interface

Open <http://127.0.0.1:5057> in your browser. The top bar shows:

- the runner status (**Runner connected**);
- the **Azure OpenAI** button with the authentication status: *API key*, *Microsoft Entra ID* or *Not configured*;
- the **EN | FR** language selector.

The **Labs** list on the left shows every registered lab with its progress (*Not started*, *In progress*, *Completed*) and last result.

#### 3. Select a lab

Click a lab in the list. Its page has five tabs:

| Tab | Content |
|-----|---------|
| **Run** | Run the lab and follow its execution |
| **Instructions** | The lab `README.md` (the French version when the lab provides a `README.fr.md`) |
| **History** | The last 30 runs: project, result, date, duration, tokens |
| **About** | What you will learn, the lab folder, the NuGet packages, the equivalent `dotnet` commands, and the checks used to judge a run |
| **Solution** | The solution files that differ from the exercise, behind a *"Try it yourself first"* gate |

#### 4. Configure Azure OpenAI from the dashboard

Click **Azure OpenAI** in the top bar and fill in:

- **Endpoint** — e.g. `https://my-resource.openai.azure.com/`
- **Chat deployment** — the deployment name in your resource (not the model name)
- **API key** — optional; leave it empty to use Microsoft Entra ID (`az login`). Once saved, the key is never displayed again (*"•••• stored — leave empty to keep it"*); **Remove API key** deletes it.

Click **Save**. The values are written to the shared user secrets (outside the repository) and are used by the **next run, from the dashboard and from `dotnet run` alike**. The form shows where each value comes from and warns when an `AzureOpenAI__*` environment variable overrides it.

#### 5. Run the lab

In the **Run** tab:

1. Choose the project: **My exercise** (`Start/`) or **Reference solution** (`Solution/`).
2. Click **Run**. The dashboard runs `dotnet build`, then `dotnet run --no-build` — the same commands as the CLI.
3. Follow the steps **Build → Run → Checks**. **Cancel** stops the run (the whole process tree is killed); a run also stops when the lab timeout is reached.

Only one run can be in progress at a time.

**Interactive labs** (the program calls `Console.ReadLine()`): during the run, an input box appears under the output. When the program reads, the box is focused with a *Waiting for input #n* badge; type the value and press **Enter** — it is echoed in the output and sent to the program. You can type ahead, and **End input** closes the standard input (the next `ReadLine()` returns `null`, like `Ctrl+D`). Waiting for your answer does not count against the lab time limit; a prompt left unanswered for 10 minutes stops the run. The **About** tab says whether a lab is interactive.

#### 6. Read the results, logs and execution details

| Where | What it shows |
|-------|---------------|
| **Output** terminal | The program output (stdout/stderr), live; the console spinner is shown as a status line. **Copy output** copies it. |
| **Verdict** | **Passed** when the build succeeds, the exit code is 0 **and** every check matches the output; otherwise **Failed** with the reason (build errors, exit code, failed checks, *Timed out*, *Cancelled*), plus the build/run durations and the exit code. |
| **Checks** | Each expected behavior of the lab (e.g. *"Scenario 2 answered"*) and its status. The delivered exercise fails them until its TODOs are completed. |
| **Errors / Build warnings** | Compiler errors (`error CS…`) or the program's stderr. A missing Azure OpenAI setting links to the settings form. |
| **Token usage** | Input / output / reasoning / total tokens per scenario, as printed by the lab; *Not available* when the lab prints none. No cost estimate. |
| **History** tab | Past runs, stored in `Dashboard/.data/history.json` (git-ignored). |

The API key never appears in the output, the history or a copied output: it is replaced by `••••`.

#### Dashboard limitations

- Only the migrated labs are listed (AzureOpenAI Lab01–Lab05): run the other labs with the CLI.
- Interactive input works for the labs declared interactive in the dashboard catalog; key-by-key input (`Console.ReadKey`) is not supported.
- Console colors are not rendered.

More details (architecture, HTTP API, security, registering a new lab, tests): [Dashboard/README.md](Dashboard/README.md).

---

## Labs Overview

The labs are organized into two main parts:

### Part 1: AzureOpenAI - Individual AI Agents

This part focuses on building **individual AI agents** with various capabilities. You will learn how to create agents, add tools, handle structured output, connect to external services, and implement human-in-the-loop patterns.

---

### Lab 01 - First Basic AI Agent

**Learn the fundamentals of creating an AI Agent**

| Scenario | Description |
|----------|-------------|
| Scenario 1 | Create a basic agent with default settings |
| Scenario 2 | Create an agent with custom instructions and name |
| Scenario 3 | Use ChatMessages for fine-grained control |
| Scenario 4 | Monitor token usage from responses |
| Scenario 5 | Stream the response with `RunStreamingAsync` |

---

### Lab 02 - AI Agent with Structured Output

**Learn how to get structured JSON responses from an AI Agent**

| Scenario | Description |
|----------|-------------|
| Scenario 1 | Manual structured output: JSON format described in the instructions, parsed with `JsonSerializer` |
| Scenario 2 | Automatic structured output with `RunAsync<T>` and `AgentResponse<T>.Result` (Recommended) |
| Scenario 3 | Response format configured on the agent (`ChatClientAgentOptions` + `ChatResponseFormat.ForJsonSchema<T>()`) |
| Scenario 4 | Response format for a single run (`AgentRunOptions`) with streaming |

---

### Lab 03 - AI Agent with Function Tools

**Learn how to give agents access to external functions and data**

| Scenario | Description |
|----------|-------------|
| Scenario 1 | Basic function tools calling: `AIFunctionFactory.Create()`, `[Description]`, `AsAIAgent(..., tools: ...)` |
| Scenario 2 | Function tools discovered with reflection (`MethodInfo` + `AIFunctionFactory.Create(method, target)`) |
| Scenario 3 | Static tools with dependency injection (`IServiceProvider` parameter + `AsAIAgent(..., services: ...)`) |
| Scenario 4 | Function calling middleware that traces every tool call (`AsBuilder().Use(...)`) |

---

### Lab 04 - AI Agent with MCP Client

**Learn how to integrate with Model Context Protocol (MCP) servers**

| Scenario | Description |
|----------|-------------|
| Scenario 1 | Connect to the Hugging Face MCP server (`McpClient.CreateAsync` + `HttpClientTransport`) and discover its tools (`ListToolsAsync`) |
| Scenario 2 | Agent with the MCP tools (`ChatClientAgentOptions`) and structured output (`RunAsync<T>`), with the list of tools called |
| Scenario 3 | Give the agent only the MCP tool it needs, and compare the token usage |

---

### Lab 05 - AI Agent with Sessions

**Learn how to hold, save and resume conversations (`AgentSession`) and where their chat history is stored (`ChatHistoryProvider`)**

| Scenario | Description |
|----------|-------------|
| Scenario 1 | Session with the default in-memory chat history: `CreateSessionAsync`, `SerializeSessionAsync` / `DeserializeSessionAsync`, the messages kept in the session |
| Scenario 2 | Custom `ChatHistoryProvider` storing the chat history in a vector store (`InMemoryVectorStore`): the serialized session only holds a key |
| Scenario 3 | The same with MongoDB: a new agent resumes the saved session after a simulated restart (MongoDB container provided) |

---

### Lab 06 - Agent-to-Agent Communication (A2A)

**Learn how to build multi-agent systems where agents communicate with each other**

This lab is split into two parts:

#### Lab 06 - A2A Server
Build an agent that exposes its capabilities as a service.

#### Lab 06 - A2A Client
Build an agent that consumes remote agent services.

---

### Lab 07 - Agentic RAG with Vector Store

**Learn how to build agents that retrieve and reason over your own data**

| Scenario | Description |
|----------|-------------|
| Scenario 1 | Initialize vector store with FAQ data |
| Scenario 2 | Direct vector search without agent |
| Scenario 3 | Agentic RAG with search tool |

---

### Lab 08 - Data Format Comparison

**Learn how to optimize data formats for AI agent interactions**

| Scenario | Description |
|----------|-------------|
| Scenario 1 | Compare JSON vs other data formats for agent tools |

---

### Lab 09 - AI Agent with Human Approval

**Learn how to implement human-in-the-loop patterns for sensitive operations**

| Scenario | Description |
|----------|-------------|
| Scenario 1 | Function tools with human approval workflow |

---

### Lab 10 - Expose AI Agent as MCP Tool

**Learn how to expose your AI Agent as an MCP server for other clients**

| Scenario | Description |
|----------|-------------|
| Scenario 1 | Create an MCP server that exposes agent capabilities |

---

### Lab 11 - AI Agent with Custom HTTP Transport

**Learn how to customize HTTP communication for debugging and monitoring**

| Scenario | Description |
|----------|-------------|
| Scenario 1 | Intercept and log HTTP requests/responses with custom handler |

---

### Lab 12 - AI Agent with AI Context Provider

**Learn how to inject dynamic context into agent conversations**

| Scenario | Description |
|----------|-------------|
| Scenario 1 | User memory management with AIContextProvider and MongoDB |

---

### Part 2: MultiAgentSystem - Multi-Agent Solutions

This part focuses on building **multi-agent systems** where multiple agents collaborate to solve complex problems. You will learn how to orchestrate agents, use agents as tools, and build sophisticated AI workflows.

---

### Lab 01 - Use Agent as Tool

**Learn how to use an AI Agent as a tool for another agent**

| Scenario | Description |
|----------|-------------|
| Scenario 1 | Create an orchestrator agent that uses specialized agents as tools |

---

### Lab 02 - Orchestration Sequential

**Learn how to orchestrate multiple agents in a sequential pipeline**

| Scenario | Description |
|----------|-------------|
| Scenario 1 | Process data through a chain of specialized agents |

---

### Lab 03 - Orchestration Concurrent

**Learn how to orchestrate multiple agents running in parallel**

| Scenario | Description |
|----------|-------------|
| Scenario 1 | Process data with multiple agents concurrently (e-commerce after-sales) |

---

## Common Utilities

The `CommonUtilities` project provides shared helper classes used across all labs:

| Class | Description |
|-------|-------------|
| `ColoredConsole` | Colored console output methods for better UX |
| `ConsoleSpinner` | Loading animation for async operations |
| `MongoDbHealthCheck` | MongoDB connectivity verification |

---

## Getting Started

1. **Clone the repository** and check the [prerequisites](#prerequisites)
   ```bash
   git clone <repository-url>
   cd MicrosoftAgentFramework
   dotnet --version   # 10.0 or newer
   ```

2. **Configure Azure OpenAI once** — see [Configure Azure OpenAI](#configure-azure-openai)

3. **Choose a lab** (AzureOpenAI/Lab01 recommended for beginners), read its `README.md` and complete the TODOs in `Start/Program.cs`

4. **Run it** with either option (see [Running the Labs](#running-the-labs-cli-or-dashboard)):
   ```bash
   # Option A — CLI
   dotnet run --project LearningLabs/AzureOpenAI/Lab01-FirstBasicAIAgent/Start

   # Option B — Dashboard: then open http://127.0.0.1:5057, select the lab and click Run
   cd Dashboard/LabDashboard && dotnet run
   ```

---

## Environment Variables Configuration

Instead of editing each `appsettings.json` file in every lab, you can set environment variables once. The .NET configuration system automatically reads environment variables using the pattern `SectionName__PropertyName` (double underscore).

### Required Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `AzureOpenAI__Endpoint` | Your Azure OpenAI endpoint URL | `https://my-resource.openai.azure.com/` |
| `AzureOpenAI__ChatDeploymentName` | Your chat model deployment name | `gpt-4o` |
| `AzureOpenAI__APIKey` | Optional API key (if unset, `DefaultAzureCredential` is used) | `<your-api-key>` |
| `AzureOpenAI__EmbeddingDeploymentName` | Your embedding model deployment (Lab07+) | `text-embedding-ada-002` |
| `MongoDB__ConnectionString` | MongoDB connection string (Lab05, Lab12) | `mongodb://localhost:27017` |

### Linux / macOS

Add the following to your `~/.bashrc` or `~/.zshrc` file:

```bash
# Azure OpenAI Configuration
export AzureOpenAI__Endpoint="https://YOUR-RESOURCE.openai.azure.com/"
export AzureOpenAI__ChatDeploymentName="YOUR-DEPLOYMENT-NAME"
export AzureOpenAI__EmbeddingDeploymentName="YOUR-EMBEDDING-DEPLOYMENT-NAME"

# MongoDB Configuration (for Lab05, Lab12)
export MongoDB__ConnectionString="mongodb://localhost:27017"
```

Then reload your shell:

```bash
source ~/.bashrc
# or for zsh
source ~/.zshrc
```

### Windows (PowerShell - Current Session)

```powershell
# Azure OpenAI Configuration
$env:AzureOpenAI__Endpoint = "https://YOUR-RESOURCE.openai.azure.com/"
$env:AzureOpenAI__ChatDeploymentName = "YOUR-DEPLOYMENT-NAME"
$env:AzureOpenAI__EmbeddingDeploymentName = "YOUR-EMBEDDING-DEPLOYMENT-NAME"

# MongoDB Configuration (for Lab05, Lab12)
$env:MongoDB__ConnectionString = "mongodb://localhost:27017"
```

### Windows (Permanent - System Environment Variables)

1. Open **System Properties** → **Advanced** → **Environment Variables**
2. Under **User variables**, click **New** for each variable:
   - Variable name: `AzureOpenAI__Endpoint`
   - Variable value: `https://YOUR-RESOURCE.openai.azure.com/`
3. Repeat for other variables
4. Click **OK** and restart your terminal

**Or using PowerShell (Administrator):**

```powershell
[Environment]::SetEnvironmentVariable("AzureOpenAI__Endpoint", "https://YOUR-RESOURCE.openai.azure.com/", "User")
[Environment]::SetEnvironmentVariable("AzureOpenAI__ChatDeploymentName", "YOUR-DEPLOYMENT-NAME", "User")
[Environment]::SetEnvironmentVariable("AzureOpenAI__EmbeddingDeploymentName", "YOUR-EMBEDDING-DEPLOYMENT-NAME", "User")
[Environment]::SetEnvironmentVariable("MongoDB__ConnectionString", "mongodb://localhost:27017", "User")
```

### Verify Your Configuration

Run this command to verify your environment variables are set:

```bash
# Linux/macOS
echo $AzureOpenAI__Endpoint

# Windows PowerShell
echo $env:AzureOpenAI__Endpoint
```

> **Note:** Environment variables take precedence over values in `appsettings.json`. You can still use `appsettings.json` for lab-specific overrides if needed.
>
> Migrated labs also read **user secrets** (shared id `microsoft-agent-framework-learninglabs`), the recommended place for the API key during local development:
> `dotnet user-secrets set "AzureOpenAI:APIKey" "<your-api-key>"` (run from any migrated lab project folder),
> or, without a terminal, the **Azure OpenAI settings** of the [Lab Bench dashboard](Dashboard/README.md#azure-openai-settings), which write the same file.

---

## Learning Path

We recommend following the labs in order, starting with the **AzureOpenAI** labs (individual agents) and then moving to the **MultiAgentSystem** labs:

```
┌────────────────────────────────────────────────────────────────────────────────────────────────┐
│                           Part 1: AzureOpenAI (Individual Agents)                              │
├────────────────────────────────────────────────────────────────────────────────────────────────┤
│  Lab01 → Lab02 → Lab03 → Lab04 → Lab05 → Lab06 → Lab07 → Lab08 → Lab09 → Lab10 → Lab11 → Lab12│
│    │       │       │       │       │       │       │       │       │       │       │       │   │
│    ▼       ▼       ▼       ▼       ▼       ▼       ▼       ▼       ▼       ▼       ▼       ▼   │
│  Basic  Struct  Tools    MCP   Threads   A2A    RAG   Format  Human    MCP   Custom  Context  │
│  Agent  Output  Calling Client                        Optim  Approval Server  HTTP  Provider  │
└────────────────────────────────────────────────────────────────────────────────────────────────┘
                                            │
                                            ▼
┌────────────────────────────────────────────────────────────────────────────────────────────────┐
│                        Part 2: MultiAgentSystem (Multi-Agent Solutions)                        │
├────────────────────────────────────────────────────────────────────────────────────────────────┤
│  Lab01 → Lab02 → Lab03 → ...                                                                   │
│    │       │       │                                                                           │
│    ▼       ▼       ▼                                                                           │
│  Agent  Sequential Concurrent                                                                  │
│  as Tool Orchestr. Orchestr.                                                                   │
└────────────────────────────────────────────────────────────────────────────────────────────────┘
```

> **Note:** While we recommend following the labs in order, there are **no strict dependencies** between the different labs. If you already have some knowledge in a specific area, feel free to jump to any lab that interests you.

---

## Useful Links

- [Microsoft Agent Framework](https://github.com/microsoft/agent-framework)
- [Microsoft Agents Documentation](https://learn.microsoft.com/en-us/agent-framework/)
- [Azure OpenAI Documentation](https://learn.microsoft.com/azure/ai-services/openai/)
- [Model Context Protocol (MCP)](https://modelcontextprotocol.io/)

---

## Contributing

If you find issues or have suggestions for improving these labs, please open an issue or submit a pull request.

---

## Author

**Mohammed BEN SAID**

---

**Happy Learning! 🚀**
