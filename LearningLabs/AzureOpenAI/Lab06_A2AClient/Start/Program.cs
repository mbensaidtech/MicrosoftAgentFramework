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
// (used by scenario 3 only: scenarios 1 and 2 talk to remote agents, whose models run on the A2A server)
// ChatClient chatClient = ...

#endregion

// The remote agents hosted by Lab06_A2AServer
RemoteAgentSettings authAgentSettings = ConfigurationHelper.GetRemoteAgentSettings("AuthAgent");
RemoteAgentSettings customerToneAgentSettings = ConfigurationHelper.GetRemoteAgentSettings("CustomerToneAgent");

#region Scenario 1: Discover a remote agent from its agent card and call it

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: Discover a remote agent from its agent card ===");

    // TODO 4: Read the agent card published at <agent URL>/.well-known/agent-card.json and display it
    // Hint: new A2ACardResolver(new Uri($"{authAgentSettings.Url}/")), then await cardResolver.GetAgentCardAsync().WithSpinner("Reading the agent card")
    //       and WriteAgentCard(authAgentCard) (provided in AgentConsole.cs)
    // AgentCard authAgentCard = ...

    // TODO 5: Create an AIAgent for the remote agent: name, description and protocol binding come from the card
    // AIAgent authAgent = ...

    // TODO 6: Ask the remote agent for a new API key ("Generate a new API key"), find the key in its answer and display it
    // Hint: FindApiKey(generateResponse.Text) (provided in AgentConsole.cs) extracts the key from the natural-language answer
    // string apiKey = ...

    // TODO 7: Ask the remote agent to validate the key, then a tampered copy of it, and display both answers
    // Hint: $"Validate this API key: {apiKey}"; a tampered copy: apiKey[..^1] + (apiKey[^1] == 'A' ? 'B' : 'A')
}

#endregion

#region Scenario 2: Connect to a remote agent by URL, without an agent card

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Connect to a remote agent by URL (direct configuration) ===");

    // TODO 8: Create an A2A client for the URL of the CustomerToneAgent, then an AIAgent from it, and display its name
    // Hint: A2A.A2AClient is written in full because the namespace of this project is also called A2AClient; dispose it ("using").
    //       The client gives the agent its name and description: customerToneAgentSettings.Name and .Description
    // using A2A.A2AClient toneClient = ...
    // AIAgent customerToneAgent = ...

    // TODO 9: Send a customer message to the remote agent and display the tone
    // Hint: $"What is the tone of this customer message: \"{CustomerMessage}\"" with the message of the README
}

#endregion

#region Scenario 3: Use a remote agent as a function tool of a local agent

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: A remote agent as a function tool of a local agent ===");

    // TODO 10: Create the remote AuthAgent from its agent card in one call
    // Hint: GetAIAgentAsync() on an A2ACardResolver reads the card and creates the agent
    // AIAgent authAgent = ...

    // TODO 11: Create a local agent (Azure OpenAI) that can call the remote agent as a tool
    // Hint: chatClient.AsAIAgent(instructions: ..., name: "Assistant", tools: [authAgent.AsAIFunction()])
    // AIAgent assistant = ...

    // TODO 12: Run the local agent, then display the tools it called, its answer and the token usage
    // Hint: WriteToolCalls(response) and WriteTokenUsage(response) are provided in AgentConsole.cs
}

#endregion
