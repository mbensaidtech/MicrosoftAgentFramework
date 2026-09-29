using System.Text.Json;
using AgenticRAG.Models;

namespace AgenticRAG;

/// <summary>
/// Reads the FAQ entries of <c>Data/sav-faq.json</c> (copied next to the program at build time).
/// </summary>
public static class FaqData
{
    public const string FileName = "Data/sav-faq.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Loads the FAQ entries from the JSON file.
    /// </summary>
    public static async Task<List<FaqRecord>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using FileStream stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, FileName));
        return await JsonSerializer.DeserializeAsync<List<FaqRecord>>(stream, JsonOptions, cancellationToken) ?? [];
    }
}
