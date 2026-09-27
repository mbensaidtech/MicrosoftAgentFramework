using LabDashboard.Catalog;
using LabDashboard.Execution;
using LabDashboard.History;
using LabDashboard.Settings;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;

namespace LabDashboard.Tests;

/// <summary>
/// End-to-end: a throwaway console project reads three lines with Console.ReadLine, run by the real runner
/// (dotnet build, then dotnet run --no-build with the input hook), answered through <see cref="RunInput"/>.
/// </summary>
public sealed class InteractiveRunTests : IDisposable
{
    private const string Key = "sk-test-0123456789abcdef0123456789";

    private const string Program = """
        Console.Write("Name> ");
        Console.WriteLine($"name=[{Console.ReadLine()}]");
        Console.WriteLine("Approve? (y/n)");
        Console.WriteLine($"approved=[{Console.ReadLine()}]");
        Console.Write("Last> ");
        string? last = Console.ReadLine();
        Console.WriteLine(last is null ? "last=<eof>" : $"last=[{last}]");
        """;

    private readonly string _root = Directory.CreateTempSubdirectory("labbench-interactive-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task Answers_each_read_in_order_with_non_ascii_text_and_masks_a_typed_secret()
    {
        (LabRunner runner, LabDefinition lab) = CreateRunner(Program, """ "interactive": true """);
        Assert.True(runner.TryStart(lab, RunTarget.Solution, out LabRun run));

        string[] answers = ["héllo €", "y", Key];
        List<RunEvent> events = await ReadAllAsync(run, request => Assert.Equal(InputResult.Accepted, run.Input!.Send(answers[request - 1])));

        RunRecord result = Assert.IsType<RunRecord>(run.Result);
        Assert.Equal(RunStatus.Passed, result.Status);
        Assert.Equal([1, 2, 3], events.Where(e => e.Type == "input-request").Select(e => e.InputId!.Value));
        Assert.Equal([1, 2, 3], events.Where(e => e.Type == "input-sent").Select(e => e.InputId!.Value));

        string stdout = string.Join("\n", result.Log.Where(l => l.Stream == "stdout").Select(l => l.Text));
        Assert.Contains("name=[héllo €]", stdout);
        Assert.Contains("approved=[y]", stdout);
        Assert.Contains($"last=[{SecretRedactor.Mask}]", stdout);

        // The prompt and the echo read as in a terminal; the typed key is masked; the read signal never shows.
        int prompt = result.Log.ToList().FindIndex(l => l is { Stream: "stdout", Text: "Name> ", Prompt: true });
        Assert.True(prompt >= 0);
        Assert.Equal(new LogLine("stdin", "héllo €"), result.Log[prompt + 1]);
        Assert.Contains(result.Log, l => l is { Stream: "stdin", Text: SecretRedactor.Mask });
        Assert.DoesNotContain(result.Log, l => l.Text.Contains(Key, StringComparison.Ordinal) || l.Text.Contains('\u001b'));
        Assert.DoesNotContain(events, e => e.Text?.Contains('\u001b') == true);
    }

    [Fact]
    public async Task Lines_typed_ahead_answer_the_next_reads_and_closing_ends_the_input()
    {
        (LabRunner runner, LabDefinition lab) = CreateRunner(Program, """ "interactive": true """);
        Assert.True(runner.TryStart(lab, RunTarget.Solution, out LabRun run));

        List<RunEvent> events = await ReadAllAsync(run, request =>
        {
            if (request == 1)
            {
                Assert.Equal(InputResult.Accepted, run.Input!.Send("first"));
                Assert.Equal(InputResult.Accepted, run.Input!.Send("second"));
            }
            else
            {
                Assert.Equal(InputResult.Accepted, run.Input!.Close());
                Assert.Equal(InputResult.Closed, run.Input!.Send("too late"));
            }
        });

        RunRecord result = Assert.IsType<RunRecord>(run.Result);
        Assert.Equal(RunStatus.Passed, result.Status);
        // Read 2 was answered by the line typed ahead: it never asked.
        Assert.Equal([1, 3], events.Where(e => e.Type == "input-request").Select(e => e.InputId!.Value));
        Assert.Contains(events, e => e.Type == "input-closed");
        Assert.Contains(result.Log, l => l.Text == "approved=[second]");
        // Nothing was typed after the last prompt: it stays on the line of the answer.
        Assert.Contains(result.Log, l => l.Text == "Last> last=<eof>");
    }

    [Fact]
    public async Task An_unanswered_prompt_times_out_without_counting_the_wait_against_the_lab_timeout()
    {
        (LabRunner runner, LabDefinition lab) = CreateRunner(Program, """ "interactive": true, "inputIdleTimeoutSeconds": 2 """);
        Assert.True(runner.TryStart(lab, RunTarget.Solution, out LabRun run));

        await ReadAllAsync(run, _ => { });

        RunRecord result = Assert.IsType<RunRecord>(run.Result);
        Assert.Equal(RunStatus.TimedOut, result.Status);
        Assert.Equal("input", result.FailureStage);
        Assert.Contains("no input received", result.Summary);
    }

    [Fact]
    public async Task A_lab_that_is_not_interactive_still_reads_end_of_input_at_once()
    {
        (LabRunner runner, LabDefinition lab) = CreateRunner(Program, "");
        Assert.True(runner.TryStart(lab, RunTarget.Solution, out LabRun run));

        List<RunEvent> events = await ReadAllAsync(run, _ => Assert.Fail("No read is announced for a lab that is not interactive."));

        RunRecord result = Assert.IsType<RunRecord>(run.Result);
        Assert.Equal(RunStatus.Passed, result.Status);
        Assert.Null(run.Input);
        Assert.Contains(result.Log, l => l.Text == "Name> name=[]");
        Assert.Contains(result.Log, l => l.Text == "Last> last=<eof>");
        Assert.DoesNotContain(events, e => e.Type.StartsWith("input", StringComparison.Ordinal));
    }

    private static async Task<List<RunEvent>> ReadAllAsync(LabRun run, Action<int> onRequest)
    {
        List<RunEvent> events = [];
        using CancellationTokenSource timeout = new(TimeSpan.FromMinutes(3));
        await foreach (RunEvent runEvent in run.ReadEventsAsync(timeout.Token))
        {
            events.Add(runEvent);
            if (runEvent.Type == "input-request")
            {
                onRequest(runEvent.InputId!.Value);
            }
        }

        return events;
    }

    private (LabRunner Runner, LabDefinition Lab) CreateRunner(string program, string extraFields)
    {
        string project = Path.Combine(_root, "AskingLab", "Solution");
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
        File.WriteAllText(Path.Combine(project, "Program.cs"), program);

        string extra = extraFields.Trim().Length == 0 ? "" : $", {extraFields}";
        string catalogFile = Path.Combine(_root, "labs.json");
        File.WriteAllText(catalogFile, $$"""
            { "labs": [ { "id": "asking", "number": "98", "track": "t", "title": "Asking", "summary": "s", "level": "l",
                          "path": "AskingLab", "startProject": "Solution/AskingLab.csproj", "solutionProject": "Solution/AskingLab.csproj",
                          "timeoutSeconds": 170 {{extra}} } ] }
            """);
        LabCatalog catalog = LabCatalog.Load(catalogFile, _root);

        AzureOpenAISettingsStore settings = new(Path.Combine(_root, "secrets", "secrets.json"), "test", [], () => new Dictionary<string, string>());
        Assert.Empty(settings.SaveAsync(new(null, null, Key)).GetAwaiter().GetResult());

        DashboardOptions options = new() { DataDirectory = "data" };
        HostingEnvironment environment = new() { ContentRootPath = _root };
        RunHistoryStore history = new(options, environment, NullLogger<RunHistoryStore>.Instance);
        return (new LabRunner(catalog, history, settings, options, NullLogger<LabRunner>.Instance), catalog.Labs[0]);
    }
}
