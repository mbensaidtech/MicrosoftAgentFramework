# Lab 10 - Expose AI Agent as MCP Tool

## Objective

In this lab, you will learn how to expose an AI Agent as an **MCP (Model Context Protocol) Tool**, enabling other applications and agents to use your AI agent's capabilities through the MCP protocol.

You will implement two scenarios:
- **Scenario 1 (RemoteWithHttpTransport)**: Expose the AI agent as a remote MCP tool, accessible via both SSE and StreamableHTTP transport types
- **Scenario 2 (LocalWithStdioServerTransport)**: Expose the AI agent as a local MCP tool using Stdio Server Transport

## What You Will Learn

- How to convert an AI Agent to an `AIFunction`
- How to create an MCP server tool from an AIFunction
- How to configure an ASP.NET Core web application as an MCP server (HTTP Transport)
- How to configure an application with Stdio Server Transport for local MCP tools
- How to register MCP tools and map MCP endpoints

## Prerequisites

- .NET 10 SDK installed
- Azure OpenAI resource deployed
- Azure CLI logged in (`az login`) for DefaultAzureCredential
- Familiarity with Lab01-Lab04 concepts

## Project Structure

```
Lab10_ExposeAIAgentAsMCPTool/
├── README.md
├── Start/                          <-- Your working folder
│   ├── Program.cs                  <-- Complete the TODOs here
│   ├── Scenario.cs                 <-- Enum for scenario selection
│   ├── Tools/
│   │   └── CompanyTools.cs         <-- Provided: company tools (full code)
│   ├── ConfigurationHelper.cs
│   ├── AzureOpenAISettings.cs
│   ├── appsettings.json
│   └── ExposeAIAgentAsMCPTool.csproj
└── Solution/                       <-- Reference solution
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

Open `Start/Program.cs` and complete the TODOs.

The Setup region and the AI Agent configuration are already provided. You need to implement the two scenarios.

---

#### Scenario 1: Expose the AI agent as a remote MCP tool, accessible via both SSE and StreamableHTTP transport types

| Step | Description | Hints |
|------|-------------|-------|
| **Step 1** | Convert agent to an AIFunction | • Extension method on ChatClientAgent: `AsAIFunction()` <br> • Returns: `AIFunction` |
| **Step 2** | Create an MCP server tool from the AIFunction | • Factory method: `McpServerTool.Create(aiFunction)` <br> • Returns: `McpServerTool` |
| **Step 3** | Build and configure the web application with MCP server | • Create builder: `WebApplication.CreateBuilder(args)` <br> • Add MCP server: `builder.Services.AddMcpServer()` <br> • Chain `.WithHttpTransport()` to enable HTTP transport <br> • Chain `.WithTools([yourTool])` to register your MCP tool <br> • Build: `builder.Build()` |
| **Step 4** | Map the MCP endpoint with custom path and run the server | • Map endpoint with path: `app.MapMcp("/mcp/companyassistant")` <br> • Enable HTTPS: `app.UseHttpsRedirection()` <br> • Run: `app.Run()` |

---

#### Scenario 2: Expose the AI Agent as an MCP Tool using Stdio Server Transport

| Step | Description | Hints |
|------|-------------|-------|
| **Step 1** | Convert agent to an AIFunction | • Same as Scenario 1 Step 1 |
| **Step 2** | Create an MCP server tool from the AIFunction | • Same as Scenario 1 Step 2 |
| **Step 3** | Build and configure the application with MCP server using Stdio transport | • Create builder: `Host.CreateEmptyApplicationBuilder(settings: null)` <br> • Add MCP server: `builder.Services.AddMcpServer()` <br> • Chain `.WithStdioServerTransport()` to enable Stdio transport <br> • Chain `.WithTools([yourTool])` to register your MCP tool |
| **Step 4** | Build and run the application | • Build: `builder.Build()` <br> • Run: `await app.RunAsync()` |

---

### Step 3: Run and Test

**Switch between scenarios** by changing the `scenarioToRun` value at the top of `Program.cs`:

```csharp
// For HTTP Transport (remote)
Scenario scenarioToRun = Scenario.RemoteWithHttpTransport;

// For Stdio Transport (local)
Scenario scenarioToRun = Scenario.LocalWithStdioServerTransport;
```

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

---

### Step 4: Test with MCP Inspector

MCP Inspector is a tool to test and debug MCP servers. Launch it with:

```bash
npx @modelcontextprotocol/inspector
```

This will open the MCP Inspector in your browser.

---

#### Testing Scenario 2: Stdio Server Transport

1. In MCP Inspector, select **STDIO** from the **Transport Type** dropdown
2. In the **Command** field, enter: `dotnet`
3. In the **Arguments** field, enter the path to the project you want to test:
   - **To test the Solution:**
     ```
     run --project /ABSOLUTE/PATH/TO/Lab10_ExposeAIAgentAsMCPTool/Solution --no-build
     ```
   - **To test your Start implementation:**
     ```
     run --project /ABSOLUTE/PATH/TO/Lab10_ExposeAIAgentAsMCPTool/Start --no-build
     ```
4. Click **Connect**

---

#### Testing Scenario 1: HTTP Transport (SSE)

First, run the application with `Scenario.RemoteWithHttpTransport` selected, then:

1. In MCP Inspector, select **SSE** from the **Transport Type** dropdown
2. In the **URL** field, enter:
   ```
   http://localhost:5000/mcp/companyassistant/sse
   ```
3. Click **Connect**

---

#### Testing Scenario 1: HTTP Transport (Streamable HTTP)

First, run the application with `Scenario.RemoteWithHttpTransport` selected, then:

1. In MCP Inspector, select **Streamable HTTP** from the **Transport Type** dropdown
2. In the **URL** field, enter:
   ```
   http://localhost:5000/mcp/companyassistant
   ```
3. Click **Connect**

---

#### Using MCP Inspector

Once connected:

1. Click **List Tools** to see the available tools (you should see `CompanyAssistant`)
2. Select the **CompanyAssistant** tool
3. In the **query** field, enter a prompt like:
   ```
   Get the information of the employee with the ID EMP001
   ```
4. Click **Run Tool** to execute and see the result

---

#### Configure in Cursor IDE

You can also configure the MCP server in Cursor's settings:

```json
{
  "mcpServers": {
    "companyassistant": {
      "command": "dotnet",
      "args": ["run", "--project", "/ABSOLUTE/PATH/TO/Solution", "--no-build"]
    }
  }
}
```

---

## Key Concepts

| Concept | Description |
|---------|-------------|
| `AIFunction` | A function abstraction that can be called by AI agents |
| `AsAIFunction()` | Extension method to convert a ChatClientAgent to an AIFunction |
| `McpServerTool` | Wrapper that exposes an AIFunction as an MCP-compatible tool |
| `McpServerTool.Create()` | Factory method to create an MCP tool from an AIFunction |
| `AddMcpServer()` | Service extension to add MCP server capabilities |
| `WithHttpTransport()` | Configures HTTP transport for remote MCP server |
| `WithStdioServerTransport()` | Configures Stdio transport for local MCP server |
| `WithTools()` | Registers tools with the MCP server |
| `MapMcp(path)` | Maps the MCP endpoint to a custom path (HTTP only) |
| `Host.CreateEmptyApplicationBuilder()` | Creates a minimal host builder for Stdio transport |

## Namespaces Reference

| Namespace | Purpose |
|-----------|---------|
| `Azure.Identity` | Provides `DefaultAzureCredential` for Azure authentication |
| `Azure.AI.OpenAI` | Provides `AzureOpenAIClient` to connect to Azure OpenAI |
| `OpenAI` | Core SDK - extension methods like `CreateAIAgent` |
| `OpenAI.Chat` | Provides `ChatClient` for chat completions |
| `Microsoft.Agents.AI` | Provides `ChatClientAgent`, `ChatClientAgentOptions` |
| `Microsoft.Extensions.AI` | Provides `AIFunctionFactory`, `AITool`, `AIFunction` |
| `ModelContextProtocol.Server` | Provides `McpServerTool`, MCP server configuration |
| `System.Reflection` | Provides `MethodInfo`, `BindingFlags` for tool discovery |
| `CommonUtilities` | Provides `ColoredConsole` for formatted output |

## Expected Output

**Scenario 1 (HTTP Transport):**
```
=== Running Scenario: RemoteWithHttpTransport ===
Azure OpenAI Settings: 
Azure OpenAI Endpoint: https://your-resource.openai.azure.com/
Azure OpenAI Chat Deployment Name: your-deployment-name

────────────────────────────────────────────────────

=== Scenario: Expose the AI agent as a remote MCP tool, accessible via both SSE and StreamableHTTP transport types. ===
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:xxxx
```

**Scenario 2 (Stdio Transport):**
```
=== Running Scenario: LocalWithStdioServerTransport ===
Azure OpenAI Settings: 
Azure OpenAI Endpoint: https://your-resource.openai.azure.com/
Azure OpenAI Chat Deployment Name: your-deployment-name

────────────────────────────────────────────────────

=== Scenario: LocalWithStdioServerTransport - Expose the AI Agent as an MCP Tool using Stdio Server Transport ===
```
*(The server will wait for MCP client connections via stdio)*

## Provided Files

The following files are provided and should not be modified:

- `ExposeAIAgentAsMCPTool.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs` - Helper to read configuration
- `AzureOpenAISettings.cs` - Settings class for Azure OpenAI
- `Scenario.cs` - Enum for scenario selection
- `Tools/CompanyTools.cs` - Company tools (full implementation provided)

The following files should be modified:

- `appsettings.json` - Update with your Azure OpenAI settings
- `Program.cs` - Complete the TODOs in both scenarios

## Useful Links

- [Microsoft - Expose an agent as an MCP tool](https://learn.microsoft.com/en-us/agent-framework/tutorials/agents/agent-as-mcp-tool?pivots=programming-language-csharp)
- [MCP Official doc - Build an MCP server using STDIO](https://modelcontextprotocol.io/docs/develop/build-server)

## Solution

If you get stuck, check the complete solution in the `Solution/` folder.
