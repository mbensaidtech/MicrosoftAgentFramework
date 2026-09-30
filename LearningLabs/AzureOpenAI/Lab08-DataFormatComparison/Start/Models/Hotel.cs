using System.ComponentModel;

namespace DataFormatComparison.Models;

/// <summary>
/// A hotel of the catalog (Data/hotels.json).
/// The same class is used everywhere in the lab: read from the JSON file, returned by the JSON tool (the framework
/// serializes it as JSON for the model), deserialized from the structured output of scenario 1 (the [Description]
/// attributes are copied into the JSON schema sent to the model) and parsed back from the CSV answer of scenario 2.
/// </summary>
[Description("A hotel of the catalog")]
public sealed class Hotel
{
    [Description("Name of the hotel")]
    public required string Name { get; set; }

    [Description("City where the hotel is located")]
    public required string City { get; set; }

    [Description("Number of stars, from 1 to 5")]
    public int Stars { get; set; }

    [Description("Price of one night")]
    public decimal PricePerNight { get; set; }

    [Description("Currency of the price, e.g. USD")]
    public string Currency { get; set; } = "USD";

    [Description("Number of rooms")]
    public int Rooms { get; set; }

    [Description("Whether the hotel has a swimming pool")]
    public bool HasPool { get; set; }

    [Description("Whether the hotel offers wifi")]
    public bool HasWifi { get; set; }

    [Description("Average guest rating, from 0 to 5")]
    public double Rating { get; set; }
}
