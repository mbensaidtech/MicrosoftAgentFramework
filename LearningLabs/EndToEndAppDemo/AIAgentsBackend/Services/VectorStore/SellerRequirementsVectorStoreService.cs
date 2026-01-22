using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using AIAgentsBackend.Configuration;
using AIAgentsBackend.Services.VectorStore.Base;
using AIAgentsBackend.Services.VectorStore.Interfaces;

namespace AIAgentsBackend.Services.VectorStore;

/// <summary>
/// Searches seller requirements knowledge base.
/// Contains information about what sellers typically ask customers
/// depending on the type of problem (photos, documents, etc.).
/// </summary>
public class SellerRequirementsVectorStoreService : PolicyVectorStoreServiceBase, ISellerRequirementsVectorStoreService
{
    private readonly VectorStoreSettings settings;

    protected override string CollectionName => settings.Collections.SellerRequirements.CollectionName;
    protected override string DataFilePath => Path.Combine(settings.DataDirectory, settings.Collections.SellerRequirements.DataFileName);
    protected override string ServiceName => "SellerRequirements";

    public SellerRequirementsVectorStoreService(
        IMongoDatabase database,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IOptions<VectorStoreSettings> settings,
        ILogger<SellerRequirementsVectorStoreService> logger)
        : base(database, embeddingGenerator, logger)
    {
        this.settings = settings.Value;
    }
}
