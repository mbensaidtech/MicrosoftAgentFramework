using Azure.Identity;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using OpenAI;
using OpenAI.Chat;
using AIExtensions = Microsoft.Extensions.AI;
using CommonUtilities;
using System.Text.Json;
using System.Text.Json.Serialization;

using AIAgentWithSO;
using AIAgentWithSO.Models;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// Set to: [1], [2], [3] or [1, 2, 3] to run specific scenarios
HashSet<int> scenariosToRun = [1, 2, 3];
// ============================================

bool ShouldRunScenario(int scenario) => scenariosToRun.Count == 0 || scenariosToRun.Contains(scenario);

#region Setup: Configuration and Client Initialization

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
Console.WriteLine($"Endpoint: {settings.Endpoint}");
Console.WriteLine($"Deployment: {settings.ChatDeploymentName}");

// TODO 1: Create AzureOpenAIClient with managed identity authentication
// Hint: Use DefaultAzureCredential for authentication
// AzureOpenAIClient client = ...

// TODO 2: Get a ChatClient for the specific deployment
// ChatClient chatClient = ...

#endregion

#region Scenario 1: Manual Structured Output - Restaurant Information

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();

    // TODO 3: Create an agent with JSON format instructions for Restaurant
    // Hint: Use chatClient.CreateAIAgent() with instructions parameter
    // ChatClientAgent restaurantAgent = ...

    // TODO 4: Run the agent with a restaurant question
    // AgentRunResponse restaurantResponse = ...

    ColoredConsole.WriteInfoLine("=== Scenario 1: Manually defined structured output - Restaurant Information ===");

    // TODO 5: Parse the JSON response and display restaurant information
    // Hint: Use JsonSerializer.Deserialize<Restaurant>() with JsonSerializerOptions
    // Restaurant restaurant = ...

    // TODO 6: Display token usage
    // Hint: Use response.Usage property (InputTokenCount, OutputTokenCount, TotalTokenCount)
}

#endregion

#region Scenario 2: Automatically generated structured output (Recommended) - Restaurant Information

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();

    // TODO 7: Create an agent with simple instructions (no JSON format needed)
    // ChatClientAgent restaurantAgentWithStructuredOutput = ...

    ColoredConsole.WriteInfoLine("=== Scenario 2: Automatically generated structured output - Restaurant Information ===");

    // TODO 8: Run the agent with RunAsync<Restaurant> for automatic structured output
    // Hint: Use await agent.RunAsync<Restaurant>(prompt) - no manual JSON parsing needed!
    // AgentRunResponse<Restaurant> structuredRestaurantResponse = ...

    // TODO 9: Display the structured response
    // Hint: Access properties via structuredRestaurantResponse.Result.PropertyName

    // TODO 10: Display token usage
    // Hint: Use response.Usage property (InputTokenCount, OutputTokenCount, TotalTokenCount)
}

#endregion

#region Scenario 3: Automatically generated structured output using AIAgent and ChatOptions - Restaurant Information

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();

    // TODO 11: Create a JSON schema for the Restaurant type
    // Hint: Use AIJsonUtilities to create schema from type

    // TODO 12: Create ChatOptions with Instructions and ResponseFormat
    // Hint: Set ResponseFormat using ChatResponseFormat.ForJsonSchema()

    // TODO 13: Create an AIAgent using ChatClientAgentOptions
    // Hint: Pass ChatOptions to the agent options

    ColoredConsole.WriteInfoLine("=== Scenario 3: Structured output using AIAgent and ChatOptions ===");

    // TODO 14: Run the agent and deserialize the response
    // Hint: Use response.Deserialize<T>() to get typed result

    // TODO 15: Display the structured response
    // Hint: Access properties via restaurantInfo.PropertyName

    // TODO 16: Display token usage
    // Hint: Use response.Usage property (InputTokenCount, OutputTokenCount, TotalTokenCount)
}

#endregion
