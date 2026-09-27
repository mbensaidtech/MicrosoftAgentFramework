using System.ComponentModel;

namespace AIAgentWithSO.Models;

/// <summary>
/// Represents a structured response about a restaurant.
/// The [Description] attributes are copied into the JSON schema generated from this type
/// (RunAsync&lt;T&gt;, ChatResponseFormat.ForJsonSchema&lt;T&gt;()), which guides the model.
/// </summary>
[Description("Information about a restaurant")]
public class Restaurant
{
    [Description("Name of the restaurant")]
    public required string Name { get; set; }

    [Description("Name of the head chef")]
    public required string ChefName { get; set; }

    [Description("Main type of cuisine")]
    public CuisineType Cuisine { get; set; }

    [Description("Number of Michelin stars, from 0 to 3")]
    public int MichelinStars { get; set; }

    [Description("Average price per person, in euros")]
    public decimal AveragePricePerPerson { get; set; }

    [Description("City where the restaurant is located")]
    public required string City { get; set; }

    [Description("Country where the restaurant is located")]
    public required string Country { get; set; }

    [Description("Year the restaurant was established")]
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
