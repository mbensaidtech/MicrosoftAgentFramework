using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using AIAgentsBackend.Configuration;
using AIAgentsBackend.Services.VectorStore.Base;
using AIAgentsBackend.Services.VectorStore.Interfaces;

namespace AIAgentsBackend.Services.VectorStore;

/// <summary>
/// Vector store service for order cancellation policy documents.
/// </summary>
public class OrderCancellationPolicyVectorStoreService : PolicyVectorStoreServiceBase, IOrderCancellationPolicyVectorStoreService
{
    private readonly VectorStoreSettings _settings;

    protected override string CollectionName => _settings.Collections.OrderCancellationPolicy.CollectionName;
    protected override string DataFilePath => Path.Combine(_settings.DataDirectory, _settings.Collections.OrderCancellationPolicy.DataFileName);
    protected override string ServiceName => "OrderCancellationPolicy";

    public OrderCancellationPolicyVectorStoreService(
        IMongoDatabase database,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IOptions<VectorStoreSettings> settings,
        ILogger<OrderCancellationPolicyVectorStoreService> logger)
        : base(database, embeddingGenerator, logger)
    {
        _settings = settings.Value;
    }
}
