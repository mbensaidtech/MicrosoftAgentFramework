namespace AIAgentsBackend.Configuration;

/// <summary>
/// Settings for each policy collection in the vector store.
/// </summary>
public class VectorStoreCollectionSettings
{
    /// <summary>
    /// Return policy settings.
    /// </summary>
    public PolicyCollectionSettings ReturnPolicy { get; set; } = new()
    {
        Enabled = true,
        CollectionName = "return-policy",
        DataFileName = "return-policy.json"
    };

    /// <summary>
    /// Refund policy settings.
    /// </summary>
    public PolicyCollectionSettings RefundPolicy { get; set; } = new()
    {
        Enabled = true,
        CollectionName = "refund-policy",
        DataFileName = "refund-policy.json"
    };

    /// <summary>
    /// Order cancellation policy settings.
    /// </summary>
    public PolicyCollectionSettings OrderCancellationPolicy { get; set; } = new()
    {
        Enabled = true,
        CollectionName = "order-cancellation-policy",
        DataFileName = "order-cancellation-policy.json"
    };

    /// <summary>
    /// Seller requirements knowledge base settings.
    /// </summary>
    public PolicyCollectionSettings SellerRequirements { get; set; } = new()
    {
        Enabled = true,
        CollectionName = "seller-requirements",
        DataFileName = "seller-requirements.json"
    };
}
