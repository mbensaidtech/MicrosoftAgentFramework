using System.Net;
using System.Net.ServerSentEvents;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using LabDashboard;
using LabDashboard.Catalog;
using LabDashboard.Execution;
using LabDashboard.History;
using LabDashboard.Reporting;
using LabDashboard.Settings;
using Markdig;
using Microsoft.Extensions.Configuration.Json;

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
builder.Services.AddSingleton(options);
builder.Services.AddSingleton(LabCatalog.Load(
    Path.Combine(builder.Environment.ContentRootPath, "labs.json"),
    Path.Combine(builder.Environment.ContentRootPath, options.RepositoryRoot)));
builder.Services.AddSingleton<RunHistoryStore>();
builder.Services.AddSingleton(services => AzureOpenAISettingsStore.ForCatalog(services.GetRequiredService<LabCatalog>()));
builder.Services.AddSingleton<LabRunner>();

// Reporting to the trainer's admin dashboard: off until the developer joins a workshop from the welcome dialog or the settings (no network call before that).
builder.Services.AddSingleton(services => IdentityStore.ForOptions(options, services.GetRequiredService<IHostEnvironment>(), services.GetRequiredService<ILogger<IdentityStore>>()));
builder.Services.AddSingleton(services => new ReportingOutbox(
    Path.Combine(Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, options.DataDirectory)), "outbox.json"),
    services.GetRequiredService<ILogger<ReportingOutbox>>()));
builder.Services.AddSingleton<ReportingClient>();
builder.Services.AddHostedService(services => services.GetRequiredService<ReportingClient>());

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

api.MapGet("/labs", (LabCatalog catalog, RunHistoryStore history, LabRunner runner) =>
    catalog.Labs.Select(lab => LabSummary.From(lab, history.ForLab(lab.Id), runner.ActiveRun)));

api.MapGet("/labs/{id}", (string id, string? lang, LabCatalog catalog, RunHistoryStore history, LabRunner runner) =>
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
        LabSummary.From(lab, runs, runner.ActiveRun),
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
        },
        runs.FirstOrDefault(),
        runs.Select(r => r with { Log = [] }).ToList()));
});

api.MapGet("/labs/{id}/solution", (string id, LabCatalog catalog) =>
{
    if (catalog.Find(id) is not { } lab)
    {
        return Results.NotFound();
    }

    reporting.Report("solution.viewed", lab.Id, null);
    return Results.Ok(SolutionFiles(catalog, lab));
});

api.MapPost("/labs/{id}/runs", (string id, RunRequest request, LabCatalog catalog, LabRunner runner) =>
{
    if (catalog.Find(id) is not { } lab)
    {
        return Results.NotFound();
    }

    return runner.TryStart(lab, request.Target, out LabRun run)
        ? Results.Accepted($"/api/runs/{run.Id}/events", new { runId = run.Id })
        : Results.Conflict(new { error = $"A run is already in progress ({run.Lab.Title}, {run.Target}).", runId = run.Id });
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

// Reporting to the admin dashboard: identity, status and help requests. The workshop key and the dev token are never returned.
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

internal sealed record RunRequest(RunTarget Target);

internal sealed record InputRequest(string? Text);

internal sealed record HelpRequest(string? LabId, string? Message);

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
    RunRecord? LastStartRun,
    RunRecord? LastSolutionRun,
    string? ActiveRunId)
{
    public static LabSummary From(LabDefinition lab, IReadOnlyList<RunRecord> runs, LabRun? activeRun)
    {
        RunRecord? lastStart = runs.FirstOrDefault(r => r.Target == RunTarget.Start);
        RunRecord? lastSolution = runs.FirstOrDefault(r => r.Target == RunTarget.Solution);

        // Progress of the exercise itself (Start project): completed once it passed all the checks.
        string progress = runs.Any(r => r.Target == RunTarget.Start && r.Status == RunStatus.Passed) ? "completed"
            : lastStart is not null ? "inProgress"
            : "notStarted";

        return new LabSummary(
            lab.Id, lab.Number, lab.Track, lab.Title, lab.Summary, lab.Level, lab.TimeoutSeconds,
            lab.Interactive, lab.InputIdleTimeoutSeconds, lab.Companion, lab.Translations, progress,
            lastStart is null ? null : lastStart with { Log = [] },
            lastSolution is null ? null : lastSolution with { Log = [] },
            activeRun?.Lab.Id == lab.Id ? activeRun.Id : null);
    }
}
