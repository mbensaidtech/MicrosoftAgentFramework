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
using System.Text.Json;

using CommonUtilities;
using AIAgentWithThreads;
using AIAgentWithThreads.Stores;
using AIAgentWithThreads.Models;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// Set to: [1], [2], or [1, 2] to run specific scenarios
HashSet<int> scenariosToRun = [1,2];
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

#region Scenario 1: Create an AI Agent with Threads using the InMemory vector store
if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    Console.WriteLine("=== Scenario 1: Create an AI Agent with Threads using the InMemory vector store ===");

    var inMemoryVectorStore = new InMemoryVectorStore();
    var agentSettings = ConfigurationHelper.GetAgent("GlobalAgent");

    var agentOptions = new ChatClientAgentOptions
    {
        Instructions = agentSettings.Instructions,
        Name = agentSettings.Name,
        ChatMessageStoreFactory = ctx => new InMemoryVectorChatMessageStore(
            inMemoryVectorStore,
            ctx.SerializedState,
            ctx.JsonSerializerOptions)
    };

    var agent = chatClient.CreateAIAgent(agentOptions);

    await RunThreadConversationTestAsync(agent);
}
#endregion

#region Scenario 2: Create an AI Agent with Threads using the MongoDB vector store

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    Console.WriteLine("=== Scenario 2: Create an AI Agent with Threads using the MongoDB vector store ===");

    var mongoVectorStore = new MongoVectorStore(ConfigurationHelper.GetMongoDatabase());
    var agentSettings = ConfigurationHelper.GetAgent("GlobalAgent");

    var agentOptions = new ChatClientAgentOptions
    {
        Instructions = agentSettings.Instructions,
        Name = agentSettings.Name,
        ChatMessageStoreFactory = ctx => new MongoVectorChatMessageStore(
            mongoVectorStore,
            ctx.SerializedState,
            ctx.JsonSerializerOptions)
    };

    var agent = chatClient.CreateAIAgent(agentOptions);

    await RunThreadConversationTestAsync(agent);
}
#endregion

#region Helper Methods

/// <summary>
/// Runs a complete conversation test: asks a question, extracts thread ID, restores thread, and asks a follow-up.
/// </summary>
async Task RunThreadConversationTestAsync(AIAgent agent)
{
    // Step 1: Create a new thread and ask the first question
    var thread = agent.GetNewThread();
    await AskQuestionAsync(agent, "Hello, what is the capital of France?", thread, "First Question: What is the capital of France?");

    // Step 2: Extract and display the thread ID
    var extractedThreadId = ExtractAndDisplayThreadId(thread);

    // Step 3: Restore thread and ask follow-up question
    var restoredThread = RestoreThreadFromId(agent, extractedThreadId);
    var followUpResponse = await AskQuestionAsync(agent, "What is the population of that city?", restoredThread, "Follow-up Question: What is the population of that city?");

    // Step 4: Display token usage for the follow-up response
    DisplayTokenUsage(followUpResponse, "Token Usage (Follow-up)");
}

/// <summary>
/// Asks a question to the agent and displays the response.
/// </summary>
async Task<AgentRunResponse> AskQuestionAsync(AIAgent agent, string question, AgentThread thread, string questionLabel)
{
    ColoredConsole.WritePrimaryLogLine(questionLabel);
    var response = await agent.RunAsync(question, thread);
    ColoredConsole.WriteSecondaryLogLine($"Response: {response}");
    return response;
}

/// <summary>
/// Extracts the thread ID from the thread's serialized state and displays it.
/// </summary>
string? ExtractAndDisplayThreadId(AgentThread thread)
{
    var serializedState = thread.Serialize();
    var extractedThreadId = ExtractThreadIdFromState(serializedState);
    ColoredConsole.WriteInfoLine($"Extracted thread id: {extractedThreadId}");
    return extractedThreadId;
}

/// <summary>
/// Restores a thread from a thread ID using the agent's DeserializeThread method.
/// </summary>
AgentThread RestoreThreadFromId(AIAgent agent, string? threadId)
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("Restoring thread from thread id to test context preservation...");

    var agentThreadState = new AgentThreadState { StoreState = threadId };
    var threadStateElement = JsonSerializer.SerializeToElement(agentThreadState);
    return agent.DeserializeThread(threadStateElement);
}

/// <summary>
/// Displays the token usage from an agent response.
/// </summary>
void DisplayTokenUsage(AgentRunResponse response, string label)
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WritePrimaryLogLine($"{label}: ");
    ColoredConsole.WriteSecondaryLogLine($"Input tokens: {response.Usage?.InputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"Output tokens: {response.Usage?.OutputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"Total tokens: {response.Usage?.TotalTokenCount}");
}

/// <summary>
/// Extracts the thread ID from the serialized state JSON element.
/// </summary>
string? ExtractThreadIdFromState(JsonElement serializedState)
{
    if (serializedState.ValueKind == JsonValueKind.Object)
    {
        // Try direct property access first
        if (serializedState.TryGetProperty("storeState", out var storeStateElement) &&
            storeStateElement.ValueKind == JsonValueKind.String)
        {
            var threadId = storeStateElement.GetString();
            if (!string.IsNullOrWhiteSpace(threadId))
            {
                return threadId;
            }
        }

        // Fall back to deserialization
        try
        {
            var agentThreadState = JsonSerializer.Deserialize<AgentThreadState>(serializedState);
            if (!string.IsNullOrWhiteSpace(agentThreadState?.StoreState))
            {
                return agentThreadState.StoreState;
            }
        }
        catch (Exception ex)
        {
            ColoredConsole.WriteErrorLine("Failed to deserialize thread state as AgentThreadState.");
            ColoredConsole.WriteErrorLine(ex.Message);
        }
    }

    if (serializedState.ValueKind == JsonValueKind.String)
    {
        return serializedState.GetString();
    }

    return null;
}

#endregion
