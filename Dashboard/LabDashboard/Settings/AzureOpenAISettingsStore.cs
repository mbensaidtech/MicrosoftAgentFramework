using System.Collections;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using LabDashboard.Catalog;
using Microsoft.Extensions.Configuration.UserSecrets;

namespace LabDashboard.Settings;

/// <summary>Where the effective value of a setting comes from, from the highest to the lowest priority.</summary>
public enum SettingSource
{
    /// <summary>An <c>AzureOpenAI__*</c> environment variable of the dashboard (inherited by the labs): it wins over the dashboard value.</summary>
    Environment,

    /// <summary>The shared user-secrets file written by the dashboard (or by <c>dotnet user-secrets set</c>).</summary>
    Dashboard,

    /// <summary>The <c>appsettings.json</c> of the labs.</summary>
    LabDefault,

    Missing
}

public sealed record SettingStatus(string? DashboardValue, string? EffectiveValue, SettingSource Source, string EnvironmentVariable);

/// <summary>Status of the API key. It never carries the key itself: <see cref="Hint"/> is only the last 4 characters of the key in use.</summary>
public sealed record ApiKeyStatus(bool Stored, SettingSource Source, bool EnvironmentValueEmpty, string EnvironmentVariable, string? Hint = null);

public sealed record AzureOpenAISettingsStatus(
    string SecretsFile,
    string UserSecretsId,
    string? StoreError,
    SettingStatus Endpoint,
    SettingStatus ChatDeploymentName,
    ApiKeyStatus ApiKey,
    string Authentication,
    bool Configured);

/// <summary>Values sent by the settings form. Empty Endpoint / ChatDeploymentName remove the value; an empty ApiKey keeps the stored key.</summary>
public sealed record AzureOpenAISettingsUpdate(string? Endpoint, string? ChatDeploymentName, string? ApiKey)
{
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        // Never print the key (a record prints every property by default).
        builder.Append($"Endpoint = {Endpoint}, ChatDeploymentName = {ChatDeploymentName}, ApiKey = {(ApiKey is null ? "none" : "set")}");
        return true;
    }
}

public sealed class AzureOpenAISettingsStoreException(string code, string message, Exception? inner = null) : Exception(message, inner)
{
    /// <summary><c>storeInvalid</c> (the file is not a JSON object), <c>storeReadFailed</c> or <c>storeWriteFailed</c>.</summary>
    public string Code { get; } = code;
}

/// <summary>
/// Reads and writes the Azure OpenAI settings of the labs in the shared .NET user-secrets file
/// (the <c>UserSecretsId</c> declared by the migrated labs). The labs read this file with <c>AddUserSecrets</c>,
/// so a value saved here is used from the dashboard and from <c>dotnet run</c>. Nothing is written in the repository.
/// </summary>
public sealed partial class AzureOpenAISettingsStore
{
    public const string Section = "AzureOpenAI";
    public const string EndpointName = "Endpoint";
    public const string DeploymentName = "ChatDeploymentName";
    public const string ApiKeyName = "APIKey";

    private static readonly JsonDocumentOptions ReadOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Func<IReadOnlyDictionary<string, string>> _environment;
    private readonly IReadOnlyList<string> _labSettingsFiles;

    public AzureOpenAISettingsStore(
        string secretsFile,
        string userSecretsId,
        IReadOnlyList<string> labSettingsFiles,
        Func<IReadOnlyDictionary<string, string>>? environment = null)
    {
        SecretsFile = secretsFile;
        UserSecretsId = userSecretsId;
        _labSettingsFiles = labSettingsFiles;
        _environment = environment ?? ReadProcessEnvironment;
    }

    public string SecretsFile { get; }

    public string UserSecretsId { get; }

    /// <summary>Store of the user-secrets id shared by every lab of the catalog.</summary>
    public static AzureOpenAISettingsStore ForCatalog(LabCatalog catalog)
    {
        string[] projects = catalog.Labs
            .SelectMany(lab => new[] { catalog.ProjectPath(lab, RunTarget.Start), catalog.ProjectPath(lab, RunTarget.Solution) })
            .ToArray();

        string?[] ids = projects
            .Select(project => XDocument.Load(project).Descendants("UserSecretsId").FirstOrDefault()?.Value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (ids is not [{ Length: > 0 } id])
        {
            throw new InvalidOperationException(
                "Every lab of labs.json must declare the same <UserSecretsId> in its Start and Solution projects " +
                $"(found: {string.Join(", ", ids.Select(i => i ?? "none"))}).");
        }

        string[] labSettings = projects
            .Select(project => Path.Combine(Path.GetDirectoryName(project)!, "appsettings.json"))
            .Where(File.Exists)
            .ToArray();

        return new AzureOpenAISettingsStore(PathHelper.GetSecretsPathFromSecretsId(id), id, labSettings);
    }

    public static string EnvironmentVariableName(string name) => $"{Section}__{name}";

    public AzureOpenAISettingsStatus GetStatus()
    {
        string? storeError = null;
        JsonObject secrets;
        try
        {
            secrets = ReadSecrets();
        }
        catch (AzureOpenAISettingsStoreException ex)
        {
            storeError = ex.Code;
            secrets = [];
        }

        IReadOnlyDictionary<string, string> environment = _environment();
        ActiveModelStatus llm = ResolveActiveChat(secrets, environment);

        return new AzureOpenAISettingsStatus(
            SecretsFile,
            UserSecretsId,
            storeError,
            llm.Endpoint,
            llm.Deployment,
            llm.ApiKey,
            llm.Authentication,
            // An empty environment variable also hides the other sources: the lab then reports the value as not configured.
            llm.Configured);
    }

    /// <summary>
    /// Validates and saves the custom LLM profile (the settings form of the LLM); when the LLM is in custom mode, the labs use it at once.
    /// Returns the validation errors (field → code), empty when saved.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> SaveAsync(AzureOpenAISettingsUpdate update)
    {
        IReadOnlyDictionary<string, string> errors = await SaveCustomProfileAsync(
            ModelKind.Chat,
            new ModelProfileUpdate(AzureOpenAIProvider, update.Endpoint, update.ChatDeploymentName, update.ApiKey));

        // Field names of this form.
        return errors.ToDictionary(error => error.Key == "deployment" ? "chatDeploymentName" : error.Key, error => error.Value);
    }

    /// <summary>Removes the key of the custom LLM profile (Microsoft Entra ID); applied at once when the LLM is in custom mode.</summary>
    public Task RemoveApiKeyAsync() => RemoveCustomApiKeyAsync(ModelKind.Chat);

    /// <summary>
    /// Reads another entry of the shared file (flat <c>"Section:Name"</c> or nested spelling), e.g. the workshop key of the reporting.
    /// Null when absent or when the file is unreadable.
    /// </summary>
    public string? GetEntry(string section, string name)
    {
        try
        {
            return Get(ReadSecrets(), section, name) is { } value && !string.IsNullOrWhiteSpace(value) ? value : null;
        }
        catch (AzureOpenAISettingsStoreException)
        {
            return null;
        }
    }

    /// <summary>Writes (or removes, with null) another entry of the shared file, keeping everything else.</summary>
    public Task SetEntryAsync(string section, string name, string? value) => UpdateAsync(secrets => Set(secrets, section, name, value));

    /// <summary>Writes (or removes, with null) several entries of one section in a single write: all of them, or none when the file cannot be read.</summary>
    public Task SetEntriesAsync(string section, IReadOnlyDictionary<string, string?> entries) => UpdateAsync(secrets =>
    {
        foreach ((string name, string? value) in entries)
        {
            Set(secrets, section, name, value);
        }
    });

    /// <summary>
    /// Every API key to mask in the run output: the active keys (LLM and embedding), the key of every profile (custom and trainer, both kinds)
    /// and the <c>AzureOpenAI__APIKey</c> / <c>AzureOpenAI__EmbeddingAPIKey</c> environment values.
    /// </summary>
    public IReadOnlyList<string> SecretValues()
    {
        List<string?> values = [];
        try
        {
            JsonObject secrets = ReadSecrets();
            values.Add(Get(secrets, ApiKeyName));
            values.Add(Get(secrets, EmbeddingApiKeyName));
            foreach (ModelKind kind in Kinds)
            {
                foreach (ModelMode mode in Modes)
                {
                    // Read as stored (not through the profile), so that a key left by a hand edit is masked too.
                    values.Add(Get(secrets, ProfileSection(kind, mode), ProfileApiKeyEntry));
                }
            }
        }
        catch (AzureOpenAISettingsStoreException)
        {
            // An unreadable file gives no key to the labs either.
        }

        IReadOnlyDictionary<string, string> environment = _environment();
        foreach (string name in new[] { ApiKeyName, EmbeddingApiKeyName })
        {
            if (TryGetEnvironment(environment, name, out string? environmentKey) && !string.IsNullOrWhiteSpace(environmentKey))
            {
                values.Add(environmentKey.Trim());
            }
        }

        return values.OfType<string>().Where(value => value.Length > 0).Distinct(StringComparer.Ordinal).ToList();
    }

    internal static string? ValidateEndpoint(string endpoint)
    {
        if (endpoint.Contains("YOUR-", StringComparison.OrdinalIgnoreCase))
        {
            return "placeholder";
        }

        return Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri) && uri.Scheme == Uri.UriSchemeHttps
            && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) && string.IsNullOrEmpty(uri.UserInfo)
            ? null
            : "invalid";
    }

    internal static string? ValidateDeployment(string deployment) =>
        deployment.Contains("YOUR-", StringComparison.OrdinalIgnoreCase) ? "placeholder"
        : DeploymentPattern().IsMatch(deployment) ? null
        : "invalid";

    /// <summary>The last 4 characters of a key, enough to recognise it; nothing for a key too short to hide the rest.</summary>
    internal static string? KeyHint(string? key) =>
        key?.Trim() is { Length: >= Execution.SecretRedactor.MinimumSecretLength } trimmed ? trimmed[^4..] : null;

    internal static string? ValidateApiKey(string apiKey) =>
        apiKey.Any(char.IsWhiteSpace) ? "invalid"
        : apiKey.Length < Execution.SecretRedactor.MinimumSecretLength ? "tooShort"
        : null;

    // Azure OpenAI deployment names: letters, digits, '-', '_' and '.', up to 64 characters.
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$")]
    private static partial Regex DeploymentPattern();

    private SettingStatus Resolve(string name, JsonObject secrets, IReadOnlyDictionary<string, string> environment)
    {
        string? stored = Get(secrets, name);
        string variable = EnvironmentVariableName(name);

        if (TryGetEnvironment(environment, name, out string? value))
        {
            return new SettingStatus(stored, value, SettingSource.Environment, variable);
        }

        if (!string.IsNullOrWhiteSpace(stored))
        {
            return new SettingStatus(stored, stored, SettingSource.Dashboard, variable);
        }

        string? labDefault = LabDefault(name);
        return labDefault is null
            ? new SettingStatus(null, null, SettingSource.Missing, variable)
            : new SettingStatus(null, labDefault, SettingSource.LabDefault, variable);
    }

    private ApiKeyStatus ResolveApiKey(string name, JsonObject secrets, IReadOnlyDictionary<string, string> environment)
    {
        string? storedKey = Get(secrets, name);
        bool keyInEnvironment = TryGetEnvironment(environment, name, out string? environmentKey);
        return new ApiKeyStatus(
            Stored: !string.IsNullOrWhiteSpace(storedKey),
            Source: keyInEnvironment ? SettingSource.Environment
                : !string.IsNullOrWhiteSpace(storedKey) ? SettingSource.Dashboard
                : SettingSource.Missing,
            EnvironmentValueEmpty: keyInEnvironment && string.IsNullOrWhiteSpace(environmentKey),
            EnvironmentVariable: EnvironmentVariableName(name),
            Hint: KeyHint(keyInEnvironment ? environmentKey : storedKey));
    }

    /// <summary>Same rule as the labs: a non-empty key in effect means API key authentication, otherwise Microsoft Entra ID.</summary>
    private static bool UsesApiKey(ApiKeyStatus apiKey) =>
        apiKey.Source == SettingSource.Environment ? !apiKey.EnvironmentValueEmpty : apiKey.Stored;

    /// <summary>The first real (non-placeholder) value found in the appsettings.json of the labs.</summary>
    private string? LabDefault(string name)
    {
        foreach (string file in _labSettingsFiles)
        {
            try
            {
                if (JsonNode.Parse(File.ReadAllText(file), documentOptions: ReadOptions) is JsonObject root
                    && Get(root, name) is { } value
                    && !string.IsNullOrWhiteSpace(value)
                    && !value.Contains("YOUR-", StringComparison.OrdinalIgnoreCase))
                {
                    return value;
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                // A broken lab file is reported by the lab itself when it runs.
            }
        }

        return null;
    }

    private Task UpdateAsync(Action<JsonObject> change) => TryUpdateAsync(secrets =>
    {
        change(secrets);
        return true;
    });

    /// <summary>Reads, changes and writes the file under the lock; nothing is written when <paramref name="change"/> returns false.</summary>
    private async Task<bool> TryUpdateAsync(Func<JsonObject, bool> change)
    {
        await _gate.WaitAsync();
        try
        {
            JsonObject secrets = ReadSecrets();
            if (!change(secrets))
            {
                return false;
            }

            Write(secrets);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private JsonObject ReadSecrets()
    {
        if (!File.Exists(SecretsFile))
        {
            return [];
        }

        try
        {
            string json = File.ReadAllText(SecretsFile);
            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }

            return JsonNode.Parse(json, documentOptions: ReadOptions) as JsonObject
                ?? throw new AzureOpenAISettingsStoreException("storeInvalid", $"'{SecretsFile}' does not contain a JSON object.");
        }
        catch (JsonException ex)
        {
            throw new AzureOpenAISettingsStoreException("storeInvalid", $"'{SecretsFile}' is not valid JSON.", ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new AzureOpenAISettingsStoreException("storeReadFailed", $"'{SecretsFile}' cannot be read.", ex);
        }
    }

    /// <summary>Atomic write (temporary file + move); the file and its folder are private to the user on macOS/Linux.</summary>
    private void Write(JsonObject secrets)
    {
        string directory = Path.GetDirectoryName(SecretsFile)!;
        string temporary = Path.Combine(directory, $".secrets.{Guid.NewGuid():N}.tmp");
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Directory.CreateDirectory(directory);
            }
            else
            {
                Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            FileStreamOptions options = new() { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            using (FileStream stream = new(temporary, options))
            {
                JsonSerializer.Serialize(stream, secrets, WriteOptions);
            }

            File.Move(temporary, SecretsFile, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDelete(temporary);
            throw new AzureOpenAISettingsStoreException("storeWriteFailed", $"'{SecretsFile}' cannot be written.", ex);
        }
    }

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: the original error is the one reported.
        }
    }

    private static string? Get(JsonObject root, string name) => Get(root, Section, name);

    private static void Set(JsonObject root, string name, string? value) => Set(root, Section, name, value);

    /// <summary>
    /// Reads a setting written flat (<c>"AzureOpenAI:Endpoint"</c>, as <c>dotnet user-secrets set</c> does) or nested.
    /// <paramref name="sectionPath"/> may itself contain ':' (e.g. <c>Dashboard:Reporting</c>): nested objects are walked level by level.
    /// </summary>
    private static string? Get(JsonObject root, string sectionPath, string name)
    {
        foreach ((string key, JsonNode? node) in root)
        {
            if (key.Equals($"{sectionPath}:{name}", StringComparison.OrdinalIgnoreCase))
            {
                return AsString(node);
            }
        }

        JsonObject? section = FindSection(root, sectionPath);
        if (section is not null)
        {
            foreach ((string childKey, JsonNode? child) in section)
            {
                if (childKey.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return AsString(child);
                }
            }
        }

        return null;
    }

    /// <summary>Removes every spelling of the setting, then writes it flat (null removes it). Other entries are kept.</summary>
    private static void Set(JsonObject root, string sectionPath, string name, string? value)
    {
        foreach (string key in root.Select(p => p.Key).Where(k => k.Equals($"{sectionPath}:{name}", StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            root.Remove(key);
        }

        if (FindSection(root, sectionPath) is { } section)
        {
            foreach (string key in section.Select(p => p.Key).Where(k => k.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                section.Remove(key);
            }

            PruneEmptySections(root, sectionPath.Split(':'));
        }

        if (value is not null)
        {
            root[$"{sectionPath}:{name}"] = value;
        }
    }

    /// <summary>The nested object at <c>a:b:c</c> (each level matched case-insensitively), or null.</summary>
    private static JsonObject? FindSection(JsonObject root, string sectionPath)
    {
        JsonObject current = root;
        foreach (string part in sectionPath.Split(':'))
        {
            JsonObject? next = null;
            foreach ((string key, JsonNode? node) in current)
            {
                if (key.Equals(part, StringComparison.OrdinalIgnoreCase) && node is JsonObject child)
                {
                    next = child;
                    break;
                }
            }

            if (next is null)
            {
                return null;
            }

            current = next;
        }

        return current;
    }

    /// <summary>Removes the objects of the path that became empty (deepest first); foreign entries are never touched.</summary>
    private static void PruneEmptySections(JsonObject parent, string[] parts)
    {
        if (parts.Length == 0)
        {
            return;
        }

        string first = parts[0];
        foreach (string key in parent.Select(p => p.Key).Where(k => k.Equals(first, StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            if (parent[key] is JsonObject child)
            {
                PruneEmptySections(child, parts[1..]);
                if (child.Count == 0)
                {
                    parent.Remove(key);
                }
            }
        }
    }

    private static string? AsString(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue(out string? text) ? text : node?.ToJsonString();

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Environment variables are matched case-insensitively, as the .NET configuration does.</summary>
    private static bool TryGetEnvironment(IReadOnlyDictionary<string, string> environment, string name, out string? value)
    {
        string variable = EnvironmentVariableName(name);
        foreach ((string key, string candidate) in environment)
        {
            if (key.Equals(variable, StringComparison.OrdinalIgnoreCase))
            {
                value = candidate;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static IReadOnlyDictionary<string, string> ReadProcessEnvironment()
    {
        Dictionary<string, string> variables = new(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key && key.StartsWith($"{Section}__", StringComparison.OrdinalIgnoreCase))
            {
                variables[key] = entry.Value as string ?? "";
            }
        }

        return variables;
    }
}
