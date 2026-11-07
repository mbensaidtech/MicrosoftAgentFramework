using System.ClientModel;
using System.ClientModel.Primitives;
using Azure.Identity;
using CommunityToolkit.VectorData.InMemory;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Files;
using OpenAI.Responses;
using CommonUtilities;
// The vector stores of the service (scenario 6), aliased: Microsoft.Extensions.VectorData also has a VectorStore (your own store)
using ServiceVectorStores = OpenAI.VectorStores;

using AgenticRAG;
using AgenticRAG.Models;
using AgenticRAG.Tools;
using static AgenticRAG.RagConsole;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// dotnet run                          -> the implemented scenarios (a scenario whose code is still the delivered one is left out)
// dotnet run -- --scenarios 2         -> scenario 2 only (and the scenarios it requires)
// dotnet run -- --scenarios 1,3       -> scenarios 1 and 3
// dotnet run -- --list-scenarios      -> the scenarios of this lab and their state
// Scenarios 5 and 6 use the Responses API: the deployment must support it.
ScenarioSelection scenarioSelection = ScenarioSelection.FromArgs(args);
// ============================================

bool ShouldRunScenario(int scenario) => scenarioSelection.ShouldRun(scenario);

#region Setup: Configuration, client, embedding generator and vector store

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
Console.WriteLine($"Endpoint: {settings.Endpoint}");
Console.WriteLine($"Chat deployment: {settings.ChatDeploymentName}");
Console.WriteLine($"Embedding deployment: {settings.EmbeddingDeploymentName}");
if (!string.IsNullOrWhiteSpace(settings.EmbeddingEndpoint))
{
    // Optional: the embedding deployment is on its own resource (see EmbeddingResource.cs and the README)
    string embeddingAuthentication = string.IsNullOrWhiteSpace(settings.EmbeddingAPIKey) ? "Microsoft Entra ID" : "API key";
    Console.WriteLine($"Embedding endpoint: {settings.EmbeddingEndpoint} ({embeddingAuthentication})");
}

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
// ForEmbeddings (EmbeddingResource.cs) returns the same client, or a client for AzureOpenAI:EmbeddingEndpoint when it is set
IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator = client
    .ForEmbeddings(settings)
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
// Requires scenario 1: it fills the vector store, which is in memory and starts empty at each run.

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Semantic search - without an agent ===");

    // Step 1: A question that shares no word with the FAQ entry it is about (faq-010 "How do I contact customer support?")
    string question = Questions.PhoneNumberForHelp;
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
// Requires scenario 1: it fills the vector store, which is in memory and starts empty at each run.

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: Agentic RAG - the agent searches the FAQ with a function tool ===");

    // Step 1: Turn the semantic search into a function tool (the [Description] attributes tell the model what it does)
    FaqSearchTool faqSearchTool = new(faqCollection);
    AITool searchFaqTool = AIFunctionFactory.Create(faqSearchTool.SearchFaqAsync, "search_faq");

    // Step 2: Create the agent with the tool: it decides itself when to search, and with which question
    AIAgent faqAgent = chatClient.AsAIAgent(
        instructions: Agents.Faq.Instructions,
        name: Agents.Faq.Name,
        tools: [searchFaqTool]);

    // Step 3: Run the agent (with spinner to show loading): it calls search_faq, then answers with the FAQ entries it received
    string question = Questions.BrokenItem;
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
// Requires scenario 1: it fills the vector store, which is in memory and starts empty at each run.

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
        Name = Agents.FaqRag.Name,
        ChatOptions = new ChatOptions { Instructions = Agents.FaqRag.Instructions },
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
    string firstQuestion = Questions.SendBackJacket;
    ColoredConsole.WriteSecondaryLogLine($"Question: {firstQuestion}");
    AgentResponse firstResponse = await ragAgent.RunAsync(firstQuestion, session);
    ColoredConsole.WritePrimaryLogLine($"Agent: {firstResponse.Text}");
    WriteTokenUsage(firstResponse, "Token Usage (first question):");

    // Step 5: Ask a follow-up question in the same session: the search input combines it with the first question
    string followUpQuestion = Questions.MoneyBackDelay;
    ColoredConsole.WriteSecondaryLogLine($"Question: {followUpQuestion}");
    AgentResponse followUpResponse = await ragAgent.RunAsync(followUpQuestion, session);
    ColoredConsole.WritePrimaryLogLine($"Agent: {followUpResponse.Text}");
    WriteTokenUsage(followUpResponse, "Token Usage (follow-up):");
}

#endregion

#region Scenario 5: Responses API with your own vector store - nothing stored by the service
// Requires scenario 1: it fills the vector store, which is in memory and starts empty at each run.
// Bonus scenario (optional): the lab is completed without it; with every bonus scenario, it is completed with its bonus (Done+).

if (ShouldRunScenario(5))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 5: Responses API with your own vector store - nothing stored by the service ===");

    // Step 1: The same search_faq function tool as scenario 3, on the same vector store: your RAG does not depend on the API of the model
    FaqSearchTool faqSearchTool = new(faqCollection);
    AITool searchFaqTool = AIFunctionFactory.Create(faqSearchTool.SearchFaqAsync, "search_faq");

    // Step 2: A chat client on the Responses API that asks the service not to store anything (store = false).
    // By default the service keeps each response 30 days, with its inputs: here, the FAQ entries returned by your tool.
    // ResponsesClient is flagged [Experimental] by the OpenAI SDK (OPENAI001), AsIChatClientWithStoredOutputDisabled by Agent Framework (MAAI001).
#pragma warning disable OPENAI001, MAAI001
    ResponsesClient responsesClient = client.GetResponsesClient();
    IChatClient statelessResponsesClient = responsesClient.AsIChatClientWithStoredOutputDisabled(settings.ChatDeploymentName);
#pragma warning restore OPENAI001, MAAI001

    // Step 3: Create the agent of scenario 3 on this client: same instructions, same tool
    AIAgent faqAgent = statelessResponsesClient.AsAIAgent(
        instructions: Agents.Faq.Instructions,
        name: Agents.Faq.Name,
        tools: [searchFaqTool]);

    // Step 4: Run the same question as scenario 3: the search runs in this application, on your vector store
    string question = Questions.BrokenItem;
    ColoredConsole.WriteSecondaryLogLine($"Question: {question}");
    AgentResponse response = await faqAgent.RunAsync(question).WithSpinner("Running agent");

    // Step 5: Display the tool calls (run by this application), the answer, and check that the service kept nothing
    WriteToolCalls(response);
    ColoredConsole.WritePrimaryLogLine($"Agent: {response.Text}");
    bool stored;
    try
    {
#pragma warning disable OPENAI001
        await responsesClient.GetResponseAsync(response.ResponseId);
#pragma warning restore OPENAI001
        stored = true;
    }
    catch (ClientResultException exception) when (exception.Status == 404)
    {
        stored = false;
    }
    ColoredConsole.WriteInfoLine($"Response {response.ResponseId} stored by the service: {(stored ? "yes" : "no (404)")}");

    // Step 6: Display token usage: compare with scenario 3 (same tool, same FAQ entries)
    WriteTokenUsage(response);
}

#endregion

#region Scenario 6: Hosted file search - the service stores, chunks, embeds and searches the FAQ
// Bonus scenario (optional): the lab is completed without it; with every bonus scenario, it is completed with its bonus (Done+).

if (ShouldRunScenario(6))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 6: Hosted file search - the service stores, chunks, embeds and searches the FAQ ===");

    // Step 1: Get the clients of the files and of the vector stores of the service (the same Azure OpenAI resource as the chat model).
    // Your InMemoryVectorStore and your embedding deployment are not used here: the service embeds and searches with its own.
    // These clients are still flagged [Experimental] (OPENAI001) by the OpenAI SDK.
#pragma warning disable OPENAI001
    OpenAIFileClient fileClient = client.GetOpenAIFileClient();
    ServiceVectorStores.VectorStoreClient vectorStoreClient = client.GetVectorStoreClient();

    List<FaqRecord> faqEntries = await FaqData.LoadAsync();
    List<string> fileIds = [];
    string? serviceVectorStoreId = null;
    try
    {
        // Step 2: Upload the FAQ to the service, one small document per entry (faq-005.md...): this is how you choose the unit of the search
        foreach (FaqRecord entry in faqEntries)
        {
            OpenAIFile file = await fileClient.UploadFileAsync(BinaryData.FromString(FaqData.ToDocument(entry)), $"{entry.Id}.md", FileUploadPurpose.Assistants);
            fileIds.Add(file.Id);
        }
        ColoredConsole.WritePrimaryLogLine($"Uploaded {fileIds.Count} FAQ documents to the service");

        // Step 3: Create a vector store of the service with these files. It expires 1 day after its last use: a safety net,
        // in case the program stops before the clean-up of step 7 (a vector store is billed per GB and per day).
        ServiceVectorStores.VectorStoreCreationOptions creationOptions = new()
        {
            Name = "lab07-sav-faq",
            ExpirationPolicy = new ServiceVectorStores.VectorStoreExpirationPolicy(ServiceVectorStores.VectorStoreExpirationAnchor.LastActiveAt, 1)
        };
        fileIds.ForEach(creationOptions.FileIds.Add);
        ServiceVectorStores.VectorStore serviceVectorStore = await vectorStoreClient.CreateVectorStoreAsync(creationOptions);
        serviceVectorStoreId = serviceVectorStore.Id;

        // Step 4: Wait until the service has chunked and embedded the files (asynchronous on its side)
        serviceVectorStore = await HostedFileSearch.WaitUntilIndexedAsync(vectorStoreClient, serviceVectorStoreId).WithSpinner("Indexing the FAQ in the vector store of the service");
        WriteServiceVectorStore(serviceVectorStore);

        // Step 5: Create the agent with the hosted file search tool, on the vector store of the service.
        // There is no function to write: the service runs the search between two model steps, in the same request.
        AIAgent hostedFaqAgent = client.GetResponsesClient().AsAIAgent(
            model: settings.ChatDeploymentName,
            instructions: Agents.HostedFaq.Instructions,
            name: Agents.HostedFaq.Name,
            tools: [new HostedFileSearchTool { Inputs = [new HostedVectorStoreContent(serviceVectorStoreId)], MaximumResultCount = 3 }]);

        // Step 6: Run the same question as scenarios 3 and 5. By default, the service does not return the chunks it found:
        // ask for them (include = file_search_call.results) through the raw options of the Responses API.
        string question = Questions.BrokenItem;
        ColoredConsole.WriteSecondaryLogLine($"Question: {question}");
        ChatClientAgentRunOptions runOptions = new(new ChatOptions
        {
            RawRepresentationFactory = _ => new CreateResponseOptions { IncludedProperties = { IncludedResponseProperty.FileSearchCallResults } }
        });
        AgentResponse response = await hostedFaqAgent.RunAsync(question, options: runOptions).WithSpinner("Running agent with the hosted file search");

        WriteFileSearch(response);
        ColoredConsole.WritePrimaryLogLine($"Agent: {response.Text}");
        WriteTokenUsage(response);
    }
    finally
    {
        // Step 7: Clean up: the documents and the vector store live in the service until you delete them
        if (serviceVectorStoreId is not null)
        {
            await vectorStoreClient.DeleteVectorStoreAsync(serviceVectorStoreId);
        }
        foreach (string fileId in fileIds)
        {
            await fileClient.DeleteFileAsync(fileId);
        }
        ColoredConsole.WriteInfoLine($"Deleted the vector store and the {fileIds.Count} documents from the service");
    }
#pragma warning restore OPENAI001
}

#endregion
