using LabDashboard.Catalog;
using LabDashboard.Execution;
using LabDashboard.History;
using LabDashboard.Settings;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;

namespace LabDashboard.Tests;

/// <summary>
/// End-to-end: throwaway console projects play a server (prints a ready line, then waits until it is stopped)
/// and a client, run by the real runner as a lab and its companion (dotnet build, then dotnet run --no-build).
/// </summary>
public sealed class CompanionRunTests : IDisposable
{
    private const string Server = """
        Console.WriteLine($"server value=[{Environment.GetEnvironmentVariable("LAB_TEST_VALUE")}]");
        Console.WriteLine("SERVER READY");
        await Task.Delay(Timeout.Infinite);
        """;

    private const string ExitingServer = """
        Console.WriteLine("server started without listening");
        """;

    private const string Client = """
        Console.WriteLine($"client value=[{Environment.GetEnvironmentVariable("LAB_TEST_VALUE")}]");
        Console.WriteLine("=== Scenario 1: Client ===");
        Console.WriteLine("Input tokens: 12");
        """;

    private readonly string _root = Directory.CreateTempSubdirectory("labbench-companion-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task A_companion_server_is_ready_before_the_lab_runs_and_is_stopped_after_it()
    {
        (LabRunner runner, LabDefinition lab) = CreateRunner(labProgram: Client, companionProgram: Server, role: "server",
            checks: """ { "id": "client", "description": "d", "pattern": "^client value=\\[shared\\]$" } """);
        Assert.True(runner.TryStart(lab, RunTarget.Solution, out LabRun run));

        RunRecord result = await CompleteAsync(run);

        Assert.Equal(RunStatus.Passed, result.Status);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains(result.Log, l => l is { Stream: "companion", Text: "server value=[shared]" });
        int ready = result.Log.ToList().FindIndex(l => l is { Stream: "system", Text: "Companion server ready." });
        int client = result.Log.ToList().FindIndex(l => l is { Stream: "stdout", Text: "client value=[shared]" });
        Assert.InRange(ready, 0, client);
        Assert.Equal("Companion server stopped.", result.Log.Last(l => l.Stream == "system").Text);
        Assert.Equal(12, result.TokenUsage!.Input);
    }

    [Fact]
    public async Task A_lab_that_is_a_server_is_checked_with_the_output_of_its_companion_client()
    {
        (LabRunner runner, LabDefinition lab) = CreateRunner(labProgram: Server, companionProgram: Client, role: "client",
            checks: """
                { "id": "server", "description": "d", "pattern": "^server value=\\[shared\\]$" },
                { "id": "client", "description": "d", "pattern": "^client value=\\[shared\\]$" }
                """);
        Assert.True(runner.TryStart(lab, RunTarget.Solution, out LabRun run));

        RunRecord result = await CompleteAsync(run);

        Assert.Equal(RunStatus.Passed, result.Status);
        Assert.All(result.Checks, check => Assert.True(check.Passed, check.Id));
        Assert.Contains(result.Log, l => l is { Stream: "companion", Text: "client value=[shared]" });
        Assert.Equal("Scenario 1 · Report 1", Assert.Single(result.TokenUsage!.Reports).Label);
        Assert.Equal("Lab stopped.", result.Log.Last(l => l.Stream == "system").Text);
    }

    [Fact]
    public async Task A_lab_that_exits_before_it_is_ready_fails_without_running_the_companion_client()
    {
        (LabRunner runner, LabDefinition lab) = CreateRunner(labProgram: ExitingServer, companionProgram: Client, role: "client",
            checks: """ { "id": "client", "description": "d", "pattern": "^client value=" } """);
        Assert.True(runner.TryStart(lab, RunTarget.Start, out LabRun run));

        RunRecord result = await CompleteAsync(run);

        Assert.Equal(RunStatus.Failed, result.Status);
        Assert.Equal("ready", result.FailureStage);
        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith("The lab exited with code 0 before it was ready", result.Summary);
        Assert.False(Assert.Single(result.Checks).Passed);
        Assert.DoesNotContain(result.Log, l => l.Stream == "companion");
    }

    [Fact]
    public async Task A_companion_server_that_exits_before_it_is_ready_fails_the_run_without_running_the_lab()
    {
        (LabRunner runner, LabDefinition lab) = CreateRunner(labProgram: Client, companionProgram: ExitingServer, role: "server",
            checks: """ { "id": "client", "description": "d", "pattern": "^client value=" } """);
        Assert.True(runner.TryStart(lab, RunTarget.Solution, out LabRun run));

        RunRecord result = await CompleteAsync(run);

        Assert.Equal(RunStatus.Failed, result.Status);
        Assert.Equal("companion", result.FailureStage);
        Assert.StartsWith("The companion server exited with code 0 before it was ready", result.Summary);
        Assert.DoesNotContain(result.Log, l => l.Text.StartsWith("client value=", StringComparison.Ordinal));
    }

    private static async Task<RunRecord> CompleteAsync(LabRun run)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromMinutes(3));
        await foreach (RunEvent _ in run.ReadEventsAsync(timeout.Token))
        {
        }

        return Assert.IsType<RunRecord>(run.Result);
    }

    private (LabRunner Runner, LabDefinition Lab) CreateRunner(string labProgram, string companionProgram, string role, string checks)
    {
        CreateProject("Lab", "Solution", labProgram);
        CreateProject("Lab", "Start", labProgram);
        CreateProject("Other", "Solution", companionProgram);

        string catalogFile = Path.Combine(_root, "labs.json");
        File.WriteAllText(catalogFile, $$"""
            { "labs": [ { "id": "pair", "number": "97", "track": "t", "title": "Pair", "summary": "s", "level": "l",
                          "path": "Lab", "startProject": "Start/Start.csproj", "solutionProject": "Solution/Solution.csproj",
                          "timeoutSeconds": 170,
                          "companion": { "role": "{{role}}", "project": "Other/Solution/Solution.csproj", "readyPattern": "^SERVER READY$",
                                         "readyTimeoutSeconds": 60, "environment": { "LAB_TEST_VALUE": "shared" } },
                          "expectations": [ {{checks}} ] } ] }
            """);
        LabCatalog catalog = LabCatalog.Load(catalogFile, _root);

        AzureOpenAISettingsStore settings = new(Path.Combine(_root, "secrets", "secrets.json"), "test", [], () => new Dictionary<string, string>());
        DashboardOptions options = new() { DataDirectory = "data" };
        HostingEnvironment environment = new() { ContentRootPath = _root };
        RunHistoryStore history = new(options, environment, NullLogger<RunHistoryStore>.Instance);
        return (new LabRunner(catalog, history, settings, options, NullLogger<LabRunner>.Instance), catalog.Labs[0]);
    }

    private void CreateProject(string lab, string folder, string program)
    {
        string project = Path.Combine(_root, lab, folder);
        Directory.CreateDirectory(project);
        File.WriteAllText(Path.Combine(project, $"{folder}.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(project, "Program.cs"), program);
    }
}
