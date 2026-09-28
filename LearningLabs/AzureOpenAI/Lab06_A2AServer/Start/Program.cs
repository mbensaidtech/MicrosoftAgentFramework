using System.ClientModel;
using System.ClientModel.Primitives;
using A2A;
using A2A.AspNetCore;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using OpenAI.Chat;
using CommonUtilities;

using A2AServer;
using A2AServer.Tools;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// This lab has a single scenario: the server runs until you stop it (Ctrl+C)
HashSet<int> scenariosToRun = [1];
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

// TODO 4: TEMPORARY WORKAROUND (see StreamingWorkaround.cs and the README): the A2A hosting runs the agents in streaming mode,
// which currently fails with Azure OpenAI content-filter chunks (dotnet/extensions#7790).
// Create the IChatClient of the agents, which answers streaming requests with a non-streaming call.
// Hint: chatClient.AsIChatClient().WithNonStreamingResponses()
// IChatClient agentChatClient = ...

#endregion

// Paths of the two agents on the server, and address the server listens on (A2AServer:BaseUrl)
const string AuthAgentPath = "/a2a/authAgent";
const string CustomerToneAgentPath = "/a2a/customerToneAgent";
A2AServerSettings serverSettings = ConfigurationHelper.GetA2AServerSettings();

#region Scenario 1: Host two agents (AuthAgent and CustomerToneAgent) with the A2A protocol

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: A2A server with two agents ===");

    // TODO 5: Create the ASP.NET Core application builder
    // var builder = ...

    // TODO 6: Create the API key tools of the AuthAgent from ConfigurationHelper.GetAPIKeySettings(),
    // and display a notice when the signing secret is not configured (apiKeyTools.IsSecretConfigured)
    // APIKeyTools apiKeyTools = ...

    // TODO 7: Wrap GenerateAPIKey and ValidateAPIKey as AI tools named "generate_api_key" and "validate_api_key"
    // IList<AITool> tools = ...

    // TODO 8: Create the two agents from agentChatClient: "AuthAgent" (with the tools) and "CustomerToneAgent" (no tools)
    // Hint: agentChatClient.AsAIAgent(instructions: ..., name: ..., tools: ...), instructions in the README.
    //       The names are the keys used by the A2A hosting to find the agents.
    // AIAgent authAgent = ...
    // AIAgent customerToneAgent = ...

    // TODO 9: Register an A2A server for each agent (builder.AddA2AServer), then build the application
    // var app = ...

    // TODO 10: Expose each agent with both A2A v1 protocol bindings at its path (AuthAgentPath, CustomerToneAgentPath)
    // Hint: app.MapA2AJsonRpc(agent, path) and app.MapA2AHttpJson(agent, path)

    // TODO 11: Publish the agent card of each agent at <agent path>/.well-known/agent-card.json
    // Hint: app.MapWellKnownAgentCard(card, path) with AgentCards.CreateAuthAgentCard($"{serverSettings.BaseUrl}{AuthAgentPath}")
    //       and AgentCards.CreateCustomerToneAgentCard(...)

    // Provided: display the endpoints (the server only listens once TODO 12 runs it)
    ColoredConsole.WritePrimaryLogLine($"AuthAgent:         {serverSettings.BaseUrl}{AuthAgentPath}");
    ColoredConsole.WritePrimaryLogLine($"  Agent card:      {serverSettings.BaseUrl}{AuthAgentPath}/.well-known/agent-card.json");
    ColoredConsole.WritePrimaryLogLine($"CustomerToneAgent: {serverSettings.BaseUrl}{CustomerToneAgentPath}");
    ColoredConsole.WritePrimaryLogLine($"  Agent card:      {serverSettings.BaseUrl}{CustomerToneAgentPath}/.well-known/agent-card.json");
    ColoredConsole.WriteSecondaryLogLine("Press Ctrl+C to stop the server.");

    // TODO 12: Run the server until Ctrl+C, on the address of the configuration
    // Hint: await app.RunAsync(serverSettings.BaseUrl);
}

#endregion
