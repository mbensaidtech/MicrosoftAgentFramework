using Microsoft.Extensions.VectorData;

namespace AIAgentsBackend.Models.VectorStore;

/// <summary>
/// A single section from a policy document, stored in the vector database.
/// </summary>
public class PolicySectionRecord
{
    /// <summary>
    /// Unique ID (documentId-sectionId).
    /// </summary>
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Parent document ID (e.g., "return-policy").
    /// </summary>
    [VectorStoreData(IsIndexed = true)]
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>
    /// Category (e.g., "returns", "refunds").
    /// </summary>
    [VectorStoreData(IsIndexed = true)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Section ID within the document.
    /// </summary>
    [VectorStoreData(IsIndexed = true)]
    public string SectionId { get; set; } = string.Empty;

    /// <summary>
    /// Section title.
    /// </summary>
    [VectorStoreData(IsIndexed = true)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Section content.
    /// </summary>
    [VectorStoreData]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Text used for generating the embedding vector.
    /// </summary>
    [VectorStoreVector(Dimensions: 1536, DistanceFunction = DistanceFunction.CosineSimilarity)]
    public string Embedding => $"Title: {Title}\nContent: {Content}";

    public override string ToString() => $"[{Category}] {Title}: {Content}";

    public string ToString(double? score) => $"[{Category}] {Title} (Score: {score:F4}): {Content}";
}
