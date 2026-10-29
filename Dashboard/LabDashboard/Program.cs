using System.Net;
using System.Net.ServerSentEvents;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using CommonUtilities;
using LabDashboard;
using LabDashboard.Catalog;
using LabDashboard.Execution;
using LabDashboard.History;
using LabDashboard.Journey;
using LabDashboard.Reporting;
using LabDashboard.Reset;
using LabDashboard.Settings;
using LabDashboard.TrainerModels;
using Markdig;
using Microsoft.Extensions.Configuration.Json;

// Trainer tool (Business Rule 23): `dotnet run --project Dashboard/LabDashboard -- invitation` prints an invitation code and exits,
// before anything of the dashboard is configured or started.
if (args is [InvitationCommand.Name, .. string[] commandArguments])
{
    return InvitationCommand.Run(commandArguments, InvitationCommand.ReadHidden, Console.Out, Console.Error);
}

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Project values that must stay out of the public repository (the admin API key): an optional, git-ignored file read right
// after appsettings.json, so environment variables and the command line still win.
int appSettingsIndex = builder.Configuration.Sources.ToList().FindLastIndex(source =>
    source is JsonConfigurationSource { Path: { } path } && path.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase));
builder.Configuration.Sources.Insert(appSettingsIndex + 1, new JsonConfigurationSource
{
    Path = "appsettings.Local.json",
    Optional = true,
    FileProvider = builder.Environment.ContentRootFileProvider,
});

DashboardOptions options = builder.Configuration.GetSection(DashboardOptions.SectionName).Get<DashboardOptions>() ?? new();

// Local tool: never reachable from the network.
builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, options.Port));

builder.Services.ConfigureHttpJsonOptions(json =>
{
    json.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    json.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});
LabCatalog labCatalog = LabCatalog.Load(
    Path.Combine(builder.Environment.ContentRootPath, "labs.json"),
    Path.Combine(builder.Environment.ContentRootPath, options.RepositoryRoot));

// Lab author tool: `dotnet run -- exercise-fingerprints` (from Dashboard/LabDashboard, on the exercises as delivered) writes the
// fingerprints that tell an implemented scenario from a delivered one, then exits.
if (args is [ExerciseFingerprints.Name, ..])
{
    return ExerciseFingerprints.Run(labCatalog, Console.Out);
}

builder.Services.AddSingleton(options);
builder.Services.AddSingleton(labCatalog);
builder.Services.AddSingleton<RunHistoryStore>();
builder.Services.AddSingleton(services => AzureOpenAISettingsStore.ForCatalog(services.GetRequiredService<LabCatalog>()));
builder.Services.AddSingleton<LabRunner>();
builder.Services.AddSingleton<LabResetService>();

// Reporting to the trainer's admin dashboard: off until the developer joins a workshop from the welcome dialog or the settings (no network call before that).
builder.Services.AddSingleton(services => IdentityStore.ForOptions(options, services.GetRequiredService<IHostEnvironment>(), services.GetRequiredService<ILogger<IdentityStore>>()));
builder.Services.AddSingleton(services => new ReportingOutbox(
    Path.Combine(Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, options.DataDirectory)), "outbox.json"),
    services.GetRequiredService<ILogger<ReportingOutbox>>()));
builder.Services.AddSingleton<ReportingClient>();
builder.Services.AddHostedService(services => services.GetRequiredService<ReportingClient>());

// Trainer AI model configuration (overview, "retrieve", then "unlock" with the trainer's code) from the admin dashboard (contract v1), with the
// admin URL and API key of the reporting and the identity's dev token: nothing is called before the candidate joined the workshop. The simulated
// configuration replaces it when enabled in the project configuration (Dashboard:TrainerModels:Mock); never from the browser.
builder.Services.AddSingleton<TrainerConfigurationService>();
builder.Services.AddSingleton<TrainerUnlockFlow>();

WebApplication app = builder.Build();
ReportingClient reporting = app.Services.GetRequiredService<ReportingClient>();

app.Use(async (context, next) =>
{
    // Reject requests whose Host is not local (DNS rebinding protection).
    if (context.Request.Host.Host is not ("127.0.0.1" or "localhost"))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    // State-changing requests need a custom header (forces a CORS preflight, which is never granted)
    // and, when the browser sends one, a same-origin Origin header (CSRF protection).
    if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
    {
        string? origin = context.Request.Headers.Origin;
        bool sameOrigin = origin is null || origin == $"{context.Request.Scheme}://{context.Request.Host}";
        if (context.Request.Headers["X-Lab-Dashboard"] != "1" || !sameOrigin)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
    }

    if (context.Request.Path.StartsWithSegments("/api"))
    {
        reporting.NoteBrowserActivity();
    }

    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    // Revalidate on every load (cheap on 127.0.0.1): an updated dashboard never shows a stale page or script.
    OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-cache",
});

MarkdownPipeline markdown = new MarkdownPipelineBuilder().UseAdvancedExtensions().DisableHtml().Build();
RouteGroupBuilder api = app.MapGroup("/api");

api.MapGet("/labs", (LabCatalog catalog, RunHistoryStore history, LabRunner runner, LabResetService reset) =>
    catalog.Labs.Select(lab => LabSummary.From(lab, history.ForLab(lab.Id), runner.ActiveRun, reset.HasCompletedCopy(lab))));

api.MapGet("/labs/{id}", (string id, string? lang, LabCatalog catalog, RunHistoryStore history, LabRunner runner, LabResetService reset) =>
{
    if (catalog.Find(id) is not { } lab)
    {
        return Results.NotFound();
    }

    // README in the requested language when the lab provides one (README.fr.md), otherwise the default README.
    string? localizedReadme = lang is null ? null : catalog.LocalizedReadmePath(lab, lang);
    string readmePath = localizedReadme ?? catalog.ReadmePath(lab);
    string readmeHtml = File.Exists(readmePath) ? Markdown.ToHtml(File.ReadAllText(readmePath), markdown) : "";
    IReadOnlyList<RunRecord> runs = history.ForLab(lab.Id);
    reporting.ReportLabOpened(lab.Id);

    return Results.Ok(new LabDetails(
        LabSummary.From(lab, runs, runner.ActiveRun, reset.HasCompletedCopy(lab)),
        lab.Objectives,
        Path.GetRelativePath(catalog.RepositoryRoot, catalog.LabDirectory(lab)),
        readmeHtml,
        localizedReadme is null ? "en" : lang!,
        PackageReferences(catalog.ProjectPath(lab, RunTarget.Solution)),
        lab.Expectations,
        new Dictionary<string, string>
        {
            ["start"] = $"cd {lab.Path}/{Path.GetDirectoryName(lab.StartProject)} && dotnet run",
            ["solution"] = $"cd {lab.Path}/{Path.GetDirectoryName(lab.SolutionProject)} && dotnet run",
            ["scenarios"] = $"dotnet run -- {LabScenarios.ScenariosOption} 1,3      dotnet run -- {LabScenarios.ListOption}",
        },
        runs.FirstOrDefault(),
        runs.Select(r => r with { Log = [] }).ToList()));
});

// The learner's journey (states, XP, rank, milestones, distinctions, next mission), recomputed from the history: read-only.
api.MapGet("/journey", (LabCatalog catalog, RunHistoryStore history, LabRunner runner) =>
    LearningJourney.Build(catalog.Labs, history.ForLab, runner.ActiveRun?.Lab.Id));

api.MapGet("/labs/{id}/solution", (string id, LabCatalog catalog) =>
{
    if (catalog.Find(id) is not { } lab)
    {
        return Results.NotFound();
    }

    reporting.Report("solution.viewed", lab.Id, null);
    return Results.Ok(SolutionFiles(catalog, lab));
});

// The scenarios of the exercise and of the solution, read now from their Program.cs (the learner edits the exercise meanwhile):
// state (implemented: its code, and the setup, are no longer the delivered ones), the scenarios each one requires, and the last result
// of each one on that project.
api.MapGet("/labs/{id}/scenarios", (string id, LabCatalog catalog, RunHistoryStore history) =>
{
    if (catalog.Find(id) is not { } lab)
    {
        return Results.NotFound();
    }

    IReadOnlyList<RunRecord> runs = history.ForLab(lab.Id);
    return Results.Ok(Enum.GetValues<RunTarget>().ToDictionary(
        target => JsonNamingPolicy.CamelCase.ConvertName(target.ToString()),
        target =>
        {
            IReadOnlyDictionary<int, (ScenarioStatus Status, RunRecord Run)> last = ExerciseProgress.LastScenarioResults(runs, target);
            return catalog.Scenarios(lab, target).Select(scenario => new
            {
                scenario.Number,
                scenario.Title,
                scenario.Implemented,
                scenario.SetupImplemented,
                scenario.Requires,
                Last = last.TryGetValue(scenario.Number, out var result)
                    ? new { result.Status, result.Run.RunId, result.Run.StartedAt }
                    : null,
            });
        }));
});

api.MapPost("/labs/{id}/runs", (string id, RunRequest request, LabCatalog catalog, LabRunner runner) =>
{
    if (catalog.Find(id) is not { } lab)
    {
        return Results.NotFound();
    }

    // The selection is checked against the Program.cs of the project, with the rules of the labs: the scenarios asked for and the
    // scenarios they require; without "scenarios", the implemented ones (as `dotnet run` without option). The program receives the
    // resulting list (`-- --scenarios 1,3`), so that what runs is exactly what was checked here.
    IReadOnlyList<LabScenario> scenarios = catalog.Scenarios(lab, request.Target);
    ScenarioRun? selection = null;
    if (scenarios.Count > 0 || request.Scenarios is not null)
    {
        ScenarioPlan plan = LabScenarios.Plan(scenarios, new ScenarioRequest(request.Scenarios));
        string? error = plan.Unknown.Count > 0 || (scenarios.Count == 0 && plan.Selected.Count > 0) ? "unknown"
            : plan.Selected.Count == 0 ? request.Scenarios is null ? "noneImplemented" : "required"
            : null;
        if (error is not null)
        {
            return Results.BadRequest(new { errors = new Dictionary<string, string> { ["scenarios"] = error } });
        }

        selection = new ScenarioRun(scenarios, plan.Selected);
    }

    return runner.TryStart(lab, request.Target, out LabRun run, selection)
        ? Results.Accepted($"/api/runs/{run.Id}/events", new { runId = run.Id, scenarios = selection?.Selected })
        : Results.Conflict(new { error = $"A run is already in progress ({run.Lab.Title}, {run.Target}).", runId = run.Id });
});

// Reset of exercises (runs + _StartCompleted): the preview issues a single-use confirmation and deletes nothing;
// the reset needs the typed folder names and, for several labs, the code of that preview.
api.MapPost("/labs/reset/preview", (ResetPreviewRequest request, LabResetService reset) =>
    reset.Preview(request.LabIds, out string? error) is { } preview
        ? Results.Ok(preview)
        : Results.BadRequest(new { errors = new Dictionary<string, string> { ["labIds"] = error! } }));

api.MapPost("/labs/reset", async (ResetRequest request, LabResetService reset) =>
{
    ResetOutcome outcome = await reset.ExecuteAsync(request);
    return outcome.Refusal switch
    {
        null => Results.Ok(new { labs = outcome.Labs }),
        ResetRefusal.Names => Results.BadRequest(new { errors = new Dictionary<string, string> { ["names"] = "mismatch" } }),
        ResetRefusal.Code => Results.BadRequest(new { errors = new Dictionary<string, string> { ["code"] = "mismatch" } }),
        ResetRefusal.Running => Results.Conflict(new { error = "running", labIds = outcome.RunningLabIds }),
        ResetRefusal.Busy => Results.Conflict(new { error = "busy" }),
        ResetRefusal.TooManyAttempts => Results.Json(new { error = "tooManyAttempts" }, statusCode: StatusCodes.Status410Gone),
        _ => Results.Json(new { error = "expired" }, statusCode: StatusCodes.Status410Gone),
    };
});

api.MapGet("/runs/{runId}/events", (string runId, LabRunner runner, CancellationToken cancellationToken) =>
    runner.Find(runId) is { } run
        ? TypedResults.ServerSentEvents(ToSse(run.ReadEventsAsync(cancellationToken)))
        : Results.NotFound());

api.MapPost("/runs/{runId}/cancel", (string runId, LabRunner runner) =>
{
    if (runner.Find(runId) is not { IsCompleted: false } run)
    {
        return Results.NotFound();
    }

    run.RequestCancel();
    return Results.Accepted();
});

// Standard input of an interactive run: one line per request, or end of input.
api.MapPost("/runs/{runId}/input", (string runId, InputRequest request, LabRunner runner) =>
{
    if (runner.Find(runId) is not { IsCompleted: false } run)
    {
        return Results.NotFound();
    }

    if (RunInput.Validate(request.Text) is { } error)
    {
        return Results.BadRequest(new { errors = new Dictionary<string, string> { ["text"] = error } });
    }

    return InputResponse(run, input => input.Send(request.Text!));
});

api.MapPost("/runs/{runId}/input/close", (string runId, LabRunner runner) =>
    runner.Find(runId) is { IsCompleted: false } run ? InputResponse(run, input => input.Close()) : Results.NotFound());

// Azure OpenAI settings shared by the labs (user secrets). The API key is write-only: it is never returned.
api.MapGet("/settings/azure-openai", (AzureOpenAISettingsStore settings) => settings.GetStatus());

api.MapPut("/settings/azure-openai", async (AzureOpenAISettingsUpdate update, AzureOpenAISettingsStore settings) =>
{
    try
    {
        IReadOnlyDictionary<string, string> errors = await settings.SaveAsync(update);
        if (errors.Count > 0)
        {
            return Results.BadRequest(new { errors });
        }

        AzureOpenAISettingsStatus status = settings.GetStatus();
        reporting.ReportSettingsChanged(status);
        return Results.Ok(status);
    }
    catch (AzureOpenAISettingsStoreException ex)
    {
        return SettingsStoreError(ex, settings);
    }
});

api.MapDelete("/settings/azure-openai/api-key", async (AzureOpenAISettingsStore settings) =>
{
    try
    {
        await settings.RemoveApiKeyAsync();
        AzureOpenAISettingsStatus status = settings.GetStatus();
        reporting.ReportSettingsChanged(status);
        return Results.Ok(status);
    }
    catch (AzureOpenAISettingsStoreException ex)
    {
        return SettingsStoreError(ex, settings);
    }
});

// AI models (LLM = "chat", embedding): per kind a custom profile, a trainer profile and the mode that selects the one the labs use.
// Keys are write-only: only "stored" and their last 4 characters are returned. Trainer profiles are written by the trainer flow only, never by a route.
api.MapGet("/settings/models", (AzureOpenAISettingsStore settings) => settings.GetModelStatus());

api.MapPut("/settings/models/{kind}/custom", async (string kind, ModelProfileUpdate update, AzureOpenAISettingsStore settings) =>
{
    if (!AzureOpenAISettingsStore.TryParseKind(kind, out ModelKind modelKind))
    {
        return Results.NotFound();
    }

    try
    {
        IReadOnlyDictionary<string, string> errors = await settings.SaveCustomProfileAsync(modelKind, update);
        return errors.Count > 0 ? Results.BadRequest(new { errors }) : ModelSettingsChanged(modelKind, settings, reporting);
    }
    catch (AzureOpenAISettingsStoreException ex)
    {
        return SettingsStoreError(ex, settings);
    }
});

api.MapDelete("/settings/models/{kind}/custom/api-key", async (string kind, AzureOpenAISettingsStore settings) =>
{
    if (!AzureOpenAISettingsStore.TryParseKind(kind, out ModelKind modelKind))
    {
        return Results.NotFound();
    }

    try
    {
        await settings.RemoveCustomApiKeyAsync(modelKind);
        return ModelSettingsChanged(modelKind, settings, reporting);
    }
    catch (AzureOpenAISettingsStoreException ex)
    {
        return SettingsStoreError(ex, settings);
    }
});

api.MapPut("/settings/models/{kind}/mode", async (string kind, ModelModeUpdate update, AzureOpenAISettingsStore settings) =>
{
    if (!AzureOpenAISettingsStore.TryParseKind(kind, out ModelKind modelKind))
    {
        return Results.NotFound();
    }

    try
    {
        ModelModeOutcome outcome = await settings.SwitchModeAsync(modelKind, update.Mode);
        return outcome.Error switch
        {
            null => ModelSettingsChanged(modelKind, settings, reporting),
            "invalid" => Results.BadRequest(new { errors = new Dictionary<string, string> { ["mode"] = "invalid" } }),
            // The profile to switch to is incomplete (the UI opens what is missing) or of an unsupported provider: nothing was written.
            _ => Results.Conflict(new { error = outcome.Error, missing = outcome.Missing }),
        };
    }
    catch (AzureOpenAISettingsStoreException ex)
    {
        return SettingsStoreError(ex, settings);
    }
});

// The trainer's AI model configurations (Business Rules 13–20): the overview, read without any code (when the AI models tab opens or on Refresh);
// Unlock for the ticked kinds (one envelope held by this server); the access code, used for this one request and never returned; cancel.
// On success the keys of the ticked kinds are saved in the user secrets and those kinds switch to trainer mode: the answer only says "stored".
// The code and the unlock run to completion even if the page goes away, so that an envelope the admin released is never lost half-way.
api.MapGet("/trainer-models", (TrainerUnlockFlow flow, CancellationToken cancellationToken) => flow.GetOverviewAsync(cancellationToken));

api.MapPost("/trainer-models/unlock", async (TrainerUnlockRequest request, TrainerUnlockFlow flow) =>
    ParseKinds(request.Kinds) is { } kinds
        ? Results.Ok(await flow.StartAsync(kinds))
        : Results.BadRequest(new { errors = new Dictionary<string, string> { ["kinds"] = "invalid" } }));

api.MapPost("/trainer-models/code", async (TrainerCodeRequest request, TrainerUnlockFlow flow, AzureOpenAISettingsStore settings) =>
{
    try
    {
        TrainerUnlockView view = await flow.SubmitCodeAsync(request.Code);
        if (view.Status == TrainerUnlockView.Unlocked && view.Kinds.Contains(ModelKind.Chat))
        {
            reporting.ReportSettingsChanged(settings.GetStatus());
        }

        return Results.Ok(view);
    }
    catch (AzureOpenAISettingsStoreException ex)
    {
        return SettingsStoreError(ex, settings);
    }
});

api.MapPost("/trainer-models/cancel", async (TrainerUnlockFlow flow) => Results.Ok(await flow.CancelAsync()));

// Reporting to the admin dashboard: identity, status and help requests. The workshop key, the admin API key and the dev token are never returned;
// the key field also takes the trainer's invitation code (Business Rule 21), which stores the admin API key too.
api.MapGet("/reporting/status", () => reporting.GetStatus());

api.MapPost("/identity", (ReportingSettingsUpdate update) => ApplyReportingSettingsAsync(update, reporting, settingsStore: app.Services.GetRequiredService<AzureOpenAISettingsStore>()));

// "Work on my own": the developer skips the workshop at the first launch; nothing is ever sent.
api.MapPost("/identity/skip", () =>
{
    reporting.SkipReporting();
    return Results.Ok(reporting.GetStatus());
});

api.MapPut("/reporting/settings", (ReportingSettingsUpdate update) => ApplyReportingSettingsAsync(update, reporting, settingsStore: app.Services.GetRequiredService<AzureOpenAISettingsStore>()));

api.MapGet("/help", () => Results.Json(reporting.Help));

api.MapPost("/help", async (HelpRequest request, LabCatalog catalog) =>
{
    if (!reporting.Enabled)
    {
        return Results.Conflict(new { error = "disabled" });
    }

    if (reporting.GetStatus().NeedsIdentity)
    {
        return Results.Conflict(new { error = "noIdentity" });
    }

    string? labId = string.IsNullOrWhiteSpace(request.LabId) ? null : catalog.Find(request.LabId)?.Id;
    if (request.LabId is { Length: > 0 } && labId is null)
    {
        return Results.BadRequest(new { errors = new Dictionary<string, string> { ["labId"] = "unknown" } });
    }

    if (request.Message is { Length: > 500 })
    {
        return Results.BadRequest(new { errors = new Dictionary<string, string> { ["message"] = "tooLong" } });
    }

    if (reporting.Help is { IsActive: true } active)
    {
        return Results.Conflict(new { error = "active", help = active });
    }

    HelpState help = await reporting.RequestHelpAsync(labId, request.Message);
    return Results.Accepted("/api/help", help);
});

api.MapPost("/help/cancel", async () =>
    reporting.Help is { IsActive: true } ? Results.Ok(await reporting.CancelHelpAsync()) : Results.NotFound());

api.MapPost("/help/dismiss", () =>
{
    reporting.DismissHelp();
    return Results.NoContent();
});

app.Lifetime.ApplicationStarted.Register(() =>
    app.Logger.LogInformation("Lab dashboard running on http://127.0.0.1:{Port} (Ctrl+C to stop)", options.Port));

app.Run();
return 0;

static async Task<IResult> ApplyReportingSettingsAsync(ReportingSettingsUpdate update, ReportingClient reporting, AzureOpenAISettingsStore settingsStore)
{
    try
    {
        IReadOnlyDictionary<string, string> errors = await reporting.ApplySettingsAsync(update);
        return errors.Count > 0 ? Results.BadRequest(new { errors }) : Results.Ok(reporting.GetStatus());
    }
    catch (AzureOpenAISettingsStoreException ex)
    {
        // The workshop key lives in the same user-secrets file as the Azure OpenAI settings.
        return SettingsStoreError(ex, settingsStore);
    }
}

// The reporting keeps sending only the authentication mode of the LLM: an embedding change does not report anything.
static IResult ModelSettingsChanged(ModelKind kind, AzureOpenAISettingsStore settings, ReportingClient reporting)
{
    if (kind == ModelKind.Chat)
    {
        reporting.ReportSettingsChanged(settings.GetStatus());
    }

    return Results.Ok(settings.GetModelStatus());
}

// The kinds ticked before Unlock: one or two distinct values among "chat" and "embedding".
static IReadOnlyList<ModelKind>? ParseKinds(IReadOnlyList<string>? values)
{
    if (values is not { Count: 1 or 2 })
    {
        return null;
    }

    List<ModelKind> kinds = [];
    foreach (string value in values)
    {
        if (!AzureOpenAISettingsStore.TryParseKind(value, out ModelKind kind) || kinds.Contains(kind))
        {
            return null;
        }

        kinds.Add(kind);
    }

    return kinds;
}

// The store refuses to overwrite a file it cannot parse: the user fixes or deletes it (the path is shown in the form).
static IResult SettingsStoreError(AzureOpenAISettingsStoreException error, AzureOpenAISettingsStore settings) =>
    Results.Json(
        new { error = error.Code, file = settings.SecretsFile },
        statusCode: error.Code == "storeInvalid" ? StatusCodes.Status409Conflict : StatusCodes.Status500InternalServerError);

static IResult InputResponse(LabRun run, Func<RunInput, InputResult> action)
{
    if (!run.Lab.Interactive)
    {
        return Results.Conflict(new { error = "notInteractive" });
    }

    // The input exists once the run has started; until then (and during the build) the program cannot read.
    InputResult result = run.Input is { } input ? action(input) : InputResult.NotAccepting;
    return result switch
    {
        InputResult.Accepted => Results.Accepted(),
        InputResult.Closed => Results.Conflict(new { error = "closed" }),
        InputResult.TooManyPending => Results.Conflict(new { error = "tooManyPending" }),
        _ => Results.Conflict(new { error = "notAccepting" }),
    };
}

static async IAsyncEnumerable<SseItem<RunEvent>> ToSse(IAsyncEnumerable<RunEvent> events)
{
    await foreach (RunEvent runEvent in events)
    {
        yield return new SseItem<RunEvent>(runEvent, runEvent.Type);
    }
}

static IReadOnlyList<string> PackageReferences(string projectFile) =>
    XDocument.Load(projectFile)
        .Descendants("PackageReference")
        .Select(p => $"{p.Attribute("Include")?.Value} {p.Attribute("Version")?.Value}".Trim())
        .ToList();

// The solution files that differ from the exercise (only C# sources): what the developer is expected to write.
static IReadOnlyList<SourceFile> SolutionFiles(LabCatalog catalog, LabDefinition lab)
{
    string startDirectory = Path.GetDirectoryName(catalog.ProjectPath(lab, RunTarget.Start))!;
    string solutionDirectory = Path.GetDirectoryName(catalog.ProjectPath(lab, RunTarget.Solution))!;

    return Directory.EnumerateFiles(solutionDirectory, "*.cs", SearchOption.AllDirectories)
        .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                       !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
        .Select(file => (file, relative: Path.GetRelativePath(solutionDirectory, file)))
        .Where(f => !File.Exists(Path.Combine(startDirectory, f.relative)) ||
                    File.ReadAllText(Path.Combine(startDirectory, f.relative)) != File.ReadAllText(f.file))
        .Select(f => new SourceFile(f.relative, File.ReadAllText(f.file)))
        .ToList();
}

/// <summary>A run of the exercise or the solution; <see cref="Scenarios"/>: the scenario numbers to run (null: the implemented ones).</summary>
internal sealed record RunRequest(RunTarget Target, IReadOnlyList<int>? Scenarios = null);

internal sealed record InputRequest(string? Text);

internal sealed record HelpRequest(string? LabId, string? Message);

internal sealed record TrainerUnlockRequest(IReadOnlyList<string>? Kinds);

/// <summary>The access code typed by the candidate: never printed (a record prints every property by default).</summary>
internal sealed record TrainerCodeRequest(string? Code)
{
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.Append($"Code = {(Code is null ? "none" : "set")}");
        return true;
    }
}

internal sealed record SourceFile(string Path, string Content);

internal sealed record LabDetails(
    LabSummary Lab,
    IReadOnlyList<string> Objectives,
    string Folder,
    string ReadmeHtml,
    string ReadmeLanguage,
    IReadOnlyList<string> Packages,
    IReadOnlyList<LabExpectation> Checks,
    IReadOnlyDictionary<string, string> Commands,
    RunRecord? LastRun,
    IReadOnlyList<RunRecord> History);

internal sealed record LabSummary(
    string Id,
    string Number,
    string Track,
    string Title,
    string Summary,
    string Level,
    int TimeoutSeconds,
    bool Interactive,
    int InputIdleTimeoutSeconds,
    LabCompanion? Companion,
    IReadOnlyDictionary<string, LabTranslation> Translations,
    string Progress,
    bool HasCompletedCopy,
    string FolderName,
    RunRecord? LastStartRun,
    RunRecord? LastSolutionRun,
    string? ActiveRunId)
{
    public static LabSummary From(LabDefinition lab, IReadOnlyList<RunRecord> runs, LabRun? activeRun, bool hasCompletedCopy)
    {
        RunRecord? lastStart = runs.FirstOrDefault(r => r.Target == RunTarget.Start);
        RunRecord? lastSolution = runs.FirstOrDefault(r => r.Target == RunTarget.Solution);

        // Progress of the exercise itself (Start project): completed once every scenario passed (ExerciseProgress).
        string progress = ExerciseProgress.CompletingRun(runs) is not null ? "completed"
            : lastStart is not null ? "inProgress"
            : "notStarted";

        return new LabSummary(
            lab.Id, lab.Number, lab.Track, lab.Title, lab.Summary, lab.Level, lab.TimeoutSeconds,
            lab.Interactive, lab.InputIdleTimeoutSeconds, lab.Companion, lab.Translations, progress, hasCompletedCopy,
            LabResetService.FolderName(lab),
            lastStart is null ? null : lastStart with { Log = [] },
            lastSolution is null ? null : lastSolution with { Log = [] },
            activeRun?.Lab.Id == lab.Id ? activeRun.Id : null);
    }
}
