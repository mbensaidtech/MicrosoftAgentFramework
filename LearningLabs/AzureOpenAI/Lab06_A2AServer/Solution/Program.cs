using System.ClientModel;
using System.ClientModel.Primitives;
using A2A;
using A2A.AspNetCore;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
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
// dotnet run                          -> the implemented scenarios (a scenario whose code is still the delivered one is left out)
// dotnet run -- --scenarios 2         -> scenario 2 only (and the scenarios it requires)
// dotnet run -- --scenarios 1,3       -> scenarios 1 and 3
// dotnet run -- --scenarios all       -> every scenario, implemented or not
// dotnet run -- --list-scenarios      -> the scenarios of this lab and their state
// This lab has a single scenario: the server runs until you stop it (Ctrl+C)
ScenarioSelection scenarioSelection = ScenarioSelection.FromArgs(args);
// ============================================

bool ShouldRunScenario(int scenario) => scenarioSelection.ShouldRun(scenario);

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

// Paths of the two agents on the server, and address the server listens on (A2AServer:BaseUrl)
const string AuthAgentPath = "/a2a/authAgent";
const string CustomerToneAgentPath = "/a2a/customerToneAgent";
A2AServerSettings serverSettings = ConfigurationHelper.GetA2AServerSettings();

#region Scenario 1: Host two agents (AuthAgent and CustomerToneAgent) with the A2A protocol

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: A2A server with two agents ===");

    // Step 1: Create the ASP.NET Core application builder
    var builder = WebApplication.CreateBuilder(args);

    // Step 2: Create the API key tools of the AuthAgent (the signing secret is optional)
    APIKeySettings apiKeySettings = ConfigurationHelper.GetAPIKeySettings();
    APIKeyTools apiKeyTools = new(apiKeySettings);
    if (!apiKeyTools.IsSecretConfigured)
    {
        ColoredConsole.WriteSecondaryLogLine("APIKeySettings:SecretKey is not set: API keys are signed with a random secret, valid until the server stops.");
    }

    IList<AITool> tools =
    [
        AIFunctionFactory.Create(apiKeyTools.GenerateAPIKey, "generate_api_key"),
        AIFunctionFactory.Create(apiKeyTools.ValidateAPIKey, "validate_api_key")
    ];

    // Step 3: Create the two agents. Their names are the keys used by the A2A hosting to find them.
    AIAgent authAgent = chatClient.AsAIAgent(
        instructions: "You are a helpful API key assistant. You are able to generate and validate API keys. " +
                      "When you generate a key, return the key exactly as produced by the tool.",
        name: "AuthAgent",
        tools: tools);

    AIAgent customerToneAgent = chatClient.AsAIAgent(
        instructions: "You are a helpful customer tone assistant. You are able to detect the tone of a customer's message. " +
                      "Start your answer with the tone in one word (for example: Angry, Neutral, Satisfied), then explain briefly.",
        name: "CustomerToneAgent");

    // Step 4: Give each agent a session store, registered under the name of the agent: the A2A hosting keeps there the
    // conversation of each contextId. Without a store, it uses a NoopAgentSessionStore, which keeps nothing: every
    // request starts a new conversation, even with the same contextId.
    // InMemoryAgentSessionStore is for development: the conversations are lost when the server stops.
    // AgentSessionStore and InMemoryAgentSessionStore are flagged [Experimental] (MAAI001) in Agent Framework 1.26.0.
#pragma warning disable MAAI001
    builder.Services.AddKeyedSingleton<AgentSessionStore>(authAgent.Name, new InMemoryAgentSessionStore());
    builder.Services.AddKeyedSingleton<AgentSessionStore>(customerToneAgent.Name, new InMemoryAgentSessionStore());
#pragma warning restore MAAI001
    ColoredConsole.WriteSecondaryLogLine("Session store: in memory (development only), one per agent.");

    // Step 5: Register an A2A server for each agent (A2A request handler + task store)
    builder.AddA2AServer(authAgent);
    builder.AddA2AServer(customerToneAgent);

    var app = builder.Build();

    // Step 6: Expose each agent with both A2A v1 protocol bindings at the same path
    app.MapA2AJsonRpc(authAgent, AuthAgentPath);
    app.MapA2AHttpJson(authAgent, AuthAgentPath);
    app.MapA2AJsonRpc(customerToneAgent, CustomerToneAgentPath);
    app.MapA2AHttpJson(customerToneAgent, CustomerToneAgentPath);

    // Step 7: Publish the agent card of each agent at <agent path>/.well-known/agent-card.json
    app.MapWellKnownAgentCard(AgentCards.CreateAuthAgentCard($"{serverSettings.BaseUrl}{AuthAgentPath}"), AuthAgentPath);
    app.MapWellKnownAgentCard(AgentCards.CreateCustomerToneAgentCard($"{serverSettings.BaseUrl}{CustomerToneAgentPath}"), CustomerToneAgentPath);

    // Step 8: Display the endpoints, then run the server until Ctrl+C
    ColoredConsole.WritePrimaryLogLine($"AuthAgent:         {serverSettings.BaseUrl}{AuthAgentPath}");
    ColoredConsole.WritePrimaryLogLine($"  Agent card:      {serverSettings.BaseUrl}{AuthAgentPath}/.well-known/agent-card.json");
    ColoredConsole.WritePrimaryLogLine($"CustomerToneAgent: {serverSettings.BaseUrl}{CustomerToneAgentPath}");
    ColoredConsole.WritePrimaryLogLine($"  Agent card:      {serverSettings.BaseUrl}{CustomerToneAgentPath}/.well-known/agent-card.json");
    ColoredConsole.WriteSecondaryLogLine("Press Ctrl+C to stop the server.");

    await app.RunAsync(serverSettings.BaseUrl);
}

#endregion
