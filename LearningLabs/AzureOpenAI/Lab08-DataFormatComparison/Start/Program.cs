using Azure.Identity;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using ToonNetSerializer;

using CommonUtilities;
using DataFormatComparison;
using DataFormatComparison.Tools;
using DataFormatComparison.Models;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// Set to: [1], [2], or [1, 2] to run specific scenarios
HashSet<int> scenariosToRun = [1, 2];
// ============================================

bool ShouldRunScenario(int scenario) => scenariosToRun.Count == 0 || scenariosToRun.Contains(scenario);

#region Setup: Configuration and Client Initialization

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
ColoredConsole.WriteSecondaryLogLine($"Endpoint: {settings.Endpoint}");
ColoredConsole.WriteSecondaryLogLine($"Deployment: {settings.ChatDeploymentName}");

// TODO 1: Create AzureOpenAIClient with managed identity authentication
// Hint: Use DefaultAzureCredential for authentication
// AzureOpenAIClient client = ...

// TODO 2: Get a ChatClient for the deployment
// ChatClient chatClient = ...

#endregion

#region Scenario 1: JSON Format Response

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: Using JSON format ===");

    // TODO 3: Create a hotel agent with tools
    // Hint: Use CreateAIAgent with instructions, name, and tools parameters
    //       Register HotelTools.GetAllHotelsUsingJsonFormat as a tool
    // ChatClientAgent hotelAgent = ...

    // TODO 4: Run the agent with structured output
    // Hint: Use RunAsync<T> with List<Hotel> to get typed results
    // AgentRunResponse<List<Hotel>> hotelResponse = ...

    // TODO 5: Display the results
    // Hint: Iterate through hotelResponse.Result

    // TODO 6: Display token usage
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WritePrimaryLogLine("Token Usage: ");
    // Hint: Access Usage.InputTokenCount, OutputTokenCount, TotalTokenCount
}

#endregion


#region Scenario 2: Toon Format Response

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Using Toon format ===");

    // TODO 7: Create a hotel agent with Toon format instructions
    // Hint: In instructions, specify the exact output format (CSV-like)
    //       Register HotelTools.GetAllHotelsUsingToonFormat as a tool
    // ChatClientAgent hotelAgent = ...

    // TODO 8: Run the agent (returns text, not typed object)
    // Hint: Use RunAsync without generic type parameter
    // AgentRunResponse hotelResponse = ...

    // TODO 9: Display the raw response
    ColoredConsole.WritePrimaryLogLine("Response: ");

    // TODO 10: Display token usage and compare with Scenario 1
    // Question: Which format uses fewer output tokens?
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WritePrimaryLogLine("Token Usage: ");
}

#endregion
