using System.ClientModel;
using System.ClientModel.Primitives;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OpenAI;
using OpenAI.Chat;
using CommonUtilities;

using AIAgentWithMCPClient;
using AIAgentWithMCPClient.Models;
using static AIAgentWithMCPClient.AgentConsole;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// Set to: [1], [2], [3] or [1, 2, 3] to run specific scenarios
HashSet<int> scenariosToRun = [1, 2, 3];
// ============================================

bool ShouldRunScenario(int scenario) => scenariosToRun.Count == 0 || scenariosToRun.Contains(scenario);

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
        Name = "HuggingFaceAssistant",
        ChatOptions = new ChatOptions
        {
            Instructions = "You are a helpful assistant that searches the Hugging Face Hub with the tools of the Hugging Face MCP server. " +
                           "Only return models found by the tools.",
            Tools = [.. mcpTools.Cast<AITool>()],
            MaxOutputTokens = 1000,
            Temperature = 0.2f
        }
    });

    // Step 3: Run the agent with structured output (with spinner to show loading):
    // it calls the MCP tools, then answers with a HuggingFaceSearchResult
    AgentResponse<HuggingFaceSearchResult> response = await agent.RunAsync<HuggingFaceSearchResult>(
        "Search 4 Hugging Face models for text embedding.").WithSpinner("Running agent with MCP tools");

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
        Name = "HuggingFaceAssistant",
        ChatOptions = new ChatOptions
        {
            Instructions = "You are a helpful assistant that searches the Hugging Face Hub with the tools of the Hugging Face MCP server. " +
                           "Only return models found by the tools.",
            Tools = selectedTools,
            MaxOutputTokens = 1000,
            Temperature = 0.2f
        }
    });

    // Step 3: Run the same request as in scenario 2 (with spinner to show loading)
    AgentResponse<HuggingFaceSearchResult> response = await agent.RunAsync<HuggingFaceSearchResult>(
        "Search 4 Hugging Face models for text embedding.").WithSpinner("Running agent with the selected MCP tool");

    // Step 4: Display the names of the models found
    ColoredConsole.WritePrimaryLogLine($"Found {response.Result.Models.Count} models: {string.Join(", ", response.Result.Models.Select(model => model.Name))}");

    // Step 5: Display token usage: compare the input tokens with scenario 2
    WriteTokenUsage(response);
}

#endregion
