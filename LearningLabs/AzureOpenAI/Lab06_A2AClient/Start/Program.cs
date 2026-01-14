using Azure.Identity;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.OpenAI;
using OpenAI;
using OpenAI.Chat;
using CommonUtilities;
using A2AClient;
using A2A;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// Set to: [1], [2], or [1, 2] to run specific scenarios
HashSet<int> scenariosToRun = [1];
// ============================================

bool ShouldRunScenario(int scenario) => scenariosToRun.Count == 0 || scenariosToRun.Contains(scenario);

#region Setup: Configuration and Client Initialization

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
var remoteAuthAgentSettings = ConfigurationHelper.GetRemoteAuthAgentSettings();

Console.WriteLine($"Endpoint: {settings.Endpoint}");
Console.WriteLine($"Deployment: {settings.ChatDeploymentName}");
Console.WriteLine($"Remote Auth Agent: {remoteAuthAgentSettings.Name}");
Console.WriteLine($"Remote Auth Agent Description: {remoteAuthAgentSettings.Description}");
Console.WriteLine($"Remote Auth Agent URL: {remoteAuthAgentSettings.Url}");

// Step 2: Create AzureOpenAIClient with managed identity authentication
AzureOpenAIClient client = new AzureOpenAIClient(new Uri(settings.Endpoint), new DefaultAzureCredential());

// Step 3: Get a ChatClient for the specific deployment
ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);

#endregion

ColoredConsole.WriteDividerLine();

#region Scenario 1: Connect to A2A Server and interact with remote agent

if (ShouldRunScenario(1))
{
    // =====================================================
    // TODO 1: Create a remote agent using the A2AClient
    // Hint: Call CreateRemoteAgentUsingUrlAsync(remoteAuthAgentSettings)
    // Store the result in: AIAgent remoteAuthAgent = ...
    // =====================================================
    

    // =====================================================
    // TODO 2: Display the remote agent name
    // Hint: Use ColoredConsole.WriteInfoLine($"Remote Auth Agent: {remoteAuthAgent.Name}");
    // =====================================================
    

    // =====================================================
    // TODO 3: Generate an API key using the remote agent
    // Hint: Call GenerateApiKeyAsync(remoteAuthAgent) and store the result
    // Store in: string apiKey = ...
    // =====================================================
    

    // =====================================================
    // TODO 4: Validate the generated API key
    // Hint: Call ValidateApiKeyAsync(remoteAuthAgent, apiKey)
    // =====================================================
    
}

#endregion

#region Helper Methods - Provided (Do Not Modify)

// This method creates a remote agent using the A2AClient with direct URL
async Task<AIAgent> CreateRemoteAgentUsingUrlAsync(RemoteAuthAgentSettings remoteAuthAgentSettings)
{
    var a2aClient = new A2A.A2AClient(new Uri(remoteAuthAgentSettings.Url));
    return a2aClient.GetAIAgent();
}

// This method creates a remote agent using the A2ACardResolver (Alternative approach)
async Task<AIAgent> CreateRemoteAgentUsingResolverAsync(RemoteAuthAgentSettings remoteAuthAgentSettings)
{
    var agentCardResolver = new A2ACardResolver(new Uri(remoteAuthAgentSettings.Url), new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(60)
    });
    return await agentCardResolver.GetAIAgentAsync();
}

// This method generates an API key using the remote agent
static async Task<string> GenerateApiKeyAsync(AIAgent remoteAuthAgent)
{
    ColoredConsole.WriteSecondaryLogLine($"Scenario 1: Generate an API key using the remote agent");
    var agentRunResponse = await remoteAuthAgent.RunAsync("Generate a new API key");
    string apiKey = agentRunResponse.Messages
        .Where(m => !string.IsNullOrWhiteSpace(m.Text))
        .Last()
        .Text;
    ColoredConsole.WriteAssistantLine($"Generated API Key: {apiKey}");
    
    return apiKey;
}

// This method validates an API key using the remote agent
static async Task ValidateApiKeyAsync(AIAgent remoteAuthAgent, string apiKey)
{
    ColoredConsole.WriteSecondaryLogLine($"Scenario 2: Validate the API key using the remote agent");
    var agentRunResponse = await remoteAuthAgent.RunAsync($"Validate this API key: {apiKey}");
    foreach (var chatMessage in agentRunResponse.Messages.Where(m => !string.IsNullOrWhiteSpace(m.Text)))
    {
        ColoredConsole.WriteAssistantLine($"Validated API Key: {chatMessage.Text}");
    }
}

#endregion
