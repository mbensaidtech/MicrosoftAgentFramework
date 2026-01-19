using Microsoft.Extensions.Options;
using AIAgentsBackend.Configuration;
using AIAgentsBackend.Services.VectorStore.Interfaces;

namespace AIAgentsBackend.HostedServices;

/// <summary>
/// Hosted service that initializes vector stores on application startup.
/// Only runs if InitializeOnStartup is set to true in configuration.
/// </summary>
public class VectorStoreInitializerHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly VectorStoreSettings _settings;
    private readonly ILogger<VectorStoreInitializerHostedService> _logger;

    public VectorStoreInitializerHostedService(
        IServiceProvider serviceProvider,
        IOptions<VectorStoreSettings> settings,
        ILogger<VectorStoreInitializerHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_settings.InitializeOnStartup)
        {
            _logger.LogInformation("Vector store initialization is disabled. Set VectorStore:InitializeOnStartup to true to enable.");
            return;
        }

        _logger.LogInformation("========================================");
        _logger.LogInformation("Starting Vector Store Initialization...");
        _logger.LogInformation("========================================");

        using var scope = _serviceProvider.CreateScope();
        var collections = _settings.Collections;

        try
        {
            // Initialize Return Policy
            if (collections.ReturnPolicy.Enabled)
            {
                var returnPolicyService = scope.ServiceProvider.GetRequiredService<IReturnPolicyVectorStoreService>();
                await returnPolicyService.InitializeAsync(cancellationToken);
            }
            else
            {
                _logger.LogInformation("Return Policy vector store is disabled.");
            }

            // Initialize Refund Policy
            if (collections.RefundPolicy.Enabled)
            {
                var refundPolicyService = scope.ServiceProvider.GetRequiredService<IRefundPolicyVectorStoreService>();
                await refundPolicyService.InitializeAsync(cancellationToken);
            }
            else
            {
                _logger.LogInformation("Refund Policy vector store is disabled.");
            }

            // Initialize Order Cancellation Policy
            if (collections.OrderCancellationPolicy.Enabled)
            {
                var orderCancellationService = scope.ServiceProvider.GetRequiredService<IOrderCancellationPolicyVectorStoreService>();
                await orderCancellationService.InitializeAsync(cancellationToken);
            }
            else
            {
                _logger.LogInformation("Order Cancellation Policy vector store is disabled.");
            }

            _logger.LogInformation("========================================");
            _logger.LogInformation("Vector Store Initialization Complete!");
            _logger.LogInformation("========================================");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during vector store initialization");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
