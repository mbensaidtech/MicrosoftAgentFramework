using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using AIAgentsBackend.Configuration;
using AIAgentsBackend.Services.VectorStore.Base;
using AIAgentsBackend.Services.VectorStore.Interfaces;

namespace AIAgentsBackend.Services.VectorStore;

/// <summary>
/// Searches order cancellation policy documents.
/// </summary>
public class OrderCancellationPolicyVectorStoreService : PolicyVectorStoreServiceBase, IOrderCancellationPolicyVectorStoreService
{
    private readonly VectorStoreSettings settings;

    protected override string CollectionName => settings.Collections.OrderCancellationPolicy.CollectionName;
    protected override string DataFilePath => Path.Combine(settings.DataDirectory, settings.Collections.OrderCancellationPolicy.DataFileName);
    protected override string ServiceName => "OrderCancellationPolicy";

    public OrderCancellationPolicyVectorStoreService(
        IMongoDatabase database,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IOptions<VectorStoreSettings> settings,
        ILogger<OrderCancellationPolicyVectorStoreService> logger)
        : base(database, embeddingGenerator, logger)
    {
        this.settings = settings.Value;
    }
}
