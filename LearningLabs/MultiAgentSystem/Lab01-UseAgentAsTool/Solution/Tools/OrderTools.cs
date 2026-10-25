using System.ComponentModel;
using System.Globalization;

namespace UseAgentAsTool.Tools;

/// <summary>
/// The order system of the online store (mocked): the tool of the order specialist in scenario 2.
/// Dates are relative to today, so that the cases stay valid whenever the lab is run.
/// </summary>
public static class OrderTools
{
    [Description("Gets an order of the online store from its order number (e.g., ORD-1042): product, status, purchase and delivery dates.")]
    public static string GetOrder([Description("The order number, e.g. ORD-1042")] string orderId)
    {
        return orderId.Trim().ToUpperInvariant() switch
        {
            "ORD-1042" => $"""
                Order ORD-1042
                Product: ZenBook 14 laptop (electronics)
                Price: 899.00 EUR, paid by credit card
                Purchase date: {DaysAgo(5)}
                Status: delivered on {DaysAgo(1)}
                """,
            "ORD-1043" => $"""
                Order ORD-1043
                Product: Wireless headphones (electronics)
                Price: 129.00 EUR, paid by PayPal
                Purchase date: {DaysAgo(10)}
                Status: delivered on {DaysAgo(8)}
                """,
            _ => $"Order {orderId} not found."
        };
    }

    private static string DaysAgo(int days) =>
        DateTime.Today.AddDays(-days).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
