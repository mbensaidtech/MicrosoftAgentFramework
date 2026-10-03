using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using LabDashboard.Catalog;
using LabDashboard.Execution;
using LabDashboard.History;
using LabDashboard.Settings;

namespace LabDashboard.Reporting;

/// <summary>What the UI shows about the reporting (never the workshop key nor the dev token).</summary>
public sealed record ReportingStatus(
    bool Enabled,
    string State,
    bool NeedsIdentity,
    bool Standalone,
    string? ServerUrl,
    string? ServerHost,
    bool WorkshopKeyConfigured,
    string WorkshopKeySource,
    string? UserId,
    string? Username,
    string? LastError,
    int OutboxCount,
    HelpState? Help,
    string DashboardVersion,
    string IdentityFile);

/// <summary>The help request of this developer as the local dashboard knows it. <c>pending</c>: not yet accepted by the server.</summary>
public sealed record HelpState(
    string? Id,
    string Status,
    string? LabId,
    string? Message,
    string? AdminNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AcknowledgedAt,
    DateTimeOffset? ClosedAt,
    string? Error)
{
    public bool IsActive => Status is "pending" or "open" or "acknowledged";
}

/// <summary>Values entered in the UI (welcome dialog or settings). A null field keeps the current value; the key is write-only.</summary>
public sealed record ReportingSettingsUpdate(string? Username, string? WorkshopKey);

/// <summary>
/// Reports what the developer does to the trainer's admin dashboard: identity, lab catalog, progress snapshots, runs,
/// heartbeats and help requests. Everything goes through an outbox flushed by a background loop: the hooks called by the
/// runner only enqueue, so a slow or absent server never delays or fails a run. Disabled unless a server URL is configured.
/// </summary>
public sealed class ReportingClient : BackgroundService
{
    public const string WorkshopKeySection = "Dashboard:Reporting";
    public const string WorkshopKeyName = "WorkshopKey";
    public const string WorkshopKeyVariable = "Dashboard__Reporting__WorkshopKey";
    public const string ApiKeyVariable = "LABS_ADMIN_API_KEY";
    public const string ApiKeyHeader = "X-API-Key";

    private static readonly JsonSerializerOptions JsonOptions = ProgressSnapshot.JsonOptions;

    private readonly ReportingOptions _options;
    private readonly IdentityStore _identity;
    private readonly ReportingOutbox _outbox;
    private readonly LabCatalog _catalog;
    private readonly RunHistoryStore _history;
    private readonly AzureOpenAISettingsStore _secrets;
    private readonly ILogger<ReportingClient> _logger;
    private readonly Func<string, string?> _environment;
    private readonly HttpClient _http;
    private readonly string? _apiKey;
    private readonly SemaphoreSlim _wake = new(0, 1);
    private readonly SemaphoreSlim _network = new(1, 1);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _labOpened = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _logged = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    private string _state = "disabled";
    private string? _lastError;
    // Set when retrying cannot help (API key missing or refused, workshop key refused): no request at all until the cause changes,
    // so a wrong value never trips the server's lockout (30 failures per minute block the machine).
    private string? _halted;
    private bool _registered;
    private bool _reregistered;
    private bool _bootstrapped;
    private int _retries;
    private DateTimeOffset _retryAt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastHeartbeat = DateTimeOffset.MinValue;
    private DateTimeOffset _lastHelpPoll = DateTimeOffset.MinValue;
    private DateTimeOffset _lastBrowserRequest = DateTimeOffset.MinValue;
    private string? _activeRunId;
    private HelpState? _help;

    public ReportingClient(
        DashboardOptions options,
        IdentityStore identity,
        ReportingOutbox outbox,
        LabCatalog catalog,
        RunHistoryStore history,
        AzureOpenAISettingsStore secrets,
        ILogger<ReportingClient> logger,
        HttpMessageHandler? handler = null,
        Func<string, string?>? environment = null)
    {
        _options = options.Reporting;
        _identity = identity;
        _outbox = outbox;
        _catalog = catalog;
        _history = history;
        _secrets = secrets;
        _logger = logger;
        _environment = environment ?? Environment.GetEnvironmentVariable;
        _http = new HttpClient(handler ?? new SocketsHttpHandler(), disposeHandler: handler is null)
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(1, _options.RequestTimeoutSeconds)),
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"LabDashboard/{DashboardVersion}");
        _apiKey = Normalize(_environment(ApiKeyVariable)) ?? Normalize(_options.ApiKey);
        if (_apiKey is null)
        {
            _halted = "apiKeyMissing";
        }

        // The catalog and the snapshot come before anything the runner reports.
        Bootstrap();
    }

    public static string DashboardVersion { get; } =
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0]
        ?? "dev";

    public static string Platform { get; } = OperatingSystem.IsMacOS() ? "macOS" : OperatingSystem.IsWindows() ? "Windows" : "Linux";

    /// <summary>The admin dashboard URL, fixed in the project configuration (Dashboard:Reporting:ServerUrl); never editable in the UI.</summary>
    public string? ServerUrl => Normalize(_options.ServerUrl);

    /// <summary>Reporting runs only once the developer joined a workshop (identity created) and a server URL is known.</summary>
    public bool Enabled => ServerUrl is not null && _identity.Current is not null;

    public HelpState? Help
    {
        get { lock (_gate) { return _help; } }
    }

    private (string? Key, string Source) WorkshopKey()
    {
        if (!string.IsNullOrWhiteSpace(_environment(WorkshopKeyVariable)))
        {
            return (_environment(WorkshopKeyVariable)!.Trim(), "environment");
        }

        if (_secrets.GetEntry(WorkshopKeySection, WorkshopKeyName) is { } stored)
        {
            return (stored.Trim(), "dashboard");
        }

        return Normalize(_options.WorkshopKey) is { } configured ? (configured, "appsettings") : (null, "missing");
    }

    public ReportingStatus GetStatus()
    {
        DevIdentity? identity = _identity.Current;
        string? serverUrl = ServerUrl;
        (string? key, string keySource) = WorkshopKey();
        lock (_gate)
        {
            bool enabled = serverUrl is not null && identity is not null;
            return new ReportingStatus(
                Enabled: enabled,
                State: !enabled ? "disabled" : _halted is not null ? "rejected" : _state,
                // First launch: no identity and no "work on my own" choice yet → the welcome dialog is shown.
                NeedsIdentity: identity is null && !_identity.Standalone,
                Standalone: _identity.Standalone,
                ServerUrl: serverUrl,
                ServerHost: serverUrl is null ? null : new Uri(serverUrl).Host,
                WorkshopKeyConfigured: key is not null,
                WorkshopKeySource: keySource,
                UserId: identity?.UserId,
                Username: identity?.Username,
                LastError: _halted ?? _lastError,
                OutboxCount: _outbox.Count,
                Help: _help,
                DashboardVersion: DashboardVersion,
                IdentityFile: _identity.File);
        }
    }

    // ---------- hooks (enqueue only, never block) ----------

    /// <summary>Enqueues an event. Does nothing when reporting is disabled.</summary>
    public void Report(string type, string? labId, object? payload)
    {
        // Nothing to attribute before the developer chose a username; the snapshot sent at registration covers what happened.
        if (!Enabled || _identity.Current is null)
        {
            return;
        }

        JsonNode? node = payload is null ? new JsonObject() : payload as JsonNode ?? JsonSerializer.SerializeToNode(payload, JsonOptions);
        _outbox.Enqueue(new ReportingEvent(Guid.NewGuid().ToString("D"), type, DateTimeOffset.Now, labId, node));
        Wake();
    }

    /// <summary>A lab page was opened; reported at most once per lab per <see cref="ReportingOptions.LabOpenedDebounceSeconds"/>.</summary>
    public void ReportLabOpened(string labId)
    {
        if (!Enabled)
        {
            return;
        }

        DateTimeOffset now = DateTimeOffset.Now;
        DateTimeOffset last = _labOpened.GetOrAdd(labId, DateTimeOffset.MinValue);
        if (now - last < TimeSpan.FromSeconds(_options.LabOpenedDebounceSeconds))
        {
            return;
        }

        _labOpened[labId] = now;
        Report("lab.opened", labId, null);
    }

    public void RunStarted(LabRun run)
    {
        lock (_gate) { _activeRunId = run.Id; }
        Report("run.started", run.Lab.Id, new { runId = run.Id, target = run.Target });
    }

    /// <summary>Called once the record is in the history: the event is followed by a full snapshot (authoritative on the server).</summary>
    public void RunFinished(RunRecord record)
    {
        lock (_gate) { _activeRunId = null; }
        if (!Enabled)
        {
            return;
        }

        Report("run.finished", record.LabId, new
        {
            runId = record.RunId,
            target = record.Target,
            status = record.Status,
            failureStage = record.FailureStage,
            summary = record.Summary,
            durationMs = record.DurationMs,
            buildDurationMs = record.BuildDurationMs,
            runDurationMs = record.RunDurationMs,
            exitCode = record.ExitCode,
            checks = record.Checks.Select(c => new { id = c.Id, passed = c.Passed }),
            totalTokens = record.TokenUsage?.Total,
        });
        Report("progress.snapshot", null, ProgressSnapshot.Progress(_catalog, _history));
    }

    public void ReportSettingsChanged(AzureOpenAISettingsStatus status) =>
        Report("settings.changed", null, new { authMode = status.Configured ? status.Authentication : "notConfigured" });

    /// <summary>Any request of the browser: the heartbeat tells the server whether the page is open.</summary>
    public void NoteBrowserActivity() => _lastBrowserRequest = DateTimeOffset.Now;

    // ---------- identity and settings (UI) ----------

    /// <summary>Validates and applies the values of the welcome dialog or of the settings. Returns field → error code, empty when applied.</summary>
    public async Task<IReadOnlyDictionary<string, string>> ApplySettingsAsync(ReportingSettingsUpdate update)
    {
        Dictionary<string, string> errors = [];
        string? username = Normalize(update.Username);
        string? workshopKey = Normalize(update.WorkshopKey);
        DevIdentity? current = _identity.Current;

        if (username is not null && IdentityStore.ValidateUsername(username) is { } usernameError)
        {
            errors["username"] = usernameError;
        }
        else if (username is null && current is null)
        {
            errors["username"] = "required";
        }

        if (workshopKey is not null && workshopKey.Any(char.IsWhiteSpace))
        {
            errors["workshopKey"] = "invalid";
        }

        // Joining a workshop needs the configured server and a key it accepts (entered now, or already configured).
        if (current is null)
        {
            if (ServerUrl is null)
            {
                errors["serverUrl"] = "notConfigured";
            }

            if (!errors.ContainsKey("workshopKey") && workshopKey is null && WorkshopKey().Key is null)
            {
                errors["workshopKey"] = "required";
            }
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        if (workshopKey is not null)
        {
            await _secrets.SetEntryAsync(WorkshopKeySection, WorkshopKeyName, workshopKey);
        }

        if (current is null)
        {
            _identity.Create(username!);
        }
        else
        {
            _identity.Update(identity => identity with { Username = username ?? identity.Username });
        }

        lock (_gate)
        {
            _registered = false;
            _reregistered = false;
            _bootstrapped = false;
            _retries = 0;
            _retryAt = DateTimeOffset.MinValue;
            _lastError = null;
            if (_halted == "workshopKey" && workshopKey is not null)
            {
                // A new workshop key is worth one more try; the API key is project configuration and needs a restart.
                _halted = null;
            }

            _state = Enabled ? "registering" : "disabled";
        }

        // A new identity (or server) starts with the catalog and a snapshot, before anything else is reported.
        Bootstrap();
        Wake();
        return errors;
    }

    /// <summary>"Work on my own": no identity, no network call, the welcome dialog is not shown again. The settings can still join a workshop later.</summary>
    public void SkipReporting()
    {
        if (_identity.Current is not null)
        {
            return;
        }

        _identity.MarkStandalone();
        lock (_gate)
        {
            _state = "disabled";
            _lastError = null;
        }
    }

    // ---------- help ----------

    /// <summary>Raises a hand. Sends at once when the server answers; otherwise keeps the request pending and retries from the loop.</summary>
    public async Task<HelpState> RequestHelpAsync(string? labId, string? message)
    {
        HelpState request;
        lock (_gate)
        {
            if (_help is { IsActive: true } active)
            {
                return active;
            }

            request = new HelpState(null, "pending", labId, Normalize(message), null, DateTimeOffset.Now, null, null, null);
            _help = request;
        }

        await SendPendingHelpAsync(CancellationToken.None);
        return Help ?? request;
    }

    /// <summary>"I'm unblocked": cancels the active request. A pending (unsent) request is simply dropped.</summary>
    public async Task<HelpState?> CancelHelpAsync()
    {
        HelpState? help = Help;
        if (help is not { IsActive: true })
        {
            return help;
        }

        if (help.Id is null)
        {
            return SetHelp(help with { Status = "cancelled", ClosedAt = DateTimeOffset.Now, Error = null });
        }

        DevIdentity? identity = _identity.Current;
        if (identity?.DevToken is null)
        {
            return help;
        }

        if (Halted is { } halted)
        {
            return SetHelp(help with { Error = halted });
        }

        try
        {
            using HttpResponseMessage response = await SendAsync(
                HttpMethod.Post, $"/api/v1/help-requests/{Uri.EscapeDataString(help.Id)}/cancel",
                new { userId = identity.UserId, username = identity.Username }, identity.DevToken, CancellationToken.None);
            if (response.IsSuccessStatusCode || response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.NotFound)
            {
                // 409: already closed by the trainer; 404: unknown to the server. Either way it is over for this developer.
                return SetHelp(help with { Status = "cancelled", ClosedAt = DateTimeOffset.Now, Error = null });
            }

            return SetHelp(help with { Error = $"http{(int)response.StatusCode}" });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return SetHelp(help with { Error = "unreachable" });
        }
    }

    /// <summary>Hides a closed request from the UI.</summary>
    public void DismissHelp()
    {
        lock (_gate)
        {
            if (_help is { IsActive: false })
            {
                _help = null;
            }
        }
    }

    // ---------- background loop ----------

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        Bootstrap();
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan interval = TimeSpan.FromSeconds(Math.Max(0.05, _options.FlushIntervalSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _wake.WaitAsync(interval, stoppingToken);
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // The loop must survive anything: reporting is best effort.
                LogThrottled("tick", LogLevel.Warning, ex, "Reporting loop error: {Message}", ex.Message);
            }
        }

        _outbox.Persist();
    }

    public override void Dispose()
    {
        _http.Dispose();
        base.Dispose();
    }

    /// <summary>One pass: persist, register, deliver help and events, heartbeat, poll the help status.</summary>
    internal async Task TickAsync(CancellationToken cancellationToken)
    {
        _outbox.Persist();
        if (!Enabled)
        {
            lock (_gate) { _state = "disabled"; }
            return;
        }

        if (_identity.Current is null)
        {
            lock (_gate) { _state = "registering"; }
            return;
        }

        Bootstrap();
        if (Halted == "apiKeyMissing")
        {
            WarnOnce("apiKeyMissing", "Reporting is off: the admin API key is not configured (Dashboard:Reporting:ApiKey in appsettings.Local.json, or the {Variable} environment variable).",
                ApiKeyVariable);
        }

        if (Paused)
        {
            return;
        }

        await _network.WaitAsync(cancellationToken);
        try
        {
            if (!await EnsureRegisteredAsync(cancellationToken))
            {
                return;
            }

            await SendPendingHelpCoreAsync(cancellationToken);
            if (Paused)
            {
                return;
            }

            await FlushAsync(cancellationToken);
            if (Paused)
            {
                // A failed delivery scheduled a retry (or stopped the reporting): the heartbeat and the help poll wait for it too.
                return;
            }

            await HeartbeatAsync(cancellationToken);
            if (!Paused)
            {
                await PollHelpAsync(cancellationToken);
            }
        }
        finally
        {
            _network.Release();
            _outbox.Persist();
        }
    }

    /// <summary>The first events of a session: the catalog and a full snapshot (once per identity / server).</summary>
    private void Bootstrap()
    {
        lock (_gate)
        {
            if (_bootstrapped || !Enabled || _identity.Current is null)
            {
                return;
            }

            _bootstrapped = true;
        }

        Report("catalog.synced", null, ProgressSnapshot.Catalog(_catalog));
        Report("progress.snapshot", null, ProgressSnapshot.Progress(_catalog, _history));
    }

    private async Task<bool> EnsureRegisteredAsync(CancellationToken cancellationToken)
    {
        DevIdentity identity = _identity.Current!;
        lock (_gate)
        {
            if (_registered && identity.DevToken is not null)
            {
                return true;
            }

            _state = "registering";
        }

        (string? key, _) = WorkshopKey();
        if (key is null && identity.DevToken is null)
        {
            Fail("rejected", "workshopKeyMissing", null);
            return false;
        }

        try
        {
            using HttpRequestMessage request = CreateRequest(HttpMethod.Post, "/api/v1/devs/register",
                new { userId = identity.UserId, username = identity.Username, dashboardVersion = DashboardVersion, platform = Platform },
                identity.DevToken);
            if (key is not null)
            {
                request.Headers.TryAddWithoutValidation("X-Workshop-Key", key);
            }

            using HttpResponseMessage response = await SendAsync(request, cancellationToken);
            if (Halted is not null)
            {
                return false;
            }

            switch (response.StatusCode)
            {
                case HttpStatusCode.Created:
                case HttpStatusCode.OK:
                    JsonNode? body = await ReadJsonAsync(response, cancellationToken);
                    string? token = body?["devToken"]?.GetValue<string>();
                    if (token is not null)
                    {
                        _identity.Update(i => i with { DevToken = token, RegisteredAt = DateTimeOffset.Now });
                    }
                    else if (identity.DevToken is null)
                    {
                        Fail("offline", "noToken", null);
                        return false;
                    }

                    Succeed();
                    lock (_gate) { _registered = true; }
                    _logger.LogInformation("Registered on {Server} as {Username}.", ServerUrl, identity.Username);
                    return true;

                case HttpStatusCode.Unauthorized:
                    Fail("rejected", "workshopKey", null);
                    return false;

                case HttpStatusCode.Forbidden:
                    Fail("rejected", "identity", null);
                    return false;

                case HttpStatusCode.TooManyRequests:
                    // Already recorded by SendAsync, with the delay asked by the server.
                    return false;

                default:
                    Fail("offline", $"http{(int)response.StatusCode}", null);
                    return false;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Fail("offline", "unreachable", null, ex);
            return false;
        }
    }

    /// <summary>Delivers the outbox in batches, oldest first, until it is empty or a request fails.</summary>
    private async Task FlushAsync(CancellationToken cancellationToken)
    {
        while (_outbox.Count > 0)
        {
            IReadOnlyList<ReportingEvent> batch = _outbox.PeekBatch();
            DevIdentity identity = _identity.Current!;
            HttpStatusCode status;
            JsonNode? body;
            try
            {
                using HttpResponseMessage response = await SendAsync(HttpMethod.Post, "/api/v1/events",
                    new { userId = identity.UserId, username = identity.Username, events = batch }, identity.DevToken!, cancellationToken);
                status = response.StatusCode;
                body = await ReadJsonAsync(response, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                Fail("offline", "unreachable", null, ex);
                return;
            }

            if (Halted is not null)
            {
                return;
            }

            if (status is HttpStatusCode.Accepted or HttpStatusCode.OK)
            {
                _outbox.Remove(batch.Select(e => e.EventId));
                Succeed();
                continue;
            }

            if (status == HttpStatusCode.BadRequest)
            {
                // A client-side bug: drop the offending event (or the whole batch when the index is unknown) rather than retrying forever.
                int? index = body?["details"]?["index"]?.GetValue<int>();
                IEnumerable<string> dropped = index is >= 0 && index < batch.Count ? [batch[index.Value].EventId] : batch.Select(e => e.EventId);
                _outbox.Remove(dropped);
                LogThrottled("validation", LogLevel.Warning, null, "The admin dashboard rejected an event ({Reason}); it was dropped.",
                    body?["details"]?["reason"]?.ToString() ?? body?["message"]?.ToString() ?? "validation");
                continue;
            }

            if (status == HttpStatusCode.Unauthorized)
            {
                // Token revoked or dev deleted on the server: register again once with the workshop key.
                lock (_gate)
                {
                    _registered = false;
                    if (_reregistered)
                    {
                        Fail("offline", "tokenRejected", null);
                        return;
                    }

                    _reregistered = true;
                }

                _identity.Update(i => i with { DevToken = null, RegisteredAt = null });
                if (!await EnsureRegisteredAsync(cancellationToken))
                {
                    return;
                }

                continue;
            }

            if (status == HttpStatusCode.TooManyRequests)
            {
                // Already recorded by SendAsync, with the delay asked by the server.
                return;
            }

            if (status == HttpStatusCode.Forbidden)
            {
                Fail("rejected", "identity", null);
                return;
            }

            Fail("offline", $"http{(int)status}", null);
            return;
        }
    }

    private async Task HeartbeatAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        if (now - _lastHeartbeat < TimeSpan.FromSeconds(_options.HeartbeatSeconds))
        {
            return;
        }

        DevIdentity identity = _identity.Current!;
        bool browserConnected = now - _lastBrowserRequest < TimeSpan.FromSeconds(90);
        string? activeRunId;
        lock (_gate) { activeRunId = _activeRunId; }

        // Not queued: a late heartbeat is worthless, and an offline period must not accumulate them.
        ReportingEvent heartbeat = new(Guid.NewGuid().ToString("D"), "heartbeat", now, null,
            JsonSerializer.SerializeToNode(new { browserConnected, activeRunId }, JsonOptions));
        try
        {
            using HttpResponseMessage response = await SendAsync(HttpMethod.Post, "/api/v1/events",
                new { userId = identity.UserId, username = identity.Username, events = new[] { heartbeat } }, identity.DevToken!, cancellationToken);
            _lastHeartbeat = now;
            if (response.IsSuccessStatusCode)
            {
                Succeed();
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Fail("offline", "unreachable", null, ex);
        }
    }

    private Task SendPendingHelpAsync(CancellationToken cancellationToken) => RunOnNetworkAsync(SendPendingHelpCoreAsync, cancellationToken);

    private async Task SendPendingHelpCoreAsync(CancellationToken cancellationToken)
    {
        HelpState? help = Help;
        DevIdentity? identity = _identity.Current;
        if (help is not { Status: "pending" } || identity?.DevToken is null)
        {
            return;
        }

        bool registered;
        lock (_gate) { registered = _registered; }
        if (!registered || Halted is not null)
        {
            SetHelp(help with { Error = Halted ?? "notRegistered" });
            return;
        }

        try
        {
            using HttpResponseMessage response = await SendAsync(HttpMethod.Post, "/api/v1/help-requests",
                new { userId = identity.UserId, username = identity.Username, labId = help.LabId, message = help.Message }, identity.DevToken, cancellationToken);
            JsonNode? body = await ReadJsonAsync(response, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK)
            {
                SetHelp(FromServer(help, body) with { Error = null });
                Succeed();
            }
            else if (response.StatusCode == HttpStatusCode.Conflict && body?["details"]?["existing"] is JsonNode existing)
            {
                // The server already has an active request for this developer (e.g. sent before a restart): adopt it.
                SetHelp(FromServer(help, existing) with { Error = null });
            }
            else if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                // Rejected by validation: retrying the same values is pointless.
                SetHelp(help with { Status = "cancelled", ClosedAt = DateTimeOffset.Now, Error = "invalid" });
            }
            else
            {
                SetHelp(help with { Error = $"http{(int)response.StatusCode}" });
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            SetHelp(help with { Error = "unreachable" });
            Fail("offline", "unreachable", null, ex);
        }
    }

    /// <summary>Reads the status of the active request back (acknowledged, resolved with a note, cancelled).</summary>
    private async Task PollHelpAsync(CancellationToken cancellationToken)
    {
        HelpState? help = Help;
        if (help is not { Id: not null, Status: "open" or "acknowledged" })
        {
            return;
        }

        DateTimeOffset now = DateTimeOffset.Now;
        if (now - _lastHelpPoll < TimeSpan.FromSeconds(_options.HelpPollSeconds))
        {
            return;
        }

        _lastHelpPoll = now;
        DevIdentity identity = _identity.Current!;
        try
        {
            using HttpResponseMessage response = await SendAsync(HttpMethod.Get, "/api/v1/devs/me", null, identity.DevToken, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            JsonNode? body = await ReadJsonAsync(response, cancellationToken);
            JsonNode? active = body?["activeHelpRequest"];
            JsonNode? last = body?["lastHelpRequest"];
            JsonNode? mine = active?["id"]?.ToString() == help.Id ? active : last?["id"]?.ToString() == help.Id ? last : null;
            if (mine is not null)
            {
                SetHelp(FromServer(help, mine));
            }
            else if (active is null || active is JsonValue)
            {
                // The server no longer reports it as active and gives no detail: closed by the trainer.
                SetHelp(help with { Status = "resolved", ClosedAt = now, Error = null });
            }

            Succeed();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Fail("offline", "unreachable", null, ex);
        }
    }

    private static HelpState FromServer(HelpState local, JsonNode? node) => local with
    {
        Id = node?["id"]?.ToString() ?? local.Id,
        Status = node?["status"]?.ToString() is { Length: > 0 } status ? status : local.Status,
        LabId = node?["labId"]?.ToString() ?? local.LabId,
        AdminNote = node?["adminNote"]?.ToString() ?? local.AdminNote,
        CreatedAt = ReadDate(node?["createdAt"]) ?? local.CreatedAt,
        AcknowledgedAt = ReadDate(node?["acknowledgedAt"]) ?? local.AcknowledgedAt,
        ClosedAt = ReadDate(node?["closedAt"]) ?? local.ClosedAt,
    };

    private static DateTimeOffset? ReadDate(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue(out string? text) && DateTimeOffset.TryParse(text, out DateTimeOffset date) ? date : null;

    private HelpState SetHelp(HelpState help)
    {
        lock (_gate) { _help = help; }
        return help;
    }

    private async Task RunOnNetworkAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        await _network.WaitAsync(cancellationToken);
        try
        {
            await action(cancellationToken);
        }
        finally
        {
            _network.Release();
        }
    }

    // ---------- http ----------

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, string? token, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = CreateRequest(method, path, body, token);
        return await SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Every call to the admin API goes through here: a refused API key or workshop key stops the reporting (retrying the same
    /// value only brings the server's lockout closer), and a 429 schedules the next attempt after the server's Retry-After.
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await _http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            // Buffered: the caller may read the body again.
            await response.Content.LoadIntoBufferAsync(cancellationToken);
            switch ((await ReadJsonAsync(response, cancellationToken))?["error"]?.ToString())
            {
                case "invalidApiKey":
                    Halt("apiKey", "The admin API refused the API key (401 invalidApiKey): reporting is stopped until the dashboard is restarted with a valid Dashboard:Reporting:ApiKey.");
                    break;
                case "invalidWorkshopKey":
                    Halt("workshopKey", "The admin API refused the workshop key (401 invalidWorkshopKey): reporting is stopped until a new key is entered in the settings.");
                    break;
            }
        }
        else if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            Fail("offline", "rateLimited", RetryAfter(response));
        }

        return response;
    }

    /// <summary>The single place where a request to the admin API is built: base URL, dev token, and the API key on every /api/v1 route but health.</summary>
    internal HttpRequestMessage CreateRequest(HttpMethod method, string path, object? body, string? token)
    {
        HttpRequestMessage request = new(method, new Uri(new Uri(ServerUrl!.TrimEnd('/') + "/"), path.TrimStart('/')));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // Never an empty header (a missing key halts the reporting before any request), never in the URL.
        if (_apiKey is not null && RequiresApiKey(path))
        {
            request.Headers.TryAddWithoutValidation(ApiKeyHeader, _apiKey);
        }

        return request;
    }

    private static bool RequiresApiKey(string path)
    {
        string normalized = "/" + path.TrimStart('/');
        return normalized.StartsWith("/api/v1/", StringComparison.Ordinal) && !normalized.Equals("/api/v1/health", StringComparison.Ordinal);
    }

    private static async Task<JsonNode?> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            string text = await response.Content.ReadAsStringAsync(cancellationToken);
            return string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException or IOException)
        {
            return null;
        }
    }

    private static TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        RetryConditionHeaderValue? header = response.Headers.RetryAfter;
        return header?.Delta ?? (header?.Date is { } date ? date - DateTimeOffset.Now : null);
    }

    // ---------- state ----------

    /// <summary>Why the reporting is stopped (<c>apiKeyMissing</c>, <c>apiKey</c>, <c>workshopKey</c>), or null.</summary>
    private string? Halted
    {
        get { lock (_gate) { return _halted; } }
    }

    private bool Paused => Halted is not null || DateTimeOffset.Now < _retryAt;

    private void Halt(string reason, string message)
    {
        lock (_gate)
        {
            _halted = reason;
            _registered = false;
        }

        WarnOnce(reason, message);
    }

    /// <summary>Logs a message once per reason for the process lifetime. The messages never contain a key value.</summary>
    private void WarnOnce(string reason, string message, params object?[] args)
    {
        if (_logged.TryAdd($"once:{reason}", DateTimeOffset.Now))
        {
#pragma warning disable CA2254 // The template is a constant of each call site.
            _logger.LogWarning(message, args);
#pragma warning restore CA2254
        }
    }

    private void Succeed()
    {
        lock (_gate)
        {
            if (_state != "connected")
            {
                _logger.LogInformation("Reporting to {Server}: connected.", ServerUrl);
            }

            _state = "connected";
            _lastError = null;
            _retries = 0;
            _retryAt = DateTimeOffset.MinValue;
        }
    }

    /// <summary>Records a failure and schedules the next attempt (exponential backoff with jitter, or the delay asked by the server).</summary>
    private void Fail(string state, string error, TimeSpan? retryAfter, Exception? exception = null)
    {
        TimeSpan delay;
        lock (_gate)
        {
            _state = state;
            _lastError = error;
            if (state == "rejected")
            {
                // No point in hammering the server: a rejected key or identity needs the developer (or the trainer) to act.
                delay = TimeSpan.FromSeconds(_options.RetryMaxSeconds);
            }
            else if (retryAfter is { } asked)
            {
                delay = asked < TimeSpan.Zero ? TimeSpan.Zero : asked;
            }
            else
            {
                double seconds = Math.Min(_options.RetryMaxSeconds, _options.RetryBaseSeconds * Math.Pow(2, Math.Min(_retries, 8)));
                delay = TimeSpan.FromSeconds(seconds * (0.8 + Random.Shared.NextDouble() * 0.4));
                _retries++;
            }

            _retryAt = DateTimeOffset.Now + delay;
        }

        LogThrottled($"fail:{error}", LogLevel.Information, exception, "Reporting to {Server}: {Error} ({State}); next attempt in {Delay:F0} s.",
            ServerUrl, error, state, delay.TotalSeconds);
    }

    /// <summary>Logs at most once per minute per key: a server down for an hour must not flood the console.</summary>
    private void LogThrottled(string key, LogLevel level, Exception? exception, string message, params object?[] args)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        DateTimeOffset last = _logged.GetOrAdd(key, DateTimeOffset.MinValue);
        if (now - last < TimeSpan.FromMinutes(1))
        {
            return;
        }

        _logged[key] = now;
#pragma warning disable CA2254 // The template is a constant of each call site.
        _logger.Log(level, exception, message, args);
#pragma warning restore CA2254
    }

    private void Wake()
    {
        if (_wake.CurrentCount == 0)
        {
            try { _wake.Release(); } catch (SemaphoreFullException) { }
        }
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
