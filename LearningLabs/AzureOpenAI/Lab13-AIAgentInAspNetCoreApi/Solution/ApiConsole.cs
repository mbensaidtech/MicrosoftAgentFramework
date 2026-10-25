using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Text;
using CommonUtilities;

namespace AIAgentWebApi;

/// <summary>
/// The client side of the scenarios: it calls the API as a front end would, and displays what comes back, so that
/// Program.cs focuses on the API. It never prints a token.
/// </summary>
public static class ApiConsole
{
    /// <summary>
    /// Sends a message to a conversation of the API, with the bearer token of a user (or none), and displays the HTTP status,
    /// then the answer as it arrives (Server-Sent Events) and the number of events received.
    /// Returns the whole answer (empty when the request was refused).
    /// </summary>
    public static async Task<string> SendMessageAsync(HttpClient httpClient, string? token, Guid conversationId, string message)
    {
        ColoredConsole.WriteUserLine($"POST /api/conversations/{conversationId}/messages: {message}");
        using HttpRequestMessage request = new(HttpMethod.Post, $"api/conversations/{conversationId}/messages")
        {
            Content = JsonContent.Create(new ChatRequest(message))
        };
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // ResponseHeadersRead: read the events while the server is still writing the answer
        using HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        ColoredConsole.WritePrimaryLogLine($"HTTP {(int)response.StatusCode} {response.StatusCode} ({response.Content.Headers.ContentType?.MediaType ?? "no content"})");
        if (!response.IsSuccessStatusCode)
        {
            return string.Empty;
        }

        StringBuilder answer = new();
        int events = 0;
        await using Stream stream = await response.Content.ReadAsStreamAsync();
        Console.Write("Agent: ");
        await foreach (SseItem<string> item in SseParser.Create(stream).EnumerateAsync())
        {
            events++;
            answer.Append(item.Data);
            Console.Write(item.Data);
        }

        Console.WriteLine();
        ColoredConsole.WriteSecondaryLogLine($"Server-sent events received: {events}");
        return answer.ToString();
    }
}
