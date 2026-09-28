using LabDashboard.Catalog;

namespace LabDashboard.Tests;

public class LabCatalogTests
{
    private static string DashboardProjectDirectory =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabDashboard"));

    [Fact]
    public void Loads_the_real_catalog_and_resolves_the_projects_of_every_lab()
    {
        LabCatalog catalog = LoadRealCatalog();

        Assert.Equal(["azureopenai-lab01", "azureopenai-lab02", "azureopenai-lab03", "azureopenai-lab04", "azureopenai-lab05", "azureopenai-lab06-server", "azureopenai-lab06-client"],
            catalog.Labs.Select(lab => lab.Id));
        foreach (LabDefinition lab in catalog.Labs)
        {
            Assert.True(File.Exists(catalog.ProjectPath(lab, RunTarget.Start)));
            Assert.True(File.Exists(catalog.ProjectPath(lab, RunTarget.Solution)));
            Assert.True(File.Exists(catalog.ReadmePath(lab)));
            if (lab.Companion is not null)
            {
                Assert.True(File.Exists(catalog.CompanionProjectPath(lab)));
            }
        }
    }

    [Fact]
    public void The_two_lab06_labs_run_each_other_as_companions_on_a_dedicated_port()
    {
        LabCatalog catalog = LoadRealCatalog();
        LabDefinition server = catalog.Find("azureopenai-lab06-server")!;
        LabDefinition client = catalog.Find("azureopenai-lab06-client")!;

        Assert.Equal(CompanionRole.Client, server.Companion!.Role);
        Assert.Equal(CompanionRole.Server, client.Companion!.Role);
        Assert.Equal(catalog.ProjectPath(client, RunTarget.Solution), catalog.CompanionProjectPath(server));
        Assert.Equal(catalog.ProjectPath(server, RunTarget.Solution), catalog.CompanionProjectPath(client));

        // Both processes of a run get the same addresses: the server listens where the client calls.
        foreach (LabCompanion companion in new[] { server.Companion, client.Companion })
        {
            string baseUrl = companion.Environment["A2AServer__BaseUrl"];
            Assert.NotEqual("http://localhost:5000", baseUrl);
            Assert.Equal($"{baseUrl}/a2a/authAgent", companion.Environment["RemoteAgents__AuthAgent__Url"]);
            Assert.Equal($"{baseUrl}/a2a/customerToneAgent", companion.Environment["RemoteAgents__CustomerToneAgent__Url"]);
            Assert.Matches(companion.ReadyPattern, $"      Now listening on: {baseUrl}");
        }
    }

    [Theory]
    [InlineData(""" "role": "server", "project": "../outside/x.csproj", "readyPattern": "ready" """, "outside the repository")]
    [InlineData(""" "role": "server", "project": "Lab/missing.csproj", "readyPattern": "ready" """, "not found")]
    [InlineData(""" "role": "server", "project": "Lab/a.csproj", "readyPattern": "(" """, "invalid companion ready pattern")]
    [InlineData(""" "role": "server", "project": "Lab/a.csproj", "readyPattern": "ready", "environment": { "BAD NAME": "x" } """, "environment variable name")]
    [InlineData(""" "role": "server", "project": "Lab/a.csproj", "readyPattern": "ready", "readyTimeoutSeconds": 0 """, "ready timeout")]
    public void Refuses_an_invalid_companion(string companion, string error)
    {
        string root = Directory.CreateTempSubdirectory().FullName;
        Directory.CreateDirectory(Path.Combine(root, "Lab"));
        File.WriteAllText(Path.Combine(root, "Lab", "a.csproj"), "<Project />");
        string catalogFile = Path.Combine(root, "labs.json");
        File.WriteAllText(catalogFile, $$"""
            { "labs": [ { "id": "x", "number": "99", "track": "t", "title": "t", "summary": "s", "level": "l",
                          "path": "Lab", "startProject": "a.csproj", "solutionProject": "a.csproj", "companion": { {{companion}} } } ] }
            """);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => LabCatalog.Load(catalogFile, root));
        Assert.Contains(error, exception.Message);
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void Every_check_of_the_real_catalog_has_a_french_description()
    {
        foreach (LabDefinition lab in LoadRealCatalog().Labs)
        {
            IReadOnlyDictionary<string, string> checks = lab.Translations["fr"].Checks!;
            Assert.All(lab.Expectations, expectation => Assert.True(checks.ContainsKey(expectation.Id), $"{lab.Id}: {expectation.Id}"));
        }
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("../../etc/passwd")]
    [InlineData("FR")]
    [InlineData("french")]
    public void Localized_readme_is_null_unless_a_safe_language_code_has_a_file(string language)
    {
        LabCatalog catalog = LoadRealCatalog();

        // No lab has a README.fr.md yet, and anything that is not a two-letter lowercase code is rejected.
        Assert.All(catalog.Labs, lab => Assert.Null(catalog.LocalizedReadmePath(lab, language)));
    }

    [Fact]
    public void Refuses_a_lab_path_outside_the_repository()
    {
        string root = Directory.CreateTempSubdirectory().FullName;
        string catalogFile = Path.Combine(root, "labs.json");
        File.WriteAllText(catalogFile, """
            { "labs": [ { "id": "x", "number": "99", "track": "t", "title": "t", "summary": "s", "level": "l",
                          "path": "../outside", "startProject": "a.csproj", "solutionProject": "b.csproj" } ] }
            """);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => LabCatalog.Load(catalogFile, root));
        Assert.Contains("outside the repository", error.Message);
    }

    internal static LabCatalog LoadRealCatalog() => LabCatalog.Load(
        Path.Combine(DashboardProjectDirectory, "labs.json"),
        Path.Combine(DashboardProjectDirectory, "..", ".."));
}
