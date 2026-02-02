using Microsoft.Extensions.Options;
using AIAgentsBackend.Configuration;
using AIAgentsBackend.Services.VectorStore.Interfaces;

namespace AIAgentsBackend.HostedServices;

/// <summary>
/// Loads policy data into the vector store when the app starts (if enabled).
/// </summary>
public class VectorStoreInitializerHostedService : IHostedService
{
    private readonly IServiceProvider serviceProvider;
    private readonly VectorStoreSettings settings;
    private readonly ILogger<VectorStoreInitializerHostedService> logger;

    public VectorStoreInitializerHostedService(
        IServiceProvider serviceProvider,
        IOptions<VectorStoreSettings> settings,
        ILogger<VectorStoreInitializerHostedService> logger)
    {
        this.serviceProvider = serviceProvider;
        this.settings = settings.Value;
        this.logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!settings.InitializeOnStartup)
        {
            logger.LogInformation("Vector store initialization is disabled. Set VectorStore:InitializeOnStartup to true to enable.");
            return;
        }

        logger.LogInformation("========================================");
        logger.LogInformation("Starting Vector Store Initialization...");
        logger.LogInformation("========================================");

        using var scope = serviceProvider.CreateScope();
        var collections = settings.Collections;

        try
        {
            if (collections.ReturnPolicy.Enabled)
            {
                var returnPolicyService = scope.ServiceProvider.GetRequiredService<IReturnPolicyVectorStoreService>();
                await returnPolicyService.InitializeAsync(cancellationToken);
            }
            else
            {
                logger.LogInformation("Return Policy vector store is disabled.");
            }

            if (collections.RefundPolicy.Enabled)
            {
                var refundPolicyService = scope.ServiceProvider.GetRequiredService<IRefundPolicyVectorStoreService>();
                await refundPolicyService.InitializeAsync(cancellationToken);
            }
            else
            {
                logger.LogInformation("Refund Policy vector store is disabled.");
            }

            if (collections.OrderCancellationPolicy.Enabled)
            {
                var orderCancellationService = scope.ServiceProvider.GetRequiredService<IOrderCancellationPolicyVectorStoreService>();
                await orderCancellationService.InitializeAsync(cancellationToken);
            }
            else
            {
                logger.LogInformation("Order Cancellation Policy vector store is disabled.");
            }

            if (collections.SellerRequirements.Enabled)
            {
                var sellerRequirementsService = scope.ServiceProvider.GetRequiredService<ISellerRequirementsVectorStoreService>();
                await sellerRequirementsService.InitializeAsync(cancellationToken);
            }
            else
            {
                logger.LogInformation("Seller Requirements vector store is disabled.");
            }

            logger.LogInformation("========================================");
            logger.LogInformation("Vector Store Initialization Complete!");
            logger.LogInformation("========================================");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during vector store initialization");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
