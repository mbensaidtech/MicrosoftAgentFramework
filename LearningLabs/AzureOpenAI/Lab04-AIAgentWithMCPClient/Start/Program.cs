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

    // TODO 9: Create the agent with ChatClientAgentOptions
    // Hint: chatClient.AsAIAgent(new ChatClientAgentOptions { Name = "HuggingFaceAssistant", ChatOptions = new ChatOptions { ... } })
    //       with Instructions (see the README), Tools = [.. mcpTools.Cast<AITool>()], MaxOutputTokens = 1000, Temperature = 0.2f
    // AIAgent agent = ...

    // TODO 10: Run the agent with structured output
    // Hint: await agent.RunAsync<HuggingFaceSearchResult>("Search 4 Hugging Face models for text embedding.").WithSpinner("Running agent with MCP tools")
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

    // TODO 15: Create the same agent as in TODO 9, with Tools = selectedTools
    // AIAgent agent = ...

    // TODO 16: Run the same request as in TODO 10
    // Hint: .WithSpinner("Running agent with the selected MCP tool")
    // AgentResponse<HuggingFaceSearchResult> response = ...

    // TODO 17: Display the names of the models found
    // Hint: ColoredConsole.WritePrimaryLogLine($"Found {response.Result.Models.Count} models: {string.Join(", ", response.Result.Models.Select(model => model.Name))}")

    // TODO 18: Display token usage and compare the input tokens with scenario 2
}

#endregion
