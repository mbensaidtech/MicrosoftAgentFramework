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

// Step 4: Load the settings of the Hugging Face MCP server (endpoint + optional access token)
var huggingFaceMcpSettings = ConfigurationHelper.GetMCPServerSettings("HuggingFace");
bool hasToken = !string.IsNullOrWhiteSpace(huggingFaceMcpSettings.BearerToken);
Console.WriteLine($"MCP Server: {huggingFaceMcpSettings.Endpoint} ({(hasToken ? "access token" : "anonymous")})");

// TODO 4: Connect to the MCP server with the Streamable HTTP transport
// Hint: await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions { ... })).WithSpinner("Connecting to the MCP server")
//       with Name = "Hugging Face", Endpoint = new Uri(huggingFaceMcpSettings.Endpoint), TransportMode = HttpTransportMode.StreamableHttp
//       and, only when hasToken is true, AdditionalHeaders with "Authorization" = $"Bearer {huggingFaceMcpSettings.BearerToken}"
// Use "await using" so that the client (and the MCP session) is closed at the end of the program.
// await using McpClient huggingFaceMcpClient = ...

#endregion

#region Scenario 1: Discover the tools of the MCP server

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: Discover the tools of the MCP server ===");

    // TODO 5: Display the name and the version of the server the client is connected to
    // Hint: huggingFaceMcpClient.ServerInfo.Name and huggingFaceMcpClient.ServerInfo.Version,
    //       with ColoredConsole.WritePrimaryLogLine($"Connected to: ...")

    // TODO 6: List the tools exposed by the server (a plain MCP request, no model call)
    // IList<McpClientTool> mcpTools = ...

    // TODO 7: Display the number of tools and their names
    // Hint: ColoredConsole.WriteSecondaryLogLine($"MCP tools available ({mcpTools.Count}): {string.Join(", ", mcpTools.Select(tool => tool.Name))}")
}

#endregion

#region Scenario 2: Agent with MCP tools and structured output

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Agent with MCP tools and structured output ===");

    // TODO 8: Get the tools of the MCP server (same call as TODO 6)
    // IList<McpClientTool> mcpTools = ...

    // TODO 9: Create the agent with ChatClientAgentOptions, and the name and the instructions of Agents.HuggingFaceAssistant
    // (provided in LabPrompts.cs)
    // Hint: chatClient.AsAIAgent(new ChatClientAgentOptions { Name = Agents.HuggingFaceAssistant.Name, ChatOptions = new ChatOptions { ... } })
    //       with Instructions = Agents.HuggingFaceAssistant.Instructions, Tools = [.. mcpTools.Cast<AITool>()], MaxOutputTokens = 1000, Temperature = 0.2f
    // AIAgent agent = ...

    // TODO 10: Run the agent with structured output and the question Questions.TextEmbeddingModels
    // Hint: await agent.RunAsync<HuggingFaceSearchResult>(Questions.TextEmbeddingModels).WithSpinner("Running agent with MCP tools")
    // AgentResponse<HuggingFaceSearchResult> response = ...

    // TODO 11: Display the structured result
    // Hint: ColoredConsole.WritePrimaryLogLine($"Found {response.Result.Models.Count} models:"), then for each model of response.Result.Models:
    //       its Name, Task, Library and Link with ColoredConsole.WriteSecondaryLogLine($"  Name: {model.Name}")..., then ColoredConsole.WriteEmptyLine()

    // TODO 12: Display the MCP tools the agent called
    // Hint: ColoredConsole.WritePrimaryLogLine("MCP tools called by the agent:"), then for each FunctionCallContent of
    //       response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>():
    //       ColoredConsole.WriteSecondaryLogLine($"- {call.Name}({arguments})") (see the README to build "arguments")

    // TODO 13: Display token usage
    // Hint: WriteTokenUsage(response)
}

#endregion

#region Scenario 3: Give the agent only the MCP tools it needs

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: Give the agent only the MCP tools it needs ===");

    // TODO 14: Get the tools of the MCP server, keep only "hub_repo_search", and display the tools kept
    // Hint: List<AITool> selectedTools = [.. mcpTools.Where(tool => tool.Name == "hub_repo_search")];
    //       ColoredConsole.WriteSecondaryLogLine($"Tools given to the agent: {string.Join(", ", selectedTools.Select(tool => tool.Name))}")

    // TODO 15: Create the same agent as in TODO 9 (Agents.HuggingFaceAssistant), with Tools = selectedTools
    // AIAgent agent = ...

    // TODO 16: Run the same request as in TODO 10 (Questions.TextEmbeddingModels)
    // Hint: .WithSpinner("Running agent with the selected MCP tool")
    // AgentResponse<HuggingFaceSearchResult> response = ...

    // TODO 17: Display the names of the models found
    // Hint: ColoredConsole.WritePrimaryLogLine($"Found {response.Result.Models.Count} models: {string.Join(", ", response.Result.Models.Select(model => model.Name))}")

    // TODO 18: Display token usage and compare the input tokens with scenario 2
}

#endregion

#region Scenario 4: Responses API - the same MCP client, the tools still run in your application
// Bonus scenario (optional): the lab is completed without it; with every bonus scenario, it is completed with its bonus (Done+).

if (ShouldRunScenario(4))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 4: Responses API - the same MCP client, the tools still run in your application ===");

    // TODO 19: Get the tools with the MCP client and keep only "hub_repo_search" (the same code as TODO 14), then display them
    // Hint: ColoredConsole.WriteSecondaryLogLine($"Tools given to the agent: {string.Join(", ", selectedTools.Select(tool => tool.Name))} (MCP client of this application)")

    // TODO 20: Create the agent of scenario 3 on the Responses API
    // Hint: client.GetResponsesClient().AsAIAgent(new ChatClientAgentOptions { ... }, model: settings.ChatDeploymentName)
    //       with the same Name and ChatOptions as in TODO 15 (Tools = selectedTools)
    // With Responses, the deployment is the model of each request. GetResponsesClient() is flagged experimental (OPENAI001): keep the pragma.
#pragma warning disable OPENAI001
    // AIAgent agent = ...
#pragma warning restore OPENAI001

    // TODO 21: Run the same request with structured output (Questions.TextEmbeddingModels)
    // Hint: .WithSpinner("Running agent on the Responses API with the local MCP tool")
    // AgentResponse<HuggingFaceSearchResult> response = ...

    // TODO 22: Display the names of the models found (the same code as TODO 17), then the tool calls and who ran them
    // Hint: WriteToolCalls(response) (provided in AgentConsole.cs)

    // TODO 23: Display the id of the response and the token usage
    // Hint: ColoredConsole.WriteSecondaryLogLine($"Response id: {response.ResponseId}"); then WriteTokenUsage(response);
}

#endregion

#region Scenario 5: Hosted MCP - the service connects to the MCP server and runs its tools
// Bonus scenario (optional): the lab is completed without it; with every bonus scenario, it is completed with its bonus (Done+).

if (ShouldRunScenario(5))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 5: Hosted MCP - the service connects to the MCP server and runs its tools ===");

    // TODO 24: Describe the MCP server as a hosted tool (no McpClient: the service connects to the server), and display it
    // Hint: HostedMcpServerTool hostedMcpServer = new(serverName: "huggingface", serverAddress: huggingFaceMcpSettings.Endpoint) { ... };
    //       with AllowedTools = ["hub_repo_search"], ApprovalMode = HostedMcpServerToolApprovalMode.NeverRequire,
    //       Headers = hasToken ? new Dictionary<string, string> { ["Authorization"] = $"Bearer {huggingFaceMcpSettings.BearerToken}" } : null
    //       ColoredConsole.WriteSecondaryLogLine($"Hosted MCP server given to the agent: {hostedMcpServer.ServerName} ({hostedMcpServer.ServerAddress}), allowed tools: {string.Join(", ", hostedMcpServer.AllowedTools)}")

    // TODO 25: Create the same agent as in TODO 20, with Tools = [hostedMcpServer]
#pragma warning disable OPENAI001
    // AIAgent agent = ...
#pragma warning restore OPENAI001

    // TODO 26: Run the same request with structured output
    // Hint: .WithSpinner("Running agent with the hosted MCP server")
    // AgentResponse<HuggingFaceSearchResult> response = ...

    // TODO 27: Display the names of the models found, the tool calls (WriteToolCalls) and the token usage: compare with scenario 4
}

#endregion
