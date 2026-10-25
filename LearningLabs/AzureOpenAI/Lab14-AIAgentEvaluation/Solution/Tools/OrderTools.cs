using System.ComponentModel;
using System.Globalization;

namespace AIAgentEvaluation.Tools;

/// <summary>
/// The order system of the online store (mocked): the tool of the support agent.
/// It returns facts the model cannot guess (tracking number, carrier, locker code): an answer that contains them
/// proves that the agent called the tool. Dates are relative to today.
/// </summary>
public static class OrderTools
{
    [Description("Gets the status of an order of the online store from its order number (e.g., ORD-1042): shipping, carrier, tracking number, delivery.")]
    public static string GetOrderStatus([Description("The order number, e.g. ORD-1042")] string orderId)
    {
        return orderId.Trim().ToUpperInvariant() switch
        {
            "ORD-1042" => $"Order ORD-1042: shipped on {Day(-1)} with the carrier ParcelExpress, tracking number TRK-48213, expected delivery on {Day(2)}.",
            "ORD-1043" => $"Order ORD-1043: delivered on {Day(-3)} to the parcel locker LCK-305 (pick-up code 6621).",
            _ => $"Order {orderId} not found."
        };
    }

    private static string Day(int offset) =>
        DateTime.Today.AddDays(offset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
