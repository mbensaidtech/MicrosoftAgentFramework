using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LabDashboard.Reporting;
using LabDashboard.Settings;
using LabDashboard.TrainerModels;
using Microsoft.Extensions.Logging;

namespace LabDashboard.Tests;

/// <summary>
/// The Admin API client of the AI model configurations (contract v1) against a handler-level fake of the admin (never the network):
/// headers, overview mapping (Rule 17), sealed retrieval, every documented error, Retry-After, timeouts, malformed bodies, the exact unlock
/// body, and the proof and the unlock key kept out of every text and log.
/// </summary>
public sealed class AdminModelConfigsClientTests : IDisposable
{
    private const string AdminUrl = "https://labs-admin.example.com";
    private const string ApiKey = "lbk_test_model_configs_api_key_5e2b";
    private const string DevToken = "tok-model-configs-dev-token-91c4f0";
    private const string UserId = "8D2B6C1E-4F3A-4E7B-B2A9-5C0D1E2F3A4B";

    private const string LlmId = "11111111-1111-4111-8111-111111111111";
    private const string EmbeddingId = "22222222-2222-4222-8222-222222222222";
    private const string OpenAiId = "33333333-3333-4333-8333-333333333333";
    private const string FutureProviderId = "44444444-4444-4444-8444-444444444444";
    private const string ImageId = "55555555-5555-4555-8555-555555555555";
    private const string LargeEmbeddingId = "66666666-6666-4666-8666-666666666666";
    private const string EnvelopeId = "7d3e5c1a-9b2f-4e8d-a6c4-0f1e2d3c4b5a";
    private const string ExpiresAt = "2026-10-25T15:10:00Z";

    private const string OverviewJson = """
        {
          "configs": [
            {
              "id": "11111111-1111-4111-8111-111111111111", "name": "Workshop GPT-4o mini", "kind": "llm", "provider": "azure-openai",
              "settings": { "endpoint": "https://workshop-aoai.openai.azure.com/", "deployment": "gpt-4o-mini", "apiVersion": "2024-10-21",
                            "model": "gpt-4o-mini", "modelVersion": "2024-07-18", "region": "swedencentral", "retries": 3 },
              "parameters": { "temperature": 0.2, "style": "concise" },
              "secretFields": ["apiKey"], "isDefault": true, "embedding": null, "updatedAt": "2026-10-25T09:30:00.000Z",
              "futureField": { "nested": [1, 2, 3] }
            },
            {
              "id": "22222222-2222-4222-8222-222222222222", "name": "Workshop embeddings", "kind": "embedding", "provider": "azure-openai",
              "settings": { "endpoint": "https://workshop-embed.openai.azure.com/", "deployment": "text-embedding-3-small", "model": "text-embedding-3-small" },
              "parameters": { "dimensions": 1536 }, "secretFields": ["apiKey"], "isDefault": true,
              "embedding": { "model": "text-embedding-3-small", "dimensions": 1536, "space": "cosine" }, "updatedAt": "2026-10-25T09:31:00.000Z"
            },
            {
              "id": "33333333-3333-4333-8333-333333333333", "name": "Backup OpenAI", "kind": "llm", "provider": "openai",
              "settings": { "model": "gpt-4o" }, "parameters": {}, "secretFields": ["apiKey"], "isDefault": false, "embedding": null,
              "updatedAt": "2026-10-24T08:00:00.000Z"
            },
            { "id": "44444444-4444-4444-8444-444444444444", "name": "Future provider", "kind": "llm", "provider": "mistral", "settings": 42, "shape": "unknown" },
            { "id": "55555555-5555-4555-8555-555555555555", "kind": "image", "provider": "azure-openai", "settings": [] },
            {
              "id": "66666666-6666-4666-8666-666666666666", "name": "Large embeddings", "kind": "embedding", "provider": "azure-openai",
              "settings": { "endpoint": "https://workshop-embed.openai.azure.com/", "deployment": "text-embedding-3-large", "model": "text-embedding-3-large" },
              "parameters": { "dimensions": 3072 }, "secretFields": ["apiKey"], "isDefault": false,
              "embedding": { "model": "text-embedding-3-large", "dimensions": 3072, "space": "cosine" }, "updatedAt": "2026-10-25T09:32:00.000Z"
            }
          ],
          "defaults": { "llm": "11111111-1111-4111-8111-111111111111", "embedding": "22222222-2222-4222-8222-222222222222", "image": "x" },
          "sealed": null,
          "sealedConfigIds": [],
          "unsealedConfigs": [],
          "sealedUnavailableReason": "notRequested",
          "serverTime": "2026-10-25T15:00:00.000Z",
          "futureRoot": { "anything": true }
        }
        """;

    private const string EnvelopeJson = """
        {
          "version": 1, "envelopeId": "7d3e5c1a-9b2f-4e8d-a6c4-0f1e2d3c4b5a", "expiresAt": "2026-10-25T15:10:00Z", "cipher": "AES-256-GCM",
          "iv": "AAECAwQFBgcICQoL", "ciphertext": "c2VhbGVkLWNvbmZpZ3VyYXRpb25zLWZvci10aGUtdGVzdA",
          "kdf": { "name": "PBKDF2-HMAC-SHA256", "iterations": 600000, "salt": "AAECAwQFBgcICQoLDA0ODw" },
          "code": { "length": 5, "alphabet": "ABCDEFGHJKMNPQRSTUVWXYZ23456789" },
          "attemptsAllowed": 3, "futureEnvelopeField": "x"
        }
        """;

    private static readonly byte[] Proof = Enumerable.Range(0, 32).Select(i => (byte)(255 - i * 3)).ToArray();
    private static readonly string ProofText = Base64Url.EncodeToString(Proof);
    private static readonly byte[] UnlockKeyBytes = Enumerable.Range(1, 32).Select(i => (byte)(i * 7)).ToArray();
    private static readonly string UnlockKeyText = Base64Url.EncodeToString(UnlockKeyBytes);

    /// <summary>The JSON options of the dashboard API (Program.cs).</summary>
    private static readonly JsonSerializerOptions ApiJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly FakeAdminHandler _handler = new();
    private readonly ListLogger _logger = new();
    private readonly ManualClock _clock = new();
    private readonly List<AdminModelConfigsClient> _clients = [];
    private DevIdentity? _identity = new() { UserId = UserId, Username = "john-dev", DevToken = DevToken };

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        foreach (AdminModelConfigsClient client in _clients)
        {
            client.Dispose();
        }

        _handler.Dispose();
    }

    // ---------- requests ----------

    [Fact]
    public async Task Every_call_sends_the_admin_api_key_and_the_dev_token_to_the_contract_routes()
    {
        Serve();
        AdminModelConfigsClient client = Client("https://labs-admin.example.com/admin/");

        Assert.True((await client.GetOverviewAsync(Token)).Succeeded);
        Assert.True((await client.GetSealedAsync(Token)).Succeeded);
        TrainerOutcome<AdminUnlockKey> unlocked = await client.UnlockAsync(EnvelopeId, Proof, Token);
        unlocked.Value!.Dispose();

        CapturedRequest[] requests = _handler.Requests.ToArray();
        Assert.Equal(
        [
            "GET https://labs-admin.example.com/admin/api/v1/model-configs",
            "GET https://labs-admin.example.com/admin/api/v1/model-configs?sealed=1",
            "POST https://labs-admin.example.com/admin/api/v1/model-configs/unlock",
        ], requests.Select(r => r.ToString()));
        Assert.All(requests, r =>
        {
            Assert.Equal(ApiKey, r.ApiKey);
            Assert.Equal(DevToken, r.Bearer);
            Assert.Contains("application/json", r.Accept, StringComparison.Ordinal);
        });
        Assert.Null(requests[0].Body);
        Assert.Null(requests[1].Body);
        Assert.StartsWith("application/json", requests[2].ContentType, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_an_identity_or_a_dev_token_nothing_is_sent_and_the_workshop_is_missing()
    {
        Serve();
        foreach (DevIdentity? identity in new DevIdentity?[] { null, new() { UserId = UserId, Username = "john-dev" }, new() { UserId = UserId, Username = "john-dev", DevToken = " " } })
        {
            _identity = identity;
            AdminModelConfigsClient client = Client();

            TrainerConfigurationError?[] errors =
            [
                (await client.GetOverviewAsync(Token)).Error,
                (await client.GetSealedAsync(Token)).Error,
                (await client.UnlockAsync(EnvelopeId, Proof, Token)).Error,
            ];

            Assert.All(errors, error =>
            {
                Assert.Equal(TrainerConfigurationErrorKind.NotConfigured, error!.Kind);
                Assert.Equal([TrainerConfigurationService.WorkshopSetting], error.Missing);
            });
        }

        Assert.Empty(_handler.Requests);
    }

    [Theory]
    [InlineData("https://labs-admin.example.com", true)]
    [InlineData("https://labs-admin.example.com/admin/", true)]
    [InlineData("http://localhost:5000", true)]
    [InlineData("http://127.0.0.1:8080/", true)]
    [InlineData("http://[::1]:5000", true)]
    [InlineData("http://labs-admin.example.com", false)]
    [InlineData("ftp://labs-admin.example.com", false)]
    [InlineData("https://user:secret@labs-admin.example.com", false)]
    [InlineData("https://labs-admin.example.com/?tenant=1", false)]
    [InlineData("labs-admin.example.com", false)]
    [InlineData("", false)]
    public void Only_https_or_a_local_http_admin_is_accepted(string url, bool allowed)
    {
        Assert.Equal(allowed, AdminModelConfigsClient.IsAllowedServerUrl(url));
        if (!allowed && Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            Assert.Throws<ArgumentException>(() => new AdminModelConfigsClient(uri, ApiKey, () => _identity, _logger, TimeSpan.FromSeconds(1), _clock, _handler));
        }
    }

    // ---------- overview (Rule 17) ----------

    [Fact]
    public async Task The_overview_maps_the_default_of_each_kind_and_lists_the_others_ignoring_what_it_does_not_know()
    {
        Serve();

        TrainerOutcome<AdminModelConfigs> document = await Client().GetOverviewAsync(Token);
        TrainerModelsOverview overview = TrainerModelsOverview.From(document.Value!, simulated: false);

        // Unknown kinds and providers are dropped; unknown properties and settings keys do not get in the way.
        Assert.Equal([LlmId, EmbeddingId, OpenAiId, LargeEmbeddingId], document.Value!.Configs.Select(c => c.Id));
        Assert.Null(document.Value.Sealed);
        Assert.Equal("swedencentral", document.Value.Configs[0].Settings["region"]);
        Assert.False(document.Value.Configs[0].Settings.ContainsKey("retries"));
        Assert.Equal(0.2, document.Value.Configs[0].Parameters["temperature"]);
        Assert.False(document.Value.Configs[0].Parameters.ContainsKey("style"));

        TrainerKindOverview chat = overview.Chat;
        Assert.Equal(TrainerModelState.Applicable, chat.State);
        Assert.True(chat.CanUse);
        Assert.Null(chat.Reason);
        Assert.Empty(chat.Warnings);
        Assert.Equal(new TrainerModelSummary(LlmId, "Workshop GPT-4o mini", "azure-openai", "https://workshop-aoai.openai.azure.com/",
            "workshop-aoai.openai.azure.com", "gpt-4o-mini", "gpt-4o-mini", "2026-10-25T09:30:00.000Z", KeyProvided: true, Dimensions: null), chat.Default);
        TrainerModelSummary other = Assert.Single(chat.Others);
        Assert.Equal((OpenAiId, "openai", null), (other.Id, other.Provider, other.Endpoint));

        TrainerKindOverview embedding = overview.Embedding;
        Assert.Equal(TrainerModelState.Applicable, embedding.State);
        Assert.Empty(embedding.Warnings);
        Assert.Equal(("text-embedding-3-small", 1536, "workshop-embed.openai.azure.com"), (embedding.Default!.Deployment, embedding.Default.Dimensions, embedding.Default.EndpointHost));
        Assert.Equal((LargeEmbeddingId, 3072), (Assert.Single(embedding.Others).Id, embedding.Others[0].Dimensions));

        Assert.Equal("2026-10-25T15:00:00.000Z", overview.ServerTime);
        Assert.False(overview.Simulated);
        Assert.Same(chat, overview.For(ModelKind.Chat));
        string json = JsonSerializer.Serialize(overview, ApiJson);
        Assert.Contains("\"state\":\"applicable\"", json, StringComparison.Ordinal);
        Assert.Contains("\"keyProvided\":true", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_default_of_another_provider_is_not_applicable_and_one_of_an_unknown_provider_too()
    {
        JsonObject openAi = OverviewDocument();
        openAi["defaults"]!["llm"] = OpenAiId;
        JsonObject unknown = OverviewDocument();
        unknown["defaults"]!["llm"] = FutureProviderId;

        TrainerKindOverview notAzure = (await OverviewAsync(openAi)).Chat;
        TrainerKindOverview notKnown = (await OverviewAsync(unknown)).Chat;

        Assert.Equal((TrainerModelState.NotApplicable, TrainerModelsOverview.ProviderReason), (notAzure.State, notAzure.Reason));
        Assert.False(notAzure.CanUse);
        Assert.Equal(("openai", "Backup OpenAI"), (notAzure.Default!.Provider, notAzure.Default.Name));
        Assert.Equal([LlmId], notAzure.Others.Select(o => o.Id));

        Assert.Equal((TrainerModelState.NotApplicable, TrainerModelsOverview.UnknownProviderReason), (notKnown.State, notKnown.Reason));
        Assert.Null(notKnown.Default);
        Assert.Equal([LlmId, OpenAiId], notKnown.Others.Select(o => o.Id));
    }

    [Fact]
    public async Task An_embedding_default_with_other_dimensions_warns_that_lab07_expects_1536_and_stays_applicable()
    {
        JsonObject large = OverviewDocument();
        large["defaults"]!["embedding"] = LargeEmbeddingId;
        JsonObject fromParameters = OverviewDocument();
        ((JsonObject)Config(fromParameters, EmbeddingId)["embedding"]!).Remove("dimensions");
        Config(fromParameters, EmbeddingId)["parameters"] = new JsonObject { ["dimensions"] = 768 };
        JsonObject unknownDimensions = OverviewDocument();
        Config(unknownDimensions, EmbeddingId)["embedding"] = null;
        Config(unknownDimensions, EmbeddingId)["parameters"] = new JsonObject();

        TrainerKindOverview largeDefault = (await OverviewAsync(large)).Embedding;
        TrainerKindOverview parametersDefault = (await OverviewAsync(fromParameters)).Embedding;
        TrainerKindOverview unknownDefault = (await OverviewAsync(unknownDimensions)).Embedding;

        Assert.Equal([TrainerModelsOverview.DimensionsWarning], largeDefault.Warnings);
        Assert.Equal(TrainerModelState.Applicable, largeDefault.State);
        Assert.Equal(3072, largeDefault.Default!.Dimensions);
        Assert.Equal([TrainerModelsOverview.DimensionsWarning], parametersDefault.Warnings);
        Assert.Equal(768, parametersDefault.Default!.Dimensions);
        Assert.Empty(unknownDefault.Warnings);
        Assert.Null(unknownDefault.Default!.Dimensions);
    }

    [Fact]
    public async Task No_default_for_a_kind_is_none_and_its_configurations_are_still_listed()
    {
        JsonObject document = OverviewDocument();
        document["defaults"]!["embedding"] = null;

        TrainerKindOverview embedding = (await OverviewAsync(document)).Embedding;

        Assert.Equal(TrainerModelState.None, embedding.State);
        Assert.Null(embedding.Default);
        Assert.Equal([EmbeddingId, LargeEmbeddingId], embedding.Others.Select(o => o.Id));
    }

    [Fact]
    public async Task Settings_the_store_would_refuse_make_the_default_unusable_and_no_api_key_means_entra_id()
    {
        JsonObject insecure = OverviewDocument();
        Config(insecure, LlmId)["settings"]!["endpoint"] = "http://workshop-aoai.openai.azure.com/";
        JsonObject noDeployment = OverviewDocument();
        ((JsonObject)Config(noDeployment, LlmId)["settings"]!).Remove("deployment");
        JsonObject noKey = OverviewDocument();
        Config(noKey, LlmId)["secretFields"] = new JsonArray();

        Assert.Equal((TrainerModelState.Unusable, TrainerModelsOverview.InvalidSettingsReason), State((await OverviewAsync(insecure)).Chat));
        Assert.Equal((TrainerModelState.Unusable, TrainerModelsOverview.InvalidSettingsReason), State((await OverviewAsync(noDeployment)).Chat));
        TrainerKindOverview entraId = (await OverviewAsync(noKey)).Chat;
        Assert.Equal((TrainerModelState.Applicable, null), State(entraId));
        Assert.False(entraId.Default!.KeyProvided);
    }

    // ---------- sealed retrieval ----------

    [Fact]
    public async Task The_sealed_retrieval_returns_the_envelope_with_expires_at_verbatim_the_configs_and_the_lowercase_dev_id()
    {
        JsonObject document = SealedDocument();
        document["sealedConfigIds"] = new JsonArray(LlmId);
        document["unsealedConfigs"] = new JsonArray(new JsonObject { ["id"] = EmbeddingId, ["reason"] = "secretsUnreadable", ["future"] = 1 });
        Serve(sealedDocument: document);

        TrainerOutcome<AdminSealedConfiguration> outcome = await Client().GetSealedAsync(Token);

        AdminSealedConfiguration sealedConfiguration = outcome.Value!;
        AdminEnvelope envelope = sealedConfiguration.Envelope;
        Assert.Equal(1, envelope.Version);
        Assert.Equal(EnvelopeId, envelope.EnvelopeId);
        Assert.Equal(ExpiresAt, envelope.ExpiresAt);
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 15, 10, 0, TimeSpan.Zero), envelope.ExpiresAtUtc);
        Assert.Equal(("AES-256-GCM", "AAECAwQFBgcICQoL", "c2VhbGVkLWNvbmZpZ3VyYXRpb25zLWZvci10aGUtdGVzdA"), (envelope.Cipher, envelope.Iv, envelope.Ciphertext));
        Assert.Equal(new AdminEnvelopeKdf("PBKDF2-HMAC-SHA256", 600000, "AAECAwQFBgcICQoLDA0ODw"), envelope.Kdf);
        Assert.Equal(new AdminEnvelopeCode(5, "ABCDEFGHJKMNPQRSTUVWXYZ23456789"), envelope.Code);
        Assert.Equal(3, envelope.AttemptsAllowed);

        Assert.Equal(UserId.ToLowerInvariant(), sealedConfiguration.DevId);
        Assert.Equal([LlmId], sealedConfiguration.SealedConfigIds);
        Assert.Equal([new AdminUnsealedConfig(EmbeddingId, "secretsUnreadable")], sealedConfiguration.UnsealedConfigs);
        Assert.Equal((LlmId, EmbeddingId), (sealedConfiguration.DefaultId(ModelKind.Chat), sealedConfiguration.DefaultId(ModelKind.Embedding)));
        Assert.Equal(4, sealedConfiguration.Configs.Count);

        // What can still be unlocked: the unsealed embedding default cannot.
        Assert.Equal((TrainerModelState.Applicable, null), State(sealedConfiguration.Overview.Chat));
        Assert.Equal((TrainerModelState.Unusable, "secretsUnreadable"), State(sealedConfiguration.Overview.Embedding));
        Assert.Contains(EnvelopeId, sealedConfiguration.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_default_with_a_key_that_this_envelope_does_not_hold_is_unusable()
    {
        JsonObject document = SealedDocument();
        document["sealedConfigIds"] = new JsonArray(EmbeddingId);
        Serve(sealedDocument: document);

        AdminSealedConfiguration sealedConfiguration = (await Client().GetSealedAsync(Token)).Value!;

        Assert.Equal((TrainerModelState.Unusable, TrainerModelsOverview.NotSealedReason), State(sealedConfiguration.Overview.Chat));
        Assert.Equal((TrainerModelState.Applicable, null), State(sealedConfiguration.Overview.Embedding));
    }

    [Theory]
    [InlineData("noSecrets", "noSecrets")]
    [InlineData("noWorkshop", "noWorkshop")]
    [InlineData("workshopClosed", "workshopClosed")]
    [InlineData("noAccessCode", "noAccessCode")]
    [InlineData("accessCodeExpired", "accessCodeExpired")]
    [InlineData("accessCodeUnreadable", "accessCodeUnreadable")]
    [InlineData("secretsUnreadable", "secretsUnreadable")]
    [InlineData("unavailable", "unavailable")]
    [InlineData("notRequested", "notRequested")]
    [InlineData("someFutureReason", "someFutureReason")]
    [InlineData("not an identifier!", "unavailable")]
    [InlineData(null, "unavailable")]
    public async Task Without_an_envelope_the_sealed_retrieval_gives_the_sealed_unavailable_reason(string? reason, string expected)
    {
        JsonObject document = OverviewDocument();
        document["sealedUnavailableReason"] = reason;
        Serve(sealedDocument: document);

        TrainerOutcome<AdminSealedConfiguration> outcome = await Client().GetSealedAsync(Token);

        Assert.Equal(TrainerConfigurationErrorKind.SealedUnavailable, outcome.Error!.Kind);
        Assert.Equal(expected, outcome.Error.Reason);
    }

    // ---------- errors ----------

    [Theory]
    [InlineData("overview", 400, """{"error":"validation","message":"Bad query.","details":{"field":"sealed","reason":"invalid"}}""", null, null, TrainerConfigurationErrorKind.Unknown, null, "validation", null, null)]
    [InlineData("overview", 401, """{"error":"invalidApiKey","message":"API key missing or invalid."}""", null, "ApiKey header=\"X-API-Key\"", TrainerConfigurationErrorKind.Unauthorized, "apiKey", null, null, null)]
    [InlineData("overview", 401, """{"error":"unauthorized","message":"Unknown dev token."}""", null, null, TrainerConfigurationErrorKind.Unauthorized, "devToken", null, null, null)]
    [InlineData("overview", 401, null, null, "ApiKey header=\"X-API-Key\"", TrainerConfigurationErrorKind.Unauthorized, "apiKey", null, null, null)]
    [InlineData("overview", 401, "<html>Unauthorized</html>", null, null, TrainerConfigurationErrorKind.Unauthorized, null, null, null, null)]
    [InlineData("overview", 403, """{"error":"forbidden","message":"Archived developer."}""", null, null, TrainerConfigurationErrorKind.Forbidden, null, null, null, null)]
    [InlineData("overview", 404, "<!doctype html><title>404</title>", null, null, TrainerConfigurationErrorKind.NotFound, null, null, null, null)]
    [InlineData("overview", 429, """{"error":"rateLimited","message":"Slow down.","details":{"retryAfterSeconds":30}}""", 30, null, TrainerConfigurationErrorKind.RateLimited, null, null, null, 30)]
    [InlineData("overview", 500, """{"error":"internal","message":"Boom."}""", null, null, TrainerConfigurationErrorKind.Unavailable, null, "internal", null, null)]
    [InlineData("overview", 503, """{"error":"unavailable","message":"Keys not configured."}""", null, null, TrainerConfigurationErrorKind.Unavailable, null, "unavailable", null, null)]
    [InlineData("overview", 502, "<html>Bad gateway</html>", null, null, TrainerConfigurationErrorKind.Unavailable, null, null, null, null)]
    [InlineData("overview", 302, null, null, null, TrainerConfigurationErrorKind.Unknown, null, null, null, null)]
    [InlineData("overview", 204, null, null, null, TrainerConfigurationErrorKind.Unknown, null, null, null, null)]
    [InlineData("sealed", 401, """{"error":"unauthorized","message":"Unknown dev token."}""", null, null, TrainerConfigurationErrorKind.Unauthorized, "devToken", null, null, null)]
    [InlineData("sealed", 401, """{"error":"invalidApiKey","message":"API key missing or invalid."}""", null, "ApiKey header=\"X-API-Key\"", TrainerConfigurationErrorKind.Unauthorized, "apiKey", null, null, null)]
    [InlineData("sealed", 403, """{"error":"forbidden","message":"Archived developer."}""", null, null, TrainerConfigurationErrorKind.Forbidden, null, null, null, null)]
    [InlineData("sealed", 404, "Not Found", null, null, TrainerConfigurationErrorKind.NotFound, null, null, null, null)]
    [InlineData("sealed", 429, """{"error":"rateLimited","message":"Too many envelopes.","details":{"reason":"tooManyEnvelopes","retryAfterSeconds":40}}""", 40, null, TrainerConfigurationErrorKind.RateLimited, null, "tooManyEnvelopes", null, 40)]
    [InlineData("sealed", 429, """{"error":"rateLimited","message":"Slow down.","details":{"retryAfterSeconds":20}}""", 12, null, TrainerConfigurationErrorKind.RateLimited, null, null, null, 20)]
    [InlineData("sealed", 410, """{"error":"envelopeExpired","message":"?"}""", null, null, TrainerConfigurationErrorKind.Unknown, null, "envelopeExpired", null, null)]
    [InlineData("sealed", 500, """{"error":"internal","message":"Boom."}""", null, null, TrainerConfigurationErrorKind.Unavailable, null, "internal", null, null)]
    [InlineData("sealed", 503, """{"error":"unavailable","message":"Keys not configured."}""", null, null, TrainerConfigurationErrorKind.Unavailable, null, "unavailable", null, null)]
    [InlineData("unlock", 400, """{"error":"validation","message":"Bad proof.","details":{"field":"proof","reason":"format"}}""", null, null, TrainerConfigurationErrorKind.Unknown, null, "validation", null, null)]
    [InlineData("unlock", 401, """{"error":"invalidAccessCode","message":"Wrong code.","details":{"attemptsLeft":2}}""", null, null, TrainerConfigurationErrorKind.InvalidCode, null, null, 2, null)]
    [InlineData("unlock", 401, """{"error":"invalidAccessCode","message":"Wrong code.","details":{"attemptsLeft":0}}""", null, null, TrainerConfigurationErrorKind.InvalidCode, null, null, 0, null)]
    [InlineData("unlock", 401, """{"error":"invalidAccessCode","message":"Wrong code."}""", null, null, TrainerConfigurationErrorKind.InvalidCode, null, null, null, null)]
    [InlineData("unlock", 401, """{"error":"invalidApiKey","message":"API key missing or invalid."}""", null, "ApiKey header=\"X-API-Key\"", TrainerConfigurationErrorKind.Unauthorized, "apiKey", null, null, null)]
    [InlineData("unlock", 401, """{"error":"unauthorized","message":"Unknown dev token."}""", null, null, TrainerConfigurationErrorKind.Unauthorized, "devToken", null, null, null)]
    [InlineData("unlock", 403, """{"error":"forbidden","message":"Archived developer."}""", null, null, TrainerConfigurationErrorKind.Forbidden, null, null, null, null)]
    [InlineData("unlock", 404, "<!doctype html><title>404</title>", null, null, TrainerConfigurationErrorKind.NotFound, null, null, null, null)]
    [InlineData("unlock", 410, """{"error":"envelopeExpired","message":"?","details":{"reason":"not an identifier!"}}""", null, null, TrainerConfigurationErrorKind.EnvelopeExpired, null, null, null, null)]
    [InlineData("unlock", 429, """{"error":"rateLimited","message":"Locked.","details":{"reason":"lockedOut","retryAfterSeconds":900}}""", 900, null, TrainerConfigurationErrorKind.RateLimited, null, "lockedOut", null, 900)]
    [InlineData("unlock", 429, """{"error":"rateLimited","message":"Frozen.","details":{"reason":"accessCodeFrozen","retryAfterSeconds":1800}}""", 1800, null, TrainerConfigurationErrorKind.RateLimited, null, "accessCodeFrozen", null, 1800)]
    [InlineData("unlock", 429, """{"error":"rateLimited","message":"Slow down.","details":{"retryAfterSeconds":60}}""", 60, null, TrainerConfigurationErrorKind.RateLimited, null, null, null, 60)]
    [InlineData("unlock", 429, """{"error":"rateLimited","message":"?","details":{"reason":"someFutureLimit","retryAfterSeconds":5}}""", null, null, TrainerConfigurationErrorKind.RateLimited, null, "someFutureLimit", null, 5)]
    [InlineData("unlock", 500, """{"error":"internal","message":"Boom."}""", null, null, TrainerConfigurationErrorKind.Unavailable, null, "internal", null, null)]
    [InlineData("unlock", 503, """{"error":"unavailable","message":"Keys not configured."}""", null, null, TrainerConfigurationErrorKind.Unavailable, null, "unavailable", null, null)]
    public async Task Each_documented_error_maps_to_its_kind_and_details(
        string operation, int status, string? body, int? retryAfterHeader, string? wwwAuthenticate,
        TrainerConfigurationErrorKind kind, string? credential, string? reason, int? attemptsLeft, int? retryAfterSeconds)
    {
        _handler.Always(_ => FakeAdminHandler.Reply(status, body, body?.StartsWith('{') == true ? "application/json" : "text/html", retryAfterHeader, wwwAuthenticate));

        TrainerConfigurationError error = (await RunAsync(Client(), operation))!;

        Assert.Equal(kind, error.Kind);
        Assert.Equal(credential, error.Credential);
        Assert.Equal(reason, error.Reason);
        Assert.Equal(attemptsLeft, error.AttemptsLeft);
        Assert.Equal(retryAfterSeconds, error.RetryAfterSeconds);
        Assert.Equal(status, error.HttpStatus);
        Assert.Empty(error.Missing);
        Assert.Single(_handler.Requests);
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains($"HTTP {status}", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("used")]
    [InlineData("expired")]
    [InlineData("exhausted")]
    [InlineData("keyRotated")]
    [InlineData("workshopChanged")]
    [InlineData("workshopClosed")]
    [InlineData("accessCodeChanged")]
    [InlineData("accessCodeRevoked")]
    [InlineData("accessCodeExpired")]
    [InlineData("accessCodeUnreadable")]
    [InlineData("configChanged")]
    [InlineData("someFutureReason")]
    public async Task A_dead_envelope_is_envelope_expired_with_its_reason_kept_as_received(string reason)
    {
        _handler.Always(_ => FakeAdminHandler.Reply(410, $$$"""{"error":"envelopeExpired","message":"Envelope gone.","details":{"reason":"{{{reason}}}"}}"""));

        TrainerOutcome<AdminUnlockKey> outcome = await Client().UnlockAsync(EnvelopeId, Proof, Token);

        Assert.Equal(TrainerConfigurationErrorKind.EnvelopeExpired, outcome.Error!.Kind);
        Assert.Equal(reason, outcome.Error.Reason);
        Assert.Equal(410, outcome.Error.HttpStatus);
        Assert.Contains(_logger.Entries, e => e.Message.Contains(EnvelopeId, StringComparison.Ordinal) && e.Message.Contains(reason, StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_message_of_an_error_is_never_logged_nor_returned()
    {
        const string message = "Server prose that only belongs in the admin's own log";
        _handler.Always(_ => FakeAdminHandler.Reply(403, $$"""{"error":"forbidden","message":"{{message}}"}"""));

        TrainerConfigurationError error = (await Client().GetOverviewAsync(Token)).Error!;

        Assert.DoesNotContain(message, error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(message, JsonSerializer.Serialize(error, ApiJson), StringComparison.Ordinal);
        Assert.DoesNotContain(_logger.Entries, e => e.Message.Contains(message, StringComparison.Ordinal));
    }

    // ---------- Retry-After (Rule 19) ----------

    [Fact]
    public async Task After_a_429_on_a_get_no_get_is_sent_before_retry_after_and_unlock_is_not_held_back()
    {
        _handler.Always(request => request.Uri.AbsolutePath.EndsWith("/unlock", StringComparison.Ordinal)
            ? FakeAdminHandler.Reply(200, UnlockAnswer())
            : FakeAdminHandler.Reply(429, """{"error":"rateLimited","message":"?","details":{"retryAfterSeconds":30}}""", retryAfterSeconds: 30));
        AdminModelConfigsClient client = Client();

        Assert.Equal(TrainerConfigurationErrorKind.RateLimited, (await client.GetOverviewAsync(Token)).Error!.Kind);
        _clock.Now += TimeSpan.FromSeconds(10);
        TrainerConfigurationError overview = (await client.GetOverviewAsync(Token)).Error!;
        TrainerConfigurationError sealedRetrieval = (await client.GetSealedAsync(Token)).Error!;
        TrainerOutcome<AdminUnlockKey> unlock = await client.UnlockAsync(EnvelopeId, Proof, Token);
        unlock.Value?.Dispose();

        Assert.Equal((TrainerConfigurationErrorKind.RateLimited, 20, (int?)null), (overview.Kind, overview.RetryAfterSeconds, overview.HttpStatus));
        Assert.Equal((TrainerConfigurationErrorKind.RateLimited, 20), (sealedRetrieval.Kind, sealedRetrieval.RetryAfterSeconds));
        Assert.True(unlock.Succeeded);
        Assert.Equal(2, _handler.Requests.Count);

        _clock.Now += TimeSpan.FromSeconds(21);
        await client.GetOverviewAsync(Token);
        Assert.Equal(3, _handler.Requests.Count);
    }

    [Fact]
    public async Task Too_many_envelopes_holds_back_the_next_envelope_only_and_the_overview_stays_available()
    {
        _handler.Always(request => request.Uri.Query == "?sealed=1"
            ? FakeAdminHandler.Reply(429, """{"error":"rateLimited","message":"?","details":{"reason":"tooManyEnvelopes","retryAfterSeconds":40}}""", retryAfterSeconds: 40)
            : FakeAdminHandler.Reply(200, OverviewJson));
        AdminModelConfigsClient client = Client();

        TrainerConfigurationError first = (await client.GetSealedAsync(Token)).Error!;
        TrainerConfigurationError second = (await client.GetSealedAsync(Token)).Error!;
        TrainerOutcome<AdminModelConfigs> overview = await client.GetOverviewAsync(Token);

        Assert.Equal((TrainerConfigurationErrorKind.RateLimited, AdminModelConfigsClient.TooManyEnvelopesReason, 40), (first.Kind, first.Reason, first.RetryAfterSeconds));
        Assert.Equal((TrainerConfigurationErrorKind.RateLimited, AdminModelConfigsClient.TooManyEnvelopesReason, 40), (second.Kind, second.Reason, second.RetryAfterSeconds));
        Assert.True(overview.Succeeded);
        Assert.Equal(["?sealed=1", ""], _handler.Requests.Select(r => r.Uri.Query));
    }

    [Fact]
    public async Task A_code_lockout_holds_back_unlock_only_and_a_retry_after_date_counts_from_the_clock()
    {
        _handler.Always(request =>
        {
            if (!request.Uri.AbsolutePath.EndsWith("/unlock", StringComparison.Ordinal))
            {
                return FakeAdminHandler.Reply(200, OverviewJson);
            }

            HttpResponseMessage response = FakeAdminHandler.Reply(429, """{"error":"rateLimited","message":"?","details":{"reason":"lockedOut"}}""");
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(_clock.Now.AddSeconds(120));
            return response;
        });
        AdminModelConfigsClient client = Client();

        TrainerConfigurationError first = (await client.UnlockAsync(EnvelopeId, Proof, Token)).Error!;
        TrainerConfigurationError second = (await client.UnlockAsync(EnvelopeId, Proof, Token)).Error!;
        bool overview = (await client.GetOverviewAsync(Token)).Succeeded;

        Assert.Equal(("lockedOut", 120), (first.Reason, first.RetryAfterSeconds));
        Assert.Equal(("lockedOut", 120), (second.Reason, second.RetryAfterSeconds));
        Assert.True(overview);
        Assert.Equal(2, _handler.Requests.Count);
    }

    // ---------- network ----------

    [Fact]
    public async Task A_timeout_a_dns_failure_or_a_refused_connection_is_a_network_error_and_the_caller_cancellation_throws()
    {
        _handler.Respond = async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return FakeAdminHandler.Reply(200, OverviewJson);
        };
        TrainerOutcome<AdminModelConfigs> timedOut = await Client(timeout: TimeSpan.FromMilliseconds(200)).GetOverviewAsync(Token);

        _handler.Respond = (_, _) => throw new HttpRequestException(HttpRequestError.NameResolutionError, "No such host is known (labs-admin.example.com:443)");
        TrainerOutcome<AdminSealedConfiguration> dns = await Client().GetSealedAsync(Token);

        _handler.Respond = (_, _) => throw new HttpRequestException(HttpRequestError.ConnectionError, "Connection refused");
        TrainerOutcome<AdminUnlockKey> refused = await Client().UnlockAsync(EnvelopeId, Proof, Token);

        Assert.Equal(TrainerConfigurationErrorKind.Network, timedOut.Error!.Kind);
        Assert.Equal(TrainerConfigurationErrorKind.Network, dns.Error!.Kind);
        Assert.Equal(TrainerConfigurationErrorKind.Network, refused.Error!.Kind);
        Assert.Null(timedOut.Error.HttpStatus);
        Assert.Contains(_logger.Entries, e => e.Message.Contains("timeout", StringComparison.Ordinal));
        Assert.Contains(_logger.Entries, e => e.Message.Contains(nameof(HttpRequestError.NameResolutionError), StringComparison.Ordinal));
        Assert.DoesNotContain(_logger.Entries, e => e.Message.Contains("No such host", StringComparison.Ordinal));

        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client().GetOverviewAsync(cancelled.Token));
    }

    // ---------- malformed answers ----------

    public static TheoryData<string, string> MalformedReadBodies() => new()
    {
        { "overview", "" },
        { "overview", "not json" },
        { "overview", "[]" },
        { "overview", "{}" },
        { "overview", """{"configs":{},"defaults":{}}""" },
        { "overview", """{"configs":[],"defaults":{"llm":null,"embedding":null}} trailing""" },
        { "overview", """{"configs":[{"name":"x","kind":"llm","provider":"azure-openai"}],"defaults":{}}""" },
        { "overview", """{"configs":[{"id":"a","name":"x","kind":"llm","provider":"azure-openai","settings":[]}],"defaults":{}}""" },
        { "overview", """{"configs":[{"id":"a","name":"x","kind":"llm","provider":"azure-openai"},{"id":"a","name":"y","kind":"llm","provider":"azure-openai"}],"defaults":{}}""" },
        { "overview", """{"configs":[],"defaults":{"llm":"11111111-1111-4111-8111-111111111111"}}""" },
        { "overview", """{"configs":[{"id":"a","name":"x","kind":"embedding","provider":"azure-openai"}],"defaults":{"llm":"a"}}""" },
        { "overview", """{"configs":[],"defaults":{"llm":42}}""" },
        { "sealed", """{"configs":[],"defaults":{},"sealed":"envelope"}""" },
        { "sealed", WithEnvelope(e => e.Remove("ciphertext")) },
        { "sealed", WithEnvelope(e => e["expiresAt"] = "in ten minutes") },
        { "sealed", WithEnvelope(e => e["envelopeId"] = "not-a-uuid") },
        { "sealed", WithEnvelope(e => e["kdf"]!["iterations"] = "600000") },
        { "sealed", WithEnvelope(e => e.Remove("code")) },
        { "sealed", WithEnvelope(e => e.Remove("attemptsAllowed")) },
        { "unlock", "" },
        { "unlock", "not json" },
        { "unlock", "{}" },
        { "unlock", $$"""{"envelopeId":"{{EnvelopeId}}"}""" },
        { "unlock", UnlockAnswer(key: UnlockKeyText[..42]) },
        { "unlock", UnlockAnswer(key: UnlockKeyText + "A") },
        { "unlock", UnlockAnswer(key: "!" + UnlockKeyText[1..]) },
        { "unlock", UnlockAnswer(key: UnlockKeyText + "=") },
        { "unlock", UnlockAnswer(envelopeId: "00000000-0000-4000-8000-000000000000") },
        { "unlock", $$"""{"envelopeId":"{{EnvelopeId}}","unlockKey":42}""" },
        { "unlock", $$"""{"envelopeId":"{{EnvelopeId}}","unlockKey":"{{UnlockKeyText}}","unlockKey":"{{UnlockKeyText}}"}""" },
        { "unlock", UnlockAnswer() + " {}" },
        { "unlock", "[" + UnlockAnswer() + "]" },
    };

    [Theory]
    [MemberData(nameof(MalformedReadBodies))]
    public async Task A_body_that_is_not_the_contract_is_an_invalid_response(string operation, string body)
    {
        _handler.Always(_ => FakeAdminHandler.Reply(200, body));

        TrainerConfigurationError error = (await RunAsync(Client(), operation))!;

        Assert.Equal(TrainerConfigurationErrorKind.InvalidResponse, error.Kind);
        Assert.Equal(200, error.HttpStatus);
    }

    [Fact]
    public async Task An_answer_over_one_megabyte_is_an_invalid_response_with_or_without_a_content_length()
    {
        JsonObject large = OverviewDocument();
        JsonArray configs = large["configs"]!.AsArray();
        for (int i = 0; i < 1500; i++)
        {
            configs.Add(new JsonObject
            {
                ["id"] = $"other-{i}", ["name"] = $"Other LLM {i}", ["kind"] = "llm", ["provider"] = "openai",
                ["settings"] = new JsonObject { ["model"] = "gpt-4o", ["notes"] = new string('n', 60) },
            });
        }

        byte[] fits = Encoding.UTF8.GetBytes(large.ToJsonString());
        byte[] tooLarge = Encoding.UTF8.GetBytes(large.ToJsonString() + new string(' ', AdminModelConfigsClient.MaxResponseBytes));
        Assert.InRange(fits.Length, 100_000, AdminModelConfigsClient.MaxResponseBytes);

        _handler.Always(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new UnknownLengthContent(fits) });
        TrainerOutcome<AdminModelConfigs> streamed = await Client().GetOverviewAsync(Token);
        _handler.Always(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new UnknownLengthContent(tooLarge) });
        TrainerOutcome<AdminModelConfigs> streamedTooLarge = await Client().GetOverviewAsync(Token);
        _handler.Always(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(tooLarge) });
        TrainerOutcome<AdminModelConfigs> declaredTooLarge = await Client().GetOverviewAsync(Token);

        Assert.Equal(1504, streamed.Value!.Configs.Count);
        Assert.Equal(TrainerConfigurationErrorKind.InvalidResponse, streamedTooLarge.Error!.Kind);
        Assert.Equal(TrainerConfigurationErrorKind.InvalidResponse, declaredTooLarge.Error!.Kind);
    }

    // ---------- unlock ----------

    [Fact]
    public async Task The_unlock_body_is_exactly_the_envelope_id_and_the_proof_and_the_key_comes_back_as_wipeable_bytes()
    {
        Serve();

        TrainerOutcome<AdminUnlockKey> outcome = await Client().UnlockAsync(EnvelopeId, Proof, Token);

        CapturedRequest request = Assert.Single(_handler.Requests);
        JsonObject body = JsonNode.Parse(request.Body)!.AsObject();
        Assert.Equal(["envelopeId", "proof"], body.Select(p => p.Key));
        Assert.Equal(EnvelopeId, body["envelopeId"]!.GetValue<string>());
        Assert.Equal(ProofText, body["proof"]!.GetValue<string>());
        Assert.Equal(43, ProofText.Length);
        Assert.Equal($$"""{"envelopeId":"{{EnvelopeId}}","proof":"{{ProofText}}"}""", request.BodyText);

        AdminUnlockKey key = outcome.Value!;
        Assert.Equal(EnvelopeId, key.EnvelopeId);
        Assert.Equal(UnlockKeyBytes, key.Bytes.ToArray());
        key.Dispose();
        Assert.True(key.IsWiped);
        Assert.Throws<ObjectDisposedException>(() => key.Bytes.ToArray());
        byte[] held = (byte[])typeof(AdminUnlockKey).GetField("_key", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(key)!;
        Assert.All(held, b => Assert.Equal(0, b));
    }

    [Fact]
    public async Task An_escaped_unlock_key_and_unknown_properties_of_the_answer_are_accepted()
    {
        string escaped = $"\\u{(int)UnlockKeyText[0]:x4}" + UnlockKeyText[1..];
        Serve(unlock: $$"""{"future":{"a":[1,2]},"envelopeId":"{{EnvelopeId.ToUpperInvariant()}}","unlockKey":"{{escaped}}"}""");

        using AdminUnlockKey key = (await Client().UnlockAsync(EnvelopeId, Proof, Token)).Value!;

        Assert.Equal(UnlockKeyBytes, key.Bytes.ToArray());
    }

    [Theory]
    [InlineData("not-a-uuid", 32)]
    [InlineData(EnvelopeId, 31)]
    [InlineData(EnvelopeId, 33)]
    public async Task A_malformed_envelope_id_or_proof_is_a_programming_error_and_nothing_is_sent(string envelopeId, int proofLength)
    {
        Serve();

        await Assert.ThrowsAsync<ArgumentException>(() => Client().UnlockAsync(envelopeId, new byte[proofLength], Token));

        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task No_proof_unlock_key_dev_token_or_api_key_appears_in_any_text_json_or_log()
    {
        Serve();
        AdminModelConfigsClient client = Client();
        TrainerOutcome<AdminModelConfigs> overview = await client.GetOverviewAsync(Token);
        TrainerOutcome<AdminSealedConfiguration> sealedOutcome = await client.GetSealedAsync(Token);
        TrainerOutcome<AdminUnlockKey> unlocked = await client.UnlockAsync(EnvelopeId, Proof, Token);
        AdminUnlockKey key = unlocked.Value!;
        _handler.Always(_ => FakeAdminHandler.Reply(401, """{"error":"invalidAccessCode","message":"Wrong code.","details":{"attemptsLeft":1}}"""));
        TrainerOutcome<AdminUnlockKey> wrong = await client.UnlockAsync(EnvelopeId, Proof, Token);

        string[] texts =
        [
            overview.ToString(), overview.Value!.ToString(), sealedOutcome.ToString(), sealedOutcome.Value!.ToString(),
            sealedOutcome.Value.Envelope.ToString(), sealedOutcome.Value.Document.ToString(), unlocked.ToString(), key.ToString(),
            wrong.ToString(), wrong.Error!.ToString(), JsonSerializer.Serialize(wrong.Error, ApiJson), JsonSerializer.Serialize(key, ApiJson),
            JsonSerializer.Serialize(sealedOutcome.Value.Overview, ApiJson),
            .. _logger.Entries.Select(e => e.Message),
        ];
        key.Dispose();

        foreach (string secret in new[] { ProofText, UnlockKeyText, DevToken, ApiKey })
        {
            Assert.All(texts, text => Assert.DoesNotContain(secret, text, StringComparison.Ordinal));
        }

        // What the log may say: the envelope, the status and the codes.
        Assert.Contains(_logger.Entries, e => e.Message.Contains($"envelope {EnvelopeId}", StringComparison.Ordinal) && e.Message.Contains("HTTP 401", StringComparison.Ordinal)
            && e.Message.Contains("invalidAccessCode", StringComparison.Ordinal));
        Assert.Contains("AttemptsLeft = 1", wrong.Error.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"attemptsLeft\":1", JsonSerializer.Serialize(wrong.Error, ApiJson), StringComparison.Ordinal);
    }

    // ---------- helpers ----------

    private AdminModelConfigsClient Client(string url = AdminUrl, TimeSpan? timeout = null)
    {
        AdminModelConfigsClient client = new(new Uri(url), ApiKey, () => _identity, _logger, timeout ?? TimeSpan.FromSeconds(10), _clock, _handler);
        _clients.Add(client);
        return client;
    }

    /// <summary>Answers the three routes with success bodies (the overview, the sealed document, the unlock answer).</summary>
    private void Serve(JsonObject? overview = null, JsonObject? sealedDocument = null, string? unlock = null)
    {
        string overviewText = (overview ?? OverviewDocument()).ToJsonString();
        string sealedText = (sealedDocument ?? SealedDocument()).ToJsonString();
        string unlockText = unlock ?? UnlockAnswer();
        _handler.Always(request =>
            request.Uri.AbsolutePath.EndsWith("/unlock", StringComparison.Ordinal) ? FakeAdminHandler.Reply(200, unlockText)
            : request.Uri.Query == "?sealed=1" ? FakeAdminHandler.Reply(200, sealedText)
            : FakeAdminHandler.Reply(200, overviewText));
    }

    private async Task<TrainerModelsOverview> OverviewAsync(JsonObject document)
    {
        Serve(overview: document);
        TrainerOutcome<AdminModelConfigs> outcome = await Client().GetOverviewAsync(Token);
        return TrainerModelsOverview.From(outcome.Value ?? throw new InvalidOperationException($"Overview failed: {outcome}"), simulated: false);
    }

    private static async Task<TrainerConfigurationError?> RunAsync(AdminModelConfigsClient client, string operation)
    {
        switch (operation)
        {
            case "overview":
                return (await client.GetOverviewAsync(Token)).Error;
            case "sealed":
                return (await client.GetSealedAsync(Token)).Error;
            default:
                TrainerOutcome<AdminUnlockKey> outcome = await client.UnlockAsync(EnvelopeId, Proof, Token);
                outcome.Value?.Dispose();
                return outcome.Error;
        }
    }

    private static (TrainerModelState, string?) State(TrainerKindOverview kind) => (kind.State, kind.Reason);

    private static JsonObject OverviewDocument() => JsonNode.Parse(OverviewJson)!.AsObject();

    private static JsonObject SealedDocument()
    {
        JsonObject document = OverviewDocument();
        document["sealed"] = JsonNode.Parse(EnvelopeJson);
        document["sealedConfigIds"] = new JsonArray(LlmId, EmbeddingId);
        document["sealedUnavailableReason"] = null;
        return document;
    }

    private static string WithEnvelope(Action<JsonObject> change)
    {
        JsonObject document = SealedDocument();
        change(document["sealed"]!.AsObject());
        return document.ToJsonString();
    }

    private static JsonObject Config(JsonObject document, string id) =>
        document["configs"]!.AsArray().Single(c => c!["id"]!.GetValue<string>() == id)!.AsObject();

    private static string UnlockAnswer(string envelopeId = EnvelopeId, string? key = null) =>
        $$"""{"envelopeId":"{{envelopeId}}","unlockKey":"{{key ?? UnlockKeyText}}"}""";

    /// <summary>A body whose length is not announced (chunked): the client grows its buffer as it reads.</summary>
    private sealed class UnknownLengthContent(byte[] content) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(content).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 25, 15, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class ListLogger : ILogger
    {
        public ConcurrentQueue<LogEntry> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Enqueue(new LogEntry(logLevel, formatter(state, exception) + (exception is null ? "" : " " + exception)));
    }
}
