# Lab 07 - Agentic RAG with Vector Store

## Objective

In this lab, you will build an **Agentic RAG** (Retrieval-Augmented Generation) system using:
- **MongoDB Atlas Vector Search** as the vector store
- **Azure OpenAI Embeddings** for semantic search
- **AI Agent with Function Tools** to search the FAQ database

## What You Will Learn

- How to create and populate a vector store with embeddings
- How to use `VectorStoreCollection` and `VectorSearchResult`
- How to implement semantic search using embeddings
- How to expose vector search as an AI Function Tool
- How to build an Agentic RAG system

## Prerequisites

- .NET 10 SDK installed
- Azure OpenAI resource with:
  - A **Chat deployment** (e.g., gpt-4o)
  - An **Embedding deployment** (e.g., text-embedding-3-small)
- MongoDB Atlas cluster or local MongoDB with Atlas Vector Search
- Azure CLI logged in (`az login`) for DefaultAzureCredential

## Project Structure

```
Lab07-AgenticRAG-VectorStore/
├── README.md
├── Start/                              <-- Your working folder
│   ├── Program.cs                      <-- Complete the TODOs here
│   ├── Services/
│   │   └── FaqVectorStoreService.cs    <-- Implement all methods here
│   ├── Tools/
│   │   └── SearchTools.cs              <-- Implement SearchFaqAsync here
│   ├── Models/
│   │   └── FaqRecord.cs                <-- Provided (vector store model)
│   ├── Data/
│   │   └── sav-faq.json                <-- FAQ data to load
│   ├── Configuration/
│   │   ├── AzureOpenAISettings.cs
│   │   └── MongoDbConfiguration.cs
│   ├── ConfigurationHelper.cs
│   ├── appsettings.json
│   └── AgenticRAG.csproj
└── Solution/                           <-- Reference solution
    └── ...
```

## Instructions

### Step 1: Configure your settings

Open `Start/appsettings.json` and update the values:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
    "ChatDeploymentName": "YOUR-CHAT-DEPLOYMENT",
    "EmbeddingDeploymentName": "YOUR-EMBEDDING-DEPLOYMENT",
    "APIKey": ""
  },
  "MongoDb": {
    "ConnectionString": "mongodb+srv://...",
    "DatabaseName": "AgenticRAG"
  }
}
```

#### Authentication Options

The labs support two authentication methods:

1. **API Key Authentication** (recommended for local development):
   - Set `APIKey` in `appsettings.json` with your Azure OpenAI API key

2. **DefaultAzureCredential** (for Azure-hosted apps):
   - Leave `APIKey` empty
   - Requires Azure CLI login (`az login`) or managed identity

The Solution folder includes the conditional logic to handle both methods.

### Step 2: Implement FaqVectorStoreService

Open `Start/Services/FaqVectorStoreService.cs` and implement all methods.

---

#### GetCollection Method

| Description | Hints |
|-------------|-------|
| Return the vector collection from the store | Use `_vectorStore.GetCollection<string, FaqRecord>(CollectionName)` |

---

#### LoadFaqDataAsync Method

| Step | Description | Hints |
|------|-------------|-------|
| **Step 1** | Build the full path to the JSON file | Use `Path.Combine(AppContext.BaseDirectory, FaqDataPath)` |
| **Step 2** | Read the JSON file | Use `File.ReadAllTextAsync(path)` |
| **Step 3** | Deserialize the JSON | Use `JsonSerializer.Deserialize<List<FaqRecord>>(json, options)` |

**Example:**
```csharp
var faqJsonPath = Path.Combine(AppContext.BaseDirectory, FaqDataPath);
var faqJson = await File.ReadAllTextAsync(faqJsonPath);

return JsonSerializer.Deserialize<List<FaqRecord>>(faqJson, new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
}) ?? [];
```

---

#### UpsertFaqRecordsAsync Method

| Step | Description | Hints |
|------|-------------|-------|
| **Step 1** | Get total count | Use `faqList.Count` |
| **Step 2** | Create spinner | Use `new ConsoleSpinner($"Generating embeddings (0/{totalCount})")` |
| **Step 3** | Loop and upsert | Use `await collection.UpsertAsync(record)` |
| **Step 4** | Update progress | Use `spinner.UpdateMessage(...)` |

**Example:**
```csharp
var totalCount = faqList.Count;
var processedCount = 0;

using var spinner = new ConsoleSpinner($"Generating embeddings (0/{totalCount})");

foreach (var record in faqList)
{
    await collection.UpsertAsync(record);
    processedCount++;
    spinner.UpdateMessage($"Generating embeddings ({processedCount}/{totalCount})");
}
```

---

#### InitializeAsync Method

| Step | Description | Hints |
|------|-------------|-------|
| **Step 1** | Load FAQ data | Call `LoadFaqDataAsync()` |
| **Step 2** | Log count | Use `ColoredConsole.WriteInfoLine($"Loaded {count} FAQ entries")` |
| **Step 3** | Get collection | Call `GetCollection()` |
| **Step 4** | Ensure collection exists | Use `collection.EnsureCollectionExistsAsync().WithSpinner(...)` |
| **Step 5** | Upsert records | Call `UpsertFaqRecordsAsync(collection, faqList)` |
| **Step 6** | Log completion | Use `ColoredConsole.WriteInfoLine(...)` |

**Example:**
```csharp
var faqList = await LoadFaqDataAsync();
ColoredConsole.WriteInfoLine($"Loaded {faqList.Count} FAQ entries");

var collection = GetCollection();

await collection.EnsureCollectionExistsAsync()
    .WithSpinner("Creating collection and indexes");

await UpsertFaqRecordsAsync(collection, faqList);

ColoredConsole.WriteInfoLine($"Vector store initialized with {faqList.Count} FAQ entries");
```

---

#### SearchAsync Method

| Step | Description | Hints |
|------|-------------|-------|
| **Step 1** | Get collection | Call `GetCollection()` |
| **Step 2** | Create search options | Set `IncludeVectors = false` |
| **Step 3** | Execute search | Use `collection.SearchAsync(query, topK, options).ToListAsync()` |

**Example:**
```csharp
var collection = GetCollection();

var options = new VectorSearchOptions<FaqRecord>
{
    IncludeVectors = false
};

var results = await collection.SearchAsync(query, topK, options).ToListAsync();
return results;
```

---

### Step 3: Implement SearchTools

Open `Start/Tools/SearchTools.cs` and implement the `SearchFaqAsync` method.

| Step | Description | Hints |
|------|-------------|-------|
| **Step 1** | Call the service | Use `_faqService.SearchAsync(question, topK)` |
| **Step 2** | Transform results | Use LINQ `.Select(r => r.Record.ToString(r.Score))` |
| **Step 3** | Return list | Use `.ToList()` |

**Example:**
```csharp
var results = await _faqService.SearchAsync(question, topK);

return results
    .Select(r => r.Record.ToString(r.Score))
    .ToList();
```

---

### Step 4: Implement Program.cs Scenarios

Open `Start/Program.cs` and implement all three scenarios.

---

#### Scenario 1: Initialize Vector Store

| Description | Hints |
|-------------|-------|
| Initialize the vector store with FAQ data | Call `await faqService.InitializeAsync()` |

---

#### Scenario 2: Direct Vector Search (No Agent)

| Step | Description | Hints |
|------|-------------|-------|
| **Step 1** | Call search | Use `searchTools.SearchFaqAsync("your question", topK: 3).WithSpinner(...)` |
| **Step 2** | Display results | Loop with `foreach` and `ColoredConsole.WritePrimaryLogLine(result)` |

**Example:**
```csharp
var results = await searchTools.SearchFaqAsync("How can I contact support?", topK: 3)
    .WithSpinner("Searching FAQ");

foreach (var result in results)
{
    ColoredConsole.WritePrimaryLogLine(result);
}
```

---

#### Scenario 3: Agentic RAG (Agent with Search Tool)

| Step | Description | Hints |
|------|-------------|-------|
| **Step 1** | Create agent | Use `client.GetChatClient(...).CreateAIAgent(instructions:, name:, tools:)` |
| **Step 2** | Run agent | Use `agent.RunAsync("question").WithSpinner(...)` |
| **Step 3** | Display response | Use `Console.WriteLine(response)` |
| **Step 4** | Display tokens | Use `response.Usage` properties |

**Example for creating the agent:**
```csharp
AIAgent faqAgent = client
    .GetChatClient(settings.ChatDeploymentName)
    .CreateAIAgent(
        instructions: "You are a helpful assistant that can answer questions about the FAQ.",
        name: "FAQAgent",
        tools: [AIFunctionFactory.Create(searchTools.SearchFaqAsync, "search_faq")]
    );
```

**Example for running and displaying:**
```csharp
AgentRunResponse response = await faqAgent.RunAsync("How can I contact support?")
    .WithSpinner("Agent is thinking");
Console.WriteLine(response);

ColoredConsole.WriteDividerLine();
ColoredConsole.WritePrimaryLogLine("Token Usage: ");
ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {response.Usage?.InputTokenCount}");
ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {response.Usage?.OutputTokenCount}");
ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {response.Usage?.TotalTokenCount}");
```

---

### Step 5: Run and Test

**First, run Scenario 1 to initialize the vector store:**
```bash
cd Start
# Edit Program.cs to set scenariosToRun = [1];
dotnet run
```

**Then, run Scenarios 2 and 3 to test the search:**
```bash
# Edit Program.cs to set scenariosToRun = [2, 3];
dotnet run
```

**Or run the Solution:**
```bash
cd Solution
dotnet run
```

## Key Concepts

| Concept | Description |
|---------|-------------|
| `MongoVectorStore` | MongoDB vector store from Semantic Kernel |
| `VectorStoreCollection` | Collection of vector-indexed documents |
| `VectorSearchResult<T>` | Search result with `Record` and `Score` |
| `[VectorStoreKey]` | Attribute marking the document key |
| `[VectorStoreData]` | Attribute marking searchable data fields |
| `[VectorStoreVector]` | Attribute marking the embedding field |
| `IEmbeddingGenerator` | Interface for generating embeddings |
| `AIFunctionFactory.Create` | Creates an AI function from a method |

## Namespaces Reference

| Namespace | Purpose |
|-----------|---------|
| `Microsoft.Extensions.VectorData` | Vector store abstractions |
| `Microsoft.SemanticKernel.Connectors.MongoDB` | MongoDB vector store |
| `Microsoft.Extensions.AI` | Embedding generator, AI extensions |
| `Microsoft.Agents.AI` | AI Agent, AIFunctionFactory |
| `CommonUtilities` | ColoredConsole, ConsoleSpinner |

## Expected Output

**Scenario 1 (Initialize):**
```
Loaded 20 FAQ entries
Creating collection and indexes...
Generating embeddings (20/20)
Vector store initialized with 20 FAQ entries
```

**Scenario 2 (Direct Search):**
```
=== Scenario 2: Search Vector Store for FAQ Data ===
Q: How do I contact customer support?
A: You can reach our customer support via email at support@example.com...
Score: 0.8542
```

**Scenario 3 (Agentic RAG):**
```
=== Scenario 3: Search Vector Store for FAQ Data Using Agent ===
Based on the FAQ, you can contact customer support through:
- Email: support@example.com
- Phone: +33 1 23 45 67 89 (Mon-Fri 9am-6pm)
- Live chat on the website
────────────────────────────────────────────────────
Token Usage: 
  Input tokens: 523
  Output tokens: 87
  Total tokens: 610
```

## Provided Files

The following files are provided and should not be modified:

- `AgenticRAG.csproj` - Project file
- `ConfigurationHelper.cs` - Configuration loading
- `Configuration/` - Settings classes
- `Models/FaqRecord.cs` - Vector store model
- `Data/sav-faq.json` - FAQ data

The following files should be modified:

- `appsettings.json` - Update with your settings
- `Services/FaqVectorStoreService.cs` - Implement all methods
- `Tools/SearchTools.cs` - Implement SearchFaqAsync
- `Program.cs` - Implement all scenarios

## Useful Links

- [Using the MongoDB Vector Store connector](https://learn.microsoft.com/en-us/semantic-kernel/concepts/vector-store-connectors/out-of-the-box-connectors/mongodb-connector?pivots=programming-language-csharp)

## Solution

If you get stuck, check the complete solution in the `Solution/` folder.
