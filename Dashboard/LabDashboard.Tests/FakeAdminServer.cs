using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LabDashboard.Tests;

/// <summary>A received request of the fake admin API.</summary>
internal sealed record AdminRequest(string Method, string Path, JsonNode? Body, string? Bearer, string? WorkshopKey, string? ApiKey, string? Query);

/// <summary>A canned answer for the next request of a path (status, optional Retry-After seconds, optional JSON body).</summary>
internal sealed record CannedResponse(int Status, int? RetryAfterSeconds = null, string? Body = null);

/// <summary>
/// In-process stand-in for the public API of the admin dashboard (register, events, help requests, me, health), on a loopback port.
/// Behaves like the spec by default (every /api/v1 route but health needs <see cref="ApiKey"/> in X-API-Key); tests queue canned responses or make it hang.
/// </summary>
internal sealed class FakeAdminServer : IAsyncDisposable
{
    public const string WorkshopKey = "dev-workshop-key";
    public const string ApiKey = "lbk_test_fake_admin_api_key_9f3c";

    private readonly WebApplication _app;

    private FakeAdminServer(int port)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, port));
        _app = builder.Build();
        Url = $"http://127.0.0.1:{port}";

        _app.Use(async (context, next) =>
        {
            if (Hang)
            {
                // Accepts the connection and never answers: the client's timeout is the only way out.
                await Task.Delay(Timeout.Infinite, context.RequestAborted);
                return;
            }

            JsonNode? body = null;
            if (context.Request.ContentLength is > 0 || context.Request.Headers.TransferEncoding.Count > 0)
            {
                string text = await new StreamReader(context.Request.Body).ReadToEndAsync();
                body = string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
            }

            string? auth = context.Request.Headers.Authorization.FirstOrDefault();
            AdminRequest request = new(
                context.Request.Method, context.Request.Path.Value ?? "", body,
                auth?.StartsWith("Bearer ", StringComparison.Ordinal) == true ? auth[7..] : null,
                context.Request.Headers["X-Workshop-Key"].FirstOrDefault(),
                context.Request.Headers["x-api-key"].FirstOrDefault(),
                context.Request.QueryString.Value);
            Requests.Enqueue(request);
            context.Items["request"] = request;

            if (request.Path != "/api/v1/health" && request.ApiKey != ApiKey)
            {
                context.Response.StatusCode = 401;
                context.Response.Headers.WWWAuthenticate = "ApiKey header=\"X-API-Key\"";
                await context.Response.WriteAsJsonAsync(new { error = "invalidApiKey", message = "API key missing or invalid." });
                return;
            }

            if (Canned.TryGetValue(request.Path, out ConcurrentQueue<CannedResponse>? queue) && queue.TryDequeue(out CannedResponse? canned))
            {
                context.Response.StatusCode = canned.Status;
                if (canned.RetryAfterSeconds is { } seconds)
                {
                    context.Response.Headers.RetryAfter = seconds.ToString();
                }

                if (canned.Body is not null)
                {
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(canned.Body);
                }

                return;
            }

            await next();
        });

        _app.MapGet("/api/v1/health", () => Results.Ok(new { status = "ok" }));

        _app.MapPost("/api/v1/devs/register", (HttpContext context) =>
        {
            AdminRequest request = (AdminRequest)context.Items["request"]!;
            string userId = request.Body?["userId"]?.ToString() ?? "";
            if (request.Bearer is not null && Tokens.TryGetValue(userId, out string? known) && known == request.Bearer)
            {
                return Results.Ok(new { userId, username = request.Body?["username"]?.ToString(), serverTime = DateTimeOffset.UtcNow });
            }

            if (request.WorkshopKey != WorkshopKey)
            {
                return Results.Json(new { error = "unauthorized" }, statusCode: 401);
            }

            if (Tokens.ContainsKey(userId))
            {
                return Results.Json(new { error = "forbidden" }, statusCode: 403);
            }

            string token = $"tok-{Guid.NewGuid():N}";
            Tokens[userId] = token;
            return Results.Json(new { userId, username = request.Body?["username"]?.ToString(), devToken = token, serverTime = DateTimeOffset.UtcNow }, statusCode: 201);
        });

        _app.MapPost("/api/v1/events", (HttpContext context) =>
        {
            AdminRequest request = (AdminRequest)context.Items["request"]!;
            if (!Authorized(request))
            {
                return Results.Json(new { error = "unauthorized" }, statusCode: 401);
            }

            JsonArray events = request.Body?["events"] as JsonArray ?? [];
            int accepted = 0, ignored = 0;
            foreach (JsonNode? node in events)
            {
                string id = node?["eventId"]?.ToString() ?? "";
                if (SeenEventIds.Add(id))
                {
                    accepted++;
                    Events.Add(node!.DeepClone());
                }
                else
                {
                    ignored++;
                }
            }

            return Results.Json(new { accepted, ignored, serverTime = DateTimeOffset.UtcNow }, statusCode: 202);
        });

        _app.MapPost("/api/v1/help-requests", (HttpContext context) =>
        {
            AdminRequest request = (AdminRequest)context.Items["request"]!;
            if (!Authorized(request))
            {
                return Results.Json(new { error = "unauthorized" }, statusCode: 401);
            }

            if (ActiveHelp is { } existing)
            {
                return Results.Json(new { error = "conflict", details = new { existing } }, statusCode: 409);
            }

            ActiveHelp = new JsonObject
            {
                ["id"] = $"help-{Guid.NewGuid():N}",
                ["status"] = "open",
                ["labId"] = request.Body?["labId"]?.DeepClone(),
                ["message"] = request.Body?["message"]?.DeepClone(),
                ["createdAt"] = DateTimeOffset.UtcNow.ToString("O"),
            };
            return Results.Json(ActiveHelp, statusCode: 201);
        });

        _app.MapPost("/api/v1/help-requests/{id}/cancel", (string id, HttpContext context) =>
        {
            AdminRequest request = (AdminRequest)context.Items["request"]!;
            if (!Authorized(request))
            {
                return Results.Json(new { error = "unauthorized" }, statusCode: 401);
            }

            if (ActiveHelp?["id"]?.ToString() != id)
            {
                return Results.Json(new { error = "notFound" }, statusCode: 404);
            }

            ActiveHelp["status"] = "cancelled";
            ActiveHelp["closedAt"] = DateTimeOffset.UtcNow.ToString("O");
            JsonNode closed = ActiveHelp;
            ActiveHelp = null;
            return Results.Ok(closed);
        });

        _app.MapGet("/api/v1/devs/me", (HttpContext context) =>
        {
            AdminRequest request = (AdminRequest)context.Items["request"]!;
            if (!Authorized(request))
            {
                return Results.Json(new { error = "unauthorized" }, statusCode: 401);
            }

            return Results.Json(new JsonObject
            {
                ["activeHelpRequest"] = ActiveHelp?["status"]?.ToString() is "open" or "acknowledged" ? ActiveHelp.DeepClone() : null,
                ["lastHelpRequest"] = ActiveHelp?.DeepClone(),
                ["serverTime"] = DateTimeOffset.UtcNow.ToString("O"),
            });
        });
    }

    public string Url { get; }

    public bool Hang { get; set; }

    public ConcurrentQueue<AdminRequest> Requests { get; } = new();

    public ConcurrentDictionary<string, ConcurrentQueue<CannedResponse>> Canned { get; } = new(StringComparer.Ordinal);

    /// <summary>userId → dev token issued at registration.</summary>
    public ConcurrentDictionary<string, string> Tokens { get; } = new(StringComparer.Ordinal);

    public HashSet<string> SeenEventIds { get; } = new(StringComparer.Ordinal);

    public List<JsonNode> Events { get; } = [];

    /// <summary>The help request of the (single) developer, as the admin would store it. Tests edit it to acknowledge or resolve.</summary>
    public JsonObject? ActiveHelp { get; set; }

    public static int FreePort()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    public static async Task<FakeAdminServer> StartAsync(int? port = null)
    {
        FakeAdminServer server = new(port ?? FreePort());
        await server._app.StartAsync();
        return server;
    }

    public void Expect(string path, params CannedResponse[] responses)
    {
        ConcurrentQueue<CannedResponse> queue = Canned.GetOrAdd(path, _ => new ConcurrentQueue<CannedResponse>());
        foreach (CannedResponse response in responses)
        {
            queue.Enqueue(response);
        }
    }

    public IEnumerable<AdminRequest> RequestsTo(string path) => Requests.Where(r => r.Path == path);

    public IEnumerable<string> EventTypes => Events.Select(e => e["type"]?.ToString() ?? "");

    private bool Authorized(AdminRequest request) =>
        request.Bearer is not null && Tokens.TryGetValue(request.Body?["userId"]?.ToString() ?? "", out string? token) && token == request.Bearer
        || request.Bearer is not null && request.Body is null && Tokens.Values.Contains(request.Bearer);

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
