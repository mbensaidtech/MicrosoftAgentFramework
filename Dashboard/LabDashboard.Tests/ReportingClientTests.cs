using System.Diagnostics;
using System.Text.Json.Nodes;
using LabDashboard.Catalog;
using LabDashboard.Execution;
using LabDashboard.History;
using LabDashboard.Reporting;
using LabDashboard.Settings;
using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LabDashboard.Tests;

/// <summary>
/// The reporting client against an in-process fake of the admin API (<see cref="FakeAdminServer"/>):
/// registration, ordered batches, backoff, Retry-After, re-registration, help requests, the API key, and a run that a hanging server cannot slow down.
/// </summary>
public sealed class ReportingClientTests : IAsyncDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("labbench-reporting-client-").FullName;
    private readonly List<FakeAdminServer> _servers = [];

    public async ValueTask DisposeAsync()
    {
        foreach (FakeAdminServer server in _servers)
        {
            await server.DisposeAsync();
        }

        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Registers_with_the_workshop_key_then_sends_catalog_snapshot_and_events_in_order_by_batches_of_one_hundred()
    {
        FakeAdminServer server = await StartServerAsync();
        Context ctx = CreateClient(server.Url, username: "john-dev");
        ctx.Client.Report("lab.opened", "asking", null);
        for (int i = 0; i < 149; i++)
        {
            ctx.Client.Report("custom.ping", null, new { n = i });
        }

        await ctx.Client.TickAsync(CancellationToken.None);

        AdminRequest register = Assert.Single(server.RequestsTo("/api/v1/devs/register"));
        Assert.Equal(FakeAdminServer.WorkshopKey, register.WorkshopKey);
        Assert.Equal(FakeAdminServer.ApiKey, register.ApiKey);
        Assert.Equal(ctx.Identity.Current!.UserId, register.Body!["userId"]!.GetValue<string>());
        Assert.Equal("john-dev", register.Body["username"]!.GetValue<string>());
        Assert.Equal(ReportingClient.Platform, register.Body["platform"]!.GetValue<string>());
        Assert.NotNull(ctx.Identity.Current.DevToken);

        // The heartbeat travels alone, outside the outbox.
        AdminRequest[] batches = server.RequestsTo("/api/v1/events").Where(b => b.Body!["events"]![0]!["type"]!.ToString() != "heartbeat").ToArray();
        Assert.Equal([100, 52], batches.Select(b => ((JsonArray)b.Body!["events"]!).Count));
        Assert.All(batches, b => Assert.Equal(ctx.Identity.Current.DevToken, b.Bearer));
        Assert.All(batches, b => Assert.Equal("john-dev", b.Body!["username"]!.GetValue<string>()));

        string[] types = server.EventTypes.Where(t => t != "heartbeat").ToArray();
        Assert.Equal(["catalog.synced", "progress.snapshot", "lab.opened"], types.Take(3));
        Assert.Equal(149, types.Count(t => t == "custom.ping"));
        Assert.Equal(Enumerable.Range(0, 149), server.Events.Where(e => e["type"]!.ToString() == "custom.ping").Select(e => e["payload"]!["n"]!.GetValue<int>()));
        Assert.Equal("asking", server.Events[2]["labId"]!.GetValue<string>());
        Assert.Equal("asking", server.Events[0]["payload"]!["labs"]![0]!["id"]!.GetValue<string>());
        Assert.Equal("notStarted", server.Events[1]["payload"]!["labs"]![0]!["state"]!.GetValue<string>());

        Assert.Equal(0, ctx.Outbox.Count);
        Assert.Equal("connected", ctx.Client.GetStatus().State);
        Assert.DoesNotContain(FakeAdminServer.WorkshopKey, File.ReadAllText(ctx.Identity.File));
    }

    [Fact]
    public async Task A_known_identity_registers_again_with_its_token_without_getting_a_new_one()
    {
        FakeAdminServer server = await StartServerAsync();
        Context ctx = CreateClient(server.Url);
        await ctx.Client.TickAsync(CancellationToken.None);
        string token = ctx.Identity.Current!.DevToken!;

        Context restarted = CreateClient(server.Url, identityFile: ctx.Identity.File);
        await restarted.Client.TickAsync(CancellationToken.None);

        AdminRequest second = server.RequestsTo("/api/v1/devs/register").Last();
        Assert.Equal(token, second.Bearer);
        Assert.Equal(token, restarted.Identity.Current!.DevToken);
        Assert.Equal("connected", restarted.Client.GetStatus().State);
        // The catalog and a fresh snapshot are sent again at every start.
        Assert.Equal(2, server.EventTypes.Count(t => t == "progress.snapshot"));
    }

    [Fact]
    public async Task A_wrong_workshop_key_is_reported_as_rejected_and_the_events_are_kept()
    {
        FakeAdminServer server = await StartServerAsync();
        Context ctx = CreateClient(server.Url, workshopKey: "wrong-key");
        ctx.Client.Report("lab.opened", "asking", null);

        await ctx.Client.TickAsync(CancellationToken.None);

        ReportingStatus status = ctx.Client.GetStatus();
        Assert.Equal("rejected", status.State);
        Assert.Equal("workshopKey", status.LastError);
        Assert.Equal(3, ctx.Outbox.Count);
        Assert.Empty(server.RequestsTo("/api/v1/events"));
        Assert.Null(ctx.Identity.Current!.DevToken);
    }

    [Fact]
    public async Task Honours_retry_after_when_the_server_rate_limits()
    {
        FakeAdminServer server = await StartServerAsync();
        server.Expect("/api/v1/events", new CannedResponse(429, RetryAfterSeconds: 1));
        Context ctx = CreateClient(server.Url);

        await ctx.Client.TickAsync(CancellationToken.None);
        Assert.Equal("offline", ctx.Client.GetStatus().State);
        Assert.Equal("rateLimited", ctx.Client.GetStatus().LastError);
        Assert.Equal(2, ctx.Outbox.Count);

        await ctx.Client.TickAsync(CancellationToken.None);
        Assert.Single(server.RequestsTo("/api/v1/events")); // too early: no request

        await Task.Delay(1100);
        await ctx.Client.TickAsync(CancellationToken.None);
        Assert.Equal(0, ctx.Outbox.Count);
        Assert.Equal("connected", ctx.Client.GetStatus().State);
    }

    [Fact]
    public async Task Re_registers_once_when_the_token_is_rejected_and_continues()
    {
        FakeAdminServer server = await StartServerAsync();
        Context ctx = CreateClient(server.Url);
        await ctx.Client.TickAsync(CancellationToken.None);
        string first = ctx.Identity.Current!.DevToken!;

        // The dev was deleted on the server: its token is unknown there.
        server.Tokens.Clear();
        ctx.Client.Report("lab.opened", "asking", null);
        await ctx.Client.TickAsync(CancellationToken.None);

        Assert.Equal(2, server.RequestsTo("/api/v1/devs/register").Count());
        Assert.Equal(FakeAdminServer.WorkshopKey, server.RequestsTo("/api/v1/devs/register").Last().WorkshopKey);
        Assert.NotEqual(first, ctx.Identity.Current.DevToken);
        Assert.Contains("lab.opened", server.EventTypes);
        Assert.Equal(0, ctx.Outbox.Count);
        Assert.Equal("connected", ctx.Client.GetStatus().State);
    }

    [Fact]
    public async Task Keeps_the_events_while_the_server_is_down_and_delivers_them_in_order_when_it_is_back()
    {
        int port = FakeAdminServer.FreePort();
        Context ctx = CreateClient($"http://127.0.0.1:{port}", retryBaseSeconds: 0.2);
        ctx.Client.Report("run.started", "asking", new { runId = "r1", target = "start" });

        await ctx.Client.TickAsync(CancellationToken.None);
        Assert.Equal("offline", ctx.Client.GetStatus().State);
        Assert.Equal("unreachable", ctx.Client.GetStatus().LastError);
        Assert.Equal(3, ctx.Outbox.Count);
        Assert.True(File.Exists(ctx.Outbox.File));
        Assert.Contains("run.started", File.ReadAllText(ctx.Outbox.File));

        ctx.Client.Report("run.finished", "asking", new { runId = "r1", target = "start", status = "passed" });
        FakeAdminServer server = await StartServerAsync(port);
        await WaitUntilAsync(async () =>
        {
            await ctx.Client.TickAsync(CancellationToken.None);
            return ctx.Outbox.Count == 0;
        });

        Assert.Equal(["catalog.synced", "progress.snapshot", "run.started", "run.finished"], server.EventTypes.Where(t => t != "heartbeat"));
        Assert.Equal("connected", ctx.Client.GetStatus().State);
    }

    [Fact]
    public async Task Duplicate_delivery_after_a_lost_answer_is_ignored_by_the_server_and_never_reapplied()
    {
        FakeAdminServer server = await StartServerAsync();
        server.Expect("/api/v1/events", new CannedResponse(503));
        Context ctx = CreateClient(server.Url, retryBaseSeconds: 0.05);
        await ctx.Client.TickAsync(CancellationToken.None);
        Assert.Equal("http503", ctx.Client.GetStatus().LastError);

        await Task.Delay(200);
        await ctx.Client.TickAsync(CancellationToken.None);

        Assert.Equal(0, ctx.Outbox.Count);
        Assert.Equal(["catalog.synced", "progress.snapshot"], server.EventTypes.Where(t => t != "heartbeat"));
    }

    [Fact]
    public async Task The_heartbeat_is_sent_directly_and_says_whether_the_browser_is_connected()
    {
        FakeAdminServer server = await StartServerAsync();
        Context ctx = CreateClient(server.Url, heartbeatSeconds: 0);
        ctx.Client.NoteBrowserActivity();

        await ctx.Client.TickAsync(CancellationToken.None);

        JsonNode heartbeat = Assert.Single(server.Events, e => e["type"]!.ToString() == "heartbeat");
        Assert.True(heartbeat["payload"]!["browserConnected"]!.GetValue<bool>());
        Assert.Null(heartbeat["payload"]!["activeRunId"]);
        Assert.Equal(0, ctx.Outbox.Count);
    }

    [Fact]
    public async Task Help_request_is_sent_acknowledged_resolved_with_the_note_and_can_be_cancelled()
    {
        FakeAdminServer server = await StartServerAsync();
        Context ctx = CreateClient(server.Url, helpPollSeconds: 0);
        await ctx.Client.TickAsync(CancellationToken.None);

        HelpState help = await ctx.Client.RequestHelpAsync("asking", "stuck on scenario 2");
        Assert.Equal("open", help.Status);
        Assert.NotNull(help.Id);
        AdminRequest sent = Assert.Single(server.RequestsTo("/api/v1/help-requests"));
        Assert.Equal("asking", sent.Body!["labId"]!.GetValue<string>());
        Assert.Equal("stuck on scenario 2", sent.Body["message"]!.GetValue<string>());
        Assert.Equal(ctx.Identity.Current!.UserId, sent.Body["userId"]!.GetValue<string>());

        // A second click while the first is active changes nothing.
        Assert.Equal(help.Id, (await ctx.Client.RequestHelpAsync(null, null)).Id);

        server.ActiveHelp!["status"] = "acknowledged";
        server.ActiveHelp["acknowledgedAt"] = DateTimeOffset.UtcNow.ToString("O");
        await ctx.Client.TickAsync(CancellationToken.None);
        Assert.Equal("acknowledged", ctx.Client.Help!.Status);
        Assert.NotNull(ctx.Client.Help.AcknowledgedAt);

        server.ActiveHelp["status"] = "resolved";
        server.ActiveHelp["adminNote"] = "See the README, section 3";
        await ctx.Client.TickAsync(CancellationToken.None);
        Assert.Equal("resolved", ctx.Client.Help!.Status);
        Assert.Equal("See the README, section 3", ctx.Client.Help.AdminNote);
        Assert.False(ctx.Client.Help.IsActive);

        ctx.Client.DismissHelp();
        Assert.Null(ctx.Client.Help);

        server.ActiveHelp = null;
        HelpState second = await ctx.Client.RequestHelpAsync(null, null);
        Assert.Equal("open", second.Status);
        HelpState? cancelled = await ctx.Client.CancelHelpAsync();
        Assert.Equal("cancelled", cancelled!.Status);
        Assert.Single(server.RequestsTo($"/api/v1/help-requests/{second.Id}/cancel"));
        Assert.Null(server.ActiveHelp);
    }

    [Fact]
    public async Task Help_request_adopts_the_one_the_server_already_has_and_waits_while_the_server_is_down()
    {
        int port = FakeAdminServer.FreePort();
        Context ctx = CreateClient($"http://127.0.0.1:{port}", retryBaseSeconds: 0.05);
        await ctx.Client.TickAsync(CancellationToken.None);

        HelpState pending = await ctx.Client.RequestHelpAsync("asking", null);
        Assert.Equal("pending", pending.Status);

        FakeAdminServer server = await StartServerAsync(port);
        server.ActiveHelp = new JsonObject { ["id"] = "help-existing", ["status"] = "open", ["labId"] = "asking", ["createdAt"] = DateTimeOffset.UtcNow.ToString("O") };
        await WaitUntilAsync(async () =>
        {
            await ctx.Client.TickAsync(CancellationToken.None);
            return ctx.Client.Help?.Status == "open";
        });

        Assert.Equal("help-existing", ctx.Client.Help!.Id);
        Assert.Single(server.RequestsTo("/api/v1/help-requests"));
    }

    [Fact]
    public async Task Changing_the_settings_updates_the_username_and_keeps_the_token()
    {
        FakeAdminServer server = await StartServerAsync();
        Context ctx = CreateClient(server.Url);
        await ctx.Client.TickAsync(CancellationToken.None);
        string userId = ctx.Identity.Current!.UserId;

        Assert.Equal(new Dictionary<string, string> { ["username"] = "invalid" }, await ctx.Client.ApplySettingsAsync(new("john doe", null)));
        Assert.Empty(await ctx.Client.ApplySettingsAsync(new("jane", null)));
        Assert.Equal("jane", ctx.Identity.Current.Username);
        string token = ctx.Identity.Current.DevToken!;
        await ctx.Client.TickAsync(CancellationToken.None);

        // The rename is a re-registration with the same id and token: the server updates the username, no new token.
        AdminRequest renamed = server.RequestsTo("/api/v1/devs/register").Last();
        Assert.Equal(userId, renamed.Body!["userId"]!.GetValue<string>());
        Assert.Equal("jane", renamed.Body["username"]!.GetValue<string>());
        Assert.Equal(token, renamed.Bearer);
        Assert.Equal("connected", ctx.Client.GetStatus().State);
    }

    [Fact]
    public void Without_a_server_url_nothing_is_enqueued_and_the_status_says_disabled()
    {
        Context ctx = CreateClient(serverUrl: null);
        ctx.Client.Report("lab.opened", "asking", null);
        ctx.Client.ReportLabOpened("asking");

        Assert.False(ctx.Client.Enabled);
        Assert.Equal(0, ctx.Outbox.Count);
        Assert.Equal("disabled", ctx.Client.GetStatus().State);
        Assert.False(File.Exists(ctx.Identity.File));
    }

    [Fact]
    public async Task First_launch_asks_for_onboarding_and_skipping_disables_reporting_until_the_developer_joins()
    {
        FakeAdminServer server = await StartServerAsync();
        Context ctx = CreateClient(server.Url, createIdentity: false);

        // A preconfigured server is not enough: without an identity nothing is enabled and the welcome dialog is due.
        ReportingStatus first = ctx.Client.GetStatus();
        Assert.True(first.NeedsIdentity);
        Assert.False(first.Enabled);
        Assert.False(first.Standalone);
        Assert.Equal(server.Url, first.ServerUrl);

        ctx.Client.SkipReporting();
        ctx.Client.Report("lab.opened", "asking", null);
        await ctx.Client.TickAsync(CancellationToken.None);

        ReportingStatus skipped = ctx.Client.GetStatus();
        Assert.True(skipped.Standalone);
        Assert.False(skipped.NeedsIdentity);
        Assert.False(skipped.Enabled);
        Assert.Equal("disabled", skipped.State);
        Assert.Equal(0, ctx.Outbox.Count);
        Assert.Empty(server.RequestsTo("/api/v1/devs/register"));
        Assert.True(File.Exists(ctx.Identity.StandaloneFile));

        // Joining later from the settings (URL and key already configured): the identity is created and the choice is forgotten.
        IReadOnlyDictionary<string, string> errors = await ctx.Client.ApplySettingsAsync(new ReportingSettingsUpdate("jane", null));
        await ctx.Client.TickAsync(CancellationToken.None);

        Assert.Empty(errors);
        ReportingStatus joined = ctx.Client.GetStatus();
        Assert.True(joined.Enabled);
        Assert.False(joined.Standalone);
        Assert.Equal("connected", joined.State);
        Assert.Equal("jane", server.RequestsTo("/api/v1/devs/register").Single().Body!["username"]!.GetValue<string>());
        Assert.False(File.Exists(ctx.Identity.StandaloneFile));
    }

    [Fact]
    public async Task Joining_needs_the_configured_server_url_and_a_workshop_key_entered_or_configured()
    {
        // No server in the project configuration: joining is impossible, whatever the developer enters.
        Context unconfigured = CreateClient(serverUrl: null, workshopKey: "", createIdentity: false);
        IReadOnlyDictionary<string, string> missing = await unconfigured.Client.ApplySettingsAsync(new ReportingSettingsUpdate("jane", null));
        Assert.Equal("notConfigured", missing["serverUrl"]);
        Assert.Equal("required", missing["workshopKey"]);
        Assert.Null(unconfigured.Identity.Current);

        // Server configured, key typed in the welcome dialog: the identity is created and registered with that key.
        FakeAdminServer server = await StartServerAsync();
        Context ctx = CreateClient(server.Url, workshopKey: "", createIdentity: false);
        Assert.Equal("required", (await ctx.Client.ApplySettingsAsync(new ReportingSettingsUpdate("jane", null)))["workshopKey"]);

        IReadOnlyDictionary<string, string> joined = await ctx.Client.ApplySettingsAsync(new ReportingSettingsUpdate("jane", FakeAdminServer.WorkshopKey));
        Assert.Empty(joined);
        Assert.True(ctx.Client.Enabled);

        await ctx.Client.TickAsync(CancellationToken.None);
        Assert.Equal("connected", ctx.Client.GetStatus().State);
        Assert.Equal(FakeAdminServer.WorkshopKey, server.RequestsTo("/api/v1/devs/register").Single().WorkshopKey);
    }

    [Fact]
    public async Task A_hanging_server_never_delays_or_changes_the_verdict_of_a_run()
    {
        FakeAdminServer server = await StartServerAsync();
        (LabCatalog catalog, RunHistoryStore history, AzureOpenAISettingsStore settings, DashboardOptions options) = CreateLab(server.Url);

        // Baseline: the same project, no reporting at all.
        LabRunner plain = new(catalog, history, settings, options, NullLogger<LabRunner>.Instance);
        RunRecord baseline = await RunAsync(plain, catalog.Labs[0]);
        Assert.Equal(RunStatus.Passed, baseline.Status);

        // Reporting to a server that accepts connections and never answers; the loop runs for real.
        server.Hang = true;
        options.Reporting.ApiKey = FakeAdminServer.ApiKey;
        IdentityStore identity = new(Path.Combine(_root, "hang", "identity.json"));
        identity.Create("john-dev");
        ReportingOutbox outbox = new(Path.Combine(_root, "hang", "outbox.json"));
        ReportingClient reporting = new(options, identity, outbox, catalog, history, settings, NullLogger<ReportingClient>.Instance, environment: _ => null);
        await reporting.StartAsync(CancellationToken.None);
        try
        {
            LabRunner reported = new(catalog, history, settings, options, NullLogger<LabRunner>.Instance, reporting);
            Stopwatch clock = Stopwatch.StartNew();
            RunRecord record = await RunAsync(reported, catalog.Labs[0]);
            clock.Stop();

            Assert.Equal(RunStatus.Passed, record.Status);
            Assert.Equal(baseline.Summary, record.Summary);
            Assert.True(record.DurationMs < baseline.DurationMs + 5000, $"reported run took {record.DurationMs} ms, baseline {baseline.DurationMs} ms");
            Assert.True(clock.ElapsedMilliseconds < baseline.DurationMs + 5000);

            // The events were enqueued (nothing was lost) and nothing was delivered (the server hangs).
            Assert.Contains(outbox.PeekBatch(), e => e.Type == "run.started" && e.Payload!["runId"]!.GetValue<string>() == record.RunId);
            Assert.Contains(outbox.PeekBatch(), e => e.Type == "run.finished" && e.Payload!["status"]!.GetValue<string>() == "passed");
            Assert.Equal("progress.snapshot", outbox.PeekBatch().Last().Type);
            Assert.Empty(server.Events);
            Assert.NotEqual("connected", reporting.GetStatus().State);
        }
        finally
        {
            await reporting.StopAsync(CancellationToken.None);
            reporting.Dispose();
        }
    }

    // ---------- API key ----------

    [Fact]
    public async Task Every_admin_api_request_carries_the_api_key_as_a_header_and_registration_also_the_workshop_key()
    {
        FakeAdminServer server = await StartServerAsync();
        Context ctx = CreateClient(server.Url, heartbeatSeconds: 0, helpPollSeconds: 0);
        await ctx.Client.TickAsync(CancellationToken.None);
        HelpState help = await ctx.Client.RequestHelpAsync("asking", null);
        await ctx.Client.TickAsync(CancellationToken.None);
        await ctx.Client.CancelHelpAsync();

        Assert.Equal(
            ["/api/v1/devs/me", "/api/v1/devs/register", "/api/v1/events", "/api/v1/help-requests", $"/api/v1/help-requests/{help.Id}/cancel"],
            server.Requests.Select(r => r.Path).Distinct().Order());
        Assert.All(server.Requests, r => Assert.Equal(FakeAdminServer.ApiKey, r.ApiKey));
        // Header only: never in the URL.
        Assert.All(server.Requests, r => Assert.True(string.IsNullOrEmpty(r.Query), r.Query));
        Assert.Equal(FakeAdminServer.WorkshopKey, server.RequestsTo("/api/v1/devs/register").Single().WorkshopKey);
        Assert.Equal("connected", ctx.Client.GetStatus().State);
    }

    [Fact]
    public async Task The_api_key_is_added_to_every_v1_route_but_health_which_works_without_it()
    {
        FakeAdminServer server = await StartServerAsync();
        Context ctx = CreateClient(server.Url);

        foreach (string path in new[] { "/api/v1/devs/register", "/api/v1/events", "/api/v1/help-requests", "/api/v1/help-requests/h1/cancel", "/api/v1/devs/me", "/api/v1/messages" })
        {
            using HttpRequestMessage request = ctx.Client.CreateRequest(HttpMethod.Post, path, null, null);
            Assert.Equal([FakeAdminServer.ApiKey], request.Headers.GetValues(ReportingClient.ApiKeyHeader));
            Assert.DoesNotContain(FakeAdminServer.ApiKey, request.RequestUri!.ToString());
        }

        using HttpRequestMessage health = ctx.Client.CreateRequest(HttpMethod.Get, "/api/v1/health", null, null);
        Assert.False(health.Headers.Contains(ReportingClient.ApiKeyHeader));

        using HttpClient http = new();
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync($"{server.Url}/api/v1/health")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync($"{server.Url}/api/v1/devs/me")).StatusCode);
    }

    [Fact]
    public async Task The_api_key_comes_from_the_project_configuration_and_the_environment_variable_wins()
    {
        FakeAdminServer server = await StartServerAsync();
        Context configured = CreateClient(server.Url, apiKey: null, configuredApiKey: FakeAdminServer.ApiKey);
        await configured.Client.TickAsync(CancellationToken.None);
        Assert.Equal("connected", configured.Client.GetStatus().State);

        Context overridden = CreateClient(server.Url, apiKey: "lbk_from_environment", configuredApiKey: FakeAdminServer.ApiKey);
        using HttpRequestMessage request = overridden.Client.CreateRequest(HttpMethod.Get, "/api/v1/devs/me", null, null);
        Assert.Equal(["lbk_from_environment"], request.Headers.GetValues(ReportingClient.ApiKeyHeader));
    }

    [Fact]
    public async Task Without_an_api_key_nothing_is_sent_the_status_says_so_and_one_warning_is_logged()
    {
        FakeAdminServer server = await StartServerAsync();
        ListLogger logger = new();
        Context ctx = CreateClient(server.Url, apiKey: null, logger: logger);
        ctx.Client.Report("lab.opened", "asking", null);

        for (int i = 0; i < 3; i++)
        {
            await ctx.Client.TickAsync(CancellationToken.None);
        }

        Assert.Empty(server.Requests);
        ReportingStatus status = ctx.Client.GetStatus();
        Assert.Equal("rejected", status.State);
        Assert.Equal("apiKeyMissing", status.LastError);
        Assert.Equal(3, ctx.Outbox.Count);
        Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("API key is not configured"));
    }

    [Fact]
    public async Task A_refused_api_key_stops_the_reporting_instead_of_retrying()
    {
        FakeAdminServer server = await StartServerAsync();
        Context ctx = CreateClient(server.Url, apiKey: "lbk_wrong_key", retryBaseSeconds: 0.01);
        ctx.Client.Report("lab.opened", "asking", null);

        for (int i = 0; i < 5; i++)
        {
            await ctx.Client.TickAsync(CancellationToken.None);
            await Task.Delay(30);
        }

        AdminRequest only = Assert.Single(server.Requests);
        Assert.Equal("/api/v1/devs/register", only.Path);
        ReportingStatus status = ctx.Client.GetStatus();
        Assert.Equal("rejected", status.State);
        Assert.Equal("apiKey", status.LastError);
        Assert.Equal(3, ctx.Outbox.Count);

        // Joining again with another workshop key does not restart the requests: the API key needs a restart.
        Assert.Empty(await ctx.Client.ApplySettingsAsync(new ReportingSettingsUpdate(null, "another-key")));
        await ctx.Client.TickAsync(CancellationToken.None);
        Assert.Single(server.Requests);
        Assert.Equal("apiKey", ctx.Client.GetStatus().LastError);
    }

    [Fact]
    public async Task An_api_key_refused_after_registration_stops_without_re_registering_nor_heartbeat()
    {
        FakeAdminServer server = await StartServerAsync();
        server.Expect("/api/v1/events", new CannedResponse(401, Body: """{ "error": "invalidApiKey", "message": "API key missing or invalid." }"""));
        Context ctx = CreateClient(server.Url, heartbeatSeconds: 0, retryBaseSeconds: 0.01);

        await ctx.Client.TickAsync(CancellationToken.None);
        await Task.Delay(30);
        await ctx.Client.TickAsync(CancellationToken.None);
        HelpState help = await ctx.Client.RequestHelpAsync("asking", null);

        Assert.Equal(["/api/v1/devs/register", "/api/v1/events"], server.Requests.Select(r => r.Path));
        Assert.Equal("apiKey", ctx.Client.GetStatus().LastError);
        Assert.Equal("pending", help.Status);
        Assert.Equal(2, ctx.Outbox.Count);
    }

    [Fact]
    public async Task A_refused_workshop_key_code_stops_until_a_new_key_is_entered()
    {
        FakeAdminServer server = await StartServerAsync();
        server.Expect("/api/v1/devs/register", new CannedResponse(401, Body: """{ "error": "invalidWorkshopKey", "message": "Unknown workshop." }"""));
        Context ctx = CreateClient(server.Url, retryBaseSeconds: 0.01);

        await ctx.Client.TickAsync(CancellationToken.None);
        await Task.Delay(30);
        await ctx.Client.TickAsync(CancellationToken.None);
        Assert.Single(server.Requests);
        Assert.Equal("workshopKey", ctx.Client.GetStatus().LastError);

        Assert.Empty(await ctx.Client.ApplySettingsAsync(new ReportingSettingsUpdate(null, FakeAdminServer.WorkshopKey)));
        await ctx.Client.TickAsync(CancellationToken.None);
        Assert.Equal("connected", ctx.Client.GetStatus().State);
    }

    [Fact]
    public async Task The_api_key_value_never_appears_in_the_logs_the_status_or_the_local_files()
    {
        FakeAdminServer server = await StartServerAsync();
        server.Expect("/api/v1/events", new CannedResponse(503), new CannedResponse(429, RetryAfterSeconds: 0));
        ListLogger logger = new();
        Context ok = CreateClient(server.Url, heartbeatSeconds: 0, retryBaseSeconds: 0.01, logger: logger);
        for (int i = 0; i < 3; i++)
        {
            await ok.Client.TickAsync(CancellationToken.None);
            await Task.Delay(30);
        }

        Context refused = CreateClient(server.Url, apiKey: "lbk_wrong_key", logger: logger);
        await refused.Client.TickAsync(CancellationToken.None);
        Context missing = CreateClient(server.Url, apiKey: null, logger: logger);
        await missing.Client.TickAsync(CancellationToken.None);

        Assert.NotEmpty(logger.Entries);
        foreach (string secret in new[] { FakeAdminServer.ApiKey, "lbk_wrong_key" })
        {
            Assert.All(logger.Entries, e => Assert.DoesNotContain(secret, e.Message + e.Exception));
            Assert.All(new[] { ok, refused, missing }, c =>
            {
                Assert.DoesNotContain(secret, System.Text.Json.JsonSerializer.Serialize(c.Client.GetStatus()));
                Assert.DoesNotContain(secret, File.ReadAllText(c.Identity.File));
                Assert.True(!File.Exists(c.Outbox.File) || !File.ReadAllText(c.Outbox.File).Contains(secret));
            });
        }
    }

    // ---------- helpers ----------

    private sealed record Context(ReportingClient Client, IdentityStore Identity, ReportingOutbox Outbox);

    private async Task<FakeAdminServer> StartServerAsync(int? port = null)
    {
        FakeAdminServer server = await FakeAdminServer.StartAsync(port);
        _servers.Add(server);
        return server;
    }

    private Context CreateClient(
        string? serverUrl,
        string username = "john-dev",
        string workshopKey = FakeAdminServer.WorkshopKey,
        string? identityFile = null,
        double retryBaseSeconds = 0.2,
        double heartbeatSeconds = 3600,
        double helpPollSeconds = 3600,
        bool createIdentity = true,
        string? apiKey = FakeAdminServer.ApiKey,
        string? configuredApiKey = null,
        ILogger<ReportingClient>? logger = null)
    {
        (LabCatalog catalog, RunHistoryStore history, AzureOpenAISettingsStore settings, DashboardOptions options) = CreateLab(serverUrl);
        options.Reporting.RetryBaseSeconds = retryBaseSeconds;
        options.Reporting.RetryMaxSeconds = Math.Max(retryBaseSeconds, 0.5);
        options.Reporting.HeartbeatSeconds = heartbeatSeconds;
        options.Reporting.HelpPollSeconds = helpPollSeconds;
        options.Reporting.RequestTimeoutSeconds = 5;
        options.Reporting.ApiKey = configuredApiKey;

        string folder = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        IdentityStore identity = new(identityFile ?? Path.Combine(folder, "identity.json"));
        if (createIdentity && identity.Current is null && serverUrl is not null)
        {
            identity.Create(username);
        }

        ReportingOutbox outbox = new(Path.Combine(folder, "outbox.json"));
        ReportingClient client = new(options, identity, outbox, catalog, history, settings, logger ?? NullLogger<ReportingClient>.Instance,
            environment: name => name switch
            {
                ReportingClient.WorkshopKeyVariable => workshopKey,
                ReportingClient.ApiKeyVariable => apiKey,
                _ => null,
            });
        return new Context(client, identity, outbox);
    }

    private (LabCatalog, RunHistoryStore, AzureOpenAISettingsStore, DashboardOptions) CreateLab(string? serverUrl)
    {
        string project = Path.Combine(_root, "AskingLab", "Solution");
        if (!File.Exists(Path.Combine(project, "AskingLab.csproj")))
        {
            Directory.CreateDirectory(project);
            File.WriteAllText(Path.Combine(project, "AskingLab.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(project, "Program.cs"), """Console.WriteLine("=== Scenario 1: hello ===");Console.WriteLine("done");""");
            File.WriteAllText(Path.Combine(_root, "labs.json"), """
                { "labs": [ { "id": "asking", "number": "98", "track": "t", "title": "Asking", "summary": "s", "level": "l",
                              "path": "AskingLab", "startProject": "Solution/AskingLab.csproj", "solutionProject": "Solution/AskingLab.csproj",
                              "timeoutSeconds": 170, "expectations": [ { "id": "s1", "description": "answered", "pattern": "^=== Scenario 1:[^\\n]*===\\n\\S" } ] } ] }
                """);
        }

        LabCatalog catalog = LabCatalog.Load(Path.Combine(_root, "labs.json"), _root);
        AzureOpenAISettingsStore settings = new(Path.Combine(_root, "secrets", "secrets.json"), "test", [], () => new Dictionary<string, string>());
        DashboardOptions options = new() { DataDirectory = "data", Reporting = { ServerUrl = serverUrl, FlushIntervalSeconds = 0.1 } };
        HostingEnvironment environment = new() { ContentRootPath = _root };
        RunHistoryStore history = new(options, environment, NullLogger<RunHistoryStore>.Instance);
        return (catalog, history, settings, options);
    }

    private static async Task<RunRecord> RunAsync(LabRunner runner, LabDefinition lab)
    {
        Assert.True(runner.TryStart(lab, RunTarget.Start, out LabRun run));
        using CancellationTokenSource timeout = new(TimeSpan.FromMinutes(3));
        await foreach (RunEvent _ in run.ReadEventsAsync(timeout.Token))
        {
        }

        return Assert.IsType<RunRecord>(run.Result);
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, int timeoutMs = 10000)
    {
        Stopwatch clock = Stopwatch.StartNew();
        while (!await condition())
        {
            Assert.True(clock.ElapsedMilliseconds < timeoutMs, "Condition not met in time.");
            await Task.Delay(50);
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

    /// <summary>Keeps every log entry, formatted, to check what reaches the console.</summary>
    private sealed class ListLogger : ILogger<ReportingClient>
    {
        public ConcurrentQueue<LogEntry> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Enqueue(new LogEntry(logLevel, formatter(state, exception), exception));
    }
}
