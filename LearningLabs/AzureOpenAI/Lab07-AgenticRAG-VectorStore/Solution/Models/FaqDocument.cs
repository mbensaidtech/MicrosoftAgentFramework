using Microsoft.Extensions.VectorData;

namespace AgenticRAG.Models;

/// <summary>
/// Represents a FAQ entry for the vector store.
/// Also used for JSON deserialization from sav-faq.json.
/// </summary>
public class FaqRecord
{
    /// <summary>
    /// Unique identifier for the FAQ entry.
    /// </summary>
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The FAQ question.
    /// </summary>
    [VectorStoreData(IsIndexed = true)]
    public string Question { get; set; } = string.Empty;

    /// <summary>
    /// The FAQ answer/response.
    /// </summary>
    [VectorStoreData]
    public string Answer { get; set; } = string.Empty;

    /// <summary>
    /// Text to be embedded (auto-generated from Question + Answer).
    /// The embedding generator will automatically convert this to a vector.
    /// </summary>
    [VectorStoreVector(Dimensions: 1536, DistanceFunction = DistanceFunction.CosineSimilarity)]
    public string Embedding => $"Question: {Question}\nAnswer: {Answer}";

    /// <summary>
    /// Returns a formatted string with the question and answer.
    /// </summary>
    public override string ToString() => $"Q: {Question}\nA: {Answer}";

    /// <summary>
    /// Returns a formatted string with the question, answer, and score.
    /// </summary>
    public string ToString(double? score) => $"Q: {Question}\nA: {Answer}\nScore: {score:F4}";
}
