using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using LabDashboard.Reporting;
using LabDashboard.Settings;
using LabDashboard.TrainerModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LabDashboard.Tests;

/// <summary>
/// The trainer configuration integration layer (Business Rules 13–19): the precedence (mock, not configured, Admin Dashboard adapter), the
/// identity requirement and the no-network guarantee against a real loopback admin server, the mock (success, wrong code, every simulated
/// error, LLM only, overview), the code format, and secrets kept out of ToString, JSON and logs. Tests never reach a real admin dashboard:
/// the Admin Dashboard adapter runs against <see cref="FakeAdminHandler"/> or <see cref="FakeAdminServer"/>.
/// </summary>
public sealed class TrainerConfigurationTests : IAsyncDisposable
{
    private const string AdminUrl = "https://labs-admin.example.com";
    private const string AdminApiKey = "lbk_test_trainer_admin_api_key_7a1d";
    private const string OtherCode = "M4PRS";
    private const string DevToken = "tok-trainer-configuration-3f9a2c";
    private const string UserId = "8d2b6c1e-4f3a-4e7b-b2a9-5c0d1e2f3a4b";

    private static readonly string DashboardProjectDirectory =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LabDashboard"));

    /// <summary>The JSON options of the dashboard API (Program.cs).</summary>
    private static readonly JsonSerializerOptions ApiJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly DevIdentity Joined = new() { UserId = UserId, Username = "john-dev", DevToken = DevToken };

    private readonly List<FakeAdminServer> _servers = [];
    private readonly List<TrainerConfigurationService> _services = [];
    private readonly FakeAdminHandler _handler = new();
    private readonly ListLogger _logger = new();
    private readonly ManualClock _clock = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync()
    {
        foreach (TrainerConfigurationService service in _services)
        {
            service.Dispose();
        }

        foreach (FakeAdminServer server in _servers)
        {
            await server.DisposeAsync();
        }

        _handler.Dispose();
    }

    public static TheoryData<TrainerConfigurationErrorKind> ErrorKinds()
    {
        TheoryData<TrainerConfigurationErrorKind> data = [];
        foreach (TrainerConfigurationErrorKind kind in Enum.GetValues<TrainerConfigurationErrorKind>())
        {
            data.Add(kind);
        }

        return data;
    }

    // ---------- precedence ----------

    [Fact]
    public async Task Without_an_identity_every_step_answers_not_configured_workshop_and_nothing_reaches_the_admin_dashboard()
    {
        FakeAdminServer server = await StartServerAsync();
        DevIdentity? identity = null;
        TrainerConfigurationService service = Track(new TrainerConfigurationService(
            new DashboardOptions { Reporting = { ServerUrl = server.Url, ApiKey = FakeAdminServer.ApiKey } }, () => identity, _logger, _clock, _ => null));

        TrainerOutcome<TrainerModelsOverview> overview = await service.GetOverviewAsync(Token);
        TrainerOutcome<SealedTrainerConfiguration> retrieved = await service.RetrieveAsync(Token);
        Assert.Equal(["workshop"], service.MissingConfiguration);

        // Joined, but not registered yet: no dev token, still nothing to send.
        identity = Joined with { DevToken = null };
        TrainerOutcome<SealedTrainerConfiguration> unregistered = await service.RetrieveAsync(Token);

        Assert.All([overview.Error!, retrieved.Error!, unregistered.Error!], error =>
        {
            Assert.Equal(TrainerConfigurationErrorKind.NotConfigured, error.Kind);
            Assert.Equal([TrainerConfigurationService.WorkshopSetting], error.Missing);
        });
        Assert.False(service.Simulated);
        Assert.Empty(server.Requests);
        Assert.Contains("\"kind\":\"notConfigured\"", JsonSerializer.Serialize(retrieved.Error, ApiJson));

        // Joining the workshop is enough, without a restart: the Admin Dashboard adapter answers (routes not deployed here: 404).
        identity = Joined;
        Assert.Empty(service.MissingConfiguration);
        Assert.Equal(TrainerConfigurationErrorKind.NotFound, (await service.GetOverviewAsync(Token)).Error!.Kind);
        Assert.Single(server.Requests);
    }

    [Fact]
    public async Task With_an_identity_the_admin_dashboard_adapter_sends_the_api_key_and_the_dev_token_and_a_missing_route_is_not_found()
    {
        FakeAdminServer server = await StartServerAsync();
        TrainerConfigurationService service = Track(new TrainerConfigurationService(
            new DashboardOptions { Reporting = { ServerUrl = server.Url, ApiKey = FakeAdminServer.ApiKey } }, () => Joined, _logger, _clock, _ => null));

        TrainerOutcome<TrainerModelsOverview> overview = await service.GetOverviewAsync(Token);
        TrainerOutcome<SealedTrainerConfiguration> retrieved = await service.RetrieveAsync(Token);

        Assert.Equal(TrainerConfigurationErrorKind.NotFound, overview.Error!.Kind);
        Assert.Equal(TrainerConfigurationErrorKind.NotFound, retrieved.Error!.Kind);
        Assert.Equal(404, retrieved.Error.HttpStatus);
        AdminRequest[] requests = server.RequestsTo("/api/v1/model-configs").ToArray();
        Assert.Equal(["", "?sealed=1"], requests.Select(r => r.Query ?? ""));
        Assert.All(requests, r =>
        {
            Assert.Equal("GET", r.Method);
            Assert.Equal(FakeAdminServer.ApiKey, r.ApiKey);
            Assert.Equal(DevToken, r.Bearer);
        });
    }

    [Fact]
    public async Task The_overview_and_the_envelope_come_from_the_admin_dashboard_and_an_envelope_that_cannot_be_opened_is_refused_without_a_call()
    {
        FakeAdminServer server = await StartServerAsync();
        server.Expect("/api/v1/model-configs",
            new CannedResponse(200, Body: AdminAnswer(sealedEnvelope: false)),
            new CannedResponse(200, Body: AdminAnswer(sealedEnvelope: true)));
        TrainerConfigurationService service = Track(new TrainerConfigurationService(
            new DashboardOptions { Reporting = { ServerUrl = server.Url, ApiKey = FakeAdminServer.ApiKey } }, () => Joined, _logger, _clock, _ => null));

        TrainerModelsOverview overview = (await service.GetOverviewAsync(Token)).Value!;
        SealedTrainerConfiguration sealedConfiguration = (await service.RetrieveAsync(Token)).Value!;
        TrainerUnlockResult unlock = await service.UnlockAsync(sealedConfiguration, MockTrainerConfiguration.DefaultCode, Token);

        Assert.Equal((TrainerModelState.Applicable, "gpt-4o-mini"), (overview.Chat.State, overview.Chat.Default!.Deployment));
        Assert.Equal(TrainerModelState.None, overview.Embedding.State);
        Assert.False(overview.Simulated);

        Assert.False(sealedConfiguration.Simulated);
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 15, 10, 0, TimeSpan.Zero), sealedConfiguration.ExpiresAt);
        Assert.Equal(TrainerModelState.Applicable, sealedConfiguration.Overview!.Chat.State);
        AdminSealedConfiguration admin = sealedConfiguration.Payload.Admin!;
        Assert.Equal(("2026-10-25T15:10:00Z", UserId), (admin.Envelope.ExpiresAt, admin.DevId));

        // The envelope of this answer is not a valid one (6-byte ciphertext): refused before any derivation and any unlock call.
        Assert.Equal(TrainerConfigurationErrorKind.InvalidResponse, unlock.Error!.Kind);
        Assert.Empty(server.RequestsTo("/api/v1/model-configs/unlock"));
    }

    [Fact]
    public async Task The_admin_dashboard_errors_reach_the_caller_with_their_details()
    {
        _handler.Always(_ => FakeAdminHandler.Reply(429, """{"error":"rateLimited","message":"?","details":{"reason":"tooManyEnvelopes","retryAfterSeconds":40}}""", retryAfterSeconds: 40));
        TrainerConfigurationService service = Service();

        TrainerConfigurationError error = (await service.RetrieveAsync(Token)).Error!;

        Assert.Equal((TrainerConfigurationErrorKind.RateLimited, "tooManyEnvelopes", 40, 429), (error.Kind, error.Reason, error.RetryAfterSeconds, error.HttpStatus));
        string json = JsonSerializer.Serialize(error, ApiJson);
        Assert.Contains("\"kind\":\"rateLimited\"", json, StringComparison.Ordinal);
        Assert.Contains("\"retryAfterSeconds\":40", json, StringComparison.Ordinal);
        Assert.Contains("\"reason\":\"tooManyEnvelopes\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("credential", json, StringComparison.Ordinal);
        Assert.Equal("TrainerConfigurationError { Kind = RateLimited, Missing = [], RetryAfterSeconds = 40, Reason = tooManyEnvelopes, HttpStatus = 429 }", error.ToString());
    }

    [Theory]
    [InlineData(null, AdminApiKey, null, "serverUrl")]
    [InlineData("  ", AdminApiKey, null, "serverUrl")]
    [InlineData(AdminUrl, null, null, "apiKey")]
    [InlineData(AdminUrl, " ", "", "apiKey")]
    [InlineData(null, null, null, "serverUrl,apiKey")]
    [InlineData("http://labs-admin.example.com", AdminApiKey, null, "serverUrl")]
    public async Task A_missing_admin_url_or_api_key_answers_not_configured_naming_what_is_missing(
        string? serverUrl, string? configuredKey, string? keyVariable, string expected)
    {
        TrainerConfigurationService service = Service(serverUrl, configuredKey, keyVariable);

        TrainerOutcome<SealedTrainerConfiguration> outcome = await service.RetrieveAsync(Token);

        Assert.Equal(TrainerConfigurationErrorKind.NotConfigured, outcome.Error!.Kind);
        Assert.Equal(expected.Split(','), outcome.Error.Missing);
        Assert.Equal(expected.Split(','), service.MissingConfiguration);
        Assert.Equal(TrainerConfigurationErrorKind.NotConfigured, (await service.GetOverviewAsync(Token)).Error!.Kind);
        Assert.Equal([.. expected.Split(','), "workshop"], Service(serverUrl, configuredKey, keyVariable, joined: false).MissingConfiguration);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task The_admin_api_key_is_resolved_like_the_reporting_the_environment_variable_counts_and_wins()
    {
        TrainerConfigurationService fromVariable = Service(AdminUrl, configuredKey: "from-file-loses", keyVariable: AdminApiKey);

        TrainerOutcome<SealedTrainerConfiguration> outcome = await fromVariable.RetrieveAsync(Token);

        Assert.Equal(TrainerConfigurationErrorKind.NotFound, outcome.Error!.Kind);
        CapturedRequest request = Assert.Single(_handler.Requests);
        Assert.Equal((AdminApiKey, DevToken), (request.ApiKey, request.Bearer));
        Assert.Equal("https://labs-admin.example.com/api/v1/model-configs?sealed=1", request.Uri.ToString());
        Assert.Equal("from-variable", ReportingClient.ResolveApiKey(
            new ReportingOptions { ApiKey = "from-file" }, name => name == ReportingClient.ApiKeyVariable ? " from-variable " : null));
        Assert.Equal("from-file", ReportingClient.ResolveApiKey(new ReportingOptions { ApiKey = " from-file " }, _ => null));
        Assert.Null(ReportingClient.ResolveServerUrl(new ReportingOptions { ServerUrl = " " }));
    }

    [Fact]
    public async Task The_mock_wins_over_a_missing_admin_configuration()
    {
        TrainerConfigurationService service = Service(serverUrl: null, configuredKey: null, mock: Mock());

        TrainerOutcome<SealedTrainerConfiguration> outcome = await service.RetrieveAsync(Token);

        Assert.True(outcome.Succeeded);
        Assert.True(service.Simulated);
        Assert.Equal(["serverUrl", "apiKey"], service.MissingConfiguration);
        Assert.True((await Service(serverUrl: null, configuredKey: null, mock: Mock(), joined: false).GetOverviewAsync(Token)).Succeeded);
    }

    [Fact]
    public async Task The_admin_api_key_of_an_invitation_is_read_at_each_call_and_the_environment_variable_still_wins()
    {
        string? stored = null;
        TrainerConfigurationService service = Service(configuredKey: null, storedKey: () => stored);

        TrainerOutcome<TrainerModelsOverview> before = await service.GetOverviewAsync(Token);
        Assert.Equal(TrainerConfigurationErrorKind.NotConfigured, before.Error!.Kind);
        Assert.Equal([TrainerConfigurationService.ApiKeySetting], before.Error.Missing);
        Assert.Empty(_handler.Requests);

        // The candidate pastes the trainer's invitation: the next call sends its key, without a restart (Rule 22).
        stored = "lbk_from_invitation";
        Assert.Empty(service.MissingConfiguration);
        Assert.Equal(TrainerConfigurationErrorKind.NotFound, (await service.GetOverviewAsync(Token)).Error!.Kind);
        Assert.Equal("lbk_from_invitation", Assert.Single(_handler.Requests).ApiKey);

        // Resolution order: LABS_ADMIN_API_KEY, then the invitation in the user secrets, then appsettings.
        TrainerConfigurationService overridden = Service(configuredKey: "lbk_from_file", keyVariable: AdminApiKey, storedKey: () => "lbk_from_invitation");
        await overridden.GetOverviewAsync(Token);
        Assert.Equal(AdminApiKey, _handler.Requests.Last().ApiKey);
        ReportingOptions configured = new() { ApiKey = "lbk_from_file" };
        Assert.Equal(("lbk_from_invitation", "dashboard"), ReportingClient.ResolveApiKeyWithSource(configured, _ => null, () => " lbk_from_invitation "));
        Assert.Equal(("lbk_from_file", "appsettings"), ReportingClient.ResolveApiKeyWithSource(configured, _ => null, () => null));
        Assert.Equal(((string?)null, "missing"), ReportingClient.ResolveApiKeyWithSource(new ReportingOptions(), _ => null, () => " "));
    }

    [Fact]
    public async Task The_service_is_built_by_dependency_injection_as_Program_registers_it_and_reads_the_identity_store()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"labbench-trainer-di-{Guid.NewGuid():N}");
        try
        {
            IdentityStore identity = new(Path.Combine(directory, "identity.json"));
            AzureOpenAISettingsStore secrets = new(Path.Combine(directory, "secrets.json"), "test", [], () => new Dictionary<string, string>());
            await using ServiceProvider services = new ServiceCollection()
                .AddLogging()
                .AddSingleton(new DashboardOptions { Reporting = { ServerUrl = AdminUrl } })
                .AddSingleton(identity)
                .AddSingleton(secrets)
                .AddSingleton<TrainerConfigurationService>()
                .BuildServiceProvider();

            TrainerConfigurationService service = services.GetRequiredService<TrainerConfigurationService>();

            Assert.False(service.Simulated);
            Assert.Equal(["apiKey", "workshop"], service.MissingConfiguration);
            identity.Create("john-dev");
            Assert.Equal(["apiKey", "workshop"], service.MissingConfiguration);
            identity.Update(current => current with { DevToken = DevToken });
            Assert.Equal(["apiKey"], service.MissingConfiguration);

            // The admin API key of the trainer's invitation, saved in the user secrets by the reporting: read at once, no restart (Rule 22).
            await secrets.SetEntryAsync(ReportingClient.WorkshopKeySection, ReportingClient.ApiKeyName, AdminApiKey);
            Assert.Empty(service.MissingConfiguration);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    // ---------- mock configuration ----------

    [Fact]
    public void The_mock_is_off_by_default_and_in_the_committed_appsettings()
    {
        Assert.False(new DashboardOptions().TrainerModels.Mock.Enabled);

        IConfiguration committed = new ConfigurationBuilder().AddJsonFile(Path.Combine(DashboardProjectDirectory, "appsettings.json")).Build();
        DashboardOptions options = committed.GetSection(DashboardOptions.SectionName).Get<DashboardOptions>()!;

        Assert.False(options.TrainerModels.Mock.Enabled);
        Assert.Equal(MockTrainerConfiguration.DefaultCode, options.TrainerModels.Mock.Code);
        Assert.True(options.TrainerModels.Mock.ProvideEmbedding);
        Assert.Null(options.TrainerModels.Mock.RetrieveError);
        Assert.Null(options.TrainerModels.Mock.UnlockError);
    }

    [Fact]
    public void The_mock_is_configured_from_the_dashboard_section_and_an_unknown_error_kind_is_refused_at_startup()
    {
        DashboardOptions options = Bind(new()
        {
            ["Dashboard:TrainerModels:Mock:Enabled"] = "true",
            ["Dashboard:TrainerModels:Mock:Code"] = OtherCode,
            ["Dashboard:TrainerModels:Mock:ProvideEmbedding"] = "false",
            ["Dashboard:TrainerModels:Mock:RetrieveError"] = "network",
            ["Dashboard:TrainerModels:Mock:UnlockError"] = "invalidResponse",
        });

        TrainerModelsMockOptions mock = options.TrainerModels.Mock;
        Assert.True(mock.Enabled);
        Assert.Equal(OtherCode, mock.Code);
        Assert.False(mock.ProvideEmbedding);
        Assert.Equal(TrainerConfigurationErrorKind.Network, mock.RetrieveError);
        Assert.Equal(TrainerConfigurationErrorKind.InvalidResponse, mock.UnlockError);

        Assert.Throws<InvalidOperationException>(() => Bind(new() { ["Dashboard:TrainerModels:Mock:RetrieveError"] = "offline" }));
    }

    // ---------- mock flow ----------

    [Fact]
    public async Task With_the_mock_the_overview_and_the_retrieved_configuration_describe_the_simulated_defaults()
    {
        TrainerConfigurationService service = Service(mock: Mock());

        TrainerModelsOverview overview = (await service.GetOverviewAsync(Token)).Value!;
        SealedTrainerConfiguration sealedConfiguration = (await service.RetrieveAsync(Token)).Value!;
        TrainerModelsOverview llmOnly = (await Service(mock: Mock(provideEmbedding: false)).GetOverviewAsync(Token)).Value!;
        TrainerOutcome<TrainerModelsOverview> failing = await Service(mock: Mock(retrieveError: TrainerConfigurationErrorKind.Forbidden)).GetOverviewAsync(Token);

        Assert.True(overview.Simulated);
        Assert.Equal((TrainerModelState.Applicable, MockTrainerConfiguration.ChatDeployment, true), (overview.Chat.State, overview.Chat.Default!.Deployment, overview.Chat.Default.KeyProvided));
        Assert.Equal((TrainerModelState.Applicable, 1536), (overview.Embedding.State, overview.Embedding.Default!.Dimensions));
        Assert.Empty(overview.Embedding.Warnings);
        Assert.Equal("trainer-mock.invalid", overview.Chat.Default.EndpointHost);
        Assert.Equal(JsonSerializer.Serialize(overview, ApiJson), JsonSerializer.Serialize(sealedConfiguration.Overview, ApiJson));
        Assert.Null(sealedConfiguration.ExpiresAt);
        Assert.Equal(TrainerModelState.None, llmOnly.Embedding.State);
        Assert.Equal(TrainerConfigurationErrorKind.Forbidden, failing.Error!.Kind);
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task With_the_mock_retrieve_then_unlock_with_its_code_returns_the_simulated_llm_and_embedding_profiles()
    {
        TrainerConfigurationService service = Service(mock: Mock());

        TrainerOutcome<SealedTrainerConfiguration> retrieved = await service.RetrieveAsync(Token);
        Assert.True(retrieved.Succeeded);
        Assert.True(retrieved.Value.Simulated);
        Assert.Equal(_clock.Now, retrieved.Value.RetrievedAt);

        TrainerUnlockResult result = await service.UnlockAsync(retrieved.Value, MockTrainerConfiguration.DefaultCode, Token);

        Assert.Equal(TrainerUnlockStatus.Unlocked, result.Status);
        Assert.Null(result.Error);
        TrainerConfigurationBundle bundle = result.Bundle!;
        Assert.True(bundle.Simulated);
        Assert.Equal(_clock.Now, bundle.RetrievedAt);
        Assert.Equal([ModelKind.Chat, ModelKind.Embedding], bundle.Profiles.Select(p => p.Kind));
        Assert.All(bundle.Profiles, profile =>
        {
            // Obviously fake, yet complete and valid Azure OpenAI profiles: the store accepts them as they are.
            Assert.Equal(AzureOpenAISettingsStore.AzureOpenAIProvider, profile.Provider);
            Assert.EndsWith("trainer-mock.invalid", new Uri(profile.Endpoint).Host, StringComparison.Ordinal);
            Assert.StartsWith("mock-", profile.Deployment, StringComparison.Ordinal);
            Assert.StartsWith("mock-", profile.ApiKey, StringComparison.Ordinal);
            Assert.Null(AzureOpenAISettingsStore.ValidateEndpoint(profile.Endpoint));
            Assert.Null(AzureOpenAISettingsStore.ValidateDeployment(profile.Deployment));
            Assert.Null(AzureOpenAISettingsStore.ValidateApiKey(profile.ApiKey!));
        });
        Assert.Equal(MockTrainerConfiguration.ChatDeployment, bundle.Chat!.Deployment);
        Assert.Equal(MockTrainerConfiguration.EmbeddingDeployment, bundle.Embedding!.Deployment);
        Assert.NotEqual(bundle.Chat.Endpoint, bundle.Embedding.Endpoint);
    }

    [Fact]
    public async Task An_unlocked_bundle_converts_to_the_trainer_profile_updates_of_the_store()
    {
        TrainerConfigurationBundle bundle = await UnlockedMockAsync(Mock());

        IReadOnlyList<TrainerProfileUpdate> updates = bundle.ToProfileUpdates([ModelKind.Chat]);

        Assert.Collection(updates,
            chat =>
            {
                Assert.Equal(new TrainerProfileUpdate(ModelKind.Chat, "azure-openai", MockTrainerConfiguration.ChatEndpoint,
                    MockTrainerConfiguration.ChatDeployment, MockTrainerConfiguration.ChatApiKey, _clock.Now, Use: true)
                {
                    ConfigId = MockTrainerConfiguration.ChatConfigId,
                    ConfigName = MockTrainerConfiguration.ChatConfigName,
                    ConfigUpdatedAt = MockTrainerConfiguration.ConfigUpdatedAt,
                }, chat);
            },
            embedding =>
            {
                Assert.Equal(new TrainerProfileUpdate(ModelKind.Embedding, "azure-openai", MockTrainerConfiguration.EmbeddingEndpoint,
                    MockTrainerConfiguration.EmbeddingDeployment, MockTrainerConfiguration.EmbeddingApiKey, _clock.Now, Use: false)
                {
                    ConfigId = MockTrainerConfiguration.EmbeddingConfigId,
                    ConfigName = MockTrainerConfiguration.EmbeddingConfigName,
                    ConfigUpdatedAt = MockTrainerConfiguration.ConfigUpdatedAt,
                }, embedding);
            });
    }

    [Fact]
    public async Task A_wrong_code_returns_invalid_code_and_the_right_code_still_unlocks_afterwards()
    {
        TrainerConfigurationService service = Service(mock: Mock());
        SealedTrainerConfiguration sealedConfiguration = (await service.RetrieveAsync(Token)).Value!;

        TrainerUnlockResult wrong = await service.UnlockAsync(sealedConfiguration, OtherCode, Token);
        TrainerUnlockResult right = await service.UnlockAsync(sealedConfiguration, MockTrainerConfiguration.DefaultCode, Token);

        Assert.Equal(TrainerUnlockStatus.Failed, wrong.Status);
        Assert.Equal(TrainerConfigurationErrorKind.InvalidCode, wrong.Error!.Kind);
        Assert.Null(wrong.Bundle);
        Assert.Equal(TrainerUnlockStatus.Unlocked, right.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("K7QX")]
    [InlineData("K7QX99")]
    [InlineData("0OIL1")]
    [InlineData("K7QX!")]
    [InlineData("K7 QX 99")]
    [InlineData("K7QX9\n9")]
    [InlineData("123456")]
    [InlineData("Ｋ７ＱＸ９")]
    public async Task A_code_that_is_not_five_characters_of_the_alphabet_is_refused_before_anything_else(string? code)
    {
        SpyAdapter adapter = new([new TrainerModelProfile(ModelKind.Chat, "azure-openai", "https://a.example.com/", "gpt", null)]);
        TrainerConfigurationService service = new(adapter, adapter, _logger, time: _clock);
        SealedTrainerConfiguration sealedConfiguration = (await service.RetrieveAsync(Token)).Value!;

        TrainerUnlockResult result = await service.UnlockAsync(sealedConfiguration, code, Token);

        Assert.Equal(TrainerUnlockStatus.InvalidCodeFormat, result.Status);
        Assert.Null(result.Error);
        Assert.Null(result.Bundle);
        Assert.Equal(0, adapter.UnsealCalls);
        Assert.False(TrainerConfigurationService.IsValidCode(code));
    }

    [Theory]
    [MemberData(nameof(ErrorKinds))]
    public async Task The_mock_simulates_each_error_kind_on_retrieve(TrainerConfigurationErrorKind kind)
    {
        TrainerConfigurationService service = Service(mock: Mock(retrieveError: kind));

        TrainerOutcome<SealedTrainerConfiguration> outcome = await service.RetrieveAsync(Token);

        Assert.False(outcome.Succeeded);
        Assert.Equal(kind, outcome.Error!.Kind);
        string[] missing = kind == TrainerConfigurationErrorKind.NotConfigured ? ["serverUrl", "apiKey"] : [];
        Assert.Equal(missing, outcome.Error.Missing);
    }

    [Theory]
    [MemberData(nameof(ErrorKinds))]
    public async Task The_mock_simulates_each_error_kind_on_unlock_even_with_the_right_code(TrainerConfigurationErrorKind kind)
    {
        TrainerModelsMockOptions mock = Mock();
        TrainerConfigurationService service = Service(mock: mock);
        SealedTrainerConfiguration sealedConfiguration = (await service.RetrieveAsync(Token)).Value!;
        mock.UnlockError = kind;

        TrainerUnlockResult result = await service.UnlockAsync(sealedConfiguration, MockTrainerConfiguration.DefaultCode, Token);

        Assert.Equal(TrainerUnlockStatus.Failed, result.Status);
        Assert.Equal(kind, result.Error!.Kind);
        Assert.Null(result.Bundle);
    }

    [Fact]
    public async Task A_simulated_network_error_can_be_retried_once_the_cause_is_gone()
    {
        TrainerModelsMockOptions mock = Mock(retrieveError: TrainerConfigurationErrorKind.Network);
        TrainerConfigurationService service = Service(mock: mock);

        TrainerOutcome<SealedTrainerConfiguration> failed = await service.RetrieveAsync(Token);
        mock.RetrieveError = null;
        TrainerOutcome<SealedTrainerConfiguration> retried = await service.RetrieveAsync(Token);

        Assert.Equal(TrainerConfigurationErrorKind.Network, failed.Error!.Kind);
        Assert.True(retried.Succeeded);
    }

    [Fact]
    public async Task The_mock_can_provide_only_the_llm()
    {
        TrainerConfigurationBundle bundle = await UnlockedMockAsync(Mock(provideEmbedding: false));

        TrainerModelProfile chat = Assert.Single(bundle.Profiles);
        Assert.Equal(ModelKind.Chat, chat.Kind);
        Assert.Same(chat, bundle.Chat);
        Assert.Null(bundle.Embedding);
        Assert.Equal(ModelKind.Chat, Assert.Single(bundle.ToProfileUpdates([ModelKind.Chat, ModelKind.Embedding])).Kind);
    }

    [Fact]
    public async Task A_configured_mock_code_replaces_the_default_and_an_invalid_one_falls_back_to_it_without_being_logged()
    {
        TrainerConfigurationService configured = Service(mock: Mock(code: OtherCode));
        SealedTrainerConfiguration sealedConfiguration = (await configured.RetrieveAsync(Token)).Value!;
        Assert.Equal(TrainerConfigurationErrorKind.InvalidCode, (await configured.UnlockAsync(sealedConfiguration, MockTrainerConfiguration.DefaultCode, Token)).Error!.Kind);
        Assert.Equal(TrainerUnlockStatus.Unlocked, (await configured.UnlockAsync(sealedConfiguration, OtherCode, Token)).Status);

        TrainerConfigurationService invalid = Service(mock: Mock(code: "0OIL1"));
        SealedTrainerConfiguration other = (await invalid.RetrieveAsync(Token)).Value!;
        Assert.Equal(TrainerUnlockStatus.Unlocked, (await invalid.UnlockAsync(other, MockTrainerConfiguration.DefaultCode, Token)).Status);
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("Mock:Code", StringComparison.Ordinal));
        Assert.DoesNotContain(_logger.Entries, e => e.Message.Contains("0OIL1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_mock_never_calls_the_network_even_with_a_configured_admin_dashboard()
    {
        FakeAdminServer server = await StartServerAsync();
        TrainerConfigurationService service = Service(server.Url, FakeAdminServer.ApiKey, mock: Mock());

        SealedTrainerConfiguration sealedConfiguration = (await service.RetrieveAsync(Token)).Value!;
        await service.UnlockAsync(sealedConfiguration, OtherCode, Token);
        await service.UnlockAsync(sealedConfiguration, MockTrainerConfiguration.DefaultCode, Token);

        Assert.Empty(server.Requests);
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("Simulated trainer configuration is on", StringComparison.Ordinal));
    }

    // ---------- robustness ----------

    [Fact]
    public async Task A_configuration_sealed_in_another_mode_is_never_unlocked()
    {
        SealedTrainerConfiguration simulated = (await Service(mock: Mock()).RetrieveAsync(Token)).Value!;
        SpyAdapter adapter = new([new TrainerModelProfile(ModelKind.Chat, "azure-openai", "https://a.example.com/", "gpt", null)]);
        TrainerConfigurationService real = new(adapter, adapter, _logger, time: _clock);
        SealedTrainerConfiguration notSimulated = (await real.RetrieveAsync(Token)).Value!;

        TrainerUnlockResult mockPayloadOnRealService = await real.UnlockAsync(simulated, MockTrainerConfiguration.DefaultCode, Token);
        TrainerUnlockResult realPayloadOnMock = await Service(mock: Mock()).UnlockAsync(notSimulated, MockTrainerConfiguration.DefaultCode, Token);

        Assert.Equal(TrainerConfigurationErrorKind.InvalidResponse, mockPayloadOnRealService.Error!.Kind);
        Assert.Equal(TrainerConfigurationErrorKind.InvalidResponse, realPayloadOnMock.Error!.Kind);
        Assert.Equal(0, adapter.UnsealCalls);
    }

    [Fact]
    public async Task Unusable_content_is_an_invalid_response()
    {
        TrainerModelProfile chat = new(ModelKind.Chat, "azure-openai", "https://a.example.com/", "gpt", null);
        TrainerModelProfile[][] unusable =
        [
            [],
            [chat, chat with { Deployment = "other" }],
            [chat with { Endpoint = " " }],
            [chat with { Deployment = "" }],
            [chat with { Provider = "" }],
            [chat with { Kind = (ModelKind)42 }],
        ];

        foreach (TrainerModelProfile[] profiles in unusable)
        {
            SpyAdapter adapter = new(profiles);
            TrainerConfigurationService service = new(adapter, adapter, _logger, time: _clock);
            SealedTrainerConfiguration sealedConfiguration = (await service.RetrieveAsync(Token)).Value!;

            TrainerUnlockResult result = await service.UnlockAsync(sealedConfiguration, MockTrainerConfiguration.DefaultCode, Token);

            Assert.Equal(TrainerConfigurationErrorKind.InvalidResponse, result.Error!.Kind);
        }

        SpyAdapter empty = new([chat], payload: []);
        TrainerOutcome<SealedTrainerConfiguration> emptyPayload = await new TrainerConfigurationService(empty, empty, _logger).RetrieveAsync(Token);
        Assert.Equal(TrainerConfigurationErrorKind.InvalidResponse, emptyPayload.Error!.Kind);
    }

    [Fact]
    public async Task An_adapter_that_throws_gives_unknown_logged_without_its_message_and_a_cancelled_caller_gets_the_cancellation()
    {
        SpyAdapter throwing = new([], failure: new InvalidOperationException($"could not read {MockTrainerConfiguration.ChatApiKey}"));
        TrainerConfigurationService service = new(throwing, throwing, _logger, time: _clock);
        SealedTrainerConfiguration sealedConfiguration = new(new SealedTrainerPayload([1, 2, 3]), _clock.Now, simulated: false);

        TrainerOutcome<SealedTrainerConfiguration> retrieved = await service.RetrieveAsync(Token);
        TrainerUnlockResult unlocked = await service.UnlockAsync(sealedConfiguration, MockTrainerConfiguration.DefaultCode, Token);

        Assert.Equal(TrainerConfigurationErrorKind.Unknown, retrieved.Error!.Kind);
        Assert.Equal(TrainerConfigurationErrorKind.Unknown, unlocked.Error!.Kind);
        Assert.Equal(2, _logger.Entries.Count(e => e.Level == LogLevel.Warning && e.Message.Contains(nameof(InvalidOperationException), StringComparison.Ordinal)));
        Assert.DoesNotContain(_logger.Entries, e => e.Message.Contains(MockTrainerConfiguration.ChatApiKey, StringComparison.Ordinal));

        // A timeout inside an adapter is not the caller's cancellation: still a result.
        SpyAdapter timingOut = new([], failure: new TaskCanceledException());
        Assert.Equal(TrainerConfigurationErrorKind.Unknown, (await new TrainerConfigurationService(timingOut, timingOut, _logger).RetrieveAsync(Token)).Error!.Kind);

        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();
        TrainerConfigurationService mock = Service(mock: Mock());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => mock.RetrieveAsync(cancelled.Token));
    }

    [Fact]
    public async Task No_code_payload_or_key_appears_in_the_text_or_the_json_of_the_results_nor_in_the_logs()
    {
        TrainerConfigurationService service = Service(mock: Mock());
        TrainerOutcome<SealedTrainerConfiguration> retrieved = await service.RetrieveAsync(Token);
        TrainerUnlockResult wrong = await service.UnlockAsync(retrieved.Value!, OtherCode, Token);
        TrainerUnlockResult invalid = await service.UnlockAsync(retrieved.Value!, "0OIL1", Token);
        TrainerUnlockResult result = await service.UnlockAsync(retrieved.Value!, MockTrainerConfiguration.DefaultCode, Token);
        TrainerConfigurationBundle bundle = result.Bundle!;

        string[] texts =
        [
            retrieved.ToString(), retrieved.Value!.ToString(), retrieved.Value.Payload.ToString(),
            wrong.ToString(), invalid.ToString(), result.ToString(), bundle.ToString(),
            bundle.Chat!.ToString(), bundle.Embedding!.ToString(), wrong.Error!.ToString(),
            string.Join(" | ", bundle.ToProfileUpdates([ModelKind.Chat, ModelKind.Embedding])),
            JsonSerializer.Serialize(retrieved.Value, ApiJson), JsonSerializer.Serialize(result, ApiJson), JsonSerializer.Serialize(bundle, ApiJson),
            .. _logger.Entries.Select(e => e.Message),
        ];
        string[] secrets =
        [
            MockTrainerConfiguration.ChatApiKey, MockTrainerConfiguration.EmbeddingApiKey, "mock-llm-key", "mock-embedding-key",
            MockTrainerConfiguration.DefaultCode, OtherCode, "0OIL1", "labbench-mock-trainer-configuration",
        ];

        foreach (string text in texts)
        {
            foreach (string secret in secrets)
            {
                Assert.DoesNotContain(secret, text, StringComparison.Ordinal);
            }
        }

        // What the review may show stays visible.
        Assert.Contains(MockTrainerConfiguration.ChatDeployment, bundle.ToString() + bundle.Chat, StringComparison.Ordinal);
        Assert.Contains("ApiKey = set", bundle.Chat.ToString(), StringComparison.Ordinal);
    }

    // ---------- helpers ----------

    /// <summary>A service as Program builds it, the candidate joined unless <paramref name="joined"/> is false, the admin behind <see cref="_handler"/>.</summary>
    private TrainerConfigurationService Service(
        string? serverUrl = AdminUrl, string? configuredKey = AdminApiKey, string? keyVariable = null, TrainerModelsMockOptions? mock = null, bool joined = true,
        Func<string?>? storedKey = null)
    {
        DashboardOptions options = new()
        {
            Reporting = { ServerUrl = serverUrl, ApiKey = configuredKey },
            TrainerModels = { Mock = mock ?? new TrainerModelsMockOptions() },
        };
        DevIdentity? current = joined ? Joined : null;
        return Track(new TrainerConfigurationService(options, () => current, _logger, _clock,
            name => name == ReportingClient.ApiKeyVariable ? keyVariable : null, _handler, storedKey));
    }

    private TrainerConfigurationService Track(TrainerConfigurationService service)
    {
        _services.Add(service);
        return service;
    }

    /// <summary>A sealed GET answer of the admin with one Azure OpenAI LLM default (and its envelope when <paramref name="sealedEnvelope"/>).</summary>
    private static string AdminAnswer(bool sealedEnvelope)
    {
        string envelope = sealedEnvelope
            ? """
              { "version": 1, "envelopeId": "7d3e5c1a-9b2f-4e8d-a6c4-0f1e2d3c4b5a", "expiresAt": "2026-10-25T15:10:00Z", "cipher": "AES-256-GCM",
                "iv": "AAECAwQFBgcICQoL", "ciphertext": "c2VhbGVk", "kdf": { "name": "PBKDF2-HMAC-SHA256", "iterations": 600000, "salt": "AAECAwQFBgcICQoLDA0ODw" },
                "code": { "length": 5, "alphabet": "ABCDEFGHJKMNPQRSTUVWXYZ23456789" }, "attemptsAllowed": 3 }
              """
            : "null";
        return $$"""
            {
              "configs": [
                { "id": "11111111-1111-4111-8111-111111111111", "name": "Workshop GPT-4o mini", "kind": "llm", "provider": "azure-openai",
                  "settings": { "endpoint": "https://workshop-aoai.openai.azure.com/", "deployment": "gpt-4o-mini", "model": "gpt-4o-mini" },
                  "parameters": {}, "secretFields": ["apiKey"], "isDefault": true, "embedding": null, "updatedAt": "2026-10-25T09:30:00.000Z" }
              ],
              "defaults": { "llm": "11111111-1111-4111-8111-111111111111", "embedding": null },
              "sealed": {{envelope}},
              "sealedConfigIds": {{(sealedEnvelope ? "[\"11111111-1111-4111-8111-111111111111\"]" : "[]")}},
              "unsealedConfigs": [],
              "sealedUnavailableReason": {{(sealedEnvelope ? "null" : "\"notRequested\"")}},
              "serverTime": "2026-10-25T15:00:00.000Z"
            }
            """;
    }

    private static TrainerModelsMockOptions Mock(
        string code = MockTrainerConfiguration.DefaultCode, bool provideEmbedding = true, TrainerConfigurationErrorKind? retrieveError = null) =>
        new() { Enabled = true, Code = code, ProvideEmbedding = provideEmbedding, RetrieveError = retrieveError };

    private async Task<TrainerConfigurationBundle> UnlockedMockAsync(TrainerModelsMockOptions mock)
    {
        TrainerConfigurationService service = Service(mock: mock);
        SealedTrainerConfiguration sealedConfiguration = (await service.RetrieveAsync(Token)).Value!;
        TrainerUnlockResult result = await service.UnlockAsync(sealedConfiguration, mock.Code, Token);
        return result.Bundle ?? throw new InvalidOperationException($"Not unlocked: {result}");
    }

    private static DashboardOptions Bind(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build().GetSection(DashboardOptions.SectionName).Get<DashboardOptions>()!;

    private async Task<FakeAdminServer> StartServerAsync()
    {
        FakeAdminServer server = await FakeAdminServer.StartAsync();
        _servers.Add(server);
        return server;
    }

    /// <summary>A source and decryptor that return fixed values (or throw), and count the unseal calls.</summary>
    private sealed class SpyAdapter(IReadOnlyList<TrainerModelProfile> profiles, byte[]? payload = null, Exception? failure = null)
        : ITrainerConfigurationSource, ITrainerConfigurationDecryptor
    {
        public int UnsealCalls { get; private set; }

        public Task<TrainerOutcome<SealedTrainerPayload>> RetrieveAsync(CancellationToken cancellationToken) =>
            failure is not null
                ? Task.FromException<TrainerOutcome<SealedTrainerPayload>>(failure)
                : Task.FromResult(TrainerOutcome<SealedTrainerPayload>.Success(new SealedTrainerPayload(payload ?? [0x2A])));

        public ValueTask<TrainerUnsealResult> UnsealAsync(SealedTrainerPayload sealedPayload, string code, CancellationToken cancellationToken)
        {
            UnsealCalls++;
            return failure is not null
                ? ValueTask.FromException<TrainerUnsealResult>(failure)
                : ValueTask.FromResult(TrainerUnsealResult.Success(profiles));
        }
    }

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 25, 15, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed record LogEntry(LogLevel Level, string Message);

    /// <summary>Keeps every log entry, formatted, to check what reaches the console.</summary>
    private sealed class ListLogger : ILogger<TrainerConfigurationService>
    {
        public ConcurrentQueue<LogEntry> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Enqueue(new LogEntry(logLevel, formatter(state, exception)));
    }
}
