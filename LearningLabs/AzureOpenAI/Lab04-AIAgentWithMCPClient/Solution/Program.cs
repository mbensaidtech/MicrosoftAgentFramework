using Azure.Identity;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using OpenAI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using CommonUtilities;
using AIAgentWithMCPClient;
using AIAgentWithMCPClient.Models;
using ModelContextProtocol.Client;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// Set to: [1], [2], or [1, 2] to run specific scenarios
HashSet<int> scenariosToRun = [1];
// ============================================

bool ShouldRunScenario(int scenario) => scenariosToRun.Count == 0 || scenariosToRun.Contains(scenario);

#region Setup: Configuration and Client Initialization

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
Console.WriteLine($"Endpoint: {settings.Endpoint}");
Console.WriteLine($"Deployment: {settings.ChatDeploymentName}");

// Step 2: Load MCP Server settings from configuration
var huggingFaceMcpSettings = ConfigurationHelper.GetMCPServerSettings("HuggingFace");
Console.WriteLine($"MCP Server: {huggingFaceMcpSettings.Endpoint}");

// Step 2: Create AzureOpenAIClient with managed identity authentication
AzureOpenAIClient client = new AzureOpenAIClient(new Uri(settings.Endpoint), new DefaultAzureCredential());

// Step 3: Get a ChatClient for the specific deployment
ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);

#endregion

ColoredConsole.WriteDividerLine();

#region Scenario 1: Connect to MCP Server and use available tools.

if (ShouldRunScenario(1))
{   
    ColoredConsole.WriteInfoLine("=== Scenario 1: Connect to MCP Server and use available tools ===");

    // Step 1: Create MCP Client
    await using McpClient huggingFaceMcpClient = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions
    {
        TransportMode = HttpTransportMode.StreamableHttp,
        Endpoint = new Uri(huggingFaceMcpSettings.Endpoint),
        AdditionalHeaders = new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {huggingFaceMcpSettings.BearerToken}" }
        }
    }));

    IList<McpClientTool> toolsInHuggingFaceMcp = await huggingFaceMcpClient.ListToolsAsync();

    // Step 2: Create Agent with MCP tools and configure the chat client options.
   ChatClientAgent huggingFaceMcpAgent = chatClient.CreateAIAgent(
    instructions: "You are a helpful assistant that can use the tools provided by the Hugging Face MCP Server.",
    tools: toolsInHuggingFaceMcp.Cast<AITool>().ToList(),
    clientFactory: chatClient=>{
        return new ConfigureOptionsChatClient(chatClient, options =>
        {
            options.MaxOutputTokens=600;
            options.Temperature=1;
        });
    }
   );

    // Step 3: Run the agent with a prompt that requires MCP tools.
    AgentRunResponse<HuggingFaceSearchResult> huggingFaceMcpAgentResponse = await huggingFaceMcpAgent.RunAsync<HuggingFaceSearchResult>(
        "Search 4 Hugging Face models for text embedding.");

    // Step 4: Display structured results
    ColoredConsole.WritePrimaryLogLine($"Found {huggingFaceMcpAgentResponse.Result.Models.Count} models:");
    foreach (var model in huggingFaceMcpAgentResponse.Result.Models)
    {
        ColoredConsole.WriteSecondaryLogLine($"  Name: {model.Name}");
        ColoredConsole.WriteSecondaryLogLine($"  Task: {model.Task}");
        ColoredConsole.WriteSecondaryLogLine($"  Library: {model.Library}");
        ColoredConsole.WriteSecondaryLogLine($"  Link: {model.Link}");
        ColoredConsole.WriteDividerLine();
    }

    // Step 5: Display token usage
    ColoredConsole.WritePrimaryLogLine("Token Usage: ");
    ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {huggingFaceMcpAgentResponse.Usage?.InputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {huggingFaceMcpAgentResponse.Usage?.OutputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {huggingFaceMcpAgentResponse.Usage?.TotalTokenCount}");

}

#endregion