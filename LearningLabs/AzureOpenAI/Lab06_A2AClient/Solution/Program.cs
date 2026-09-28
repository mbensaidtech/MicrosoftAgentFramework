using System.ClientModel;
using System.ClientModel.Primitives;
using A2A;
using Azure.Identity;
using Microsoft.Agents.AI;
using OpenAI;
using OpenAI.Chat;
using CommonUtilities;

using A2AClient;
using static A2AClient.AgentConsole;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// Set to: [1], [2], [3] or [1, 2, 3] to run specific scenarios
// All scenarios need the A2A server of Lab06_A2AServer (http://localhost:5000)
HashSet<int> scenariosToRun = [1, 2, 3];
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
// (used by scenario 3 only: scenarios 1 and 2 talk to remote agents, whose models run on the A2A server)
ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);

#endregion

// The remote agents hosted by Lab06_A2AServer
RemoteAgentSettings authAgentSettings = ConfigurationHelper.GetRemoteAgentSettings("AuthAgent");
RemoteAgentSettings customerToneAgentSettings = ConfigurationHelper.GetRemoteAgentSettings("CustomerToneAgent");

#region Scenario 1: Discover a remote agent from its agent card and call it

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: Discover a remote agent from its agent card ===");

    // Step 1: Read the agent card published at <agent URL>/.well-known/agent-card.json
    A2ACardResolver cardResolver = new(new Uri($"{authAgentSettings.Url}/"));
    AgentCard authAgentCard = await cardResolver.GetAgentCardAsync().WithSpinner("Reading the agent card");
    WriteAgentCard(authAgentCard);

    // Step 2: Create an AIAgent for the remote agent: name, description and protocol binding come from the card
    AIAgent authAgent = authAgentCard.AsAIAgent();

    // Step 3: Ask the remote agent for a new API key (its tools run on the server)
    AgentResponse generateResponse = await authAgent.RunAsync("Generate a new API key").WithSpinner("Calling the remote agent");
    string apiKey = FindApiKey(generateResponse.Text);
    ColoredConsole.WriteAssistantLine($"Generated API key: {apiKey}");

    // Step 4: Ask the remote agent to validate the key, then a tampered copy of it
    AgentResponse validResponse = await authAgent.RunAsync($"Validate this API key: {apiKey}").WithSpinner("Calling the remote agent");
    ColoredConsole.WriteAssistantLine($"Validation of the generated key: {validResponse.Text}");

    string tamperedKey = apiKey[..^1] + (apiKey[^1] == 'A' ? 'B' : 'A');
    AgentResponse tamperedResponse = await authAgent.RunAsync($"Validate this API key: {tamperedKey}").WithSpinner("Calling the remote agent");
    ColoredConsole.WriteAssistantLine($"Validation of a tampered key: {tamperedResponse.Text}");
}

#endregion

#region Scenario 2: Connect to a remote agent by URL, without an agent card

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Connect to a remote agent by URL (direct configuration) ===");

    // Step 1: Create an A2A client for the agent URL (JSON-RPC binding). No agent card is read:
    // the client gives the agent its name and description.
    // A2A.A2AClient is written in full because the namespace of this project is also called A2AClient.
    using A2A.A2AClient toneClient = new(new Uri(customerToneAgentSettings.Url));
    AIAgent customerToneAgent = toneClient.AsAIAgent(name: customerToneAgentSettings.Name, description: customerToneAgentSettings.Description);
    ColoredConsole.WritePrimaryLogLine($"Remote agent: {customerToneAgent.Name} at {customerToneAgentSettings.Url}");

    // Step 2: Send a customer message to the remote agent
    const string CustomerMessage = "I have been waiting for my order for two weeks and nobody answers my emails!";
    ColoredConsole.WriteUserLine($"Customer message: {CustomerMessage}");
    AgentResponse toneResponse = await customerToneAgent.RunAsync($"What is the tone of this customer message: \"{CustomerMessage}\"")
        .WithSpinner("Calling the remote agent");
    ColoredConsole.WriteAssistantLine($"Tone: {toneResponse.Text}");
}

#endregion

#region Scenario 3: Use a remote agent as a function tool of a local agent

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: A remote agent as a function tool of a local agent ===");

    // Step 1: Create the remote AuthAgent from its agent card (as in scenario 1)
    A2ACardResolver cardResolver = new(new Uri($"{authAgentSettings.Url}/"));
    AIAgent authAgent = await cardResolver.GetAIAgentAsync().WithSpinner("Reading the agent card");

    // Step 2: Create a local agent (Azure OpenAI) that can call the remote agent as a tool
    AIAgent assistant = chatClient.AsAIAgent(
        instructions: "You are a helpful assistant. Use the available tools to answer the user's questions.",
        name: "Assistant",
        tools: [authAgent.AsAIFunction()]);

    // Step 3: Run the local agent: its model decides when to call the remote agent
    AgentResponse response = await assistant.RunAsync(
        "I need a new API key. Generate one, check that it is valid, and give me the key and the result of the check.")
        .WithSpinner("Running the local agent");
    WriteToolCalls(response);
    ColoredConsole.WriteAssistantLine($"Assistant: {response.Text}");
    WriteTokenUsage(response);
}

#endregion
