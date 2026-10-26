using System.Buffers.Text;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LabDashboard.Tests;

/// <summary>
/// The server side of the Admin Dashboard contract v1 for the AI model configurations, on top of <see cref="FakeAdminHandler"/>: it issues envelopes
/// on <c>GET ?sealed=1</c> (the first one is the official test vector, the others are sealed here with the same protocol), and answers
/// <c>POST /unlock</c> by checking the proof and releasing the unlock key once. Scripted answers (<see cref="UnlockOverrides"/>,
/// <see cref="SealedOverrides"/>) are served first, one per call, to simulate every error. Never the network.
/// </summary>
internal sealed class FakeModelConfigServer
{
    // The official test vector (local-dashboard-contract.md, "Test vector"). Not a real secret.
    public const string VectorCode = "K7QX9";
    public const string VectorDevId = "8d2b6c1e-4f3a-4e7b-b2a9-5c0d1e2f3a4b";
    public const string VectorEnvelopeId = "3f1c7a52-6a0e-4b8e-9a51-2f6c1d0e9b77";
    public const string VectorExpiresAt = "2026-10-25T12:10:00.000Z";
    public const string VectorUnlockKey = "oKGio6SlpqeoqaqrrK2ur7CxsrO0tba3uLm6u7y9vr8";
    public const string VectorCodeKey = "dPp1m4BvjcBA_CJXVQ9Js9pwMMTqz-dHcuXHTHW88S4";
    public const string VectorProof = "Pl-wK3wMsxjqkLcphHjD6UuRxNzCtLH9f9Q0Hg8rGlE";
    public const string VectorConfigId = "11111111-2222-4333-8444-555555555555";
    public const string VectorApiKey = "test-vector-api-key-not-a-real-secret";
    public const string VectorPlaintext = """{"v":1,"envelopeId":"3f1c7a52-6a0e-4b8e-9a51-2f6c1d0e9b77","configs":{"11111111-2222-4333-8444-555555555555":{"apiKey":"test-vector-api-key-not-a-real-secret"}}}""";

    public const string VectorEnvelopeJson = """
        {
          "version": 1,
          "envelopeId": "3f1c7a52-6a0e-4b8e-9a51-2f6c1d0e9b77",
          "expiresAt": "2026-10-25T12:10:00.000Z",
          "cipher": "AES-256-GCM",
          "iv": "wMHCw8TFxsfIycrL",
          "ciphertext": "we1LM8JmpY9wk1owbtSIBCsMBhfbKI_tByZGaLBp8V1sGoCjjir08N3CfTFSqkQRIIN1a2O1RW5-zUMlZr2wE0yNNrjyfH11riL1JgtbqRdDwhCwB1nU3DKtuZRy7XVccnnjtPXfy4ATtdBLd4mcoc4WiHXKKcAIv1X4ByZElAT0EFXo1dO_IMwAnDrU1Wbt1HsINczw8YXwvjhMo_8y2NejGEHyktTyqk3C3rGeaJKv",
          "kdf": { "name": "PBKDF2-HMAC-SHA256", "iterations": 600000, "salt": "AAECAwQFBgcICQoLDA0ODw" },
          "code": { "length": 5, "alphabet": "ABCDEFGHJKMNPQRSTUVWXYZ23456789" },
          "attemptsAllowed": 3
        }
        """;

    public const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    public const string ExpiresAt = "2026-10-25T15:10:00.000Z";

    private readonly object _gate = new();
    private int _issued;

    public FakeModelConfigServer(FakeAdminHandler handler)
    {
        Handler = handler;
        handler.Respond = Handle;
    }

    public FakeAdminHandler Handler { get; }

    /// <summary>The code the trainer announced (used to seal the envelopes issued here; the vector is always <see cref="VectorCode"/>).</summary>
    public string Code { get; set; } = VectorCode;

    /// <summary>The developer the envelopes are bound to.</summary>
    public string DevId { get; set; } = VectorDevId;

    /// <summary>The first envelope is the official vector (its configuration is <see cref="VectorConfigId"/>); false: every envelope is sealed here.</summary>
    public bool VectorFirst { get; set; } = true;

    /// <summary>True: the proof is not checked (to see what the client does with a key released for a wrong code, another developer…).</summary>
    public bool AcceptAnyProof { get; set; }

    /// <summary>Replaces the unlock key the server releases (base64url).</summary>
    public string? UnlockKeyOverride { get; set; }

    /// <summary>Changes the envelope JSON of the answer (after it was built).</summary>
    public Action<JsonObject>? MutateEnvelope { get; set; }

    /// <summary>The <c>configs[]</c> of the answer, <c>defaults</c>, <c>sealedConfigIds</c> and <c>unsealedConfigs</c>.</summary>
    public List<JsonObject> Configs { get; set; } = [Config(VectorConfigId, "llm", "Vector GPT", "https://vector.openai.azure.com/", "gpt-vector")];

    public string? DefaultLlm { get; set; } = VectorConfigId;

    public string? DefaultEmbedding { get; set; }

    public List<string> SealedIds { get; set; } = [VectorConfigId];

    public List<string> UnsealedIds { get; set; } = [];

    /// <summary>The plaintext of an envelope sealed here, by envelope id (default: a key for every sealed configuration).</summary>
    public Func<string, string>? Plaintext { get; set; }

    /// <summary>Served before the normal answer, one per call; a null entry means "answer normally".</summary>
    public Queue<Func<HttpResponseMessage>?> UnlockOverrides { get; } = new();

    public Queue<Func<HttpResponseMessage>?> SealedOverrides { get; } = new();

    public List<Issued> IssuedEnvelopes { get; } = [];

    public IEnumerable<CapturedRequest> UnlockRequests => Handler.Requests.Where(r => r.Method == HttpMethod.Post && r.Uri.AbsolutePath.EndsWith("/unlock", StringComparison.Ordinal));

    public IEnumerable<CapturedRequest> SealedRequests => Handler.Requests.Where(r => r.Method == HttpMethod.Get && r.Uri.Query.Contains("sealed=1", StringComparison.Ordinal));

    public static JsonObject Config(string id, string kind, string name, string endpoint, string deployment, string provider = "azure-openai", bool apiKey = true, string updatedAt = "2026-10-25T09:12:03.120Z") => new()
    {
        ["id"] = id,
        ["name"] = name,
        ["kind"] = kind,
        ["provider"] = provider,
        ["settings"] = new JsonObject { ["endpoint"] = endpoint, ["deployment"] = deployment, ["apiVersion"] = "2024-10-21", ["model"] = deployment },
        ["parameters"] = new JsonObject(),
        ["secretFields"] = apiKey ? new JsonArray("apiKey") : new JsonArray(),
        ["isDefault"] = true,
        ["updatedAt"] = updatedAt,
    };

    public static string ApiKeyOf(string configId) => configId == VectorConfigId ? VectorApiKey : $"api-key-{configId[..8]}-not-a-real-secret";

    public static string ToBase64Url(byte[] bytes) => Base64Url.EncodeToString(bytes);

    public static byte[] FromBase64Url(string text) => Base64Url.DecodeFromChars(text);

    public static byte[] DeriveCodeKey(string code, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(code), salt, iterations, HashAlgorithmName.SHA256, 32);

    public static string ProofOf(byte[] codeKey, string envelopeId, string devId) =>
        ToBase64Url(HMACSHA256.HashData(codeKey, Encoding.UTF8.GetBytes($"labs-admin/model-unlock/v1|{envelopeId}|{devId}")));

    /// <summary>Seals a plaintext as the contract does (the same protocol the dashboard opens, written independently of it).</summary>
    public static (JsonObject Envelope, string Proof, string UnlockKey) Seal(
        string code, string devId, string envelopeId, string expiresAt, string plaintext, byte[]? unlockKey = null, byte[]? salt = null, int iterations = 600_000)
    {
        salt ??= Enumerable.Range(0, 16).Select(i => (byte)(i + 16)).ToArray();
        unlockKey ??= Enumerable.Range(0, 32).Select(i => (byte)(200 - i)).ToArray();
        byte[] iv = Enumerable.Range(0, 12).Select(i => (byte)(i * 5)).ToArray();
        byte[] codeKey = DeriveCodeKey(code, salt, iterations);
        string context = $"labs-admin/model-envelope/v1|{envelopeId}|{devId}";
        byte[] envelopeKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, [.. codeKey, .. unlockKey], 32, salt: [], info: Encoding.UTF8.GetBytes(context));
        byte[] clear = Encoding.UTF8.GetBytes(plaintext);
        byte[] cipher = new byte[clear.Length];
        byte[] tag = new byte[16];
        using (AesGcm aes = new(envelopeKey, 16))
        {
            aes.Encrypt(iv, clear, cipher, tag, Encoding.UTF8.GetBytes($"{context}|{expiresAt}"));
        }

        JsonObject envelope = new()
        {
            ["version"] = 1,
            ["envelopeId"] = envelopeId,
            ["expiresAt"] = expiresAt,
            ["cipher"] = "AES-256-GCM",
            ["iv"] = ToBase64Url(iv),
            ["ciphertext"] = ToBase64Url([.. cipher, .. tag]),
            ["kdf"] = new JsonObject { ["name"] = "PBKDF2-HMAC-SHA256", ["iterations"] = iterations, ["salt"] = ToBase64Url(salt) },
            ["code"] = new JsonObject { ["length"] = 5, ["alphabet"] = Alphabet },
            ["attemptsAllowed"] = 3,
        };
        return (envelope, ProofOf(codeKey, envelopeId, devId), ToBase64Url(unlockKey));
    }

    private Task<HttpResponseMessage> Handle(CapturedRequest request, CancellationToken cancellationToken)
    {
        Func<HttpResponseMessage>? scripted = null;
        bool isUnlock = request.Method == HttpMethod.Post && request.Uri.AbsolutePath.EndsWith("/unlock", StringComparison.Ordinal);
        bool isSealed = request.Method == HttpMethod.Get && request.Uri.Query.Contains("sealed=1", StringComparison.Ordinal);
        lock (_gate)
        {
            Queue<Func<HttpResponseMessage>?>? queue = isUnlock ? UnlockOverrides : isSealed ? SealedOverrides : null;
            if (queue is { Count: > 0 })
            {
                scripted = queue.Dequeue();
            }
        }

        if (scripted is not null)
        {
            return Task.FromResult(scripted());
        }

        if (isUnlock)
        {
            return Task.FromResult(Unlock(request));
        }

        if (request.Method == HttpMethod.Get && request.Uri.AbsolutePath.EndsWith("/model-configs", StringComparison.Ordinal))
        {
            return Task.FromResult(Document(withEnvelope: isSealed));
        }

        return Task.FromResult(FakeAdminHandler.Reply(404, "{}"));
    }

    private HttpResponseMessage Document(bool withEnvelope)
    {
        JsonObject? envelope = null;
        if (withEnvelope)
        {
            lock (_gate)
            {
                int n = ++_issued;
                if (VectorFirst && n == 1)
                {
                    envelope = JsonNode.Parse(VectorEnvelopeJson)!.AsObject();
                    IssuedEnvelopes.Add(new Issued(VectorEnvelopeId, VectorCode, VectorProof, VectorUnlockKey));
                }
                else
                {
                    string id = $"00000000-0000-4000-8000-{n:D12}";
                    string plaintext = (Plaintext ?? DefaultPlaintext)(id);
                    (JsonObject sealedEnvelope, string proof, string unlockKey) = Seal(Code, DevId, id, ExpiresAt, plaintext);
                    envelope = sealedEnvelope;
                    IssuedEnvelopes.Add(new Issued(id, Code, proof, unlockKey));
                }

                MutateEnvelope?.Invoke(envelope);
            }
        }

        JsonObject document = new()
        {
            ["configs"] = new JsonArray(Configs.Select(c => (JsonNode)c.DeepClone()).ToArray()),
            ["defaults"] = new JsonObject { ["llm"] = DefaultLlm, ["embedding"] = DefaultEmbedding },
            ["sealed"] = envelope,
            ["sealedConfigIds"] = new JsonArray(SealedIds.Select(i => (JsonNode)JsonValue.Create(i)!).ToArray()),
            ["unsealedConfigs"] = new JsonArray(UnsealedIds.Select(i => (JsonNode)new JsonObject { ["id"] = i, ["reason"] = "secretsUnreadable" }).ToArray()),
            ["sealedUnavailableReason"] = withEnvelope ? null : "notRequested",
            ["serverTime"] = "2026-10-25T12:00:00.000Z",
        };
        return FakeAdminHandler.Reply(200, document.ToJsonString());
    }

    private string DefaultPlaintext(string envelopeId) =>
        JsonSerializer.Serialize(new
        {
            v = 1,
            envelopeId,
            configs = SealedIds.ToDictionary(id => id, id => new { apiKey = ApiKeyOf(id) }),
        });

    private HttpResponseMessage Unlock(CapturedRequest request)
    {
        using JsonDocument body = JsonDocument.Parse(request.Body!);
        string envelopeId = body.RootElement.GetProperty("envelopeId").GetString()!;
        string proof = body.RootElement.GetProperty("proof").GetString()!;
        Issued? issued;
        lock (_gate)
        {
            issued = IssuedEnvelopes.FirstOrDefault(e => e.EnvelopeId == envelopeId);
        }

        if (issued is null || issued.Used)
        {
            return FakeAdminHandler.Reply(410, $$$"""{"error":"envelopeExpired","message":"x","details":{"reason":"{{{(issued is null ? "unknown" : "used")}}}"}}""");
        }

        if (!AcceptAnyProof && proof != issued.Proof)
        {
            issued.AttemptsLeft--;
            return FakeAdminHandler.Reply(401, $$$"""{"error":"invalidAccessCode","message":"x","details":{"attemptsLeft":{{{issued.AttemptsLeft}}}}}""");
        }

        issued.Used = true;
        return FakeAdminHandler.Reply(200, $$"""{"envelopeId":"{{envelopeId}}","unlockKey":"{{UnlockKeyOverride ?? issued.UnlockKey}}"}""");
    }

    public sealed class Issued(string envelopeId, string code, string proof, string unlockKey)
    {
        public string EnvelopeId { get; } = envelopeId;

        public string Code { get; } = code;

        public string Proof { get; } = proof;

        public string UnlockKey { get; } = unlockKey;

        public int AttemptsLeft { get; set; } = 3;

        public bool Used { get; set; }
    }
}
