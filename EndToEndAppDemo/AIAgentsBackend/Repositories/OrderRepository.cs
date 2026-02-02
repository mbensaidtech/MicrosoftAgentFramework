using AIAgentsBackend.Configuration;
using AIAgentsBackend.Models.Orders;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AIAgentsBackend.Repositories;

/// <summary>
/// MongoDB repository for orders and order statuses.
/// </summary>
public class OrderRepository : IOrderRepository
{
    private readonly IMongoCollection<Order> ordersCollection;
    private readonly IMongoCollection<OrderStatus> orderStatusesCollection;
    private readonly ILogger<OrderRepository> logger;

    public OrderRepository(
        IMongoClient mongoClient, 
        IOptions<MongoDbSettings> mongoSettings,
        ILogger<OrderRepository> logger)
    {
        var settings = mongoSettings.Value ?? throw new ArgumentNullException(nameof(mongoSettings));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (mongoClient == null)
            throw new ArgumentNullException(nameof(mongoClient));

        var database = mongoClient.GetDatabase(settings.DatabaseName);
        ordersCollection = database.GetCollection<Order>(settings.OrdersCollectionName);
        orderStatusesCollection = database.GetCollection<OrderStatus>(settings.OrderStatusesCollectionName);

        CreateIndexes();
    }

    private void CreateIndexes()
    {
        try
        {
            // Index for querying orders by orderId
            var orderIdIndex = new CreateIndexModel<Order>(
                Builders<Order>.IndexKeys.Ascending(x => x.OrderId),
                new CreateIndexOptions { Name = "idx_orderId", Unique = true });

            // Index for querying orders by customer (login/username)
            var customerIndex = new CreateIndexModel<Order>(
                Builders<Order>.IndexKeys.Ascending(x => x.Customer),
                new CreateIndexOptions { Name = "idx_customer" });

            ordersCollection.Indexes.CreateMany([orderIdIndex, customerIndex]);

            // Index for order statuses by statusId
            var statusIdIndex = new CreateIndexModel<OrderStatus>(
                Builders<OrderStatus>.IndexKeys.Ascending(x => x.StatusId),
                new CreateIndexOptions { Name = "idx_statusId", Unique = true });

            // Index for order statuses by code
            var codeIndex = new CreateIndexModel<OrderStatus>(
                Builders<OrderStatus>.IndexKeys.Ascending(x => x.Code),
                new CreateIndexOptions { Name = "idx_code", Unique = true });

            orderStatusesCollection.Indexes.CreateMany([statusIdIndex, codeIndex]);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[OrderRepository] Could not create indexes");
        }
    }

    #region Orders

    public async Task<Order?> GetOrderByIdAsync(string orderId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            return null;

        var filter = Builders<Order>.Filter.Eq(x => x.OrderId, orderId);
        var order = await ordersCollection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        return order;
    }

    public async Task<IEnumerable<Order>> GetOrdersByCustomerAsync(string customer, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customer))
            return [];

        // Case-insensitive exact match on customer login
        var filter = Builders<Order>.Filter.Regex(x => x.Customer, 
            new MongoDB.Bson.BsonRegularExpression($"^{customer}$", "i"));
        var sort = Builders<Order>.Sort.Descending(x => x.CreatedAt);

        var orders = await ordersCollection
            .Find(filter)
            .Sort(sort)
            .ToListAsync(cancellationToken);
        return orders;
    }

    public async Task<IEnumerable<Order>> GetAllOrdersAsync(CancellationToken cancellationToken = default)
    {
        var sort = Builders<Order>.Sort.Descending(x => x.CreatedAt);
        var orders = await ordersCollection
            .Find(FilterDefinition<Order>.Empty)
            .Sort(sort)
            .ToListAsync(cancellationToken);
        return orders;
    }

    public async Task<Order> AddOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        if (order == null)
            throw new ArgumentNullException(nameof(order));

        order.CreatedAt = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;

        await ordersCollection.InsertOneAsync(order, cancellationToken: cancellationToken);
        return order;
    }

    public async Task AddOrdersAsync(IEnumerable<Order> orders, CancellationToken cancellationToken = default)
    {
        var ordersList = orders.ToList();
        if (ordersList.Count == 0)
            return;

        await ordersCollection.InsertManyAsync(ordersList, cancellationToken: cancellationToken);
    }

    public async Task<bool> UpdateOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        if (order == null)
            throw new ArgumentNullException(nameof(order));

        order.UpdatedAt = DateTime.UtcNow;

        var filter = Builders<Order>.Filter.Eq(x => x.OrderId, order.OrderId);
        var result = await ordersCollection.ReplaceOneAsync(filter, order, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> HasOrdersAsync(CancellationToken cancellationToken = default)
    {
        var count = await ordersCollection.CountDocumentsAsync(FilterDefinition<Order>.Empty, cancellationToken: cancellationToken);
        return count > 0;
    }

    #endregion

    #region Order Statuses

    public async Task<OrderStatus?> GetOrderStatusByIdAsync(string statusId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(statusId))
            return null;

        var filter = Builders<OrderStatus>.Filter.Eq(x => x.StatusId, statusId);
        var status = await orderStatusesCollection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        return status;
    }

    public async Task<OrderStatus?> GetOrderStatusByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var filter = Builders<OrderStatus>.Filter.Eq(x => x.Code, code.ToUpperInvariant());
        var status = await orderStatusesCollection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        return status;
    }

    public async Task<IEnumerable<OrderStatus>> GetAllOrderStatusesAsync(CancellationToken cancellationToken = default)
    {
        var sort = Builders<OrderStatus>.Sort.Ascending(x => x.SortOrder);
        var statuses = await orderStatusesCollection
            .Find(FilterDefinition<OrderStatus>.Empty)
            .Sort(sort)
            .ToListAsync(cancellationToken);
        return statuses;
    }

    public async Task<OrderStatus> AddOrderStatusAsync(OrderStatus status, CancellationToken cancellationToken = default)
    {
        if (status == null)
            throw new ArgumentNullException(nameof(status));

        await orderStatusesCollection.InsertOneAsync(status, cancellationToken: cancellationToken);
        return status;
    }

    public async Task AddOrderStatusesAsync(IEnumerable<OrderStatus> statuses, CancellationToken cancellationToken = default)
    {
        var statusesList = statuses.ToList();
        if (statusesList.Count == 0)
            return;

        await orderStatusesCollection.InsertManyAsync(statusesList, cancellationToken: cancellationToken);
    }

    public async Task<bool> HasOrderStatusesAsync(CancellationToken cancellationToken = default)
    {
        var count = await orderStatusesCollection.CountDocumentsAsync(FilterDefinition<OrderStatus>.Empty, cancellationToken: cancellationToken);
        return count > 0;
    }

    #endregion
}

