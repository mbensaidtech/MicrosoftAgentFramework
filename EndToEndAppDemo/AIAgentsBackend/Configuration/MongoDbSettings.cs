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

    /// <summary>
    /// Collection name for customer-seller conversations.
    /// </summary>
    public string CustomerSellerConversationCollectionName { get; set; } = "customer_seller_conversations";

    /// <summary>
    /// Collection name for orders.
    /// </summary>
    public string OrdersCollectionName { get; set; } = "orders";

    /// <summary>
    /// Collection name for order statuses.
    /// </summary>
    public string OrderStatusesCollectionName { get; set; } = "order_statuses";
}
