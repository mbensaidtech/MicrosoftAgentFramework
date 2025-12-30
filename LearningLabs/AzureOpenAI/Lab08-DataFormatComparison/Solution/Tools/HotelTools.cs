using System.Text.Json;
using DataFormatComparison.Models;

namespace DataFormatComparison.Tools;

public static class HotelTools
{
    private static readonly string HotelsFilePath = Path.Combine(AppContext.BaseDirectory, "Data", "hotels.json");
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>
    /// Returns all hotels as a List of Hotel objects
    /// </summary>
    public static string GetAllHotelsUsingJsonFormat()
    {
        return LoadHotelsFromJson();
    }

    /// <summary>
    /// Returns all hotels as a formatted string
    /// </summary>
    public static string GetAllHotelsUsingToonFormat()
    {
        var hotels = JsonSerializer.Deserialize<List<Hotel>>(LoadHotelsFromJson(), JsonOptions) 
            ?? new List<Hotel>();
        return ToonNetSerializer.ToonNet.Encode(hotels);
    }

    /// <summary>
    /// Loads all hotels from the JSON file (shared by all methods)
    /// </summary>
    private static string LoadHotelsFromJson()
    {
       return File.ReadAllText(HotelsFilePath);
    }
}
