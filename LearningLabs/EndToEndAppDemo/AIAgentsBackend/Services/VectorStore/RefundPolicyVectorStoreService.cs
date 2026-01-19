using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using AIAgentsBackend.Configuration;
using AIAgentsBackend.Services.VectorStore.Base;
using AIAgentsBackend.Services.VectorStore.Interfaces;

namespace AIAgentsBackend.Services.VectorStore;

/// <summary>
/// Vector store service for refund policy documents.
/// </summary>
public class RefundPolicyVectorStoreService : PolicyVectorStoreServiceBase, IRefundPolicyVectorStoreService
{
    private readonly VectorStoreSettings _settings;

    protected override string CollectionName => _settings.Collections.RefundPolicy.CollectionName;
    protected override string DataFilePath => Path.Combine(_settings.DataDirectory, _settings.Collections.RefundPolicy.DataFileName);
    protected override string ServiceName => "RefundPolicy";

    public RefundPolicyVectorStoreService(
        IMongoDatabase database,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IOptions<VectorStoreSettings> settings,
        ILogger<RefundPolicyVectorStoreService> logger)
        : base(database, embeddingGenerator, logger)
    {
        _settings = settings.Value;
    }
}
