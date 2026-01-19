using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using AIAgentsBackend.Configuration;
using AIAgentsBackend.Services.VectorStore.Base;
using AIAgentsBackend.Services.VectorStore.Interfaces;

namespace AIAgentsBackend.Services.VectorStore;

/// <summary>
/// Vector store service for return policy documents.
/// </summary>
public class ReturnPolicyVectorStoreService : PolicyVectorStoreServiceBase, IReturnPolicyVectorStoreService
{
    private readonly VectorStoreSettings _settings;

    protected override string CollectionName => _settings.Collections.ReturnPolicy.CollectionName;
    protected override string DataFilePath => Path.Combine(_settings.DataDirectory, _settings.Collections.ReturnPolicy.DataFileName);
    protected override string ServiceName => "ReturnPolicy";

    public ReturnPolicyVectorStoreService(
        IMongoDatabase database,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IOptions<VectorStoreSettings> settings,
        ILogger<ReturnPolicyVectorStoreService> logger)
        : base(database, embeddingGenerator, logger)
    {
        _settings = settings.Value;
    }
}
