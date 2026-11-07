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

// TODO 3: Get a ChatClient (Chat Completions API) for the chat deployment
// ChatClient chatClient = ...

// TODO 4: Get an embedding generator for the embedding deployment
// Hint: client.ForEmbeddings(settings).GetEmbeddingClient(settings.EmbeddingDeploymentName).AsIEmbeddingGenerator()
// (ForEmbeddings, in EmbeddingResource.cs, returns the same client, or a client for AzureOpenAI:EmbeddingEndpoint when it is set)
// IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator = ...

// TODO 5: Create the in-memory vector store with the embedding generator (it turns the text of the records into vectors)
// Hint: new InMemoryVectorStore(new InMemoryVectorStoreOptions { EmbeddingGenerator = embeddingGenerator })
// VectorStore vectorStore = ...

// TODO 6: Get the FAQ collection: records of type FaqRecord, keyed by a string, named "sav-faq"
// Hint: vectorStore.GetCollection<string, FaqRecord>("sav-faq")
// VectorStoreCollection<string, FaqRecord> faqCollection = ...

#endregion

#region Scenario 1: Fill the vector store with the FAQ

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: Fill the vector store with the FAQ ===");

    // TODO 7: Load the FAQ entries from Data/sav-faq.json and display how many were loaded
    // Hint: List<FaqRecord> faqEntries = await FaqData.LoadAsync();
    //       ColoredConsole.WritePrimaryLogLine($"Loaded {faqEntries.Count} FAQ entries from {FaqData.FileName}")

    // TODO 8: Create the collection if it does not exist yet
    // Hint: await faqCollection.EnsureCollectionExistsAsync()

    // TODO 9: Upsert the entries (the store embeds the Embedding property of each record), then display a confirmation
    // Hint: await faqCollection.UpsertAsync(faqEntries).WithSpinner("Generating the embeddings and indexing the FAQ")
    //       ColoredConsole.WriteInfoLine($"Vector store ready: {faqEntries.Count} FAQ entries indexed in the collection '{faqCollection.Name}'")
}

#endregion

#region Scenario 2: Semantic search - without an agent
// Requires scenario 1: it fills the vector store, which is in memory and starts empty at each run.

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Semantic search - without an agent ===");

    // TODO 10: Search the 3 FAQ entries closest to the question Questions.PhoneNumberForHelp (provided in LabPrompts.cs)
    // and display them with WriteSearchResult(rank, result)
    // Hint: string question = Questions.PhoneNumberForHelp; (shares no word with faq-010, which it is about)
    //       ColoredConsole.WriteSecondaryLogLine($"Question: {question}");
    //       await foreach (VectorSearchResult<FaqRecord> result in faqCollection.SearchAsync(question, top: 3)) { ... }
}

#endregion

#region Scenario 3: Agentic RAG - the agent decides when to search the FAQ (function tool)
// Requires scenario 1: it fills the vector store, which is in memory and starts empty at each run.

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: Agentic RAG - the agent searches the FAQ with a function tool ===");

    // TODO 11: Turn the semantic search of FaqSearchTool into a function tool named "search_faq"
    // Hint: FaqSearchTool faqSearchTool = new(faqCollection);
    //       AITool searchFaqTool = AIFunctionFactory.Create(faqSearchTool.SearchFaqAsync, "search_faq");

    // TODO 12: Create the agent with the tool, and the instructions and the name of Agents.Faq
    // Hint: chatClient.AsAIAgent(instructions: Agents.Faq.Instructions, name: Agents.Faq.Name, tools: [searchFaqTool])
    // AIAgent faqAgent = ...

    // TODO 13: Run the agent with the question Questions.BrokenItem, then display the tool calls and the answer
    // Hint: string question = Questions.BrokenItem;
    //       ColoredConsole.WriteSecondaryLogLine($"Question: {question}");
    //       AgentResponse response = await faqAgent.RunAsync(question).WithSpinner("Running agent");
    //       WriteToolCalls(response); then ColoredConsole.WritePrimaryLogLine($"Agent: {response.Text}")

    // TODO 14: Display token usage
    // Hint: WriteTokenUsage(response)
}

#endregion

#region Scenario 4: RAG with TextSearchProvider - the FAQ is searched before every model call (context provider)
// Requires scenario 1: it fills the vector store, which is in memory and starts empty at each run.

if (ShouldRunScenario(4))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 4: RAG with TextSearchProvider - the FAQ is searched before every model call ===");

    // TODO 15: Write the search function of the provider (see the README): it searches the 2 closest entries and
    // returns them as TextSearchProvider.TextSearchResult (SourceName = the id, Text = question + answer), then
    // displays the search input and the ids found with "[TextSearchProvider] ..." lines
    // async Task<IEnumerable<TextSearchProvider.TextSearchResult>> SearchFaqForProviderAsync(string text, CancellationToken cancellationToken)
    // {
    //     ...
    // }

    // TODO 16: Configure the provider: search before every model call, with the last 2 user messages as search context
    // Hint: new TextSearchProviderOptions { SearchTime = TextSearchProviderOptions.TextSearchBehavior.BeforeAIInvoke, RecentMessageMemoryLimit = 2 }
    // TextSearchProviderOptions textSearchOptions = ...

    // TODO 17: Create the agent with ChatClientAgentOptions: Name and ChatOptions.Instructions of Agents.FaqRag,
    // AIContextProviders = [new TextSearchProvider(SearchFaqForProviderAsync, textSearchOptions)]
    // and a ChatHistoryProvider that keeps the provider messages out of the stored history (see the README)
    // AIAgent ragAgent = ...

    // TODO 18: Create a session and ask the first question Questions.SendBackJacket in it
    // (no spinner: the provider writes to the console during the run)
    // Hint: AgentSession session = await ragAgent.CreateSessionAsync();
    //       string firstQuestion = Questions.SendBackJacket;
    //       ColoredConsole.WriteSecondaryLogLine($"Question: {firstQuestion}");
    //       AgentResponse firstResponse = await ragAgent.RunAsync(firstQuestion, session);
    //       ColoredConsole.WritePrimaryLogLine($"Agent: {firstResponse.Text}");
    //       WriteTokenUsage(firstResponse, "Token Usage (first question):")

    // TODO 19: Ask the follow-up question Questions.MoneyBackDelay in the same session, and display the answer and its token usage
    // Hint: string followUpQuestion = Questions.MoneyBackDelay;
    //       same code as TODO 18, with WriteTokenUsage(followUpResponse, "Token Usage (follow-up):")
}

#endregion

#region Scenario 5: Responses API with your own vector store - nothing stored by the service
// Requires scenario 1: it fills the vector store, which is in memory and starts empty at each run.
// Bonus scenario (optional): the lab is completed without it; with every bonus scenario, it is completed with its bonus (Done+).

if (ShouldRunScenario(5))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 5: Responses API with your own vector store - nothing stored by the service ===");

    // TODO 20: Create the search_faq function tool of scenario 3 (the same code as TODO 11)

    // TODO 21: Get a chat client on the Responses API that asks the service not to store anything (store = false)
    // Hint: ResponsesClient responsesClient = client.GetResponsesClient();
    //       IChatClient statelessResponsesClient = responsesClient.AsIChatClientWithStoredOutputDisabled(settings.ChatDeploymentName);
    // Note: flagged experimental by the OpenAI SDK (OPENAI001) and by Agent Framework (MAAI001): keep the pragma around this code.
#pragma warning disable OPENAI001, MAAI001
    // ResponsesClient responsesClient = ...
    // IChatClient statelessResponsesClient = ...
#pragma warning restore OPENAI001, MAAI001

    // TODO 22: Create the agent of scenario 3 on this client (Agents.Faq, tools: [searchFaqTool])
    // Hint: AIAgent faqAgent = statelessResponsesClient.AsAIAgent(instructions: ..., name: ..., tools: [searchFaqTool]);

    // TODO 23: Ask Questions.BrokenItem, then display the tool calls and the answer (the same code as TODO 13 and TODO 14)

    // TODO 24: Check that the service kept nothing, then display the token usage
    // Hint: call responsesClient.GetResponseAsync(response.ResponseId) in a try block (keep the OPENAI001 pragma): it throws a
    //       ClientResultException with Status 404 when the response is not stored. Then
    //       ColoredConsole.WriteInfoLine($"Response {response.ResponseId} stored by the service: {(stored ? "yes" : "no (404)")}") and WriteTokenUsage(response)
}

#endregion

#region Scenario 6: Hosted file search - the service stores, chunks, embeds and searches the FAQ
// Bonus scenario (optional): the lab is completed without it; with every bonus scenario, it is completed with its bonus (Done+).

if (ShouldRunScenario(6))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 6: Hosted file search - the service stores, chunks, embeds and searches the FAQ ===");

    // TODO 25: Get the clients of the files and of the vector stores of the service, load the FAQ entries,
    // and prepare the list of the uploaded files and the id of the vector store (for the clean-up)
    // Hint: OpenAIFileClient fileClient = client.GetOpenAIFileClient();
    //       ServiceVectorStores.VectorStoreClient vectorStoreClient = client.GetVectorStoreClient();
    //       List<FaqRecord> faqEntries = await FaqData.LoadAsync(); List<string> fileIds = []; string? serviceVectorStoreId = null;
    // Note: these clients are flagged experimental (OPENAI001): the pragma covers the whole scenario.
#pragma warning disable OPENAI001

    // TODO 26: In a try block, upload one document per FAQ entry, then display the number of documents uploaded
    // Hint: foreach entry: OpenAIFile file = await fileClient.UploadFileAsync(BinaryData.FromString(FaqData.ToDocument(entry)), $"{entry.Id}.md", FileUploadPurpose.Assistants);
    //       fileIds.Add(file.Id);  then ColoredConsole.WritePrimaryLogLine($"Uploaded {fileIds.Count} FAQ documents to the service")

    // TODO 27: Create the vector store of the service with these files (it expires 1 day after its last use), wait until it is indexed, and display it
    // Hint: ServiceVectorStores.VectorStoreCreationOptions creationOptions = new() { Name = "lab07-sav-faq",
    //           ExpirationPolicy = new ServiceVectorStores.VectorStoreExpirationPolicy(ServiceVectorStores.VectorStoreExpirationAnchor.LastActiveAt, 1) };
    //       fileIds.ForEach(creationOptions.FileIds.Add);
    //       ServiceVectorStores.VectorStore serviceVectorStore = await vectorStoreClient.CreateVectorStoreAsync(creationOptions); serviceVectorStoreId = serviceVectorStore.Id;
    //       serviceVectorStore = await HostedFileSearch.WaitUntilIndexedAsync(vectorStoreClient, serviceVectorStoreId).WithSpinner("Indexing the FAQ in the vector store of the service");
    //       WriteServiceVectorStore(serviceVectorStore);  (provided in RagConsole.cs)

    // TODO 28: Create the agent with the hosted file search tool (Agents.HostedFaq)
    // Hint: AIAgent hostedFaqAgent = client.GetResponsesClient().AsAIAgent(model: settings.ChatDeploymentName, instructions: ..., name: ...,
    //           tools: [new HostedFileSearchTool { Inputs = [new HostedVectorStoreContent(serviceVectorStoreId)], MaximumResultCount = 3 }]);

    // TODO 29: Ask Questions.BrokenItem and ask the service to return the chunks it found, then display the file search, the answer and the usage
    // Hint: ChatClientAgentRunOptions runOptions = new(new ChatOptions { RawRepresentationFactory = _ => new CreateResponseOptions
    //           { IncludedProperties = { IncludedResponseProperty.FileSearchCallResults } } });
    //       AgentResponse response = await hostedFaqAgent.RunAsync(question, options: runOptions).WithSpinner("Running agent with the hosted file search");
    //       WriteFileSearch(response); ColoredConsole.WritePrimaryLogLine($"Agent: {response.Text}"); WriteTokenUsage(response);

    // TODO 30: In the finally block, delete the vector store and the documents from the service, and say so
    // Hint: await vectorStoreClient.DeleteVectorStoreAsync(serviceVectorStoreId) (when not null); await fileClient.DeleteFileAsync(fileId) for each file;
    //       ColoredConsole.WriteInfoLine($"Deleted the vector store and the {fileIds.Count} documents from the service")

#pragma warning restore OPENAI001
}

#endregion
