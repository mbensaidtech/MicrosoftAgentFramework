using Microsoft.Agents.AI;
using CommonUtilities;

using AIAgentWithSO.Models;

namespace AIAgentWithSO;

/// <summary>
/// Console output helpers shared by the scenarios, so that Program.cs focuses on structured output.
/// </summary>
public static class RestaurantConsole
{
    /// <summary>Displays the properties of a restaurant.</summary>
    public static void WriteRestaurant(Restaurant restaurant)
    {
        ColoredConsole.WriteSecondaryLogLine($"Name: {restaurant.Name}");
        ColoredConsole.WriteSecondaryLogLine($"Chef: {restaurant.ChefName}");
        ColoredConsole.WriteSecondaryLogLine($"Cuisine: {restaurant.Cuisine}");
        ColoredConsole.WriteSecondaryLogLine($"Michelin Stars: {restaurant.MichelinStars}");
        ColoredConsole.WriteSecondaryLogLine($"Average Price: €{restaurant.AveragePricePerPerson}");
        ColoredConsole.WriteSecondaryLogLine($"Location: {restaurant.City}, {restaurant.Country}");
        ColoredConsole.WriteSecondaryLogLine($"Established: {restaurant.YearEstablished}");
    }

    /// <summary>Displays the token usage of an agent run (also accepts an <see cref="AgentResponse{T}"/>).</summary>
    public static void WriteTokenUsage(AgentResponse response)
    {
        ColoredConsole.WriteDividerLine();
        ColoredConsole.WritePrimaryLogLine("Token Usage:");
        ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {response.Usage?.InputTokenCount}");
        ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {response.Usage?.OutputTokenCount}");
        ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {response.Usage?.TotalTokenCount}");
    }
}
