using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.MongoDB;
using MongoDB.Driver;
using AIAgentsBackend.Models.VectorStore;

namespace AIAgentsBackend.Services.VectorStore.Base;

/// <summary>
/// Base class for searching policy documents using vector embeddings.
/// </summary>
public abstract class PolicyVectorStoreServiceBase
{
    private readonly IMongoDatabase database;
    private readonly IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator;
    private readonly MongoVectorStore vectorStore;
    private readonly ILogger logger;

    /// <summary>
    /// MongoDB collection name for this policy type.
    /// </summary>
    protected abstract string CollectionName { get; }

    /// <summary>
    /// Path to the JSON file with policy data.
    /// </summary>
    protected abstract string DataFilePath { get; }

    /// <summary>
    /// Name shown in logs.
    /// </summary>
    protected abstract string ServiceName { get; }

    protected PolicyVectorStoreServiceBase(
        IMongoDatabase database,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        ILogger logger)
    {
        this.database = database;
        this.embeddingGenerator = embeddingGenerator;
        this.logger = logger;
        vectorStore = new MongoVectorStore(database, new MongoVectorStoreOptions
        {
            EmbeddingGenerator = embeddingGenerator
        });
    }

    /// <summary>
    /// Loads policy data and creates embeddings in the vector store.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[{ServiceName}] Starting initialization...", ServiceName);

        var document = await LoadPolicyDocumentAsync();
        var records = ConvertToRecords(document);

        logger.LogInformation("[{ServiceName}] Loaded {Count} sections from {DocumentId}",
            ServiceName, records.Count, document.DocumentId);

        var collection = GetCollection();

        logger.LogInformation("[{ServiceName}] Creating collection and indexes...", ServiceName);
        await collection.EnsureCollectionExistsAsync(cancellationToken);

        await UpsertRecordsAsync(collection, records, cancellationToken);

        logger.LogInformation("[{ServiceName}] Initialization complete with {Count} sections",
            ServiceName, records.Count);
    }

    /// <summary>
    /// Finds policy sections that match the query.
    /// </summary>
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

        logger.LogDebug("[{ServiceName}] Search for '{Query}' returned {Count} results",
            ServiceName, query, results.Count);

        return results;
    }

    /// <summary>
    /// Finds matching policies and returns them as readable text.
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

    private VectorStoreCollection<string, PolicySectionRecord> GetCollection()
    {
        return vectorStore.GetCollection<string, PolicySectionRecord>(CollectionName);
    }

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
            logger.LogDebug("[{ServiceName}] Generated embedding {Processed}/{Total}",
                ServiceName, processedCount, totalCount);
        }

        logger.LogInformation("[{ServiceName}] Upserted {Count} records with embeddings",
            ServiceName, totalCount);
    }
}
