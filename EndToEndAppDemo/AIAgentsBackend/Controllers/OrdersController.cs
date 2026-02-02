using AIAgentsBackend.Controllers.Models;
using AIAgentsBackend.Models.Orders;
using AIAgentsBackend.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace AIAgentsBackend.Controllers;

/// <summary>
/// API for managing orders and order statuses.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderRepository orderRepository;
    private readonly ILogger<OrdersController> logger;

    public OrdersController(
        IOrderRepository orderRepository,
        ILogger<OrdersController> logger)
    {
        this.orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region Orders

    /// <summary>
    /// Gets an order by its order ID.
    /// </summary>
    /// <param name="orderId">The order ID (e.g., ORD-2026-001)</param>
    [HttpGet("{orderId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> GetOrderById(string orderId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            return BadRequest("Order ID is required");

        logger.LogInformation("Getting order by ID: {OrderId}", orderId);

        var order = await orderRepository.GetOrderByIdAsync(orderId, cancellationToken);
        if (order == null)
        {
            logger.LogWarning("Order not found: {OrderId}", orderId);
            return NotFound($"Order with ID '{orderId}' not found");
        }

        var status = await orderRepository.GetOrderStatusByIdAsync(order.StatusId, cancellationToken);
        return Ok(MapToOrderDto(order, status));
    }

    /// <summary>
    /// Searches orders by customer login/username.
    /// </summary>
    /// <param name="customer">Customer login/username to search by (e.g., mbensaid)</param>
    [HttpGet("search")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<OrderSearchResponse>> SearchOrders(
        [FromQuery] string? customer,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Searching orders - Customer: {Customer}", customer);

        IEnumerable<Order> orders;

        if (!string.IsNullOrWhiteSpace(customer))
        {
            orders = await orderRepository.GetOrdersByCustomerAsync(customer, cancellationToken);
        }
        else
        {
            // Return all orders if no search criteria provided
            orders = await orderRepository.GetAllOrdersAsync(cancellationToken);
        }

        var ordersList = orders.ToList();
        var statusCache = new Dictionary<string, OrderStatus?>();

        var orderDtos = new List<OrderDto>();
        foreach (var order in ordersList)
        {
            if (!statusCache.TryGetValue(order.StatusId, out var status))
            {
                status = await orderRepository.GetOrderStatusByIdAsync(order.StatusId, cancellationToken);
                statusCache[order.StatusId] = status;
            }
            orderDtos.Add(MapToOrderDto(order, status));
        }

        return Ok(new OrderSearchResponse
        {
            TotalCount = orderDtos.Count,
            Orders = orderDtos
        });
    }

    /// <summary>
    /// Gets all orders.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<OrderSearchResponse>> GetAllOrders(CancellationToken cancellationToken)
    {
        logger.LogInformation("Getting all orders");

        var orders = await orderRepository.GetAllOrdersAsync(cancellationToken);
        var ordersList = orders.ToList();
        var statusCache = new Dictionary<string, OrderStatus?>();

        var orderDtos = new List<OrderDto>();
        foreach (var order in ordersList)
        {
            if (!statusCache.TryGetValue(order.StatusId, out var status))
            {
                status = await orderRepository.GetOrderStatusByIdAsync(order.StatusId, cancellationToken);
                statusCache[order.StatusId] = status;
            }
            orderDtos.Add(MapToOrderDto(order, status));
        }

        return Ok(new OrderSearchResponse
        {
            TotalCount = orderDtos.Count,
            Orders = orderDtos
        });
    }

    #endregion

    #region Order Statuses

    /// <summary>
    /// Gets all available order statuses.
    /// </summary>
    [HttpGet("statuses")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<OrderStatusListResponse>> GetAllOrderStatuses(CancellationToken cancellationToken)
    {
        logger.LogInformation("Getting all order statuses");

        var statuses = await orderRepository.GetAllOrderStatusesAsync(cancellationToken);
        var statusesList = statuses.ToList();

        return Ok(new OrderStatusListResponse
        {
            TotalCount = statusesList.Count,
            Statuses = statusesList.Select(MapToOrderStatusDto).ToList()
        });
    }

    /// <summary>
    /// Gets an order status by its status ID.
    /// </summary>
    /// <param name="statusId">The status ID (e.g., STATUS-CONFIRMED)</param>
    [HttpGet("statuses/{statusId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderStatusDto>> GetOrderStatusById(string statusId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(statusId))
            return BadRequest("Status ID is required");

        logger.LogInformation("Getting order status by ID: {StatusId}", statusId);

        var status = await orderRepository.GetOrderStatusByIdAsync(statusId, cancellationToken);
        if (status == null)
        {
            logger.LogWarning("Order status not found: {StatusId}", statusId);
            return NotFound($"Order status with ID '{statusId}' not found");
        }

        return Ok(MapToOrderStatusDto(status));
    }

    /// <summary>
    /// Gets an order status by its code.
    /// </summary>
    /// <param name="code">The status code (e.g., CONFIRMED, SHIPPING, DELIVERED)</param>
    [HttpGet("statuses/code/{code}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderStatusDto>> GetOrderStatusByCode(string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest("Status code is required");

        logger.LogInformation("Getting order status by code: {Code}", code);

        var status = await orderRepository.GetOrderStatusByCodeAsync(code, cancellationToken);
        if (status == null)
        {
            logger.LogWarning("Order status not found for code: {Code}", code);
            return NotFound($"Order status with code '{code}' not found");
        }

        return Ok(MapToOrderStatusDto(status));
    }

    /// <summary>
    /// Gets the status of a specific order by its order ID.
    /// </summary>
    /// <param name="orderId">The order ID (e.g., ORD-2026-001)</param>
    [HttpGet("{orderId}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderStatusDto>> GetOrderStatus(string orderId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            return BadRequest("Order ID is required");

        logger.LogInformation("Getting status for order: {OrderId}", orderId);

        var order = await orderRepository.GetOrderByIdAsync(orderId, cancellationToken);
        if (order == null)
        {
            logger.LogWarning("Order not found: {OrderId}", orderId);
            return NotFound($"Order with ID '{orderId}' not found");
        }

        var status = await orderRepository.GetOrderStatusByIdAsync(order.StatusId, cancellationToken);
        if (status == null)
        {
            logger.LogWarning("Order status not found for order: {OrderId}, StatusId: {StatusId}", orderId, order.StatusId);
            return NotFound($"Status not found for order '{orderId}'");
        }

        return Ok(MapToOrderStatusDto(status));
    }

    #endregion

    #region Mapping Helpers

    private static OrderDto MapToOrderDto(Order order, OrderStatus? status)
    {
        return new OrderDto
        {
            OrderId = order.OrderId,
            Customer = order.Customer,
            Items = order.Items.Select(item => new OrderItemDto
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            }).ToList(),
            TotalAmount = order.TotalAmount,
            Currency = order.Currency,
            Status = status != null ? MapToOrderStatusDto(status) : null,
            ShippingAddress = new ShippingAddressDto
            {
                Street = order.ShippingAddress.Street,
                City = order.ShippingAddress.City,
                PostalCode = order.ShippingAddress.PostalCode,
                Country = order.ShippingAddress.Country
            },
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt
        };
    }

    private static OrderStatusDto MapToOrderStatusDto(OrderStatus status)
    {
        return new OrderStatusDto
        {
            StatusId = status.StatusId,
            Code = status.Code,
            DisplayName = status.DisplayName,
            Description = status.Description,
            SortOrder = status.SortOrder,
            ColorCode = status.ColorCode,
            IsFinal = status.IsFinal
        };
    }

    #endregion
}

