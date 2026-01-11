using Azure.Identity;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using OpenAI.Chat;
using OpenAI;
using ChatOptions = Microsoft.Extensions.AI.ChatOptions;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using Microsoft.Extensions.AI;

using CommonUtilities;
using AIAgentWithAIContextProvider;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// Set to: [1], [2], [3] or [1, 2, 3] to run specific scenarios
HashSet<int> scenariosToRun = [1];
// ============================================

bool ShouldRunScenario(int scenario) => scenariosToRun.Count == 0 || scenariosToRun.Contains(scenario);

#region Setup: Configuration and Client Initialization

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
Console.WriteLine($"Endpoint: {settings.Endpoint}");
Console.WriteLine($"Deployment: {settings.ChatDeploymentName}");

// Step 2: Load MongoDB configuration and verify connectivity
var mongoConfig = ConfigurationHelper.GetMongoDbConfiguration();
await MongoDbHealthCheck.EnsureMongoDbIsRunningAsync(mongoConfig.ConnectionString, mongoConfig.DatabaseName);

// Step 3: Get MongoDB database instance
var mongoDatabase = ConfigurationHelper.GetMongoDatabase();

// Step 4: Create UserMemoriesRepository
var userMemoriesRepository = new UserMemoriesRepository(mongoDatabase, mongoConfig.UserMemoriesCollectionName);

// Step 5: Create AzureOpenAIClient with managed identity authentication
AzureOpenAIClient client = new AzureOpenAIClient(new Uri(settings.Endpoint), new DefaultAzureCredential());

// Step 6: Get a ChatClient for the specific deployment
ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);

#endregion

#region Scenario 1: User Memory using AI Context Provider

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Running Scenario 1: User Memory using AI Context Provider ===");

    string userId = "a7e00675-50b0-4fe7-94bb-53d00ba938d7";
    
    //Step 1: Create the user data extractor agent
    // TODO: Create a ChatClientAgent with instructions for extracting user data

    //Step 2: Create the chat options for the agent with memory
    // TODO: Create ChatOptions with instructions for the assistant

    //Step 3: Create the agent with memory using the UserMemoryAIContextProvider
    // TODO: Create a ChatClientAgent with ChatClientAgentOptions that includes:
    //       - ChatOptions
    //       - AIContextProviderFactory that creates a UserMemoryAIContextProvider

    //Step 4: Create a new thread for the conversation
    // TODO: Create a new AgentThread

    //Step 5: Assistant starts the conversation
    // TODO: Run the agent without user input to get a greeting

    //Step 6: User responds in a loop
    // TODO: Implement the conversation loop where:
    //       - User enters input
    //       - Exit if input is empty or "exit"
    //       - Create a ChatMessage and run the agent
    //       - Display the response

    ColoredConsole.WriteWarningLine("TODO: Complete steps 1-6 to implement the scenario");
}

#endregion
