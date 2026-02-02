using AIAgentsBackend.Models.Orders;

namespace AIAgentsBackend.Repositories;

/// <summary>
/// Repository for managing orders and order statuses in MongoDB.
/// </summary>
public interface IOrderRepository
{
    #region Orders

    /// <summary>
    /// Gets an order by its order ID.
    /// </summary>
    Task<Order?> GetOrderByIdAsync(string orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all orders for a specific customer (by login/username).
    /// </summary>
    Task<IEnumerable<Order>> GetOrdersByCustomerAsync(string customer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all orders.
    /// </summary>
    Task<IEnumerable<Order>> GetAllOrdersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new order.
    /// </summary>
    Task<Order> AddOrderAsync(Order order, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds multiple orders at once.
    /// </summary>
    Task AddOrdersAsync(IEnumerable<Order> orders, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing order.
    /// </summary>
    Task<bool> UpdateOrderAsync(Order order, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the orders collection has any documents.
    /// </summary>
    Task<bool> HasOrdersAsync(CancellationToken cancellationToken = default);

    #endregion

    #region Order Statuses

    /// <summary>
    /// Gets an order status by its status ID.
    /// </summary>
    Task<OrderStatus?> GetOrderStatusByIdAsync(string statusId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an order status by its code (e.g., CONFIRMED, SHIPPING).
    /// </summary>
    Task<OrderStatus?> GetOrderStatusByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all order statuses ordered by sort order.
    /// </summary>
    Task<IEnumerable<OrderStatus>> GetAllOrderStatusesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new order status.
    /// </summary>
    Task<OrderStatus> AddOrderStatusAsync(OrderStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds multiple order statuses at once.
    /// </summary>
    Task AddOrderStatusesAsync(IEnumerable<OrderStatus> statuses, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the order statuses collection has any documents.
    /// </summary>
    Task<bool> HasOrderStatusesAsync(CancellationToken cancellationToken = default);

    #endregion
}

