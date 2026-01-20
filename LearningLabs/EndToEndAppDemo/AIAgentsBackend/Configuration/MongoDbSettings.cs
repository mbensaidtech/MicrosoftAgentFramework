namespace AIAgentsBackend.Configuration;

/// <summary>
/// MongoDB connection settings.
/// </summary>
public class MongoDbSettings
{
    /// <summary>
    /// Connection string to MongoDB.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Database name.
    /// </summary>
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>
    /// Collection name for thread messages.
    /// </summary>
    public string ThreadMessagesCollectionName { get; set; } = "threadMessages";

    /// <summary>
    /// Collection name for chat history (used by MongoVectorChatMessageStore).
    /// </summary>
    public string ChatMessageStoreCollectionName { get; set; } = "chat_history";
}
