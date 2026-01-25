using System.ComponentModel;
using System.Text;
using AIAgentsBackend.Models.Orders;
using AIAgentsBackend.Repositories;

namespace AIAgentsBackend.Agents.Tools;

/// <summary>
/// Tools for the Order Agent to handle order data and order actions.
/// </summary>
public class OrderTools
{
    private readonly IServiceProvider serviceProvider;

    public OrderTools(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Gets order information by order ID.
    /// Use this when the customer provides an order ID and you need to retrieve the order details.
    /// </summary>
    /// <param name="orderId">The order ID (e.g., ORD-2026-001)</param>
    /// <returns>Order information including items, amounts, and current status</returns>
    [Description("Récupère les informations d'une commande à partir de son numéro. Utilise cet outil quand le client fournit un numéro de commande et que tu dois obtenir les détails (produits, montants, statut).")]
    public async Task<string> GetOrderByIdAsync(
        [Description("Le numéro de commande (ex: 'ORD-2026-001')")] string orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId))
        {
            return "Numéro de commande non fourni. Demande au client son numéro de commande.";
        }

        using var scope = serviceProvider.CreateScope();
        var orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

        var order = await orderRepository.GetOrderByIdAsync(orderId);
        if (order == null)
        {
            return $"Aucune commande trouvée avec le numéro '{orderId}'. Vérifie le numéro de commande avec le client.";
        }

        var status = await orderRepository.GetOrderStatusByIdAsync(order.StatusId);
        return FormatOrderInfo(order, status);
    }

    /// <summary>
    /// Gets the status of an order by order ID.
    /// Use this when the customer asks about the status of their order.
    /// </summary>
    /// <param name="orderId">The order ID (e.g., ORD-2026-001)</param>
    /// <returns>Current order status with description</returns>
    [Description("Récupère le statut d'une commande. Utilise cet outil quand le client demande où en est sa commande ou quel est le statut de sa livraison.")]
    public async Task<string> GetOrderStatusAsync(
        [Description("Le numéro de commande (ex: 'ORD-2026-001')")] string orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId))
        {
            return "Numéro de commande non fourni. Demande au client son numéro de commande.";
        }

        using var scope = serviceProvider.CreateScope();
        var orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

        var order = await orderRepository.GetOrderByIdAsync(orderId);
        if (order == null)
        {
            return $"Aucune commande trouvée avec le numéro '{orderId}'. Vérifie le numéro de commande avec le client.";
        }

        var status = await orderRepository.GetOrderStatusByIdAsync(order.StatusId);
        if (status == null)
        {
            return $"Statut de commande non trouvé pour la commande '{orderId}'.";
        }

        return FormatOrderStatus(order, status);
    }

    /// <summary>
    /// Searches orders by customer login/username.
    /// Use this when the customer wants to find their orders but doesn't have the order ID.
    /// </summary>
    /// <param name="customer">The customer's login/username (e.g., mbensaid)</param>
    /// <returns>List of orders for the customer</returns>
    [Description("Recherche les commandes d'un client par son identifiant/login. Utilise cet outil quand le client veut retrouver ses commandes mais ne connaît pas son numéro de commande.")]
    public async Task<string> SearchOrdersByCustomerAsync(
        [Description("L'identifiant/login du client (ex: 'mbensaid')")] string customer)
    {
        if (string.IsNullOrWhiteSpace(customer))
        {
            return "Identifiant client non fourni. Demande au client son identifiant pour rechercher ses commandes.";
        }

        using var scope = serviceProvider.CreateScope();
        var orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

        var orders = await orderRepository.GetOrdersByCustomerAsync(customer);
        var ordersList = orders.ToList();

        if (ordersList.Count == 0)
        {
            return $"Aucune commande trouvée pour le client '{customer}'. Vérifie l'identifiant.";
        }

        return await FormatOrdersListAsync(ordersList, orderRepository);
    }

    private static string FormatOrderInfo(Order order, OrderStatus? status)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"📦 **Commande {order.OrderId}**");
        sb.AppendLine();
        sb.AppendLine($"**Client:** {order.Customer}");
        sb.AppendLine($"**Date de commande:** {order.CreatedAt:dd/MM/yyyy HH:mm}");
        sb.AppendLine();
        
        sb.AppendLine("**Articles commandés:**");
        foreach (var item in order.Items)
        {
            sb.AppendLine($"  - {item.ProductName} x{item.Quantity} : {item.UnitPrice:N2} {order.Currency}");
        }
        sb.AppendLine();
        sb.AppendLine($"**Total:** {order.TotalAmount:N2} {order.Currency}");
        sb.AppendLine();
        
        if (status != null)
        {
            sb.AppendLine($"**Statut actuel:** {status.DisplayName}");
            sb.AppendLine($"**Description:** {status.Description}");
        }
        
        sb.AppendLine();
        sb.AppendLine("**Adresse de livraison:**");
        sb.AppendLine($"  {order.ShippingAddress.Street}");
        sb.AppendLine($"  {order.ShippingAddress.PostalCode} {order.ShippingAddress.City}");
        sb.AppendLine($"  {order.ShippingAddress.Country}");

        return sb.ToString();
    }

    private static string FormatOrderStatus(Order order, OrderStatus status)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"📋 **Statut de la commande {order.OrderId}**");
        sb.AppendLine();
        sb.AppendLine($"**Statut:** {status.DisplayName}");
        sb.AppendLine($"**Description:** {status.Description}");
        sb.AppendLine();
        sb.AppendLine($"**Dernière mise à jour:** {order.UpdatedAt:dd/MM/yyyy HH:mm}");
        
        if (status.IsFinal)
        {
            sb.AppendLine();
            sb.AppendLine("ℹ️ Cette commande est dans un statut final.");
        }

        return sb.ToString();
    }

    private static async Task<string> FormatOrdersListAsync(List<Order> orders, IOrderRepository orderRepository)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"📋 **{orders.Count} commande(s) trouvée(s)**");
        sb.AppendLine();

        foreach (var order in orders)
        {
            var status = await orderRepository.GetOrderStatusByIdAsync(order.StatusId);
            var statusText = status?.DisplayName ?? "Inconnu";

            sb.AppendLine($"**{order.OrderId}** - {order.CreatedAt:dd/MM/yyyy}");
            sb.AppendLine($"  Montant: {order.TotalAmount:N2} {order.Currency}");
            sb.AppendLine($"  Statut: {statusText}");
            sb.AppendLine($"  Articles: {string.Join(", ", order.Items.Select(i => i.ProductName))}");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
