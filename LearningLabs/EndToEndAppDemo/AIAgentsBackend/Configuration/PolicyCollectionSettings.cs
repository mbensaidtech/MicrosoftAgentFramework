namespace AIAgentsBackend.Configuration;

/// <summary>
/// Configuration for a single policy collection.
/// </summary>
public class PolicyCollectionSettings
{
    /// <summary>
    /// Whether this collection is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// MongoDB collection name for this vector store.
    /// </summary>
    public string CollectionName { get; set; } = string.Empty;

    /// <summary>
    /// Name of the JSON data file in the DataDirectory.
    /// </summary>
    public string DataFileName { get; set; } = string.Empty;
}
