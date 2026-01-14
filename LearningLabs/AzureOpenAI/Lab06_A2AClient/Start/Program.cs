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
var a2aServerSettings = ConfigurationHelper.GetA2AServerSettings();

Console.WriteLine($"Endpoint: {settings.Endpoint}");
Console.WriteLine($"Deployment: {settings.ChatDeploymentName}");
Console.WriteLine($"A2A Server: {a2aServerSettings.Url}");

// Step 2: Create AzureOpenAIClient with managed identity authentication
AzureOpenAIClient client = new AzureOpenAIClient(new Uri(settings.Endpoint), new DefaultAzureCredential());

// Step 3: Get a ChatClient for the specific deployment
ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);

#endregion

ColoredConsole.WriteDividerLine();

#region Scenario 1: Connect to A2A Server and interact with remote agent

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteInfoLine("Scenario 1: A2A Client - Connect to Remote Auth Agent");

    // TODO: Step 1 - Create an HTTP client for A2A communication
    // Hint: Use new HttpClient()
    
    // TODO: Step 2 - Create an A2A client to connect to the remote server
    // Hint: Use new A2AClient.Client.A2AClient(httpClient, a2aServerSettings.Url)

    // TODO: Step 3 - Get the agent card from the remote server
    // Hint: Use await a2aClient.GetAgentCardAsync()
    // Display the agent name, description, version, and available skills

    ColoredConsole.WriteDividerLine();

    // TODO: Step 4 - Create an interactive loop to send requests to the remote agent
    // Hint: 
    // - Read user input with Console.ReadLine()
    // - Create a SendTaskRequest with TaskSendParams containing the user's message
    // - Send using await a2aClient.SendTaskAsync(taskRequest)
    // - Extract and display the response from response.Result.Status.Message.Parts

    ColoredConsole.WriteInfoLine("TODO: Implement the A2A client logic");
}

#endregion

#region Scenario 2: Batch requests to remote agent

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteInfoLine("Scenario 2: Batch Requests to Remote Auth Agent");

    // TODO: Create an A2A client and send multiple batch requests
    // Example requests:
    // - "Generate a new API key for me"
    // - "Generate another API key"  
    // - "What can you help me with?"

    ColoredConsole.WriteInfoLine("TODO: Implement batch requests logic");
}

#endregion

