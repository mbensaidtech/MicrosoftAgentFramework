using CommonUtilities;
using System.Text.Json;

namespace AIAgentWithCustomHttpTransport;

/// <summary>
/// Custom HTTP handler to intercept and log HTTP requests/responses to Azure OpenAI.
/// Students should implement the SendAsync method to:
/// - Add custom headers
/// - Log requests and responses using ColoredConsole
/// </summary>
public class CustomHttpClientHandler : HttpClientHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Step 1: Add custom headers here
        // TODO: Add a custom header "X-agent-name" with value "AIAgentWithCustomHttpTransport"

        // Step 2: Write the HTTP request to the console
        // TODO: Display request method, URI, headers, and body

        // Step 3: Send the HTTP request to the Azure OpenAI API
        // TODO: Call the base class method and store the response

        // Step 4: Write the HTTP response to the console
        // TODO: Display response status, headers, and body

        throw new NotImplementedException("Complete the implementation of SendAsync");
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
