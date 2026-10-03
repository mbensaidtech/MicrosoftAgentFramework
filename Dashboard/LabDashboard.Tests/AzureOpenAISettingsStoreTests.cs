using System.Text.Json.Nodes;
using LabDashboard.Settings;

namespace LabDashboard.Tests;

/// <summary>Every test works in a temporary folder: the real user-secrets file of the machine is never touched.</summary>
public sealed class AzureOpenAISettingsStoreTests : IDisposable
{
    private const string Key = "0123456789abcdef0123456789abcdef";

    private readonly string _root = Directory.CreateTempSubdirectory("labbench-settings-").FullName;
    private readonly Dictionary<string, string> _environment = new(StringComparer.Ordinal);

    private string SecretsFile => Path.Combine(_root, "usersecrets", "microsoft-agent-framework-learninglabs", "secrets.json");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task Saves_flat_keys_that_dotnet_user_secrets_understands_and_keeps_foreign_entries()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SecretsFile)!);
        File.WriteAllText(SecretsFile, """{ "Other:Key": "x", "Nested": { "Value": 1 } }""");

        IReadOnlyDictionary<string, string> errors = await Store().SaveAsync(new("https://my-resource.openai.azure.com/", "gpt-4o-mini", Key));

        Assert.Empty(errors);
        JsonObject saved = ReadSaved();
        Assert.Equal("https://my-resource.openai.azure.com/", (string?)saved["AzureOpenAI:Endpoint"]);
        Assert.Equal("gpt-4o-mini", (string?)saved["AzureOpenAI:ChatDeploymentName"]);
        Assert.Equal(Key, (string?)saved["AzureOpenAI:APIKey"]);
        Assert.Equal("x", (string?)saved["Other:Key"]);
        Assert.Equal(1, (int?)saved["Nested"]!["Value"]);
    }

    [Fact]
    public async Task Replaces_a_nested_spelling_of_a_setting_instead_of_duplicating_it()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SecretsFile)!);
        File.WriteAllText(SecretsFile, """{ "AzureOpenAI": { "Endpoint": "https://old.openai.azure.com/", "Other": "kept" } }""");

        await Store().SaveAsync(new("https://new.openai.azure.com/", null, null));

        JsonObject saved = ReadSaved();
        Assert.Equal("https://new.openai.azure.com/", (string?)saved["AzureOpenAI:Endpoint"]);
        Assert.Null(saved["AzureOpenAI"]!["Endpoint"]);
        Assert.Equal("kept", (string?)saved["AzureOpenAI"]!["Other"]);
    }

    [Fact]
    public async Task Never_returns_the_api_key_and_keeps_it_when_the_key_field_is_empty()
    {
        AzureOpenAISettingsStore store = Store();
        await store.SaveAsync(new("https://my-resource.openai.azure.com/", "gpt-4o-mini", Key));
        await store.SaveAsync(new("https://my-resource.openai.azure.com/", "gpt-4o-mini", ""));

        AzureOpenAISettingsStatus status = store.GetStatus();
        Assert.True(status.ApiKey.Stored);
        Assert.Equal("apiKey", status.Authentication);
        Assert.DoesNotContain(Key, System.Text.Json.JsonSerializer.Serialize(status));
        Assert.Equal(Key, (string?)ReadSaved()["AzureOpenAI:APIKey"]);
    }

    [Fact]
    public async Task Removing_the_key_switches_to_microsoft_entra_id()
    {
        AzureOpenAISettingsStore store = Store();
        await store.SaveAsync(new("https://my-resource.openai.azure.com/", "gpt-4o-mini", Key));

        await store.RemoveApiKeyAsync();

        AzureOpenAISettingsStatus status = store.GetStatus();
        Assert.False(status.ApiKey.Stored);
        Assert.Equal(SettingSource.Missing, status.ApiKey.Source);
        Assert.Equal("entraId", status.Authentication);
        Assert.Null(ReadSaved()["AzureOpenAI:APIKey"]);
    }

    [Fact]
    public async Task An_empty_endpoint_removes_the_value_so_that_the_lab_default_applies_again()
    {
        string labSettings = LabSettingsFile("""{ "AzureOpenAI": { "Endpoint": "https://lab.openai.azure.com/", "ChatDeploymentName": "YOUR-DEPLOYMENT-NAME" } }""");
        AzureOpenAISettingsStore store = Store(labSettings);
        await store.SaveAsync(new("https://my-resource.openai.azure.com/", "gpt-4o-mini", null));

        await store.SaveAsync(new("", "gpt-4o-mini", null));

        AzureOpenAISettingsStatus status = store.GetStatus();
        Assert.Equal(SettingSource.LabDefault, status.Endpoint.Source);
        Assert.Equal("https://lab.openai.azure.com/", status.Endpoint.EffectiveValue);
        Assert.Null(ReadSaved()["AzureOpenAI:Endpoint"]);
    }

    [Fact]
    public void Placeholders_of_the_labs_count_as_not_configured()
    {
        string labSettings = LabSettingsFile("""{ "AzureOpenAI": { "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/", "ChatDeploymentName": "YOUR-DEPLOYMENT-NAME" } }""");

        AzureOpenAISettingsStatus status = Store(labSettings).GetStatus();

        Assert.Equal(SettingSource.Missing, status.Endpoint.Source);
        Assert.Equal(SettingSource.Missing, status.ChatDeploymentName.Source);
        Assert.False(status.Configured);
        Assert.Equal("entraId", status.Authentication);
    }

    [Fact]
    public async Task Environment_variables_override_the_dashboard_values()
    {
        AzureOpenAISettingsStore store = Store();
        await store.SaveAsync(new("https://my-resource.openai.azure.com/", "gpt-4o-mini", Key));
        _environment["azureopenai__endpoint"] = "https://from-env.openai.azure.com/"; // matched case-insensitively, as by .NET
        _environment["AzureOpenAI__APIKey"] = "";

        AzureOpenAISettingsStatus status = store.GetStatus();

        Assert.Equal(SettingSource.Environment, status.Endpoint.Source);
        Assert.Equal("https://from-env.openai.azure.com/", status.Endpoint.EffectiveValue);
        Assert.Equal("https://my-resource.openai.azure.com/", status.Endpoint.DashboardValue);
        Assert.Equal(SettingSource.Environment, status.ApiKey.Source);
        Assert.True(status.ApiKey.EnvironmentValueEmpty);
        Assert.Equal("entraId", status.Authentication); // an empty variable hides the stored key from the labs
    }

    [Fact]
    public async Task The_status_shows_only_the_last_four_characters_of_the_key_in_use()
    {
        AzureOpenAISettingsStore store = Store();
        await store.SaveAsync(new("https://my-resource.openai.azure.com/", "gpt-4o-mini", Key));
        Assert.Equal(Key[^4..], store.GetStatus().ApiKey.Hint);

        // A key set on the computer is the one the labs use: its last characters are shown, never the key.
        _environment["AzureOpenAI__APIKey"] = "env-key-0123456789-wxyz";
        AzureOpenAISettingsStatus status = store.GetStatus();
        Assert.Equal("wxyz", status.ApiKey.Hint);
        Assert.DoesNotContain("env-key-0123456789", System.Text.Json.JsonSerializer.Serialize(status));

        _environment["AzureOpenAI__APIKey"] = "";
        Assert.Null(store.GetStatus().ApiKey.Hint);
    }

    [Theory]
    [InlineData("http://my-resource.openai.azure.com/", null, null, "endpoint", "invalid")]
    [InlineData("my-resource.openai.azure.com", null, null, "endpoint", "invalid")]
    [InlineData("https://YOUR-RESOURCE.openai.azure.com/", null, null, "endpoint", "placeholder")]
    [InlineData(null, "gpt 4o", null, "chatDeploymentName", "invalid")]
    [InlineData(null, "YOUR-DEPLOYMENT-NAME", null, "chatDeploymentName", "placeholder")]
    [InlineData(null, null, "abc", "apiKey", "tooShort")]
    [InlineData(null, null, "abc def ghi jkl", "apiKey", "invalid")]
    public async Task Rejects_invalid_values_without_touching_the_file(string? endpoint, string? deployment, string? apiKey, string field, string code)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SecretsFile)!);
        File.WriteAllText(SecretsFile, """{ "Other:Key": "x" }""");

        IReadOnlyDictionary<string, string> errors = await Store().SaveAsync(new(endpoint, deployment, apiKey));

        Assert.Equal(code, errors[field]);
        Assert.Equal("""{ "Other:Key": "x" }""", File.ReadAllText(SecretsFile));
    }

    [Fact]
    public async Task Accepts_the_v1_endpoint_as_well()
    {
        Assert.Empty(await Store().SaveAsync(new("https://my-resource.openai.azure.com/openai/v1/", "gpt-4o-mini", null)));
    }

    [Fact]
    public async Task Refuses_to_overwrite_a_file_that_is_not_valid_json()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SecretsFile)!);
        File.WriteAllText(SecretsFile, "{ not json");
        AzureOpenAISettingsStore store = Store();

        AzureOpenAISettingsStoreException error = await Assert.ThrowsAsync<AzureOpenAISettingsStoreException>(
            () => store.SaveAsync(new("https://my-resource.openai.azure.com/", "gpt-4o-mini", null)));

        Assert.Equal("storeInvalid", error.Code);
        Assert.Equal("{ not json", File.ReadAllText(SecretsFile));
        Assert.Equal("storeInvalid", store.GetStatus().StoreError);
    }

    [Fact]
    public async Task Creates_the_file_private_to_the_user()
    {
        await Store().SaveAsync(new("https://my-resource.openai.azure.com/", "gpt-4o-mini", Key));

        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(SecretsFile));
        }

        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(SecretsFile)!, "*.tmp"));
    }

    [Fact]
    public async Task Secret_values_contain_the_stored_and_the_environment_keys()
    {
        AzureOpenAISettingsStore store = Store();
        await store.SaveAsync(new(null, null, Key));
        _environment["AzureOpenAI__APIKey"] = "environment-key-123456";

        Assert.Equal([Key, "environment-key-123456"], store.SecretValues());
    }

    [Fact]
    public void Reads_the_user_secrets_id_declared_by_the_labs_of_the_real_catalog()
    {
        AzureOpenAISettingsStore store = AzureOpenAISettingsStore.ForCatalog(LabCatalogTests.LoadRealCatalog());

        Assert.Equal("microsoft-agent-framework-learninglabs", store.UserSecretsId);
        Assert.EndsWith(Path.Combine("microsoft-agent-framework-learninglabs", "secrets.json"), store.SecretsFile);
    }

    private AzureOpenAISettingsStore Store(params string[] labSettingsFiles) =>
        new(SecretsFile, "microsoft-agent-framework-learninglabs", labSettingsFiles, () => _environment);

    private string LabSettingsFile(string json)
    {
        string file = Path.Combine(_root, $"appsettings-{Guid.NewGuid():N}.json");
        File.WriteAllText(file, json);
        return file;
    }

    private JsonObject ReadSaved() => (JsonObject)JsonNode.Parse(File.ReadAllText(SecretsFile))!;
}
