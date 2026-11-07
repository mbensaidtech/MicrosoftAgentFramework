using System.ClientModel;
using System.ClientModel.Primitives;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;
using CommonUtilities;

using AIAgentWithMCPClient;
using AIAgentWithMCPClient.Models;
using static AIAgentWithMCPClient.AgentConsole;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// dotnet run                          -> the implemented scenarios (a scenario whose code is still the delivered one is left out)
// dotnet run -- --scenarios 2         -> scenario 2 only (and the scenarios it requires)
// dotnet run -- --scenarios 1,3       -> scenarios 1 and 3
// dotnet run -- --list-scenarios      -> the scenarios of this lab and their state
// Scenarios 4 and 5 use the Responses API: the deployment must support it.
ScenarioSelection scenarioSelection = ScenarioSelection.FromArgs(args);
// ============================================

bool ShouldRunScenario(int scenario) => scenarioSelection.ShouldRun(scenario);

#region Setup: Configuration, Azure OpenAI client and MCP client

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

// Step 4: Load the settings of the Hugging Face MCP server (endpoint + optional access token)
var huggingFaceMcpSettings = ConfigurationHelper.GetMCPServerSettings("HuggingFace");
bool hasToken = !string.IsNullOrWhiteSpace(huggingFaceMcpSettings.BearerToken);
Console.WriteLine($"MCP Server: {huggingFaceMcpSettings.Endpoint} ({(hasToken ? "access token" : "anonymous")})");

// Step 5: Connect to the MCP server with the Streamable HTTP transport.
// The access token, when there is one, is sent in the Authorization header of every request to the server.
// The client is disposed (and the MCP session closed) at the end of the program.
await using McpClient huggingFaceMcpClient = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions
{
    Name = "Hugging Face",
    Endpoint = new Uri(huggingFaceMcpSettings.Endpoint),
    TransportMode = HttpTransportMode.StreamableHttp,
    AdditionalHeaders = hasToken
        ? new Dictionary<string, string> { ["Authorization"] = $"Bearer {huggingFaceMcpSettings.BearerToken}" }
        : null
})).WithSpinner("Connecting to the MCP server");

#endregion

#region Scenario 1: Discover the tools of the MCP server

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: Discover the tools of the MCP server ===");

    // Step 1: Display the server the client is connected to (sent by the server during the MCP initialization)
    ColoredConsole.WritePrimaryLogLine($"Connected to: {huggingFaceMcpClient.ServerInfo.Name} {huggingFaceMcpClient.ServerInfo.Version}");

    // Step 2: List the tools exposed by the server (no model call: this is a plain MCP request)
    IList<McpClientTool> mcpTools = await huggingFaceMcpClient.ListToolsAsync();

    // Step 3: Display the name of each tool: this is the name the model will see
    ColoredConsole.WriteSecondaryLogLine($"MCP tools available ({mcpTools.Count}): {string.Join(", ", mcpTools.Select(tool => tool.Name))}");
}

#endregion

#region Scenario 2: Agent with MCP tools and structured output

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Agent with MCP tools and structured output ===");

    // Step 1: Get the tools of the MCP server. Each McpClientTool is an AIFunction: the agent can call it like a .NET tool,
    // and the call is sent to the MCP server.
    IList<McpClientTool> mcpTools = await huggingFaceMcpClient.ListToolsAsync();

    // Step 2: Create the agent with ChatClientAgentOptions: instructions, tools and chat options live in ChatOptions
    AIAgent agent = chatClient.AsAIAgent(new ChatClientAgentOptions
    {
        Name = Agents.HuggingFaceAssistant.Name,
        ChatOptions = new ChatOptions
        {
            Instructions = Agents.HuggingFaceAssistant.Instructions,
            Tools = [.. mcpTools.Cast<AITool>()],
            MaxOutputTokens = 1000,
            Temperature = 0.2f
        }
    });

    // Step 3: Run the agent with structured output (with spinner to show loading):
    // it calls the MCP tools, then answers with a HuggingFaceSearchResult
    AgentResponse<HuggingFaceSearchResult> response = await agent.RunAsync<HuggingFaceSearchResult>(Questions.TextEmbeddingModels).WithSpinner("Running agent with MCP tools");

    // Step 4: Display the structured result
    ColoredConsole.WritePrimaryLogLine($"Found {response.Result.Models.Count} models:");
    foreach (HuggingFaceModel model in response.Result.Models)
    {
        ColoredConsole.WriteSecondaryLogLine($"  Name: {model.Name}");
        ColoredConsole.WriteSecondaryLogLine($"  Task: {model.Task}");
        ColoredConsole.WriteSecondaryLogLine($"  Library: {model.Library}");
        ColoredConsole.WriteSecondaryLogLine($"  Link: {model.Link}");
        ColoredConsole.WriteEmptyLine();
    }

    // Step 5: Display the MCP tools the agent called: the response messages keep every function call of the run
    ColoredConsole.WritePrimaryLogLine("MCP tools called by the agent:");
    foreach (FunctionCallContent call in response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>())
    {
        string arguments = string.Join(", ", call.Arguments?.Select(argument => $"{argument.Key}: {argument.Value}") ?? []);
        ColoredConsole.WriteSecondaryLogLine($"- {call.Name}({arguments})");
    }

    // Step 6: Display token usage (total of every model call of the run; the tool definitions are sent with each call)
    WriteTokenUsage(response);
}

#endregion

#region Scenario 3: Give the agent only the MCP tools it needs

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: Give the agent only the MCP tools it needs ===");

    // Step 1: Keep only the search tool. The definition (name, description, JSON schema) of every tool given to the agent
    // is sent to the model with each request: fewer tools means fewer input tokens, and fewer things the agent can do.
    IList<McpClientTool> mcpTools = await huggingFaceMcpClient.ListToolsAsync();
    List<AITool> selectedTools = [.. mcpTools.Where(tool => tool.Name == "hub_repo_search")];
    ColoredConsole.WriteSecondaryLogLine($"Tools given to the agent: {string.Join(", ", selectedTools.Select(tool => tool.Name))}");

    // Step 2: Create the same agent as in scenario 2, with the selected tools only
    AIAgent agent = chatClient.AsAIAgent(new ChatClientAgentOptions
    {
        Name = Agents.HuggingFaceAssistant.Name,
        ChatOptions = new ChatOptions
        {
            Instructions = Agents.HuggingFaceAssistant.Instructions,
            Tools = selectedTools,
            MaxOutputTokens = 1000,
            Temperature = 0.2f
        }
    });

    // Step 3: Run the same request as in scenario 2 (with spinner to show loading)
    AgentResponse<HuggingFaceSearchResult> response = await agent.RunAsync<HuggingFaceSearchResult>(Questions.TextEmbeddingModels).WithSpinner("Running agent with the selected MCP tool");

    // Step 4: Display the names of the models found
    ColoredConsole.WritePrimaryLogLine($"Found {response.Result.Models.Count} models: {string.Join(", ", response.Result.Models.Select(model => model.Name))}");

    // Step 5: Display token usage: compare the input tokens with scenario 2
    WriteTokenUsage(response);
}

#endregion

#region Scenario 4: Responses API - the same MCP client, the tools still run in your application
// Bonus scenario (optional): the lab is completed without it; with every bonus scenario, it is completed with its bonus (Done+).

if (ShouldRunScenario(4))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 4: Responses API - the same MCP client, the tools still run in your application ===");

    // Step 1: Discover the tools with the MCP client, exactly as with Chat Completions, and keep the search tool.
    // Discovering MCP tools and giving them to an agent does not depend on the API of the model: an McpClientTool is an AIFunction.
    IList<McpClientTool> mcpTools = await huggingFaceMcpClient.ListToolsAsync();
    List<AITool> selectedTools = [.. mcpTools.Where(tool => tool.Name == "hub_repo_search")];
    ColoredConsole.WriteSecondaryLogLine($"Tools given to the agent: {string.Join(", ", selectedTools.Select(tool => tool.Name))} (MCP client of this application)");

    // Step 2: Create the agent of scenario 3 on the Responses API: same options, the deployment is the model of each request.
    // GetResponsesClient() is still flagged [Experimental] (OPENAI001) by the OpenAI SDK.
#pragma warning disable OPENAI001
    AIAgent agent = client.GetResponsesClient().AsAIAgent(new ChatClientAgentOptions
    {
        Name = Agents.HuggingFaceAssistant.Name,
        ChatOptions = new ChatOptions
        {
            Instructions = Agents.HuggingFaceAssistant.Instructions,
            Tools = selectedTools,
            MaxOutputTokens = 1000,
            Temperature = 0.2f
        }
    }, model: settings.ChatDeploymentName);
#pragma warning restore OPENAI001

    // Step 3: Run the same request with structured output. The model asks for hub_repo_search (a function_call item),
    // the agent runs it from this process through the MCP client, and sends the result back (a function_call_output item).
    AgentResponse<HuggingFaceSearchResult> response = await agent.RunAsync<HuggingFaceSearchResult>(Questions.TextEmbeddingModels).WithSpinner("Running agent on the Responses API with the local MCP tool");

    // Step 4: Display the names of the models found, then the tool calls and who ran them
    ColoredConsole.WritePrimaryLogLine($"Found {response.Result.Models.Count} models: {string.Join(", ", response.Result.Models.Select(model => model.Name))}");
    WriteToolCalls(response);

    // Step 5: Display the id of the response (stored by the service by default) and the token usage: compare with scenario 3
    ColoredConsole.WriteSecondaryLogLine($"Response id: {response.ResponseId}");
    WriteTokenUsage(response);
}

#endregion

#region Scenario 5: Hosted MCP - the service connects to the MCP server and runs its tools
// Bonus scenario (optional): the lab is completed without it; with every bonus scenario, it is completed with its bonus (Done+).

if (ShouldRunScenario(5))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 5: Hosted MCP - the service connects to the MCP server and runs its tools ===");

    // Step 1: Describe the MCP server instead of connecting to it: no McpClient here, this application never talks to Hugging Face.
    // The service (Azure OpenAI) lists the tools of the server, calls them, and gives the results to the model, in one request.
    // - AllowedTools: the tools the model may use (the same choice as scenario 3)
    // - ApprovalMode: NeverRequire lets the service call them without asking you first (the default asks for an approval)
    // - Headers: sent by the service to the MCP server; the token, when there is one, is therefore given to the service too
    HostedMcpServerTool hostedMcpServer = new(serverName: "huggingface", serverAddress: huggingFaceMcpSettings.Endpoint)
    {
        AllowedTools = ["hub_repo_search"],
        ApprovalMode = HostedMcpServerToolApprovalMode.NeverRequire,
        Headers = hasToken ? new Dictionary<string, string> { ["Authorization"] = $"Bearer {huggingFaceMcpSettings.BearerToken}" } : null
    };
    ColoredConsole.WriteSecondaryLogLine($"Hosted MCP server given to the agent: {hostedMcpServer.ServerName} ({hostedMcpServer.ServerAddress}), allowed tools: {string.Join(", ", hostedMcpServer.AllowedTools)}");

    // Step 2: Create the same agent on the Responses API, with the hosted MCP server as its only tool.
    // A hosted tool only exists on the Responses API. Beware: an agent on Chat Completions does not refuse it, it silently
    // leaves it out of the request, and the model answers from its own knowledge.
#pragma warning disable OPENAI001
    AIAgent agent = client.GetResponsesClient().AsAIAgent(new ChatClientAgentOptions
    {
        Name = Agents.HuggingFaceAssistant.Name,
        ChatOptions = new ChatOptions
        {
            Instructions = Agents.HuggingFaceAssistant.Instructions,
            Tools = [hostedMcpServer],
            MaxOutputTokens = 1000,
            Temperature = 0.2f
        }
    }, model: settings.ChatDeploymentName);
#pragma warning restore OPENAI001

    // Step 3: Run the same request: one call to the service, which runs the whole tool loop on its side
    AgentResponse<HuggingFaceSearchResult> response = await agent.RunAsync<HuggingFaceSearchResult>(Questions.TextEmbeddingModels).WithSpinner("Running agent with the hosted MCP server");

    // Step 4: Display the names of the models found, then the tool calls: this time, the service listed and ran the tools
    ColoredConsole.WritePrimaryLogLine($"Found {response.Result.Models.Count} models: {string.Join(", ", response.Result.Models.Select(model => model.Name))}");
    WriteToolCalls(response);

    // Step 5: Display token usage: compare with scenario 4 (same tool, run on the other side)
    WriteTokenUsage(response);
}

#endregion
