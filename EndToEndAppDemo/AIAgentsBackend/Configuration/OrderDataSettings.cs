namespace AIAgentsBackend.Configuration;

/// <summary>
/// Settings for order data initialization.
/// </summary>
public class OrderDataSettings
{
    public const string SectionName = "OrderData";

    /// <summary>
    /// Whether to initialize order data on startup.
    /// </summary>
    public bool InitializeOnStartup { get; set; } = true;

    /// <summary>
    /// Directory containing the JSON data files.
    /// </summary>
    public string DataDirectory { get; set; } = "Data";

    /// <summary>
    /// File name for orders data.
    /// </summary>
    public string OrdersFileName { get; set; } = "orders.json";

    /// <summary>
    /// File name for order statuses data.
    /// </summary>
    public string OrderStatusesFileName { get; set; } = "order-statuses.json";
}

