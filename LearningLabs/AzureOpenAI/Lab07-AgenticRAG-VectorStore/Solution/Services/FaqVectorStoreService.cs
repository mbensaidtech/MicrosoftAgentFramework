using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.MongoDB;
using MongoDB.Driver;

using CommonUtilities;
using AgenticRAG.Models;

namespace AgenticRAG.Services;

/// <summary>
/// Service for managing FAQ data in a MongoDB vector store.
/// </summary>
public class FaqVectorStoreService
{
    private readonly IMongoDatabase _database;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly MongoVectorStore _vectorStore;
    private const string CollectionName = "sav-faq";
    private const string FaqDataPath = "Data/sav-faq.json";

    public FaqVectorStoreService(
        IMongoDatabase database,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
    {
        _database = database;
        _embeddingGenerator = embeddingGenerator;
        _vectorStore = new MongoVectorStore(database, new MongoVectorStoreOptions
        {
            EmbeddingGenerator = embeddingGenerator
        });
    }

    /// <summary>
    /// Initializes the vector store with FAQ data.
    /// Creates the collection, indexes, and generates embeddings for all FAQ entries.
    /// </summary>
    public async Task InitializeAsync()
    {
        var faqList = await LoadFaqDataAsync();
        ColoredConsole.WriteInfoLine($"Loaded {faqList.Count} FAQ entries");

        var collection = GetCollection();

        // Ensure collection and indexes exist
        await collection.EnsureCollectionExistsAsync()
            .WithSpinner("Creating collection and indexes");

        // Upsert FAQ records with progress
        await UpsertFaqRecordsAsync(collection, faqList);

        ColoredConsole.WriteInfoLine($"Vector store initialized with {faqList.Count} FAQ entries");
    }

    /// <summary>
    /// Searches for FAQ entries similar to the given query.
    /// </summary>
    public async Task<IReadOnlyList<VectorSearchResult<FaqRecord>>> SearchAsync(
        string query,
        int topK = 3)
    {
        var collection = GetCollection();
        
        var options = new Microsoft.Extensions.VectorData.VectorSearchOptions<FaqRecord>
        {
            IncludeVectors = false
        };
        
        var results = await collection.SearchAsync(query, topK, options).ToListAsync();
        return results;
    }

    /// <summary>
    /// Gets the FAQ vector collection.
    /// </summary>
    private VectorStoreCollection<string, FaqRecord> GetCollection()
    {
        return _vectorStore.GetCollection<string, FaqRecord>(CollectionName);
    }

    /// <summary>
    /// Loads FAQ data from the JSON file.
    /// </summary>
    private async Task<List<FaqRecord>> LoadFaqDataAsync()
    {
        var faqJsonPath = Path.Combine(AppContext.BaseDirectory, FaqDataPath);
        var faqJson = await File.ReadAllTextAsync(faqJsonPath);
        
        return JsonSerializer.Deserialize<List<FaqRecord>>(faqJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? [];
    }

    /// <summary>
    /// Upserts FAQ records into the vector store with progress indication.
    /// </summary>
    private async Task UpsertFaqRecordsAsync(
        VectorStoreCollection<string, FaqRecord> collection,
        List<FaqRecord> faqList)
    {
        var totalCount = faqList.Count;
        var processedCount = 0;

        using var spinner = new ConsoleSpinner($"Generating embeddings (0/{totalCount})");
        
        foreach (var record in faqList)
        {
            await collection.UpsertAsync(record);
            processedCount++;
            spinner.UpdateMessage($"Generating embeddings ({processedCount}/{totalCount})");
        }
    }
}
