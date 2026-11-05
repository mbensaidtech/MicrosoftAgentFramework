using System.ClientModel;
using System.ClientModel.Primitives;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using CommonUtilities;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

using AIAgentWithAIContextProvider;
using static AIAgentWithAIContextProvider.MemoryConsole;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// dotnet run                          -> the implemented scenarios (a scenario whose code is still the delivered one is left out)
// dotnet run -- --scenarios 2         -> scenario 2 only (and the scenarios it requires)
// dotnet run -- --scenarios 1,3       -> scenarios 1 and 3
// dotnet run -- --list-scenarios      -> the scenarios of this lab and their state
ScenarioSelection scenarioSelection = ScenarioSelection.FromArgs(args);
// ============================================

bool ShouldRunScenario(int scenario) => scenarioSelection.ShouldRun(scenario);

// The user of this lab. Its memories are stored under this id.
// In a real application, it is the id of the signed-in user.
const string UserId = "ada";

// The agent that talks with the user (Agents.MemoryAssistant), the agent that the context provider uses to extract memories
// (Agents.MemoryExtractor) and the conversation of scenario 1 (Questions.Introduction, then Questions.RecallNameDinnerAndActivity
// in a new session) are provided in LabPrompts.cs.

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
ColoredConsole.WriteInfoLine("Agent with a long-term memory - the memories of the user are stored outside of the application");
ColoredConsole.WritePrimaryLogLine($"User id: {UserId}");
ColoredConsole.WritePrimaryLogLine($"{Agents.MemoryAssistant.Name} instructions: {Agents.MemoryAssistant.Instructions}");
ColoredConsole.WritePrimaryLogLine($"Memory extractor instructions: {Agents.MemoryExtractor.Instructions}");

// Step 1: Open the repository of the memories chosen by Storage:Mode (MongoDB with or without Docker, or a JSON file, see the README)
IUserMemoriesRepository memoriesRepository = await ConfigurationHelper.CreateUserMemoriesRepositoryAsync();

// Step 2: Create the agent that extracts memories from a user message (used by the context provider, with structured output)
AIAgent memoryExtractor = chatClient.AsAIAgent(instructions: Agents.MemoryExtractor.Instructions, name: Agents.MemoryExtractor.Name);

// Step 3: Create the agent with the context provider.
// One provider instance for the agent, shared by all its sessions: the stateInitializer gives each new session
// its state (the id of the user), stored in the session and not in the provider.
AIAgent memoryAgent = chatClient.AsAIAgent(new ChatClientAgentOptions
{
    Name = Agents.MemoryAssistant.Name,
    ChatOptions = new() { Instructions = Agents.MemoryAssistant.Instructions },
    AIContextProviders =
    [
        new UserMemoryAIContextProvider(memoryExtractor, memoriesRepository, stateInitializer: _ => new UserMemoryAIContextProvider.State(UserId))
    ],
    // The memories are added again to each run by the provider: keep them out of the chat history of the session,
    // which only stores the messages of the conversation (same filter as Lab 07)
    ChatHistoryProvider = new InMemoryChatHistoryProvider(new InMemoryChatHistoryProviderOptions
    {
        StorageInputRequestMessageFilter = messages => messages.Where(message =>
            message.GetAgentRequestMessageSourceType() != AgentRequestMessageSourceType.AIContextProvider &&
            message.GetAgentRequestMessageSourceType() != AgentRequestMessageSourceType.ChatHistory)
    })
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
    // Before the model call, the provider adds the memories to the request, as a message (none yet);
    // after it, the provider extracts the new facts from the user message and stores them in the repository.
    ColoredConsole.WriteInfoLine("First session");
    ColoredConsole.WriteUserLine($"User: {Questions.Introduction}");
    AgentSession firstSession = await memoryAgent.CreateSessionAsync();
    AgentResponse introductionResponse = await memoryAgent.RunAsync(Questions.Introduction, firstSession);
    ColoredConsole.WriteAssistantLine($"Agent: {introductionResponse.Text}");

    // Step 3: Display the memories stored for the user
    WriteStoredMemories(UserId, await memoriesRepository.GetMemoriesAsync(UserId));

    // Step 4: A new conversation - a new session, so an empty chat history.
    // The agent can only know the name and the preferences of the user from the memories added by the provider.
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("New session (empty chat history)");
    ColoredConsole.WriteUserLine($"User: {Questions.RecallNameDinnerAndActivity}");
    AgentSession newSession = await memoryAgent.CreateSessionAsync();
    AgentResponse recallResponse = await memoryAgent.RunAsync(Questions.RecallNameDinnerAndActivity, newSession);
    ColoredConsole.WriteAssistantLine($"Agent: {recallResponse.Text}");

    // Step 5: Display token usage: the input tokens include the message with the memories added by the provider
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

    ColoredConsole.WriteInfoLine("Chat ended. Run scenario 2 again: the agent still remembers what you told it (the memories are stored outside of the application).");
}

#endregion

#region Context provider: a long-term memory of the user, in a repository

/// <summary>
/// An <see cref="AIContextProvider"/> that gives the agent a long-term memory of the user.
/// <list type="bullet">
/// <item><description>Before each model call (<see cref="ProvideAIContextAsync"/>): loads the memories of the user from the repository
/// and adds them to the request of the run, as a message framed as data.</description></item>
/// <item><description>After each successful run (<see cref="StoreAIContextAsync"/>): extracts the new facts from the user message
/// with another agent (structured output) and stores them in the repository.</description></item>
/// </list>
/// One instance is attached to the agent and shared by all its sessions: it keeps no session data in its fields.
/// The id of the user lives in the session (<see cref="AgentSession.StateBag"/>), through <see cref="ProviderSessionState{TState}"/>.
/// </summary>
internal sealed class UserMemoryAIContextProvider : AIContextProvider
{
    private readonly AIAgent _memoryExtractor;
    private readonly IUserMemoriesRepository _memoriesRepository;
    private readonly ProviderSessionState<State> _sessionState;
    private IReadOnlyList<string>? _stateKeys;

    /// <param name="memoryExtractor">The agent that extracts memories from a user message.</param>
    /// <param name="memoriesRepository">The storage of the memories (MongoDB or a JSON file).</param>
    /// <param name="stateInitializer">Gives the state of a session the first time the provider sees it: the id of the user.</param>
    public UserMemoryAIContextProvider(AIAgent memoryExtractor, IUserMemoriesRepository memoriesRepository, Func<AgentSession?, State> stateInitializer)
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
        // Step 1: Get the id of the user from the state of the session
        string userId = _sessionState.GetOrInitializeState(context.Session).UserId;

        // Step 2: Load the memories of the user from the repository
        List<string> memories = await _memoriesRepository.GetMemoriesAsync(userId, cancellationToken);
        WriteMemoriesProvided(userId, memories);

        // Step 3: Return them as a message of the run, framed as data, or no context at all.
        // Not as instructions: the memories were written from what the user said, so they must not get the trust
        // of the system message (instructions are sent as the system message).
        return memories.Count == 0
            ? new AIContext()
            : new AIContext
            {
                Messages =
                [
                    new ChatMessage(ChatRole.User,
                        ContextTexts.MemoriesIntroduction + string.Join("\n", memories.Select(memory => $"- {memory}")))
                ]
            };
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
            new(ChatRole.System, ContextTexts.KnownFactsIntroduction + string.Join("\n", knownMemories.DefaultIfEmpty(ContextTexts.NoKnownFact))),
            .. userMessages
        ];
        AgentResponse<ExtractedMemories> extraction = await _memoryExtractor.RunAsync<ExtractedMemories>(extractionMessages, cancellationToken: cancellationToken);

        // Step 3: Store the new memories in the repository (the user id comes from the session, never from the model)
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
