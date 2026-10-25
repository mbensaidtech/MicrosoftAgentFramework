using System.ClientModel;
using System.ClientModel.Primitives;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using MongoDB.Driver;
using OpenAI;
using OpenAI.Chat;
using CommonUtilities;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

using AIAgentWithAIContextProvider;
using static AIAgentWithAIContextProvider.MemoryConsole;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// Set to: [1], [2] or [1, 2] to run specific scenarios (scenario 2 is a chat: it reads your messages at the console)
HashSet<int> scenariosToRun = [1, 2];
// ============================================

bool ShouldRunScenario(int scenario) => scenariosToRun.Count == 0 || scenariosToRun.Contains(scenario);

// The user of this lab. Its memories are stored in MongoDB under this id.
// In a real application, it is the id of the signed-in user.
const string UserId = "ada";

// The agent that talks with the user, and the agent that the context provider uses to extract memories.
const string AgentName = "MemoryAssistant";
const string AgentInstructions =
    "You are a friendly personal assistant. Use what you remember about the user to personalize your answers. " +
    "When a conversation starts without a message from the user, introduce yourself and welcome the user, by name if you know it.";
const string ExtractorInstructions =
    "You extract facts about the user from the user message: name, job, city, interests, preferences, constraints. " +
    "Write each fact as a short sentence in the third person, for example: \"The user works as a nurse.\". " +
    "Do not extract a fact that is already known, nor questions or requests. Return an empty list when there is no new fact.";

// The conversation of scenario 1: the user introduces herself, then asks a question in a new session.
const string IntroductionMessage = "Hi! I'm Ada, a software developer from Lyon. I love hiking in the Alps and I'm vegetarian.";
const string RecallQuestion = "What is my name? Suggest a dinner idea and a weekend activity for me.";

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

#region Agent with a long-term memory (shared by both scenarios)

ColoredConsole.WriteDividerLine();
ColoredConsole.WriteInfoLine("Agent with a long-term memory - the memories of the user are stored in MongoDB");
ColoredConsole.WritePrimaryLogLine($"User id: {UserId}");
ColoredConsole.WritePrimaryLogLine($"{AgentName} instructions: {AgentInstructions}");
ColoredConsole.WritePrimaryLogLine($"Memory extractor instructions: {ExtractorInstructions}");

// TODO 4: Connect to MongoDB (start it first, see the README) and create the repository of the memories
// Hint: await ConfigurationHelper.ConnectToMongoDbAsync(), then new UserMemoriesRepository(database)
// IMongoDatabase database = ...
// UserMemoriesRepository memoriesRepository = ...

// TODO 5: Create the agent that extracts memories from a user message (the context provider will use it)
// Hint: chatClient.AsAIAgent(instructions: ExtractorInstructions, name: "MemoryExtractor")
// AIAgent memoryExtractor = ...

// TODO 6: Create the agent with the context provider (UserMemoryAIContextProvider, at the bottom of this file)
// Hint: chatClient.AsAIAgent(new ChatClientAgentOptions { ... }) with:
//   - Name = AgentName and ChatOptions = new() { Instructions = AgentInstructions }
//   - AIContextProviders = [new UserMemoryAIContextProvider(memoryExtractor, memoriesRepository,
//         stateInitializer: _ => new UserMemoryAIContextProvider.State(UserId))]
//   - ChatHistoryProvider = new InMemoryChatHistoryProvider(new InMemoryChatHistoryProviderOptions { StorageInputRequestMessageFilter = ... })
//         that keeps the messages added by the provider out of the chat history (AgentRequestMessageSourceType, see the README)
// AIAgent memoryAgent = ...

#endregion

#region Scenario 1: Remember the user from one session to another

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: Remember the user from one session to another ===");

    // TODO 7: Start from an empty memory: delete the memories of the user, and display how many were deleted
    // Hint: await memoriesRepository.DeleteMemoriesAsync(UserId)

    ColoredConsole.WriteInfoLine("First session");
    ColoredConsole.WriteUserLine($"User: {IntroductionMessage}");

    // TODO 8: Create a session, run the agent with IntroductionMessage in it and display the answer ("Agent: ...")
    // Before the model call, the provider adds the memories to the instructions; after it, it extracts and stores the new ones.

    // TODO 9: Display the memories stored in MongoDB for the user
    // Hint: WriteStoredMemories(UserId, await memoriesRepository.GetMemoriesAsync(UserId))

    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("New session (empty chat history)");
    ColoredConsole.WriteUserLine($"User: {RecallQuestion}");

    // TODO 10: Create a NEW session, run the agent with RecallQuestion in it, display the answer, then WriteTokenUsage(...)
    // The chat history of this session is empty: what the agent knows about the user comes from the provider.
}

#endregion

#region Scenario 2: Chat with the agent - it greets you with what it remembers

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Chat with the agent - it greets you with what it remembers ===");

    // TODO 11: Create a new session and let the agent start the conversation: RunAsync(session), without a message.
    // Display the greeting ("Agent: ...")

    ColoredConsole.WriteSecondaryLogLine("Type a message, or an empty line / 'exit' to end the chat.");

    // TODO 12: Chat in the same session until an empty message or "exit":
    // - Console.Write("You: ") then Console.ReadLine() (null when the input is closed)
    // - run the agent with the message in the session and display the answer

    ColoredConsole.WriteInfoLine("Chat ended. Run scenario 2 again: the agent still remembers what you told it (the memories are in MongoDB).");
}

#endregion

#region Context provider: a long-term memory of the user, in MongoDB

/// <summary>
/// An <see cref="AIContextProvider"/> that gives the agent a long-term memory of the user.
/// <list type="bullet">
/// <item><description>Before each model call (<see cref="ProvideAIContextAsync"/>): loads the memories of the user from MongoDB
/// and adds them to the instructions of the run.</description></item>
/// <item><description>After each successful run (<see cref="StoreAIContextAsync"/>): extracts the new facts from the user message
/// with another agent (structured output) and stores them in MongoDB.</description></item>
/// </list>
/// One instance is attached to the agent and shared by all its sessions: it keeps no session data in its fields.
/// The id of the user lives in the session (<see cref="AgentSession.StateBag"/>), through <see cref="ProviderSessionState{TState}"/>.
/// </summary>
internal sealed class UserMemoryAIContextProvider : AIContextProvider
{
    private readonly AIAgent _memoryExtractor;
    private readonly UserMemoriesRepository _memoriesRepository;
    private readonly ProviderSessionState<State> _sessionState;
    private IReadOnlyList<string>? _stateKeys;

    /// <param name="memoryExtractor">The agent that extracts memories from a user message.</param>
    /// <param name="memoriesRepository">The MongoDB storage of the memories.</param>
    /// <param name="stateInitializer">Gives the state of a session the first time the provider sees it: the id of the user.</param>
    public UserMemoryAIContextProvider(AIAgent memoryExtractor, UserMemoriesRepository memoriesRepository, Func<AgentSession?, State> stateInitializer)
    {
        _memoryExtractor = memoryExtractor;
        _memoriesRepository = memoriesRepository;
        _sessionState = new ProviderSessionState<State>(stateInitializer, stateKey: nameof(UserMemoryAIContextProvider));
    }

    /// <summary>The key of the state of this provider in <see cref="AgentSession.StateBag"/> (unique among the providers of the agent).</summary>
    public override IReadOnlyList<string> StateKeys => _stateKeys ??= [_sessionState.StateKey];

    /// <summary>
    /// Called before each model call: returns the context to add to the run (here, a message with the memories).
    /// </summary>
    protected override async ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default)
    {
        // TODO 13: Give the memories of the user to the model
        // - get the id of the user from the state of the session: _sessionState.GetOrInitializeState(context.Session).UserId
        // - load the memories of the user from MongoDB and display them with WriteMemoriesProvided(userId, memories)
        // - return a new AIContext whose Messages contain one user message that lists the memories, framed as data
        //   (or an empty AIContext when there is none). Not Instructions: they are sent as the system message.
        // Replace the last line of this method.

        // Until TODO 13 is done, the provider adds nothing: the base class returns an empty AIContext
        return await base.ProvideAIContextAsync(context, cancellationToken);
    }

    /// <summary>
    /// Called after each successful run: <c>context.RequestMessages</c> only contains the messages passed to <c>RunAsync</c>
    /// (not the chat history, not the context of the providers), and <c>context.ResponseMessages</c> the answer of the agent.
    /// </summary>
    protected override async ValueTask StoreAIContextAsync(InvokedContext context, CancellationToken cancellationToken = default)
    {
        // TODO 14: Get the user messages of the run (none when the agent spoke first: WriteNothingToExtract() and return),
        // then the id of the user (same as TODO 13) and the memories already known for this user

        // TODO 15: Ask the memory extractor for the new facts, as structured output: RunAsync<ExtractedMemories>(messages)
        // with a system message that lists the facts already known, followed by the user messages

        // TODO 16: Store the new memories in MongoDB (skip the empty and already known ones),
        // then display them with WriteMemoriesExtracted(userId, newMemories, extraction.Usage)
        // Replace the last line of this method.

        // Until TODO 16 is done, nothing is stored: the base class does nothing
        await base.StoreAIContextAsync(context, cancellationToken);
    }

    /// <summary>
    /// The state of the provider in a session, serialized with the session: the id of the user whose memories are used.
    /// </summary>
    public sealed class State(string userId)
    {
        public string UserId { get; } = userId ?? throw new ArgumentNullException(nameof(userId));
    }
}

#endregion
