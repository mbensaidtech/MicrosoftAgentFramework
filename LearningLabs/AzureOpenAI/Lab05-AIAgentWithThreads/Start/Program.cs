using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using Azure.Identity;
using Microsoft.Agents.AI;
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
// dotnet run                          -> the implemented scenarios (a scenario whose code is still the delivered one is left out)
// dotnet run -- --scenarios 2         -> scenario 2 only (and the scenarios it requires)
// dotnet run -- --scenarios 1,3       -> scenarios 1 and 3
// dotnet run -- --scenarios all       -> every scenario, implemented or not
// dotnet run -- --list-scenarios      -> the scenarios of this lab and their state
// Scenario 3 needs MongoDB (see the README).
ScenarioSelection scenarioSelection = ScenarioSelection.FromArgs(args);
// ============================================

bool ShouldRunScenario(int scenario) => scenarioSelection.ShouldRun(scenario);

// The same agent and the same conversation in every scenario: only where the chat history is kept, and how much of it, changes.
const string AgentName = "GlobalAgent";
const string AgentInstructions = "You are a global agent that can answer questions about any topic. The response should be very short and concise.";
const string FirstQuestion = "Hello, my name is Ada. What is the capital of France?";
const string IntermediateQuestion = "And what is the capital of Italy?";
const string FollowUpQuestion = "What is the population of that city? And what is my name?";

#region Setup: Configuration and Client Initialization

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
Console.WriteLine($"Endpoint: {settings.Endpoint}");
Console.WriteLine($"Deployment: {settings.ChatDeploymentName}");

// The conversation of every scenario: the same agent, the same questions
ColoredConsole.WritePrimaryLogLine($"{AgentName} instructions: {AgentInstructions}");
ColoredConsole.WriteSecondaryLogLine($"Questions: \"{FirstQuestion}\", then \"{FollowUpQuestion}\" (scenario 2 asks \"{IntermediateQuestion}\" in between)");

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

#region Scenario 2: Bound the chat history - keep only the last messages

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Bound the chat history with a chat reducer ===");

    // TODO 10: Create the agent with AsAIAgent(new ChatClientAgentOptions { ... }):
    //   - Name = AgentName and ChatOptions = new() { Instructions = AgentInstructions }
    //   - ChatHistoryProvider = new InMemoryChatHistoryProvider(new InMemoryChatHistoryProviderOptions
    //         { ChatReducer = new AIExtensions.MessageCountingChatReducer(2) })
    // MessageCountingChatReducer is flagged experimental (MEAI001): keep the pragma around it.
#pragma warning disable MEAI001
    // AIAgent agent = ...
#pragma warning restore MEAI001

    // TODO 11: Get the InMemoryChatHistoryProvider of the agent with agent.GetService<...>() and create a session

    // TODO 12: Ask FirstQuestion in the session, then display the turn:
    //          WriteTurn(FirstQuestion, response, chatHistory.GetMessages(session).Count)

    // TODO 13: Ask IntermediateQuestion in the same session and display the turn (same as TODO 12)

    // TODO 14: Ask FollowUpQuestion in the same session and display the turn: does the agent still know the city? the name?

    // TODO 15: Display the token usage of the last run with WriteTokenUsage(response)
}

#endregion

#region Scenario 3: Custom ChatHistoryProvider - chat history in MongoDB

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: Custom ChatHistoryProvider - chat history in MongoDB ===");

    // TODO 16: Connect to MongoDB with ConfigurationHelper.ConnectToMongoDbAsync() (start MongoDB first, see the README)

    // TODO 17: Write a function Func<IMongoDatabase, AIAgent> createAgent that creates the agent of TODO 10
    // with a new MongoChatHistoryProvider(mongoDatabase) as ChatHistoryProvider

    // TODO 18: Create the agent with createAgent(database), create a session and ask FirstQuestion

    // TODO 19: Serialize the session, display it and save it as text

    // TODO 20: Simulate a restart of the application: create a new agent with a new MongoDB connection
    // (createAgent(await ConfigurationHelper.ConnectToMongoDbAsync())) and restore the saved session with this new agent

    // TODO 21: Get the MongoChatHistoryProvider of the new agent and display the key of the restored session

    // TODO 22: Ask FollowUpQuestion to the new agent in the restored session, display the answer and the token usage
}

#endregion
