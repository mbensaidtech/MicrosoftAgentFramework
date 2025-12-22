namespace AIAgentWithThreads.Configuration;

/// <summary>
/// Configuration settings for MongoDB vector store.
/// </summary>
public class MongoDbConfiguration
{
    /// <summary>
    /// Gets or sets the MongoDB connection string.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the MongoDB database name.
    /// </summary>
    public string DatabaseName { get; set; } = string.Empty;
}
