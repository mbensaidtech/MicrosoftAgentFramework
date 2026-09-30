using System.Text.Json;
using DataFormatComparison.Models;

namespace DataFormatComparison;

/// <summary>
/// Reads the hotels of <c>Data/hotels.json</c> (copied next to the program at build time).
/// </summary>
public static class HotelData
{
    public const string FileName = "Data/hotels.json";

    // The file uses snake_case property names (price_per_night, has_pool...)
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>
    /// Loads the hotels from the JSON file.
    /// </summary>
    public static async Task<List<Hotel>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using FileStream stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, FileName));
        return await JsonSerializer.DeserializeAsync<List<Hotel>>(stream, JsonOptions, cancellationToken) ?? [];
    }
}
