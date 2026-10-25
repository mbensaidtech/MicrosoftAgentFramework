using System.ComponentModel;
using System.Globalization;
using System.Text.Json;

namespace AIAgentWithSkills;

/// <summary>
/// The content and the script of the code-defined skill of scenario 2 (shipping quotes): a static resource,
/// a resource computed when the agent reads it, and a script that runs in the program (no subprocess).
/// </summary>
public static class ShippingRates
{
    /// <summary>Static resource: the rates of each zone.</summary>
    public const string RatesTable = """
        # Shipping rates
        Price = base price of the zone + price per kg x weight (kg). Parcels up to 30 kg.
        | Zone | Countries | Base price | Price per kg |
        |------|-----------|------------|--------------|
        | A | France | 4.90 EUR | 0.85 EUR |
        | B | European Union | 9.50 EUR | 1.70 EUR |
        | C | Rest of the world | 19.00 EUR | 3.40 EUR |
        """;

    /// <summary>Dynamic resource: computed each time the agent reads it.</summary>
    public static string Validity() =>
        $"Quotes made today are valid until {DateTime.Today.AddDays(7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.";

    /// <summary>Script: computes the price of a parcel and gives the quote an id the model cannot guess.</summary>
    [Description("Computes a shipping quote. Returns the quote id and the price in EUR.")]
    public static string Quote(
        [Description("Weight of the parcel in kg")] double weightKg,
        [Description("Zone: A, B or C")] string zone)
    {
        (decimal basePrice, decimal perKg) = zone.Trim().ToUpperInvariant() switch
        {
            "A" => (4.90m, 0.85m),
            "B" => (9.50m, 1.70m),
            "C" => (19.00m, 3.40m),
            _ => throw new ArgumentException($"Unknown zone '{zone}': use A, B or C.")
        };

        decimal price = Math.Round(basePrice + (perKg * (decimal)weightKg), 2);
        string quoteId = $"QT-{zone.Trim().ToUpperInvariant()}{(int)Math.Round(weightKg * 100)}-{(int)(price * 7) % 997:D3}";
        return JsonSerializer.Serialize(new { quoteId, priceEur = price.ToString("0.00", CultureInfo.InvariantCulture) });
    }
}
