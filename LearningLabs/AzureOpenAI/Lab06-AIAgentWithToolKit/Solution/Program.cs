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
    IList<AITool> tools = [];

    // Step 1: Create the tools factory.
    AIToolsFactory aiToolsFactory = new AIToolsFactory();

    // Step 2: Get the tools from the factory.
    tools = aiToolsFactory.GetTools(typeof(CompanyTools));

    // Step 3: Create the agent.
    ChatClientAgent basicFunctionToolsAgent = chatClient.CreateAIAgent(
        instructions: "you are a helpful assistant that can help with company tasks",
        tools: tools);

    // Step 4: Run the agent.
    AgentRunResponse basicFunctionToolsResponse = await basicFunctionToolsAgent.RunAsync(
        "Get the information of the employee with the ID EMP001");

    // Step 5: Display the response
    ColoredConsole.WriteInfoLine("=== Scenario 1: Function tools calling - basic ===");
    ColoredConsole.WriteSecondaryLogLine(basicFunctionToolsResponse.ToString());

    // Step 6: Display token usage
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WritePrimaryLogLine("Token Usage: ");
    ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {basicFunctionToolsResponse.Usage?.InputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {basicFunctionToolsResponse.Usage?.OutputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {basicFunctionToolsResponse.Usage?.TotalTokenCount}");

    ColoredConsole.WriteDividerLine();
}

#endregion
