using Azure.Identity;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using OpenAI;
using OpenAI.Chat;
using Microsoft.Extensions.AI;
using CommonUtilities;
using UseAgentAsTool;

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
Console.WriteLine($"Deployment: {settings.DeploymentName}");

// Step 2: Create AzureOpenAIClient with managed identity authentication
AzureOpenAIClient client = new AzureOpenAIClient(new Uri(settings.Endpoint), new DefaultAzureCredential());

// Step 3: Get a ChatClient for the specific deployment
ChatClient chatClient = client.GetChatClient(settings.DeploymentName);

#endregion

ColoredConsole.WriteDividerLine();

#region Scenario 1: Use Agent as Tool

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteInfoLine("=== Scenario 1: Use Agent as Tool ===");

    // TODO 1: Create a specialized translator agent
    // Use chatClient.CreateAIAgent with instructions for translation
    // ChatClientAgent translatorAgent = ...

    // TODO 2: Create a function that wraps the translator agent
    // This function should take text and targetLanguage as parameters
    // and return the translated text by calling the translator agent
    // async Task<string> TranslateText(string text, string targetLanguage) { ... }

    // TODO 3: Create an AITool from the wrapper function
    // Use AIFunctionFactory.Create to create the tool
    // var translatorTool = AIFunctionFactory.Create(...)

    // TODO 4: Create an orchestrator agent with the translator tool
    // Use chatClient.CreateAIAgent with instructions and tools parameter
    // ChatClientAgent orchestratorAgent = ...

    // TODO 5: Run the orchestrator agent
    // Ask it to translate "Hello, how are you today?" to French and Spanish
    // AgentRunResponse orchestratorResponse = ...

    // TODO 6: Display the response and token usage
}

#endregion
