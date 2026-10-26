using System.Collections.Concurrent;
using System.Net;
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
/// T11: the envelope of the Admin Dashboard opened exactly as contract v1 defines it (official test vector), the code format, the validation before
/// any derivation, the unlock body, the retry rules, the error mapping, the bundle mapping, and no secret in any text or log. The whole flow runs through
/// <see cref="TrainerConfigurationService"/> against <see cref="FakeModelConfigServer"/> (the server side of the contract): never the network.
/// </summary>
public sealed class AdminEnvelopeDecryptorTests : IDisposable
{
    private const string AdminUrl = "https://labs-admin.example.com";
    private const string AdminApiKey = "lbk_test_envelope_admin_api_key_44c1";
    private const string DevToken = "tok-envelope-dev-token-0a9e";
    private const string LlmId = "aaaaaaaa-1111-4111-8111-aaaaaaaaaaaa";
    private const string EmbeddingId = "bbbbbbbb-2222-4222-8222-bbbbbbbbbbbb";

    private static readonly DevIdentity Joined = new() { UserId = FakeModelConfigServer.VectorDevId.ToUpperInvariant(), Username = "john-dev", DevToken = DevToken };

    private static readonly JsonSerializerOptions ApiJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly FakeAdminHandler _handler = new();
    private readonly FakeModelConfigServer _server;
    private readonly ListLogger _logger = new();
    private readonly List<TrainerConfigurationService> _services = [];

    public AdminEnvelopeDecryptorTests()
    {
        _server = new FakeModelConfigServer(_handler);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        foreach (TrainerConfigurationService service in _services)
        {
            service.Dispose();
        }

        _handler.Dispose();
    }

    // ---------- the official test vector ----------

    [Theory]
    [InlineData("K7QX9")]
    [InlineData("k7qx-9")]
    [InlineData(" K7QX9 ")]
    public async Task The_official_test_vector_opens_with_the_code_however_it_is_typed(string typed)
    {
        TrainerConfigurationService service = Service();
        SealedTrainerConfiguration envelope = await RetrieveAsync(service);

        TrainerUnlockResult result = await service.UnlockAsync(envelope, typed, Token);

        Assert.Equal(TrainerUnlockStatus.Unlocked, result.Status);
        TrainerModelProfile chat = result.Bundle!.Chat!;
        Assert.Equal(FakeModelConfigServer.VectorApiKey, chat.ApiKey);
        Assert.Equal(("https://vector.openai.azure.com/", "gpt-vector"), (chat.Endpoint, chat.Deployment));
        Assert.Equal((FakeModelConfigServer.VectorConfigId, "Vector GPT", "2026-10-25T09:12:03.120Z"), (chat.ConfigId, chat.ConfigName, chat.ConfigUpdatedAt));
        Assert.Null(result.Bundle.Embedding);
        Assert.Equal(FakeModelConfigServer.VectorEnvelopeId, result.EnvelopeId);
        Assert.Null(result.Renewed);
        Assert.False(result.EnvelopeGone);

        // The server accepted the proof of the vector (and nothing else) and released its key once.
        CapturedRequest unlock = Assert.Single(_server.UnlockRequests);
        Assert.Equal(
            $$"""{"envelopeId":"{{FakeModelConfigServer.VectorEnvelopeId}}","proof":"{{FakeModelConfigServer.VectorProof}}"}""",
            unlock.BodyText);
        Assert.True(_server.IssuedEnvelopes.Single().Used);
    }

    [Fact]
    public async Task The_unlock_body_has_exactly_the_envelope_id_and_the_proof_and_never_the_code()
    {
        TrainerConfigurationService service = Service();
        await service.UnlockAsync(await RetrieveAsync(service), "K7QX9", Token);

        CapturedRequest unlock = Assert.Single(_server.UnlockRequests);
        using JsonDocument body = JsonDocument.Parse(unlock.Body!);
        Assert.Equal(["envelopeId", "proof"], body.RootElement.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(FakeModelConfigServer.VectorProof, body.RootElement.GetProperty("proof").GetString());
        Assert.DoesNotContain("K7QX9", unlock.BodyText!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DevToken, unlock.Bearer);
    }

    [Fact]
    public void The_independent_sealing_of_the_fake_server_matches_the_vector()
    {
        // Guards the test double itself: its PBKDF2/HMAC give the vector's code key and proof.
        byte[] salt = FakeModelConfigServer.FromBase64Url("AAECAwQFBgcICQoLDA0ODw");
        byte[] codeKey = FakeModelConfigServer.DeriveCodeKey("K7QX9", salt, 600_000);
        Assert.Equal(FakeModelConfigServer.VectorCodeKey, FakeModelConfigServer.ToBase64Url(codeKey));
        Assert.Equal(FakeModelConfigServer.VectorProof, FakeModelConfigServer.ProofOf(codeKey, FakeModelConfigServer.VectorEnvelopeId, FakeModelConfigServer.VectorDevId));
    }

    // ---------- tag failures: nothing is used ----------

    [Fact]
    public async Task A_wrong_code_with_a_released_key_fails_on_the_tag_and_nothing_is_used()
    {
        _server.AcceptAnyProof = true;
        await AssertTagFailureAsync(typed: "M4PRS");
    }

    [Fact]
    public async Task A_wrong_unlock_key_fails_on_the_tag_and_nothing_is_used()
    {
        _server.UnlockKeyOverride = FakeModelConfigServer.ToBase64Url(Enumerable.Repeat((byte)7, 32).ToArray());
        await AssertTagFailureAsync(typed: "K7QX9");
    }

    [Fact]
    public async Task Another_dev_id_fails_on_the_tag_and_nothing_is_used()
    {
        _server.AcceptAnyProof = true;
        await AssertTagFailureAsync(typed: "K7QX9", identity: new DevIdentity { UserId = "0d2b6c1e-4f3a-4e7b-b2a9-5c0d1e2f3a4b", Username = "x", DevToken = DevToken });
    }

    [Fact]
    public async Task A_modified_expires_at_fails_on_the_tag_and_nothing_is_used()
    {
        _server.MutateEnvelope = e => e["expiresAt"] = "2026-10-25T12:10:00.001Z";
        await AssertTagFailureAsync(typed: "K7QX9");
    }

    [Fact]
    public async Task A_tampered_ciphertext_fails_on_the_tag_and_nothing_is_used()
    {
        _server.MutateEnvelope = e =>
        {
            byte[] data = FakeModelConfigServer.FromBase64Url(e["ciphertext"]!.GetValue<string>());
            data[3] ^= 0x01;
            e["ciphertext"] = FakeModelConfigServer.ToBase64Url(data);
        };
        await AssertTagFailureAsync(typed: "K7QX9");
    }

    private async Task AssertTagFailureAsync(string typed, DevIdentity? identity = null)
    {
        TrainerConfigurationService service = Service(identity);
        SealedTrainerConfiguration envelope = await RetrieveAsync(service);

        TrainerUnlockResult result = await service.UnlockAsync(envelope, typed, Token);

        Assert.Equal(TrainerUnlockStatus.Failed, result.Status);
        Assert.Equal(TrainerConfigurationErrorKind.InvalidResponse, result.Error!.Kind);
        Assert.Equal(AdminEnvelopeDecryptor.EnvelopeUnreadableReason, result.Error.Reason);
        Assert.Null(result.Bundle);
        Assert.Single(_server.UnlockRequests);
    }

    // ---------- the envelope is validated before any derivation ----------

    [Theory]
    [InlineData("iterations1000")]
    [InlineData("iterationsHuge")]
    [InlineData("iterationsOverMax")]
    [InlineData("salt15")]
    [InlineData("salt17")]
    [InlineData("cipher")]
    [InlineData("version")]
    [InlineData("kdfName")]
    [InlineData("iv11")]
    [InlineData("ciphertextOnlyTag")]
    [InlineData("paddedSalt")]
    public async Task An_envelope_with_unsupported_parameters_is_refused_before_any_derivation_and_any_call(string defect)
    {
        _server.MutateEnvelope = e =>
        {
            switch (defect)
            {
                case "iterations1000": e["kdf"]!["iterations"] = 1000; break;
                case "iterationsHuge": e["kdf"]!["iterations"] = 2_000_000_000; break; // would take minutes if it were derived
                case "iterationsOverMax": e["kdf"]!["iterations"] = 10_000_001; break;
                case "salt15": e["kdf"]!["salt"] = FakeModelConfigServer.ToBase64Url(new byte[15]); break;
                case "salt17": e["kdf"]!["salt"] = FakeModelConfigServer.ToBase64Url(new byte[17]); break;
                case "cipher": e["cipher"] = "AES-128-GCM"; break;
                case "version": e["version"] = 2; break;
                case "kdfName": e["kdf"]!["name"] = "PBKDF2-HMAC-SHA1"; break;
                case "iv11": e["iv"] = FakeModelConfigServer.ToBase64Url(new byte[11]); break;
                case "ciphertextOnlyTag": e["ciphertext"] = FakeModelConfigServer.ToBase64Url(new byte[16]); break;
                case "paddedSalt": e["kdf"]!["salt"] = "AAECAwQFBgcICQoLDA0ODw=="; break;
            }
        };
        TrainerConfigurationService service = Service();
        SealedTrainerConfiguration envelope = await RetrieveAsync(service);

        TrainerUnlockResult result = await service.UnlockAsync(envelope, "K7QX9", Token);

        Assert.Equal(TrainerConfigurationErrorKind.InvalidResponse, result.Error!.Kind);
        Assert.Equal(AdminEnvelopeDecryptor.InvalidEnvelopeReason, result.Error.Reason);
        Assert.Empty(_server.UnlockRequests);
    }

    [Theory]
    [InlineData("0OIL1")]
    [InlineData("K7QX")]
    [InlineData("K7QX99")]
    [InlineData("K7Q!9")]
    [InlineData("")]
    [InlineData(null)]
    public async Task A_code_outside_the_format_is_refused_locally_with_no_request(string? typed)
    {
        TrainerConfigurationService service = Service();
        SealedTrainerConfiguration envelope = await RetrieveAsync(service);
        int before = _handler.Requests.Count;

        TrainerUnlockResult result = await service.UnlockAsync(envelope, typed, Token);

        Assert.Equal(TrainerUnlockStatus.InvalidCodeFormat, result.Status);
        Assert.Equal(before, _handler.Requests.Count);
        Assert.Empty(_server.UnlockRequests);
    }

    [Fact]
    public async Task The_code_format_of_the_envelope_replaces_the_default_one_when_it_has_one()
    {
        _server.MutateEnvelope = e => e["code"] = new JsonObject { ["length"] = 6, ["alphabet"] = FakeModelConfigServer.Alphabet };
        TrainerConfigurationService service = Service();
        SealedTrainerConfiguration envelope = await RetrieveAsync(service);

        TrainerUnlockResult five = await service.UnlockAsync(envelope, "K7QX9", Token);

        Assert.Equal(TrainerUnlockStatus.InvalidCodeFormat, five.Status);
        Assert.Empty(_server.UnlockRequests);
    }

    // ---------- Rule 20: errors and the single retry ----------

    [Theory]
    [InlineData("expired")]
    [InlineData("unknown")]
    [InlineData("keyRotated")]
    public async Task An_envelope_that_expired_is_replaced_and_the_unlock_retried_once_with_the_same_code(string reason)
    {
        _server.UnlockOverrides.Enqueue(() => Gone(reason));
        TrainerConfigurationService service = Service();
        SealedTrainerConfiguration envelope = await RetrieveAsync(service);

        TrainerUnlockResult result = await service.UnlockAsync(envelope, "K7QX9", Token);

        Assert.Equal(TrainerUnlockStatus.Unlocked, result.Status);
        Assert.NotNull(result.Renewed);
        Assert.True(result.EnvelopeGone);
        string newId = _server.IssuedEnvelopes[1].EnvelopeId;
        Assert.Equal(newId, result.EnvelopeId);
        Assert.Equal(newId, result.Renewed.Payload.Admin!.Envelope.EnvelopeId);
        Assert.Equal(FakeModelConfigServer.ApiKeyOf(FakeModelConfigServer.VectorConfigId), result.Bundle!.Chat!.ApiKey);
        Assert.Equal(2, _server.SealedRequests.Count());
        Assert.Equal(2, _server.UnlockRequests.Count());
        Assert.Contains($"\"envelopeId\":\"{newId}\"", _server.UnlockRequests.Last().BodyText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("exhausted")]
    [InlineData("used")]
    [InlineData("configChanged")]
    [InlineData("workshopChanged")]
    [InlineData("workshopClosed")]
    [InlineData("accessCodeChanged")]
    [InlineData("accessCodeRevoked")]
    [InlineData("accessCodeExpired")]
    [InlineData("accessCodeUnreadable")]
    [InlineData("someFutureReason")]
    public async Task The_other_reasons_do_not_retry_and_are_reported(string reason)
    {
        _server.UnlockOverrides.Enqueue(() => Gone(reason));
        TrainerConfigurationService service = Service();
        SealedTrainerConfiguration envelope = await RetrieveAsync(service);

        TrainerUnlockResult result = await service.UnlockAsync(envelope, "K7QX9", Token);

        Assert.Equal(TrainerConfigurationErrorKind.EnvelopeExpired, result.Error!.Kind);
        Assert.Equal(reason, result.Error.Reason);
        Assert.Equal(410, result.Error.HttpStatus);
        Assert.Null(result.Renewed);
        Assert.True(result.EnvelopeGone);
        Assert.Single(_server.SealedRequests);
        Assert.Single(_server.UnlockRequests);
    }

    [Fact]
    public async Task There_is_at_most_one_retry()
    {
        _server.UnlockOverrides.Enqueue(() => Gone("expired"));
        _server.UnlockOverrides.Enqueue(() => Gone("keyRotated"));
        TrainerConfigurationService service = Service();
        SealedTrainerConfiguration envelope = await RetrieveAsync(service);

        TrainerUnlockResult result = await service.UnlockAsync(envelope, "K7QX9", Token);

        Assert.Equal(TrainerConfigurationErrorKind.EnvelopeExpired, result.Error!.Kind);
        Assert.Equal("keyRotated", result.Error.Reason);
        Assert.Equal(2, _server.SealedRequests.Count());
        Assert.Equal(2, _server.UnlockRequests.Count());
        // The second envelope is the one in use now: the caller replaces its own.
        Assert.Equal(_server.IssuedEnvelopes[1].EnvelopeId, result.EnvelopeId);
        Assert.NotNull(result.Renewed);
    }

    [Fact]
    public async Task A_retry_that_cannot_fetch_a_new_envelope_returns_the_error_of_that_fetch()
    {
        _server.UnlockOverrides.Enqueue(() => Gone("expired"));
        _server.SealedOverrides.Enqueue(null); // the participant's own retrieval
        _server.SealedOverrides.Enqueue(() => FakeAdminHandler.Reply(429,
            """{"error":"rateLimited","message":"x","details":{"reason":"tooManyEnvelopes","retryAfterSeconds":30}}""", retryAfterSeconds: 30));
        TrainerConfigurationService service = Service();
        SealedTrainerConfiguration envelope = await RetrieveAsync(service);

        TrainerUnlockResult result = await service.UnlockAsync(envelope, "K7QX9", Token);

        Assert.Equal(TrainerConfigurationErrorKind.RateLimited, result.Error!.Kind);
        Assert.Equal("tooManyEnvelopes", result.Error.Reason);
        Assert.Equal(30, result.Error.RetryAfterSeconds);
        Assert.True(result.EnvelopeGone);
        Assert.Null(result.Renewed);
        Assert.Single(_server.UnlockRequests);
    }

    [Fact]
    public async Task A_retry_whose_new_envelope_is_unavailable_returns_that_reason()
    {
        _server.UnlockOverrides.Enqueue(() => Gone("keyRotated"));
        _server.SealedOverrides.Enqueue(null);
        _server.SealedOverrides.Enqueue(() => FakeAdminHandler.Reply(200, $$"""
            {"configs":[],"defaults":{"llm":null,"embedding":null},"sealed":null,"sealedConfigIds":[],"unsealedConfigs":[],
             "sealedUnavailableReason":"noAccessCode","serverTime":"2026-10-25T12:00:00.000Z"}
            """));
        TrainerConfigurationService service = Service();
        SealedTrainerConfiguration envelope = await RetrieveAsync(service);

        TrainerUnlockResult result = await service.UnlockAsync(envelope, "K7QX9", Token);

        Assert.Equal(TrainerConfigurationErrorKind.SealedUnavailable, result.Error!.Kind);
        Assert.Equal("noAccessCode", result.Error.Reason);
    }

    [Fact]
    public async Task A_wrong_code_keeps_the_attempts_left_of_the_envelope()
    {
        TrainerConfigurationService service = Service();
        SealedTrainerConfiguration envelope = await RetrieveAsync(service);

        TrainerUnlockResult first = await service.UnlockAsync(envelope, "M4PRS", Token);
        TrainerUnlockResult second = await service.UnlockAsync(envelope, "M4PRT", Token);

        Assert.Equal(TrainerConfigurationErrorKind.InvalidCode, first.Error!.Kind);
        Assert.Equal(2, first.Error.AttemptsLeft);
        Assert.Equal(1, second.Error!.AttemptsLeft);
        Assert.False(first.EnvelopeGone);
        Assert.Null(first.Renewed);
        Assert.Equal(FakeModelConfigServer.VectorEnvelopeId, second.EnvelopeId);
        Assert.Null(first.Bundle);

        // The right code still opens it afterwards.
        Assert.Equal(TrainerUnlockStatus.Unlocked, (await service.UnlockAsync(envelope, "K7QX9", Token)).Status);
    }

    [Fact]
    public async Task A_wrong_code_after_a_retry_reports_the_new_envelope()
    {
        _server.VectorFirst = false;
        _server.Code = "M4PRS";
        _server.UnlockOverrides.Enqueue(() => Gone("expired"));
        TrainerConfigurationService service = Service();
        SealedTrainerConfiguration envelope = await RetrieveAsync(service);

        TrainerUnlockResult result = await service.UnlockAsync(envelope, "K7QX9", Token);

        Assert.Equal(TrainerConfigurationErrorKind.InvalidCode, result.Error!.Kind);
        Assert.Equal(2, result.Error.AttemptsLeft); // the attempts of the new envelope
        Assert.Equal(_server.IssuedEnvelopes[1].EnvelopeId, result.EnvelopeId);
        Assert.NotNull(result.Renewed);
    }

    [Theory]
    [InlineData(429, """{"error":"rateLimited","message":"x","details":{"reason":"lockedOut","retryAfterSeconds":120}}""", TrainerConfigurationErrorKind.RateLimited, "lockedOut")]
    [InlineData(429, """{"error":"rateLimited","message":"x","details":{"reason":"accessCodeFrozen","retryAfterSeconds":120}}""", TrainerConfigurationErrorKind.RateLimited, "accessCodeFrozen")]
    [InlineData(403, """{"error":"forbidden","message":"x"}""", TrainerConfigurationErrorKind.Forbidden, null)]
    [InlineData(401, """{"error":"unauthorized","message":"x"}""", TrainerConfigurationErrorKind.Unauthorized, null)]
    [InlineData(503, """{"error":"unavailable","message":"x"}""", TrainerConfigurationErrorKind.Unavailable, "unavailable")]
    public async Task Rate_limits_and_server_errors_are_passed_through_unchanged_without_retry(int status, string body, TrainerConfigurationErrorKind kind, string? reason)
    {
        _server.UnlockOverrides.Enqueue(() => FakeAdminHandler.Reply(status, body, retryAfterSeconds: status == 429 ? 120 : null));
        TrainerConfigurationService service = Service();
        SealedTrainerConfiguration envelope = await RetrieveAsync(service);

        TrainerUnlockResult result = await service.UnlockAsync(envelope, "K7QX9", Token);

        Assert.Equal(kind, result.Error!.Kind);
        Assert.Equal(reason, result.Error.Reason);
        Assert.Equal(status, result.Error.HttpStatus);
        Assert.False(result.EnvelopeGone);
        Assert.Single(_server.UnlockRequests);
        Assert.Single(_server.SealedRequests);
    }

    [Fact]
    public async Task A_network_failure_is_passed_through_without_retry()
    {
        _server.UnlockOverrides.Enqueue(() => throw new HttpRequestException("connection refused"));
        TrainerConfigurationService service = Service();
        SealedTrainerConfiguration envelope = await RetrieveAsync(service);

        TrainerUnlockResult result = await service.UnlockAsync(envelope, "K7QX9", Token);

        Assert.Equal(TrainerConfigurationErrorKind.Network, result.Error!.Kind);
        Assert.Single(_server.UnlockRequests);
        Assert.Single(_server.SealedRequests);
    }

    // ---------- the plaintext ----------

    [Theory]
    [InlineData("wrongVersion")]
    [InlineData("noVersion")]
    [InlineData("otherEnvelopeId")]
    [InlineData("noEnvelopeId")]
    [InlineData("notJson")]
    [InlineData("noConfigs")]
    public async Task A_plaintext_that_is_not_the_one_of_this_envelope_is_refused(string defect)
    {
        _server.VectorFirst = false;
        _server.Plaintext = id => defect switch
        {
            "wrongVersion" => $$$$"""{"v":2,"envelopeId":"{{{{id}}}}","configs":{"{{{{FakeModelConfigServer.VectorConfigId}}}}":{"apiKey":"k"}}}""",
            "noVersion" => $$$$"""{"envelopeId":"{{{{id}}}}","configs":{"{{{{FakeModelConfigServer.VectorConfigId}}}}":{"apiKey":"k"}}}""",
            "otherEnvelopeId" => $$$$"""{"v":1,"envelopeId":"3f1c7a52-6a0e-4b8e-9a51-2f6c1d0e9b77","configs":{"{{{{FakeModelConfigServer.VectorConfigId}}}}":{"apiKey":"k"}}}""",
            "noEnvelopeId" => $$$$"""{"v":1,"configs":{"{{{{FakeModelConfigServer.VectorConfigId}}}}":{"apiKey":"k"}}}""",
            "notJson" => "this is not json",
            _ => $$"""{"v":1,"envelopeId":"{{id}}"}""",
        };
        TrainerConfigurationService service = Service();
        SealedTrainerConfiguration envelope = await RetrieveAsync(service);

        TrainerUnlockResult result = await service.UnlockAsync(envelope, "K7QX9", Token);

        Assert.Equal(TrainerConfigurationErrorKind.InvalidResponse, result.Error!.Kind);
        Assert.Equal(AdminEnvelopeDecryptor.InvalidPlaintextReason, result.Error.Reason);
        Assert.Null(result.Bundle);
    }

    // ---------- Rule 17: the bundle ----------

    [Fact]
    public async Task Both_defaults_are_mapped_with_their_endpoint_deployment_key_and_configuration_identity()
    {
        UseConfigs(
            FakeModelConfigServer.Config(LlmId, "llm", "Workshop GPT", "https://llm.openai.azure.com/", "gpt-4o-mini", updatedAt: "2026-10-25T09:00:00.000Z"),
            FakeModelConfigServer.Config(EmbeddingId, "embedding", "Workshop embeddings", "https://embed.openai.azure.com/", "text-embedding-3-small", updatedAt: "2026-10-25T09:05:00.000Z"));

        TrainerConfigurationBundle bundle = (await UnlockAsync()).Bundle!;

        Assert.Equal((ModelKind.Chat, "https://llm.openai.azure.com/", "gpt-4o-mini", FakeModelConfigServer.ApiKeyOf(LlmId)),
            (bundle.Chat!.Kind, bundle.Chat.Endpoint, bundle.Chat.Deployment, bundle.Chat.ApiKey));
        Assert.Equal((LlmId, "Workshop GPT", "2026-10-25T09:00:00.000Z"), (bundle.Chat.ConfigId, bundle.Chat.ConfigName, bundle.Chat.ConfigUpdatedAt));
        Assert.Equal((ModelKind.Embedding, "https://embed.openai.azure.com/", "text-embedding-3-small", FakeModelConfigServer.ApiKeyOf(EmbeddingId)),
            (bundle.Embedding!.Kind, bundle.Embedding.Endpoint, bundle.Embedding.Deployment, bundle.Embedding.ApiKey));
        Assert.Equal((EmbeddingId, "Workshop embeddings", "2026-10-25T09:05:00.000Z"), (bundle.Embedding.ConfigId, bundle.Embedding.ConfigName, bundle.Embedding.ConfigUpdatedAt));
    }

    [Fact]
    public async Task A_default_of_another_provider_is_absent_from_the_bundle()
    {
        UseConfigs(
            FakeModelConfigServer.Config(LlmId, "llm", "OpenAI", "https://api.openai.com/", "gpt-4o", provider: "openai"),
            FakeModelConfigServer.Config(EmbeddingId, "embedding", "Workshop embeddings", "https://embed.openai.azure.com/", "text-embedding-3-small"));

        TrainerConfigurationBundle bundle = (await UnlockAsync()).Bundle!;

        Assert.Null(bundle.Chat);
        Assert.NotNull(bundle.Embedding);
    }

    [Fact]
    public async Task An_unsealed_default_is_absent_from_the_bundle()
    {
        UseConfigs(
            FakeModelConfigServer.Config(LlmId, "llm", "Workshop GPT", "https://llm.openai.azure.com/", "gpt-4o-mini"),
            FakeModelConfigServer.Config(EmbeddingId, "embedding", "Workshop embeddings", "https://embed.openai.azure.com/", "text-embedding-3-small"));
        _server.SealedIds = [LlmId];
        _server.UnsealedIds = [EmbeddingId];

        TrainerConfigurationBundle bundle = (await UnlockAsync()).Bundle!;

        Assert.NotNull(bundle.Chat);
        Assert.Null(bundle.Embedding);
    }

    [Fact]
    public async Task A_key_that_the_plaintext_does_not_hold_is_an_invalid_response()
    {
        UseConfigs(FakeModelConfigServer.Config(LlmId, "llm", "Workshop GPT", "https://llm.openai.azure.com/", "gpt-4o-mini"));
        _server.Plaintext = id => $$$$"""{"v":1,"envelopeId":"{{{{id}}}}","configs":{"{{{{EmbeddingId}}}}":{"apiKey":"other-key-not-a-real-secret"}}}""";

        TrainerUnlockResult result = await UnlockAsync();

        Assert.Equal(TrainerConfigurationErrorKind.InvalidResponse, result.Error!.Kind);
        Assert.Equal(AdminEnvelopeDecryptor.MissingKeyReason, result.Error.Reason);
        Assert.Null(result.Bundle);
    }

    [Fact]
    public async Task A_configuration_without_an_api_key_secret_gives_a_profile_without_key()
    {
        UseConfigs(FakeModelConfigServer.Config(LlmId, "llm", "Entra GPT", "https://llm.openai.azure.com/", "gpt-4o-mini", apiKey: false));
        _server.SealedIds = [LlmId];

        TrainerConfigurationBundle bundle = (await UnlockAsync()).Bundle!;

        Assert.Null(bundle.Chat!.ApiKey);
        Assert.Equal(LlmId, bundle.Chat.ConfigId);
    }

    [Fact]
    public async Task No_usable_default_at_all_is_an_invalid_response()
    {
        UseConfigs(FakeModelConfigServer.Config(LlmId, "llm", "OpenAI", "https://api.openai.com/", "gpt-4o", provider: "openai"));

        TrainerUnlockResult result = await UnlockAsync();

        Assert.Equal(TrainerConfigurationErrorKind.InvalidResponse, result.Error!.Kind);
    }

    [Fact]
    public async Task The_profile_updates_carry_the_key_and_the_identity_stays_on_the_profile()
    {
        UseConfigs(FakeModelConfigServer.Config(LlmId, "llm", "Workshop GPT", "https://llm.openai.azure.com/", "gpt-4o-mini"));
        TrainerConfigurationBundle bundle = (await UnlockAsync()).Bundle!;

        TrainerProfileUpdate update = Assert.Single(bundle.ToProfileUpdates([ModelKind.Chat]));

        Assert.Equal(FakeModelConfigServer.ApiKeyOf(LlmId), update.ApiKey);
        Assert.Equal("https://llm.openai.azure.com/", update.Endpoint);
    }

    // ---------- nothing secret anywhere ----------

    [Fact]
    public async Task No_secret_appears_in_any_text_json_or_log()
    {
        TrainerConfigurationService service = Service();
        SealedTrainerConfiguration envelope = await RetrieveAsync(service);
        TrainerUnlockResult wrong = await service.UnlockAsync(envelope, "M4PRS", Token);
        _server.UnlockOverrides.Enqueue(() => Gone("expired"));
        TrainerUnlockResult right = await service.UnlockAsync(envelope, " k7qx-9 ", Token);
        TrainerUnlockResult refused = await service.UnlockAsync(envelope, "0OIL1", Token);

        TrainerConfigurationBundle bundle = right.Bundle!;
        string[] texts =
        [
            envelope.ToString(), envelope.Payload.ToString(), envelope.Payload.Admin!.ToString(), envelope.Payload.Admin.Envelope.ToString(),
            wrong.ToString(), right.ToString(), refused.ToString(), wrong.Error!.ToString(), bundle.ToString(), bundle.Chat!.ToString(),
            right.Renewed!.ToString(), right.Renewed.Payload.ToString(),
            JsonSerializer.Serialize(wrong, ApiJson), JsonSerializer.Serialize(right, ApiJson), JsonSerializer.Serialize(bundle, ApiJson),
            string.Join(" | ", bundle.ToProfileUpdates([ModelKind.Chat])),
            .. _logger.Entries.Select(e => e.Message),
        ];
        string[] secrets =
        [
            FakeModelConfigServer.VectorCode, "K7QX", "7QX9", "M4PRS", FakeModelConfigServer.VectorProof, FakeModelConfigServer.VectorCodeKey,
            FakeModelConfigServer.VectorUnlockKey, FakeModelConfigServer.VectorApiKey, FakeModelConfigServer.ApiKeyOf(FakeModelConfigServer.VectorConfigId),
            "api-key-", "not-a-real-secret", _server.IssuedEnvelopes[1].Proof, _server.IssuedEnvelopes[1].UnlockKey, "labs-admin/model-unlock",
        ];

        Assert.NotEmpty(_logger.Entries);
        foreach (string text in texts)
        {
            foreach (string secret in secrets)
            {
                Assert.DoesNotContain(secret, text, StringComparison.OrdinalIgnoreCase);
            }
        }

        // What is not secret stays visible.
        Assert.Contains(_server.IssuedEnvelopes[1].EnvelopeId, right.ToString(), StringComparison.Ordinal);
        Assert.Contains("ApiKey = set", bundle.Chat.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_cancelled_unlock_throws_and_leaves_no_secret_in_the_log()
    {
        TrainerConfigurationService service = Service();
        SealedTrainerConfiguration envelope = await RetrieveAsync(service);
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await service.UnlockAsync(envelope, "K7QX9", cancelled.Token));

        Assert.Empty(_server.UnlockRequests);
        Assert.DoesNotContain(_logger.Entries, e => e.Message.Contains("K7QX9", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_code_format_is_five_characters_of_the_alphabet_once_normalized()
    {
        Assert.Equal("K7QX9", TrainerCodeFormat.Normalize(" k7-qx 9 "));
        Assert.Null(TrainerCodeFormat.Normalize("0OIL1"));
        Assert.Null(TrainerCodeFormat.Normalize("K7QX90"));
        Assert.True(TrainerConfigurationService.IsValidCode("k7qx-9"));
        Assert.False(TrainerConfigurationService.IsValidCode("123456"));
        Assert.True(TrainerConfigurationService.IsValidCode(MockTrainerConfiguration.DefaultCode));
        Assert.Equal(5, MockTrainerConfiguration.DefaultCode.Length);
    }

    // ---------- helpers ----------

    private TrainerConfigurationService Service(DevIdentity? identity = null)
    {
        DashboardOptions options = new() { Reporting = { ServerUrl = AdminUrl, ApiKey = AdminApiKey } };
        DevIdentity current = identity ?? Joined;
        TrainerConfigurationService service = new(options, () => current, _logger, null, _ => null, _handler);
        _services.Add(service);
        return service;
    }

    private static async Task<SealedTrainerConfiguration> RetrieveAsync(TrainerConfigurationService service)
    {
        TrainerOutcome<SealedTrainerConfiguration> retrieved = await service.RetrieveAsync(Token);
        return retrieved.Value ?? throw new InvalidOperationException($"Not retrieved: {retrieved}");
    }

    private async Task<TrainerUnlockResult> UnlockAsync()
    {
        TrainerConfigurationService service = Service();
        return await service.UnlockAsync(await RetrieveAsync(service), _server.Code, Token);
    }

    /// <summary>Configurations sealed here (not the vector): the first LLM is the default LLM, the embedding one the default embedding.</summary>
    private void UseConfigs(params JsonObject[] configs)
    {
        _server.VectorFirst = false;
        _server.Configs = [.. configs];
        _server.DefaultLlm = configs.FirstOrDefault(c => c["kind"]!.GetValue<string>() == "llm")?["id"]?.GetValue<string>();
        _server.DefaultEmbedding = configs.FirstOrDefault(c => c["kind"]!.GetValue<string>() == "embedding")?["id"]?.GetValue<string>();
        _server.SealedIds = configs.Where(c => c["secretFields"]!.AsArray().Count > 0).Select(c => c["id"]!.GetValue<string>()).ToList();
    }

    private static HttpResponseMessage Gone(string reason) =>
        FakeAdminHandler.Reply(410, $$$"""{"error":"envelopeExpired","message":"x","details":{"reason":"{{{reason}}}"}}""");

    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class ListLogger : ILogger<TrainerConfigurationService>
    {
        public ConcurrentQueue<LogEntry> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Enqueue(new LogEntry(logLevel, formatter(state, exception)));
            if (exception is not null)
            {
                Entries.Enqueue(new LogEntry(logLevel, exception.ToString()));
            }
        }
    }
}
