using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AIAgentsBackend.Models.Orders;

/// <summary>
/// Represents a customer order.
/// </summary>
public class Order
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    /// <summary>
    /// Unique order identifier (e.g., ORD-2026-001).
    /// </summary>
    [BsonElement("orderId")]
    public string OrderId { get; set; } = string.Empty;

    /// <summary>
    /// Customer login/username (e.g., mbensaid).
    /// </summary>
    [BsonElement("customer")]
    public string Customer { get; set; } = string.Empty;

    /// <summary>
    /// List of items in the order.
    /// </summary>
    [BsonElement("items")]
    public List<OrderItem> Items { get; set; } = new();

    /// <summary>
    /// Total amount of the order.
    /// </summary>
    [BsonElement("totalAmount")]
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Currency of the order (e.g., EUR, USD).
    /// </summary>
    [BsonElement("currency")]
    public string Currency { get; set; } = "EUR";

    /// <summary>
    /// Current status ID reference.
    /// </summary>
    [BsonElement("statusId")]
    public string StatusId { get; set; } = string.Empty;

    /// <summary>
    /// Shipping address.
    /// </summary>
    [BsonElement("shippingAddress")]
    public ShippingAddress ShippingAddress { get; set; } = new();

    /// <summary>
    /// Order creation date.
    /// </summary>
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last update date.
    /// </summary>
    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents an item in an order.
/// </summary>
public class OrderItem
{
    /// <summary>
    /// Product identifier.
    /// </summary>
    [BsonElement("productId")]
    public string ProductId { get; set; } = string.Empty;

    /// <summary>
    /// Product name.
    /// </summary>
    [BsonElement("productName")]
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// Quantity ordered.
    /// </summary>
    [BsonElement("quantity")]
    public int Quantity { get; set; }

    /// <summary>
    /// Unit price.
    /// </summary>
    [BsonElement("unitPrice")]
    public decimal UnitPrice { get; set; }
}

/// <summary>
/// Represents a shipping address.
/// </summary>
public class ShippingAddress
{
    [BsonElement("street")]
    public string Street { get; set; } = string.Empty;

    [BsonElement("city")]
    public string City { get; set; } = string.Empty;

    [BsonElement("postalCode")]
    public string PostalCode { get; set; } = string.Empty;

    [BsonElement("country")]
    public string Country { get; set; } = string.Empty;
}

