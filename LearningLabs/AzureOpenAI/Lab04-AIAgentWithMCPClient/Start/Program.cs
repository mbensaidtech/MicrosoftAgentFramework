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
ColoredConsole.WriteEmptyLine();
ColoredConsole.WritePrimaryLogLine("Azure OpenAI Settings: ");
ColoredConsole.WriteSecondaryLogLine($"Endpoint: {settings.Endpoint}");
ColoredConsole.WriteSecondaryLogLine($"Deployment: {settings.ChatDeploymentName}");

// Step 2: Load MCP Server settings from configuration
var huggingFaceMcpSettings = ConfigurationHelper.GetMCPServerSettings("HuggingFace");
ColoredConsole.WriteSecondaryLogLine($"MCP Server: {huggingFaceMcpSettings.Endpoint}");

// Step 3: Create AzureOpenAIClient with managed identity authentication
AzureOpenAIClient client = new AzureOpenAIClient(new Uri(settings.Endpoint), new DefaultAzureCredential());

// Step 4: Get a ChatClient for the specific deployment
ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);

#endregion

#region Scenario 1: Connect to MCP Server and use available tools.

if (ShouldRunScenario(1))
{   
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: Connect to MCP Server and use available tools ===");

    // TODO 1: Create MCP Client
    // Use McpClient.CreateAsync with HttpClientTransport
    // Configure HttpClientTransportOptions with TransportMode, Endpoint, and AdditionalHeaders (for Authorization)
    // await using McpClient huggingFaceMcpClient = ...

    // TODO 2: List available tools from the MCP server
    // Use the ListToolsAsync method
    // IList<McpClientTool> toolsInHuggingFaceMcp = ...

    // TODO 3: Create Agent with MCP tools
    // Use chatClient.CreateAIAgent with instructions and tools parameters
    // Hint: MCP tools need to be converted to AITool type
    // Optional: Use clientFactory to configure MaxOutputTokens and Temperature
    // ChatClientAgent huggingFaceMcpAgent = ...

    // TODO 4: Run the agent with structured output
    // Use RunAsync<HuggingFaceSearchResult> to get typed results
    // AgentRunResponse<HuggingFaceSearchResult> response = ...

    // TODO 5: Display structured results
    // Hint: Access the models via response.Result.Models and loop through them

    // TODO 6: Display token usage
    // Hint: Use ColoredConsole.WriteBurgundyLine("Token Usage: ") and response.Usage properties
}

#endregion
