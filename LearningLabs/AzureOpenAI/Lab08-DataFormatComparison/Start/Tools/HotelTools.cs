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
    /// Returns all hotels in JSON format
    /// </summary>
    public static string GetAllHotelsUsingJsonFormat()
    {
        return LoadHotelsFromJson();
    }

    /// <summary>
    /// Returns all hotels in Toon format
    /// </summary>
    public static string GetAllHotelsUsingToonFormat()
    {
        var hotels = JsonSerializer.Deserialize<List<Hotel>>(LoadHotelsFromJson(), JsonOptions) 
            ?? new List<Hotel>();
        
        // TODO: Encode hotels to Toon format using ToonNetSerializer
        throw new NotImplementedException();
    }

    /// <summary>
    /// Loads all hotels from the JSON file (shared by all methods)
    /// </summary>
    private static string LoadHotelsFromJson()
    {
        return File.ReadAllText(HotelsFilePath);
    }
}

