using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LabDashboard.Reporting;
using LabDashboard.Settings;
using LabDashboard.TrainerModels;
using Microsoft.Extensions.Logging;

namespace LabDashboard.Tests;

/// <summary>
/// T5: the trainer flow of the AI models tab (Business Rules 9, 13, 14, 17, 19, 20) through <see cref="TrainerUnlockFlow"/>, the real
/// <see cref="TrainerConfigurationService"/> and the real envelope protocol, against <see cref="FakeModelConfigServer"/>: overview without a code, one
/// envelope held per Unlock, the keys saved for the ticked kinds only, the custom profiles untouched, every error saving nothing, and no secret in
/// any answer or log. A temporary user-secrets file, never the machine's; never the network.
/// </summary>
public sealed class TrainerUnlockFlowTests : IDisposable
{
    private const string AdminUrl = "https://labs-admin.example.com";
    private const string AdminApiKey = "lbk_test_unlock_flow_admin_api_key_8b2d";
    private const string DevToken = "tok-unlock-flow-dev-token-6e1f";
    private const string LlmId = "aaaaaaaa-1111-4111-8111-aaaaaaaaaaaa";
    private const string EmbeddingId = "bbbbbbbb-2222-4222-8222-bbbbbbbbbbbb";
    private const string LlmEndpoint = "https://workshop-llm.openai.azure.com/";
    private const string EmbeddingEndpoint = "https://workshop-embed.openai.azure.com/";
    private const string Code = FakeModelConfigServer.VectorCode;
    private const string WrongCode = "M4PRS";

    private static readonly DevIdentity Joined = new() { UserId = FakeModelConfigServer.VectorDevId, Username = "john-dev", DevToken = DevToken };

    /// <summary>The JSON options of the dashboard API (Program.cs).</summary>
    private static readonly JsonSerializerOptions ApiJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _root = Directory.CreateTempSubdirectory("labbench-trainer-unlock-").FullName;
    private readonly FakeAdminHandler _handler = new();
    private readonly FakeModelConfigServer _server;
    private readonly ListLogger _logger = new();
    private readonly ManualClock _clock = new();
    private readonly AzureOpenAISettingsStore _settings;
    private readonly List<IDisposable> _owned = [];

    public TrainerUnlockFlowTests()
    {
        _server = new FakeModelConfigServer(_handler)
        {
            VectorFirst = false,
            Configs =
            [
                FakeModelConfigServer.Config(LlmId, "llm", "Workshop GPT", LlmEndpoint, "gpt-4o-mini", updatedAt: "2026-10-25T09:00:00.000Z"),
                FakeModelConfigServer.Config(EmbeddingId, "embedding", "Workshop embeddings", EmbeddingEndpoint, "text-embedding-3-small", updatedAt: "2026-10-25T09:05:00.000Z"),
            ],
            DefaultLlm = LlmId,
            DefaultEmbedding = EmbeddingId,
            SealedIds = [LlmId, EmbeddingId],
        };
        _settings = new AzureOpenAISettingsStore(SecretsFile, "test", [], () => new Dictionary<string, string>());
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string SecretsFile => Path.Combine(_root, "secrets.json");

    public void Dispose()
    {
        foreach (IDisposable owned in _owned)
        {
            owned.Dispose();
        }

        _handler.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    // ---------- overview ----------

    [Fact]
    public async Task The_overview_shows_the_defaults_without_any_code_and_without_fetching_an_envelope()
    {
        TrainerUnlockFlow flow = Flow();

        TrainerModelsView view = await flow.GetOverviewAsync(Token);

        Assert.Null(view.Error);
        Assert.False(view.Simulated);
        Assert.Equal((TrainerModelState.Applicable, LlmId, "workshop-llm.openai.azure.com"),
            (view.Overview!.Chat.State, view.Overview.Chat.Default!.Id, view.Overview.Chat.Default.EndpointHost));
        Assert.Equal(TrainerModelState.Applicable, view.Overview.Embedding.State);
        Assert.False(view.Overview.Chat.UpdatedSinceUnlock);
        Assert.Equal(TrainerUnlockView.Idle, view.Unlock.Status);
        Assert.Empty(_server.SealedRequests);
        Assert.Empty(_server.UnlockRequests);
    }

    [Fact]
    public async Task Without_the_workshop_the_overview_says_what_is_missing_and_nothing_is_called()
    {
        TrainerUnlockFlow flow = Flow(joined: false);

        TrainerModelsView view = await flow.GetOverviewAsync(Token);

        Assert.Equal(TrainerConfigurationErrorKind.NotConfigured, view.Error!.Kind);
        Assert.Equal([TrainerConfigurationService.WorkshopSetting], view.Error.Missing);
        Assert.Null(view.Overview);
        Assert.Empty(_handler.Requests);
    }

    // ---------- the happy path ----------

    [Fact]
    public async Task Unlock_then_the_right_code_saves_both_trainer_profiles_switches_both_kinds_and_leaves_the_custom_profiles_untouched()
    {
        await _settings.SaveCustomProfileAsync(ModelKind.Chat, new ModelProfileUpdate(null, "https://mine.openai.azure.com/", "my-gpt", "my-own-llm-key-not-a-real-secret-0001"));
        await _settings.SaveCustomProfileAsync(ModelKind.Embedding, new ModelProfileUpdate(null, null, "my-embeddings", null, SameResourceAsLlm: true));
        ModelSettingsStatus before = _settings.GetModelStatus();
        TrainerUnlockFlow flow = Flow();

        TrainerUnlockView started = await flow.StartAsync([ModelKind.Chat, ModelKind.Embedding], Token);

        Assert.Equal(TrainerUnlockView.CodeRequired, started.Status);
        Assert.Equal([ModelKind.Chat, ModelKind.Embedding], started.Kinds);
        Assert.Equal((5, FakeModelConfigServer.Alphabet, 3), (started.CodeLength, started.CodeAlphabet, started.AttemptsLeft));
        Assert.Equal(_clock.Now + TrainerUnlockFlow.MaxHold, started.ExpiresAt);
        Assert.Single(_server.SealedRequests);
        Assert.Empty(_server.UnlockRequests);

        TrainerUnlockView unlocked = await flow.SubmitCodeAsync("k7qx-9", Token);

        Assert.Equal(TrainerUnlockView.Unlocked, unlocked.Status);
        Assert.Null(unlocked.Error);
        ModelSettingsStatus models = unlocked.Models!;
        Assert.Equal((ModelMode.Trainer, ModelMode.Trainer), (models.Chat.Mode, models.Embedding.Mode));
        Assert.Equal((LlmEndpoint, "gpt-4o-mini", true), (models.Chat.Trainer.Endpoint, models.Chat.Trainer.Deployment, models.Chat.Trainer.ApiKey.Stored));
        Assert.Equal((LlmId, "Workshop GPT", "2026-10-25T09:00:00.000Z"), (models.Chat.Trainer.ConfigId, models.Chat.Trainer.ConfigName, models.Chat.Trainer.ConfigUpdatedAt));
        Assert.Equal(_clock.Now, models.Chat.Trainer.RetrievedAt);
        Assert.Equal((EmbeddingEndpoint, "text-embedding-3-small"), (models.Embedding.Trainer.Endpoint, models.Embedding.Trainer.Deployment));

        // What the labs read: the trainer values, keys included; the custom profiles exactly as typed before.
        Assert.Equal(LlmEndpoint, Active(AzureOpenAISettingsStore.EndpointName));
        Assert.Equal(FakeModelConfigServer.ApiKeyOf(LlmId), Active(AzureOpenAISettingsStore.ApiKeyName));
        Assert.Equal(EmbeddingEndpoint, Active(AzureOpenAISettingsStore.EmbeddingEndpointName));
        Assert.Equal(FakeModelConfigServer.ApiKeyOf(EmbeddingId), Active(AzureOpenAISettingsStore.EmbeddingApiKeyName));
        Assert.Equal(Shape(before.Chat.Custom), Shape(models.Chat.Custom));
        Assert.Equal(Shape(before.Embedding.Custom), Shape(models.Embedding.Custom));

        // The envelope is dropped; the opened keys are masked in the run output.
        Assert.Equal(TrainerUnlockView.Idle, flow.GetState().Status);
        Assert.Contains(FakeModelConfigServer.ApiKeyOf(LlmId), _settings.SecretValues());
        Assert.Contains(FakeModelConfigServer.ApiKeyOf(EmbeddingId), _settings.SecretValues());

        // Back to the candidate's own configuration: the values typed before.
        Assert.Null((await _settings.SwitchModeAsync(ModelKind.Chat, "custom")).Error);
        Assert.Equal(("https://mine.openai.azure.com/", "my-own-llm-key-not-a-real-secret-0001"),
            (Active(AzureOpenAISettingsStore.EndpointName), Active(AzureOpenAISettingsStore.ApiKeyName)));
    }

    [Fact]
    public async Task Only_the_ticked_kind_is_saved_and_switched()
    {
        TrainerUnlockFlow flow = Flow();

        await flow.StartAsync([ModelKind.Chat], Token);
        TrainerUnlockView unlocked = await flow.SubmitCodeAsync(Code, Token);

        Assert.Equal(TrainerUnlockView.Unlocked, unlocked.Status);
        Assert.Equal((ModelMode.Trainer, ModelMode.Custom), (unlocked.Models!.Chat.Mode, unlocked.Models.Embedding.Mode));
        Assert.Null(unlocked.Models.Embedding.Trainer.Endpoint);
        Assert.DoesNotContain(FakeModelConfigServer.ApiKeyOf(EmbeddingId), File.ReadAllText(SecretsFile));
    }

    [Fact]
    public async Task A_trainer_change_after_the_unlock_is_flagged_on_the_overview()
    {
        TrainerUnlockFlow flow = Flow();
        await flow.StartAsync([ModelKind.Chat], Token);
        await flow.SubmitCodeAsync(Code, Token);
        Assert.False((await flow.GetOverviewAsync(Token)).Overview!.Chat.UpdatedSinceUnlock);

        _server.Configs[0]["updatedAt"] = "2026-10-25T11:30:00.000Z";
        TrainerModelsOverview overview = (await flow.GetOverviewAsync(Token)).Overview!;

        Assert.True(overview.Chat.UpdatedSinceUnlock);
        Assert.False(overview.Embedding.UpdatedSinceUnlock);
    }

    // ---------- the code ----------

    [Theory]
    [InlineData("0OIL1")]
    [InlineData("K7QX")]
    [InlineData("K7QX99")]
    [InlineData("")]
    [InlineData(null)]
    public async Task A_code_out_of_format_is_refused_without_any_call_and_the_unlock_stays_under_way(string? code)
    {
        TrainerUnlockFlow flow = Flow();
        await flow.StartAsync([ModelKind.Chat], Token);

        TrainerUnlockView view = await flow.SubmitCodeAsync(code, Token);

        Assert.Equal(TrainerUnlockView.CodeRequired, view.Status);
        Assert.Equal((TrainerConfigurationErrorKind.InvalidCode, AdminEnvelopeDecryptor.InvalidCodeFormatReason), (view.Error!.Kind, view.Error.Reason));
        Assert.Empty(_server.UnlockRequests);
        Assert.Equal(TrainerUnlockView.Unlocked, (await flow.SubmitCodeAsync(Code, Token)).Status);
    }

    [Fact]
    public async Task A_wrong_code_shows_the_attempts_left_and_at_zero_the_next_attempt_uses_a_new_envelope()
    {
        TrainerUnlockFlow flow = Flow();
        await flow.StartAsync([ModelKind.Chat], Token);

        int?[] attemptsLeft = new int?[3];
        for (int i = 0; i < 3; i++)
        {
            TrainerUnlockView wrong = await flow.SubmitCodeAsync(WrongCode, Token);
            Assert.Equal((TrainerUnlockView.CodeRequired, TrainerConfigurationErrorKind.InvalidCode), (wrong.Status, wrong.Error!.Kind));
            attemptsLeft[i] = wrong.Error.AttemptsLeft;
        }

        Assert.Equal([2, 1, 0], attemptsLeft);
        Assert.Single(_server.SealedRequests);
        Assert.False(File.Exists(SecretsFile));

        TrainerUnlockView unlocked = await flow.SubmitCodeAsync(Code, Token);

        Assert.Equal(TrainerUnlockView.Unlocked, unlocked.Status);
        Assert.Equal(2, _server.SealedRequests.Count());
        Assert.Equal(_server.IssuedEnvelopes[1].EnvelopeId, JsonNode.Parse(_server.UnlockRequests.Last().Body!)!["envelopeId"]!.GetValue<string>());
    }

    [Fact]
    public async Task Without_an_unlock_under_way_a_code_is_not_sent_anywhere()
    {
        TrainerUnlockFlow flow = Flow();

        TrainerUnlockView view = await flow.SubmitCodeAsync(Code, Token);

        Assert.Equal(TrainerUnlockView.Idle, view.Status);
        Assert.Equal((TrainerConfigurationErrorKind.EnvelopeExpired, TrainerUnlockFlow.NoEnvelopeReason), (view.Error!.Kind, view.Error.Reason));
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task The_held_envelope_is_dropped_at_its_expiry_and_on_cancel_and_nothing_is_written()
    {
        TrainerUnlockFlow flow = Flow();
        await flow.StartAsync([ModelKind.Chat], Token);
        _clock.Now += TrainerUnlockFlow.MaxHold;

        Assert.Equal(TrainerUnlockView.Idle, flow.GetState().Status);
        Assert.Equal(TrainerUnlockFlow.NoEnvelopeReason, (await flow.SubmitCodeAsync(Code, Token)).Error!.Reason);

        await flow.StartAsync([ModelKind.Chat], Token);
        Assert.Equal(TrainerUnlockView.Idle, (await flow.CancelAsync(Token)).Status);
        Assert.Equal(TrainerUnlockFlow.NoEnvelopeReason, (await flow.SubmitCodeAsync(Code, Token)).Error!.Reason);

        Assert.Empty(_server.UnlockRequests);
        Assert.False(File.Exists(SecretsFile));
    }

    [Fact]
    public async Task The_hold_follows_the_admin_clock_so_a_skewed_local_clock_does_not_shorten_it()
    {
        // The local clock is 3 hours ahead of the admin's: the envelope (valid 10 minutes for the admin) is still held for 10 minutes here.
        _clock.Now = new DateTimeOffset(2026, 10, 25, 15, 5, 0, TimeSpan.Zero);
        _server.MutateEnvelope = e => e["expiresAt"] = "2026-10-25T12:10:00.000Z";
        TrainerUnlockFlow flow = Flow();

        TrainerUnlockView started = await flow.StartAsync([ModelKind.Chat], Token);

        Assert.Equal(_clock.Now + TimeSpan.FromMinutes(10), started.ExpiresAt);
    }

    // ---------- Rule 20 ----------

    [Fact]
    public async Task An_expired_envelope_is_replaced_and_the_code_retried_once_without_asking_again()
    {
        _server.UnlockOverrides.Enqueue(() => Gone("expired"));
        TrainerUnlockFlow flow = Flow();
        await flow.StartAsync([ModelKind.Chat], Token);

        TrainerUnlockView view = await flow.SubmitCodeAsync(Code, Token);

        Assert.Equal(TrainerUnlockView.Unlocked, view.Status);
        Assert.Equal(2, _server.SealedRequests.Count());
    }

    [Theory]
    [InlineData("exhausted")]
    [InlineData("used")]
    public async Task An_exhausted_or_used_envelope_asks_for_the_code_again_with_a_new_envelope(string reason)
    {
        _server.UnlockOverrides.Enqueue(() => Gone(reason));
        TrainerUnlockFlow flow = Flow();
        await flow.StartAsync([ModelKind.Chat], Token);

        TrainerUnlockView asked = await flow.SubmitCodeAsync(Code, Token);

        Assert.Equal(TrainerUnlockView.CodeRequired, asked.Status);
        Assert.Equal((TrainerConfigurationErrorKind.EnvelopeExpired, reason), (asked.Error!.Kind, asked.Error.Reason));
        Assert.Single(_server.SealedRequests);
        Assert.Equal(TrainerUnlockView.Unlocked, (await flow.SubmitCodeAsync(Code, Token)).Status);
        Assert.Equal(2, _server.SealedRequests.Count());
    }

    [Theory]
    [InlineData("configChanged")]
    [InlineData("accessCodeChanged")]
    [InlineData("workshopClosed")]
    public async Task Other_410_reasons_end_the_unlock_and_save_nothing(string reason)
    {
        _server.UnlockOverrides.Enqueue(() => Gone(reason));
        TrainerUnlockFlow flow = Flow();
        await flow.StartAsync([ModelKind.Chat], Token);

        TrainerUnlockView view = await flow.SubmitCodeAsync(Code, Token);

        Assert.Equal(TrainerUnlockView.Idle, view.Status);
        Assert.Equal(reason, view.Error!.Reason);
        Assert.False(File.Exists(SecretsFile));
    }

    [Theory]
    [InlineData(429, """{"error":"rateLimited","message":"x","details":{"reason":"lockedOut","retryAfterSeconds":600}}""", TrainerConfigurationErrorKind.RateLimited, TrainerUnlockView.CodeRequired)]
    [InlineData(503, """{"error":"unavailable","message":"x"}""", TrainerConfigurationErrorKind.Unavailable, TrainerUnlockView.CodeRequired)]
    [InlineData(403, """{"error":"forbidden","message":"x"}""", TrainerConfigurationErrorKind.Forbidden, TrainerUnlockView.Idle)]
    [InlineData(401, """{"error":"invalidApiKey","message":"x"}""", TrainerConfigurationErrorKind.Unauthorized, TrainerUnlockView.Idle)]
    public async Task Refusals_of_the_unlock_give_their_error_and_save_nothing(int status, string body, TrainerConfigurationErrorKind kind, string state)
    {
        _server.UnlockOverrides.Enqueue(() => FakeAdminHandler.Reply(status, body, retryAfterSeconds: status == 429 ? 600 : null));
        TrainerUnlockFlow flow = Flow();
        await flow.StartAsync([ModelKind.Chat], Token);

        TrainerUnlockView view = await flow.SubmitCodeAsync(Code, Token);

        Assert.Equal((kind, state), (view.Error!.Kind, view.Status));
        Assert.False(File.Exists(SecretsFile));
        if (status == 429)
        {
            Assert.Equal(("lockedOut", 600), (view.Error.Reason, view.Error.RetryAfterSeconds));
        }
    }

    [Fact]
    public async Task Without_an_access_code_on_the_admin_the_unlock_does_not_start()
    {
        _server.SealedOverrides.Enqueue(() => FakeAdminHandler.Reply(200, """
            {"configs":[],"defaults":{"llm":null,"embedding":null},"sealed":null,"sealedConfigIds":[],"unsealedConfigs":[],
             "sealedUnavailableReason":"noAccessCode","serverTime":"2026-10-25T12:00:00.000Z"}
            """));
        TrainerUnlockFlow flow = Flow();

        TrainerUnlockView view = await flow.StartAsync([ModelKind.Chat], Token);

        Assert.Equal(TrainerUnlockView.Idle, view.Status);
        Assert.Equal((TrainerConfigurationErrorKind.SealedUnavailable, "noAccessCode"), (view.Error!.Kind, view.Error.Reason));
        Assert.Equal(TrainerUnlockView.Idle, flow.GetState().Status);
    }

    [Fact]
    public async Task A_ticked_kind_that_the_sealed_answer_cannot_use_holds_nothing()
    {
        _server.SealedIds = [LlmId];
        _server.UnsealedIds = [EmbeddingId];
        TrainerUnlockFlow flow = Flow();

        TrainerUnlockView view = await flow.StartAsync([ModelKind.Chat, ModelKind.Embedding], Token);

        Assert.Equal(TrainerUnlockView.Idle, view.Status);
        Assert.Equal((TrainerConfigurationErrorKind.InvalidResponse, TrainerUnlockFlow.NotApplicableReason), (view.Error!.Kind, view.Error.Reason));
        Assert.Equal(TrainerUnlockView.Idle, flow.GetState().Status);
    }

    [Fact]
    public async Task At_most_one_envelope_is_held_a_new_unlock_replaces_the_previous_one()
    {
        TrainerUnlockFlow flow = Flow();
        await flow.StartAsync([ModelKind.Chat], Token);
        await flow.StartAsync([ModelKind.Embedding], Token);

        TrainerUnlockView unlocked = await flow.SubmitCodeAsync(Code, Token);

        Assert.Equal([ModelKind.Embedding], unlocked.Kinds);
        string used = JsonNode.Parse(Assert.Single(_server.UnlockRequests).Body!)!["envelopeId"]!.GetValue<string>();
        Assert.Equal(_server.IssuedEnvelopes[1].EnvelopeId, used);
    }

    [Fact]
    public async Task Kinds_must_be_one_or_two_distinct_values()
    {
        TrainerUnlockFlow flow = Flow();

        await Assert.ThrowsAsync<ArgumentException>(() => flow.StartAsync([], Token));
        await Assert.ThrowsAsync<ArgumentException>(() => flow.StartAsync([ModelKind.Chat, ModelKind.Chat], Token));
        Assert.Empty(_handler.Requests);
    }

    // ---------- the mock ----------

    [Fact]
    public async Task With_the_mock_the_whole_flow_works_without_any_request_and_says_it_is_simulated()
    {
        TrainerUnlockFlow flow = Flow(mock: new TrainerModelsMockOptions { Enabled = true });

        TrainerModelsView overview = await flow.GetOverviewAsync(Token);
        await flow.StartAsync([ModelKind.Chat, ModelKind.Embedding], Token);
        TrainerUnlockView wrong = await flow.SubmitCodeAsync(WrongCode, Token);
        TrainerUnlockView unlocked = await flow.SubmitCodeAsync(MockTrainerConfiguration.DefaultCode.ToLowerInvariant(), Token);

        Assert.True(overview.Simulated);
        Assert.Equal(TrainerConfigurationErrorKind.InvalidCode, wrong.Error!.Kind);
        Assert.Equal(TrainerUnlockView.Unlocked, unlocked.Status);
        Assert.Equal((MockTrainerConfiguration.ChatConfigId, MockTrainerConfiguration.ChatEndpoint),
            (unlocked.Models!.Chat.Trainer.ConfigId, unlocked.Models.Chat.Trainer.Endpoint));
        Assert.False((await flow.GetOverviewAsync(Token)).Overview!.Chat.UpdatedSinceUnlock);
        Assert.Empty(_handler.Requests);
    }

    // ---------- nothing secret ----------

    [Fact]
    public async Task No_answer_and_no_log_line_holds_the_code_the_proof_the_unlock_key_or_a_key()
    {
        TrainerUnlockFlow flow = Flow();
        List<object> answers =
        [
            await flow.GetOverviewAsync(Token),
            await flow.StartAsync([ModelKind.Chat, ModelKind.Embedding], Token),
            await flow.SubmitCodeAsync(WrongCode, Token),
            await flow.SubmitCodeAsync(" k7qx-9 ", Token),
            await flow.GetOverviewAsync(Token),
            flow.GetState(),
        ];

        FakeModelConfigServer.Issued issued = _server.IssuedEnvelopes[0];
        string[] secrets =
        [
            Code, WrongCode, "k7qx-9", issued.Proof, issued.UnlockKey, AdminApiKey, DevToken,
            FakeModelConfigServer.ApiKeyOf(LlmId), FakeModelConfigServer.ApiKeyOf(EmbeddingId),
        ];
        string texts = string.Join("\n", answers.Select(a => JsonSerializer.Serialize(a, ApiJson) + a))
            + string.Join("\n", _logger.Entries.Select(e => e.Message));
        Assert.Equal(TrainerUnlockView.Unlocked, ((TrainerUnlockView)answers[3]).Status);
        Assert.NotEmpty(_logger.Entries);
        foreach (string secret in secrets)
        {
            Assert.DoesNotContain(secret, texts, StringComparison.Ordinal);
        }
    }

    // ---------- helpers ----------

    private TrainerUnlockFlow Flow(bool joined = true, TrainerModelsMockOptions? mock = null)
    {
        DashboardOptions options = new()
        {
            Reporting = { ServerUrl = AdminUrl, ApiKey = AdminApiKey },
            TrainerModels = { Mock = mock ?? new TrainerModelsMockOptions() },
        };
        DevIdentity? current = joined ? Joined : null;
        TrainerConfigurationService service = new(options, () => current, _logger, _clock, _ => null, _handler);
        TrainerUnlockFlow flow = new(service, _settings, _logger, _clock);
        _owned.Add(flow);
        _owned.Add(service);
        return flow;
    }

    private static string Shape(ModelProfileStatus profile) =>
        $"{profile.Provider}|{profile.Endpoint}|{profile.Deployment}|{profile.ApiKey}|{profile.SameResourceAsLlm}|{profile.Complete}|{string.Join(",", profile.Missing)}";

    private string? Active(string name) => _settings.GetEntry(AzureOpenAISettingsStore.Section, name);

    private static HttpResponseMessage Gone(string reason) =>
        FakeAdminHandler.Reply(410, $$$"""{"error":"envelopeExpired","message":"x","details":{"reason":"{{{reason}}}"}}""");

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class ListLogger : ILogger
    {
        public ConcurrentQueue<LogEntry> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Enqueue(new LogEntry(logLevel, formatter(state, exception) + exception));
    }
}
