using System.Net;
using System.Net.Http.Headers;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Agents.AI;
using CommonUtilities;

namespace AIAgentWithCustomHttpTransport;

/// <summary>
/// Console output helpers for the HTTP traffic written by the logging handler, so that Program.cs focuses on
/// the handler and the transport. They never print a credential: the value of the <c>Authorization</c> and
/// <c>api-key</c> headers is masked.
/// </summary>
public static class HttpConsole
{
    // Headers that carry a credential. With the OpenAI SDK, the API key travels as "Authorization: Bearer <key>",
    // like a Microsoft Entra ID token; "api-key" is the other header accepted by Azure OpenAI.
    private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase) { "Authorization", "api-key" };

    private static readonly JsonSerializerOptions IndentedJson = new()
    {
        WriteIndented = true,
        // Display only: keep characters such as accents readable instead of \uXXXX escapes.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Writes the headers of a request or a response, followed by the headers of its content (Content-Type, Content-Length).
    /// </summary>
    public static void WriteHeaders(HttpHeaders headers, HttpContent? content)
    {
        ColoredConsole.WriteWarningLine("[Headers]");
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> allHeaders = content is null ? headers : headers.Concat(content.Headers);
        foreach ((string name, IEnumerable<string> values) in allHeaders)
        {
            ColoredConsole.WriteSecondaryLogLine($"   {name}: {MaskIfSensitive(name, string.Join(", ", values))}");
        }

        ColoredConsole.WriteEmptyLine();
    }

    /// <summary>
    /// Returns the value of a header as it can be displayed: for a credential header, only the authentication
    /// scheme is kept (<c>Bearer [MASKED]</c>). Never show a part of a key or a token.
    /// </summary>
    public static string MaskIfSensitive(string name, string value)
    {
        if (!SensitiveHeaders.Contains(name))
        {
            return value;
        }

        int space = value.IndexOf(' ');
        return space > 0 ? $"{value[..space]} [MASKED]" : "[MASKED]";
    }

    /// <summary>
    /// Reads and writes the body of a request or a response (indented when it is JSON).
    /// A streamed response (<c>text/event-stream</c>) is not read: reading it here would wait for the end of
    /// the stream before the SDK receives the first event.
    /// </summary>
    public static async Task WriteBodyAsync(HttpContent? content, CancellationToken cancellationToken)
    {
        if (content is null)
        {
            return;
        }

        if (content.Headers.ContentType?.MediaType == "text/event-stream")
        {
            ColoredConsole.WriteWarningLine("[Body] streamed response (text/event-stream): not read here, so that the SDK receives the events as they arrive");
            return;
        }

        // ReadAsStringAsync buffers the content: the SDK can still read it after the handler.
        string body = await content.ReadAsStringAsync(cancellationToken);
        if (body.Length == 0)
        {
            return;
        }

        ColoredConsole.WriteWarningLine("[Body]");
        foreach (string line in FormatJson(body).Split('\n'))
        {
            ColoredConsole.WriteSecondaryLogLine($"   {line}");
        }
    }

    /// <summary>Category of an HTTP status code.</summary>
    public static string StatusLabel(HttpStatusCode statusCode) => (int)statusCode switch
    {
        >= 200 and < 300 => "OK",
        >= 300 and < 400 => "REDIRECT",
        >= 400 and < 500 => "CLIENT ERROR",
        >= 500 => "SERVER ERROR",
        _ => "UNKNOWN"
    };

    /// <summary>
    /// Displays the token usage of an agent run. It is the total of the "usage" objects of all the HTTP responses of the run.
    /// </summary>
    public static void WriteTokenUsage(AgentResponse response)
    {
        ColoredConsole.WriteEmptyLine();
        ColoredConsole.WritePrimaryLogLine("Token Usage:");
        ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {response.Usage?.InputTokenCount}");
        ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {response.Usage?.OutputTokenCount}");
        ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {response.Usage?.TotalTokenCount}");
    }

    private static string FormatJson(string content)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(content);
            return JsonSerializer.Serialize(document.RootElement, IndentedJson);
        }
        catch (JsonException)
        {
            // Not JSON (an HTML error page, plain text...): shown as is
            return content;
        }
    }
}
