using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LabDashboard.Settings;

namespace LabDashboard.TrainerModels;

// What the Admin Dashboard answers on GET /api/v1/model-configs (contract v1, "Admin API contract v1" in
// specs/dashboard-model-configuration/spec.md), as the dashboard reads it, and the overview built from it for the page (Business Rule 17).
// Nothing here is a secret: the keys only travel inside the envelope, which only the decryptor (T11) opens.

/// <summary>Routes and values of the Admin API contract v1 for the AI model configurations.</summary>
internal static partial class AdminModelConfigsContract
{
    /// <summary>Overview: <c>sealed</c> null, <c>sealedUnavailableReason</c> <c>notRequested</c>. Relative to the admin base URL.</summary>
    public const string OverviewPath = "api/v1/model-configs";

    /// <summary>Same answer plus the envelope, on Unlock only (Rule 19).</summary>
    public const string SealedPath = "api/v1/model-configs?sealed=1";

    public const string UnlockPath = "api/v1/model-configs/unlock";

    public const string LlmKind = "llm";
    public const string EmbeddingKind = "embedding";

    /// <summary>The providers of contract v1. A configuration with any other provider is ignored (forward compatibility).</summary>
    public static IReadOnlySet<string> KnownProviders { get; } =
        new HashSet<string>([AzureOpenAISettingsStore.AzureOpenAIProvider, "openai", "deepseek", "anthropic"], StringComparer.Ordinal);

    /// <summary>The <c>secretFields</c> entry that means "the envelope holds an API key for this configuration".</summary>
    public const string ApiKeySecretField = "apiKey";

    /// <summary>
    /// Keeps a code of the admin API (<c>error</c>, <c>details.reason</c>, <c>sealedUnavailableReason</c>…) only when it is an identifier:
    /// whatever the server sends, no free text reaches a result, the page or the log.
    /// </summary>
    public static string? Code(string? value) => value is not null && CodePattern().IsMatch(value) ? value : null;

    /// <summary>A canonical UUID (<c>8-4-4-4-12</c> hexadecimal digits), as the contract sends the ids of the envelopes.</summary>
    public static bool IsUuid(string? value) => value is { Length: 36 } && Guid.TryParseExact(value, "D", out _);

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_.-]{0,63}$")]
    private static partial Regex CodePattern();
}

/// <summary>The <c>embedding</c> object of an embedding configuration.</summary>
internal sealed record AdminEmbeddingInfo(string? Model, int? Dimensions, string? Space);

/// <summary>A configuration of <c>unsealedConfigs</c>: its key could not be put in the envelope (<c>secretsUnreadable</c>).</summary>
internal sealed record AdminUnsealedConfig(string Id, string Reason);

/// <summary>
/// One trainer configuration of <c>configs[]</c>, with a known kind and a known provider (the others are ignored when reading).
/// <see cref="Settings"/> keeps the string settings as received (azure-openai: <c>endpoint</c>, <c>deployment</c>, <c>apiVersion</c>,
/// <c>model</c>, <c>modelVersion</c>; other keys are kept but never interpreted), <see cref="Parameters"/> the numeric parameters.
/// </summary>
internal sealed record AdminModelConfig(
    string Id,
    string Name,
    ModelKind Kind,
    string Provider,
    IReadOnlyDictionary<string, string> Settings,
    IReadOnlyDictionary<string, double> Parameters,
    IReadOnlyList<string> SecretFields,
    bool IsDefault,
    AdminEmbeddingInfo? Embedding,
    string? UpdatedAt)
{
    public string? Endpoint => Setting("endpoint");

    public string? Deployment => Setting("deployment");

    public string? Model => Setting("model") ?? Embedding?.Model;

    /// <summary>The envelope holds an API key for this configuration; otherwise its profile has no key (Microsoft Entra ID).</summary>
    public bool KeyProvided => SecretFields.Contains(AdminModelConfigsContract.ApiKeySecretField, StringComparer.Ordinal);

    /// <summary>Embedding configurations: <c>embedding.dimensions</c>, else <c>parameters.dimensions</c>; null when neither is a positive integer.</summary>
    public int? Dimensions =>
        Kind != ModelKind.Embedding ? null
        : Embedding?.Dimensions is > 0 ? Embedding.Dimensions
        : Parameters.TryGetValue("dimensions", out double value) && value is >= 1 and <= int.MaxValue && Math.Floor(value) == value ? (int)value
        : null;

    private string? Setting(string key) => Settings.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Id = {Id}, Name = {Name}, Kind = {Kind}, Provider = {Provider}, Endpoint = {Endpoint}, Deployment = {Deployment}, ");
        builder.Append($"KeyProvided = {KeyProvided}, IsDefault = {IsDefault}, UpdatedAt = {UpdatedAt}");
        return true;
    }
}

/// <summary>The <c>kdf</c> object of the envelope, as received (validated by the decryptor, Rule 18).</summary>
internal sealed record AdminEnvelopeKdf(string Name, int Iterations, string Salt);

/// <summary>The <c>code</c> object of the envelope: the format of the access code the trainer announced.</summary>
internal sealed record AdminEnvelopeCode(int Length, string Alphabet);

/// <summary>
/// The <c>sealed</c> envelope, as received. Binary values stay base64url text, decoded and validated by the decryptor before any derivation
/// (Rule 18). <see cref="ExpiresAt"/> is the exact string received: the decryptor puts it, verbatim, in the associated data of AES-GCM.
/// Not secret on its own (it cannot be opened without the code and the unlock key), yet <see cref="object.ToString"/> leaves out the binary values.
/// </summary>
internal sealed record AdminEnvelope(
    int Version,
    string EnvelopeId,
    string ExpiresAt,
    DateTimeOffset ExpiresAtUtc,
    string Cipher,
    string Iv,
    string Ciphertext,
    AdminEnvelopeKdf Kdf,
    AdminEnvelopeCode Code,
    int AttemptsAllowed)
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Version = {Version}, EnvelopeId = {EnvelopeId}, ExpiresAt = {ExpiresAt}, Cipher = {Cipher}, ");
        builder.Append($"Kdf = {Kdf.Name}/{Kdf.Iterations}, Code = {Code.Length} characters, AttemptsAllowed = {AttemptsAllowed}");
        return true;
    }
}

/// <summary>
/// The answer of <c>GET /api/v1/model-configs</c> (with or without <c>?sealed=1</c>) once read: unknown properties, unknown kinds and
/// unknown providers are ignored. <see cref="UnknownProviderConfigs"/>: id → kind of the configurations ignored for their provider, so that a
/// default that designates one is shown as not applicable rather than missing. <see cref="Sealed"/> is only read on the sealed retrieval.
/// </summary>
internal sealed record AdminModelConfigs(
    IReadOnlyList<AdminModelConfig> Configs,
    string? DefaultLlmId,
    string? DefaultEmbeddingId,
    AdminEnvelope? Sealed,
    IReadOnlyList<string> SealedConfigIds,
    IReadOnlyList<AdminUnsealedConfig> UnsealedConfigs,
    string? SealedUnavailableReason,
    string? ServerTime,
    IReadOnlyDictionary<string, ModelKind> UnknownProviderConfigs)
{
    public string? DefaultId(ModelKind kind) => kind == ModelKind.Chat ? DefaultLlmId : DefaultEmbeddingId;

    public AdminModelConfig? Find(string? id) => id is null ? null : Configs.FirstOrDefault(c => c.Id == id);

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Configs = {Configs.Count}, DefaultLlmId = {DefaultLlmId}, DefaultEmbeddingId = {DefaultEmbeddingId}, ");
        builder.Append($"Sealed = {(Sealed is null ? "none" : Sealed.EnvelopeId)}, SealedUnavailableReason = {SealedUnavailableReason}");
        return true;
    }
}

/// <summary>
/// What the sealed retrieval hands to the decryptor (T11): the envelope (with <c>expiresAt</c> verbatim), the configurations, the defaults,
/// <c>sealedConfigIds</c>, <c>unsealedConfigs</c>, and the <see cref="DevId"/> the envelope is bound to (the identity's <c>userId</c>, lowercase,
/// read when the envelope was fetched). <see cref="Overview"/>: the same answer per kind, with what can still be unlocked.
/// </summary>
internal sealed class AdminSealedConfiguration
{
    public AdminSealedConfiguration(AdminModelConfigs document, string devId)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(devId);
        Document = document;
        Envelope = document.Sealed ?? throw new ArgumentException("The answer has no envelope.", nameof(document));
        DevId = devId;
        Overview = TrainerModelsOverview.From(document, simulated: false);
    }

    public AdminModelConfigs Document { get; }

    public AdminEnvelope Envelope { get; }

    /// <summary>The <c>devId</c> of the protocol (Rule 16): the identity's <c>userId</c>, lowercase as registered.</summary>
    public string DevId { get; }

    public TrainerModelsOverview Overview { get; }

    public IReadOnlyList<AdminModelConfig> Configs => Document.Configs;

    public IReadOnlyList<string> SealedConfigIds => Document.SealedConfigIds;

    public IReadOnlyList<AdminUnsealedConfig> UnsealedConfigs => Document.UnsealedConfigs;

    public string? DefaultId(ModelKind kind) => Document.DefaultId(kind);

    public override string ToString() =>
        $"AdminSealedConfiguration {{ EnvelopeId = {Envelope.EnvelopeId}, ExpiresAt = {Envelope.ExpiresAt}, DevId = {DevId}, SealedConfigs = {SealedConfigIds.Count} }}";
}

/// <summary>Reads the JSON of <c>GET /api/v1/model-configs</c>. Null when the body is not what the contract defines.</summary>
internal static class AdminModelConfigsParser
{
    private static readonly JsonDocumentOptions Options = new() { MaxDepth = 32 };

    public static AdminModelConfigs? Parse(ReadOnlyMemory<byte> utf8Json, bool withEnvelope)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(utf8Json, Options);
            return Read(document.RootElement, withEnvelope);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static AdminModelConfigs? Read(JsonElement root, bool withEnvelope)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("configs", out JsonElement configsElement) || configsElement.ValueKind != JsonValueKind.Array
            || !root.TryGetProperty("defaults", out JsonElement defaults) || defaults.ValueKind != JsonValueKind.Object
            || !TryString(defaults, "llm", out string? defaultLlm) || !TryString(defaults, "embedding", out string? defaultEmbedding)
            || !TryString(root, "sealedUnavailableReason", out string? sealedUnavailableReason)
            || !TryString(root, "serverTime", out string? serverTime)
            || !TryStrings(root, "sealedConfigIds", out List<string> sealedConfigIds)
            || !TryUnsealed(root, out List<AdminUnsealedConfig> unsealed))
        {
            return null;
        }

        List<AdminModelConfig> configs = [];
        Dictionary<string, ModelKind> unknownProviders = new(StringComparer.Ordinal);
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (JsonElement element in configsElement.EnumerateArray())
        {
            if (!TryConfig(element, out string? id, out ModelKind? kind, out bool knownProvider, out AdminModelConfig? config) || !ids.Add(id!))
            {
                return null;
            }

            if (config is not null)
            {
                configs.Add(config);
            }
            else if (kind is { } knownKind && !knownProvider)
            {
                unknownProviders[id!] = knownKind;
            }
        }

        AdminEnvelope? envelope = null;
        if (withEnvelope && root.TryGetProperty("sealed", out JsonElement sealedElement) && sealedElement.ValueKind != JsonValueKind.Null
            && !TryEnvelope(sealedElement, out envelope))
        {
            return null;
        }

        AdminModelConfigs result = new(configs, defaultLlm, defaultEmbedding, envelope, sealedConfigIds, unsealed,
            sealedUnavailableReason, serverTime, unknownProviders);

        // A default designates a configuration of its kind: a dangling or mismatched default is not the contract.
        return Resolves(result, ModelKind.Chat) && Resolves(result, ModelKind.Embedding) ? result : null;
    }

    private static bool Resolves(AdminModelConfigs document, ModelKind kind) =>
        document.DefaultId(kind) is not { } id
        || document.Find(id)?.Kind == kind
        || document.UnknownProviderConfigs.TryGetValue(id, out ModelKind ignored) && ignored == kind;

    /// <summary>
    /// False when the entry is malformed. A well-formed entry of an unknown kind or provider gives a null <paramref name="config"/>:
    /// it is ignored, as the contract asks.
    /// </summary>
    private static bool TryConfig(JsonElement element, out string? id, out ModelKind? kind, out bool knownProvider, out AdminModelConfig? config)
    {
        id = null;
        kind = null;
        knownProvider = false;
        config = null;
        if (element.ValueKind != JsonValueKind.Object
            || !TryString(element, "id", out id) || string.IsNullOrWhiteSpace(id)
            || !TryString(element, "kind", out string? kindValue) || kindValue is null
            || !TryString(element, "provider", out string? provider) || provider is null)
        {
            return false;
        }

        kind = kindValue switch
        {
            AdminModelConfigsContract.LlmKind => ModelKind.Chat,
            AdminModelConfigsContract.EmbeddingKind => ModelKind.Embedding,
            _ => null,
        };
        knownProvider = AdminModelConfigsContract.KnownProviders.Contains(provider);
        if (kind is null || !knownProvider)
        {
            // Ignored: only what identifies it is read, so that a future shape of these entries cannot break the answer.
            return true;
        }

        if (!TryString(element, "name", out string? name) || name is null
            || !TryString(element, "updatedAt", out string? updatedAt)
            || !TryObject(element, "settings", out JsonElement? settingsElement)
            || !TryObject(element, "parameters", out JsonElement? parametersElement)
            || !TryStrings(element, "secretFields", out List<string> secretFields)
            || !TryObject(element, "embedding", out JsonElement? embeddingElement))
        {
            return false;
        }

        Dictionary<string, string> settings = new(StringComparer.Ordinal);
        if (settingsElement is { } settingsObject)
        {
            foreach (JsonProperty property in settingsObject.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    settings[property.Name] = property.Value.GetString()!;
                }
            }
        }

        Dictionary<string, double> parameters = new(StringComparer.Ordinal);
        if (parametersElement is { } parametersObject)
        {
            foreach (JsonProperty property in parametersObject.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetDouble(out double number))
                {
                    parameters[property.Name] = number;
                }
            }
        }

        AdminEmbeddingInfo? embedding = null;
        if (embeddingElement is { } embeddingObject)
        {
            if (!TryString(embeddingObject, "model", out string? model) || !TryString(embeddingObject, "space", out string? space))
            {
                return false;
            }

            int? dimensions = embeddingObject.TryGetProperty("dimensions", out JsonElement dimensionsElement)
                && dimensionsElement.ValueKind == JsonValueKind.Number && dimensionsElement.TryGetInt32(out int value) ? value : null;
            embedding = new AdminEmbeddingInfo(model, dimensions, space);
        }

        bool isDefault = element.TryGetProperty("isDefault", out JsonElement isDefaultElement) && isDefaultElement.ValueKind == JsonValueKind.True;
        config = new AdminModelConfig(id, name, kind.Value, provider, settings, parameters, secretFields, isDefault, embedding, updatedAt);
        return true;
    }

    private static bool TryEnvelope(JsonElement element, out AdminEnvelope? envelope)
    {
        envelope = null;
        if (element.ValueKind != JsonValueKind.Object
            || !TryInt(element, "version", out int version)
            || !TryString(element, "envelopeId", out string? envelopeId) || !AdminModelConfigsContract.IsUuid(envelopeId)
            || !TryString(element, "expiresAt", out string? expiresAt) || string.IsNullOrWhiteSpace(expiresAt)
            || !DateTimeOffset.TryParse(expiresAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset expiresAtUtc)
            || !TryRequiredString(element, "cipher", out string? cipher)
            || !TryRequiredString(element, "iv", out string? iv)
            || !TryRequiredString(element, "ciphertext", out string? ciphertext)
            || !element.TryGetProperty("kdf", out JsonElement kdf) || kdf.ValueKind != JsonValueKind.Object
            || !TryRequiredString(kdf, "name", out string? kdfName) || !TryInt(kdf, "iterations", out int iterations) || !TryRequiredString(kdf, "salt", out string? salt)
            || !element.TryGetProperty("code", out JsonElement code) || code.ValueKind != JsonValueKind.Object
            || !TryInt(code, "length", out int codeLength) || !TryRequiredString(code, "alphabet", out string? alphabet)
            || !TryInt(element, "attemptsAllowed", out int attemptsAllowed))
        {
            return false;
        }

        envelope = new AdminEnvelope(version, envelopeId!, expiresAt, expiresAtUtc, cipher!, iv!, ciphertext!,
            new AdminEnvelopeKdf(kdfName!, iterations, salt!), new AdminEnvelopeCode(codeLength, alphabet!), attemptsAllowed);
        return true;
    }

    private static bool TryUnsealed(JsonElement root, out List<AdminUnsealedConfig> unsealed)
    {
        unsealed = [];
        if (!root.TryGetProperty("unsealedConfigs", out JsonElement element) || element.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (JsonElement entry in element.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object || !TryString(entry, "id", out string? id) || id is null || !TryString(entry, "reason", out string? reason))
            {
                return false;
            }

            unsealed.Add(new AdminUnsealedConfig(id, AdminModelConfigsContract.Code(reason) ?? TrainerModelsOverview.SecretsUnreadableReason));
        }

        return true;
    }

    /// <summary>Absent or null: true with a null value; a string: true; anything else: false (malformed).</summary>
    private static bool TryString(JsonElement element, string name, out string? value)
    {
        value = null;
        if (!element.TryGetProperty(name, out JsonElement property) || property.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString();
        return true;
    }

    private static bool TryRequiredString(JsonElement element, string name, out string? value) =>
        TryString(element, name, out value) && !string.IsNullOrEmpty(value);

    private static bool TryInt(JsonElement element, string name, out int value)
    {
        value = 0;
        return element.TryGetProperty(name, out JsonElement property) && property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out value);
    }

    /// <summary>Absent or null: true with no value; an object: true; anything else: false.</summary>
    private static bool TryObject(JsonElement element, string name, out JsonElement? value)
    {
        value = null;
        if (!element.TryGetProperty(name, out JsonElement property) || property.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        value = property;
        return property.ValueKind == JsonValueKind.Object;
    }

    /// <summary>Absent or null: an empty list; an array: its strings (other values ignored); anything else: false.</summary>
    private static bool TryStrings(JsonElement element, string name, out List<string> values)
    {
        values = [];
        if (!element.TryGetProperty(name, out JsonElement property) || property.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        values.AddRange(property.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString()!));
        return true;
    }
}

/// <summary>Whether the default configuration of a kind can be unlocked and applied (Business Rule 17).</summary>
internal enum TrainerModelState
{
    /// <summary>The trainer designates no configuration of this kind.</summary>
    None,

    /// <summary>An Azure OpenAI configuration with a valid endpoint and deployment: the candidate can tick it.</summary>
    Applicable,

    /// <summary>Another provider (the labs only call Azure OpenAI), or one this dashboard does not know: it cannot be applied.</summary>
    NotApplicable,

    /// <summary>An Azure OpenAI configuration the dashboard cannot use: its key is not in the envelope, or its settings are invalid. "Tell the trainer".</summary>
    Unusable,
}

/// <summary>
/// One trainer configuration as the page shows it, never with a key: <see cref="KeyProvided"/> only says whether the envelope holds one.
/// <see cref="UpdatedAt"/> is the string received, compared as is with the stored <c>ConfigUpdatedAt</c> ("updated by the trainer since your last unlock").
/// </summary>
internal sealed record TrainerModelSummary(
    string Id,
    string Name,
    string Provider,
    string? Endpoint,
    string? EndpointHost,
    string? Deployment,
    string? Model,
    string? UpdatedAt,
    bool KeyProvided,
    int? Dimensions)
{
    public static TrainerModelSummary From(AdminModelConfig config)
    {
        string? endpoint = config.Endpoint;
        string? host = endpoint is not null && Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri) ? uri.Host : null;
        return new TrainerModelSummary(config.Id, config.Name, config.Provider, endpoint, host, config.Deployment, config.Model,
            config.UpdatedAt, config.KeyProvided, config.Dimensions);
    }
}

/// <summary>
/// The trainer's configurations of one kind (Rule 17): the <see cref="Default"/> the dashboard would use, whether it can be used
/// (<see cref="State"/>, <see cref="Reason"/>), its <see cref="Warnings"/>, and the <see cref="Others"/> of that kind, for information only.
/// </summary>
internal sealed record TrainerKindOverview(
    ModelKind Kind,
    TrainerModelState State,
    string? Reason,
    TrainerModelSummary? Default,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<TrainerModelSummary> Others)
{
    /// <summary>The candidate can tick this kind before Unlock.</summary>
    public bool CanUse => State == TrainerModelState.Applicable;

    /// <summary>
    /// The trainer changed this kind's default since the candidate's last unlock (another configuration, or a newer <c>updatedAt</c> than the one
    /// saved with the trainer profile): "updated by the trainer since your last unlock". Set by the unlock flow, false otherwise.
    /// </summary>
    public bool UpdatedSinceUnlock { get; init; }
}

/// <summary>The overview of the trainer configurations for the AI models tab, read without any code (Rule 17). Never holds a key.</summary>
internal sealed record TrainerModelsOverview(TrainerKindOverview Chat, TrainerKindOverview Embedding, string? ServerTime, bool Simulated)
{
    /// <summary>Lab07 builds its vector store with this many dimensions.</summary>
    public const int Lab07EmbeddingDimensions = 1536;

    /// <summary>Warning of an embedding default whose dimensions are not <see cref="Lab07EmbeddingDimensions"/>: "Lab07 expects 1536 dimensions".</summary>
    public const string DimensionsWarning = "dimensions";

    // Values of TrainerKindOverview.Reason.
    /// <summary><see cref="TrainerModelState.NotApplicable"/>: a provider of the contract other than Azure OpenAI.</summary>
    public const string ProviderReason = "provider";

    /// <summary><see cref="TrainerModelState.NotApplicable"/>: a provider this dashboard does not know (the configuration itself is ignored).</summary>
    public const string UnknownProviderReason = "unknownProvider";

    /// <summary><see cref="TrainerModelState.Unusable"/>: listed in <c>unsealedConfigs</c> (or the reason the admin gave there).</summary>
    public const string SecretsUnreadableReason = "secretsUnreadable";

    /// <summary><see cref="TrainerModelState.Unusable"/>: the configuration has a key, but this envelope does not hold it (not in <c>sealedConfigIds</c>).</summary>
    public const string NotSealedReason = "notSealed";

    /// <summary><see cref="TrainerModelState.Unusable"/>: the endpoint or the deployment is missing, or the settings store would refuse it.</summary>
    public const string InvalidSettingsReason = "invalidSettings";

    public TrainerKindOverview For(ModelKind kind) => kind == ModelKind.Chat ? Chat : Embedding;

    public static TrainerModelsOverview From(AdminModelConfigs document, bool simulated)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new TrainerModelsOverview(Kind(document, ModelKind.Chat), Kind(document, ModelKind.Embedding), document.ServerTime, simulated);
    }

    private static TrainerKindOverview Kind(AdminModelConfigs document, ModelKind kind)
    {
        string? defaultId = document.DefaultId(kind);
        IReadOnlyList<TrainerModelSummary> others = document.Configs
            .Where(c => c.Kind == kind && c.Id != defaultId)
            .Select(TrainerModelSummary.From)
            .ToList();

        AdminModelConfig? config = document.Find(defaultId);
        if (config is null || config.Kind != kind)
        {
            // No default, or one whose provider is unknown to this dashboard (ignored when reading).
            return defaultId is not null && document.UnknownProviderConfigs.ContainsKey(defaultId)
                ? new TrainerKindOverview(kind, TrainerModelState.NotApplicable, UnknownProviderReason, null, [], others)
                : new TrainerKindOverview(kind, TrainerModelState.None, null, null, [], others);
        }

        TrainerModelSummary summary = TrainerModelSummary.From(config);
        List<string> warnings = [];
        if (kind == ModelKind.Embedding && summary.Dimensions is { } dimensions && dimensions != Lab07EmbeddingDimensions)
        {
            warnings.Add(DimensionsWarning);
        }

        (TrainerModelState state, string? reason) = Applicability(document, config);
        return new TrainerKindOverview(kind, state, reason, summary, warnings, others);
    }

    private static (TrainerModelState State, string? Reason) Applicability(AdminModelConfigs document, AdminModelConfig config)
    {
        if (!string.Equals(config.Provider, AzureOpenAISettingsStore.AzureOpenAIProvider, StringComparison.Ordinal))
        {
            return (TrainerModelState.NotApplicable, ProviderReason);
        }

        if (document.UnsealedConfigs.FirstOrDefault(u => u.Id == config.Id) is { } unsealed)
        {
            return (TrainerModelState.Unusable, unsealed.Reason);
        }

        // The sealed answer only: a configuration with a key needs it in this envelope, or its profile would silently lose the key.
        if (document.Sealed is not null && config.KeyProvided && !document.SealedConfigIds.Contains(config.Id, StringComparer.Ordinal))
        {
            return (TrainerModelState.Unusable, NotSealedReason);
        }

        bool validSettings = config.Endpoint is { } endpoint && AzureOpenAISettingsStore.ValidateEndpoint(endpoint) is null
            && config.Deployment is { } deployment && AzureOpenAISettingsStore.ValidateDeployment(deployment) is null;
        return validSettings ? (TrainerModelState.Applicable, null) : (TrainerModelState.Unusable, InvalidSettingsReason);
    }
}
