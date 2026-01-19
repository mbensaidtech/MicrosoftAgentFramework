using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.MongoDB;
using MongoDB.Driver;
using AIAgentsBackend.Models.VectorStore;

namespace AIAgentsBackend.Services.VectorStore.Base;

/// <summary>
/// Base class for policy vector store services.
/// Provides common functionality for initializing and searching policy documents.
/// </summary>
public abstract class PolicyVectorStoreServiceBase
{
    private readonly IMongoDatabase _database;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly MongoVectorStore _vectorStore;
    private readonly ILogger _logger;

    /// <summary>
    /// The MongoDB collection name for this vector store.
    /// </summary>
    protected abstract string CollectionName { get; }

    /// <summary>
    /// The path to the JSON data file.
    /// </summary>
    protected abstract string DataFilePath { get; }

    /// <summary>
    /// Display name for logging purposes.
    /// </summary>
    protected abstract string ServiceName { get; }

    protected PolicyVectorStoreServiceBase(
        IMongoDatabase database,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        ILogger logger)
    {
        _database = database;
        _embeddingGenerator = embeddingGenerator;
        _logger = logger;
        _vectorStore = new MongoVectorStore(database, new MongoVectorStoreOptions
        {
            EmbeddingGenerator = embeddingGenerator
        });
    }

    /// <summary>
    /// Initializes the vector store with policy data.
    /// Creates the collection, indexes, and generates embeddings for all sections.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[{ServiceName}] Starting initialization...", ServiceName);

        var document = await LoadPolicyDocumentAsync();
        var records = ConvertToRecords(document);

        _logger.LogInformation("[{ServiceName}] Loaded {Count} sections from {DocumentId}",
            ServiceName, records.Count, document.DocumentId);

        var collection = GetCollection();

        // Ensure collection and indexes exist
        _logger.LogInformation("[{ServiceName}] Creating collection and indexes...", ServiceName);
        await collection.EnsureCollectionExistsAsync(cancellationToken);

        // Upsert records
        await UpsertRecordsAsync(collection, records, cancellationToken);

        _logger.LogInformation("[{ServiceName}] Initialization complete with {Count} sections",
            ServiceName, records.Count);
    }

    /// <summary>
    /// Searches for policy sections similar to the given query.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="topK">Number of results to return (default: 3).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching policy sections with scores.</returns>
    public async Task<IReadOnlyList<VectorSearchResult<PolicySectionRecord>>> SearchAsync(
        string query,
        int topK = 3,
        CancellationToken cancellationToken = default)
    {
        var collection = GetCollection();

        var options = new Microsoft.Extensions.VectorData.VectorSearchOptions<PolicySectionRecord>
        {
            IncludeVectors = false
        };

        var results = await collection.SearchAsync(query, topK, options, cancellationToken).ToListAsync(cancellationToken);

        _logger.LogDebug("[{ServiceName}] Search for '{Query}' returned {Count} results",
            ServiceName, query, results.Count);

        return results;
    }

    /// <summary>
    /// Searches and returns formatted string results.
    /// </summary>
    public async Task<List<string>> SearchFormattedAsync(
        string query,
        int topK = 3,
        CancellationToken cancellationToken = default)
    {
        var results = await SearchAsync(query, topK, cancellationToken);
        return results
            .Select(r => r.Record.ToString(r.Score))
            .ToList();
    }

    /// <summary>
    /// Gets the vector collection for this service.
    /// </summary>
    private VectorStoreCollection<string, PolicySectionRecord> GetCollection()
    {
        return _vectorStore.GetCollection<string, PolicySectionRecord>(CollectionName);
    }

    /// <summary>
    /// Loads the policy document from the JSON file.
    /// </summary>
    private async Task<PolicyDocument> LoadPolicyDocumentAsync()
    {
        var fullPath = Path.Combine(AppContext.BaseDirectory, DataFilePath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Policy data file not found: {fullPath}");
        }

        var json = await File.ReadAllTextAsync(fullPath);

        return JsonSerializer.Deserialize<PolicyDocument>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException($"Failed to deserialize policy document from {fullPath}");
    }

    /// <summary>
    /// Converts a policy document to vector store records.
    /// </summary>
    private static List<PolicySectionRecord> ConvertToRecords(PolicyDocument document)
    {
        return document.Sections.Select(section => new PolicySectionRecord
        {
            Id = $"{document.DocumentId}-{section.Id}",
            DocumentId = document.DocumentId,
            Category = document.Category,
            SectionId = section.Id,
            Title = section.Title,
            Content = section.Content
        }).ToList();
    }

    /// <summary>
    /// Upserts records into the vector store with progress logging.
    /// </summary>
    private async Task UpsertRecordsAsync(
        VectorStoreCollection<string, PolicySectionRecord> collection,
        List<PolicySectionRecord> records,
        CancellationToken cancellationToken)
    {
        var totalCount = records.Count;
        var processedCount = 0;

        foreach (var record in records)
        {
            await collection.UpsertAsync(record, cancellationToken);
            processedCount++;
            _logger.LogDebug("[{ServiceName}] Generated embedding {Processed}/{Total}",
                ServiceName, processedCount, totalCount);
        }

        _logger.LogInformation("[{ServiceName}] Upserted {Count} records with embeddings",
            ServiceName, totalCount);
    }
}
