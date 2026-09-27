using System.ClientModel;
using System.ClientModel.Primitives;
using Azure.Identity;
using Microsoft.Agents.AI;
using OpenAI;
using OpenAI.Chat;
using AIExtensions = Microsoft.Extensions.AI;
using CommonUtilities;

using FirstBasicAIAgent;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// Set to: [1], [2], [3], [4], [5] or [1, 2, 3, 4, 5] to run specific scenarios
HashSet<int> scenariosToRun = [1, 2, 3, 4, 5];
// ============================================

bool ShouldRunScenario(int scenario) => scenariosToRun.Count == 0 || scenariosToRun.Contains(scenario);

#region Setup: Configuration and Client Initialization

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
Console.WriteLine($"Endpoint: {settings.Endpoint}");
Console.WriteLine($"Deployment: {settings.ChatDeploymentName}");

// TODO 1: Create the OpenAIClientOptions that point to the Azure OpenAI v1 endpoint
// Hint: AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint) turns "https://<resource>.openai.azure.com/" into ".../openai/v1/"
// OpenAIClientOptions clientOptions = ...

// TODO 2: Create the OpenAIClient
// - If settings.APIKey is set: authenticate with an ApiKeyCredential
// - Otherwise: authenticate with Microsoft Entra ID using a BearerTokenPolicy built from
//   DefaultAzureCredential and the scope "https://ai.azure.com/.default"
// Note: the AuthenticationPolicy constructor is flagged experimental (OPENAI001), keep the pragma around it.
#pragma warning disable OPENAI001
// OpenAIClient client = ...
#pragma warning restore OPENAI001

// TODO 3: Get a ChatClient (Chat Completions API) for the deployment
// ChatClient chatClient = ...

#endregion

#region Scenario 1: Basic Agent - Simple prompt with default settings

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();

    // TODO 4: Create a basic AI Agent from the ChatClient (no instructions, no name)
    // AIAgent basicAgent = ...

    // TODO 5: Run the agent with a simple string prompt
    // Hint: chain .WithSpinner("Running agent") to show a loading animation
    // AgentResponse basicResponse = ...

    // TODO 6: Display the response
    ColoredConsole.WriteInfoLine("=== Scenario 1: Basic Agent ===");
    // ColoredConsole.WritePrimaryLogLine(basicResponse.Text);
}

#endregion

#region Scenario 2: Agent with Instructions - Custom behavior and identity

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();

    // TODO 7: Create an AI Agent with specific instructions and a name
    // AIAgent geographyAgent = ...

    // TODO 8: Run the agent with a geography-related question
    // AgentResponse geographyResponse = ...

    // TODO 9: Display the response (the agent name is available through geographyAgent.Name)
    ColoredConsole.WriteInfoLine("=== Scenario 2: Agent with Instructions ===");
    // ColoredConsole.WritePrimaryLogLine(geographyResponse.Text);
}

#endregion

#region Scenario 3: Using ChatMessages - Fine-grained control with message roles

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();

    // TODO 10: Create an AI Agent with specific instructions and a name
    // AIAgent geographyAgent = ...

    // TODO 11: Create a system message to refine the agent behavior for this run only
    // AIExtensions.ChatMessage systemMessage = ...

    // TODO 12: Create a user message with a geography question
    // AIExtensions.ChatMessage userMessage = ...

    // TODO 13: Run the agent with a collection of ChatMessages
    // AgentResponse chatMessageResponse = ...

    // TODO 14: Display the response
    ColoredConsole.WriteInfoLine("=== Scenario 3: Using ChatMessages ===");
    // ColoredConsole.WritePrimaryLogLine(chatMessageResponse.Text);
}

#endregion

#region Scenario 4: Get consumed tokens from the agent response

if (ShouldRunScenario(4))
{
    ColoredConsole.WriteDividerLine();

    // TODO 15: Create an AI Agent with specific instructions and a name
    // AIAgent colorDecoAgent = ...

    // TODO 16: Run the agent with a color-related question
    // AgentResponse colorResponse = ...

    // TODO 17: Display the response
    ColoredConsole.WriteInfoLine("=== Scenario 4: Get consumed tokens from the agent response ===");
    // ColoredConsole.WritePrimaryLogLine(colorResponse.Text);

    // TODO 18: Display the consumed tokens
    // Hint: Use response.Usage property (InputTokenCount, OutputTokenCount, ReasoningTokenCount, TotalTokenCount)
}

#endregion

#region Scenario 5: Streaming - Display the response while it is generated

if (ShouldRunScenario(5))
{
    ColoredConsole.WriteDividerLine();

    // TODO 19: Create an AI Agent with specific instructions and a name
    // AIAgent storytellerAgent = ...

    ColoredConsole.WriteInfoLine("=== Scenario 5: Streaming ===");

    // TODO 20: Run the agent with RunStreamingAsync and write each AgentResponseUpdate as soon as it arrives
    // Hint: await foreach (AgentResponseUpdate update in ...) { Console.Write(update.Text); }
    // Also keep every update in a List<AgentResponseUpdate> for TODO 21
    // Note: do not use .WithSpinner() here, the streamed text itself shows the progress

    Console.WriteLine();

    // TODO 21: Combine the updates into a single AgentResponse and display its token usage
    // Hint: AgentResponse streamedResponse = updates.ToAgentResponse();
}

#endregion
