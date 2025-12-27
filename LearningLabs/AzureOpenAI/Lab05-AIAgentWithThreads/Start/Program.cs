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
HashSet<int> scenariosToRun = [1, 2];
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

    // TODO 1: Create an InMemoryVectorStore instance
    // Hint: var inMemoryVectorStore = new InMemoryVectorStore();

    // TODO 2: Get the agent settings from configuration using ConfigurationHelper.GetAgent("GlobalAgent")

    // TODO 3: Create ChatClientAgentOptions with:
    //   - Instructions from agentSettings
    //   - Name from agentSettings
    //   - ChatMessageStoreFactory that creates an InMemoryVectorChatMessageStore
    // Hint: Use a lambda: ctx => new InMemoryVectorChatMessageStore(inMemoryVectorStore, ctx.SerializedState, ctx.JsonSerializerOptions)

    // TODO 4: Create the AI Agent using chatClient.CreateAIAgent(agentOptions)

    // TODO 5: Call RunThreadConversationTestAsync(agent) to test the thread conversation
}
#endregion

#region Scenario 2: Create an AI Agent with Threads using the MongoDB vector store

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    Console.WriteLine("=== Scenario 2: Create an AI Agent with Threads using the MongoDB vector store ===");

    // TODO 6: Create a MongoVectorStore instance using ConfigurationHelper.GetMongoDatabase()
    // Hint: var mongoVectorStore = new MongoVectorStore(ConfigurationHelper.GetMongoDatabase());

    // TODO 7: Get the agent settings from configuration using ConfigurationHelper.GetAgent("GlobalAgent")

    // TODO 8: Create ChatClientAgentOptions with:
    //   - Instructions from agentSettings
    //   - Name from agentSettings
    //   - ChatMessageStoreFactory that creates a MongoVectorChatMessageStore
    // Hint: Use a lambda: ctx => new MongoVectorChatMessageStore(mongoVectorStore, ctx.SerializedState, ctx.JsonSerializerOptions)

    // TODO 9: Create the AI Agent using chatClient.CreateAIAgent(agentOptions)

    // TODO 10: Call RunThreadConversationTestAsync(agent) to test the thread conversation
}
#endregion

#region Helper Methods

/// <summary>
/// Runs a complete conversation test: asks a question, extracts thread ID, restores thread, and asks a follow-up.
/// This method demonstrates thread persistence by:
/// 1. Creating a new thread and asking a question
/// 2. Extracting the thread ID from the serialized state
/// 3. Restoring the thread using the extracted ID
/// 4. Asking a follow-up question that relies on the previous context
/// </summary>
async Task RunThreadConversationTestAsync(AIAgent agent)
{
    // TODO 11: Create a new thread using agent.GetNewThread()

    // TODO 12: Call AskQuestionAsync to ask the first question:
    //   "Hello, what is the capital of France?" with label "First Question: What is the capital of France?"

    // TODO 13: Call ExtractAndDisplayThreadId to extract the thread ID from the thread

    // TODO 14: Call RestoreThreadFromId to restore the thread from the extracted ID

    // TODO 15: Call AskQuestionAsync with the restored thread to ask a follow-up question:
    //   "What is the population of that city?" with label "Follow-up Question: What is the population of that city?"
    //   Store the response in a variable for token usage display

    // TODO 16: Call DisplayTokenUsage to display the token usage from the follow-up response

    throw new NotImplementedException("Complete the TODOs above");
}

/// <summary>
/// Asks a question to the agent and displays the response.
/// </summary>
async Task<AgentRunResponse> AskQuestionAsync(AIAgent agent, string question, AgentThread thread, string questionLabel)
{
    // TODO 17: Display the question label using ColoredConsole.WritePrimaryLogLine

    // TODO 18: Run the agent with the question using agent.RunAsync(question, thread)

    // TODO 19: Display the response using ColoredConsole.WriteSecondaryLogLine

    // TODO 20: Return the response

    throw new NotImplementedException("Complete the TODOs above");
}

/// <summary>
/// Extracts the thread ID from the thread's serialized state and displays it.
/// </summary>
string? ExtractAndDisplayThreadId(AgentThread thread)
{
    // TODO 21: Serialize the thread state using thread.Serialize()

    // TODO 22: Extract the thread ID using ExtractThreadIdFromState

    // TODO 23: Display the extracted thread ID using ColoredConsole.WriteInfoLine

    // TODO 24: Return the extracted thread ID

    throw new NotImplementedException("Complete the TODOs above");
}

/// <summary>
/// Restores a thread from a thread ID using the agent's DeserializeThread method.
/// </summary>
AgentThread RestoreThreadFromId(AIAgent agent, string? threadId)
{
    // TODO 25: Display a divider and info message about restoring the thread

    // TODO 26: Create an AgentThreadState with the threadId as StoreState

    // TODO 27: Serialize the AgentThreadState to a JsonElement using JsonSerializer.SerializeToElement

    // TODO 28: Deserialize the thread using agent.DeserializeThread and return it

    throw new NotImplementedException("Complete the TODOs above");
}

/// <summary>
/// Displays the token usage from an agent response.
/// </summary>
void DisplayTokenUsage(AgentRunResponse response, string label)
{
    // TODO 29: Display a divider line

    // TODO 30: Display the label using ColoredConsole.WritePrimaryLogLine

    // TODO 31: Display input tokens, output tokens, and total tokens using ColoredConsole.WriteSecondaryLogLine

    throw new NotImplementedException("Complete the TODOs above");
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
