namespace AIAgentsBackend.Configuration;

/// <summary>
/// Configuration for individual vector store collections.
/// </summary>
public class VectorStoreCollectionSettings
{
    /// <summary>
    /// Return policy vector store settings.
    /// </summary>
    public PolicyCollectionSettings ReturnPolicy { get; set; } = new()
    {
        Enabled = true,
        CollectionName = "return-policy",
        DataFileName = "return-policy.json"
    };

    /// <summary>
    /// Refund policy vector store settings.
    /// </summary>
    public PolicyCollectionSettings RefundPolicy { get; set; } = new()
    {
        Enabled = true,
        CollectionName = "refund-policy",
        DataFileName = "refund-policy.json"
    };

    /// <summary>
    /// Order cancellation policy vector store settings.
    /// </summary>
    public PolicyCollectionSettings OrderCancellationPolicy { get; set; } = new()
    {
        Enabled = true,
        CollectionName = "order-cancellation-policy",
        DataFileName = "order-cancellation-policy.json"
    };
}
