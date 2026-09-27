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

// Step 2: Create an OpenAIClient that targets the Azure OpenAI v1 endpoint (https://<resource>.openai.azure.com/openai/v1/)
// - API key when one is configured
// - otherwise Microsoft Entra ID: DefaultAzureCredential + BearerTokenPolicy (scope https://ai.azure.com/.default)
// WARNING: DefaultAzureCredential is convenient for development. In production, prefer a specific credential
// (e.g. ManagedIdentityCredential) to avoid latency and unintended credential probing.
OpenAIClientOptions clientOptions = new() { Endpoint = AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint) };

// The OpenAIClient(AuthenticationPolicy, ...) constructor is still flagged [Experimental] (OPENAI001) by the OpenAI SDK,
// although it is the pattern documented by Microsoft for Entra ID with the Azure OpenAI v1 API.
#pragma warning disable OPENAI001
OpenAIClient client = !string.IsNullOrWhiteSpace(settings.APIKey)
    ? new OpenAIClient(new ApiKeyCredential(settings.APIKey), clientOptions)
    : new OpenAIClient(new BearerTokenPolicy(new DefaultAzureCredential(), "https://ai.azure.com/.default"), clientOptions);
#pragma warning restore OPENAI001

// Step 3: Get a ChatClient (Chat Completions API) for the deployment.
// With Chat Completions, the service keeps no history: the agent sends it again at each run, from the session.
ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);

#endregion

#region Scenario 1: Session with the default in-memory chat history

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: Session with the default in-memory chat history ===");

    // Step 1: Create the agent. Without a ChatHistoryProvider, it uses an InMemoryChatHistoryProvider
    AIAgent agent = chatClient.AsAIAgent(instructions: AgentInstructions, name: AgentName);

    // Step 2: Create a session and ask the first question in this session
    AgentSession session = await agent.CreateSessionAsync();
    ColoredConsole.WritePrimaryLogLine($"User: {FirstQuestion}");
    AgentResponse firstResponse = await agent.RunAsync(FirstQuestion, session).WithSpinner("Running agent");
    ColoredConsole.WriteSecondaryLogLine($"Agent: {firstResponse.Text}");

    // Step 3: Look at the chat history: the InMemoryChatHistoryProvider keeps it inside the session
    List<AIExtensions.ChatMessage> messages = agent.GetService<InMemoryChatHistoryProvider>()!.GetMessages(session);
    ColoredConsole.WriteInfoLine($"Messages in the session: {messages.Count} ({string.Join(", ", messages.Select(message => message.Role))})");

    // Step 4: Serialize the session: the JSON contains the whole conversation
    JsonElement serializedSession = await agent.SerializeSessionAsync(session);
    WriteSerializedSession(serializedSession);

    // Step 5: Save the session as text (a file, a database...), then restore it
    string savedSession = JsonSerializer.Serialize(serializedSession);
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("Restoring the session from the saved JSON...");
    AgentSession resumedSession = await agent.DeserializeSessionAsync(JsonSerializer.Deserialize<JsonElement>(savedSession));

    // Step 6: Ask a follow-up question in the restored session: "that city" and "my name" come from the history
    ColoredConsole.WritePrimaryLogLine($"User: {FollowUpQuestion}");
    AgentResponse followUpResponse = await agent.RunAsync(FollowUpQuestion, resumedSession).WithSpinner("Running agent");
    ColoredConsole.WriteSecondaryLogLine($"Agent: {followUpResponse.Text}");

    // Step 7: Display token usage
    WriteTokenUsage(followUpResponse);
}

#endregion

#region Scenario 2: Custom ChatHistoryProvider - chat history in a vector store

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Custom ChatHistoryProvider - chat history in a vector store ===");

    // Step 1: Create the vector store that receives the messages (in memory: lost when the application stops)
    VectorStore vectorStore = new InMemoryVectorStore();

    // Step 2: Create the agent with ChatClientAgentOptions and the custom ChatHistoryProvider
    AIAgent agent = chatClient.AsAIAgent(new ChatClientAgentOptions
    {
        Name = AgentName,
        ChatOptions = new() { Instructions = AgentInstructions },
        ChatHistoryProvider = new VectorChatHistoryProvider(vectorStore)
    });

    // Step 3: Create a session and ask the first question
    AgentSession session = await agent.CreateSessionAsync();
    ColoredConsole.WritePrimaryLogLine($"User: {FirstQuestion}");
    AgentResponse firstResponse = await agent.RunAsync(FirstQuestion, session).WithSpinner("Running agent");
    ColoredConsole.WriteSecondaryLogLine($"Agent: {firstResponse.Text}");

    // Step 4: Get the provider from the agent and display the key under which the messages are stored
    VectorChatHistoryProvider chatHistoryProvider = agent.GetService<VectorChatHistoryProvider>()!;
    ColoredConsole.WriteInfoLine($"Chat history stored in the vector store under the key: {chatHistoryProvider.GetSessionDbKey(session)}");

    // Step 5: Serialize the session: the JSON only contains the key, the messages stay in the vector store
    JsonElement serializedSession = await agent.SerializeSessionAsync(session);
    WriteSerializedSession(serializedSession);

    // Step 6: Save the session as text, then restore it
    string savedSession = JsonSerializer.Serialize(serializedSession);
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("Restoring the session from the saved JSON...");
    AgentSession resumedSession = await agent.DeserializeSessionAsync(JsonSerializer.Deserialize<JsonElement>(savedSession));

    // Step 7: Ask the follow-up question: the provider reloads the history from the vector store
    ColoredConsole.WritePrimaryLogLine($"User: {FollowUpQuestion}");
    AgentResponse followUpResponse = await agent.RunAsync(FollowUpQuestion, resumedSession).WithSpinner("Running agent");
    ColoredConsole.WriteSecondaryLogLine($"Agent: {followUpResponse.Text}");

    // Step 8: Display token usage
    WriteTokenUsage(followUpResponse);
}

#endregion

#region Scenario 3: Custom ChatHistoryProvider - chat history in MongoDB

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: Custom ChatHistoryProvider - chat history in MongoDB ===");

    // Step 1: Connect to MongoDB (started with docker compose, see the README)
    IMongoDatabase database = await ConfigurationHelper.ConnectToMongoDbAsync();

    // Step 2: Write a function that creates the agent with a MongoChatHistoryProvider on a database
    Func<IMongoDatabase, AIAgent> createAgent = mongoDatabase => chatClient.AsAIAgent(new ChatClientAgentOptions
    {
        Name = AgentName,
        ChatOptions = new() { Instructions = AgentInstructions },
        ChatHistoryProvider = new MongoChatHistoryProvider(mongoDatabase)
    });

    // Step 3: Create the agent and a session, and ask the first question
    AIAgent agent = createAgent(database);
    AgentSession session = await agent.CreateSessionAsync();
    ColoredConsole.WritePrimaryLogLine($"User: {FirstQuestion}");
    AgentResponse firstResponse = await agent.RunAsync(FirstQuestion, session).WithSpinner("Running agent");
    ColoredConsole.WriteSecondaryLogLine($"Agent: {firstResponse.Text}");

    // Step 4: Serialize the session and save it as text
    JsonElement serializedSession = await agent.SerializeSessionAsync(session);
    WriteSerializedSession(serializedSession);
    string savedSession = JsonSerializer.Serialize(serializedSession);

    // Step 5: Simulate a restart of the application: new MongoDB connection, new provider, new agent.
    // Nothing is shared in memory with the first agent: only the saved JSON and the documents in MongoDB.
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("Simulating a restart: new agent, new MongoDB connection, session restored from the saved JSON...");
    AIAgent restartedAgent = createAgent(await ConfigurationHelper.ConnectToMongoDbAsync());
    AgentSession resumedSession = await restartedAgent.DeserializeSessionAsync(JsonSerializer.Deserialize<JsonElement>(savedSession));

    // Step 6: Display the key of the restored session: the key of the documents in the chat_history collection
    MongoChatHistoryProvider chatHistoryProvider = restartedAgent.GetService<MongoChatHistoryProvider>()!;
    ColoredConsole.WriteInfoLine($"Restored session - chat history stored in MongoDB under the key: {chatHistoryProvider.GetSessionDbKey(resumedSession)}");

    // Step 7: Ask the follow-up question to the new agent: the history comes from MongoDB
    ColoredConsole.WritePrimaryLogLine($"User: {FollowUpQuestion}");
    AgentResponse followUpResponse = await restartedAgent.RunAsync(FollowUpQuestion, resumedSession).WithSpinner("Running agent");
    ColoredConsole.WriteSecondaryLogLine($"Agent: {followUpResponse.Text}");

    // Step 8: Display token usage
    WriteTokenUsage(followUpResponse);
}

#endregion
