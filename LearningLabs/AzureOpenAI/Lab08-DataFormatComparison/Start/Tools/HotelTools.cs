using System.ComponentModel;
using DataFormatComparison.Models;

namespace DataFormatComparison.Tools;

/// <summary>
/// The function tools of the lab: the same hotel catalog, returned in two formats.
/// The model reads the [Description] attributes to decide when to call a tool; the return value is what it receives:
/// - objects are serialized as JSON by the framework (indented, camelCase property names);
/// - a string is sent as is (as a JSON string, so a line break costs two characters: \n).
/// </summary>
public sealed class HotelTools(IReadOnlyList<Hotel> hotels)
{
    /// <summary>
    /// Returns the hotels as objects: the framework serializes them as JSON for the model.
    /// </summary>
    [Description("Returns all the hotels of the catalog: name, city, stars, price per night, currency, number of rooms, pool, wifi and rating.")]
    public IReadOnlyList<Hotel> GetAllHotelsAsJson() => hotels;

    /// <summary>
    /// Returns the hotels as CSV text: a header line, then one line per hotel.
    /// </summary>
    [Description("Returns all the hotels of the catalog as CSV text: a header line (Name,City,Stars,PricePerNight,Currency,Rooms,HasPool,HasWifi,Rating), then one line per hotel.")]
    public string GetAllHotelsAsCsv() => HotelCsv.Serialize(hotels);
}
