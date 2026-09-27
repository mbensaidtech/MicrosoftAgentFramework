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
}
