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

// Step 2: Create an OpenAIClient that targets the Azure OpenAI v1 endpoint (https://<resource>.openai.azure.com/openai/v1/)
// - API key when one is configured
// - otherwise Microsoft Entra ID: DefaultAzureCredential + BearerTokenPolicy (scope https://ai.azure.com/.default)
// WARNING: DefaultAzureCredential is convenient for development. In production, prefer a specific credential
// (e.g. ManagedIdentityCredential) to avoid latency and unintended credential probing.
OpenAIClientOptions clientOptions = new() { Endpoint = AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint) };

// The OpenAIClient(AuthenticationPolicy, ...) constructor is still flagged [Experimental] (OPENAI001) by the OpenAI SDK,
// although it is the pattern documented by Microsoft for Entra ID with the Azure OpenAI v1 API.
#pragma warning disable OPENAI001
OpenAIClient client = !string.IsNullOrWhiteSpace(settings.APIKey)
    ? new OpenAIClient(new ApiKeyCredential(settings.APIKey), clientOptions)
    : new OpenAIClient(new BearerTokenPolicy(new DefaultAzureCredential(), "https://ai.azure.com/.default"), clientOptions);
#pragma warning restore OPENAI001

// Step 3: Get a ChatClient (Chat Completions API) for the deployment
ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);

#endregion

#region Scenario 1: Basic Agent - Simple prompt with default settings

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();

    // Step 1: Create a basic AI Agent from the ChatClient (no instructions, no name)
    AIAgent basicAgent = chatClient.AsAIAgent();

    // Step 2: Run the agent with a simple string prompt (with spinner to show loading)
    AgentResponse basicResponse = await basicAgent.RunAsync("Hello, what is the capital of France?").WithSpinner("Running agent");

    // Step 3: Display the response
    ColoredConsole.WriteInfoLine("=== Scenario 1: Basic Agent ===");
    ColoredConsole.WritePrimaryLogLine(basicResponse.Text);
}

#endregion

#region Scenario 2: Agent with Instructions - Custom behavior and identity

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();

    // Step 1: Create an AI Agent with specific instructions and a name
    AIAgent geographyAgent = chatClient.AsAIAgent(
        instructions: "You are a helpful geography assistant. You are able to answer questions about the geography of the world.",
        name: "GeographyAgent");

    // Step 2: Run the agent with a geography-related question (with spinner to show loading)
    AgentResponse geographyResponse = await geographyAgent.RunAsync("Hello, what is the surface area of France?").WithSpinner("Running agent");

    // Step 3: Display the response
    ColoredConsole.WriteInfoLine($"=== Scenario 2: Agent with Instructions ({geographyAgent.Name}) ===");
    ColoredConsole.WritePrimaryLogLine(geographyResponse.Text);
}

#endregion

#region Scenario 3: Using ChatMessages - Fine-grained control with message roles

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();

    // Step 1: Create an AI Agent with specific instructions and a name
    AIAgent geographyAgent = chatClient.AsAIAgent(
        instructions: "You are a helpful geography assistant. You are able to answer questions about the geography of the world.",
        name: "GeographyAgent");

    // Step 2: Create a system message to refine the agent behavior for this run only
    // (system messages must always be written by the developer, never built from end-user input)
    AIExtensions.ChatMessage systemMessage = new(
        AIExtensions.ChatRole.System,
        "You are a geography expert. Provide detailed and accurate information about world geography.");

    // Step 3: Create a user message with a geography question
    AIExtensions.ChatMessage userMessage = new(
        AIExtensions.ChatRole.User,
        "What are the neighboring countries of France? give me the countries in a list without any other text.");

    // Step 4: Run the agent with a collection of ChatMessages (with spinner to show loading)
    AgentResponse chatMessageResponse = await geographyAgent.RunAsync([systemMessage, userMessage]).WithSpinner("Running agent");

    // Step 5: Display the response
    ColoredConsole.WriteInfoLine("=== Scenario 3: Using ChatMessages ===");
    ColoredConsole.WritePrimaryLogLine(chatMessageResponse.Text);
}

#endregion

#region Scenario 4: Get consumed tokens from the agent response

if (ShouldRunScenario(4))
{
    ColoredConsole.WriteDividerLine();

    // Step 1: Create an AI Agent with specific instructions and a name
    AIAgent colorDecoAgent = chatClient.AsAIAgent(
        instructions: "You are a helpful color decorator assistant. You are able to answer questions about the color of the world.",
        name: "ColorDecoratorAgent");

    // Step 2: Run the agent with a color-related question (with spinner to show loading)
    AgentResponse colorResponse = await colorDecoAgent.RunAsync("Hello, what are colors that match with the color blue? give me the colors in a list without any other text.").WithSpinner("Running agent");

    // Step 3: Display the response
    ColoredConsole.WriteInfoLine("=== Scenario 4: Get consumed tokens from the agent response ===");
    ColoredConsole.WritePrimaryLogLine(colorResponse.Text);

    // Step 4: Display the consumed tokens
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WritePrimaryLogLine("Token Usage: ");
    ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {colorResponse.Usage?.InputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {colorResponse.Usage?.OutputTokenCount}");
    // Reasoning models spend part of the output tokens on hidden reasoning (null/0 for non-reasoning models)
    ColoredConsole.WriteSecondaryLogLine($"  Reasoning tokens (included in output): {colorResponse.Usage?.ReasoningTokenCount ?? 0}");
    ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {colorResponse.Usage?.TotalTokenCount}");
}

#endregion

#region Scenario 5: Streaming - Display the response while it is generated

if (ShouldRunScenario(5))
{
    ColoredConsole.WriteDividerLine();

    // Step 1: Create an AI Agent with specific instructions and a name
    AIAgent storytellerAgent = chatClient.AsAIAgent(
        instructions: "You are a storyteller. You write short, vivid stories for travelers.",
        name: "StorytellerAgent");

    ColoredConsole.WriteInfoLine("=== Scenario 5: Streaming ===");

    // Step 2: Run the agent in streaming mode and write each update as soon as it arrives
    // (no spinner here: the text itself shows the progress)
    List<AgentResponseUpdate> updates = [];
    await foreach (AgentResponseUpdate update in storytellerAgent.RunStreamingAsync("Tell me a four-sentence story about a trip to Paris."))
    {
        Console.Write(update.Text);
        updates.Add(update);
    }

    Console.WriteLine();

    // Step 3: Combine the updates into a single AgentResponse to read the token usage of the streamed run
    AgentResponse streamedResponse = updates.ToAgentResponse();
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WritePrimaryLogLine("Token Usage (streaming): ");
    ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {streamedResponse.Usage?.InputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {streamedResponse.Usage?.OutputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {streamedResponse.Usage?.TotalTokenCount}");
}

#endregion
