using System.Collections;

namespace LabDashboard.Settings;

/// <summary>The storage the labs use (<c>Storage:Mode</c>), where it comes from, and the modes that can be chosen.</summary>
public sealed record LabStorageStatus(string Mode, SettingSource Source, string EnvironmentVariable, IReadOnlyList<string> Modes);

/// <summary>The mode chosen in the settings.</summary>
public sealed record LabStorageUpdate(string? Mode);

/// <summary>
/// Reads and writes <c>Storage:Mode</c>, the storage of the labs that keep data outside of the application (Lab05 scenario 3, Lab12):
/// MongoDB in Docker, MongoDB started by the lab without Docker, or a JSON file. Saved in the shared user-secrets file, like
/// <c>dotnet user-secrets set "Storage:Mode" ...</c>: the value is used from the dashboard and from <c>dotnet run</c>.
/// </summary>
public sealed class LabStorageSettings(AzureOpenAISettingsStore store, Func<IReadOnlyDictionary<string, string>>? environment = null)
{
    public const string Section = "Storage";
    public const string ModeName = "Mode";
    public const string EnvironmentVariable = $"{Section}__{ModeName}";

    /// <summary>The value of the <c>appsettings.json</c> of the labs: the MongoDB container of the lab.</summary>
    public const string DefaultMode = "MongoDb";

    /// <summary>The modes of the labs (<c>StorageMode</c>), in the order of the settings.</summary>
    public static readonly IReadOnlyList<string> Modes = [DefaultMode, "EmbeddedMongoDb", "JsonFile"];

    private readonly Func<IReadOnlyDictionary<string, string>> _environment = environment ?? ReadProcessEnvironment;

    public LabStorageStatus GetStatus()
    {
        // An environment variable of the dashboard is inherited by the labs, and wins over the user secrets.
        foreach ((string key, string value) in _environment())
        {
            if (key.Equals(EnvironmentVariable, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(value))
            {
                return new LabStorageStatus(Canonical(value) ?? value.Trim(), SettingSource.Environment, EnvironmentVariable, Modes);
            }
        }

        return Canonical(store.GetEntry(Section, ModeName)) is { } stored
            ? new LabStorageStatus(stored, SettingSource.Dashboard, EnvironmentVariable, Modes)
            : new LabStorageStatus(DefaultMode, SettingSource.LabDefault, EnvironmentVariable, Modes);
    }

    /// <summary>Saves the mode; returns <c>"invalid"</c> (nothing written) for an unknown mode.</summary>
    /// <exception cref="AzureOpenAISettingsStoreException">The user-secrets file cannot be read or written.</exception>
    public async Task<string?> SaveAsync(LabStorageUpdate update)
    {
        if (Canonical(update.Mode) is not { } mode)
        {
            return "invalid";
        }

        await store.SetEntryAsync(Section, ModeName, mode);
        return null;
    }

    /// <summary>The spelling of the labs for a mode (the labs read it case-insensitively), or null for an unknown one.</summary>
    private static string? Canonical(string? mode) =>
        Modes.FirstOrDefault(known => known.Equals(mode?.Trim(), StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyDictionary<string, string> ReadProcessEnvironment()
    {
        Dictionary<string, string> variables = new(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key && key.Equals(EnvironmentVariable, StringComparison.OrdinalIgnoreCase))
            {
                variables[key] = entry.Value as string ?? "";
            }
        }

        return variables;
    }
}
