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
    ChatClientAgent userDataExtractorAgent = chatClient.CreateAIAgent(
        instructions: "You are a helpful assistant that can extract user data from a given text. If the info already exists in the context, don't extract it again. Extract only data that helps us to know the user better.");

    //Step 2: Create the chat options for the agent with memory
    ChatOptions chatOptions = new()
    {
        Instructions = "You are a helpful assistant that can use the user memory to answer questions. When you start a new conversation, start by introducing yourself and welcome the user to the conversation using the user name from the memories if there is any."
    };

    //Step 3: Create the agent with memory using the UserMemoryAIContextProvider
    ChatClientAgent agentWithMemory = chatClient.CreateAIAgent(new ChatClientAgentOptions
    {
        ChatOptions = chatOptions,
        AIContextProviderFactory = _ => new UserMemoryAIContextProvider(userDataExtractorAgent, userId, userMemoriesRepository)
    });

    //Step 4: Create a new thread for the conversation
    AgentThread thread = agentWithMemory.GetNewThread();
    
    //Step 5: Assistant starts the conversation
    AgentRunResponse greeting = await agentWithMemory.RunAsync(thread);
    ColoredConsole.WriteAssistantLine(greeting.Text);
    
    //Step 6: User responds in a loop
    while (true)
    {
        Console.Write("> ");
        string? userInput = Console.ReadLine();
        if (string.IsNullOrEmpty(userInput) || userInput == "exit")
        {
            break;
        }
        ChatMessage userMessage = new(ChatRole.User, userInput);
        AgentRunResponse response = await agentWithMemory.RunAsync(userMessage, thread).WithSpinner("Running agent");
        ColoredConsole.WriteAssistantLine(response.Text);
    }
}

#endregion
