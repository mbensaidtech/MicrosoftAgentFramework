using CommonUtilities;
using System.Text.Json;

namespace AIAgentWithCustomHttpTransport;

public class CustomHttpClientHandler : HttpClientHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        //Step 1: Add custom headers here
        request.Headers.Add("X-agent-name", "AIAgentWithCustomHttpTransport");

        //Step 2: Write the HTTP request to the console
        ColoredConsole.WriteDividerLine();
        ColoredConsole.WriteInfoLine("=== HTTP REQUEST ===");
        ColoredConsole.WriteEmptyLine();

        ColoredConsole.WritePrimaryLogLine($"[URL] {request.Method} {request.RequestUri}");
        ColoredConsole.WriteEmptyLine();

        ColoredConsole.WriteWarningLine("[Headers]");
        foreach (var header in request.Headers)
        {
            var value = string.Join(", ", header.Value);
            if (header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Length > 20 ? $"{value[..20]}... [MASKED]" : "[MASKED]";
            }
            ColoredConsole.WriteSecondaryLogLine($"   {header.Key}: {value}");
        }
        ColoredConsole.WriteEmptyLine();

        if (request.Content != null)
        {
            var httpRequest = await request.Content.ReadAsStringAsync(cancellationToken);
            ColoredConsole.WriteWarningLine("[Body]");
            WriteFormattedJson(httpRequest);
        }

        //Step 3: Send the HTTP request to the Azure OpenAI API
        var response = await base.SendAsync(request, cancellationToken);

        //Step 4: Write the HTTP response to the console
        ColoredConsole.WriteEmptyLine();
        ColoredConsole.WriteSuccessLine("=== HTTP RESPONSE ===");
        ColoredConsole.WriteEmptyLine();

        ColoredConsole.WritePrimaryLogLine($"[Status] {(int)response.StatusCode} {response.StatusCode} ({GetStatusLabel(response.StatusCode)})");
        ColoredConsole.WriteEmptyLine();

        ColoredConsole.WriteWarningLine("[Headers]");
        foreach (var header in response.Headers)
        {
            ColoredConsole.WriteSecondaryLogLine($"   {header.Key}: {string.Join(", ", header.Value)}");
        }
        ColoredConsole.WriteEmptyLine();

        if (response.Content != null)
        {
            var httpResponse = await response.Content.ReadAsStringAsync(cancellationToken);
            ColoredConsole.WriteWarningLine("[Body]");
            WriteFormattedJson(httpResponse);
        }

        ColoredConsole.WriteDividerLine();

        return response;
    }

    private static string GetStatusLabel(System.Net.HttpStatusCode statusCode) => (int)statusCode switch
    {
        >= 200 and < 300 => "OK",
        >= 300 and < 400 => "REDIRECT",
        >= 400 and < 500 => "CLIENT ERROR",
        >= 500 => "SERVER ERROR",
        _ => "UNKNOWN"
    };

    private static void WriteFormattedJson(string content)
    {
        try
        {
            var jsonDocument = JsonDocument.Parse(content);
            var formattedJson = JsonSerializer.Serialize(jsonDocument, JsonOptions);
            
            foreach (var line in formattedJson.Split('\n'))
            {
                ColoredConsole.WriteSecondaryLogLine($"   {line}");
            }
        }
        catch
        {
            ColoredConsole.WriteSecondaryLogLine($"   {content}");
        }
    }
}
