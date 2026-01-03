using Azure.Identity;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using OpenAI.Chat;
using OpenAI;
using System.Reflection;

using CommonUtilities;
using ExposeAIAgentAsMCPTool;
using ExposeAIAgentAsMCPTool.Tools;

// ============================================
// SCENARIO SELECTION - Choose which scenario to run
// ============================================
// Available scenarios: RemoteWithHttpTransport, LocalWithStdioServerTransport
// (only ONE scenario can run at a time since each starts a server)
Scenario scenarioToRun = Scenario.LocalWithStdioServerTransport;
// ============================================

ColoredConsole.WriteInfoLine($"=== Running Scenario: {scenarioToRun} ===");

#region Setup: Configuration and Client Initialization

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
ColoredConsole.WritePrimaryLogLine("Azure OpenAI Settings: ");
ColoredConsole.WriteSecondaryLogLine($"Azure OpenAI Endpoint: {settings.Endpoint}");
ColoredConsole.WriteSecondaryLogLine($"Azure OpenAI Chat Deployment Name: {settings.ChatDeploymentName}");

// Step 2: Create AzureOpenAIClient with managed identity authentication
AzureOpenAIClient client = new AzureOpenAIClient(new Uri(settings.Endpoint), new DefaultAzureCredential());

// Step 3: Get a ChatClient for the specific deployment
ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);

#endregion

#region Create and configure the AI Agent

// Step 1: Create an instance of CompanyTools
var companyTools = new CompanyTools();

// Step 2: Get all public instance methods from CompanyTools using reflection
MethodInfo[] methods = typeof(CompanyTools).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

// Step 3: Create a list of AITool from the methods using AIFunctionFactory.Create
List<AITool> tools = methods.Select(method => AIFunctionFactory.Create(method, companyTools)).Cast<AITool>().ToList();

// Step 4: Create agent with tools
var agentOptions = new ChatClientAgentOptions
{ 
    Name = "CompanyAssistant",
    Description = "A helpful assistant that can help with company tasks.",
    ChatOptions = new() { Instructions = "You are a helpful assistant that can help with company tasks.", Tools = tools },
};

ChatClientAgent agent = chatClient.CreateAIAgent(agentOptions);

#endregion

#region Scenario 1: Expose the AI agent as a remote MCP tool, accessible via both SSE and StreamableHTTP transport types.

if (scenarioToRun == Scenario.RemoteWithHttpTransport)
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario: Expose the AI agent as a remote MCP tool, accessible via both SSE and StreamableHTTP transport types. ===");

    // Step 1: Convert agent to an AIFunction
    AIFunction agentAsAIFunction = agent.AsAIFunction();

    // Step 2: Create an MCP server tool from the AIFunction
    McpServerTool agentAsMcpTool = McpServerTool.Create(agentAsAIFunction);

    // Step 3: Build and configure the web application with MCP server
    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddMcpServer()
        .WithHttpTransport()
        .WithTools([agentAsMcpTool]);

    var app = builder.Build();

    // Step 4: Map the MCP endpoint with custom path and run the server
    app.MapMcp("/mcp/companyassistant");
    app.UseHttpsRedirection();
    app.Run();
}

#endregion

#region Scenario 2: Expose the AI Agent as an MCP Tool using Stdio Server Transport

if (scenarioToRun == Scenario.LocalWithStdioServerTransport)
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario: LocalWithStdioServerTransport - Expose the AI Agent as an MCP Tool using Stdio Server Transport ===");

    // Step 1: Convert agent to an AIFunction
    AIFunction agentAsAIFunction = agent.AsAIFunction();

    // Step 2: Create an MCP server tool from the AIFunction
    McpServerTool agentAsMcpTool = McpServerTool.Create(agentAsAIFunction);

    // Step 3: Build and configure the application with MCP server using Stdio transport
    var builder = Host.CreateEmptyApplicationBuilder(settings: null);

    builder.Services.AddMcpServer()
        .WithStdioServerTransport()
        .WithTools([agentAsMcpTool]);

    var app = builder.Build();

    // Step 4: Build and run the application
    await app.RunAsync();
}

#endregion
