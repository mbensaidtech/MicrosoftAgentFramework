namespace LabDashboard;

public sealed class DashboardOptions
{
    public const string SectionName = "Dashboard";

    /// <summary>Port of the local web UI. The dashboard only listens on 127.0.0.1.</summary>
    public int Port { get; set; } = 5057;

    /// <summary>Repository root, relative to the dashboard project folder.</summary>
    public string RepositoryRoot { get; set; } = "../..";

    /// <summary>Folder of the local run history (git-ignored), relative to the dashboard project folder.</summary>
    public string DataDirectory { get; set; } = "../.data";

    public int HistoryRunsPerLab { get; set; } = 30;

    /// <summary>Optional path of the dotnet executable. Defaults to the SDK host, then to "dotnet" on the PATH.</summary>
    public string? DotnetPath { get; set; }

    /// <summary>Reporting to the trainer's admin dashboard (see <see cref="ReportingOptions"/>). Disabled unless a server URL is set.</summary>
    public ReportingOptions Reporting { get; set; } = new();
}

/// <summary>
/// Section <c>Dashboard:Reporting</c>. The server URL and the API key are fixed by the project (appsettings.json / appsettings.Local.json,
/// overridable by environment variables) and never editable in the UI; the workshop key is entered by the developer.
/// </summary>
public sealed class ReportingOptions
{
    /// <summary>Base URL of the admin dashboard (e.g. https://labs-admin.example.com), set in appsettings.json. Empty: joining a workshop is impossible, no network call at all.</summary>
    public string? ServerUrl { get; set; }

    /// <summary>
    /// Shared key of the admin API, sent as <c>X-API-Key</c> on every <c>/api/v1/*</c> request, together with the workshop key or the dev token.
    /// Project configuration, never entered in the UI: the git-ignored <c>appsettings.Local.json</c>, or the <c>LABS_ADMIN_API_KEY</c> environment variable (wins).
    /// Missing: reporting stays off.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>Key distributed by the trainer, required to register. Never written in the repository.</summary>
    public string? WorkshopKey { get; set; }

    public double HeartbeatSeconds { get; set; } = 60;

    public double FlushIntervalSeconds { get; set; } = 5;

    public double RequestTimeoutSeconds { get; set; } = 10;

    /// <summary>Exponential backoff after a failed request: from <see cref="RetryBaseSeconds"/> up to <see cref="RetryMaxSeconds"/>, with jitter.</summary>
    public double RetryBaseSeconds { get; set; } = 5;

    public double RetryMaxSeconds { get; set; } = 60;

    /// <summary>How often the status of an active help request is read back from the server.</summary>
    public double HelpPollSeconds { get; set; } = 10;

    /// <summary>A lab opened again within this delay is not reported twice.</summary>
    public double LabOpenedDebounceSeconds { get; set; } = 300;
}
