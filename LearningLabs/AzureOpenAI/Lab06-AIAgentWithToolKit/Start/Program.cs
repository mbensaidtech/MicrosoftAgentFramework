using Azure.Identity;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using OpenAI;
using OpenAI.Chat;
using CommonUtilities;
using AgentFrameworkToolkit.Tools;
using Microsoft.Extensions.AI;
using AIAgentWithToolKit;
using AIAgentWithToolKit.Tools;

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

#region Scenario 1: Use function tools with the ToolKit.

if (ShouldRunScenario(1))
{
    // TODO 1: Create an empty list of AITool
    // IList<AITool> tools = ...

    // TODO 2: Create the AIToolsFactory instance
    // AIToolsFactory aiToolsFactory = ...

    // TODO 3: Get the tools from the factory using the CompanyTools type
    // tools = aiToolsFactory.GetTools(...)

    // TODO 4: Create the agent with instructions and tools
    // ChatClientAgent basicFunctionToolsAgent = chatClient.CreateAIAgent(...)

    // TODO 5: Run the agent with a prompt
    // AgentRunResponse basicFunctionToolsResponse = await basicFunctionToolsAgent.RunAsync(...)

    // TODO 6: Display the response
    // ColoredConsole.WriteInfoLine("=== Scenario 1: Function tools calling - basic ===");
    // ColoredConsole.WriteSecondaryLogLine(...)

    // TODO 7: Display token usage
    // ColoredConsole.WriteDividerLine();
    // ColoredConsole.WritePrimaryLogLine("Token Usage: ");
    // ColoredConsole.WriteSecondaryLogLine($"  Input tokens: ...");
    // ColoredConsole.WriteSecondaryLogLine($"  Output tokens: ...");
    // ColoredConsole.WriteSecondaryLogLine($"  Total tokens: ...");
}

#endregion
