# Lab 04 - AI Agent with MCP Client

## Objective

In this lab, you will learn how to integrate an AI Agent with the **Model Context Protocol (MCP)** using the Microsoft Agents Framework with Azure OpenAI.

MCP is a standardized protocol that allows AI agents to connect to external servers and use their tools. In this lab, you will connect to the Hugging Face MCP server and use its tools to search for models.

## What You Will Learn

- How to create an MCP client and connect to an MCP server
- How to configure HTTP transport with authentication headers
- How to retrieve available tools from an MCP server
- How to integrate MCP tools with an AI Agent
- How to use structured output with `RunAsync<T>` to get typed results
- How to configure chat client options (MaxOutputTokens, Temperature)

## Prerequisites

- .NET 10 SDK installed
- Azure OpenAI resource deployed
- Azure CLI logged in (`az login`) for DefaultAzureCredential
- A Hugging Face account and API token

## Project Structure

```
Lab04-AIAgentWithMCPClient/
├── README.md
├── Start/                      <-- Your working folder
│   ├── Program.cs              <-- Complete the TODOs here
│   ├── Models/
│   │   └── HuggingFaceModel.cs
│   ├── ConfigurationHelper.cs
│   ├── AzureOpenAISettings.cs
│   ├── MCPServerSettings.cs
│   ├── appsettings.json
│   └── AIAgentWithMCPClient.csproj
└── Solution/                   <-- Reference solution
    └── ...
```

## Instructions

### Step 1: Configure your settings

Open `Start/appsettings.json` and update the values:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
    "ChatDeploymentName": "YOUR-DEPLOYMENT-NAME",
    "APIKey": ""
  },
  "MCPServers": {
    "HuggingFace": {
      "Endpoint": "https://huggingface.co/mcp",
      "BearerToken": "YOUR-HUGGINGFACE-TOKEN"
    }
  }
}
```

#### Authentication Options

The labs support two authentication methods:

1. **API Key Authentication** (recommended for local development):
   - Set `APIKey` in `appsettings.json` with your Azure OpenAI API key

2. **DefaultAzureCredential** (for Azure-hosted apps):
   - Leave `APIKey` empty
   - Requires Azure CLI login (`az login`) or managed identity

The Solution folder includes the conditional logic to handle both methods.

### Step 2: Complete the Program.cs

Open `Start/Program.cs` and complete the TODOs:

---

#### Scenario 1: Connect to MCP Server and Use Available Tools

In this scenario, you connect to the Hugging Face MCP server, retrieve its tools, and use them with an AI Agent.

| Step | Description | Hints |
|------|-------------|-------|
| **TODO 1** | Create MCP Client with HTTP transport | • Use `McpClient.CreateAsync(new HttpClientTransport(...))` <br> • Configure `HttpClientTransportOptions` with: <br> &nbsp;&nbsp;- `TransportMode = HttpTransportMode.StreamableHttp` <br> &nbsp;&nbsp;- `Endpoint = new Uri(huggingFaceMcpSettings.Endpoint)` <br> &nbsp;&nbsp;- `AdditionalHeaders` with `Authorization: Bearer {token}` <br> • Use `await using` for proper disposal |
| **TODO 2** | List available tools from MCP server | • Method: `await huggingFaceMcpClient.ListToolsAsync()` <br> • Returns: `IList<McpClientTool>` |
| **TODO 3** | Create Agent with MCP tools | • Use `chatClient.CreateAIAgent(instructions: "...", tools: [...])` <br> • Convert MCP tools: `toolsInHuggingFaceMcp.Cast<AITool>().ToList()` <br> • Optional: Use `clientFactory` to configure `MaxOutputTokens` and `Temperature` |
| **TODO 4** | Run the agent with structured output | • Method: `await agent.RunAsync<HuggingFaceSearchResult>(prompt)` <br> • Prompt: `"Search 4 Hugging Face models for text embedding."` <br> • Returns: `AgentRunResponse<HuggingFaceSearchResult>` |
| **TODO 5** | Display structured results | • Access models: `response.Result.Models` <br> • Loop and display: `Name`, `Task`, `Library`, `Link` |
| **TODO 6** | Display token usage | • Use `ColoredConsole.WriteBurgundyLine("Token Usage: ")` <br> • Access: `response.Usage?.InputTokenCount`, `OutputTokenCount`, `TotalTokenCount` |

---

### Step 3: Run and Test

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

## Key Concepts

| Concept | Description |
|---------|-------------|
| `McpClient` | Client to connect to MCP servers |
| `HttpClientTransport` | HTTP transport layer for MCP communication |
| `HttpClientTransportOptions` | Configuration for HTTP transport (endpoint, headers, mode) |
| `HttpTransportMode.StreamableHttp` | Transport mode that supports HTTP streaming |
| `McpClientTool` | Tool exposed by an MCP server |
| `AITool` | Microsoft Agents abstraction for tools |
| `ChatClientAgent` | Microsoft Agents wrapper around ChatClient |
| `RunAsync<T>` | Run agent with automatic structured/typed output |
| `AgentRunResponse<T>` | Response with strongly-typed `Result` property |
| `ConfigureOptionsChatClient` | Wrapper to configure chat options like MaxOutputTokens |
| `clientFactory` | Factory function to customize the underlying chat client |

## Optional: Using the Console Spinner

The `CommonUtilities` library provides a `ConsoleSpinner` that shows a loading animation while the agent is processing. This is **optional** but improves the user experience.

**Usage with extension method:**
```csharp
// Simply chain .WithSpinner() to any async Task
AgentRunResponse<HuggingFaceSearchResult> response = await agent
    .RunAsync<HuggingFaceSearchResult>("Search 4 Hugging Face models for text embedding.")
    .WithSpinner("Running agent with MCP tools");
```

**Usage with using statement:**
```csharp
using var spinner = new ConsoleSpinner("Processing request");
var response = await agent.RunAsync<HuggingFaceSearchResult>("your prompt");
// Spinner automatically stops when disposed
```

The spinner displays an animated indicator with elapsed time: `⠋ Running agent with MCP tools... [00:03]`

## Namespaces Reference

| Namespace | Purpose |
|-----------|---------|
| `Azure.Identity` | Provides `DefaultAzureCredential` for Azure authentication |
| `Azure.AI.OpenAI` | Provides `AzureOpenAIClient` to connect to Azure OpenAI |
| `OpenAI` | Core SDK - extension methods like `CreateAIAgent` |
| `OpenAI.Chat` | Provides `ChatClient` for chat completions |
| `Microsoft.Agents.AI` | Provides `ChatClientAgent`, `AgentRunResponse`, `AgentRunResponse<T>` |
| `Microsoft.Extensions.AI` | Provides `AITool`, `ConfigureOptionsChatClient` |
| `ModelContextProtocol.Client` | Provides `McpClient`, `HttpClientTransport`, `McpClientTool` |
| `CommonUtilities` | Provides `ColoredConsole` for formatted output |

## Expected Output

```
Azure OpenAI Settings: 
Endpoint: https://your-resource.openai.azure.com/
Deployment: your-deployment-name
MCP Server: https://huggingface.co/mcp

────────────────────────────────────────────────────

=== Scenario 1: Connect to MCP Server and use available tools ===
Found 4 models:
  Name: sentence-transformers/all-MiniLM-L6-v2
  Task: sentence-similarity
  Library: sentence-transformers
  Link: https://hf.co/sentence-transformers/all-MiniLM-L6-v2

  Name: BAAI/bge-small-en-v1.5
  Task: sentence-similarity
  Library: sentence-transformers
  Link: https://hf.co/BAAI/bge-small-en-v1.5

  ...

Token Usage: 
  Input tokens: 1234
  Output tokens: 456
  Total tokens: 1690
```

**Color Legend:**
- Scenario title (`=== ... ===`) appears in **cyan**
- `Token Usage:` appears in **Burgundy (dark red)**
- Token values appear in **gray**

## Provided Files

The following files are provided and should not be modified:

- `AIAgentWithMCPClient.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs` - Helper to read configuration
- `AzureOpenAISettings.cs` - Settings class for Azure OpenAI
- `MCPServerSettings.cs` - Settings class for MCP servers
- `Models/HuggingFaceModel.cs` - Model classes for structured output

The following files should be modified:

- `appsettings.json` - Update with your Azure OpenAI settings and Hugging Face token
- `Program.cs` - Complete the TODOs

## Useful Links

- [Build an MCP client](https://modelcontextprotocol.io/docs/develop/build-client#c%23)
- [Using MCP tools with Agents](https://learn.microsoft.com/en-us/agent-framework/user-guide/model-context-protocol/using-mcp-tools?pivots=programming-language-csharp)

## Solution

If you get stuck, check the complete solution in the `Solution/` folder.
