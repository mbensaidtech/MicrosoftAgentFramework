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
// Hint: client.GetEmbeddingClient(settings.EmbeddingDeploymentName).AsIEmbeddingGenerator()
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

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Semantic search - without an agent ===");

    // TODO 10: Search the 3 FAQ entries closest to a question and display them with WriteSearchResult(rank, result)
    // Hint: string question = "Is there a phone number I can call for help?"; (shares no word with faq-010, which it is about)
    //       ColoredConsole.WriteSecondaryLogLine($"Question: {question}");
    //       await foreach (VectorSearchResult<FaqRecord> result in faqCollection.SearchAsync(question, top: 3)) { ... }
}

#endregion

#region Scenario 3: Agentic RAG - the agent decides when to search the FAQ (function tool)

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: Agentic RAG - the agent searches the FAQ with a function tool ===");

    // TODO 11: Turn the semantic search of FaqSearchTool into a function tool named "search_faq"
    // Hint: FaqSearchTool faqSearchTool = new(faqCollection);
    //       AITool searchFaqTool = AIFunctionFactory.Create(faqSearchTool.SearchFaqAsync, "search_faq");

    // TODO 12: Create the agent with the tool (see the README for the instructions)
    // Hint: chatClient.AsAIAgent(instructions: "...", name: "FaqAgent", tools: [searchFaqTool])
    // AIAgent faqAgent = ...

    // TODO 13: Run the agent with a question, then display the tool calls and the answer
    // Hint: string question = "I received a broken item yesterday. What should I do?";
    //       ColoredConsole.WriteSecondaryLogLine($"Question: {question}");
    //       AgentResponse response = await faqAgent.RunAsync(question).WithSpinner("Running agent");
    //       WriteToolCalls(response); then ColoredConsole.WritePrimaryLogLine($"Agent: {response.Text}")

    // TODO 14: Display token usage
    // Hint: WriteTokenUsage(response)
}

#endregion

#region Scenario 4: RAG with TextSearchProvider - the FAQ is searched before every model call (context provider)

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

    // TODO 17: Create the agent with ChatClientAgentOptions: Name, ChatOptions.Instructions (see the README),
    // AIContextProviders = [new TextSearchProvider(SearchFaqForProviderAsync, textSearchOptions)]
    // and a ChatHistoryProvider that keeps the provider messages out of the stored history (see the README)
    // AIAgent ragAgent = ...

    // TODO 18: Create a session and ask a first question in it (no spinner: the provider writes to the console during the run)
    // Hint: AgentSession session = await ragAgent.CreateSessionAsync();
    //       string firstQuestion = "I want to send back a jacket I bought last week. How does it work?";
    //       ColoredConsole.WriteSecondaryLogLine($"Question: {firstQuestion}");
    //       AgentResponse firstResponse = await ragAgent.RunAsync(firstQuestion, session);
    //       ColoredConsole.WritePrimaryLogLine($"Agent: {firstResponse.Text}");
    //       WriteTokenUsage(firstResponse, "Token Usage (first question):")

    // TODO 19: Ask a follow-up question in the same session, and display the answer and its token usage
    // Hint: string followUpQuestion = "And how long until I get my money back?";
    //       same code as TODO 18, with WriteTokenUsage(followUpResponse, "Token Usage (follow-up):")
}

#endregion
