using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using Azure.Identity;
using Microsoft.Agents.AI;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;
using CommonUtilities;
using AIExtensions = Microsoft.Extensions.AI;

using AIAgentWithSessions;
using AIAgentWithSessions.Stores;
using static AIAgentWithSessions.SessionConsole;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// dotnet run                          -> the implemented scenarios (a scenario whose code is still the delivered one is left out)
// dotnet run -- --scenarios 2         -> scenario 2 only (and the scenarios it requires)
// dotnet run -- --scenarios 1,3       -> scenarios 1 and 3
// dotnet run -- --list-scenarios      -> the scenarios of this lab and their state
// Scenarios 3 and 5 need a storage for the chat history: MongoDB with Docker, MongoDB without Docker, or a JSON file (see the README).
// Scenarios 4 and 5 use the Responses API: the deployment must support it.
ScenarioSelection scenarioSelection = ScenarioSelection.FromArgs(args);
// ============================================

bool ShouldRunScenario(int scenario) => scenarioSelection.ShouldRun(scenario);

#region Setup: Configuration and Client Initialization

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
Console.WriteLine($"Endpoint: {settings.Endpoint}");
Console.WriteLine($"Deployment: {settings.ChatDeploymentName}");

// The conversation of every scenario: the same agent, the same questions
ColoredConsole.WritePrimaryLogLine($"{Agents.Global.Name} instructions: {Agents.Global.Instructions}");
ColoredConsole.WriteSecondaryLogLine($"Questions: \"{Questions.First}\", then \"{Questions.FollowUp}\" (scenario 2 asks \"{Questions.Intermediate}\" in between)");

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
    AIAgent agent = chatClient.AsAIAgent(instructions: Agents.Global.Instructions, name: Agents.Global.Name);

    // Step 2: Create a session and ask the first question in this session
    AgentSession session = await agent.CreateSessionAsync();
    ColoredConsole.WritePrimaryLogLine($"User: {Questions.First}");
    AgentResponse firstResponse = await agent.RunAsync(Questions.First, session).WithSpinner("Running agent");
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
    ColoredConsole.WritePrimaryLogLine($"User: {Questions.FollowUp}");
    AgentResponse followUpResponse = await agent.RunAsync(Questions.FollowUp, resumedSession).WithSpinner("Running agent");
    ColoredConsole.WriteSecondaryLogLine($"Agent: {followUpResponse.Text}");

    // Step 7: Display token usage
    WriteTokenUsage(followUpResponse);
}

#endregion

#region Scenario 2: Bound the chat history - keep only the last messages

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Bound the chat history with a chat reducer ===");

    // Step 1: Create the agent with an InMemoryChatHistoryProvider that keeps only the last 2 messages of the conversation.
    // Without a limit, every run sends back the whole history: the cost grows with each turn, until the context window is full.
    // MessageCountingChatReducer keeps the most recent messages (it drops old tool calls and results with their turn).
    // It is still flagged [Experimental] (MEAI001) in Microsoft.Extensions.AI.
#pragma warning disable MEAI001
    AIAgent agent = chatClient.AsAIAgent(new ChatClientAgentOptions
    {
        Name = Agents.Global.Name,
        ChatOptions = new() { Instructions = Agents.Global.Instructions },
        ChatHistoryProvider = new InMemoryChatHistoryProvider(new InMemoryChatHistoryProviderOptions
        {
            ChatReducer = new AIExtensions.MessageCountingChatReducer(2)
        })
    });
#pragma warning restore MEAI001

    // Step 2: Get the provider (to read the stored history) and create a session
    InMemoryChatHistoryProvider chatHistory = agent.GetService<InMemoryChatHistoryProvider>()!;
    AgentSession session = await agent.CreateSessionAsync();

    // Step 3: Two turns in the same session; after each one, look at how many messages the session keeps.
    // The reducer runs before the history is given to the next run: the session briefly holds 4 messages, the model gets 2 of them.
    AgentResponse response = await agent.RunAsync(Questions.First, session).WithSpinner("Running agent");
    WriteTurn(Questions.First, response, chatHistory.GetMessages(session).Count);

    response = await agent.RunAsync(Questions.Intermediate, session).WithSpinner("Running agent");
    WriteTurn(Questions.Intermediate, response, chatHistory.GetMessages(session).Count);

    // Step 4: The follow-up question: "that city" is in the last turn (kept), "my name" was in the first one (dropped)
    response = await agent.RunAsync(Questions.FollowUp, session).WithSpinner("Running agent");
    WriteTurn(Questions.FollowUp, response, chatHistory.GetMessages(session).Count);

    // Step 5: Display token usage
    WriteTokenUsage(response);
}

#endregion

#region Scenario 3: Custom ChatHistoryProvider - chat history outside of the application

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: Custom ChatHistoryProvider - chat history outside of the application ===");

    // Step 1: Open the storage of the chat history chosen by Storage:Mode (MongoDB with or without Docker, or a JSON file, see the README)
    IChatHistoryStore store = await ConfigurationHelper.CreateChatHistoryStoreAsync();

    // Step 2: Write a function that creates the agent with a PersistentChatHistoryProvider on a store
    Func<IChatHistoryStore, AIAgent> createAgent = chatHistoryStore => chatClient.AsAIAgent(new ChatClientAgentOptions
    {
        Name = Agents.Global.Name,
        ChatOptions = new() { Instructions = Agents.Global.Instructions },
        ChatHistoryProvider = new PersistentChatHistoryProvider(chatHistoryStore)
    });

    // Step 3: Create the agent and a session, and ask the first question
    AIAgent agent = createAgent(store);
    AgentSession session = await agent.CreateSessionAsync();
    ColoredConsole.WritePrimaryLogLine($"User: {Questions.First}");
    AgentResponse firstResponse = await agent.RunAsync(Questions.First, session).WithSpinner("Running agent");
    ColoredConsole.WriteSecondaryLogLine($"Agent: {firstResponse.Text}");

    // Step 4: Serialize the session and save it as text
    JsonElement serializedSession = await agent.SerializeSessionAsync(session);
    WriteSerializedSession(serializedSession);
    string savedSession = JsonSerializer.Serialize(serializedSession);

    // Step 5: Simulate a restart of the application: new connection to the storage, new provider, new agent.
    // Nothing is shared in memory with the first agent: only the saved JSON and the stored messages.
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("Simulating a restart: new agent, new connection to the storage, session restored from the saved JSON...");
    AIAgent restartedAgent = createAgent(await ConfigurationHelper.CreateChatHistoryStoreAsync());
    AgentSession resumedSession = await restartedAgent.DeserializeSessionAsync(JsonSerializer.Deserialize<JsonElement>(savedSession));

    // Step 6: Display the key of the restored session: the key of the stored messages (field SessionId)
    PersistentChatHistoryProvider chatHistoryProvider = restartedAgent.GetService<PersistentChatHistoryProvider>()!;
    ColoredConsole.WriteInfoLine($"Restored session - chat history stored under the key: {chatHistoryProvider.GetSessionDbKey(resumedSession)}");

    // Step 7: Ask the follow-up question to the new agent: the history comes from the storage
    ColoredConsole.WritePrimaryLogLine($"User: {Questions.FollowUp}");
    AgentResponse followUpResponse = await restartedAgent.RunAsync(Questions.FollowUp, resumedSession).WithSpinner("Running agent");
    ColoredConsole.WriteSecondaryLogLine($"Agent: {followUpResponse.Text}");

    // Step 8: Display token usage
    WriteTokenUsage(followUpResponse);
}

#endregion

#region Scenario 4: Responses API - the service keeps the conversation
// Bonus scenario (optional): the lab is completed without it; with every bonus scenario, it is completed with its bonus (Done+).

if (ShouldRunScenario(4))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 4: Responses API - the service keeps the conversation ===");

    // Step 1: Create the same agent on the Responses API, from the same OpenAIClient (with Responses, the deployment is the model
    // of each request). By default, the Responses API stores every response (store = true) for 30 days.
    // ResponsesClient is still flagged [Experimental] (OPENAI001) by the OpenAI SDK.
#pragma warning disable OPENAI001
    ResponsesClient responsesClient = client.GetResponsesClient();
    AIAgent agent = responsesClient.AsAIAgent(model: settings.ChatDeploymentName, instructions: Agents.Global.Instructions, name: Agents.Global.Name);
#pragma warning restore OPENAI001

    // Step 2: Create a session and ask the first question: the same code as scenario 1
    AgentSession session = await agent.CreateSessionAsync();
    ColoredConsole.WritePrimaryLogLine($"User: {Questions.First}");
    AgentResponse firstResponse = await agent.RunAsync(Questions.First, session).WithSpinner("Running agent");
    ColoredConsole.WriteSecondaryLogLine($"Agent: {firstResponse.Text}");

    // Step 3: Look at what the session keeps: no messages, only the id of the last response stored by the service.
    // ChatClientAgentSession is the session type of a ChatClientAgent; ConversationId is the id the service gave back.
    ColoredConsole.WriteInfoLine($"Conversation id kept in the session: {((ChatClientAgentSession)session).ConversationId}");

    // Step 4: Serialize the session, save it as text and restore it: the JSON only holds the id (the history is in the service)
    JsonElement serializedSession = await agent.SerializeSessionAsync(session);
    WriteSerializedSession(serializedSession);
    string savedSession = JsonSerializer.Serialize(serializedSession);
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("Restoring the session from the saved JSON...");
    AgentSession resumedSession = await agent.DeserializeSessionAsync(JsonSerializer.Deserialize<JsonElement>(savedSession));

    // Step 5: Ask the follow-up question: only the new message and the id are sent (previous_response_id),
    // the service brings back the history. The session now points to the new response: the id changes at each turn.
    ColoredConsole.WritePrimaryLogLine($"User: {Questions.FollowUp}");
    AgentResponse followUpResponse = await agent.RunAsync(Questions.FollowUp, resumedSession).WithSpinner("Running agent");
    ColoredConsole.WriteSecondaryLogLine($"Agent: {followUpResponse.Text}");
    ColoredConsole.WriteInfoLine($"Conversation id kept in the session after the follow-up: {((ChatClientAgentSession)resumedSession).ConversationId}");

    // Step 6: Check where the conversation lives: the service keeps the response (GET /responses/{id} succeeds)
    await WriteServiceStorageAsync(responsesClient, followUpResponse.ResponseId);

    // Step 7: Display token usage: the history was not sent, but it is still read by the model, and billed
    WriteTokenUsage(followUpResponse);

    // Step 8: The conversation is data kept by the provider: to erase it, you ask the provider.
    // Delete the two stored responses (otherwise they are kept 30 days); the saved session can no longer be resumed.
#pragma warning disable OPENAI001
    await responsesClient.DeleteResponseAsync(firstResponse.ResponseId);
    await responsesClient.DeleteResponseAsync(followUpResponse.ResponseId);
#pragma warning restore OPENAI001
    ColoredConsole.WriteInfoLine("Deleted the 2 responses stored by the service for this conversation");
}

#endregion

#region Scenario 5: Responses API without storage - the conversation stays in your application
// Bonus scenario (optional): the lab is completed without it; with every bonus scenario, it is completed with its bonus (Done+).

if (ShouldRunScenario(5))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 5: Responses API without storage - the conversation stays in your application ===");

    // Step 1: Open the storage of the chat history: the same store as scenario 3 (MongoDB or a JSON file, chosen by Storage:Mode)
    IChatHistoryStore store = await ConfigurationHelper.CreateChatHistoryStoreAsync();

    // Step 2: Get a chat client on the Responses API that asks the service NOT to store anything (store = false).
    // Without it, the service returns a conversation id and the agent refuses a ChatHistoryProvider
    // ("Only ConversationId or ChatHistoryProvider may be used, but not both").
    // ResponsesClient is flagged [Experimental] by the OpenAI SDK (OPENAI001), AsIChatClientWithStoredOutputDisabled by Agent Framework (MAAI001).
#pragma warning disable OPENAI001, MAAI001
    ResponsesClient responsesClient = client.GetResponsesClient();
    AIExtensions.IChatClient statelessResponsesClient = responsesClient.AsIChatClientWithStoredOutputDisabled(settings.ChatDeploymentName);
#pragma warning restore OPENAI001, MAAI001

    // Step 3: The same agent as scenario 3, with the PersistentChatHistoryProvider: only the chat client under the agent changes.
    // new ChatClientAgent(chatClient, options) is what AsAIAgent(options) does for you on an OpenAI client.
    Func<IChatHistoryStore, AIAgent> createAgent = chatHistoryStore => new ChatClientAgent(statelessResponsesClient, new ChatClientAgentOptions
    {
        Name = Agents.Global.Name,
        ChatOptions = new() { Instructions = Agents.Global.Instructions },
        ChatHistoryProvider = new PersistentChatHistoryProvider(chatHistoryStore)
    });

    // Step 4: Your application keeps its own table of conversations, keyed by its business id.
    // A dictionary stands for it here; in a real application it is a table of your database, bound to the authenticated user.
    string businessConversationId = BusinessIds.SupportCase;
    Dictionary<string, string> savedSessions = [];

    // Step 5: Create the agent and a session, and ask the first question
    AIAgent agent = createAgent(store);
    AgentSession session = await agent.CreateSessionAsync();
    ColoredConsole.WritePrimaryLogLine($"User: {Questions.First}");
    AgentResponse firstResponse = await agent.RunAsync(Questions.First, session).WithSpinner("Running agent");
    ColoredConsole.WriteSecondaryLogLine($"Agent: {firstResponse.Text}");

    // Step 6: Check where the conversation lives: the service gave a response id, but kept nothing (GET /responses/{id} returns 404)
    await WriteServiceStorageAsync(responsesClient, firstResponse.ResponseId);

    // Step 7: Save the serialized session under the business id: it holds the key of the chat history, no service id
    JsonElement serializedSession = await agent.SerializeSessionAsync(session);
    WriteSerializedSession(serializedSession);
    savedSessions[businessConversationId] = JsonSerializer.Serialize(serializedSession);

    // Step 8: Simulate a restart: new agent, new connection to the storage, session found with the business id
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine($"Simulating a restart: new agent, new connection to the storage, session of '{businessConversationId}' restored...");
    AIAgent restartedAgent = createAgent(await ConfigurationHelper.CreateChatHistoryStoreAsync());
    AgentSession resumedSession = await restartedAgent.DeserializeSessionAsync(JsonSerializer.Deserialize<JsonElement>(savedSessions[businessConversationId]));

    // Step 9: Ask the follow-up question: the provider loads the history from your storage and the agent sends it to the service
    ColoredConsole.WritePrimaryLogLine($"User: {Questions.FollowUp}");
    AgentResponse followUpResponse = await restartedAgent.RunAsync(Questions.FollowUp, resumedSession).WithSpinner("Running agent");
    ColoredConsole.WriteSecondaryLogLine($"Agent: {followUpResponse.Text}");

    // Step 10: Display the identifiers of the conversation (your id, the session and its key, the ids of the service)
    string chatHistoryKey = restartedAgent.GetService<PersistentChatHistoryProvider>()!.GetSessionDbKey(resumedSession);
    WriteIdentifiers(businessConversationId, chatHistoryKey, followUpResponse.ResponseId, ((ChatClientAgentSession)resumedSession).ConversationId);

    // Step 11: Display token usage: the history comes from your storage and is sent with the request
    WriteTokenUsage(followUpResponse);
}

#endregion
