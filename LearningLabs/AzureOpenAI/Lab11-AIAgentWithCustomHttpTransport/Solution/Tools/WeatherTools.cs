using System.ComponentModel;

namespace AIAgentWithCustomHttpTransport.Tools;

/// <summary>
/// Function tool of scenario 2 (same tool as the official "add tools" getting-started sample).
/// The [Description] attributes are sent to the model in the "tools" array of the HTTP request: look for them in the logged body.
/// </summary>
public static class WeatherTools
{
    [Description("Get the weather for a given location.")]
    public static string GetWeather([Description("The location to get the weather for.")] string location)
        => $"The weather in {location} is cloudy with a high of 15 degrees Celsius.";
}
