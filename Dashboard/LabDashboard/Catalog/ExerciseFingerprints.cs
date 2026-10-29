using CommonUtilities;

namespace LabDashboard.Catalog;

/// <summary>
/// Lab author command (<c>dotnet run -- exercise-fingerprints</c>): writes, for every lab of the catalog, the fingerprints of its exercise
/// as delivered (<see cref="LabScenarios.FingerprintFile"/>), read by the labs and the dashboard to tell a scenario the learner implemented
/// from a delivered one. Run it after changing a Start or a Solution, on the exercises as they will be delivered (the catalog test checks it).
/// </summary>
public static class ExerciseFingerprints
{
    public const string Name = "exercise-fingerprints";

    public static string FilePath(LabCatalog catalog, LabDefinition lab) => Path.Combine(catalog.LabDirectory(lab), LabScenarios.FingerprintFile);

    /// <summary>The content of the file for the current Start and Solution of the lab.</summary>
    public static string Generate(LabCatalog catalog, LabDefinition lab) => LabScenarios.WriteDelivered(LabScenarios.CreateDelivered(
        File.ReadAllText(catalog.ProgramPath(lab, RunTarget.Start)),
        File.ReadAllText(catalog.ProgramPath(lab, RunTarget.Solution))));

    public static int Run(LabCatalog catalog, TextWriter output)
    {
        foreach (LabDefinition lab in catalog.Labs)
        {
            string path = FilePath(catalog, lab);
            string content = Generate(catalog, lab);
            bool unchanged = File.Exists(path) && File.ReadAllText(path) == content;
            if (!unchanged)
            {
                File.WriteAllText(path, content);
            }

            output.WriteLine($"{(unchanged ? "unchanged" : "written  ")}  {Path.GetRelativePath(catalog.RepositoryRoot, path)}");
        }

        return 0;
    }
}
