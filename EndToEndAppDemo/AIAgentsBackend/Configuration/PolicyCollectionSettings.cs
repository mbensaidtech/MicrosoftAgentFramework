namespace AIAgentsBackend.Configuration;

/// <summary>
/// Settings for a single policy collection.
/// </summary>
public class PolicyCollectionSettings
{
    /// <summary>
    /// Whether this policy is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// MongoDB collection name.
    /// </summary>
    public string CollectionName { get; set; } = string.Empty;

    /// <summary>
    /// JSON file name in the data folder.
    /// </summary>
    public string DataFileName { get; set; } = string.Empty;
}
