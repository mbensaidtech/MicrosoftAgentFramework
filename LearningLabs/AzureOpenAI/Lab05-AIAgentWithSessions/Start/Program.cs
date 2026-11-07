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

    // TODO 4: Create the agent with the instructions and the name of Agents.Global (provided in LabPrompts.cs)
    // Hint: chatClient.AsAIAgent(instructions: Agents.Global.Instructions, name: Agents.Global.Name)
    // Without a ChatHistoryProvider, the agent keeps the chat history in memory, inside the session
    // AIAgent agent = ...

    // TODO 5: Create a session with agent.CreateSessionAsync(), then ask Questions.First in this session
    // Display "User: ..." before the run and "Agent: ..." (the Text of the response) after it

    // TODO 6: Get the InMemoryChatHistoryProvider of the agent with agent.GetService<...>()
    // and display the number of messages it keeps for the session (GetMessages(session)) and their roles

    // TODO 7: Serialize the session with agent.SerializeSessionAsync(session) and display it with WriteSerializedSession

    // TODO 8: Save the serialized session as text (JsonSerializer.Serialize), then restore it:
    // agent.DeserializeSessionAsync(JsonSerializer.Deserialize<JsonElement>(savedSession))

    // TODO 9: Ask Questions.FollowUp in the restored session, display the answer, then WriteTokenUsage(followUpResponse)
}

#endregion

#region Scenario 2: Bound the chat history - keep only the last messages

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Bound the chat history with a chat reducer ===");

    // TODO 10: Create the agent with AsAIAgent(new ChatClientAgentOptions { ... }):
    //   - Name = Agents.Global.Name and ChatOptions = new() { Instructions = Agents.Global.Instructions }
    //   - ChatHistoryProvider = new InMemoryChatHistoryProvider(new InMemoryChatHistoryProviderOptions
    //         { ChatReducer = new AIExtensions.MessageCountingChatReducer(2) })
    // MessageCountingChatReducer is flagged experimental (MEAI001): keep the pragma around it.
#pragma warning disable MEAI001
    // AIAgent agent = ...
#pragma warning restore MEAI001

    // TODO 11: Get the InMemoryChatHistoryProvider of the agent with agent.GetService<...>() and create a session

    // TODO 12: Ask Questions.First in the session, then display the turn:
    //          WriteTurn(Questions.First, response, chatHistory.GetMessages(session).Count)

    // TODO 13: Ask Questions.Intermediate in the same session and display the turn (same as TODO 12)

    // TODO 14: Ask Questions.FollowUp in the same session and display the turn: does the agent still know the city? the name?

    // TODO 15: Display the token usage of the last run with WriteTokenUsage(response)
}

#endregion

#region Scenario 3: Custom ChatHistoryProvider - chat history outside of the application

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: Custom ChatHistoryProvider - chat history outside of the application ===");

    // TODO 16: Open the storage of the chat history with ConfigurationHelper.CreateChatHistoryStoreAsync()
    // (MongoDB with or without Docker, or a JSON file: choose it with Storage:Mode, see the README)

    // TODO 17: Write a function Func<IChatHistoryStore, AIAgent> createAgent that creates the agent of TODO 10
    // with a new PersistentChatHistoryProvider(chatHistoryStore) as ChatHistoryProvider

    // TODO 18: Create the agent with createAgent(store), create a session and ask Questions.First

    // TODO 19: Serialize the session, display it and save it as text

    // TODO 20: Simulate a restart of the application: create a new agent with a new connection to the storage
    // (createAgent(await ConfigurationHelper.CreateChatHistoryStoreAsync())) and restore the saved session with this new agent

    // TODO 21: Get the PersistentChatHistoryProvider of the new agent and display the key of the restored session

    // TODO 22: Ask Questions.FollowUp to the new agent in the restored session, display the answer and the token usage
}

#endregion

#region Scenario 4: Responses API - the service keeps the conversation
// Bonus scenario (optional): the lab is completed without it; with every bonus scenario, it is completed with its bonus (Done+).

if (ShouldRunScenario(4))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 4: Responses API - the service keeps the conversation ===");

    // TODO 23: Get a ResponsesClient and create the same agent (Agents.Global) on the Responses API
    // Hint: ResponsesClient responsesClient = client.GetResponsesClient();
    //       AIAgent agent = responsesClient.AsAIAgent(model: settings.ChatDeploymentName, instructions: ..., name: ...);
    // With Responses, the deployment is the model of each request. By default, the service stores every response (store = true).
    // Note: GetResponsesClient() is flagged experimental (OPENAI001), keep the pragma around this code.
#pragma warning disable OPENAI001
    // ResponsesClient responsesClient = ...
    // AIAgent agent = ...
#pragma warning restore OPENAI001

    // TODO 24: Create a session and ask Questions.First in it (the same code as TODO 5)

    // TODO 25: Display the conversation id kept in the session
    // Hint: ColoredConsole.WriteInfoLine($"Conversation id kept in the session: {((ChatClientAgentSession)session).ConversationId}")
    // No messages are kept on your side: only the id of the last response stored by the service (resp_...)

    // TODO 26: Serialize the session, display it, save it as text and restore it (the same code as TODO 7 and TODO 8)

    // TODO 27: Ask Questions.FollowUp in the restored session, display the answer,
    // then the conversation id kept in the restored session after the follow-up (it changed: it is the id of the new response)
    // Hint: ColoredConsole.WriteInfoLine($"Conversation id kept in the session after the follow-up: {((ChatClientAgentSession)resumedSession).ConversationId}")

    // TODO 28: Check that the service keeps the conversation, then display the token usage
    // Hint: await WriteServiceStorageAsync(responsesClient, followUpResponse.ResponseId); then WriteTokenUsage(followUpResponse);

    // TODO 29: Delete the two responses stored by the service (otherwise they are kept 30 days)
    // Hint: await responsesClient.DeleteResponseAsync(firstResponse.ResponseId); and the same for followUpResponse (keep the OPENAI001 pragma),
    //       then ColoredConsole.WriteInfoLine("Deleted the 2 responses stored by the service for this conversation")
}

#endregion

#region Scenario 5: Responses API without storage - the conversation stays in your application
// Bonus scenario (optional): the lab is completed without it; with every bonus scenario, it is completed with its bonus (Done+).

if (ShouldRunScenario(5))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 5: Responses API without storage - the conversation stays in your application ===");

    // TODO 30: Open the storage of the chat history (the same call as TODO 16), then get a chat client on the Responses API
    // that asks the service not to store anything (store = false)
    // Hint: ResponsesClient responsesClient = client.GetResponsesClient();
    //       AIExtensions.IChatClient statelessResponsesClient = responsesClient.AsIChatClientWithStoredOutputDisabled(settings.ChatDeploymentName);
    // Note: AsIChatClientWithStoredOutputDisabled is flagged experimental by Agent Framework (MAAI001), keep the pragma around this code.
#pragma warning disable OPENAI001, MAAI001
    // ResponsesClient responsesClient = ...
    // AIExtensions.IChatClient statelessResponsesClient = ...
#pragma warning restore OPENAI001, MAAI001

    // TODO 31: Write the function that creates the agent of scenario 3 (PersistentChatHistoryProvider) on this chat client
    // Hint: Func<IChatHistoryStore, AIAgent> createAgent = chatHistoryStore => new ChatClientAgent(statelessResponsesClient, new ChatClientAgentOptions { ... });
    //       with the same Name, ChatOptions and ChatHistoryProvider as in TODO 17

    // TODO 32: Keep the business id of the conversation and your table of saved sessions, create the agent and a session,
    // ask Questions.First, then check that the service kept nothing
    // Hint: string businessConversationId = BusinessIds.SupportCase; Dictionary<string, string> savedSessions = [];
    //       then the same code as TODO 18, and await WriteServiceStorageAsync(responsesClient, firstResponse.ResponseId);

    // TODO 33: Serialize the session, display it and save it under the business id, then simulate a restart and restore it
    // Hint: savedSessions[businessConversationId] = JsonSerializer.Serialize(serializedSession);
    //       ColoredConsole.WriteInfoLine($"Simulating a restart: new agent, new connection to the storage, session of '{businessConversationId}' restored...");
    //       AIAgent restartedAgent = createAgent(await ConfigurationHelper.CreateChatHistoryStoreAsync());
    //       AgentSession resumedSession = await restartedAgent.DeserializeSessionAsync(JsonSerializer.Deserialize<JsonElement>(savedSessions[businessConversationId]));

    // TODO 34: Ask Questions.FollowUp to the new agent, display the answer, the identifiers of the conversation and the token usage
    // Hint: string chatHistoryKey = restartedAgent.GetService<PersistentChatHistoryProvider>()!.GetSessionDbKey(resumedSession);
    //       WriteIdentifiers(businessConversationId, chatHistoryKey, followUpResponse.ResponseId, ((ChatClientAgentSession)resumedSession).ConversationId);
    //       WriteTokenUsage(followUpResponse);
}

#endregion
