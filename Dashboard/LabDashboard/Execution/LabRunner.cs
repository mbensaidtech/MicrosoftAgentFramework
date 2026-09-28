using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using LabDashboard.Catalog;
using LabDashboard.History;
using LabDashboard.Settings;

namespace LabDashboard.Execution;

/// <summary>
/// Builds and runs one lab project at a time with the same commands a developer types:
/// <c>dotnet build</c> then <c>dotnet run --no-build</c>. The labs are not modified in any way.
/// The API keys the labs may receive are masked in everything that leaves the runner (live events, result, history).
/// </summary>
public sealed class LabRunner(
    LabCatalog catalog,
    RunHistoryStore history,
    AzureOpenAISettingsStore settings,
    DashboardOptions options,
    ILogger<LabRunner> logger)
{
    private const int MaxLogLines = 2000;
    private const int KeptRuns = 20;

    private readonly object _gate = new();
    private readonly ConcurrentDictionary<string, LabRun> _runs = new();
    private LabRun? _active;

    public LabRun? ActiveRun
    {
        get
        {
            lock (_gate)
            {
                return _active is { IsCompleted: false } ? _active : null;
            }
        }
    }

    public LabRun? Find(string runId) => _runs.GetValueOrDefault(runId);

    /// <summary>Starts a run unless another one is in progress (builds share CommonUtilities, so runs are serialized).</summary>
    public bool TryStart(LabDefinition lab, RunTarget target, out LabRun run)
    {
        lock (_gate)
        {
            if (_active is { IsCompleted: false } active)
            {
                run = active;
                return false;
            }

            run = new LabRun(lab, target);
            _active = run;
            _runs[run.Id] = run;
            TrimCompletedRuns();
        }

        LabRun started = run;
        _ = Task.Run(() => ExecuteAsync(started));
        return true;
    }

    private async Task ExecuteAsync(LabRun run)
    {
        Collector collector = new(run, new SecretRedactor(settings.SecretValues()));
        if (run.Lab.Interactive)
        {
            run.Input = new RunInput(run, collector.Stdin);
        }

        using CancellationTokenSource timeout = new();
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(run.Cancellation.Token, timeout.Token);
        using CancellationTokenSource watching = new();
        Task watchdog = WatchTimeoutsAsync(run, timeout, watching.Token);

        RunRecord record;
        try
        {
            record = await BuildRunAndCheckAsync(run, collector, linked.Token);
        }
        catch (OperationCanceledException)
        {
            (RunStatus status, string? stage, string summary) = run.CancelRequested ? (RunStatus.Cancelled, (string?)null, "Cancelled")
                : run.InputTimedOut ? (RunStatus.TimedOut, "input", $"Timed out: no input received for {run.Lab.InputIdleTimeoutSeconds} s")
                : (RunStatus.TimedOut, null, $"Timed out after {run.Lab.TimeoutSeconds} s");
            collector.System(summary);
            record = CreateRecord(run, status, stage, summary, collector);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Run {RunId} of {LabId} failed unexpectedly.", run.Id, run.Lab.Id);
            string message = collector.Redact(ex.Message);
            collector.System($"Dashboard error: {message}");
            record = CreateRecord(run, RunStatus.Failed, "dashboard", $"Dashboard error: {message}", collector) with { Errors = [message] };
        }

        watching.Cancel();
        await watchdog;

        try
        {
            await history.AddAsync(record);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not save run {RunId} to the history.", run.Id);
        }
        finally
        {
            run.Complete(record);
        }
    }

    /// <summary>Why a run must stop now, if it must: the lab timeout counts only the time not spent waiting for the learner.</summary>
    internal static TimeoutKind? EvaluateTimeouts(TimeSpan elapsed, TimeSpan waited, TimeSpan currentWait, TimeSpan labTimeout, TimeSpan inputIdleTimeout) =>
        currentWait >= inputIdleTimeout ? TimeoutKind.Input
        : elapsed - waited >= labTimeout ? TimeoutKind.Lab
        : null;

    internal enum TimeoutKind
    {
        Lab,
        Input
    }

    private static async Task WatchTimeoutsAsync(LabRun run, CancellationTokenSource timeout, CancellationToken stop)
    {
        TimeSpan labTimeout = TimeSpan.FromSeconds(run.Lab.TimeoutSeconds);
        TimeSpan inputIdleTimeout = TimeSpan.FromSeconds(run.Lab.InputIdleTimeoutSeconds);
        using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(200));
        try
        {
            while (await timer.WaitForNextTickAsync(stop))
            {
                RunInput? input = run.Input;
                TimeoutKind? kind = EvaluateTimeouts(
                    run.Elapsed, input?.WaitedTime ?? TimeSpan.Zero, input?.CurrentWait ?? TimeSpan.Zero, labTimeout, inputIdleTimeout);
                if (kind is not null)
                {
                    run.InputTimedOut = kind == TimeoutKind.Input;
                    await timeout.CancelAsync();
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The run finished first.
        }
    }

    private async Task<RunRecord> BuildRunAndCheckAsync(LabRun run, Collector collector, CancellationToken cancellationToken)
    {
        string project = catalog.ProjectPath(run.Lab, run.Target);
        string projectDirectory = Path.GetDirectoryName(project)!;
        string dotnet = ResolveDotnet();

        // 1. Build
        Stopwatch phase = Stopwatch.StartNew();
        collector.Phase("build", "started");
        collector.System($"$ dotnet build {Relative(project)} -nologo -v minimal");
        int buildExit = await ProcessRunner.RunAsync(
            dotnet, ["build", project, "-nologo", "-v", "minimal"], projectDirectory, collector.ForPhase("build"), cancellationToken);
        long buildMs = phase.ElapsedMilliseconds;
        IReadOnlyList<string> warnings = OutputAnalyzer.BuildDiagnostics(collector.BuildLines, "warning");

        if (buildExit != 0)
        {
            collector.Phase("build", "failed");
            IReadOnlyList<string> buildErrors = OutputAnalyzer.BuildDiagnostics(collector.BuildLines, "error");
            return CreateRecord(run, RunStatus.Failed, "build", $"Build failed ({buildErrors.Count} error(s))", collector) with
            {
                BuildDurationMs = buildMs,
                ExitCode = buildExit,
                Errors = buildErrors,
                BuildWarnings = warnings,
            };
        }

        string? companionProject = catalog.CompanionProjectPath(run.Lab);
        if (companionProject is not null)
        {
            // The companion is built like the lab (it may be the reference solution of another lab).
            collector.System($"$ dotnet build {Relative(companionProject)} -nologo -v minimal");
            buildExit = await ProcessRunner.RunAsync(
                dotnet, ["build", companionProject, "-nologo", "-v", "minimal"], Path.GetDirectoryName(companionProject)!,
                collector.ForPhase("build"), cancellationToken);
            buildMs = phase.ElapsedMilliseconds;
            warnings = OutputAnalyzer.BuildDiagnostics(collector.BuildLines, "warning");
            if (buildExit != 0)
            {
                collector.Phase("build", "failed");
                IReadOnlyList<string> buildErrors = OutputAnalyzer.BuildDiagnostics(collector.BuildLines, "error");
                return CreateRecord(run, RunStatus.Failed, "build", $"Build of the companion failed ({buildErrors.Count} error(s))", collector) with
                {
                    BuildDurationMs = buildMs,
                    ExitCode = buildExit,
                    Errors = buildErrors,
                    BuildWarnings = warnings,
                };
            }
        }

        collector.Phase("build", "completed");

        // 2. Run: the exact command a developer would type (with its companion, if any)
        phase.Restart();
        collector.Phase("run", "started");
        RunOutcome outcome = run.Lab.Companion is not { } companion
            ? await RunLabAsync(run, project, projectDirectory, dotnet, collector, cancellationToken)
            : companion.Role == CompanionRole.Server
                ? await RunWithCompanionServerAsync(run, companion, project, projectDirectory, companionProject!, dotnet, collector, cancellationToken)
                : await RunWithCompanionClientAsync(run, companion, project, projectDirectory, companionProject!, dotnet, collector, cancellationToken);
        long runMs = phase.ElapsedMilliseconds;
        collector.Phase("run", outcome.FailureStage is null && outcome.ExitCode == 0 ? "completed" : "failed");

        // 3. Checks on the standard output (of the lab, then of a companion client)
        collector.Phase("checks", "started");
        IReadOnlyList<string> stdout = run.Lab.Companion?.Role == CompanionRole.Client
            ? [.. collector.RunStdout, .. collector.CompanionStdout]
            : collector.RunStdout;
        IReadOnlyList<ExpectationResult> checks = OutputAnalyzer.EvaluateExpectations(run.Lab.Expectations, stdout);
        IReadOnlyList<string> runtimeErrors = outcome.Errors ?? OutputAnalyzer.RuntimeErrors(collector.RunStderr);
        int failedChecks = checks.Count(c => !c.Passed);

        (RunStatus status, string? stage, string summary) = (outcome, failedChecks) switch
        {
            ({ FailureStage: { } failureStage }, _) => (RunStatus.Failed, failureStage, $"{outcome.Summary}: {runtimeErrors.FirstOrDefault() ?? "no error output"}"),
            ({ ExitCode: not 0 }, _) => (RunStatus.Failed, "run", $"Exited with code {outcome.ExitCode}: {runtimeErrors.FirstOrDefault() ?? "no error output"}"),
            (_, > 0) => (RunStatus.Failed, "checks", $"{failedChecks} of {checks.Count} check(s) failed"),
            _ => (RunStatus.Passed, (string?)null, checks.Count == 0 ? "Exited with code 0" : $"All {checks.Count} checks passed"),
        };
        collector.Phase("checks", status == RunStatus.Passed ? "completed" : "failed");

        return CreateRecord(run, status, stage, summary, collector) with
        {
            BuildDurationMs = buildMs,
            RunDurationMs = runMs,
            ExitCode = outcome.ExitCode,
            Checks = checks,
            TokenUsage = OutputAnalyzer.ParseTokenUsage(stdout),
            Errors = runtimeErrors,
            BuildWarnings = warnings,
        };
    }

    /// <summary>
    /// How the run phase ended. <see cref="FailureStage"/> is set when a companion run could not complete:
    /// "ready" (the lab, a server, never said it was ready) or "companion" (the companion failed).
    /// </summary>
    private sealed record RunOutcome(int? ExitCode, string? FailureStage = null, string? Summary = null, IReadOnlyList<string>? Errors = null);

    private async Task<RunOutcome> RunLabAsync(
        LabRun run, string project, string projectDirectory, string dotnet, Collector collector, CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        collector.System($"$ dotnet run --project {Relative(project)} --no-build");
        InteractiveInput? interactive = run.Input is null ? null : new InteractiveInput(run.Input, ResolveInputHook());
        int exitCode = await ProcessRunner.RunAsync(
            dotnet, ["run", "--project", project, "--no-build"], projectDirectory, collector.ForPhase("run"), cancellationToken, interactive, environment);
        return new RunOutcome(exitCode);
    }

    /// <summary>The lab calls a server: start the companion server, wait until it is ready, run the lab, then stop the server.</summary>
    private async Task<RunOutcome> RunWithCompanionServerAsync(
        LabRun run, LabCompanion companion, string project, string projectDirectory, string companionProject, string dotnet,
        Collector collector, CancellationToken cancellationToken)
    {
        Regex readyPattern = new(companion.ReadyPattern, RegexOptions.CultureInvariant);
        TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        collector.System($"$ dotnet run --project {Relative(companionProject)} --no-build   (companion server{EnvironmentText(companion)})");
        Task<int> server = ProcessRunner.RunAsync(
            dotnet, ["run", "--project", companionProject, "--no-build"], Path.GetDirectoryName(companionProject)!,
            collector.ForPhase("companion", line => { if (readyPattern.IsMatch(line)) { ready.TrySetResult(); } }),
            stop.Token, environment: companion.Environment);
        try
        {
            int? serverExit = await WaitUntilReadyAsync(ready.Task, server, companion.ReadyTimeoutSeconds, cancellationToken);
            if (!ready.Task.IsCompleted)
            {
                return new RunOutcome(serverExit, "companion",
                    serverExit is { } code
                        ? $"The companion server exited with code {code} before it was ready"
                        : $"The companion server was not ready after {companion.ReadyTimeoutSeconds} s",
                    collector.CompanionErrors);
            }

            collector.System("Companion server ready.");
            return await RunLabAsync(run, project, projectDirectory, dotnet, collector, cancellationToken, companion.Environment);
        }
        finally
        {
            await StopAsync(stop, server);
            collector.System("Companion server stopped.");
        }
    }

    /// <summary>The lab is a server: start it, wait until it is ready, run the companion client, then stop the lab.</summary>
    private async Task<RunOutcome> RunWithCompanionClientAsync(
        LabRun run, LabCompanion companion, string project, string projectDirectory, string companionProject, string dotnet,
        Collector collector, CancellationToken cancellationToken)
    {
        Regex readyPattern = new(companion.ReadyPattern, RegexOptions.CultureInvariant);
        TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        collector.System($"$ dotnet run --project {Relative(project)} --no-build   (server, stopped after the companion client{EnvironmentText(companion)})");
        Task<int> server = ProcessRunner.RunAsync(
            dotnet, ["run", "--project", project, "--no-build"], projectDirectory,
            collector.ForPhase("run", line => { if (readyPattern.IsMatch(line)) { ready.TrySetResult(); } }),
            stop.Token, environment: companion.Environment);
        try
        {
            int? labExit = await WaitUntilReadyAsync(ready.Task, server, companion.ReadyTimeoutSeconds, cancellationToken);
            if (!ready.Task.IsCompleted)
            {
                string reason = labExit is { } code
                    ? $"The lab exited with code {code} before it was ready"
                    : $"The lab was not ready after {companion.ReadyTimeoutSeconds} s";
                return new RunOutcome(labExit, "ready", reason,
                    [$"No output line matched the ready pattern '{companion.ReadyPattern}'.", .. OutputAnalyzer.RuntimeErrors(collector.RunStderr)]);
            }

            collector.System("Lab ready.");
            collector.System($"$ dotnet run --project {Relative(companionProject)} --no-build   (companion client)");
            int clientExit = await ProcessRunner.RunAsync(
                dotnet, ["run", "--project", companionProject, "--no-build"], Path.GetDirectoryName(companionProject)!,
                collector.ForPhase("companion"), cancellationToken, environment: companion.Environment);
            return clientExit == 0
                ? new RunOutcome(0)
                : new RunOutcome(clientExit, "companion", $"The companion client exited with code {clientExit}", collector.CompanionErrors);
        }
        finally
        {
            await StopAsync(stop, server);
            collector.System("Lab stopped.");
        }
    }

    /// <summary>Waits for the ready signal; returns the exit code when the server exited first, null when it is ready or timed out.</summary>
    private static async Task<int?> WaitUntilReadyAsync(Task ready, Task<int> server, int timeoutSeconds, CancellationToken cancellationToken)
    {
        using CancellationTokenSource delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task delay = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), delayCancellation.Token);
        Task first = await Task.WhenAny(ready, server, delay);
        await delayCancellation.CancelAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return first == server && !ready.IsCompleted ? await server : null;
    }

    private static async Task StopAsync(CancellationTokenSource stop, Task<int> process)
    {
        await stop.CancelAsync();
        try
        {
            await process;
        }
        catch (OperationCanceledException)
        {
            // Stopped on purpose: the process tree was killed.
        }
    }

    private static string EnvironmentText(LabCompanion companion) =>
        companion.Environment.Count == 0 ? "" : ", " + string.Join(", ", companion.Environment.Select(e => $"{e.Key}={e.Value}"));

    private static RunRecord CreateRecord(LabRun run, RunStatus status, string? failureStage, string summary, Collector collector) => new()
    {
        RunId = run.Id,
        LabId = run.Lab.Id,
        Target = run.Target,
        Status = status,
        FailureStage = failureStage,
        Summary = summary,
        StartedAt = run.StartedAt,
        DurationMs = (long)run.Elapsed.TotalMilliseconds,
        Log = collector.Log,
    };

    private string ResolveDotnet() =>
        options.DotnetPath
        ?? Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")
        ?? "dotnet";

    private static string ResolveInputHook() =>
        File.Exists(InteractiveInput.DefaultHookPath)
            ? InteractiveInput.DefaultHookPath
            : throw new InvalidOperationException($"The input hook '{InteractiveInput.DefaultHookPath}' is missing: rebuild the dashboard.");

    private string Relative(string path) => Path.GetRelativePath(catalog.RepositoryRoot, path);

    private void TrimCompletedRuns()
    {
        foreach (LabRun old in _runs.Values.Where(r => r.IsCompleted).OrderByDescending(r => r.StartedAt).Skip(KeptRuns))
        {
            _runs.TryRemove(old.Id, out _);
        }
    }

    /// <summary>Publishes the output as events and keeps the lines needed for the analysis and the history.</summary>
    private sealed class Collector(LabRun run, SecretRedactor redactor)
    {
        private readonly object _gate = new();
        private readonly LabRun _run = run;
        private readonly SecretRedactor.Streaming _stdout = redactor.CreateStreaming();
        private readonly SecretRedactor.Streaming _stderr = redactor.CreateStreaming();
        private readonly SecretRedactor.Streaming _companionStdout = redactor.CreateStreaming();
        private readonly SecretRedactor.Streaming _companionStderr = redactor.CreateStreaming();
        private readonly List<LogLine> _log = [];
        private readonly List<string> _buildLines = [];
        private readonly List<string> _runStdout = [];
        private readonly List<string> _runStderr = [];
        private readonly List<string> _companionStdoutLines = [];
        private readonly List<string> _companionStderrLines = [];
        private readonly System.Text.StringBuilder _openStdout = new();
        private string _loggedPrompt = "";
        private string? _lastTransient;
        private DateTime _lastTransientAt;

        public IReadOnlyList<string> BuildLines { get { lock (_gate) { return _buildLines.ToList(); } } }

        public IReadOnlyList<string> RunStdout { get { lock (_gate) { return _runStdout.ToList(); } } }

        public IReadOnlyList<string> RunStderr { get { lock (_gate) { return _runStderr.ToList(); } } }

        /// <summary>Standard output of the companion process, if any.</summary>
        public IReadOnlyList<string> CompanionStdout { get { lock (_gate) { return _companionStdoutLines.ToList(); } } }

        /// <summary>The stderr lines of the companion worth showing.</summary>
        public IReadOnlyList<string> CompanionErrors { get { lock (_gate) { return OutputAnalyzer.RuntimeErrors(_companionStderrLines); } } }

        public IReadOnlyList<LogLine> Log { get { lock (_gate) { return _log.ToList(); } } }

        public void Phase(string phase, string state) => _run.Publish(_run.CreateEvent("phase") with { Phase = phase, State = state });

        public string Redact(string text) => redactor.Redact(text);

        public void System(string text)
        {
            text = redactor.Redact(text);
            AddLog("system", text);
            _run.Publish(_run.CreateEvent("output") with { Stream = "system", Text = text, EndOfLine = true });
        }

        /// <summary>A line typed by the learner, echoed as a terminal would show it.</summary>
        public void Stdin(string text)
        {
            text = redactor.Redact(text);
            lock (_gate)
            {
                // The prompt written by the program is still an open line: log it now, the learner's input continues it.
                if (_openStdout.Length > 0)
                {
                    string prompt = redactor.Redact(_openStdout.ToString());
                    AddLog(new LogLine("stdout", prompt) { Prompt = true });
                    _loggedPrompt += prompt;
                    _openStdout.Clear();
                }

                AddLog(new LogLine("stdin", text));
            }

            _run.Publish(_run.CreateEvent("output") with { Stream = "stdin", Text = text, EndOfLine = true });
        }

        /// <summary>
        /// Observer of one process: "build", "run" (the lab) or "companion" (shown as the "companion" stream).
        /// <paramref name="onStdoutLine"/> receives each completed (masked) stdout line, e.g. to detect that a server is ready.
        /// </summary>
        public IProcessOutputObserver ForPhase(string phase, Action<string>? onStdoutLine = null) => new Observer(this, phase, onStdoutLine);

        private void AddLog(string stream, string text) => AddLog(new LogLine(stream, text));

        private void AddLog(LogLine line)
        {
            lock (_gate)
            {
                if (_log.Count < MaxLogLines)
                {
                    _log.Add(line);
                }
            }
        }

        private sealed class Observer(Collector owner, string phase, Action<string>? onStdoutLine) : IProcessOutputObserver
        {
            private bool IsCompanion => phase == "companion";

            public void OnText(OutputStream stream, string text, bool endOfLine)
            {
                // Streamed text can split a secret in two pieces: the streaming redactor holds back a possible beginning.
                // The companion runs at the same time as the lab: it has its own redactors.
                SecretRedactor.Streaming redaction = (stream, IsCompanion) switch
                {
                    (OutputStream.Stdout, false) => owner._stdout,
                    (OutputStream.Stderr, false) => owner._stderr,
                    (OutputStream.Stdout, true) => owner._companionStdout,
                    _ => owner._companionStderr,
                };
                string safe = redaction.Push(text, endOfLine);
                if (phase == "run" && stream == OutputStream.Stdout)
                {
                    lock (owner._gate)
                    {
                        if (endOfLine)
                        {
                            owner._openStdout.Clear();
                        }
                        else
                        {
                            owner._openStdout.Append(safe);
                        }
                    }
                }

                if (safe.Length > 0 || endOfLine)
                {
                    owner._run.Publish(owner._run.CreateEvent("output") with { Stream = Name(stream), Text = safe, EndOfLine = endOfLine });
                }
            }

            public void OnTransient(OutputStream stream, string text)
            {
                text = owner.Redact(text);
                // Spinner frames change every few milliseconds: forward at most 4 updates per second.
                lock (owner._gate)
                {
                    if (text == owner._lastTransient || DateTime.UtcNow - owner._lastTransientAt < TimeSpan.FromMilliseconds(250))
                    {
                        return;
                    }

                    owner._lastTransient = text;
                    owner._lastTransientAt = DateTime.UtcNow;
                }

                owner._run.Publish(owner._run.CreateEvent("progress") with { Stream = Name(stream), Text = text });
            }

            public void OnLine(OutputStream stream, string line)
            {
                line = owner.Redact(line);
                lock (owner._gate)
                {
                    // The checks see the whole line; the log already holds the prompts shown before an input.
                    string logged = line;
                    if (phase == "run" && stream == OutputStream.Stdout && owner._loggedPrompt.Length > 0)
                    {
                        logged = line.StartsWith(owner._loggedPrompt, StringComparison.Ordinal) ? line[owner._loggedPrompt.Length..] : line;
                        owner._loggedPrompt = "";
                    }

                    owner.AddLog(Name(stream), logged);
                    if (phase == "build")
                    {
                        owner._buildLines.Add(line);
                    }
                    else if (IsCompanion)
                    {
                        (stream == OutputStream.Stdout ? owner._companionStdoutLines : owner._companionStderrLines).Add(line);
                    }
                    else if (stream == OutputStream.Stdout)
                    {
                        owner._runStdout.Add(line);
                    }
                    else
                    {
                        owner._runStderr.Add(line);
                    }
                }

                if (stream == OutputStream.Stdout)
                {
                    onStdoutLine?.Invoke(line);
                }
            }

            private string Name(OutputStream stream) => IsCompanion ? "companion" : stream == OutputStream.Stdout ? "stdout" : "stderr";
        }
    }
}
