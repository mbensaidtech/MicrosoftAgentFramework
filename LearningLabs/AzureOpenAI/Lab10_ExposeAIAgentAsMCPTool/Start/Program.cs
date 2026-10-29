using System.ClientModel;
using System.ClientModel.Primitives;
using System.Reflection;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using OpenAI;
using OpenAI.Chat;
using CommonUtilities;

using ExposeAIAgentAsMCPTool;
using ExposeAIAgentAsMCPTool.Tools;
using static ExposeAIAgentAsMCPTool.McpConsole;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// dotnet run                          -> the implemented scenarios (a scenario whose code is still the delivered one is left out)
// dotnet run -- --scenarios 2         -> scenario 2 only (and the scenarios it requires)
// dotnet run -- --scenarios 1,3       -> scenarios 1 and 3
// dotnet run -- --scenarios all       -> every scenario, implemented or not
// dotnet run -- --list-scenarios      -> the scenarios of this lab and their state
ScenarioSelection scenarioSelection = ScenarioSelection.FromArgs(args);
// ============================================

bool ShouldRunScenario(int scenario) => scenarioSelection.ShouldRun(scenario);

// The program can also run as an MCP server only (see "Use the server from MCP Inspector or VS Code" in the README):
//   --stdio : the stdio MCP server. Scenarios 2 and 3 start this program with it, and so do MCP Inspector or VS Code.
//   --http  : scenario 1, then its HTTP MCP server keeps running until Ctrl+C.
bool stdioServerMode = args.Contains("--stdio");
bool httpServerMode = args.Contains("--http");

if (stdioServerMode)
{
    // With the stdio transport, stdout carries the MCP messages and nothing else.
    // Everything the lab prints goes to stderr instead, which MCP hosts show as the log of the server.
    Console.SetOut(Console.Error);
}

#region Setup: Configuration and Client Initialization

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
Console.WriteLine($"Endpoint: {settings.Endpoint}");
Console.WriteLine($"Deployment: {settings.ChatDeploymentName}");

// TODO 1: Create the OpenAIClientOptions that point to the Azure OpenAI v1 endpoint
// Hint: AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint) turns "https://<resource>.openai.azure.com/" into ".../openai/v1/"
// OpenAIClientOptions clientOptions = ...

// TODO 2: Create the OpenAIClient
// - If settings.APIKey is set: authenticate with an ApiKeyCredential
// - Otherwise: authenticate with Microsoft Entra ID using a BearerTokenPolicy built from
//   DefaultAzureCredential and the scope "https://ai.azure.com/.default"
// Note: the AuthenticationPolicy constructor is flagged experimental (OPENAI001), keep the pragma around it.
#pragma warning disable OPENAI001
// OpenAIClient client = ...
#pragma warning restore OPENAI001

// TODO 3: Get a ChatClient (Chat Completions API) for the deployment
// ChatClient chatClient = ...

#endregion

#region The CompanyAssistant agent and its MCP tool (used by every scenario and by the stdio server)

// TODO 4: Create the CompanyAssistant agent with the company tools (same as Lab 03)
// Hint: var companyTools = new CompanyTools(); then chatClient.AsAIAgent(instructions: ..., name: "CompanyAssistant",
//       description: ..., tools: [AIFunctionFactory.Create(companyTools.GetEmployeeInfo, "get_employee_info"), ...])
//       with the three public methods of CompanyTools. The name and the description become those of the MCP tool.
// AIAgent companyAssistant = ...

// TODO 5: Turn the agent into a function (one string parameter "query", the agent's answer as result)
// Hint: companyAssistant.AsAIFunction() - without a session, every call runs the agent in a new session
// AIFunction companyAssistantFunction = ...

// TODO 6: Wrap the function in an MCP server tool
// Hint: McpServerTool.Create(companyAssistantFunction)
// McpServerTool companyAssistantTool = ...

#endregion

#region Stdio server mode: started with --stdio by an MCP host (scenarios 2 and 3, MCP Inspector, VS Code)

if (stdioServerMode)
{
    // TODO 7: Create an empty host and register the MCP server with the stdio transport and the CompanyAssistant tool
    // Hint: Host.CreateEmptyApplicationBuilder(settings: null) (no configuration, no logging: nothing may reach stdout),
    //       then builder.Services.AddMcpServer().WithStdioServerTransport().WithTools([companyAssistantTool])
    // HostApplicationBuilder stdioBuilder = ...

    // TODO 8: Run the server until the host closes stdin
    // Hint: ColoredConsole.WriteSecondaryLogLine("CompanyAssistant MCP server ready on stdio."), then await stdioBuilder.Build().RunAsync()

    return;
}

#endregion

// Path of the MCP endpoint on the HTTP server, and address the server listens on (McpServer:BaseUrl)
const string McpPath = "/mcp/companyassistant";
McpServerSettings serverSettings = ConfigurationHelper.GetMcpServerSettings();
string mcpUrl = $"{serverSettings.BaseUrl}{McpPath}";

#region Scenario 1: Expose the agent as a remote MCP tool (Streamable HTTP)

if (ShouldRunScenario(1) || httpServerMode)
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: Expose the agent as a remote MCP tool (Streamable HTTP) ===");

    // TODO 9: Create an ASP.NET Core application and register the MCP server with the HTTP transport and the CompanyAssistant tool
    // Hint: WebApplication.CreateBuilder(args), then builder.Services.AddMcpServer().WithHttpTransport().WithTools([companyAssistantTool])
    // WebApplicationBuilder builder = ...

    // TODO 10: Build the application, map the MCP endpoint on McpPath and start the server without blocking
    // Hint: builder.Build(), app.MapMcp(McpPath), app.Urls.Add(serverSettings.BaseUrl), await app.StartAsync(),
    //       then ColoredConsole.WritePrimaryLogLine($"MCP server listening on {mcpUrl}")
    // WebApplication app = ...

    const string query = "Get the information of the employee with the ID EMP003.";
    ColoredConsole.WritePrimaryLogLine($"Query: {query}");

    // TODO 11: Be an MCP client of the server: connect with the Streamable HTTP transport, list the tools, call the tool
    // Hint: await using (McpClient mcpClient = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions
    //       { Name = "CompanyAssistant over HTTP", Endpoint = new Uri(mcpUrl), TransportMode = HttpTransportMode.StreamableHttp }))) { ... }
    //       and in the block: WriteTools(await mcpClient.ListToolsAsync()), then
    //       await mcpClient.CallToolAsync("CompanyAssistant", new Dictionary<string, object?> { ["query"] = query })
    //       (.AsTask().WithSpinner("Waiting for the MCP tool")) and WriteToolResult(result)

    // TODO 12: With --http, keep the server running for other MCP clients; otherwise stop it
    // Hint: if (httpServerMode) { ColoredConsole.WriteSecondaryLogLine("..."); await app.WaitForShutdownAsync(); return; }
    //       then await app.StopAsync()
}

#endregion

#region Scenario 2: Expose the agent as a local MCP tool (stdio)

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Expose the agent as a local MCP tool (stdio) ===");

    const string query = "Which meeting rooms can host at least 10 people?";
    ColoredConsole.WritePrimaryLogLine($"Query: {query}");

    // TODO 13: Describe how to start the server: this same program, started with --stdio as a child process
    // Hint: new StdioClientTransport(new StdioClientTransportOptions { Name = "CompanyAssistant over stdio", Command = "dotnet",
    //       Arguments = [Assembly.GetEntryAssembly()!.Location, "--stdio"],
    //       StandardErrorLines = line => ColoredConsole.WriteSecondaryLogLine($"[server] {line}") })
    // StdioClientTransport transport = ...

    // TODO 14: Connect: the client starts the server process, then opens the MCP session
    // Hint: await using McpClient mcpClient = await McpClient.CreateAsync(transport);

    // TODO 15: List the tools of the server and call the tool (same as in TODO 11, another transport)
    // Hint: WriteTools(await mcpClient.ListToolsAsync()), then CallToolAsync("CompanyAssistant", ...) and WriteToolResult(result)
}

#endregion

#region Scenario 3: Another agent uses the CompanyAssistant through MCP

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: Another agent uses the CompanyAssistant through MCP ===");

    string prompt = $"Book the meeting room ROOM-B for the employee EMP002 on {DateTime.Today.AddDays(1):yyyy-MM-dd} " +
                    "from 10:00 to 11:00, subject: Campaign review. Tell me the name of the room and the name of the employee.";
    ColoredConsole.WritePrimaryLogLine($"User: {prompt}");

    // TODO 16: Start the stdio server and connect to it (same as TODO 13 and TODO 14)
    // await using McpClient mcpClient = ...

    // TODO 17: Get the MCP tools and create the OfficeAssistant agent, whose only tool is the CompanyAssistant
    // Hint: IList<McpClientTool> mcpTools = await mcpClient.ListToolsAsync(); then chatClient.AsAIAgent(instructions: ...,
    //       name: "OfficeAssistant", tools: [.. mcpTools.Cast<AITool>()]) - an McpClientTool is an AIFunction (Lab 04)
    // AIAgent officeAssistant = ...

    // TODO 18: Run the OfficeAssistant, then display the MCP tool calls, the answer and the token usage
    // Hint: await officeAssistant.RunAsync(prompt).WithSpinner("Running the OfficeAssistant"), then WriteToolCalls(response),
    //       "Agent answer:" with response.Text, and WriteTokenUsage(response)
}

#endregion
