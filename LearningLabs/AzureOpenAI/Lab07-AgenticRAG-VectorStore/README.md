# Lab 07 - Agentic RAG with a Vector Store

## Objective

In this lab, you will build a **RAG** (Retrieval-Augmented Generation) system with **Microsoft Agent Framework** and **Azure OpenAI**: an agent that answers customer questions from the FAQ of an online shop, instead of from its own knowledge.

The FAQ is stored in a **vector store**: each entry is turned into a vector (an *embedding*) by an Azure OpenAI embedding model, so that a question finds the entries with the closest **meaning**, not the same words. You will then give this search to an agent in two ways:

1. **Agentic RAG** — the search is a **function tool**: the agent decides when to search, and with which question.
2. **RAG with `TextSearchProvider`** — the search is an **AI context provider** of Agent Framework: it runs before every model call and injects the results into the context.

## What You Will Learn

- How to get an `IEmbeddingGenerator` from an Azure OpenAI embedding deployment
- How to describe records for a vector store (`[VectorStoreKey]`, `[VectorStoreData]`, `[VectorStoreVector]`) and let the store embed them
- How to fill a `VectorStoreCollection` (`EnsureCollectionExistsAsync`, `UpsertAsync`) and search it (`SearchAsync`, `VectorSearchResult<T>`)
- How to expose a semantic search as a function tool of an agent (`AIFunctionFactory.Create`)
- How to add RAG to an agent with `TextSearchProvider` and `ChatClientAgentOptions.AIContextProviders`
- How to compare both approaches: tool calls, injected context, token usage

## Prerequisites

- .NET 10 SDK or later
- An Azure OpenAI (or Microsoft Foundry) resource with **two deployments**:
  - a chat model (for example `gpt-4o-mini` or `gpt-5.4-mini`)
  - an embedding model (for example `text-embedding-3-small`)
- One of the following for authentication:
  - the resource **API key**, or
  - Azure CLI logged in (`az login`) with the **Cognitive Services OpenAI User** role on the resource
- Lab 01 (client creation, `AsAIAgent()`, `AgentResponse`) and Lab 03 (function tools) completed

No database to install: the vector store of this lab is in memory.

## Project Structure

```
Lab07-AgenticRAG-VectorStore/
├── README.md
├── Start/                          <-- Your working folder
│   ├── Program.cs                  <-- Complete the TODOs here
│   ├── Models/
│   │   └── FaqRecord.cs            <-- The record stored in the vector store (provided)
│   ├── Tools/
│   │   └── FaqSearchTool.cs        <-- The function tool of scenario 3 (provided)
│   ├── Data/
│   │   └── sav-faq.json            <-- The 20 FAQ entries
│   ├── FaqData.cs                  <-- Reads sav-faq.json (provided)
│   ├── RagConsole.cs               <-- Display helpers (provided)
│   ├── ConfigurationHelper.cs
│   ├── AzureOpenAISettings.cs
│   ├── appsettings.json
│   └── AgenticRAG.csproj
└── Solution/                       <-- Reference solution
    └── ...
```

## Instructions

### Step 1: Configure your settings

Settings are read, in this order (last wins): `appsettings.json` → **user secrets** → **environment variables**.

1. Open `Start/appsettings.json` and set the non-secret values:

   ```json
   {
     "AzureOpenAI": {
       "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
       "ChatDeploymentName": "YOUR-DEPLOYMENT-NAME",
       "EmbeddingDeploymentName": "YOUR-EMBEDDING-DEPLOYMENT-NAME"
     }
   }
   ```

   Use the resource endpoint shown in the Azure portal. The `/openai/v1/` suffix required by the v1 API is added for you.
   `*.openai.azure.com`, `*.cognitiveservices.azure.com` and `*.services.ai.azure.com` endpoints all work.
   `EmbeddingDeploymentName` is new in this lab: the name of your **embedding** deployment (user secret `AzureOpenAI:EmbeddingDeploymentName` or environment variable `AzureOpenAI__EmbeddingDeploymentName` also work).

2. Choose an authentication method:

   - **API key** (simplest for local development) — store it as a user secret, **never** in `appsettings.json`:

     ```bash
     cd Start
     dotnet user-secrets set "AzureOpenAI:APIKey" "<your-api-key>"
     ```

     The user secrets id is shared by all the labs: if you already did it for a previous lab, there is nothing to do.
     You can also use the environment variable `AzureOpenAI__APIKey`.

   - **Microsoft Entra ID** — leave `APIKey` unset and run `az login`. `DefaultAzureCredential` picks up your Azure CLI identity.

3. **Or use the dashboard**: the *Azure OpenAI settings* of the optional [Lab Bench dashboard](../../../Dashboard/README.md#azure-openai-settings) write the endpoint, the chat deployment and the API key to the same user secrets. The embedding deployment is not part of the form: set it in `appsettings.json` or as a user secret.

### Step 2: Look at the provided files

- `Models/FaqRecord.cs` — a FAQ entry as the vector store sees it. The attributes define the schema of the collection: `[VectorStoreKey]` on `Id`, `[VectorStoreData]` on `Question` and `Answer`, and `[VectorStoreVector]` on `Embedding`. `Embedding` is a **string** (question + answer): when a record is upserted, the store generates its vector itself with the `EmbeddingGenerator` you give it, and when you search with a string, the store embeds the question the same way. The same class reads `Data/sav-faq.json`.
- `Tools/FaqSearchTool.cs` — the function tool of scenario 3: `SearchFaqAsync(question, top)` searches the collection and returns the closest entries as text (id, score, question, answer). The `[Description]` attributes are what the model reads to decide when to call it.
- `FaqData.cs` — `LoadAsync()` reads the 20 entries of `Data/sav-faq.json` (an after-sales FAQ: returns, refunds, delivery, payment, warranty...).
- `RagConsole.cs` — `WriteSearchResult(rank, result)`, `WriteToolCalls(response)` and `WriteTokenUsage(response, heading)`. `Program.cs` imports them with `using static`, so you can call them directly.

### Step 3: Complete the Program.cs

Open `Start/Program.cs` and complete the TODOs:

---

#### Setup: Configuration, client, embedding generator and vector store

TODO 1 to 3 are the same as in Lab 01.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 1** | Create the `OpenAIClientOptions` for the Azure OpenAI v1 endpoint | • `new OpenAIClientOptions { Endpoint = ... }` <br> • `AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint)` (from `CommonUtilities`) returns the `.../openai/v1/` URI |
| **TODO 2** | Create the `OpenAIClient` | • API key: `new OpenAIClient(new ApiKeyCredential(settings.APIKey), clientOptions)` <br> • Entra ID: `new OpenAIClient(new BearerTokenPolicy(new DefaultAzureCredential(), "https://ai.azure.com/.default"), clientOptions)` <br> • Use a conditional on `string.IsNullOrWhiteSpace(settings.APIKey)` to support both |
| **TODO 3** | Get a `ChatClient` for the chat deployment | • `client.GetChatClient(settings.ChatDeploymentName)` |
| **TODO 4** | Get an embedding generator for the embedding deployment | • `IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator = client.GetEmbeddingClient(settings.EmbeddingDeploymentName).AsIEmbeddingGenerator();` <br> • `AsIEmbeddingGenerator()` wraps the `EmbeddingClient` of the OpenAI SDK into the `Microsoft.Extensions.AI` abstraction that the vector store understands |
| **TODO 5** | Create the vector store with the embedding generator | • `VectorStore vectorStore = new InMemoryVectorStore(new InMemoryVectorStoreOptions { EmbeddingGenerator = embeddingGenerator });` <br> • `InMemoryVectorStore` comes from `CommunityToolkit.VectorData.InMemory`; any other `Microsoft.Extensions.VectorData` store has the same API |
| **TODO 6** | Get the FAQ collection | • `VectorStoreCollection<string, FaqRecord> faqCollection = vectorStore.GetCollection<string, FaqRecord>("sav-faq");` <br> • `string` is the type of the key (`FaqRecord.Id`), `FaqRecord` the type of the records |

---

#### Scenario 1: Fill the vector store with the FAQ

The store is in memory: it starts empty at each run, so scenario 1 must run before the others (see `scenariosToRun` at the top of the file).

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 7** | Load the FAQ entries and display how many were loaded | • `List<FaqRecord> faqEntries = await FaqData.LoadAsync();` <br> • `ColoredConsole.WritePrimaryLogLine($"Loaded {faqEntries.Count} FAQ entries from {FaqData.FileName}");` |
| **TODO 8** | Create the collection if it does not exist | • `await faqCollection.EnsureCollectionExistsAsync();` |
| **TODO 9** | Upsert the entries, then display a confirmation | • `await faqCollection.UpsertAsync(faqEntries).WithSpinner("Generating the embeddings and indexing the FAQ");` <br> • One call: the store sends the `Embedding` text of the 20 records to the embedding model, then stores the records with their vectors <br> • `ColoredConsole.WriteInfoLine($"Vector store ready: {faqEntries.Count} FAQ entries indexed in the collection '{faqCollection.Name}'");` |

---

#### Scenario 2: Semantic search - without an agent

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 10** | Search the 3 closest entries and display them | • `string question = "Is there a phone number I can call for help?";` <br> • `ColoredConsole.WriteSecondaryLogLine($"Question: {question}");` <br> • `int rank = 0;` <br> • `await foreach (VectorSearchResult<FaqRecord> result in faqCollection.SearchAsync(question, top: 3)) { WriteSearchResult(++rank, result); }` <br> • The question shares no word with `faq-010` ("How do I contact customer support?"), yet it is the first result: the search compares meanings <br> • `result.Record` is the `FaqRecord`, `result.Score` its cosine similarity with the question (1 = same meaning) |

---

#### Scenario 3: Agentic RAG - the agent decides when to search the FAQ (function tool)

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 11** | Turn the search into a function tool | • `FaqSearchTool faqSearchTool = new(faqCollection);` <br> • `AITool searchFaqTool = AIFunctionFactory.Create(faqSearchTool.SearchFaqAsync, "search_faq");` |
| **TODO 12** | Create the agent with the tool | • `AIAgent faqAgent = chatClient.AsAIAgent(instructions: "You are the customer support assistant of an online shop. Answer only from the FAQ: call search_faq to find the relevant entries, answer from them and quote the id of the entries you used. If the FAQ does not cover the question, say so.", name: "FaqAgent", tools: [searchFaqTool]);` |
| **TODO 13** | Run the agent, then display the tool calls and the answer | • `string question = "I received a broken item yesterday. What should I do?";` <br> • `ColoredConsole.WriteSecondaryLogLine($"Question: {question}");` <br> • `AgentResponse response = await faqAgent.RunAsync(question).WithSpinner("Running agent");` <br> • `WriteToolCalls(response);` — the function calls of the run are kept in `response.Messages` <br> • `ColoredConsole.WritePrimaryLogLine($"Agent: {response.Text}");` <br> • Expected: `Tool called: search_faq(question: ...)`, then an answer built on `faq-005` (contact customer service within 48 hours, with photos) |
| **TODO 14** | Display token usage | • `WriteTokenUsage(response);` <br> • Total of the model calls of the run: the call that asks for the tool, then the call that answers with the tool result |

---

#### Scenario 4: RAG with TextSearchProvider - the FAQ is searched before every model call

`TextSearchProvider` is the RAG building block of Agent Framework: an **AI context provider** that calls your search function before each model call and adds the results to the context, as a message ("Additional Context ... Include citations..."). The model does not decide anything: the search always happens.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 15** | Write the search function of the provider | • Signature: `async Task<IEnumerable<TextSearchProvider.TextSearchResult>> SearchFaqForProviderAsync(string text, CancellationToken cancellationToken)` (a local function inside the scenario) <br> • `List<TextSearchProvider.TextSearchResult> results = [];` <br> • `await foreach (VectorSearchResult<FaqRecord> result in faqCollection.SearchAsync(text, top: 2, cancellationToken: cancellationToken)) { results.Add(new TextSearchProvider.TextSearchResult { SourceName = result.Record.Id, Text = $"Q: {result.Record.Question}\nA: {result.Record.Answer}", RawRepresentation = result }); }` <br> • `ColoredConsole.WriteWarningLine($"[TextSearchProvider] Search input: {text.ReplaceLineEndings(" \| ")}");` <br> • `ColoredConsole.WriteWarningLine($"[TextSearchProvider] Results: {string.Join(", ", results.Select(result => result.SourceName))}");` <br> • `return results;` <br> • `SourceName` becomes the "source document name" that the model is asked to cite |
| **TODO 16** | Configure the provider | • `TextSearchProviderOptions textSearchOptions = new() { SearchTime = TextSearchProviderOptions.TextSearchBehavior.BeforeAIInvoke, RecentMessageMemoryLimit = 2 };` <br> • `BeforeAIInvoke` (the default): search before every model call <br> • `RecentMessageMemoryLimit = 2`: the last 2 user messages of the session are added to the search input, so that a vague follow-up question is searched with its context |
| **TODO 17** | Create the agent with the provider | • `AIAgent ragAgent = chatClient.AsAIAgent(new ChatClientAgentOptions { Name = "FaqRagAgent", ChatOptions = new ChatOptions { Instructions = "You are the customer support assistant of an online shop. Answer only from the provided context and quote the id of the FAQ entries you used. Keep the answers short." }, AIContextProviders = [new TextSearchProvider(SearchFaqForProviderAsync, textSearchOptions)], ChatHistoryProvider = new InMemoryChatHistoryProvider(new InMemoryChatHistoryProviderOptions { StorageInputRequestMessageFilter = messages => messages.Where(message => message.GetAgentRequestMessageSourceType() != AgentRequestMessageSourceType.AIContextProvider && message.GetAgentRequestMessageSourceType() != AgentRequestMessageSourceType.ChatHistory) }) });` <br> • `AIContextProviders`: the providers called around each run <br> • The `ChatHistoryProvider` filter keeps the messages produced by the provider (the search results) out of the stored history: without it, every later question would send them again |
| **TODO 18** | Create a session and ask a first question | • `AgentSession session = await ragAgent.CreateSessionAsync();` <br> • `string firstQuestion = "I want to send back a jacket I bought last week. How does it work?";` <br> • `ColoredConsole.WriteSecondaryLogLine($"Question: {firstQuestion}");` <br> • `AgentResponse firstResponse = await ragAgent.RunAsync(firstQuestion, session);` (no `.WithSpinner()`: the provider writes to the console during the run) <br> • `ColoredConsole.WritePrimaryLogLine($"Agent: {firstResponse.Text}");` <br> • `WriteTokenUsage(firstResponse, "Token Usage (first question):");` <br> • Expected: the provider finds `faq-001` and the answer gives the 30 days and 'My Orders' |
| **TODO 19** | Ask a follow-up question in the same session | • `string followUpQuestion = "And how long until I get my money back?";` <br> • Same code as TODO 18 with `followUpQuestion`, `followUpResponse` and `WriteTokenUsage(followUpResponse, "Token Usage (follow-up):")` <br> • Expected: the search input contains both questions, the provider finds `faq-002`, and the answer gives 5-7 business days |

---

### Step 4: Run and Test

**Run the Start project (your implementation):**
```bash
cd Start
dotnet run
```

**Run the Solution (reference):**
```bash
cd Solution
dotnet run
```

To run only some scenarios, edit `scenariosToRun` at the top of `Program.cs` (for example `[1, 3]`). Keep scenario 1: it fills the vector store.

## How the two RAG approaches work

```
 Scenario 3 - function tool (agentic RAG)          Scenario 4 - TextSearchProvider (context provider)

 RunAsync(question)                                 RunAsync(question, session)
        │                                                  │
        ▼                                                  ▼
 model call 1: "I need search_faq(question)"        TextSearchProvider: search(question [+ recent messages])
        │                                                  │ results injected as a context message
        ▼                                                  ▼
 search_faq → vector search → FAQ entries           model call: answer from the context
        │
        ▼
 model call 2: answer from the tool result
```

| | Scenario 3 — function tool | Scenario 4 — `TextSearchProvider` |
|---|---|---|
| Who decides to search? | The model (it may also not search, or search twice) | Nobody: the search runs before every model call |
| Search query | Written by the model | The user message(s), as typed |
| Model calls per question | At least 2 | 1 |
| Where are the results? | In the tool result (`FunctionResultContent`) | In a context message (`Additional Context...`) |
| Good for | Assistants with several tools, questions that do not always need the knowledge base | Question answering over one knowledge base, predictable cost |

`TextSearchProviderOptions.SearchTime = OnDemandFunctionCalling` turns the provider into the tool of scenario 3 (a function named `Search` by default): the same class covers both approaches.

## Key Concepts

| Concept | Description |
|---------|-------------|
| Embedding | A vector of numbers that represents the meaning of a text; texts with close meanings have close vectors |
| `IEmbeddingGenerator<string, Embedding<float>>` | `Microsoft.Extensions.AI` abstraction of an embedding model; `AsIEmbeddingGenerator()` creates one from an OpenAI `EmbeddingClient` |
| `VectorStore` / `VectorStoreCollection<TKey, TRecord>` | `Microsoft.Extensions.VectorData` abstractions of a store and of a collection of typed records |
| `InMemoryVectorStore` | In-memory implementation (`CommunityToolkit.VectorData.InMemory`), used by the official RAG samples; `InMemoryVectorStoreOptions.EmbeddingGenerator` lets the store embed the records |
| `[VectorStoreKey]`, `[VectorStoreData]`, `[VectorStoreVector]` | Attributes that describe the record: key, stored data, vector (with its dimensions and distance function) |
| `EnsureCollectionExistsAsync()` / `UpsertAsync()` | Create the collection; insert or update records (the store generates the vectors of string vector properties) |
| `SearchAsync(value, top)` / `VectorSearchResult<T>` | Vector search from a value (a string, embedded by the store); results with `Record` and `Score` |
| RAG | Retrieval-Augmented Generation: retrieve the relevant documents, then let the model answer from them |
| Agentic RAG | The retrieval is a tool: the agent decides when to call it and with which query |
| `TextSearchProvider` | AI context provider that runs your search function before each model call and injects the results (or exposes it as a tool) |
| `TextSearchProviderOptions` | `SearchTime`, `RecentMessageMemoryLimit`, `ContextPrompt`, `CitationsPrompt`, `FunctionToolName`... |
| `ChatClientAgentOptions.AIContextProviders` | The context providers of an agent (this lab uses one; Lab 12 writes its own) |
| `AgentRequestMessageSourceType` | Where a message of a run comes from: `External` (the caller), `ChatHistory`, `AIContextProvider`; used to filter what the history stores |

## Namespaces Reference

| Namespace | Purpose |
|-----------|---------|
| `OpenAI` | `OpenAIClient`, `OpenAIClientOptions` |
| `OpenAI.Chat` | `ChatClient` and the `AsAIAgent()` extension methods |
| `System.ClientModel` / `System.ClientModel.Primitives` | `ApiKeyCredential` / `BearerTokenPolicy` |
| `Azure.Identity` | `DefaultAzureCredential` |
| `Microsoft.Extensions.AI` | `IEmbeddingGenerator`, `Embedding<float>`, `AsIEmbeddingGenerator()`, `AIFunctionFactory`, `AITool`, `ChatOptions`, `FunctionCallContent` |
| `Microsoft.Extensions.VectorData` | `VectorStore`, `VectorStoreCollection`, `VectorSearchResult`, the record attributes |
| `CommunityToolkit.VectorData.InMemory` | `InMemoryVectorStore`, `InMemoryVectorStoreOptions` |
| `Microsoft.Agents.AI` | `AIAgent`, `AgentResponse`, `AgentSession`, `ChatClientAgentOptions`, `TextSearchProvider`, `TextSearchProviderOptions`, `InMemoryChatHistoryProvider`, `AgentRequestMessageSourceType` |
| `System.ComponentModel` | `[Description]` (in `FaqSearchTool.cs`) |
| `CommonUtilities` | `ColoredConsole`, `WithSpinner()`, `AzureOpenAIEndpoint` |

## Expected Output

```
Endpoint: https://your-resource.openai.azure.com/
Chat deployment: your-chat-deployment
Embedding deployment: your-embedding-deployment
----------------------------------------
=== Scenario 1: Fill the vector store with the FAQ ===
Loaded 20 FAQ entries from Data/sav-faq.json
Vector store ready: 20 FAQ entries indexed in the collection 'sav-faq'
----------------------------------------
=== Scenario 2: Semantic search - without an agent ===
Question: Is there a phone number I can call for help?
1. faq-010 (score 0.5xxx): How do I contact customer support?
   You can reach our customer support via email at support@example.com, by phone at +33 1 23 45 67 89 ...
2. faq-004 (score 0.4xxx): Can I change or cancel my order after placing it?
   ...
3. faq-005 (score 0.3xxx): What should I do if I receive a damaged product?
   ...
----------------------------------------
=== Scenario 3: Agentic RAG - the agent searches the FAQ with a function tool ===
Question: I received a broken item yesterday. What should I do?
Tool called: search_faq(question: damaged product received)
Agent: Contact our customer service within 48 hours of delivery with photos of the damage. We will arrange a free return pickup
and send you a replacement or process a full refund, including shipping costs (faq-005).
----------------------------------------
Token Usage:
  Input tokens: 6xx
  Output tokens: 8x
  Total tokens: 7xx
----------------------------------------
=== Scenario 4: RAG with TextSearchProvider - the FAQ is searched before every model call ===
Question: I want to send back a jacket I bought last week. How does it work?
[TextSearchProvider] Search input: I want to send back a jacket I bought last week. How does it work?
[TextSearchProvider] Results: faq-001, faq-008
Agent: Go to 'My Orders', select the order and click 'Request Return', within 30 days of delivery; the jacket must be unused and in its original packaging (faq-001).
----------------------------------------
Token Usage (first question):
  Input tokens: 3xx
  Output tokens: 5x
  Total tokens: 4xx
Question: And how long until I get my money back?
[TextSearchProvider] Search input: I want to send back a jacket I bought last week. How does it work? | And how long until I get my money back?
[TextSearchProvider] Results: faq-002, faq-001
Agent: Refunds are processed within 5-7 business days after we receive and inspect the returned item, to your original payment method (faq-002).
----------------------------------------
Token Usage (follow-up):
  Input tokens: 4xx
  Output tokens: 4x
  Total tokens: 4xx
```

Answers, scores and token counts vary from one run to another and from one embedding model to another.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `'AzureOpenAI:Endpoint' is not configured` (or `EmbeddingDeploymentName`) | Replace the `YOUR-...` placeholders in `appsettings.json` (or use user secrets / environment variables) |
| `401 Unauthorized` with an API key | Check the key (user secret `AzureOpenAI:APIKey`) and that it belongs to the resource of the endpoint |
| `401` / `403` with Entra ID | Run `az login` and make sure your identity has the **Cognitive Services OpenAI User** role on the resource |
| `404 DeploymentNotFound` on scenario 1 | `EmbeddingDeploymentName` must be the name of an **embedding** deployment (not the model name) |
| Scenarios 2-4 find nothing, or `VectorStoreException` about the collection | Run scenario 1 first: the vector store is in memory and starts empty |
| The scores are all very close (0.7-0.9) | Normal with some embedding models: only the **order** of the results matters |
| Scenario 3: the agent answers without calling the tool | Check the `[Description]` of `SearchFaqAsync` and the instructions of TODO 12; the model decides from them |
| Scenario 3: the agent says it *encountered an error* while searching | The tool threw an exception (for example an empty collection): the run does not fail, the error is sent back to the model, which reports it |
| Scenario 4: no `[TextSearchProvider]` line | The provider is not attached: check `AIContextProviders = [...]` in `ChatClientAgentOptions` (TODO 17) |
| The `[TextSearchProvider]` lines are garbled | Remove `.WithSpinner()` from the runs of scenario 4 |

## Provided Files

The following files are provided and should not be modified:

- `AgenticRAG.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs` - Loads and validates the configuration
- `AzureOpenAISettings.cs` - Settings class for Azure OpenAI (chat and embedding deployments)
- `Models/FaqRecord.cs` - The record of the vector store
- `Tools/FaqSearchTool.cs` - The function tool of scenario 3
- `FaqData.cs` - Reads the FAQ file
- `RagConsole.cs` - Display helpers
- `Data/sav-faq.json` - The FAQ entries

The following files should be modified:

- `appsettings.json` - Update with your Azure OpenAI endpoint and deployments
- `Program.cs` - Complete the TODOs

### NuGet packages

| Package | Why |
|---------|-----|
| `Microsoft.Agents.AI.OpenAI` | Agent Framework + `AsAIAgent()` for OpenAI clients (brings `OpenAI`, `Microsoft.Extensions.AI` and `Microsoft.Extensions.VectorData.Abstractions`); `TextSearchProvider` is in `Microsoft.Agents.AI` |
| `Azure.Identity` | `DefaultAzureCredential` for Entra ID authentication |
| `Microsoft.Extensions.Configuration.*` | `appsettings.json`, user secrets and environment variables, bound to `AzureOpenAISettings` |
| `CommunityToolkit.VectorData.InMemory` | `InMemoryVectorStore`, the vector store of the official sample `AgentWithRAG_Step01_BasicTextRAG` |

## Going Further

- **A real vector database**: `InMemoryVectorStore` can be replaced by any `Microsoft.Extensions.VectorData` connector without changing the rest of the code; the official sample `AgentWithRAG_Step02_CustomVectorStoreRAG` uses Qdrant (`CommunityToolkit.VectorData.Qdrant`), and connectors exist for Azure AI Search, Azure Cosmos DB, PostgreSQL (pgvector), SQL Server, Redis... (see the `CommunityToolkit.VectorData.*` packages).
- **Chunking**: real documents are split into overlapping chunks before they are embedded (the sample above splits Markdown pages into 2000-character chunks with a 200-character overlap).
- **Filters**: `SearchAsync(value, top, new VectorSearchOptions<FaqRecord> { Filter = record => ... })` combines the vector search with a filter on the data properties (mark them `[VectorStoreData(IsIndexed = true)]` for stores that need an index).
- **On-demand search with the provider**: `TextSearchProviderOptions.SearchTime = TextSearchProviderOptions.TextSearchBehavior.OnDemandFunctionCalling` exposes the search as a function tool (`FunctionToolName`, `FunctionToolDescription`) instead of running it before every call.
- **Custom context format**: `ContextPrompt`, `CitationsPrompt` or `ContextFormatter` control the message that the provider injects.
- **Foundry RAG**: the official sample `AgentWithRAG_Step04_FoundryServiceRAG` uses the vector store service of Microsoft Foundry instead of a local store.
- **Your own context provider**: `TextSearchProvider` is one `AIContextProvider`; Lab 12 shows how to write yours.

## Coming from an older version of the lab?

| Before (preview) | Now (1.22.0) |
|------------------|--------------|
| `Azure.AI.OpenAI` / `AzureOpenAIClient` | `OpenAI` / `OpenAIClient` + Azure OpenAI v1 endpoint |
| `chatClient.CreateAIAgent(...)` | `chatClient.AsAIAgent(...)`, variables typed `AIAgent` |
| `AgentRunResponse` | `AgentResponse` |
| `Microsoft.SemanticKernel.Connectors.MongoDB` (preview), `MongoVectorStore` on MongoDB Atlas | `CommunityToolkit.VectorData.InMemory` (stable), `InMemoryVectorStore`, same `Microsoft.Extensions.VectorData` API |
| `Microsoft.Extensions.VectorData.Abstractions` 9.7.0 referenced explicitly | 10.10.0, transitive |
| `MongoDb` section (connection string with credentials) in `appsettings.json` | Removed: no database |
| `FaqVectorStoreService` and `SearchTools` methods to implement | The vector store calls are in `Program.cs`; `FaqSearchTool` is provided |
| `new VectorSearchOptions<FaqRecord> { IncludeVectors = false }`, `SearchAsync(query, topK, options).ToListAsync()` | `await foreach (VectorSearchResult<FaqRecord> result in collection.SearchAsync(query, top: 3))` (vectors are not returned by default) |
| `<NoWarn>MEAI001</NoWarn>`, `Microsoft.Extensions.Hosting` | Removed; `Microsoft.Extensions.Configuration.*` packages |
| API key in `appsettings.json` | API key in user secrets or environment variables |
| — | Scenario 4: RAG with `TextSearchProvider` (new in Agent Framework) |

## Useful Links

- [Microsoft Agent Framework documentation](https://learn.microsoft.com/agent-framework/overview/?pivots=programming-language-csharp)
- [Official RAG samples (`AgentWithRAG_Step01` to `Step05`)](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/AgentWithRAG)
- [Microsoft.Extensions.VectorData: vector stores](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-vectordata-overview)
- [Azure OpenAI embeddings](https://learn.microsoft.com/azure/ai-services/openai/how-to/embeddings)
- [Azure OpenAI v1 API](https://learn.microsoft.com/azure/foundry/openai/api-version-lifecycle)

## Solution

If you get stuck, check the complete solution in the `Solution/` folder.
