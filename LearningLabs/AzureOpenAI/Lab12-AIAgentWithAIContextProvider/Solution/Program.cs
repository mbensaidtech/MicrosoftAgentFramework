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

// Step 3: Get a ChatClient (Chat Completions API) for the deployment
ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);

#endregion

#region Agent with a long-term memory (shared by both scenarios)

ColoredConsole.WriteDividerLine();
ColoredConsole.WriteInfoLine("Agent with a long-term memory - the memories of the user are stored in MongoDB");
ColoredConsole.WritePrimaryLogLine($"User id: {UserId}");
ColoredConsole.WritePrimaryLogLine($"{AgentName} instructions: {AgentInstructions}");
ColoredConsole.WritePrimaryLogLine($"Memory extractor instructions: {ExtractorInstructions}");

// Step 1: Connect to MongoDB (started with docker compose, see the README) and create the repository of the memories
IMongoDatabase database = await ConfigurationHelper.ConnectToMongoDbAsync();
UserMemoriesRepository memoriesRepository = new(database);

// Step 2: Create the agent that extracts memories from a user message (used by the context provider, with structured output)
AIAgent memoryExtractor = chatClient.AsAIAgent(instructions: ExtractorInstructions, name: "MemoryExtractor");

// Step 3: Create the agent with the context provider.
// One provider instance for the agent, shared by all its sessions: the stateInitializer gives each new session
// its state (the id of the user), stored in the session and not in the provider.
AIAgent memoryAgent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
    Name = AgentName,
    ChatOptions = new() { Instructions = AgentInstructions },
    AIContextProviders =
    [
        new UserMemoryAIContextProvider(memoryExtractor, memoriesRepository, stateInitializer: _ => new UserMemoryAIContextProvider.State(UserId))
    ]
});

#endregion

#region Scenario 1: Remember the user from one session to another

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: Remember the user from one session to another ===");

    // Step 1: Start from an empty memory, so that the scenario gives the same result at each run
    long deletedMemories = await memoriesRepository.DeleteMemoriesAsync(UserId);
    ColoredConsole.WriteInfoLine($"Memories of user '{UserId}' cleared ({deletedMemories} deleted)");

    // Step 2: First conversation - the user introduces herself.
    // Before the model call, the provider adds the memories to the instructions (none yet);
    // after it, the provider extracts the new facts from the user message and stores them in MongoDB.
    ColoredConsole.WriteInfoLine("First session");
    ColoredConsole.WriteUserLine($"User: {IntroductionMessage}");
    AgentSession firstSession = await memoryAgent.CreateSessionAsync();
    AgentResponse introductionResponse = await memoryAgent.RunAsync(IntroductionMessage, firstSession);
    ColoredConsole.WriteAssistantLine($"Agent: {introductionResponse.Text}");

    // Step 3: Display the memories stored in MongoDB for the user
    WriteStoredMemories(UserId, await memoriesRepository.GetMemoriesAsync(UserId));

    // Step 4: A new conversation - a new session, so an empty chat history.
    // The agent can only know the name and the preferences of the user from the memories added by the provider.
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("New session (empty chat history)");
    ColoredConsole.WriteUserLine($"User: {RecallQuestion}");
    AgentSession newSession = await memoryAgent.CreateSessionAsync();
    AgentResponse recallResponse = await memoryAgent.RunAsync(RecallQuestion, newSession);
    ColoredConsole.WriteAssistantLine($"Agent: {recallResponse.Text}");

    // Step 5: Display token usage: the input tokens include the memories added to the instructions
    WriteTokenUsage(recallResponse);
}

#endregion

#region Scenario 2: Chat with the agent - it greets you with what it remembers

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Chat with the agent - it greets you with what it remembers ===");

    // Step 1: Create a new session and let the agent start the conversation: RunAsync without a message.
    // The greeting uses the memories of the user (from scenario 1, or from your previous chats).
    AgentSession chatSession = await memoryAgent.CreateSessionAsync();
    AgentResponse greeting = await memoryAgent.RunAsync(chatSession);
    ColoredConsole.WriteAssistantLine($"Agent: {greeting.Text}");

    // Step 2: Chat until an empty message or "exit". Tell the agent new facts about you: the provider stores them.
    ColoredConsole.WriteSecondaryLogLine("Type a message, or an empty line / 'exit' to end the chat.");
    while (true)
    {
        Console.Write("You: ");
        string? userInput = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(userInput) || userInput.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine();
            break;
        }

        AgentResponse response = await memoryAgent.RunAsync(userInput, chatSession);
        ColoredConsole.WriteAssistantLine($"Agent: {response.Text}");
    }

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
    /// Called before each model call: returns the context to add to the run (here, instructions).
    /// </summary>
    protected override async ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default)
    {
        // Step 1: Get the id of the user from the state of the session
        string userId = _sessionState.GetOrInitializeState(context.Session).UserId;

        // Step 2: Load the memories of the user from MongoDB
        List<string> memories = await _memoriesRepository.GetMemoriesAsync(userId, cancellationToken);
        WriteMemoriesProvided(userId, memories);

        // Step 3: Return them as additional instructions (added after the instructions of the agent), or no context at all
        return memories.Count == 0
            ? new AIContext()
            : new AIContext { Instructions = "What you remember about the user from previous conversations:\n" + string.Join("\n", memories.Select(memory => $"- {memory}")) };
    }

    /// <summary>
    /// Called after each successful run: <c>context.RequestMessages</c> only contains the messages passed to <c>RunAsync</c>
    /// (not the chat history, not the context of the providers), and <c>context.ResponseMessages</c> the answer of the agent.
    /// </summary>
    protected override async ValueTask StoreAIContextAsync(InvokedContext context, CancellationToken cancellationToken = default)
    {
        // Step 1: Get the user messages of the run (none when the agent spoke first) and the memories already known
        List<ChatMessage> userMessages = context.RequestMessages.Where(message => message.Role == ChatRole.User).ToList();
        if (userMessages.Count == 0)
        {
            WriteNothingToExtract();
            return;
        }

        string userId = _sessionState.GetOrInitializeState(context.Session).UserId;
        List<string> knownMemories = await _memoriesRepository.GetMemoriesAsync(userId, cancellationToken);

        // Step 2: Ask the extractor agent for the new facts, as structured output (no session: a one-shot run)
        List<ChatMessage> extractionMessages =
        [
            new(ChatRole.System, $"Facts already known about the user (do not extract them again):\n{string.Join("\n", knownMemories.DefaultIfEmpty("(none)"))}"),
            .. userMessages
        ];
        AgentResponse<ExtractedMemories> extraction = await _memoryExtractor.RunAsync<ExtractedMemories>(extractionMessages, cancellationToken: cancellationToken);

        // Step 3: Store the new memories in MongoDB (the user id comes from the session, never from the model)
        List<string> newMemories = extraction.Result.Memories
            .Where(memory => !string.IsNullOrWhiteSpace(memory) && !knownMemories.Contains(memory))
            .ToList();
        await _memoriesRepository.AddMemoriesAsync(userId, newMemories, cancellationToken);
        WriteMemoriesExtracted(userId, newMemories, extraction.Usage);
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
