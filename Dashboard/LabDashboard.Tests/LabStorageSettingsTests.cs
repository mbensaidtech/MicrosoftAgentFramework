using System.Text.Json.Nodes;
using LabDashboard.Settings;

namespace LabDashboard.Tests;

/// <summary>Every test works in a temporary folder: the real user-secrets file of the machine is never touched.</summary>
public sealed class LabStorageSettingsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("labbench-storage-").FullName;
    private readonly Dictionary<string, string> _environment = new(StringComparer.Ordinal);

    private string SecretsFile => Path.Combine(_root, "usersecrets", "microsoft-agent-framework-learninglabs", "secrets.json");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Without_any_value_the_labs_use_their_default_the_mongodb_container()
    {
        LabStorageStatus status = Settings().GetStatus();

        Assert.Equal("MongoDb", status.Mode);
        Assert.Equal(SettingSource.LabDefault, status.Source);
        Assert.Equal(["MongoDb", "EmbeddedMongoDb", "JsonFile"], status.Modes);
    }

    [Fact]
    public async Task Saves_the_flat_entry_that_dotnet_user_secrets_writes_and_keeps_the_other_entries()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SecretsFile)!);
        File.WriteAllText(SecretsFile, """{ "AzureOpenAI:Endpoint": "https://my-resource.openai.azure.com/", "Storage": { "DataDirectory": "/data" } }""");

        Assert.Null(await Settings().SaveAsync(new("EmbeddedMongoDb")));

        JsonObject saved = JsonNode.Parse(File.ReadAllText(SecretsFile))!.AsObject();
        Assert.Equal("EmbeddedMongoDb", (string?)saved["Storage:Mode"]);
        Assert.Equal("https://my-resource.openai.azure.com/", (string?)saved["AzureOpenAI:Endpoint"]);
        Assert.Equal("/data", (string?)saved["Storage"]!["DataDirectory"]);

        LabStorageStatus status = Settings().GetStatus();
        Assert.Equal("EmbeddedMongoDb", status.Mode);
        Assert.Equal(SettingSource.Dashboard, status.Source);
    }

    [Fact]
    public async Task Reads_a_value_set_by_hand_whatever_its_case_and_saves_the_spelling_of_the_labs()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SecretsFile)!);
        File.WriteAllText(SecretsFile, """{ "Storage": { "Mode": "jsonfile" } }""");

        Assert.Equal("JsonFile", Settings().GetStatus().Mode);

        await Settings().SaveAsync(new("mongodb"));

        JsonObject saved = JsonNode.Parse(File.ReadAllText(SecretsFile))!.AsObject();
        Assert.Equal("MongoDb", (string?)saved["Storage:Mode"]);
        Assert.Null(saved["Storage"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Atlas")]
    public async Task Refuses_an_unknown_mode_without_writing_anything(string? mode)
    {
        Assert.Equal("invalid", await Settings().SaveAsync(new(mode)));
        Assert.False(File.Exists(SecretsFile));
    }

    [Fact]
    public async Task An_environment_variable_wins_over_the_saved_mode()
    {
        await Settings().SaveAsync(new("JsonFile"));
        _environment["STORAGE__MODE"] = "EmbeddedMongoDb";

        LabStorageStatus status = Settings().GetStatus();

        Assert.Equal("EmbeddedMongoDb", status.Mode);
        Assert.Equal(SettingSource.Environment, status.Source);
        Assert.Equal("Storage__Mode", status.EnvironmentVariable);
    }

    [Fact]
    public async Task An_unreadable_file_is_reported_on_save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SecretsFile)!);
        File.WriteAllText(SecretsFile, "not json");

        AzureOpenAISettingsStoreException error = await Assert.ThrowsAsync<AzureOpenAISettingsStoreException>(() => Settings().SaveAsync(new("JsonFile")));
        Assert.Equal("storeInvalid", error.Code);
        Assert.Equal("MongoDb", Settings().GetStatus().Mode);
    }

    private LabStorageSettings Settings() =>
        new(new AzureOpenAISettingsStore(SecretsFile, "microsoft-agent-framework-learninglabs", [], () => new Dictionary<string, string>()), () => _environment);
}
