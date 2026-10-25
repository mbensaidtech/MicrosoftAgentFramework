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
// Set to: [1], [2], [3] or [1, 2, 3] to run specific scenarios
HashSet<int> scenariosToRun = [1, 2, 3];
// ============================================

bool ShouldRunScenario(int scenario) => scenariosToRun.Count == 0 || scenariosToRun.Contains(scenario);

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

// Step 2: Create an OpenAIClient that targets the Azure OpenAI v1 endpoint (https://<resource>.openai.azure.com/openai/v1/)
// - API key when one is configured
// - otherwise Microsoft Entra ID: DefaultAzureCredential + BearerTokenPolicy (scope https://ai.azure.com/.default)
// WARNING: DefaultAzureCredential is convenient for development. In production, prefer a specific credential
// (e.g. ManagedIdentityCredential) to avoid latency and unintended credential probing.
OpenAIClientOptions clientOptions = new() { Endpoint = AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint) };

// The OpenAIClient(AuthenticationPolicy, ...) constructor is still flagged [Experimental] (OPENAI001) by the OpenAI SDK,
// although it is the pattern documented by Microsoft for Entra ID with the Azure OpenAI v1 API.
#pragma warning disable OPENAI001
OpenAIClient client = !string.IsNullOrWhiteSpace(settings.APIKey)
    ? new OpenAIClient(new ApiKeyCredential(settings.APIKey), clientOptions)
    : new OpenAIClient(new BearerTokenPolicy(new DefaultAzureCredential(), "https://ai.azure.com/.default"), clientOptions);
#pragma warning restore OPENAI001

// Step 3: Get a ChatClient (Chat Completions API) for the deployment
ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);

#endregion

#region The CompanyAssistant agent and its MCP tool (used by every scenario and by the stdio server)

// Step 4: Create the CompanyAssistant agent with the company tools (same as Lab 03).
// Its name and its description become the name and the description of the MCP tool.
var companyTools = new CompanyTools();
AIAgent companyAssistant = chatClient.AsAIAgent(
    instructions: "You are a helpful assistant that can help with company tasks. Answer with the facts returned by the tools.",
    name: "CompanyAssistant",
    description: "Answers questions about the employees and the meeting rooms of the company, and books meeting rooms.",
    tools:
    [
        AIFunctionFactory.Create(companyTools.GetEmployeeInfo, "get_employee_info"),
        AIFunctionFactory.Create(companyTools.GetMeetingRooms, "get_meeting_rooms"),
        AIFunctionFactory.Create(companyTools.BookMeetingRoom, "book_meeting_room")
    ]);

// Step 5: Turn the agent into a function: one string parameter, "query", and the text of the agent's answer as result.
// No session is given: every call runs the agent in a new session, so the callers never share a conversation.
AIFunction companyAssistantFunction = companyAssistant.AsAIFunction();

// Step 6: Wrap the function in an MCP server tool
McpServerTool companyAssistantTool = McpServerTool.Create(companyAssistantFunction);

#endregion

#region Stdio server mode: started with --stdio by an MCP host (scenarios 2 and 3, MCP Inspector, VS Code)

if (stdioServerMode)
{
    // Step 7: Create an empty host (no configuration source, no logging: nothing may reach stdout)
    // and register the MCP server with the stdio transport and the CompanyAssistant tool
    HostApplicationBuilder stdioBuilder = Host.CreateEmptyApplicationBuilder(settings: null);
    stdioBuilder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithTools([companyAssistantTool]);

    // Step 8: Run the server: it reads the MCP requests on stdin and answers on stdout, until the host closes stdin
    ColoredConsole.WriteSecondaryLogLine("CompanyAssistant MCP server ready on stdio.");
    await stdioBuilder.Build().RunAsync();
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

    // Step 1: Create an ASP.NET Core application and register the MCP server
    // with the HTTP transport (Streamable HTTP) and the CompanyAssistant tool
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
    builder.Services
        .AddMcpServer()
        .WithHttpTransport()
        .WithTools([companyAssistantTool]);

    // Step 2: Build the application, map the MCP endpoint on its path, and start the server without blocking
    // (the program calls it in the next step)
    WebApplication app = builder.Build();
    app.MapMcp(McpPath);
    app.Urls.Add(serverSettings.BaseUrl);
    await app.StartAsync();
    ColoredConsole.WritePrimaryLogLine($"MCP server listening on {mcpUrl}");

    // Step 3: Be an MCP client of the server, as MCP Inspector or VS Code would be: connect with the Streamable HTTP
    // transport, list the tools (the agent is one tool, with a "query" parameter) and call the tool. The server runs the
    // CompanyAssistant agent, which calls its own tools, and returns the agent's answer.
    const string query = "Get the information of the employee with the ID EMP003.";
    ColoredConsole.WritePrimaryLogLine($"Query: {query}");

    await using (McpClient mcpClient = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions
    {
        Name = "CompanyAssistant over HTTP",
        Endpoint = new Uri(mcpUrl),
        TransportMode = HttpTransportMode.StreamableHttp
    })))
    {
        IList<McpClientTool> mcpTools = await mcpClient.ListToolsAsync();
        WriteTools(mcpTools);

        CallToolResult result = await mcpClient.CallToolAsync("CompanyAssistant", new Dictionary<string, object?> { ["query"] = query })
            .AsTask().WithSpinner("Waiting for the MCP tool");
        WriteToolResult(result);
    }

    // Step 4: With --http, keep the server running for other MCP clients; otherwise stop it
    if (httpServerMode)
    {
        ColoredConsole.WriteSecondaryLogLine("Server still running for other MCP clients (--http). Press Ctrl+C to stop it.");
        await app.WaitForShutdownAsync();
        return;
    }

    await app.StopAsync();
}

#endregion

#region Scenario 2: Expose the agent as a local MCP tool (stdio)

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Expose the agent as a local MCP tool (stdio) ===");

    const string query = "Which meeting rooms can host at least 10 people?";
    ColoredConsole.WritePrimaryLogLine($"Query: {query}");

    // Step 1: Describe how to start the server. An MCP host starts a local MCP server as a child process and talks to it
    // on stdin/stdout: here the lab is the host, and the server is this same program started with --stdio.
    // What the server writes on stderr (its log) is displayed with a [server] prefix.
    StdioClientTransport transport = new(new StdioClientTransportOptions
    {
        Name = "CompanyAssistant over stdio",
        Command = "dotnet",
        Arguments = [Assembly.GetEntryAssembly()!.Location, "--stdio"],
        StandardErrorLines = line => ColoredConsole.WriteSecondaryLogLine($"[server] {line}")
    });

    // Step 2: Connect: the client starts the server process, then opens the MCP session.
    // At the end of the block, the client is disposed: it closes stdin and the server process exits.
    await using McpClient mcpClient = await McpClient.CreateAsync(transport);

    // Step 3: List the tools of the server (same tool as in scenario 1, another transport) and call the tool
    IList<McpClientTool> mcpTools = await mcpClient.ListToolsAsync();
    WriteTools(mcpTools);

    CallToolResult result = await mcpClient.CallToolAsync("CompanyAssistant", new Dictionary<string, object?> { ["query"] = query })
        .AsTask().WithSpinner("Waiting for the MCP tool");
    WriteToolResult(result);
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

    // Step 1: Start the stdio server and connect to it (same as scenario 2)
    await using McpClient mcpClient = await McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
    {
        Name = "CompanyAssistant over stdio",
        Command = "dotnet",
        Arguments = [Assembly.GetEntryAssembly()!.Location, "--stdio"],
        StandardErrorLines = line => ColoredConsole.WriteSecondaryLogLine($"[server] {line}")
    }));

    // Step 2: Get the MCP tools (each McpClientTool is an AIFunction, Lab 04) and create another agent, the OfficeAssistant,
    // whose only tool is the CompanyAssistant
    IList<McpClientTool> mcpTools = await mcpClient.ListToolsAsync();
    AIAgent officeAssistant = chatClient.AsAIAgent(
        instructions: "You are the personal assistant of the employees. Delegate every question or task about the company " +
                      "to the CompanyAssistant tool, then report its answer briefly.",
        name: "OfficeAssistant",
        tools: [.. mcpTools.Cast<AITool>()]);

    // Step 3: Run the OfficeAssistant: its model calls the CompanyAssistant tool, and the MCP server runs the CompanyAssistant agent.
    // Then display the MCP tool calls with their results, the answer and the token usage (the OfficeAssistant's model calls
    // only: the CompanyAssistant runs in the server process)
    AgentResponse response = await officeAssistant.RunAsync(prompt).WithSpinner("Running the OfficeAssistant");
    WriteToolCalls(response);
    ColoredConsole.WritePrimaryLogLine("Agent answer:");
    ColoredConsole.WriteSecondaryLogLine(response.Text);
    WriteTokenUsage(response);
}

#endregion
