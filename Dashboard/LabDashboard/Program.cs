using System.Net;
using System.Net.ServerSentEvents;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using LabDashboard;
using LabDashboard.Catalog;
using LabDashboard.Execution;
using LabDashboard.History;
using LabDashboard.Settings;
using Markdig;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

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

WebApplication app = builder.Build();

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
    catalog.Find(id) is { } lab ? Results.Ok(SolutionFiles(catalog, lab)) : Results.NotFound());

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
        return errors.Count > 0 ? Results.BadRequest(new { errors }) : Results.Ok(settings.GetStatus());
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
        return Results.Ok(settings.GetStatus());
    }
    catch (AzureOpenAISettingsStoreException ex)
    {
        return SettingsStoreError(ex, settings);
    }
});

app.Lifetime.ApplicationStarted.Register(() =>
    app.Logger.LogInformation("Lab dashboard running on http://127.0.0.1:{Port} (Ctrl+C to stop)", options.Port));

app.Run();

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
