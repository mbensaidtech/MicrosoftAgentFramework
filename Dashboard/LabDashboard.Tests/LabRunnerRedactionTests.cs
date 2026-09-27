using LabDashboard.Catalog;
using LabDashboard.Execution;
using LabDashboard.History;
using LabDashboard.Settings;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;

namespace LabDashboard.Tests;

/// <summary>
/// End-to-end: a throwaway console project prints the stored API key (on one line and split across two writes);
/// the key must not reach the live events, the run result or the history file.
/// </summary>
public sealed class LabRunnerRedactionTests : IDisposable
{
    private const string Key = "sk-test-0123456789abcdef0123456789";

    private readonly string _root = Directory.CreateTempSubdirectory("labbench-redaction-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task The_stored_api_key_printed_by_a_lab_is_masked_everywhere()
    {
        LabCatalog catalog = CreateLeakyLab();
        string secretsFile = Path.Combine(_root, "secrets", "secrets.json");
        AzureOpenAISettingsStore settings = new(secretsFile, "test", [], () => new Dictionary<string, string>());
        Assert.Empty(await settings.SaveAsync(new(null, null, Key)));

        DashboardOptions options = new() { DataDirectory = "data" };
        HostingEnvironment environment = new() { ContentRootPath = _root };
        RunHistoryStore history = new(options, environment, NullLogger<RunHistoryStore>.Instance);
        LabRunner runner = new(catalog, history, settings, options, NullLogger<LabRunner>.Instance);

        Assert.True(runner.TryStart(catalog.Labs[0], RunTarget.Solution, out LabRun run));
        List<RunEvent> events = [];
        using CancellationTokenSource timeout = new(TimeSpan.FromMinutes(3));
        await foreach (RunEvent runEvent in run.ReadEventsAsync(timeout.Token))
        {
            events.Add(runEvent);
        }

        RunRecord result = Assert.IsType<RunRecord>(run.Result);
        Assert.Equal(RunStatus.Passed, result.Status);

        string streamed = string.Concat(events.Where(e => e.Type == "output").Select(e => e.Text));
        Assert.DoesNotContain(Key, streamed);
        Assert.Contains($"direct={SecretRedactor.Mask}", streamed);
        Assert.Contains($"split={SecretRedactor.Mask}", streamed);

        Assert.DoesNotContain(result.Log, line => line.Text.Contains(Key, StringComparison.Ordinal));
        Assert.Contains(result.Log, line => line.Text == $"split={SecretRedactor.Mask}");

        string historyFile = await File.ReadAllTextAsync(Path.Combine(_root, "data", "history.json"));
        Assert.DoesNotContain(Key, historyFile);
    }

    private LabCatalog CreateLeakyLab()
    {
        string project = Path.Combine(_root, "LeakyLab", "Solution");
        Directory.CreateDirectory(project);
        File.WriteAllText(Path.Combine(project, "LeakyLab.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(project, "Program.cs"), $$"""
            Console.WriteLine("direct={{Key}}");
            Console.Write("split={{Key[..12]}}");
            Thread.Sleep(300);
            Console.WriteLine("{{Key[12..]}}");
            """);

        string catalogFile = Path.Combine(_root, "labs.json");
        File.WriteAllText(catalogFile, """
            { "labs": [ { "id": "leaky", "number": "99", "track": "t", "title": "Leaky", "summary": "s", "level": "l",
                          "path": "LeakyLab", "startProject": "Solution/LeakyLab.csproj", "solutionProject": "Solution/LeakyLab.csproj",
                          "timeoutSeconds": 170 } ] }
            """);

        return LabCatalog.Load(catalogFile, _root);
    }
}
