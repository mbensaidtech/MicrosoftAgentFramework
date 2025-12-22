using Azure.Identity;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.OpenAI;
using OpenAI;
using OpenAI.Chat;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Microsoft.SemanticKernel.Connectors.MongoDB;
using Microsoft.Extensions.VectorData;
using Microsoft.Extensions.AI;

using CommonUtilities;
using AIAgentWithThreads;
using AIAgentWithThreads.Stores;

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
Console.WriteLine($"Endpoint: {settings.Endpoint}");
Console.WriteLine($"Deployment: {settings.DeploymentName}");

// Step 2: Create AzureOpenAIClient with managed identity authentication
AzureOpenAIClient client = new AzureOpenAIClient(new Uri(settings.Endpoint), new DefaultAzureCredential());

// Step 3: Get a ChatClient for the specific deployment
ChatClient chatClient = client.GetChatClient(settings.DeploymentName);

#endregion

ColoredConsole.WriteDividerLine();

# region Scenario 1: Create an AI Agent with Threads using the InMemory vector store
if (ShouldRunScenario(1))
{
    VectorStore vectorStore = new InMemoryVectorStore();

    var agent = chatClient.CreateAIAgent();
    var response = await agent.RunAsync("Hello, what is the capital of France?");
    Console.WriteLine(response);
}
#endregion

# region Scenario 2: Create an AI Agent with Threads using the MongoDB vector store

if (ShouldRunScenario(2))
{
    VectorStore vectorStore = new MongoVectorStore(ConfigurationHelper.GetMongoDatabase());
    var agentSettings = ConfigurationHelper.GetAgent("GlobalAgent");
     var orchestratorOptions = new Microsoft.Agents.AI.ChatClientAgentOptions
        {
            Instructions = agentSettings.Instructions,
            Name = agentSettings.Name,
            ChatMessageStoreFactory = ctx =>
            {
                return new VectorChatMessageStore(
                    vectorStore,
                    ctx.SerializedState,
                    ctx.JsonSerializerOptions);
            }
        };
    var agent = chatClient.CreateAIAgent(orchestratorOptions);
    var response = await agent.RunAsync("Hello, what is the capital of France?");
    Console.WriteLine(response);
}
#endregion


