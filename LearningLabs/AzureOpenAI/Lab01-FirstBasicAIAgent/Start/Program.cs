using Azure.Identity;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using OpenAI;
using OpenAI.Chat;
using AIExtensions = Microsoft.Extensions.AI;
using CommonUtilities;

using FirstBasicAIAgent;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// Set to: [1], [2], [3], [4] or [1, 2, 3, 4] to run specific scenarios
HashSet<int> scenariosToRun = [1, 2, 3, 4];
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

#region Scenario 1: Basic Agent - Simple prompt with default settings

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();

    // TODO 3: Create a basic AI Agent from the ChatClient (no instructions, no name)
    // ChatClientAgent basicAgent = ...

    // TODO 4: Run the agent with a simple string prompt
    // AgentRunResponse basicResponse = ...

    // TODO 5: Display the response
    ColoredConsole.WriteInfoLine("=== Scenario 1: Basic Agent ===");
    // ColoredConsole.WritePrimaryLogLine(basicResponse.ToString());
}

#endregion

#region Scenario 2: Agent with Instructions - Custom behavior and identity

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();

    // TODO 6: Create an AI Agent with specific instructions and a name
    // ChatClientAgent geographyAgent = ...

    // TODO 7: Run the agent with a geography-related question
    // AgentRunResponse geographyResponse = ...

    // TODO 8: Display the response
    ColoredConsole.WriteInfoLine("=== Scenario 2: Agent with Instructions ===");
    // ColoredConsole.WritePrimaryLogLine(geographyResponse.ToString());
}

#endregion

#region Scenario 3: Using ChatMessages - Fine-grained control with message roles

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();

    // TODO 9: Create an AI Agent with specific instructions and a name
    // ChatClientAgent geographyAgent = ...

    // TODO 10: Create a system message to define agent behavior
    // AIExtensions.ChatMessage systemMessage = ...

    // TODO 11: Create a user message with a geography question
    // AIExtensions.ChatMessage userMessage = ...

    // TODO 12: Run the agent with an array of ChatMessages
    // AgentRunResponse chatMessageResponse = ...

    // TODO 13: Display the response
    ColoredConsole.WriteInfoLine("=== Scenario 3: Using ChatMessages ===");
    // ColoredConsole.WritePrimaryLogLine(chatMessageResponse.ToString());
}

#endregion

#region Scenario 4: Get consumed tokens from the agent run response

if (ShouldRunScenario(4))
{
    ColoredConsole.WriteDividerLine();

    // TODO 14: Create an AI Agent with specific instructions and a name
    // ChatClientAgent colorDecoAgent = ...

    // TODO 15: Run the agent with a color-related question
    // AgentRunResponse colorResponse = ...

    // TODO 16: Display the response
    ColoredConsole.WriteInfoLine("=== Scenario 4: Get consumed tokens from the agent run response ===");
    // ColoredConsole.WritePrimaryLogLine(colorResponse.ToString());

    // TODO 17: Display the consumed tokens
    // Hint: Use response.Usage property (InputTokenCount, OutputTokenCount, TotalTokenCount)
}

#endregion
