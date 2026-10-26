using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace LabDashboard.Tests;

/// <summary>A request as the admin API received it, captured (body copied) before the client wipes anything.</summary>
internal sealed record CapturedRequest(HttpMethod Method, Uri Uri, string? ApiKey, string? Bearer, string Accept, string? ContentType, byte[]? Body)
{
    public string? BodyText => Body is null ? null : Encoding.UTF8.GetString(Body);

    public override string ToString() => $"{Method} {Uri}";
}

/// <summary>
/// In-process stand-in for the admin API at the <see cref="HttpMessageHandler"/> level: no socket, never the network. Records every request
/// and answers with <see cref="Respond"/> (by default a 404 HTML page, as a server without the routes would).
/// </summary>
internal sealed class FakeAdminHandler : HttpMessageHandler
{
    public ConcurrentQueue<CapturedRequest> Requests { get; } = new();

    public Func<CapturedRequest, CancellationToken, Task<HttpResponseMessage>> Respond { get; set; } =
        (_, _) => Task.FromResult(Reply(404, "<!doctype html><title>404</title>", "text/html"));

    /// <summary>Answers every request with a fresh copy of the same answer.</summary>
    public void Always(Func<CapturedRequest, HttpResponseMessage> respond) => Respond = (request, _) => Task.FromResult(respond(request));

    public static HttpResponseMessage Reply(int status, string? body = null, string mediaType = "application/json", int? retryAfterSeconds = null, string? wwwAuthenticate = null)
    {
        HttpResponseMessage response = new((HttpStatusCode)status);
        if (body is not null)
        {
            response.Content = new StringContent(body, Encoding.UTF8, mediaType);
        }

        if (retryAfterSeconds is { } seconds)
        {
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(seconds));
        }

        if (wwwAuthenticate is not null)
        {
            response.Headers.WwwAuthenticate.ParseAdd(wwwAuthenticate);
        }

        return response;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        byte[]? body = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        CapturedRequest captured = new(
            request.Method,
            request.RequestUri!,
            request.Headers.TryGetValues("X-API-Key", out IEnumerable<string>? keys) ? keys.Single() : null,
            request.Headers.Authorization is { Scheme: "Bearer" } authorization ? authorization.Parameter : null,
            request.Headers.Accept.ToString(),
            request.Content?.Headers.ContentType?.ToString(),
            body);
        Requests.Enqueue(captured);
        HttpResponseMessage response = await Respond(captured, cancellationToken);
        response.RequestMessage = request;
        return response;
    }
}
