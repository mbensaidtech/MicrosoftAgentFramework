using Azure.Identity;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using OpenAI.Chat;
using OpenAI;
using System.ClientModel.Primitives;
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
// TODO: Create an HttpClient instance using CustomHttpClientHandler

// Step 3: Create AzureOpenAIClient with managed identity authentication and the custom HTTP client
// TODO: Create the AzureOpenAIClient with custom transport options

// Step 4: Get a ChatClient for the specific deployment
// TODO: Get the ChatClient from the AzureOpenAIClient

#endregion

#region Scenario 1: Basic Agent - Simple prompt with default settings

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Running Scenario 1: Basic Agent ===");
    
    // Once you complete the setup steps above, uncomment the following lines:
    // ChatClientAgent basicAgent = chatClient.CreateAIAgent();
    // AgentRunResponse basicResponse = await basicAgent.RunAsync("Hello, what is the capital of France?");
    // ColoredConsole.WritePrimaryLogLine(basicResponse.ToString());
    
    ColoredConsole.WriteWarningLine("TODO: Complete steps 2-4 in the Setup region, then uncomment the scenario code above.");
}

#endregion
