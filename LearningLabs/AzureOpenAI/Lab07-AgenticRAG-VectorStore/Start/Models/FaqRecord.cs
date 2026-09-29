using Microsoft.Extensions.VectorData;

namespace AgenticRAG.Models;

/// <summary>
/// A FAQ entry, as stored in the vector store (the attributes describe the storage schema).
/// The same class is used to read <c>Data/sav-faq.json</c>.
/// </summary>
public sealed class FaqRecord
{
    /// <summary>
    /// Size of the vectors stored for each entry. Metadata of the collection: the embedding model decides the real size
    /// (1536 for text-embedding-3-small and text-embedding-ada-002, 3072 for text-embedding-3-large).
    /// </summary>
    public const int EmbeddingDimensions = 1536;

    /// <summary>
    /// Unique identifier of the FAQ entry (the key of the record).
    /// </summary>
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The question of the FAQ entry.
    /// </summary>
    [VectorStoreData]
    public string Question { get; set; } = string.Empty;

    /// <summary>
    /// The answer of the FAQ entry.
    /// </summary>
    [VectorStoreData]
    public string Answer { get; set; } = string.Empty;

    /// <summary>
    /// The text that is embedded (question + answer). Because the property is a <c>string</c>, the vector store
    /// generates the vector itself with its <c>EmbeddingGenerator</c> when the record is upserted.
    /// </summary>
    [VectorStoreVector(EmbeddingDimensions, DistanceFunction = DistanceFunction.CosineSimilarity)]
    public string Embedding => $"Question: {Question}\nAnswer: {Answer}";
}
