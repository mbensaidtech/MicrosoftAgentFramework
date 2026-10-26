using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LabDashboard.Settings;

namespace LabDashboard.Tests;

/// <summary>
/// LLM and embedding profiles (custom, trainer, mode) in the shared user-secrets file.
/// Every test works in a temporary folder with a fake environment: the real user-secrets file of the machine is never touched.
/// </summary>
public sealed class ModelProfilesTests : IDisposable
{
    private const string CustomKey = "custom-llm-key-0123456789-c1c1";
    private const string EmbeddingKey = "custom-embedding-key-0123456789-e2e2";
    private const string TrainerKey = "trainer-llm-key-0123456789-t3t3";
    private const string TrainerEmbeddingKey = "trainer-embedding-key-0123456789-t4t4";

    private const string LegacyFlat = """
        {
          "AzureOpenAI:Endpoint": "https://mine.openai.azure.com/",
          "AzureOpenAI:ChatDeploymentName": "gpt-4o-mini",
          "AzureOpenAI:APIKey": "custom-llm-key-0123456789-c1c1",
          "AzureOpenAI:EmbeddingDeploymentName": "text-embedding-3-small",
          "Dashboard:Reporting:WorkshopKey": "workshop-key-123456",
          "Other:Key": "x"
        }
        """;

    private const string LegacyNested = """
        {
          "AzureOpenAI": {
            "Endpoint": "https://mine.openai.azure.com/",
            "ChatDeploymentName": "gpt-4o-mini",
            "APIKey": "custom-llm-key-0123456789-c1c1",
            "EmbeddingDeploymentName": "text-embedding-3-small"
          },
          "Other:Key": "x"
        }
        """;

    /// <summary>The JSON options of the dashboard API (Program.cs).</summary>
    private static readonly JsonSerializerOptions ApiJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _root = Directory.CreateTempSubdirectory("labbench-models-").FullName;
    private readonly Dictionary<string, string> _environment = new(StringComparer.Ordinal);

    private string SecretsFile => Path.Combine(_root, "usersecrets", "microsoft-agent-framework-learninglabs", "secrets.json");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Theory]
    [InlineData(LegacyFlat)]
    [InlineData(LegacyNested)]
    public void An_existing_configuration_is_the_custom_llm_profile_and_nothing_is_written(string existing)
    {
        WriteSecrets(existing);
        AzureOpenAISettingsStore store = Store();

        ModelSettingsStatus status = store.GetModelStatus();
        store.GetStatus();
        store.SecretValues();

        Assert.Equal(ModelMode.Custom, status.Chat.Mode);
        Assert.True(status.Chat.Applied);
        Assert.Equal("azure-openai", status.Chat.Custom.Provider);
        Assert.Equal("https://mine.openai.azure.com/", status.Chat.Custom.Endpoint);
        Assert.Equal("gpt-4o-mini", status.Chat.Custom.Deployment);
        Assert.Equal(new StoredKeyStatus(true, "c1c1"), status.Chat.Custom.ApiKey);
        Assert.True(status.Chat.Custom.Complete);
        Assert.Equal(SettingSource.Dashboard, status.Chat.Active.Endpoint.Source);
        Assert.Equal("apiKey", status.Chat.Active.Authentication);

        Assert.Equal(ModelMode.Custom, status.Embedding.Mode);
        Assert.Equal(true, status.Embedding.Custom.SameResourceAsLlm);
        Assert.Equal("text-embedding-3-small", status.Embedding.Custom.Deployment);
        Assert.Null(status.Embedding.Custom.Endpoint);
        Assert.True(status.Embedding.Active.SameResourceAsLlm);
        Assert.Equal("apiKey", status.Embedding.Active.Authentication); // the key of the LLM resource
        Assert.True(status.Embedding.Active.Configured);

        Assert.False(status.Chat.Trainer.Complete);
        Assert.Equal(["endpoint", "deployment"], status.Chat.Trainer.Missing);
        Assert.Equal(existing, File.ReadAllText(SecretsFile)); // a pure read-time interpretation
    }

    [Fact]
    public void A_fresh_machine_has_both_kinds_in_custom_mode_with_empty_profiles()
    {
        ModelSettingsStatus status = Store().GetModelStatus();

        Assert.Equal(ModelMode.Custom, status.Chat.Mode);
        Assert.Equal(ModelMode.Custom, status.Embedding.Mode);
        Assert.False(status.Chat.Custom.Complete);
        Assert.False(status.Chat.Active.Configured);
        Assert.False(status.Embedding.Active.Configured);
        Assert.Equal(["azure-openai"], status.Providers);
        Assert.False(File.Exists(SecretsFile));
    }

    [Fact]
    public async Task The_first_change_of_one_kind_keeps_every_value_the_labs_read()
    {
        WriteSecrets(LegacyFlat);
        AzureOpenAISettingsStore store = Store();

        Assert.Empty(await store.SaveCustomProfileAsync(ModelKind.Embedding, new(null, "https://embeddings.openai.azure.com/", "text-embedding-3-large", EmbeddingKey, SameResourceAsLlm: false)));

        JsonObject saved = ReadSaved();
        Assert.Equal("https://mine.openai.azure.com/", (string?)saved["AzureOpenAI:Endpoint"]);
        Assert.Equal("gpt-4o-mini", (string?)saved["AzureOpenAI:ChatDeploymentName"]);
        Assert.Equal(CustomKey, (string?)saved["AzureOpenAI:APIKey"]);
        Assert.DoesNotContain(saved, entry => entry.Key.StartsWith("LabBench:Models:Chat", StringComparison.Ordinal)); // the LLM was not changed
        Assert.Equal("workshop-key-123456", (string?)saved["Dashboard:Reporting:WorkshopKey"]);
        Assert.Equal("x", (string?)saved["Other:Key"]);
    }

    [Fact]
    public async Task An_embedding_on_its_own_resource_writes_the_three_embedding_keys_and_the_llm_resource_removes_them()
    {
        WriteSecrets(LegacyFlat);
        AzureOpenAISettingsStore store = Store();

        Assert.Empty(await store.SaveCustomProfileAsync(ModelKind.Embedding, new("azure-openai", "https://embeddings.openai.azure.com/", "text-embedding-3-large", EmbeddingKey, SameResourceAsLlm: false)));

        JsonObject saved = ReadSaved();
        Assert.Equal("https://embeddings.openai.azure.com/", (string?)saved["AzureOpenAI:EmbeddingEndpoint"]);
        Assert.Equal("text-embedding-3-large", (string?)saved["AzureOpenAI:EmbeddingDeploymentName"]);
        Assert.Equal(EmbeddingKey, (string?)saved["AzureOpenAI:EmbeddingAPIKey"]);
        Assert.Equal("false", (string?)saved["LabBench:Models:Embedding:Custom:SameResourceAsLlm"]);
        Assert.Equal("custom", (string?)saved["LabBench:Models:Embedding:Mode"]);
        ModelKindStatus embedding = store.GetModelStatus().Embedding;
        Assert.False(embedding.Active.SameResourceAsLlm);
        Assert.Equal("e2e2", embedding.Custom.ApiKey.Hint);

        Assert.Empty(await store.SaveCustomProfileAsync(ModelKind.Embedding, new(null, "https://ignored.openai.azure.com/", "text-embedding-3-small", "ignored-key-0123456789", SameResourceAsLlm: true)));

        saved = ReadSaved();
        Assert.Equal("text-embedding-3-small", (string?)saved["AzureOpenAI:EmbeddingDeploymentName"]);
        Assert.Null(saved["AzureOpenAI:EmbeddingEndpoint"]);
        Assert.Null(saved["AzureOpenAI:EmbeddingAPIKey"]);
        Assert.Null(saved["LabBench:Models:Embedding:Custom:Endpoint"]);
        Assert.Null(saved["LabBench:Models:Embedding:Custom:APIKey"]);
        Assert.Equal(CustomKey, (string?)saved["AzureOpenAI:APIKey"]); // the LLM is untouched
        embedding = store.GetModelStatus().Embedding;
        Assert.True(embedding.Active.SameResourceAsLlm);
        Assert.Equal(true, embedding.Custom.SameResourceAsLlm);
    }

    [Fact]
    public async Task An_embedding_on_its_own_resource_without_a_key_uses_microsoft_entra_id_never_the_llm_key()
    {
        WriteSecrets(LegacyFlat);
        AzureOpenAISettingsStore store = Store();
        await store.SaveCustomProfileAsync(ModelKind.Embedding, new(null, "https://embeddings.openai.azure.com/", "text-embedding-3-large", EmbeddingKey, false));

        // An empty key keeps the stored one; removing it leaves the embedding on Microsoft Entra ID.
        await store.SaveCustomProfileAsync(ModelKind.Embedding, new(null, "https://embeddings.openai.azure.com/", "text-embedding-3-large", "", false));
        Assert.Equal(EmbeddingKey, (string?)ReadSaved()["AzureOpenAI:EmbeddingAPIKey"]);

        await store.RemoveCustomApiKeyAsync(ModelKind.Embedding);

        JsonObject saved = ReadSaved();
        Assert.Null(saved["AzureOpenAI:EmbeddingAPIKey"]);
        Assert.Null(saved["LabBench:Models:Embedding:Custom:APIKey"]);
        Assert.Equal(CustomKey, (string?)saved["AzureOpenAI:APIKey"]);
        ModelSettingsStatus status = store.GetModelStatus();
        Assert.Equal("entraId", status.Embedding.Active.Authentication);
        Assert.Equal("apiKey", status.Chat.Active.Authentication);
    }

    [Fact]
    public async Task Switching_the_llm_to_trainer_and_back_restores_the_custom_values_unchanged()
    {
        WriteSecrets(LegacyFlat);
        AzureOpenAISettingsStore store = Store();
        Assert.Empty(await store.SaveTrainerProfilesAsync([Trainer(ModelKind.Chat)]));
        Assert.Equal("https://mine.openai.azure.com/", (string?)ReadSaved()["AzureOpenAI:Endpoint"]); // retrieved, not used yet

        ModelModeOutcome toTrainer = await store.SwitchModeAsync(ModelKind.Chat, "trainer");

        Assert.Null(toTrainer.Error);
        JsonObject saved = ReadSaved();
        Assert.Equal("https://trainer.openai.azure.com/", (string?)saved["AzureOpenAI:Endpoint"]);
        Assert.Equal("trainer-gpt", (string?)saved["AzureOpenAI:ChatDeploymentName"]);
        Assert.Equal(TrainerKey, (string?)saved["AzureOpenAI:APIKey"]);
        Assert.Equal("text-embedding-3-small", (string?)saved["AzureOpenAI:EmbeddingDeploymentName"]); // the embedding is independent
        ModelSettingsStatus status = store.GetModelStatus();
        Assert.Equal(ModelMode.Trainer, status.Chat.Mode);
        Assert.Equal("https://mine.openai.azure.com/", status.Chat.Custom.Endpoint);
        Assert.Equal("c1c1", status.Chat.Custom.ApiKey.Hint);
        Assert.Equal(ModelMode.Custom, status.Embedding.Mode);

        ModelModeOutcome toCustom = await store.SwitchModeAsync(ModelKind.Chat, "custom");

        Assert.Null(toCustom.Error);
        saved = ReadSaved();
        Assert.Equal("https://mine.openai.azure.com/", (string?)saved["AzureOpenAI:Endpoint"]);
        Assert.Equal("gpt-4o-mini", (string?)saved["AzureOpenAI:ChatDeploymentName"]);
        Assert.Equal(CustomKey, (string?)saved["AzureOpenAI:APIKey"]);
        Assert.Equal("https://trainer.openai.azure.com/", (string?)saved["LabBench:Models:Chat:Trainer:Endpoint"]); // no profile is deleted
        Assert.Equal(ModelMode.Custom, store.GetModelStatus().Chat.Mode);
    }

    [Fact]
    public async Task Switching_the_embedding_to_trainer_and_back_restores_the_resource_of_the_llm()
    {
        WriteSecrets(LegacyFlat);
        AzureOpenAISettingsStore store = Store();
        await store.SaveTrainerProfilesAsync([Trainer(ModelKind.Embedding)]);

        Assert.Null((await store.SwitchModeAsync(ModelKind.Embedding, "trainer")).Error);
        JsonObject saved = ReadSaved();
        Assert.Equal("https://trainer-embeddings.openai.azure.com/", (string?)saved["AzureOpenAI:EmbeddingEndpoint"]);
        Assert.Equal(TrainerEmbeddingKey, (string?)saved["AzureOpenAI:EmbeddingAPIKey"]);
        Assert.Equal(CustomKey, (string?)saved["AzureOpenAI:APIKey"]);

        Assert.Null((await store.SwitchModeAsync(ModelKind.Embedding, "custom")).Error);
        saved = ReadSaved();
        Assert.Equal("text-embedding-3-small", (string?)saved["AzureOpenAI:EmbeddingDeploymentName"]);
        Assert.Null(saved["AzureOpenAI:EmbeddingEndpoint"]);
        Assert.Null(saved["AzureOpenAI:EmbeddingAPIKey"]);
    }

    [Theory]
    [InlineData("chat", "trainer", new[] { "endpoint", "deployment" })]
    [InlineData("embedding", "trainer", new[] { "endpoint", "deployment" })]
    [InlineData("embedding", "custom", new[] { "deployment" })]
    public async Task Switching_to_an_incomplete_profile_is_refused_and_writes_nothing(string kind, string mode, string[] missing)
    {
        const string existing = """{ "AzureOpenAI:Endpoint": "https://mine.openai.azure.com/", "AzureOpenAI:ChatDeploymentName": "gpt-4o-mini", "Other:Key": "x" }""";
        WriteSecrets(existing);
        Assert.True(AzureOpenAISettingsStore.TryParseKind(kind, out ModelKind modelKind));

        ModelModeOutcome outcome = await Store().SwitchModeAsync(modelKind, mode);

        Assert.Equal("profileIncomplete", outcome.Error);
        Assert.Equal(missing, outcome.Missing);
        Assert.Equal(existing, File.ReadAllText(SecretsFile));
    }

    [Fact]
    public async Task A_trainer_providing_only_the_llm_leaves_the_embedding_trainer_mode_unavailable()
    {
        AzureOpenAISettingsStore store = Store();
        Assert.Empty(await store.SaveTrainerProfilesAsync([Trainer(ModelKind.Chat, use: true)]));
        string before = File.ReadAllText(SecretsFile);

        ModelModeOutcome outcome = await store.SwitchModeAsync(ModelKind.Embedding, "trainer");

        Assert.Equal("profileIncomplete", outcome.Error);
        Assert.Equal(before, File.ReadAllText(SecretsFile));
        Assert.Equal(ModelMode.Trainer, store.GetModelStatus().Chat.Mode);
        Assert.Equal(ModelMode.Custom, store.GetModelStatus().Embedding.Mode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("both")]
    [InlineData("0")]
    public async Task An_unknown_mode_is_refused(string mode)
    {
        Assert.Equal("invalid", (await Store().SwitchModeAsync(ModelKind.Chat, mode)).Error);
        Assert.False(File.Exists(SecretsFile));
    }

    [Theory]
    [InlineData("chat", "openai")]
    [InlineData("chat", "anthropic")]
    [InlineData("embedding", "deepseek")]
    public async Task A_provider_other_than_azure_openai_is_refused(string kind, string provider)
    {
        WriteSecrets("""{ "Other:Key": "x" }""");
        Assert.True(AzureOpenAISettingsStore.TryParseKind(kind, out ModelKind modelKind));
        AzureOpenAISettingsStore store = Store();

        IReadOnlyDictionary<string, string> errors = await store.SaveCustomProfileAsync(modelKind, new(provider, "https://mine.openai.azure.com/", "gpt-4o-mini", null, false));
        IReadOnlyDictionary<string, string> trainerErrors = await store.SaveTrainerProfilesAsync([Trainer(modelKind) with { Provider = provider }]);

        Assert.Equal("unsupportedProvider", errors["provider"]);
        Assert.Equal("unsupportedProvider", trainerErrors[$"{kind}.provider"]);
        Assert.Equal("""{ "Other:Key": "x" }""", File.ReadAllText(SecretsFile));
    }

    [Fact]
    public async Task A_stored_profile_of_another_provider_cannot_be_selected()
    {
        const string existing = """
            {
              "LabBench:Models:Chat:Trainer:Provider": "openai",
              "LabBench:Models:Chat:Trainer:Endpoint": "https://api.openai.com/",
              "LabBench:Models:Chat:Trainer:Deployment": "gpt-4o"
            }
            """;
        WriteSecrets(existing);

        ModelModeOutcome outcome = await Store().SwitchModeAsync(ModelKind.Chat, "trainer");

        Assert.Equal("unsupportedProvider", outcome.Error);
        Assert.Equal(existing, File.ReadAllText(SecretsFile));
    }

    [Theory]
    [InlineData("chat", "http://mine.openai.azure.com/", "gpt-4o-mini", null, false, "endpoint", "invalid")]
    [InlineData("chat", "https://YOUR-RESOURCE.openai.azure.com/", "gpt-4o-mini", null, false, "endpoint", "placeholder")]
    [InlineData("chat", "https://mine.openai.azure.com/", "YOUR-DEPLOYMENT-NAME", null, false, "deployment", "placeholder")]
    [InlineData("chat", "https://mine.openai.azure.com/", "gpt 4o", null, false, "deployment", "invalid")]
    [InlineData("chat", "https://mine.openai.azure.com/", "gpt-4o-mini", "abc", false, "apiKey", "tooShort")]
    [InlineData("embedding", "https://embeddings.openai.azure.com/", "text-embedding-3-small", "abc def ghi jkl", false, "apiKey", "invalid")]
    [InlineData("embedding", null, "text-embedding-3-small", null, false, "endpoint", "required")]
    [InlineData("embedding", null, "text embedding", null, true, "deployment", "invalid")]
    public async Task Custom_profiles_reuse_the_validations_of_the_form_and_write_nothing_when_invalid(
        string kind, string? endpoint, string? deployment, string? apiKey, bool sameResource, string field, string code)
    {
        WriteSecrets("""{ "Other:Key": "x" }""");
        Assert.True(AzureOpenAISettingsStore.TryParseKind(kind, out ModelKind modelKind));

        IReadOnlyDictionary<string, string> errors = await Store().SaveCustomProfileAsync(modelKind, new(null, endpoint, deployment, apiKey, sameResource));

        Assert.Equal(code, errors[field]);
        Assert.Equal("""{ "Other:Key": "x" }""", File.ReadAllText(SecretsFile));
    }

    [Fact]
    public async Task Trainer_profiles_never_change_a_custom_profile_and_change_the_active_values_only_of_the_kinds_in_trainer_mode()
    {
        WriteSecrets(LegacyFlat);
        AzureOpenAISettingsStore store = Store();
        await store.SaveCustomProfileAsync(ModelKind.Embedding, new(null, null, "my-embedding", null, true));

        // "Use it" for the LLM only.
        Assert.Empty(await store.SaveTrainerProfilesAsync([Trainer(ModelKind.Chat, use: true), Trainer(ModelKind.Embedding)]));

        JsonObject saved = ReadSaved();
        Assert.Equal("https://trainer.openai.azure.com/", (string?)saved["AzureOpenAI:Endpoint"]);
        Assert.Equal("my-embedding", (string?)saved["AzureOpenAI:EmbeddingDeploymentName"]);
        Assert.Null(saved["AzureOpenAI:EmbeddingEndpoint"]);
        ModelSettingsStatus status = store.GetModelStatus();
        Assert.Equal(ModelMode.Trainer, status.Chat.Mode);
        Assert.Equal(ModelMode.Custom, status.Embedding.Mode);
        Assert.Equal("https://mine.openai.azure.com/", status.Chat.Custom.Endpoint);
        Assert.Equal("my-embedding", status.Embedding.Custom.Deployment);
        Assert.True(status.Embedding.Trainer.Complete);
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 9, 30, 0, TimeSpan.Zero), status.Chat.Trainer.RetrievedAt);

        // A refresh updates the trainer profile in use, keeps the modes and the custom profiles.
        Assert.Empty(await store.SaveTrainerProfilesAsync([Trainer(ModelKind.Chat) with { Deployment = "trainer-gpt-2" }]));

        status = store.GetModelStatus();
        Assert.Equal("trainer-gpt-2", (string?)ReadSaved()["AzureOpenAI:ChatDeploymentName"]);
        Assert.Equal(ModelMode.Trainer, status.Chat.Mode);
        Assert.Equal("gpt-4o-mini", status.Chat.Custom.Deployment);
        Assert.Equal("c1c1", status.Chat.Custom.ApiKey.Hint);
    }

    [Fact]
    public async Task The_trainer_configuration_identity_is_saved_with_the_profile_replaced_by_the_next_unlock_and_never_on_a_custom_profile()
    {
        WriteSecrets(LegacyFlat);
        AzureOpenAISettingsStore store = Store();

        Assert.Empty(await store.SaveTrainerProfilesAsync(
            [Trainer(ModelKind.Chat, use: true) with { ConfigId = "11111111-1111-4111-8111-111111111111", ConfigName = "Workshop GPT", ConfigUpdatedAt = "2026-10-25T09:00:00.000Z" }]));

        ModelSettingsStatus status = store.GetModelStatus();
        Assert.Equal(("11111111-1111-4111-8111-111111111111", "Workshop GPT", "2026-10-25T09:00:00.000Z"),
            (status.Chat.Trainer.ConfigId, status.Chat.Trainer.ConfigName, status.Chat.Trainer.ConfigUpdatedAt));
        Assert.Equal("Workshop GPT", (string?)ReadSaved()["LabBench:Models:Chat:Trainer:ConfigName"]);
        Assert.Equal(((string?)null, (string?)null, (string?)null), (status.Chat.Custom.ConfigId, status.Chat.Custom.ConfigName, status.Chat.Custom.ConfigUpdatedAt));

        // The next unlock brings the trainer's new values: the identity follows, the custom profile stays as it was.
        Assert.Empty(await store.SaveTrainerProfilesAsync(
            [Trainer(ModelKind.Chat) with { ConfigId = "22222222-2222-4222-8222-222222222222", ConfigName = "Workshop GPT 2", ConfigUpdatedAt = "2026-10-26T08:00:00.000Z" }]));

        status = store.GetModelStatus();
        Assert.Equal(("22222222-2222-4222-8222-222222222222", "2026-10-26T08:00:00.000Z"), (status.Chat.Trainer.ConfigId, status.Chat.Trainer.ConfigUpdatedAt));
        Assert.Equal(("https://mine.openai.azure.com/", "gpt-4o-mini", "c1c1"), (status.Chat.Custom.Endpoint, status.Chat.Custom.Deployment, status.Chat.Custom.ApiKey.Hint));
        Assert.DoesNotContain(ReadSaved(), entry => entry.Key.StartsWith("LabBench:Models:Chat:Custom:Config", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Incomplete_or_duplicate_trainer_profiles_are_refused_and_nothing_is_written()
    {
        WriteSecrets(LegacyFlat);
        AzureOpenAISettingsStore store = Store();

        IReadOnlyDictionary<string, string> errors = await store.SaveTrainerProfilesAsync(
            [Trainer(ModelKind.Chat), Trainer(ModelKind.Embedding) with { Endpoint = null, Deployment = " " }]);
        IReadOnlyDictionary<string, string> duplicate = await store.SaveTrainerProfilesAsync([Trainer(ModelKind.Chat), Trainer(ModelKind.Chat)]);

        Assert.Equal("required", errors["embedding.endpoint"]);
        Assert.Equal("required", errors["embedding.deployment"]);
        Assert.Equal("duplicate", duplicate["chat"]);
        Assert.Equal(LegacyFlat, File.ReadAllText(SecretsFile));
    }

    [Fact]
    public async Task The_llm_settings_form_saves_the_custom_profile_and_applies_it_only_in_custom_mode()
    {
        WriteSecrets(LegacyFlat);
        AzureOpenAISettingsStore store = Store();
        await store.SaveTrainerProfilesAsync([Trainer(ModelKind.Chat, use: true)]);

        Assert.Empty(await store.SaveAsync(new("https://mine-2.openai.azure.com/", "gpt-4.1-mini", "")));

        Assert.Equal("https://trainer.openai.azure.com/", (string?)ReadSaved()["AzureOpenAI:Endpoint"]); // the trainer profile stays in use
        Assert.Equal("https://mine-2.openai.azure.com/", store.GetModelStatus().Chat.Custom.Endpoint);

        await store.SwitchModeAsync(ModelKind.Chat, "custom");

        JsonObject saved = ReadSaved();
        Assert.Equal("https://mine-2.openai.azure.com/", (string?)saved["AzureOpenAI:Endpoint"]);
        Assert.Equal("gpt-4.1-mini", (string?)saved["AzureOpenAI:ChatDeploymentName"]);
        Assert.Equal(CustomKey, (string?)saved["AzureOpenAI:APIKey"]); // an empty key field keeps the stored key
    }

    [Fact]
    public async Task In_custom_mode_a_value_set_with_dotnet_user_secrets_is_the_custom_value_and_survives_a_trainer_round_trip()
    {
        AzureOpenAISettingsStore store = Store();
        await store.SaveAsync(new("https://mine.openai.azure.com/", "gpt-4o-mini", null));
        JsonObject secrets = ReadSaved();
        secrets["AzureOpenAI:APIKey"] = CustomKey; // dotnet user-secrets set "AzureOpenAI:APIKey" …
        WriteSecrets(secrets.ToJsonString());

        Assert.Equal("c1c1", store.GetModelStatus().Chat.Custom.ApiKey.Hint);
        await store.SaveAsync(new("https://mine.openai.azure.com/", "gpt-4o", ""));
        Assert.Equal(CustomKey, (string?)ReadSaved()["AzureOpenAI:APIKey"]);

        await store.SaveTrainerProfilesAsync([Trainer(ModelKind.Chat, use: true)]);
        await store.SwitchModeAsync(ModelKind.Chat, "custom");

        JsonObject saved = ReadSaved();
        Assert.Equal(CustomKey, (string?)saved["AzureOpenAI:APIKey"]);
        Assert.Equal("gpt-4o", (string?)saved["AzureOpenAI:ChatDeploymentName"]);
    }

    [Fact]
    public async Task Every_other_entry_of_the_file_is_kept_and_profiles_are_written_flat_and_private()
    {
        const string foreign = """
            {
              "Dashboard:Reporting:WorkshopKey": "workshop-key-123456",
              "MCPServers": { "HuggingFace": { "BearerToken": "hf-token" } },
              "AzureOpenAI": { "Other": "kept" },
              "Other:Key": "x"
            }
            """;
        WriteSecrets(foreign);
        AzureOpenAISettingsStore store = Store();

        await store.SaveAsync(new("https://mine.openai.azure.com/", "gpt-4o-mini", CustomKey));
        await store.SaveCustomProfileAsync(ModelKind.Embedding, new(null, "https://embeddings.openai.azure.com/", "text-embedding-3-small", EmbeddingKey, false));
        await store.SaveTrainerProfilesAsync([Trainer(ModelKind.Chat, use: true), Trainer(ModelKind.Embedding, use: true)]);
        await store.SwitchModeAsync(ModelKind.Chat, "custom");
        await store.RemoveCustomApiKeyAsync(ModelKind.Chat);

        JsonObject saved = ReadSaved();
        Assert.Equal("workshop-key-123456", (string?)saved["Dashboard:Reporting:WorkshopKey"]);
        Assert.Equal("hf-token", (string?)saved["MCPServers"]!["HuggingFace"]!["BearerToken"]);
        Assert.Equal("kept", (string?)saved["AzureOpenAI"]!["Other"]);
        Assert.Equal("x", (string?)saved["Other:Key"]);
        Assert.Equal("custom", (string?)saved["LabBench:Models:Chat:Mode"]);
        Assert.Equal("trainer", (string?)saved["LabBench:Models:Embedding:Mode"]);
        Assert.Null(saved["LabBench"]);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(SecretsFile)!, "*.tmp"));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(SecretsFile));
        }
    }

    [Fact]
    public async Task No_status_contains_a_stored_key()
    {
        AzureOpenAISettingsStore store = await StoreWithEveryKey();
        _environment["AzureOpenAI__APIKey"] = "environment-llm-key-0123456789-v5v5";
        _environment["AzureOpenAI__EmbeddingAPIKey"] = "environment-embedding-key-0123456789-v6v6";

        string models = JsonSerializer.Serialize(store.GetModelStatus(), ApiJson);
        string legacy = JsonSerializer.Serialize(store.GetStatus(), ApiJson);

        foreach (string key in new[] { CustomKey, EmbeddingKey, TrainerKey, TrainerEmbeddingKey, "environment-llm-key-0123456789-v5v5", "environment-embedding-key-0123456789-v6v6" })
        {
            Assert.DoesNotContain(key, models);
            Assert.DoesNotContain(key, legacy);
            Assert.DoesNotContain(key[^15..], models); // more than the 4-character hint
            Assert.DoesNotContain(key[^15..], legacy);
        }

        ModelSettingsStatus status = store.GetModelStatus();
        Assert.Equal("c1c1", status.Chat.Custom.ApiKey.Hint);
        Assert.Equal("t3t3", status.Chat.Trainer.ApiKey.Hint);
        Assert.Equal("e2e2", status.Embedding.Custom.ApiKey.Hint);
        Assert.Equal("t4t4", status.Embedding.Trainer.ApiKey.Hint);
        Assert.Equal("v5v5", status.Chat.Active.ApiKey.Hint);
        Assert.Equal("v6v6", status.Embedding.Active.ApiKey.Hint);
        Assert.DoesNotContain(CustomKey, new ModelProfileUpdate(null, null, null, CustomKey).ToString());
        Assert.DoesNotContain(TrainerKey, Trainer(ModelKind.Chat).ToString());
    }

    [Fact]
    public async Task Secret_values_contain_every_stored_key_of_every_profile_and_the_environment_keys()
    {
        AzureOpenAISettingsStore store = await StoreWithEveryKey();
        _environment["AzureOpenAI__APIKey"] = " environment-llm-key-0123456789 ";
        _environment["AzureOpenAI__EmbeddingAPIKey"] = "environment-embedding-key-0123456789";

        IReadOnlyList<string> values = store.SecretValues();

        Assert.Equal(
            new[] { CustomKey, EmbeddingKey, TrainerKey, TrainerEmbeddingKey, "environment-llm-key-0123456789", "environment-embedding-key-0123456789" }.Order(),
            values.Order());
    }

    [Fact]
    public async Task Embedding_environment_variables_win_and_are_reported()
    {
        AzureOpenAISettingsStore store = Store();
        await store.SaveAsync(new("https://mine.openai.azure.com/", "gpt-4o-mini", null));
        await store.SaveCustomProfileAsync(ModelKind.Embedding, new(null, null, "text-embedding-3-small", null, true));
        _environment["azureopenai__embeddingendpoint"] = "https://env-embeddings.openai.azure.com/"; // matched case-insensitively, as by .NET
        _environment["AzureOpenAI__EmbeddingDeploymentName"] = "env-embedding";
        _environment["AzureOpenAI__EmbeddingAPIKey"] = "environment-embedding-key-0123456789-wxyz";

        ActiveModelStatus active = store.GetModelStatus().Embedding.Active;

        Assert.Equal(SettingSource.Environment, active.Endpoint.Source);
        Assert.Equal("https://env-embeddings.openai.azure.com/", active.Endpoint.EffectiveValue);
        Assert.Equal("AzureOpenAI__EmbeddingEndpoint", active.Endpoint.EnvironmentVariable);
        Assert.Equal(SettingSource.Environment, active.Deployment.Source);
        Assert.Equal("text-embedding-3-small", active.Deployment.DashboardValue);
        Assert.Equal("env-embedding", active.Deployment.EffectiveValue);
        Assert.Equal(SettingSource.Environment, active.ApiKey.Source);
        Assert.Equal("wxyz", active.ApiKey.Hint);
        Assert.False(active.SameResourceAsLlm);
        Assert.Equal("apiKey", active.Authentication);
        Assert.Equal("entraId", store.GetModelStatus().Chat.Active.Authentication);
    }

    [Fact]
    public async Task A_file_that_is_not_valid_json_is_never_overwritten()
    {
        WriteSecrets("{ not json");
        AzureOpenAISettingsStore store = Store();

        Assert.Equal("storeInvalid", store.GetModelStatus().StoreError);
        AzureOpenAISettingsStoreException save = await Assert.ThrowsAsync<AzureOpenAISettingsStoreException>(
            () => store.SaveCustomProfileAsync(ModelKind.Embedding, new(null, null, "text-embedding-3-small", null, true)));
        AzureOpenAISettingsStoreException switching = await Assert.ThrowsAsync<AzureOpenAISettingsStoreException>(
            () => store.SwitchModeAsync(ModelKind.Chat, "custom"));

        Assert.Equal("storeInvalid", save.Code);
        Assert.Equal("storeInvalid", switching.Code);
        Assert.Equal("{ not json", File.ReadAllText(SecretsFile));
        Assert.Empty(store.SecretValues());
    }

    [Theory]
    [InlineData("chat", true)]
    [InlineData("Embedding", true)]
    [InlineData("llm", false)]
    [InlineData("0", false)]
    [InlineData(null, false)]
    public void Only_chat_and_embedding_are_model_kinds(string? value, bool expected)
    {
        Assert.Equal(expected, AzureOpenAISettingsStore.TryParseKind(value, out _));
    }

    /// <summary>A key in each of the four profiles; the LLM in trainer mode, the embedding in custom mode on its own resource.</summary>
    private async Task<AzureOpenAISettingsStore> StoreWithEveryKey()
    {
        AzureOpenAISettingsStore store = Store();
        Assert.Empty(await store.SaveAsync(new("https://mine.openai.azure.com/", "gpt-4o-mini", CustomKey)));
        Assert.Empty(await store.SaveCustomProfileAsync(ModelKind.Embedding, new(null, "https://embeddings.openai.azure.com/", "text-embedding-3-small", EmbeddingKey, false)));
        Assert.Empty(await store.SaveTrainerProfilesAsync([Trainer(ModelKind.Chat, use: true), Trainer(ModelKind.Embedding)]));
        return store;
    }

    private static TrainerProfileUpdate Trainer(ModelKind kind, bool use = false) =>
        kind == ModelKind.Chat
            ? new(kind, "azure-openai", "https://trainer.openai.azure.com/", "trainer-gpt", TrainerKey, new DateTimeOffset(2026, 10, 25, 9, 30, 0, TimeSpan.Zero), use)
            : new(kind, "azure-openai", "https://trainer-embeddings.openai.azure.com/", "trainer-embedding", TrainerEmbeddingKey, new DateTimeOffset(2026, 10, 25, 9, 30, 0, TimeSpan.Zero), use);

    private AzureOpenAISettingsStore Store() =>
        new(SecretsFile, "microsoft-agent-framework-learninglabs", [], () => _environment);

    private void WriteSecrets(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SecretsFile)!);
        File.WriteAllText(SecretsFile, json);
    }

    private JsonObject ReadSaved() => (JsonObject)JsonNode.Parse(File.ReadAllText(SecretsFile))!;
}
