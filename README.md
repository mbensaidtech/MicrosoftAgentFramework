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

- .NET 8 SDK or newer
- Azure OpenAI resource deployed
- Azure CLI logged in (`az login`) for DefaultAzureCredential
- Visual Studio Code or Visual Studio 2022+

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

---

### Lab 02 - AI Agent with Structured Output

**Learn how to get structured JSON responses from an AI Agent**

| Scenario | Description |
|----------|-------------|
| Scenario 1 | Manual structured output with JSON parsing |
| Scenario 2 | Automatic structured output with `RunAsync<T>` (Recommended) |
| Scenario 3 | Structured output using `AIAgent` with `ChatOptions` |

---

### Lab 03 - AI Agent with Function Tools

**Learn how to give agents access to external functions and data**

| Scenario | Description |
|----------|-------------|
| Scenario 1 | Basic function tools calling |
| Scenario 2 | Function tools using reflection |
| Scenario 3 | Static tools with Dependency Injection |

---

### Lab 04 - AI Agent with MCP Client

**Learn how to integrate with Model Context Protocol (MCP) servers**

| Scenario | Description |
|----------|-------------|
| Scenario 1 | Connect to MCP server and use available tools |

---

### Lab 05 - AI Agent with Threads

**Learn how to maintain conversation context across multiple interactions**

| Scenario | Description |
|----------|-------------|
| Scenario 1 | Multi-turn conversations with thread context |
| Scenario 2 | Persistent chat history with MongoDB |

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

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd MicrosoftAgentFramework
   ```

2. **Choose a lab to start with** (AzureOpenAI/Lab01 recommended for beginners)
   ```bash
   # For individual agents (Part 1)
   cd LearningLabs/AzureOpenAI/Lab01-FirstBasicAIAgent/Start
   
   # For multi-agent systems (Part 2)
   cd LearningLabs/MultiAgentSystem/Lab01-UseAgentAsTool/Start
   ```

3. **Configure your settings** - See the [Environment Variables Configuration](#environment-variables-configuration) section below

4. **Complete the TODOs** and run your solution
   ```bash
   dotnet run
   ```

---

## Environment Variables Configuration

Instead of editing each `appsettings.json` file in every lab, you can set environment variables once. The .NET configuration system automatically reads environment variables using the pattern `SectionName__PropertyName` (double underscore).

### Required Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `AzureOpenAI__Endpoint` | Your Azure OpenAI endpoint URL | `https://my-resource.openai.azure.com/` |
| `AzureOpenAI__ChatDeploymentName` | Your chat model deployment name | `gpt-4o` |
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

- [Microsoft Agents Framework](https://github.com/microsoft/agents)
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
