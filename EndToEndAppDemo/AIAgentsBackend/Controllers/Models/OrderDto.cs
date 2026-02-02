namespace AIAgentsBackend.Controllers.Models;

/// <summary>
/// DTO for order response.
/// </summary>
public class OrderDto
{
    public string OrderId { get; set; } = string.Empty;
    public string Customer { get; set; } = string.Empty;
    public List<OrderItemDto> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public OrderStatusDto? Status { get; set; }
    public ShippingAddressDto ShippingAddress { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// DTO for order item.
/// </summary>
public class OrderItemDto
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal SubTotal => Quantity * UnitPrice;
}

/// <summary>
/// DTO for shipping address.
/// </summary>
public class ShippingAddressDto
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

/// <summary>
/// DTO for order status.
/// </summary>
public class OrderStatusDto
{
    public string StatusId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string ColorCode { get; set; } = string.Empty;
    public bool IsFinal { get; set; }
}

/// <summary>
/// Response for order search results.
/// </summary>
public class OrderSearchResponse
{
    public int TotalCount { get; set; }
    public List<OrderDto> Orders { get; set; } = new();
}

/// <summary>
/// Response for all order statuses.
/// </summary>
public class OrderStatusListResponse
{
    public int TotalCount { get; set; }
    public List<OrderStatusDto> Statuses { get; set; } = new();
}

