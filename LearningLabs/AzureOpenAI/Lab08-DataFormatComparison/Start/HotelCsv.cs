using System.Globalization;
using System.Text;
using DataFormatComparison.Models;

namespace DataFormatComparison;

/// <summary>
/// CSV encoding of the hotels: one header line, then one line per hotel with comma-separated values
/// (RFC 4180: a value that contains a comma, a quote or a line break is quoted).
/// This is the compact text format compared with JSON in this lab: the property names appear once, in the header,
/// instead of being repeated for every hotel, and there are no braces, brackets or quotes around the values.
/// </summary>
public static class HotelCsv
{
    /// <summary>The header line: the columns, in the order of the values of every line.</summary>
    public const string Header = "Name,City,Stars,PricePerNight,Currency,Rooms,HasPool,HasWifi,Rating";

    private static readonly string[] Columns = Header.Split(',');

    /// <summary>
    /// Encodes the hotels as CSV text (lines separated by '\n', invariant culture for the numbers).
    /// </summary>
    public static string Serialize(IEnumerable<Hotel> hotels)
    {
        StringBuilder csv = new(Header);
        foreach (Hotel hotel in hotels)
        {
            csv.Append('\n');
            csv.Append(string.Join(',',
                Quote(hotel.Name),
                Quote(hotel.City),
                hotel.Stars.ToString(CultureInfo.InvariantCulture),
                hotel.PricePerNight.ToString(CultureInfo.InvariantCulture),
                Quote(hotel.Currency),
                hotel.Rooms.ToString(CultureInfo.InvariantCulture),
                hotel.HasPool ? "true" : "false",
                hotel.HasWifi ? "true" : "false",
                hotel.Rating.ToString(CultureInfo.InvariantCulture)));
        }

        return csv.ToString();
    }

    /// <summary>
    /// Parses CSV text produced by <see cref="Serialize"/> (or by the model, when asked for the same format) back into hotels.
    /// Lines of Markdown code fences (```) are ignored, in case the model adds some despite the instructions.
    /// </summary>
    /// <exception cref="FormatException">The text is not in the expected format (wrong header, wrong number of values, invalid value).</exception>
    public static List<Hotel> Deserialize(string csv)
    {
        List<string> lines = [.. csv.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith("```", StringComparison.Ordinal))];

        if (lines.Count == 0 || !string.Equals(lines[0].Replace(" ", string.Empty), Header, StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException($"The first line must be the header '{Header}', got: '{lines.FirstOrDefault()}'.");
        }

        List<Hotel> hotels = [];
        for (int index = 1; index < lines.Count; index++)
        {
            string[] values = SplitLine(lines[index]);
            if (values.Length != Columns.Length)
            {
                throw new FormatException($"Line {index + 1} has {values.Length} values instead of {Columns.Length}: '{lines[index]}'.");
            }

            try
            {
                hotels.Add(new Hotel
                {
                    Name = values[0],
                    City = values[1],
                    Stars = int.Parse(values[2], CultureInfo.InvariantCulture),
                    PricePerNight = decimal.Parse(values[3], CultureInfo.InvariantCulture),
                    Currency = values[4],
                    Rooms = int.Parse(values[5], CultureInfo.InvariantCulture),
                    HasPool = ParseBool(values[6]),
                    HasWifi = ParseBool(values[7]),
                    Rating = double.Parse(values[8], CultureInfo.InvariantCulture)
                });
            }
            catch (Exception ex) when (ex is FormatException or OverflowException)
            {
                throw new FormatException($"Line {index + 1} cannot be parsed: '{lines[index]}' ({ex.Message})", ex);
            }
        }

        return hotels;
    }

    private static string Quote(string value) =>
        value.AsSpan().IndexOfAny(",\"\n") >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

    private static bool ParseBool(string value) => value.Trim().ToLowerInvariant() switch
    {
        "true" or "yes" or "1" => true,
        "false" or "no" or "0" => false,
        _ => throw new FormatException($"'{value}' is not a boolean (true/false).")
    };

    /// <summary>Splits one CSV line into its values, honoring quoted values.</summary>
    private static string[] SplitLine(string line)
    {
        List<string> values = [];
        StringBuilder current = new();
        bool quoted = false;

        for (int index = 0; index < line.Length; index++)
        {
            char character = line[index];
            if (quoted)
            {
                if (character == '"' && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else if (character == '"')
                {
                    quoted = false;
                }
                else
                {
                    current.Append(character);
                }
            }
            else if (character == '"')
            {
                quoted = true;
            }
            else if (character == ',')
            {
                values.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        values.Add(current.ToString().Trim());
        return [.. values];
    }
}
