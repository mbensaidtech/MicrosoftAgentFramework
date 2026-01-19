using Microsoft.Extensions.VectorData;

namespace AIAgentsBackend.Models.VectorStore;

/// <summary>
/// Represents a policy section record for the vector store.
/// Each section from a policy document becomes one record with its own embedding.
/// </summary>
public class PolicySectionRecord
{
    /// <summary>
    /// Unique identifier for the record (documentId-sectionId).
    /// </summary>
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The document this section belongs to (e.g., "return-policy").
    /// </summary>
    [VectorStoreData(IsIndexed = true)]
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>
    /// Category of the policy (e.g., "returns", "refunds", "cancellation").
    /// </summary>
    [VectorStoreData(IsIndexed = true)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Section identifier within the document.
    /// </summary>
    [VectorStoreData(IsIndexed = true)]
    public string SectionId { get; set; } = string.Empty;

    /// <summary>
    /// Title of the section.
    /// </summary>
    [VectorStoreData(IsIndexed = true)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The actual content of the section.
    /// </summary>
    [VectorStoreData]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Text to be embedded (combines title and content for better semantic search).
    /// The embedding generator will automatically convert this to a vector.
    /// </summary>
    [VectorStoreVector(Dimensions: 1536, DistanceFunction = DistanceFunction.CosineSimilarity)]
    public string Embedding => $"Title: {Title}\nContent: {Content}";

    /// <summary>
    /// Returns a formatted string with the section information.
    /// </summary>
    public override string ToString() => $"[{Category}] {Title}: {Content}";

    /// <summary>
    /// Returns a formatted string with the section information and search score.
    /// </summary>
    public string ToString(double? score) => $"[{Category}] {Title} (Score: {score:F4}): {Content}";
}
