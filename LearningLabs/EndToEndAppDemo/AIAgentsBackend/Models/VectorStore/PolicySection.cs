namespace AIAgentsBackend.Models.VectorStore;

/// <summary>
/// Represents a section within a policy document.
/// </summary>
public class PolicySection
{
    /// <summary>
    /// Section identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Section title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Section content.
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
