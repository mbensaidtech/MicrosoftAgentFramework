using Azure.Identity;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using OpenAI;
using OpenAI.Chat;
using AIExtensions = Microsoft.Extensions.AI;
using CommonUtilities;

using FirstBasicAIAgent;

#region Setup: Configuration and Client Initialization

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
Console.WriteLine($"Endpoint: {settings.Endpoint}");
Console.WriteLine($"Deployment: {settings.DeploymentName}");

// TODO 1: Create AzureOpenAIClient with managed identity authentication
// Use the endpoint from settings and DefaultAzureCredential for authentication
// AzureOpenAIClient client = ...

// TODO 2: Get a ChatClient for the specific deployment
// Use the deployment name from settings
// ChatClient chatClient = ...

#endregion

ColoredConsole.WriteDividerLine();

#region Scenario 1: Basic Agent - Simple prompt with default settings

// TODO 3: Create a basic AI Agent from the ChatClient (no instructions, no name)
// Use the CreateAIAgent() extension method
// ChatClientAgent basicAgent = ...

// TODO 4: Run the agent with a simple string prompt
// Use RunAsync with a question like "Hello, what is the capital of France?"
// AgentRunResponse basicResponse = ...

// TODO 5: Display the response
Console.WriteLine("=== Scenario 1: Basic Agent ===");
// Console.WriteLine(basicResponse);

#endregion

ColoredConsole.WriteDividerLine();

#region Scenario 2: Agent with Instructions - Custom behavior and identity

// TODO 6: Create an AI Agent with specific instructions and a name
// Use CreateAIAgent(instructions: "...", name: "...")
// ChatClientAgent geographyAgent = ...

// TODO 7: Run the agent with a geography-related question
// AgentRunResponse geographyResponse = ...

// TODO 8: Display the response
Console.WriteLine("=== Scenario 2: Agent with Instructions ===");
// Console.WriteLine(geographyResponse);

#endregion

ColoredConsole.WriteDividerLine();

#region Scenario 3: Using ChatMessages - Fine-grained control with message roles

// TODO 9: Create a system message to define agent behavior
// Use AIExtensions.ChatMessage with AIExtensions.ChatRole.System
// AIExtensions.ChatMessage systemMessage = ...

// TODO 10: Create a user message with a geography question
// Use AIExtensions.ChatMessage with AIExtensions.ChatRole.User
// AIExtensions.ChatMessage userMessage = ...

// TODO 11: Run the agent with an array of ChatMessages
// Use RunAsync with [systemMessage, userMessage]
// AgentRunResponse chatMessageResponse = ...

// TODO 12: Display the response
Console.WriteLine("=== Scenario 3: Using ChatMessages ===");
// Console.WriteLine(chatMessageResponse);

#endregion

ColoredConsole.WriteDividerLine();

#region Scenario 4: Get consumed tokens from the agent run response

// TODO 13: Create an AI Agent with specific instructions and a name (e.g., ColorDecoratorAgent)
// ChatClientAgent colorDecoAgent = ...

// TODO 14: Run the agent with a color-related question
// AgentRunResponse colorResponse = ...

// TODO 15: Display the response
Console.WriteLine("=== Scenario 4: Get consumed tokens from the agent run response ===");
// Console.WriteLine(colorResponse);

// TODO 16: Display the consumed tokens using colorResponse.Usage
// Use ColoredConsole.WritePrimaryLogLine and ColoredConsole.WriteSecondaryLogLine
// Properties: InputTokenCount, OutputTokenCount, TotalTokenCount, AdditionalCounts

#endregion
