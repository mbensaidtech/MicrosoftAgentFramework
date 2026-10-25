using System.Security.Cryptography;
using LabDashboard.Catalog;
using LabDashboard.Execution;
using LabDashboard.History;
using LabDashboard.Reporting;
using LabDashboard.Reset;
using LabDashboard.Settings;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;

namespace LabDashboard.Tests;

/// <summary>
/// Reset of exercises: preview, confirmation protocol (names, code, single use, expiry), what is deleted and what is kept.
/// Everything happens in a temporary repository: the real labs, history and _StartCompleted folders are never touched.
/// </summary>
public sealed class LabResetTests : IDisposable
{
    private const string LabProgram = """
        Console.WriteLine("=== Scenario 1: Hello ===");
        Console.WriteLine("hello");
        """;

    private readonly string _root = Directory.CreateTempSubdirectory("labbench-reset-").FullName;
    private readonly ManualClock _clock = new();
    private readonly List<string> _readOnlyFolders = [];
    private static int _runNumber;

    public void Dispose()
    {
        if (!OperatingSystem.IsWindows())
        {
            foreach (string folder in _readOnlyFolders)
            {
                File.SetUnixFileMode(folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }

        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task The_preview_counts_runs_and_describes_the_completed_copy_without_deleting_anything()
    {
        Bench bench = await CreateBenchAsync();
        WriteFile("Labs/LabA-First/_StartCompleted/obj/Debug/net10.0/LabA.cache", "12345");
        WriteFile("Labs/LabA-First/_StartCompleted/.DS_Store", "x");
        string historyBefore = File.ReadAllText(bench.HistoryFile);

        ResetPreview preview = Preview(bench, "lab-a");

        LabResetPreview lab = Assert.Single(preview.Labs);
        Assert.Equal(("LabA-First", "completed", 2, 1), (lab.FolderName, lab.Progress, lab.StartRuns, lab.SolutionRuns));
        Assert.Equal(Path.Combine("Labs", "LabA-First", "_StartCompleted"), lab.CompletedCopy!.Path);
        Assert.Equal((2, 6L, 0), (lab.CompletedCopy.Files, lab.CompletedCopy.Bytes, lab.CompletedCopy.SourceFiles));
        Assert.Null(lab.Blocked);
        Assert.NotNull(preview.ConfirmationId);
        Assert.Null(preview.Code); // one exercise: no code
        Assert.Equal(_clock.Now + LabResetService.ConfirmationLifetime, preview.ExpiresAt);

        Assert.Equal(historyBefore, File.ReadAllText(bench.HistoryFile));
        Assert.True(File.Exists(Path.Combine(_root, "Labs/LabA-First/_StartCompleted/obj/Debug/net10.0/LabA.cache")));
    }

    [Fact]
    public async Task Files_outside_bin_and_obj_are_reported_as_source_files()
    {
        Bench bench = await CreateBenchAsync();
        WriteFile("Labs/LabA-First/_StartCompleted/Program.cs", "// completed");
        WriteFile("Labs/LabA-First/_StartCompleted/bin/Debug/LabA.dll", "dll");

        CompletedCopyInfo copy = Preview(bench, "lab-a").Labs[0].CompletedCopy!;

        Assert.Equal((2, 1), (copy.Files, copy.SourceFiles));
    }

    [Fact]
    public async Task Several_labs_get_a_five_character_code_without_ambiguous_characters()
    {
        Bench bench = await CreateBenchAsync();

        ResetPreview preview = Preview(bench, "lab-b", "LAB-A", "lab-a");

        Assert.Equal(["lab-a", "lab-b"], preview.Labs.Select(l => l.Id)); // catalog order, duplicates removed
        Assert.Matches("^[ABCDEFGHJKMNPQRSTUVWXYZ23456789]{5}$", preview.Code);
    }

    [Theory]
    [InlineData(null, "required")]
    [InlineData(new string[0], "required")]
    [InlineData(new[] { "lab-a", "nope" }, "unknown")]
    public async Task A_missing_or_unknown_lab_is_rejected(string[]? labIds, string code)
    {
        Bench bench = await CreateBenchAsync();

        Assert.Null(bench.Reset.Preview(labIds, out string? error));
        Assert.Equal(code, error);
    }

    [Fact]
    public async Task Reset_removes_the_runs_and_the_folder_of_the_lab_and_nothing_else()
    {
        Bench bench = await CreateBenchAsync();
        WriteFile("Labs/LabA-First/_StartCompleted/obj/project.assets.json", "{}");
        WriteFile("Labs/LabA-First/_StartCompleted/Program.cs", "// completed");
        WriteFile("Labs/LabA-First/.tmp-start-check/Program.cs", "// other local folder");
        WriteFile("Labs/LabA-First/Start/obj/project.assets.json", "{}");
        string[] kept = ["Labs/LabA-First/Start/Project.csproj", "Labs/LabA-First/Start/Program.cs", "Labs/LabA-First/Start/obj/project.assets.json",
            "Labs/LabA-First/Solution/Project.csproj", "Labs/LabA-First/Solution/Program.cs", "Labs/LabA-First/README.md",
            "Labs/LabA-First/.tmp-start-check/Program.cs", "labs.json"];
        Dictionary<string, string> hashes = kept.ToDictionary(path => path, Hash);

        ResetPreview preview = Preview(bench, "lab-a");
        ResetOutcome outcome = await bench.Reset.ExecuteAsync(new ResetRequest(preview.ConfirmationId, ["LabA-First"], null));

        LabResetResult result = Assert.Single(outcome.Labs!);
        Assert.Equal(new LabResetResult("lab-a", LabResetStatus.Reset, 3, 2, true), result);
        Assert.Empty(bench.History.ForLab("lab-a"));
        Assert.False(Directory.Exists(Path.Combine(_root, "Labs/LabA-First/_StartCompleted")));
        Assert.All(kept, path => Assert.Equal(hashes[path], Hash(path)));
        Assert.Equal(2, bench.History.ForLab("lab-b").Count);

        // Written to disk: a restarted dashboard sees the same history.
        DashboardOptions options = new() { DataDirectory = Path.GetDirectoryName(Path.GetRelativePath(_root, bench.HistoryFile))! };
        RunHistoryStore reloaded = new(options, new HostingEnvironment { ContentRootPath = _root }, NullLogger<RunHistoryStore>.Instance);
        Assert.Empty(reloaded.ForLab("lab-a"));
        Assert.Equal(2, reloaded.ForLab("lab-b").Count);
        Assert.Equal("notStarted", Preview(bench, "lab-a").Labs[0].Progress);
    }

    [Fact]
    public async Task Names_must_be_typed_exactly_and_a_mismatch_deletes_nothing()
    {
        Bench bench = await CreateBenchAsync();
        WriteFile("Labs/LabA-First/_StartCompleted/obj/x", "x");
        ResetPreview preview = Preview(bench, "lab-a");

        string[]?[] attempts = [null, [], ["laba-first"], ["LabB-Second"], ["LabA-First", "LabA-First"]];
        foreach (string[]? names in attempts)
        {
            Assert.Equal(ResetRefusal.Names, (await bench.Reset.ExecuteAsync(new ResetRequest(preview.ConfirmationId, names, null))).Refusal);
        }

        Assert.Equal(3, bench.History.ForLab("lab-a").Count);
        Assert.True(Directory.Exists(Path.Combine(_root, "Labs/LabA-First/_StartCompleted")));

        // Surrounding spaces are not part of the name.
        ResetOutcome outcome = await bench.Reset.ExecuteAsync(new ResetRequest(preview.ConfirmationId, ["  LabA-First "], null));
        Assert.Equal(LabResetStatus.Reset, outcome.Labs![0].Status);
    }

    [Fact]
    public async Task Several_labs_need_only_the_code_and_the_third_wrong_code_cancels_the_confirmation()
    {
        Bench bench = await CreateBenchAsync();
        ResetPreview preview = Preview(bench, "lab-a", "lab-b");
        string[] names = ["LabB-Second", "LabA-First"]; // the right names do not replace the code
        string wrong = preview.Code == "AAAAA" ? "BBBBB" : "AAAAA";

        Assert.Equal(ResetRefusal.Code, (await bench.Reset.ExecuteAsync(new ResetRequest(preview.ConfirmationId, names, null))).Refusal);
        Assert.Equal(ResetRefusal.Code, (await bench.Reset.ExecuteAsync(new ResetRequest(preview.ConfirmationId, null, wrong))).Refusal);
        Assert.Equal(ResetRefusal.TooManyAttempts, (await bench.Reset.ExecuteAsync(new ResetRequest(preview.ConfirmationId, null, wrong))).Refusal);
        Assert.Equal(ResetRefusal.Expired, (await bench.Reset.ExecuteAsync(new ResetRequest(preview.ConfirmationId, null, preview.Code))).Refusal);
        Assert.Equal(3, bench.History.ForLab("lab-a").Count);
        Assert.Equal(2, bench.History.ForLab("lab-b").Count);

        // A new preview, the right code only (no name, case does not matter): both labs are reset.
        ResetPreview again = Preview(bench, "lab-a", "lab-b");
        ResetOutcome outcome = await bench.Reset.ExecuteAsync(new ResetRequest(again.ConfirmationId, null, again.Code!.ToLowerInvariant()));
        Assert.All(outcome.Labs!, result => Assert.Equal(LabResetStatus.Reset, result.Status));
        Assert.Empty(bench.History.ForLab("lab-a"));
        Assert.Empty(bench.History.ForLab("lab-b"));
    }

    [Fact]
    public async Task With_several_labs_the_names_are_not_checked()
    {
        Bench bench = await CreateBenchAsync();
        ResetPreview preview = Preview(bench, "lab-a", "lab-b");

        ResetOutcome outcome = await bench.Reset.ExecuteAsync(new ResetRequest(preview.ConfirmationId, ["whatever"], preview.Code));

        Assert.Null(outcome.Refusal);
        Assert.Equal(2, outcome.Labs!.Count(result => result.Status == LabResetStatus.Reset));
    }

    [Fact]
    public async Task One_lab_needs_its_name_and_no_code()
    {
        Bench bench = await CreateBenchAsync();
        ResetPreview preview = Preview(bench, "lab-b");

        Assert.Null(preview.Code);
        Assert.Equal(ResetRefusal.Names, (await bench.Reset.ExecuteAsync(new ResetRequest(preview.ConfirmationId, null, "ABCDE"))).Refusal);
        Assert.Equal(2, bench.History.ForLab("lab-b").Count);
        Assert.Equal(LabResetStatus.Reset, (await bench.Reset.ExecuteAsync(new ResetRequest(preview.ConfirmationId, ["LabB-Second"], null))).Labs![0].Status);
    }

    [Theory]
    [InlineData("LearningLabs/AzureOpenAI/Lab01-FirstBasicAIAgent", "Lab01-FirstBasicAIAgent")]
    [InlineData("LearningLabs/AzureOpenAI/Lab06_A2AServer/", "Lab06_A2AServer")]
    [InlineData("Lab03", "Lab03")]
    public void The_name_of_an_exercise_is_its_lab_folder(string path, string name)
    {
        LabDefinition lab = new()
        {
            Id = "x", Number = "01", Track = "t", Title = "t", Summary = "s", Level = "l",
            Path = path, StartProject = "Start/P.csproj", SolutionProject = "Solution/P.csproj",
        };

        Assert.Equal(name, LabResetService.FolderName(lab));
    }

    [Fact]
    public async Task A_confirmation_is_used_once_so_a_replayed_request_resets_nothing()
    {
        Bench bench = await CreateBenchAsync();
        ResetPreview preview = Preview(bench, "lab-a");
        ResetRequest request = new(preview.ConfirmationId, ["LabA-First"], null);

        Assert.Null((await bench.Reset.ExecuteAsync(request)).Refusal);
        await bench.History.AddAsync(Run("lab-a", RunTarget.Start, RunStatus.Failed)); // a new run after the reset

        Assert.Equal(ResetRefusal.Expired, (await bench.Reset.ExecuteAsync(request)).Refusal);
        Assert.Single(bench.History.ForLab("lab-a"));
    }

    [Fact]
    public async Task A_confirmation_expires_after_ten_minutes()
    {
        Bench bench = await CreateBenchAsync();
        ResetPreview preview = Preview(bench, "lab-a");

        _clock.Now += LabResetService.ConfirmationLifetime;

        Assert.Equal(ResetRefusal.Expired, (await bench.Reset.ExecuteAsync(new ResetRequest(preview.ConfirmationId, ["LabA-First"], null))).Refusal);
        Assert.Equal(ResetRefusal.Expired, (await bench.Reset.ExecuteAsync(new ResetRequest("not-a-confirmation", ["LabA-First"], null))).Refusal);
        Assert.Equal(3, bench.History.ForLab("lab-a").Count);
    }

    [Fact]
    public async Task A_lab_already_at_zero_is_reported_as_nothing_to_reset()
    {
        Bench bench = await CreateBenchAsync(withRuns: false);

        ResetPreview preview = Preview(bench, "lab-a");
        Assert.True(preview.Labs[0].NothingToReset);
        ResetOutcome outcome = await bench.Reset.ExecuteAsync(new ResetRequest(preview.ConfirmationId, ["LabA-First"], null));

        Assert.Equal(new LabResetResult("lab-a", LabResetStatus.NothingToReset, 0, 0, true), outcome.Labs![0]);
    }

    [Fact]
    public async Task A_symbolic_link_is_never_followed_nor_deleted()
    {
        if (OperatingSystem.IsWindows())
        {
            return; // creating links needs a privilege on Windows
        }

        Bench bench = await CreateBenchAsync();
        WriteFile("Outside/keep.txt", "precious");
        Directory.CreateSymbolicLink(Path.Combine(_root, "Labs/LabA-First/_StartCompleted"), Path.Combine(_root, "Outside"));

        ResetPreview preview = Preview(bench, "lab-a");

        Assert.Equal(ResetBlock.Link, preview.Labs[0].Blocked);
        Assert.Null(preview.ConfirmationId);
        Assert.True(bench.Reset.HasCompletedCopy(bench.Catalog.Find("lab-a")!));
        Assert.True(File.Exists(Path.Combine(_root, "Outside/keep.txt")));
    }

    [Fact]
    public async Task A_link_inside_the_completed_copy_is_removed_and_its_target_kept()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Bench bench = await CreateBenchAsync();
        WriteFile("Outside/keep.txt", "precious");
        WriteFile("Labs/LabA-First/_StartCompleted/obj/x", "x");
        Directory.CreateSymbolicLink(Path.Combine(_root, "Labs/LabA-First/_StartCompleted/obj/linked"), Path.Combine(_root, "Outside"));

        ResetPreview preview = Preview(bench, "lab-a");
        Assert.Equal(2, preview.Labs[0].CompletedCopy!.Files); // the link is one entry, not followed
        ResetOutcome outcome = await bench.Reset.ExecuteAsync(new ResetRequest(preview.ConfirmationId, ["LabA-First"], null));

        Assert.Equal(LabResetStatus.Reset, outcome.Labs![0].Status);
        Assert.False(Directory.Exists(Path.Combine(_root, "Labs/LabA-First/_StartCompleted")));
        Assert.Equal("precious", File.ReadAllText(Path.Combine(_root, "Outside/keep.txt")));
    }

    [Fact]
    public async Task A_catalog_project_inside_the_completed_copy_blocks_the_lab()
    {
        CreateProject("Labs/LabC-Odd/_StartCompleted");
        CreateProject("Labs/LabC-Odd/Solution");
        Bench bench = await CreateBenchAsync(extraLab: Lab("lab-c", "Labs/LabC-Odd", "_StartCompleted/Project.csproj"));

        ResetPreview preview = Preview(bench, "lab-c");

        Assert.Equal(ResetBlock.ContainsProject, preview.Labs[0].Blocked);
        Assert.Null(preview.ConfirmationId);
    }

    [Fact]
    public async Task A_folder_that_cannot_be_deleted_keeps_the_runs_of_its_lab_and_the_other_labs_are_reset()
    {
        if (OperatingSystem.IsWindows())
        {
            return; // permissions are not enforced the same way
        }

        if (Environment.UserName == "root")
        {
            return; // root deletes read-only entries
        }

        Bench bench = await CreateBenchAsync();
        WriteFile("Labs/LabA-First/_StartCompleted/obj/a", "a");
        WriteFile("Labs/LabA-First/_StartCompleted/obj/b", "b");
        string locked = Path.Combine(_root, "Labs/LabA-First/_StartCompleted/obj");
        File.SetUnixFileMode(locked, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        _readOnlyFolders.Add(locked);

        ResetPreview preview = Preview(bench, "lab-a", "lab-b");
        ResetOutcome outcome = await bench.Reset.ExecuteAsync(new ResetRequest(preview.ConfirmationId, ["LabA-First", "LabB-Second"], preview.Code));

        LabResetResult a = outcome.Labs!.Single(r => r.Id == "lab-a");
        Assert.Equal((LabResetStatus.Failed, false, 0), (a.Status, a.Verified, a.RunsRemoved));
        Assert.Contains(Path.Combine("Labs", "LabA-First", "_StartCompleted"), a.Error);
        Assert.Equal(3, bench.History.ForLab("lab-a").Count);
        Assert.Equal(LabResetStatus.Reset, outcome.Labs!.Single(r => r.Id == "lab-b").Status);
        Assert.Empty(bench.History.ForLab("lab-b"));
    }

    [Fact]
    public async Task A_running_lab_can_be_neither_previewed_nor_reset()
    {
        Bench bench = await CreateBenchAsync(program: "await Task.Delay(Timeout.Infinite);");
        ResetPreview before = Preview(bench, "lab-a");

        Assert.True(bench.Runner.TryStart(bench.Catalog.Find("lab-a")!, RunTarget.Start, out LabRun run));
        try
        {
            Assert.Equal(ResetBlock.Running, Preview(bench, "lab-a").Labs[0].Blocked);
            ResetOutcome outcome = await bench.Reset.ExecuteAsync(new ResetRequest(before.ConfirmationId, ["LabA-First"], null));
            Assert.Equal(ResetRefusal.Running, outcome.Refusal);
            Assert.Equal(["lab-a"], outcome.RunningLabIds!);
            Assert.Equal(3, bench.History.ForLab("lab-a").Count);
        }
        finally
        {
            run.RequestCancel();
            await CompleteAsync(run);
        }
    }

    [Fact]
    public async Task A_reset_lab_runs_again_normally()
    {
        Bench bench = await CreateBenchAsync();
        WriteFile("Labs/LabA-First/_StartCompleted/obj/x", "x");
        ResetPreview preview = Preview(bench, "lab-a");
        Assert.Null((await bench.Reset.ExecuteAsync(new ResetRequest(preview.ConfirmationId, ["LabA-First"], null))).Refusal);

        Assert.True(bench.Runner.TryStart(bench.Catalog.Find("lab-a")!, RunTarget.Start, out LabRun run));
        RunRecord record = await CompleteAsync(run);

        Assert.Equal(RunStatus.Passed, record.Status);
        Assert.Equal(record.RunId, Assert.Single(bench.History.ForLab("lab-a")).RunId);
        Assert.Equal("completed", Preview(bench, "lab-a").Labs[0].Progress);
    }

    [Fact]
    public async Task The_trainer_gets_a_new_snapshot_only_when_the_developer_joined_a_workshop()
    {
        Bench joined = await CreateBenchAsync(joined: true);
        int before = joined.Outbox!.PeekBatch().Count(e => e.Type == "progress.snapshot");
        ResetPreview preview = Preview(joined, "lab-a");
        await joined.Reset.ExecuteAsync(new ResetRequest(preview.ConfirmationId, ["LabA-First"], null));

        ReportingEvent snapshot = joined.Outbox.PeekBatch().Last();
        Assert.Equal("progress.snapshot", snapshot.Type);
        Assert.Equal(before + 1, joined.Outbox.PeekBatch().Count(e => e.Type == "progress.snapshot"));
        Assert.Equal("notStarted", snapshot.Payload!["labs"]![0]!["state"]!.GetValue<string>());
        Assert.Equal("completed", snapshot.Payload!["labs"]![1]!["state"]!.GetValue<string>());

        Bench alone = await CreateBenchAsync(joined: false, folder: "alone");
        ResetPreview other = Preview(alone, "lab-a");
        await alone.Reset.ExecuteAsync(new ResetRequest(other.ConfirmationId, ["LabA-First"], null));
        Assert.Equal(0, alone.Outbox!.Count);
    }

    [Fact]
    public async Task The_history_store_removes_several_labs_in_one_write()
    {
        Bench bench = await CreateBenchAsync();

        IReadOnlyDictionary<string, int> removed = await bench.History.RemoveLabsAsync(["lab-a", "lab-b", "lab-z"]);

        Assert.Equal(3, removed["lab-a"]);
        Assert.Equal(2, removed["lab-b"]);
        Assert.Equal(0, removed["lab-z"]);
        Assert.Equal("{}", File.ReadAllText(bench.HistoryFile).Trim());
        Assert.False(File.Exists(bench.HistoryFile + ".tmp"));
    }

    // ---------- helpers ----------

    private sealed record Bench(LabCatalog Catalog, RunHistoryStore History, LabRunner Runner, LabResetService Reset, string HistoryFile, ReportingOutbox? Outbox);

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 25, 15, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static ResetPreview Preview(Bench bench, params string[] labIds) =>
        bench.Reset.Preview(labIds, out _) ?? throw new InvalidOperationException("No preview.");

    /// <summary>Two labs in a temporary repository: lab-a (completed: 2 Start runs, 1 Solution run) and lab-b (2 Start runs).</summary>
    private async Task<Bench> CreateBenchAsync(
        bool withRuns = true, string? extraLab = null, string program = LabProgram, bool? joined = null, string folder = "bench")
    {
        foreach (string lab in new[] { "Labs/LabA-First", "Labs/LabB-Second" })
        {
            CreateProject($"{lab}/Start", program);
            CreateProject($"{lab}/Solution", program);
            WriteFile($"{lab}/README.md", "# Lab");
        }

        string catalogFile = Path.Combine(_root, "labs.json");
        File.WriteAllText(catalogFile, $$"""
            { "labs": [ {{Lab("lab-a", "Labs/LabA-First")}}, {{Lab("lab-b", "Labs/LabB-Second")}}{{(extraLab is null ? "" : $", {extraLab}")}} ] }
            """);
        LabCatalog catalog = LabCatalog.Load(catalogFile, _root);

        string data = Path.Combine(folder, "data");
        DashboardOptions options = new() { DataDirectory = data };
        HostingEnvironment environment = new() { ContentRootPath = _root };
        RunHistoryStore history = new(options, environment, NullLogger<RunHistoryStore>.Instance);
        if (withRuns)
        {
            await history.AddAsync(Run("lab-a", RunTarget.Start, RunStatus.Failed));
            await history.AddAsync(Run("lab-a", RunTarget.Start, RunStatus.Passed));
            await history.AddAsync(Run("lab-a", RunTarget.Solution, RunStatus.Passed));
            await history.AddAsync(Run("lab-b", RunTarget.Start, RunStatus.Passed));
            await history.AddAsync(Run("lab-b", RunTarget.Start, RunStatus.Failed));
        }

        AzureOpenAISettingsStore settings = new(Path.Combine(_root, "secrets", "secrets.json"), "test", [], () => new Dictionary<string, string>());
        ReportingClient? reporting = null;
        ReportingOutbox? outbox = null;
        if (joined is not null)
        {
            // Never started: events stay in the outbox, nothing is sent.
            options.Reporting.ServerUrl = "http://127.0.0.1:9";
            IdentityStore identity = new(Path.Combine(_root, data, "identity.json"));
            if (joined.Value)
            {
                identity.Create("john-dev");
            }

            outbox = new ReportingOutbox(Path.Combine(_root, data, "outbox.json"));
            reporting = new ReportingClient(options, identity, outbox, catalog, history, settings, NullLogger<ReportingClient>.Instance, environment: _ => null);
        }

        LabRunner runner = new(catalog, history, settings, options, NullLogger<LabRunner>.Instance, reporting);
        LabResetService reset = new(catalog, history, runner, NullLogger<LabResetService>.Instance, reporting, _clock);
        return new Bench(catalog, history, runner, reset, Path.Combine(_root, data, "history.json"), outbox);
    }

    private static string Lab(string id, string path, string startProject = "Start/Project.csproj") => $$"""
        { "id": "{{id}}", "number": "01", "track": "t", "title": "{{id}}", "summary": "s", "level": "l",
          "path": "{{path}}", "startProject": "{{startProject}}", "solutionProject": "Solution/Project.csproj",
          "timeoutSeconds": 170, "expectations": [ { "id": "s1", "description": "answered", "pattern": "^=== Scenario 1:[^\\n]*===\\n\\S" } ] }
        """;

    private void CreateProject(string folder, string program = LabProgram)
    {
        WriteFile($"{folder}/Project.csproj", """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);
        WriteFile($"{folder}/Program.cs", program);
    }

    private void WriteFile(string relativePath, string content)
    {
        string path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private string Hash(string relativePath) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(_root, relativePath))));

    private static RunRecord Run(string labId, RunTarget target, RunStatus status) => new()
    {
        RunId = $"run-{Interlocked.Increment(ref _runNumber)}",
        LabId = labId,
        Target = target,
        Status = status,
        Summary = status.ToString(),
        StartedAt = DateTimeOffset.Now,
        DurationMs = 1000,
    };

    private static async Task<RunRecord> CompleteAsync(LabRun run)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromMinutes(3));
        await foreach (RunEvent _ in run.ReadEventsAsync(timeout.Token))
        {
        }

        return Assert.IsType<RunRecord>(run.Result);
    }
}
