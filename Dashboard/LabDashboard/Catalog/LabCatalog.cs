using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace LabDashboard.Catalog;

public enum RunTarget
{
    Start,
    Solution
}

/// <summary>A check run against the standard output of a lab run (multiline regular expression).</summary>
public sealed record LabExpectation(string Id, string Description, string Pattern);

/// <summary>Optional translation of the lab texts shown by the dashboard (keyed by language code, e.g. "fr").</summary>
public sealed record LabTranslation
{
    public string? Track { get; init; }
    public string? Title { get; init; }
    public string? Summary { get; init; }
    public string? Level { get; init; }
    public IReadOnlyList<string>? Objectives { get; init; }

    /// <summary>Check descriptions keyed by expectation id.</summary>
    public IReadOnlyDictionary<string, string>? Checks { get; init; }
}

/// <summary>How a companion process takes part in a run (see <see cref="LabCompanion"/>).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CompanionRole>))]
public enum CompanionRole
{
    /// <summary>The companion is a server the lab calls: started (and ready) before the lab, stopped after it.</summary>
    Server,

    /// <summary>The lab is a server: it is started first, the companion runs once the lab is ready, then the lab is stopped.</summary>
    Client
}

/// <summary>
/// A second project run next to the lab, for labs that only make sense in pairs (an A2A client and its server).
/// The companion is always the reference solution of the other lab, built and run with the same commands as a lab.
/// </summary>
public sealed record LabCompanion
{
    public required CompanionRole Role { get; init; }

    /// <summary>The companion project, relative to the repository root.</summary>
    public required string Project { get; init; }

    /// <summary>A line of the server (the companion for <see cref="CompanionRole.Server"/>, the lab for <see cref="CompanionRole.Client"/>) that says it is ready.</summary>
    public required string ReadyPattern { get; init; }

    public int ReadyTimeoutSeconds { get; init; } = 60;

    /// <summary>Environment variables set on both processes of the run (for example a dedicated port), shown in the log.</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();
}

/// <summary>One lab as declared in labs.json. Paths are relative to the repository root.</summary>
public sealed record LabDefinition
{
    public required string Id { get; init; }
    public required string Number { get; init; }
    public required string Track { get; init; }
    public required string Title { get; init; }
    public required string Summary { get; init; }
    public required string Level { get; init; }
    public IReadOnlyList<string> Objectives { get; init; } = [];
    public required string Path { get; init; }
    public required string StartProject { get; init; }
    public required string SolutionProject { get; init; }
    public string Readme { get; init; } = "README.md";
    public int TimeoutSeconds { get; init; } = 180;

    /// <summary>The program reads standard input (<c>Console.ReadLine</c>): the dashboard keeps stdin open and asks the learner.</summary>
    public bool Interactive { get; init; }

    /// <summary>An interactive run whose prompt stays unanswered this long times out. The lab timeout is paused while it waits.</summary>
    public int InputIdleTimeoutSeconds { get; init; } = 600;

    /// <summary>Optional second project of the run (see <see cref="LabCompanion"/>).</summary>
    public LabCompanion? Companion { get; init; }
    public IReadOnlyList<LabExpectation> Expectations { get; init; } = [];
    public IReadOnlyDictionary<string, LabTranslation> Translations { get; init; } = new Dictionary<string, LabTranslation>();

    public string ProjectFor(RunTarget target) => target == RunTarget.Start ? StartProject : SolutionProject;
}

/// <summary>
/// The whitelist of runnable labs. Only the projects declared here can ever be built or run by the dashboard.
/// </summary>
public sealed partial class LabCatalog
{
    private readonly Dictionary<string, LabDefinition> _labs;

    private LabCatalog(string repositoryRoot, IReadOnlyList<LabDefinition> labs)
    {
        RepositoryRoot = repositoryRoot;
        Labs = labs;
        _labs = labs.ToDictionary(lab => lab.Id, StringComparer.OrdinalIgnoreCase);
    }

    public string RepositoryRoot { get; }

    public IReadOnlyList<LabDefinition> Labs { get; }

    public LabDefinition? Find(string id) => _labs.GetValueOrDefault(id);

    public string LabDirectory(LabDefinition lab) => Resolve(lab.Path);

    public string ProjectPath(LabDefinition lab, RunTarget target) => Resolve(System.IO.Path.Combine(lab.Path, lab.ProjectFor(target)));

    public string? CompanionProjectPath(LabDefinition lab) => lab.Companion is null ? null : Resolve(lab.Companion.Project);

    public string ReadmePath(LabDefinition lab) => Resolve(System.IO.Path.Combine(lab.Path, lab.Readme));

    /// <summary>The README in the requested language (e.g. README.fr.md) when it exists, otherwise null.</summary>
    public string? LocalizedReadmePath(LabDefinition lab, string language)
    {
        if (language.Length != 2 || !language.All(char.IsAsciiLetterLower))
        {
            return null;
        }

        string path = Resolve(System.IO.Path.Combine(lab.Path, System.IO.Path.ChangeExtension(lab.Readme, $"{language}.md")));
        return File.Exists(path) ? path : null;
    }

    public static LabCatalog Load(string catalogFile, string repositoryRoot)
    {
        string root = System.IO.Path.GetFullPath(repositoryRoot);
        CatalogFile file = JsonSerializer.Deserialize<CatalogFile>(File.ReadAllText(catalogFile), JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException($"'{catalogFile}' is empty.");

        LabCatalog catalog = new(root, file.Labs);
        foreach (LabDefinition lab in file.Labs)
        {
            catalog.Validate(lab);
        }

        return catalog;
    }

    private void Validate(LabDefinition lab)
    {
        if (lab.TimeoutSeconds <= 0 || lab.InputIdleTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException($"Lab '{lab.Id}': timeouts must be positive.");
        }

        foreach (RunTarget target in Enum.GetValues<RunTarget>())
        {
            string project = ProjectPath(lab, target);
            if (!File.Exists(project))
            {
                throw new InvalidOperationException($"Lab '{lab.Id}': project '{project}' not found.");
            }
        }

        if (lab.Companion is { } companion)
        {
            ValidateCompanion(lab, companion);
        }

        foreach (LabExpectation expectation in lab.Expectations)
        {
            try
            {
                _ = new Regex(expectation.Pattern, RegexOptions.Multiline);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException($"Lab '{lab.Id}': invalid pattern for expectation '{expectation.Id}'.", ex);
            }
        }
    }

    private void ValidateCompanion(LabDefinition lab, LabCompanion companion)
    {
        if (!Enum.IsDefined(companion.Role) || companion.ReadyTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException($"Lab '{lab.Id}': invalid companion role or ready timeout.");
        }

        if (lab.Interactive)
        {
            throw new InvalidOperationException($"Lab '{lab.Id}': an interactive lab cannot have a companion.");
        }

        string project = Resolve(companion.Project);
        if (!project.EndsWith(".csproj", StringComparison.Ordinal) || !File.Exists(project))
        {
            throw new InvalidOperationException($"Lab '{lab.Id}': companion project '{project}' not found.");
        }

        try
        {
            _ = new Regex(companion.ReadyPattern);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException($"Lab '{lab.Id}': invalid companion ready pattern.", ex);
        }

        foreach (string name in companion.Environment.Keys)
        {
            if (!EnvironmentVariableName().IsMatch(name))
            {
                throw new InvalidOperationException($"Lab '{lab.Id}': invalid companion environment variable name '{name}'.");
            }
        }
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex EnvironmentVariableName();

    /// <summary>Resolves a catalog path and refuses anything that escapes the repository root.</summary>
    private string Resolve(string relativePath)
    {
        string fullPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(RepositoryRoot, relativePath));
        string rootWithSeparator = RepositoryRoot.EndsWith(System.IO.Path.DirectorySeparatorChar)
            ? RepositoryRoot
            : RepositoryRoot + System.IO.Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Path '{relativePath}' is outside the repository.");
        }

        return fullPath;
    }

    private sealed record CatalogFile(IReadOnlyList<LabDefinition> Labs);
}
