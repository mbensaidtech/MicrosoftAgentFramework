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

// Step 2: Create AzureOpenAIClient with managed identity authentication
AzureOpenAIClient client = new AzureOpenAIClient(new Uri(settings.Endpoint), new DefaultAzureCredential());

// Step 3: Get a ChatClient for the specific deployment
ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);

#endregion

#region Scenario 1: Basic Agent - Simple prompt with default settings

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();

    // Step 1: Create a basic AI Agent from the ChatClient (no instructions, no name)
    ChatClientAgent basicAgent = chatClient.CreateAIAgent();

    // Step 2: Run the agent with a simple string prompt (with spinner to show loading)
    AgentRunResponse basicResponse = await basicAgent.RunAsync("Hello, what is the capital of France?").WithSpinner("Running agent");

    // Step 3: Display the response
    ColoredConsole.WriteInfoLine("=== Scenario 1: Basic Agent ===");
    ColoredConsole.WritePrimaryLogLine(basicResponse.ToString());
}

#endregion

#region Scenario 2: Agent with Instructions - Custom behavior and identity

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    // Step 1: Create an AI Agent with specific instructions and a name
    ChatClientAgent geographyAgent = chatClient.CreateAIAgent(
        instructions: "You are a helpful geography assistant. You are able to answer questions about the geography of the world.",
        name: "GeographyAgent");

    // Step 2: Run the agent with a geography-related question (with spinner to show loading)
    AgentRunResponse geographyResponse = await geographyAgent.RunAsync("Hello, what is the surface area of France?").WithSpinner("Running agent");

    // Step 3: Display the response
    ColoredConsole.WriteInfoLine("=== Scenario 2: Agent with Instructions ===");
    ColoredConsole.WritePrimaryLogLine(geographyResponse.ToString());
}

#endregion

#region Scenario 3: Using ChatMessages - Fine-grained control with message roles

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    // Step 1: Create an AI Agent with specific instructions and a name
    ChatClientAgent geographyAgent = chatClient.CreateAIAgent(
        instructions: "You are a helpful geography assistant. You are able to answer questions about the geography of the world.",
        name: "GeographyAgent");

    // Step 2: Create a system message to define agent behavior
    AIExtensions.ChatMessage systemMessage = new(
        AIExtensions.ChatRole.System,
        "You are a geography expert. Provide detailed and accurate information about world geography.");

    // Step 3: Create a user message with a geography question
    AIExtensions.ChatMessage userMessage = new(
        AIExtensions.ChatRole.User,
        "What are the neighboring countries of France? give me the countries in a list without any other text.");

    // Step 4: Run the agent with an array of ChatMessages (with spinner to show loading)
    AgentRunResponse chatMessageResponse = await geographyAgent.RunAsync([systemMessage, userMessage]).WithSpinner("Running agent");

    // Step 5: Display the response
    ColoredConsole.WriteInfoLine("=== Scenario 3: Using ChatMessages ===");
    ColoredConsole.WritePrimaryLogLine(chatMessageResponse.ToString());
}

#endregion

#region Scenario 4: Get consumed tokens from the agent run response

if (ShouldRunScenario(4))
{
    ColoredConsole.WriteDividerLine();

    // Step 1: Create an AI Agent with specific instructions and a name
    ChatClientAgent colorDecoAgent = chatClient.CreateAIAgent(
        instructions: "You are a helpful color decorator assistant. You are able to answer questions about the color of the world.",
        name: "ColorDecoratorAgent");

    // Step 2: Run the agent with a color-related question (with spinner to show loading)
    AgentRunResponse colorResponse = await colorDecoAgent.RunAsync("Hello, what are colors that match with the color blue? give me the colors in a list without any other text.").WithSpinner("Running agent");

    // Step 3: Display the response
    ColoredConsole.WriteInfoLine("=== Scenario 4: Get consumed tokens from the agent run response ===");
    ColoredConsole.WritePrimaryLogLine(colorResponse.ToString());

    // Step 4: Display the consumed tokens
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WritePrimaryLogLine("Token Usage: ");
    ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {colorResponse.Usage?.InputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {colorResponse.Usage?.OutputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {colorResponse.Usage?.TotalTokenCount}");
}

#endregion