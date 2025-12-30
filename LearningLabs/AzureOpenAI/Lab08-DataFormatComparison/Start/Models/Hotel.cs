namespace DataFormatComparison.Models;

/// <summary>
/// Represents a hotel
/// </summary>
public class Hotel
{
    public required string Name { get; set; }
    public required string City { get; set; }
    public int Stars { get; set; }
    public decimal PricePerNight { get; set; }
    public string Currency { get; set; } = "USD";
    public int Rooms { get; set; }
    public bool HasPool { get; set; }
    public required bool HasWifi { get; set; }
    public double Rating { get; set; }
}

