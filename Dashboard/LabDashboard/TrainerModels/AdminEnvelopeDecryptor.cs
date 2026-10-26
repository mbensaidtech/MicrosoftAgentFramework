using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LabDashboard.Settings;

namespace LabDashboard.TrainerModels;

/// <summary>
/// Opens the sealed envelope of the Admin Dashboard exactly as contract v1 defines it (Business Rules 13, 17, 18, 20; "Envelope protocol v1" of
/// <c>local-dashboard-contract.md</c>), with the .NET base library only:
/// <list type="number">
/// <item>validate the envelope before anything (version, cipher, KDF name and bounds, sizes): otherwise <c>invalidResponse</c>, nothing derived;</item>
/// <item><c>K_code</c> = PBKDF2-HMAC-SHA256 of the code; proof = HMAC(<c>K_code</c>, <c>labs-admin/model-unlock/v1|envelopeId|devId</c>);</item>
/// <item>the raw unlock call (<see cref="AdminModelConfigsClient.UnlockAsync"/>); <c>K_env</c> = HKDF-SHA256(<c>K_code ‖ unlockKey</c>, empty salt,
/// <c>labs-admin/model-envelope/v1|envelopeId|devId</c>); AES-256-GCM with the associated data
/// <c>labs-admin/model-envelope/v1|envelopeId|devId|expiresAt</c> (verbatim);</item>
/// <item>deserialize the plaintext, then wipe it; check <c>v</c> and <c>envelopeId</c>; map the defaults to the profiles (Rule 17).</item>
/// </list>
/// A <c>410 envelopeExpired</c> with the reason <c>expired</c>, <c>unknown</c> or <c>keyRotated</c> fetches a new envelope and retries once with the same code
/// (<see cref="TrainerUnsealResult.Renewed"/>); the other 410 reasons and every other error are returned as the client reports them.
/// The code, the proof, <c>K_code</c>, the unlock key, <c>K_env</c> and the plaintext are wiped as soon as they are no longer needed and never reach a log,
/// an exception message or a result. Only envelope ids, statuses and reasons are logged.
/// </summary>
internal sealed class AdminEnvelopeDecryptor(AdminModelConfigsClient client, ILogger? logger = null) : ITrainerConfigurationDecryptor
{
    public const string UnlockLabel = "labs-admin/model-unlock/v1";
    public const string EnvelopeLabel = "labs-admin/model-envelope/v1";

    public const int EnvelopeVersion = 1;
    public const string Cipher = "AES-256-GCM";
    public const string KdfName = "PBKDF2-HMAC-SHA256";
    public const int MinIterations = 600_000;
    public const int MaxIterations = 10_000_000;

    // Values of TrainerConfigurationError.Reason set here (identifiers only).
    public const string InvalidEnvelopeReason = "invalidEnvelope";
    public const string InvalidCodeFormatReason = "invalidCodeFormat";
    public const string EnvelopeUnreadableReason = "envelopeUnreadable";
    public const string InvalidPlaintextReason = "invalidPlaintext";
    public const string MissingKeyReason = "missingKey";

    private const int KeyLength = 32;
    private const int SaltLength = 16;
    private const int IvLength = 12;
    private const int TagLength = 16;

    private static readonly JsonSerializerOptions PlaintextOptions = new(JsonSerializerDefaults.Web) { MaxDepth = 8 };

    private readonly ILogger _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

    /// <summary>The raw unlock call.</summary>
    internal AdminModelConfigsClient Client { get; } = client;

    /// <summary>The <c>410</c> reasons after which a new envelope is fetched and the unlock retried once with the same code (Rule 20).</summary>
    public static bool IsRetryableReason(string? reason) => reason is "expired" or "unknown" or "keyRotated";

    public async ValueTask<TrainerUnsealResult> UnsealAsync(SealedTrainerPayload payload, string code, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (payload.Admin is not { } current)
        {
            return TrainerUnsealResult.Failure(TrainerConfigurationErrorKind.InvalidResponse);
        }

        SealedTrainerPayload? renewed = null;
        bool envelopeGone = false;
        for (int attempt = 0; ; attempt++)
        {
            Attempt result = await OpenAsync(current, code, cancellationToken);
            if (result.Error is null)
            {
                return TrainerUnsealResult.Success(result.Profiles!, renewed);
            }

            TrainerConfigurationError error = result.Error;
            if (error.Kind != TrainerConfigurationErrorKind.EnvelopeExpired)
            {
                return TrainerUnsealResult.Failure(error, renewed, envelopeGone);
            }

            // The server no longer accepts this envelope, whatever the reason.
            envelopeGone = true;
            if (attempt > 0 || !IsRetryableReason(error.Reason))
            {
                return TrainerUnsealResult.Failure(error, renewed, envelopeGone);
            }

            _logger.LogInformation("Admin envelope {EnvelopeId} refused ({Reason}): fetching a new envelope and retrying once.", current.Envelope.EnvelopeId, error.Reason);
            TrainerOutcome<AdminSealedConfiguration> fresh = await Client.GetSealedAsync(cancellationToken);
            if (!fresh.Succeeded)
            {
                return TrainerUnsealResult.Failure(fresh.Error, renewed: null, envelopeGone: true);
            }

            current = fresh.Value;
            renewed = new SealedTrainerPayload(current);
        }
    }

    /// <summary>One opening of one envelope: nothing derived until the envelope and the code are valid.</summary>
    private async ValueTask<Attempt> OpenAsync(AdminSealedConfiguration configuration, string code, CancellationToken cancellationToken)
    {
        AdminEnvelope envelope = configuration.Envelope;
        if (!TryValidate(envelope, out ParsedEnvelope? parsed))
        {
            return Fail(envelope, new TrainerConfigurationError(TrainerConfigurationErrorKind.InvalidResponse) { Reason = InvalidEnvelopeReason });
        }

        if (TrainerCodeFormat.Normalize(code, envelope.Code.Length, envelope.Code.Alphabet) is not { } normalized)
        {
            return Fail(envelope, new TrainerConfigurationError(TrainerConfigurationErrorKind.InvalidCode) { Reason = InvalidCodeFormatReason });
        }

        string envelopeId = envelope.EnvelopeId;
        string devId = configuration.DevId;
        byte[] codeBytes = Encoding.UTF8.GetBytes(normalized);
        byte[]? codeKey = null;
        byte[]? proof = null;
        AdminUnlockKey? unlockKey = null;
        try
        {
            byte[] salt = parsed.Salt;
            int iterations = envelope.Kdf.Iterations;
            codeKey = await Task.Run(() => Rfc2898DeriveBytes.Pbkdf2(codeBytes, salt, iterations, HashAlgorithmName.SHA256, KeyLength), cancellationToken);
            proof = HMACSHA256.HashData(codeKey, Encoding.UTF8.GetBytes($"{UnlockLabel}|{envelopeId}|{devId}"));

            TrainerOutcome<AdminUnlockKey> unlocked = await Client.UnlockAsync(envelopeId, proof, cancellationToken);
            if (!unlocked.Succeeded)
            {
                _logger.LogInformation("Admin envelope {EnvelopeId}: unlock refused ({Kind}, {Reason}).", envelopeId, unlocked.Error.Kind, unlocked.Error.Reason);
                return new Attempt(null, unlocked.Error);
            }

            unlockKey = unlocked.Value;
            return Decrypt(configuration, parsed, codeKey, unlockKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(codeBytes);
            if (codeKey is not null)
            {
                CryptographicOperations.ZeroMemory(codeKey);
            }

            if (proof is not null)
            {
                CryptographicOperations.ZeroMemory(proof);
            }

            unlockKey?.Dispose();
        }
    }

    /// <summary>Steps 5 to 7 and the mapping: synchronous, so that the key material only lives in this method's buffers.</summary>
    private Attempt Decrypt(AdminSealedConfiguration configuration, ParsedEnvelope parsed, byte[] codeKey, AdminUnlockKey unlockKey)
    {
        AdminEnvelope envelope = configuration.Envelope;
        string context = $"{EnvelopeLabel}|{envelope.EnvelopeId}|{configuration.DevId}";
        ReadOnlySpan<byte> share = unlockKey.Bytes;
        byte[] ikm = new byte[codeKey.Length + share.Length];
        byte[]? envelopeKey = null;
        byte[]? plaintext = null;
        try
        {
            codeKey.CopyTo(ikm, 0);
            share.CopyTo(ikm.AsSpan(codeKey.Length));
            envelopeKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, KeyLength, salt: [], info: Encoding.UTF8.GetBytes(context));

            int length = parsed.Data.Length - TagLength;
            plaintext = new byte[length];
            try
            {
                using AesGcm aes = new(envelopeKey, TagLength);
                aes.Decrypt(parsed.Iv, parsed.Data.AsSpan(0, length), parsed.Data.AsSpan(length), plaintext, Encoding.UTF8.GetBytes($"{context}|{envelope.ExpiresAt}"));
            }
            catch (CryptographicException)
            {
                // A wrong code or tampering (after an accepted proof: tampering): nothing of the output is used.
                return Fail(envelope, new TrainerConfigurationError(TrainerConfigurationErrorKind.InvalidResponse) { Reason = EnvelopeUnreadableReason });
            }

            OpenedEnvelope? opened;
            try
            {
                opened = JsonSerializer.Deserialize<OpenedEnvelope>(plaintext, PlaintextOptions);
            }
            catch (JsonException)
            {
                opened = null;
            }
            finally
            {
                // Objects now: the buffer is no longer needed.
                CryptographicOperations.ZeroMemory(plaintext);
            }

            if (opened is not { V: EnvelopeVersion, Configs: { } secrets } || !string.Equals(opened.EnvelopeId, envelope.EnvelopeId, StringComparison.Ordinal))
            {
                return Fail(envelope, new TrainerConfigurationError(TrainerConfigurationErrorKind.InvalidResponse) { Reason = InvalidPlaintextReason });
            }

            return Map(configuration, secrets);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(ikm);
            if (envelopeKey is not null)
            {
                CryptographicOperations.ZeroMemory(envelopeKey);
            }

            if (plaintext is not null)
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }
    }

    /// <summary>
    /// Rule 17, per kind: the default configuration when it is an Azure OpenAI one, sealed and not unsealed. Endpoint and deployment from its settings,
    /// the key from the plaintext when <c>secretFields</c> has <c>apiKey</c> (missing: <c>invalidResponse</c>), none otherwise (Microsoft Entra ID).
    /// A kind without a usable default is absent.
    /// </summary>
    private Attempt Map(AdminSealedConfiguration configuration, IReadOnlyDictionary<string, OpenedConfig?> secrets)
    {
        List<TrainerModelProfile> profiles = [];
        foreach (ModelKind kind in new[] { ModelKind.Chat, ModelKind.Embedding })
        {
            string? id = configuration.DefaultId(kind);
            AdminModelConfig? config = configuration.Configs.FirstOrDefault(c => c.Id == id && c.Kind == kind);
            if (config is null
                || !string.Equals(config.Provider, AzureOpenAISettingsStore.AzureOpenAIProvider, StringComparison.Ordinal)
                || !configuration.SealedConfigIds.Contains(config.Id, StringComparer.Ordinal)
                || configuration.UnsealedConfigs.Any(u => u.Id == config.Id)
                || config.Endpoint is not { } endpoint || config.Deployment is not { } deployment)
            {
                continue;
            }

            string? apiKey = null;
            if (config.KeyProvided)
            {
                if (!secrets.TryGetValue(config.Id, out OpenedConfig? secret) || string.IsNullOrEmpty(secret?.ApiKey))
                {
                    return Fail(configuration.Envelope, new TrainerConfigurationError(TrainerConfigurationErrorKind.InvalidResponse) { Reason = MissingKeyReason });
                }

                apiKey = secret.ApiKey;
            }

            profiles.Add(new TrainerModelProfile(kind, config.Provider, endpoint, deployment, apiKey)
            {
                ConfigId = config.Id,
                ConfigName = config.Name,
                ConfigUpdatedAt = config.UpdatedAt,
            });
        }

        _logger.LogInformation("Admin envelope {EnvelopeId} opened: {Count} model profile(s).", configuration.Envelope.EnvelopeId, profiles.Count);
        return new Attempt(profiles, null);
    }

    private Attempt Fail(AdminEnvelope envelope, TrainerConfigurationError error)
    {
        _logger.LogWarning("Admin envelope {EnvelopeId} could not be used ({Kind}, {Reason}).", envelope.EnvelopeId, error.Kind, error.Reason);
        return new Attempt(null, error);
    }

    /// <summary>Step 1 of the protocol: the parameters are not authenticated, so they are bounded before anything is derived.</summary>
    private static bool TryValidate(AdminEnvelope envelope, out ParsedEnvelope parsed)
    {
        parsed = null!;
        if (envelope.Version != EnvelopeVersion
            || !string.Equals(envelope.Cipher, Cipher, StringComparison.Ordinal)
            || !string.Equals(envelope.Kdf.Name, KdfName, StringComparison.Ordinal)
            || envelope.Kdf.Iterations is < MinIterations or > MaxIterations
            || !TryDecode(envelope.Kdf.Salt, out byte[] salt) || salt.Length != SaltLength
            || !TryDecode(envelope.Iv, out byte[] iv) || iv.Length != IvLength
            || !TryDecode(envelope.Ciphertext, out byte[] data) || data.Length <= TagLength
            || !TrainerCodeFormat.IsSane(envelope.Code.Length, envelope.Code.Alphabet))
        {
            return false;
        }

        parsed = new ParsedEnvelope(salt, iv, data);
        return true;
    }

    /// <summary>base64url without padding (RFC 4648 §5), strict.</summary>
    private static bool TryDecode(string value, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrEmpty(value) || value.Contains('='))
        {
            return false;
        }

        try
        {
            bytes = Base64Url.DecodeFromChars(value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private sealed record ParsedEnvelope(byte[] Salt, byte[] Iv, byte[] Data);

    private sealed record Attempt(IReadOnlyList<TrainerModelProfile>? Profiles, TrainerConfigurationError? Error);

    private sealed class OpenedEnvelope
    {
        [JsonPropertyName("v")]
        public int? V { get; set; }

        [JsonPropertyName("envelopeId")]
        public string? EnvelopeId { get; set; }

        [JsonPropertyName("configs")]
        public Dictionary<string, OpenedConfig?>? Configs { get; set; }
    }

    private sealed class OpenedConfig
    {
        [JsonPropertyName("apiKey")]
        public string? ApiKey { get; set; }

        public override string ToString() => "OpenedConfig";
    }
}
