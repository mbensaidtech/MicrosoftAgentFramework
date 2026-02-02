using System.Text.Json;
using AIAgentsBackend.Configuration;
using AIAgentsBackend.Models.Orders;
using AIAgentsBackend.Repositories;
using Microsoft.Extensions.Options;

namespace AIAgentsBackend.HostedServices;

/// <summary>
/// Loads order and order status data into MongoDB when the app starts (if enabled).
/// </summary>
public class OrderDataInitializerHostedService : IHostedService
{
    private readonly IServiceProvider serviceProvider;
    private readonly OrderDataSettings settings;
    private readonly ILogger<OrderDataInitializerHostedService> logger;
    private readonly IWebHostEnvironment environment;

    public OrderDataInitializerHostedService(
        IServiceProvider serviceProvider,
        IOptions<OrderDataSettings> settings,
        ILogger<OrderDataInitializerHostedService> logger,
        IWebHostEnvironment environment)
    {
        this.serviceProvider = serviceProvider;
        this.settings = settings.Value;
        this.logger = logger;
        this.environment = environment;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!settings.InitializeOnStartup)
        {
            logger.LogInformation("Order data initialization is disabled. Set OrderData:InitializeOnStartup to true to enable.");
            return;
        }

        logger.LogInformation("========================================");
        logger.LogInformation("Starting Order Data Initialization...");
        logger.LogInformation("========================================");

        using var scope = serviceProvider.CreateScope();
        var orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

        try
        {
            // Initialize order statuses first (orders depend on them)
            await InitializeOrderStatusesAsync(orderRepository, cancellationToken);

            // Then initialize orders
            await InitializeOrdersAsync(orderRepository, cancellationToken);

            logger.LogInformation("========================================");
            logger.LogInformation("Order Data Initialization Complete!");
            logger.LogInformation("========================================");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during order data initialization");
            throw;
        }
    }

    private async Task InitializeOrderStatusesAsync(IOrderRepository orderRepository, CancellationToken cancellationToken)
    {
        var hasStatuses = await orderRepository.HasOrderStatusesAsync(cancellationToken);
        if (hasStatuses)
        {
            logger.LogInformation("Order statuses collection already has data. Skipping initialization.");
            return;
        }

        var dataPath = Path.Combine(environment.ContentRootPath, settings.DataDirectory, settings.OrderStatusesFileName);
        if (!File.Exists(dataPath))
        {
            logger.LogWarning("Order statuses data file not found: {Path}", dataPath);
            return;
        }

        logger.LogInformation("Loading order statuses from: {Path}", dataPath);

        var jsonContent = await File.ReadAllTextAsync(dataPath, cancellationToken);
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var statusData = JsonSerializer.Deserialize<OrderStatusDataFile>(jsonContent, jsonOptions);
        if (statusData?.Statuses == null || statusData.Statuses.Count == 0)
        {
            logger.LogWarning("No order statuses found in data file");
            return;
        }

        var statuses = statusData.Statuses.Select(s => new OrderStatus
        {
            StatusId = s.StatusId,
            Code = s.Code,
            DisplayName = s.DisplayName,
            Description = s.Description,
            SortOrder = s.SortOrder,
            ColorCode = s.ColorCode,
            IsFinal = s.IsFinal
        }).ToList();

        await orderRepository.AddOrderStatusesAsync(statuses, cancellationToken);
        logger.LogInformation("Initialized {Count} order statuses", statuses.Count);
    }

    private async Task InitializeOrdersAsync(IOrderRepository orderRepository, CancellationToken cancellationToken)
    {
        var hasOrders = await orderRepository.HasOrdersAsync(cancellationToken);
        if (hasOrders)
        {
            logger.LogInformation("Orders collection already has data. Skipping initialization.");
            return;
        }

        var dataPath = Path.Combine(environment.ContentRootPath, settings.DataDirectory, settings.OrdersFileName);
        if (!File.Exists(dataPath))
        {
            logger.LogWarning("Orders data file not found: {Path}", dataPath);
            return;
        }

        logger.LogInformation("Loading orders from: {Path}", dataPath);

        var jsonContent = await File.ReadAllTextAsync(dataPath, cancellationToken);
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var orderData = JsonSerializer.Deserialize<OrderDataFile>(jsonContent, jsonOptions);
        if (orderData?.Orders == null || orderData.Orders.Count == 0)
        {
            logger.LogWarning("No orders found in data file");
            return;
        }

        var orders = orderData.Orders.Select(o => new Order
        {
            OrderId = o.OrderId,
            Customer = o.Customer,
            Items = o.Items.Select(i => new OrderItem
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList(),
            TotalAmount = o.TotalAmount,
            Currency = o.Currency,
            StatusId = o.StatusId,
            ShippingAddress = new ShippingAddress
            {
                Street = o.ShippingAddress.Street,
                City = o.ShippingAddress.City,
                PostalCode = o.ShippingAddress.PostalCode,
                Country = o.ShippingAddress.Country
            },
            CreatedAt = o.CreatedAt,
            UpdatedAt = o.UpdatedAt
        }).ToList();

        await orderRepository.AddOrdersAsync(orders, cancellationToken);
        logger.LogInformation("Initialized {Count} orders", orders.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    #region JSON Data Models

    private class OrderDataFile
    {
        public List<OrderJsonModel> Orders { get; set; } = new();
    }

    private class OrderJsonModel
    {
        public string OrderId { get; set; } = string.Empty;
        public string Customer { get; set; } = string.Empty;
        public List<OrderItemJsonModel> Items { get; set; } = new();
        public decimal TotalAmount { get; set; }
        public string Currency { get; set; } = "EUR";
        public string StatusId { get; set; } = string.Empty;
        public ShippingAddressJsonModel ShippingAddress { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    private class OrderItemJsonModel
    {
        public string ProductId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    private class ShippingAddressJsonModel
    {
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
    }

    private class OrderStatusDataFile
    {
        public List<OrderStatusJsonModel> Statuses { get; set; } = new();
    }

    private class OrderStatusJsonModel
    {
        public string StatusId { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public string ColorCode { get; set; } = string.Empty;
        public bool IsFinal { get; set; }
    }

    #endregion
}

