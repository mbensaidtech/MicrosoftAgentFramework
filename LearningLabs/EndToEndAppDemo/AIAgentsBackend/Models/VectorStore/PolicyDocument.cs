namespace AIAgentsBackend.Models.VectorStore;

/// <summary>
/// A policy document loaded from JSON.
/// </summary>
public class PolicyDocument
{
    /// <summary>
    /// Document ID.
    /// </summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>
    /// Category (e.g., "returns", "refunds").
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Document title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Last update date.
    /// </summary>
    public string LastUpdated { get; set; } = string.Empty;

    /// <summary>
    /// Sections in the document.
    /// </summary>
    public List<PolicySection> Sections { get; set; } = [];
}
