namespace AIAgentWithAIContextProvider.Configuration;

/// <summary>
/// Configuration settings for MongoDB.
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

    /// <summary>
    /// Gets or sets the MongoDB collection name for user memories.
    /// </summary>
    public string UserMemoriesCollectionName { get; set; } = string.Empty;
}
