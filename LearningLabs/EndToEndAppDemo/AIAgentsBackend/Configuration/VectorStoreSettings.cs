namespace AIAgentsBackend.Configuration;

/// <summary>
/// Configuration settings for vector store initialization and behavior.
/// </summary>
public class VectorStoreSettings
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "VectorStore";

    /// <summary>
    /// Whether to initialize vector stores with data on application startup.
    /// Set to true only for the first run or when data needs to be re-indexed.
    /// </summary>
    public bool InitializeOnStartup { get; set; } = false;

    /// <summary>
    /// The path to the directory containing vector store data files.
    /// Relative to the application base directory.
    /// </summary>
    public string DataDirectory { get; set; } = "Data/VectorStore";

    /// <summary>
    /// Individual vector store configurations.
    /// </summary>
    public VectorStoreCollectionSettings Collections { get; set; } = new();
}
