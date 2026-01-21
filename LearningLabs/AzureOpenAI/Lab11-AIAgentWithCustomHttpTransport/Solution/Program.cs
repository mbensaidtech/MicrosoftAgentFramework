using Azure.Identity;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using OpenAI.Chat;
using OpenAI;
using System.ClientModel.Primitives;
using System.ClientModel;
using CommonUtilities;
using AIAgentWithCustomHttpTransport;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// Set to: [1], [2], [3] or [1, 2, 3] to run specific scenarios
HashSet<int> scenariosToRun = [1];
// ============================================

bool ShouldRunScenario(int scenario) => scenariosToRun.Count == 0 || scenariosToRun.Contains(scenario);

#region Setup: Configuration and Client Initialization

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
Console.WriteLine($"Endpoint: {settings.Endpoint}");
Console.WriteLine($"Deployment: {settings.ChatDeploymentName}");

// Step 2: Create a custom HTTP client with the custom HTTP handler
var httpClient = new HttpClient(new CustomHttpClientHandler());

// Step 3: Create AzureOpenAIClient (API key or DefaultAzureCredential) with custom HTTP transport
var clientOptions = new AzureOpenAIClientOptions 
{ 
    Transport = new HttpClientPipelineTransport(httpClient), 
    NetworkTimeout = TimeSpan.FromSeconds(30) 
};
AzureOpenAIClient client = !string.IsNullOrEmpty(settings.APIKey)
    ? new AzureOpenAIClient(new Uri(settings.Endpoint), new ApiKeyCredential(settings.APIKey), clientOptions)
    : new AzureOpenAIClient(new Uri(settings.Endpoint), new DefaultAzureCredential(), clientOptions);

// Step 4: Get a ChatClient for the specific deployment
ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);

#endregion

#region Scenario 1: Basic Agent - Simple prompt with default settings

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Running Scenario 1: Basic Agent ===");
    ChatClientAgent basicAgent = chatClient.CreateAIAgent();
    AgentRunResponse basicResponse = await basicAgent.RunAsync("Hello, what is the capital of France?");
 
    ColoredConsole.WritePrimaryLogLine(basicResponse.ToString());
}

#endregion
