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
├── Start/             <-- Your working folder
│   ├── Program.cs     <-- Complete the TODOs here
│   ├── Models/
│   │   └── HuggingFaceModel.cs
│   └── ...
└── Solution/          <-- Reference solution (check if needed)
    └── ...
```

## Instructions

### Step 1: Configure your settings

Open `Start/appsettings.json` and update the values:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
    "ChatDeploymentName": "YOUR-DEPLOYMENT-NAME"
  },
  "MCPServers": {
    "HuggingFace": {
      "Endpoint": "https://huggingface.co/mcp",
      "BearerToken": "YOUR-HUGGINGFACE-TOKEN"
    }
  }
}
```

### Step 2: Complete the Program.cs

Open `Start/Program.cs` and complete the 6 TODOs in Scenario 1:

| TODO | Description |
|------|-------------|
| TODO 1 | Create an MCP Client with HTTP transport and authentication |
| TODO 2 | List available tools from the MCP server |
| TODO 3 | Create an AI Agent with MCP tools |
| TODO 4 | Run the agent with structured output |
| TODO 5 | Display the structured results |
| TODO 6 | Display token usage |

### Step 3: Run and Test

```bash
cd Start
dotnet run
```

## Key Concepts

| Concept | Description |
|---------|-------------|
| `McpClient` | Client to connect to MCP servers |
| `HttpClientTransport` | HTTP transport for MCP communication |
| `HttpTransportMode.StreamableHttp` | Transport mode for HTTP streaming |
| `McpClientTool` | Tool exposed by an MCP server |
| `AITool` | Microsoft Agents abstraction for tools |
| `RunAsync<T>` | Run agent with structured/typed output |
| `ConfigureOptionsChatClient` | Configure chat client options like MaxOutputTokens |

## Expected Output

```
Endpoint: https://your-resource.openai.azure.com/
Deployment: your-deployment-name
MCP Server: https://huggingface.co/mcp
----------------------------------------
=== Scenario 1: Connect to MCP Server and use available tools ===
Found 4 models:
  Name: sentence-transformers/all-MiniLM-L6-v2
  Task: sentence-similarity
  Library: sentence-transformers
  Link: https://hf.co/sentence-transformers/all-MiniLM-L6-v2
----------------------------------------
  ...
Token Usage: 
  Input tokens: 1234
  Output tokens: 456
  Total tokens: 1690
```

## Provided Files

The following files are provided and should not be modified:

- `AIAgentWithMCPClient.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs` - Helper to read configuration
- `AzureOpenAISettings.cs` - Settings class for Azure OpenAI
- `MCPServerSettings.cs` - Settings class for MCP servers
- `Models/HuggingFaceModel.cs` - Model classes for structured output

The following file should be modified with your values:

- `appsettings.json` - Update with your Azure OpenAI endpoint, deployment name, and Hugging Face token

## Solution

If you get stuck, you can check the complete solution in the `Solution/` folder.
