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

        Assert.Equal(["azureopenai-lab01", "azureopenai-lab02", "azureopenai-lab03", "azureopenai-lab04", "azureopenai-lab05"], catalog.Labs.Select(lab => lab.Id));
        foreach (LabDefinition lab in catalog.Labs)
        {
            Assert.True(File.Exists(catalog.ProjectPath(lab, RunTarget.Start)));
            Assert.True(File.Exists(catalog.ProjectPath(lab, RunTarget.Solution)));
            Assert.True(File.Exists(catalog.ReadmePath(lab)));
        }
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
