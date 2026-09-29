using System.ClientModel;
using System.ClientModel.Primitives;
using Azure.Identity;
using CommunityToolkit.VectorData.InMemory;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using OpenAI;
using OpenAI.Chat;
using CommonUtilities;

using AgenticRAG;
using AgenticRAG.Models;
using AgenticRAG.Tools;
using static AgenticRAG.RagConsole;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// Set to: [1], [1, 2], [1, 3], [1, 4] or [1, 2, 3, 4] to run specific scenarios.
// Scenario 1 fills the vector store: the other scenarios need it (the store is in memory, it starts empty).
HashSet<int> scenariosToRun = [1, 2, 3, 4];
// ============================================

bool ShouldRunScenario(int scenario) => scenariosToRun.Count == 0 || scenariosToRun.Contains(scenario);

#region Setup: Configuration, client, embedding generator and vector store

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
Console.WriteLine($"Endpoint: {settings.Endpoint}");
Console.WriteLine($"Chat deployment: {settings.ChatDeploymentName}");
Console.WriteLine($"Embedding deployment: {settings.EmbeddingDeploymentName}");

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

// Step 3: Get a ChatClient (Chat Completions API) for the chat deployment
ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);

// Step 4: Get an embedding generator for the embedding deployment (the Microsoft.Extensions.AI abstraction of the EmbeddingClient)
IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator = client
    .GetEmbeddingClient(settings.EmbeddingDeploymentName)
    .AsIEmbeddingGenerator();

// Step 5: Create the vector store, with the embedding generator: it turns the text of the records into vectors
// (any Microsoft.Extensions.VectorData store could replace the in-memory one)
VectorStore vectorStore = new InMemoryVectorStore(new InMemoryVectorStoreOptions { EmbeddingGenerator = embeddingGenerator });

// Step 6: Get the FAQ collection: records of type FaqRecord (its attributes define the schema), keyed by a string
VectorStoreCollection<string, FaqRecord> faqCollection = vectorStore.GetCollection<string, FaqRecord>("sav-faq");

#endregion

#region Scenario 1: Fill the vector store with the FAQ

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: Fill the vector store with the FAQ ===");

    // Step 1: Load the FAQ entries from Data/sav-faq.json
    List<FaqRecord> faqEntries = await FaqData.LoadAsync();
    ColoredConsole.WritePrimaryLogLine($"Loaded {faqEntries.Count} FAQ entries from {FaqData.FileName}");

    // Step 2: Create the collection (and its vector index) if it does not exist yet
    await faqCollection.EnsureCollectionExistsAsync();

    // Step 3: Upsert the entries: the store embeds the text of the Embedding property of each record (one call to the embedding model)
    await faqCollection.UpsertAsync(faqEntries).WithSpinner("Generating the embeddings and indexing the FAQ");
    ColoredConsole.WriteInfoLine($"Vector store ready: {faqEntries.Count} FAQ entries indexed in the collection '{faqCollection.Name}'");
}

#endregion

#region Scenario 2: Semantic search - without an agent

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Semantic search - without an agent ===");

    // Step 1: A question that shares no word with the FAQ entry it is about (faq-010 "How do I contact customer support?")
    string question = "Is there a phone number I can call for help?";
    ColoredConsole.WriteSecondaryLogLine($"Question: {question}");

    // Step 2: Search the 3 closest entries: the store embeds the question, then compares its vector with the vectors of the entries
    int rank = 0;
    await foreach (VectorSearchResult<FaqRecord> result in faqCollection.SearchAsync(question, top: 3))
    {
        // Step 3: Display each result: the record and its relevance score
        WriteSearchResult(++rank, result);
    }
}

#endregion

#region Scenario 3: Agentic RAG - the agent decides when to search the FAQ (function tool)

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: Agentic RAG - the agent searches the FAQ with a function tool ===");

    // Step 1: Turn the semantic search into a function tool (the [Description] attributes tell the model what it does)
    FaqSearchTool faqSearchTool = new(faqCollection);
    AITool searchFaqTool = AIFunctionFactory.Create(faqSearchTool.SearchFaqAsync, "search_faq");

    // Step 2: Create the agent with the tool: it decides itself when to search, and with which question
    AIAgent faqAgent = chatClient.AsAIAgent(
        instructions: "You are the customer support assistant of an online shop. " +
                      "Answer only from the FAQ: call search_faq to find the relevant entries, answer from them and quote the id of the entries you used. " +
                      "If the FAQ does not cover the question, say so.",
        name: "FaqAgent",
        tools: [searchFaqTool]);

    // Step 3: Run the agent (with spinner to show loading): it calls search_faq, then answers with the FAQ entries it received
    string question = "I received a broken item yesterday. What should I do?";
    ColoredConsole.WriteSecondaryLogLine($"Question: {question}");
    AgentResponse response = await faqAgent.RunAsync(question).WithSpinner("Running agent");

    // Step 4: Display the tool calls made during the run, then the answer
    WriteToolCalls(response);
    ColoredConsole.WritePrimaryLogLine($"Agent: {response.Text}");

    // Step 5: Display token usage (total of the model calls of the run: the tool request, then the answer)
    WriteTokenUsage(response);
}

#endregion

#region Scenario 4: RAG with TextSearchProvider - the FAQ is searched before every model call (context provider)

if (ShouldRunScenario(4))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 4: RAG with TextSearchProvider - the FAQ is searched before every model call ===");

    // Step 1: The search function of the provider: it receives the text to search (the new message, plus recent ones)
    // and returns the results in the format of the provider (source name, link, text)
    async Task<IEnumerable<TextSearchProvider.TextSearchResult>> SearchFaqForProviderAsync(string text, CancellationToken cancellationToken)
    {
        List<TextSearchProvider.TextSearchResult> results = [];
        await foreach (VectorSearchResult<FaqRecord> result in faqCollection.SearchAsync(text, top: 2, cancellationToken: cancellationToken))
        {
            results.Add(new TextSearchProvider.TextSearchResult
            {
                SourceName = result.Record.Id,
                Text = $"Q: {result.Record.Question}\nA: {result.Record.Answer}",
                RawRepresentation = result
            });
        }

        ColoredConsole.WriteWarningLine($"[TextSearchProvider] Search input: {text.ReplaceLineEndings(" | ")}");
        ColoredConsole.WriteWarningLine($"[TextSearchProvider] Results: {string.Join(", ", results.Select(result => result.SourceName))}");
        return results;
    }

    // Step 2: Configure the provider: search before every model call (the default), and search with the last 2 user messages
    // so that a follow-up question ("and when...?") is searched with the context of the previous one
    TextSearchProviderOptions textSearchOptions = new()
    {
        SearchTime = TextSearchProviderOptions.TextSearchBehavior.BeforeAIInvoke,
        RecentMessageMemoryLimit = 2
    };

    // Step 3: Create the agent with the TextSearchProvider as an AI context provider: the results are added to the model context
    // of every run, as a message. The chat history provider filter keeps these result messages out of the stored history
    // (they would otherwise be sent again with every later question).
    AIAgent ragAgent = chatClient.AsAIAgent(new ChatClientAgentOptions
    {
        Name = "FaqRagAgent",
        ChatOptions = new ChatOptions
        {
            Instructions = "You are the customer support assistant of an online shop. " +
                           "Answer only from the provided context and quote the id of the FAQ entries you used. Keep the answers short."
        },
        AIContextProviders = [new TextSearchProvider(SearchFaqForProviderAsync, textSearchOptions)],
        ChatHistoryProvider = new InMemoryChatHistoryProvider(new InMemoryChatHistoryProviderOptions
        {
            StorageInputRequestMessageFilter = messages => messages.Where(message =>
                message.GetAgentRequestMessageSourceType() != AgentRequestMessageSourceType.AIContextProvider &&
                message.GetAgentRequestMessageSourceType() != AgentRequestMessageSourceType.ChatHistory)
        })
    });

    // Step 4: Ask a first question in a session (no spinner: the provider writes to the console during the run)
    AgentSession session = await ragAgent.CreateSessionAsync();
    string firstQuestion = "I want to send back a jacket I bought last week. How does it work?";
    ColoredConsole.WriteSecondaryLogLine($"Question: {firstQuestion}");
    AgentResponse firstResponse = await ragAgent.RunAsync(firstQuestion, session);
    ColoredConsole.WritePrimaryLogLine($"Agent: {firstResponse.Text}");
    WriteTokenUsage(firstResponse, "Token Usage (first question):");

    // Step 5: Ask a follow-up question in the same session: the search input combines it with the first question
    string followUpQuestion = "And how long until I get my money back?";
    ColoredConsole.WriteSecondaryLogLine($"Question: {followUpQuestion}");
    AgentResponse followUpResponse = await ragAgent.RunAsync(followUpQuestion, session);
    ColoredConsole.WritePrimaryLogLine($"Agent: {followUpResponse.Text}");
    WriteTokenUsage(followUpResponse, "Token Usage (follow-up):");
}

#endregion
