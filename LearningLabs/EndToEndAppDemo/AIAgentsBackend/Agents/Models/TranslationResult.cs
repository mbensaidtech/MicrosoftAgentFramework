using System.Text.Json.Serialization;

namespace AIAgentsBackend.Agents.Models;

/// <summary>
/// Model for structured translation output.
/// </summary>
public class TranslationResult
{
    /// <summary>
    /// The translated text.
    /// </summary>
    [JsonPropertyName("translatedText")]
    public string TranslatedText { get; set; } = string.Empty;

    /// <summary>
    /// The source language of the original text (e.g., "English", "French", "en", "fr").
    /// </summary>
    [JsonPropertyName("sourceLanguage")]
    public string SourceLanguage { get; set; } = string.Empty;

    /// <summary>
    /// The target language of the translation (e.g., "Spanish", "German", "es", "de").
    /// </summary>
    [JsonPropertyName("targetLanguage")]
    public string TargetLanguage { get; set; } = string.Empty;
}
