namespace AIAgentsBackend.Models.VectorStore;

/// <summary>
/// Represents the structure of a policy JSON document.
/// Used for deserializing policy data files.
/// </summary>
public class PolicyDocument
{
    /// <summary>
    /// Unique identifier for the document.
    /// </summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>
    /// Category of the policy (e.g., "returns", "refunds", "cancellation").
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Title of the policy document.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Last update date of the document.
    /// </summary>
    public string LastUpdated { get; set; } = string.Empty;

    /// <summary>
    /// List of sections in the document.
    /// </summary>
    public List<PolicySection> Sections { get; set; } = [];
}
