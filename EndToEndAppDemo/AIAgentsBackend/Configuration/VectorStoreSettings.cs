namespace AIAgentsBackend.Configuration;

/// <summary>
/// Settings for the vector store.
/// </summary>
public class VectorStoreSettings
{
    /// <summary>
    /// Section name in appsettings.json.
    /// </summary>
    public const string SectionName = "VectorStore";

    /// <summary>
    /// If true, loads data into the vector store when the app starts.
    /// Only needed on first run or when re-indexing.
    /// </summary>
    public bool InitializeOnStartup { get; set; } = false;

    /// <summary>
    /// Folder containing the policy JSON files.
    /// </summary>
    public string DataDirectory { get; set; } = "Data/VectorStore";

    /// <summary>
    /// Settings for each policy collection.
    /// </summary>
    public VectorStoreCollectionSettings Collections { get; set; } = new();
}
