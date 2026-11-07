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

    /// <summary>
    /// Scenario 6: the text of one FAQ entry, as a small Markdown document to upload to the file search of the service.
    /// One document per entry: the service splits each file into chunks, so one entry never shares a chunk with another one.
    /// </summary>
    public static string ToDocument(FaqRecord entry) =>
        $"# {entry.Id}\n\n## Question\n\n{entry.Question}\n\n## Answer\n\n{entry.Answer}\n";
}
