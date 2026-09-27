using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using Azure.Identity;
using CommunityToolkit.VectorData.InMemory;
using Microsoft.Agents.AI;
using Microsoft.Extensions.VectorData;
using MongoDB.Driver;
using OpenAI;
using OpenAI.Chat;
using CommonUtilities;
using AIExtensions = Microsoft.Extensions.AI;

using AIAgentWithThreads;
using AIAgentWithThreads.Stores;
using static AIAgentWithThreads.SessionConsole;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// Set to: [1], [2], [3] or [1, 2, 3] to run specific scenarios (scenario 3 needs MongoDB, see the README)
HashSet<int> scenariosToRun = [1, 2, 3];
// ============================================

bool ShouldRunScenario(int scenario) => scenariosToRun.Count == 0 || scenariosToRun.Contains(scenario);

// The same agent and the same conversation in every scenario: only the storage of the chat history changes.
const string AgentName = "GlobalAgent";
const string AgentInstructions = "You are a global agent that can answer questions about any topic. The response should be very short and concise.";
const string FirstQuestion = "Hello, my name is Ada. What is the capital of France?";
const string FollowUpQuestion = "What is the population of that city? And what is my name?";

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
// ChatClient chatClient = ...

#endregion

#region Scenario 1: Session with the default in-memory chat history

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: Session with the default in-memory chat history ===");

    // TODO 4: Create the agent with AsAIAgent(instructions: AgentInstructions, name: AgentName)
    // Without a ChatHistoryProvider, the agent keeps the chat history in memory, inside the session
    // AIAgent agent = ...

    // TODO 5: Create a session with agent.CreateSessionAsync(), then ask FirstQuestion in this session
    // Display "User: ..." before the run and "Agent: ..." (the Text of the response) after it

    // TODO 6: Get the InMemoryChatHistoryProvider of the agent with agent.GetService<...>()
    // and display the number of messages it keeps for the session (GetMessages(session)) and their roles

    // TODO 7: Serialize the session with agent.SerializeSessionAsync(session) and display it with WriteSerializedSession

    // TODO 8: Save the serialized session as text (JsonSerializer.Serialize), then restore it:
    // agent.DeserializeSessionAsync(JsonSerializer.Deserialize<JsonElement>(savedSession))

    // TODO 9: Ask FollowUpQuestion in the restored session, display the answer, then WriteTokenUsage(followUpResponse)
}

#endregion

#region Scenario 2: Custom ChatHistoryProvider - chat history in a vector store

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Custom ChatHistoryProvider - chat history in a vector store ===");

    // TODO 10: Create an InMemoryVectorStore (type it as VectorStore)

    // TODO 11: Create the agent with AsAIAgent(new ChatClientAgentOptions { ... }):
    //   - Name = AgentName
    //   - ChatOptions = new() { Instructions = AgentInstructions }
    //   - ChatHistoryProvider = new VectorChatHistoryProvider(vectorStore)

    // TODO 12: Create a session and ask FirstQuestion (same as TODO 5)

    // TODO 13: Get the VectorChatHistoryProvider with agent.GetService<...>()
    // and display the key under which the messages are stored: GetSessionDbKey(session)

    // TODO 14: Serialize the session, display it, save it as text and restore it (same as TODO 7 and TODO 8)
    // The JSON now only contains the key: the messages stay in the vector store

    // TODO 15: Ask FollowUpQuestion in the restored session, display the answer and the token usage (same as TODO 9)
}

#endregion

#region Scenario 3: Custom ChatHistoryProvider - chat history in MongoDB

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: Custom ChatHistoryProvider - chat history in MongoDB ===");

    // TODO 16: Connect to MongoDB with ConfigurationHelper.ConnectToMongoDbAsync() (start MongoDB first, see the README)

    // TODO 17: Write a function Func<IMongoDatabase, AIAgent> createAgent that creates the agent of TODO 11
    // with a new MongoChatHistoryProvider(mongoDatabase) as ChatHistoryProvider

    // TODO 18: Create the agent with createAgent(database), create a session and ask FirstQuestion

    // TODO 19: Serialize the session, display it and save it as text

    // TODO 20: Simulate a restart of the application: create a new agent with a new MongoDB connection
    // (createAgent(await ConfigurationHelper.ConnectToMongoDbAsync())) and restore the saved session with this new agent

    // TODO 21: Get the MongoChatHistoryProvider of the new agent and display the key of the restored session

    // TODO 22: Ask FollowUpQuestion to the new agent in the restored session, display the answer and the token usage
}

#endregion
