namespace AIAgentWithSO.Models;

/// <summary>
/// Represents a structured response about a restaurant
/// </summary>
public class Restaurant
{
    public required string Name { get; set; }

    public required string ChefName { get; set; }

    public CuisineType Cuisine { get; set; }

    public int MichelinStars { get; set; }

    public decimal AveragePricePerPerson { get; set; }

    public required string City { get; set; }

    public required string Country { get; set; }

    public int YearEstablished { get; set; }
}

/// <summary>
/// Types of cuisine
/// </summary>
public enum CuisineType
{
    French,
    Italian,
    Japanese,
    Chinese,
    Mexican,
    Indian,
    Spanish,
    American,
    Mediterranean,
    Thai,
    Korean,
    Vietnamese,
    Other
}
