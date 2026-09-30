using System.Globalization;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CommonUtilities;
using DataFormatComparison.Models;

namespace DataFormatComparison;

/// <summary>
/// Console output helpers shared by the scenarios, so that Program.cs focuses on the data formats.
/// </summary>
public static class FormatConsole
{
    /// <summary>
    /// Displays a list of hotels under a heading with their count.
    /// </summary>
    public static void WriteHotels(IReadOnlyCollection<Hotel> hotels, string heading)
    {
        ColoredConsole.WritePrimaryLogLine($"{heading}: {hotels.Count}");
        foreach (Hotel hotel in hotels)
        {
            ColoredConsole.WriteSecondaryLogLine(string.Create(CultureInfo.InvariantCulture,
                $"  {hotel.Name} ({hotel.City}) - {hotel.PricePerNight} {hotel.Currency}/night - stars: {hotel.Stars} - rating: {hotel.Rating}"));
        }
    }

    /// <summary>
    /// Displays the tools called during an agent run, with their arguments, and the size of the tool results
    /// exactly as they were sent to the model (the function calls and results of the tool loop are kept in the response messages).
    /// </summary>
    public static void WriteToolCalls(AgentResponse response)
    {
        List<FunctionCallContent> calls = [.. response.Messages.SelectMany(message => message.Contents).OfType<FunctionCallContent>()];
        if (calls.Count == 0)
        {
            ColoredConsole.WriteWarningLine("Tool called: (none)");
            return;
        }

        foreach (FunctionCallContent call in calls)
        {
            string arguments = string.Join(", ", call.Arguments?.Select(argument => $"{argument.Key}: {argument.Value}") ?? []);
            ColoredConsole.WriteWarningLine($"Tool called: {call.Name}({arguments})");
        }

        ColoredConsole.WriteWarningLine($"Tool result sent to the model: {ToolResultLength(response)} characters");
    }

    /// <summary>
    /// The total size, in characters, of the tool results of a run as the model received them.
    /// </summary>
    public static int ToolResultLength(AgentResponse response) =>
        response.Messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>().Sum(result => ToolResultText(result).Length);

    /// <summary>
    /// The text of a tool result as the OpenAI chat client sends it: a string as is, anything else serialized as JSON.
    /// (The framework has already turned the return value of the tool into a JsonElement: a string becomes a JSON string,
    /// with quotes and escaped line breaks; objects become indented JSON.)
    /// </summary>
    private static string ToolResultText(FunctionResultContent result) => result.Result switch
    {
        null => string.Empty,
        string text => text,
        object value => JsonSerializer.Serialize(value, AIJsonUtilities.DefaultOptions.GetTypeInfo(typeof(object)))
    };

    /// <summary>
    /// Displays the token usage of an agent run.
    /// With function tools, one run makes several model calls (the model asks for the tool, then answers with its result):
    /// the usage of the response is the total of all these calls. The tool result is part of the input tokens of the second call.
    /// </summary>
    public static void WriteTokenUsage(AgentResponse response, string heading = "Token Usage:")
    {
        ColoredConsole.WriteDividerLine();
        ColoredConsole.WritePrimaryLogLine(heading);
        ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {response.Usage?.InputTokenCount}");
        ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {response.Usage?.OutputTokenCount}");
        ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {response.Usage?.TotalTokenCount}");
    }

    /// <summary>
    /// Displays side by side what the model received (size of the tool result) and consumed (tokens) in the JSON run and in the CSV run.
    /// </summary>
    public static void WriteComparison(AgentResponse jsonResponse, AgentResponse csvResponse)
    {
        ColoredConsole.WriteDividerLine();
        ColoredConsole.WriteInfoLine("=== Comparison: JSON vs CSV ===");
        ColoredConsole.WriteSecondaryLogLine($"{string.Empty,-22}{"JSON",10}{"CSV",10}{"CSV vs JSON",14}");
        WriteComparisonRow("Tool result (chars)", ToolResultLength(jsonResponse), ToolResultLength(csvResponse));
        WriteComparisonRow("Input tokens", jsonResponse.Usage?.InputTokenCount, csvResponse.Usage?.InputTokenCount);
        WriteComparisonRow("Output tokens", jsonResponse.Usage?.OutputTokenCount, csvResponse.Usage?.OutputTokenCount);
        WriteComparisonRow("Total tokens", jsonResponse.Usage?.TotalTokenCount, csvResponse.Usage?.TotalTokenCount);
    }

    private static void WriteComparisonRow(string label, long? json, long? csv)
    {
        string difference = json is > 0 && csv is not null
            ? string.Create(CultureInfo.InvariantCulture, $"{(csv.Value - json.Value) * 100.0 / json.Value:+0;-0;0}%")
            : "n/a";
        ColoredConsole.WritePrimaryLogLine(string.Create(CultureInfo.InvariantCulture, $"{label,-22}{json,10}{csv,10}{difference,14}"));
    }
}
