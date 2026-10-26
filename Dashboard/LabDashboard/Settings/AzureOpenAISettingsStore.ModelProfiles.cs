using System.Globalization;
using System.Text.Json.Nodes;

namespace LabDashboard.Settings;

// The LLM and the embedding model, each with a custom profile, a trainer profile and a mode, in the same user-secrets file:
// - the profiles and the modes live under LabBench:Models:{Chat|Embedding}:… (ignored by the labs, which only bind AzureOpenAI);
// - the selected profile of each kind is written to the active AzureOpenAI:* entries, the only ones the labs read;
// - in custom mode, the active entries are the custom profile: a value set with `dotnet user-secrets set` is the developer's own value,
//   and a configuration saved before the profiles existed is the custom profile, in custom mode, without any write (backward compatibility).
//   The Custom:* entries are the copy written at every change of the kind, and restored when the kind comes back from trainer mode.
public sealed partial class AzureOpenAISettingsStore
{
    public const string EmbeddingEndpointName = "EmbeddingEndpoint";
    public const string EmbeddingDeploymentName = "EmbeddingDeploymentName";
    public const string EmbeddingApiKeyName = "EmbeddingAPIKey";

    /// <summary>The only provider of phase 1: the labs only call Azure OpenAI.</summary>
    public const string AzureOpenAIProvider = "azure-openai";

    public const string ModelsSection = "LabBench:Models";

    private const string ModeEntry = "Mode";
    private const string ProviderEntry = "Provider";
    private const string ProfileEndpointEntry = "Endpoint";
    private const string ProfileDeploymentEntry = "Deployment";
    private const string ProfileApiKeyEntry = "APIKey";
    private const string SameResourceEntry = "SameResourceAsLlm";
    private const string RetrievedAtEntry = "RetrievedAt";
    private const string ConfigIdEntry = "ConfigId";
    private const string ConfigNameEntry = "ConfigName";
    private const string ConfigUpdatedAtEntry = "ConfigUpdatedAt";

    private static readonly ModelKind[] Kinds = [ModelKind.Chat, ModelKind.Embedding];
    private static readonly ModelMode[] Modes = [ModelMode.Custom, ModelMode.Trainer];

    /// <summary><c>chat</c> or <c>embedding</c> (route value), case-insensitive.</summary>
    public static bool TryParseKind(string? value, out ModelKind kind)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "chat":
                kind = ModelKind.Chat;
                return true;
            case "embedding":
                kind = ModelKind.Embedding;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    /// <summary>Both kinds: mode, profiles (keys reduced to "stored" and a 4-character hint), and the values the labs use with their source.</summary>
    public ModelSettingsStatus GetModelStatus()
    {
        string? storeError = null;
        JsonObject secrets;
        try
        {
            secrets = ReadSecrets();
        }
        catch (AzureOpenAISettingsStoreException ex)
        {
            storeError = ex.Code;
            secrets = [];
        }

        IReadOnlyDictionary<string, string> environment = _environment();
        ActiveModelStatus chat = ResolveActiveChat(secrets, environment);
        ActiveModelStatus embedding = ResolveActiveEmbedding(secrets, environment, chat);

        return new ModelSettingsStatus(
            SecretsFile,
            UserSecretsId,
            storeError,
            [AzureOpenAIProvider],
            KindStatus(secrets, ModelKind.Chat, chat),
            KindStatus(secrets, ModelKind.Embedding, embedding));
    }

    /// <summary>
    /// Validates and saves the custom profile of a kind; when the kind is in custom mode, the labs use it at once.
    /// Returns the validation errors (field → code: <c>provider</c>, <c>endpoint</c>, <c>deployment</c>, <c>apiKey</c>), empty when saved.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> SaveCustomProfileAsync(ModelKind kind, ModelProfileUpdate update)
    {
        string? provider = Normalize(update.Provider);
        bool sameResource = kind == ModelKind.Embedding && (update.SameResourceAsLlm ?? true);
        string? endpoint = sameResource ? null : Normalize(update.Endpoint);
        string? deployment = Normalize(update.Deployment);
        string? apiKey = sameResource ? null : Normalize(update.ApiKey);

        Dictionary<string, string> errors = ValidateProfile(provider, endpoint, deployment, apiKey);
        if (kind == ModelKind.Embedding && !sameResource && endpoint is null)
        {
            // "Its own resource" without an endpoint would silently fall back to the LLM resource.
            errors.TryAdd("endpoint", "required");
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        await UpdateAsync(secrets =>
        {
            KindState state = SaveCurrentCustomProfile(secrets, kind);
            ModelProfile custom = new(AzureOpenAIProvider, endpoint, deployment, sameResource ? null : apiKey ?? state.Custom.ApiKey, sameResource);
            WriteProfile(secrets, kind, ModelMode.Custom, custom);
            if (state.Mode == ModelMode.Custom)
            {
                Apply(secrets, kind, custom);
            }
        });

        return errors;
    }

    /// <summary>Removes the key of the custom profile of a kind (Microsoft Entra ID); applied at once in custom mode.</summary>
    public Task RemoveCustomApiKeyAsync(ModelKind kind) => UpdateAsync(secrets =>
    {
        KindState state = SaveCurrentCustomProfile(secrets, kind);
        ModelProfile custom = state.Custom with { ApiKey = null };
        WriteProfile(secrets, kind, ModelMode.Custom, custom);
        if (state.Mode == ModelMode.Custom)
        {
            Apply(secrets, kind, custom);
        }
    });

    /// <summary>
    /// Selects the profile the labs use for a kind and writes it to the active entries. No profile is ever deleted.
    /// A profile that is incomplete or of another provider is refused, and then nothing is written.
    /// </summary>
    public async Task<ModelModeOutcome> SwitchModeAsync(ModelKind kind, string? mode)
    {
        if (ParseMode(mode) is not { } target)
        {
            return new ModelModeOutcome("invalid", []);
        }

        ModelModeOutcome outcome = ModelModeOutcome.Switched;
        await TryUpdateAsync(secrets =>
        {
            KindState state = ReadKind(secrets, kind);
            ModelProfile profile = target == ModelMode.Custom ? state.Custom : state.Trainer;
            if (!IsSupportedProvider(profile.Provider))
            {
                outcome = new ModelModeOutcome("unsupportedProvider", []);
                return false;
            }

            if (MissingFields(profile) is { Count: > 0 } missing)
            {
                outcome = new ModelModeOutcome("profileIncomplete", missing);
                return false;
            }

            // Before the trainer values replace the active entries, the developer's own values are kept as the custom profile.
            SaveCurrentCustomProfile(secrets, kind);
            Set(secrets, KindSection(kind), ModeEntry, ModeValue(target));
            Apply(secrets, kind, profile);
            return true;
        });

        return outcome;
    }

    /// <summary>
    /// Saves trainer profiles in one write, once retrieved and confirmed (no route lets the browser call it directly).
    /// Every profile is validated first (errors keyed <c>chat.endpoint</c>, <c>embedding.deployment</c>…): nothing is written when one is invalid.
    /// Custom profiles are never changed; the active entries of a kind change only when it is in trainer mode or <see cref="TrainerProfileUpdate.Use"/> is set.
    /// </summary>
    internal async Task<IReadOnlyDictionary<string, string>> SaveTrainerProfilesAsync(IReadOnlyCollection<TrainerProfileUpdate> profiles)
    {
        Dictionary<string, string> errors = [];
        List<(TrainerProfileUpdate Update, ModelProfile Profile)> valid = [];
        foreach (IGrouping<ModelKind, TrainerProfileUpdate> group in profiles.GroupBy(p => p.Kind))
        {
            string prefix = KindValue(group.Key);
            if (group.Count() > 1)
            {
                errors[prefix] = "duplicate";
                continue;
            }

            TrainerProfileUpdate update = group.Single();
            string? provider = Normalize(update.Provider);
            string? endpoint = Normalize(update.Endpoint);
            string? deployment = Normalize(update.Deployment);
            string? apiKey = Normalize(update.ApiKey);

            Dictionary<string, string> profileErrors = ValidateProfile(provider, endpoint, deployment, apiKey);
            if (endpoint is null)
            {
                profileErrors.TryAdd("endpoint", "required");
            }

            if (deployment is null)
            {
                profileErrors.TryAdd("deployment", "required");
            }

            foreach ((string field, string code) in profileErrors)
            {
                errors[$"{prefix}.{field}"] = code;
            }

            valid.Add((update, new ModelProfile(AzureOpenAIProvider, endpoint, deployment, apiKey, SameResourceAsLlm: false, update.RetrievedAt,
                Normalize(update.ConfigId), Normalize(update.ConfigName), Normalize(update.ConfigUpdatedAt))));
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        await UpdateAsync(secrets =>
        {
            foreach ((TrainerProfileUpdate update, ModelProfile profile) in valid)
            {
                KindState state = SaveCurrentCustomProfile(secrets, update.Kind);
                WriteProfile(secrets, update.Kind, ModelMode.Trainer, profile);
                if (update.Use)
                {
                    Set(secrets, KindSection(update.Kind), ModeEntry, ModeValue(ModelMode.Trainer));
                }

                if (update.Use || state.Mode == ModelMode.Trainer)
                {
                    Apply(secrets, update.Kind, profile);
                }
            }
        });

        return errors;
    }

    /// <summary>Mode and profiles of a kind, read from the file. In custom mode the custom profile is read from the active entries.</summary>
    private static KindState ReadKind(JsonObject secrets, ModelKind kind)
    {
        ModelMode mode = ParseMode(Get(secrets, KindSection(kind), ModeEntry)) ?? ModelMode.Custom;
        ModelProfile custom = mode == ModelMode.Custom ? ActiveProfile(secrets, kind) : ReadProfile(secrets, kind, ModelMode.Custom);
        return new KindState(mode, custom, ReadProfile(secrets, kind, ModelMode.Trainer));
    }

    /// <summary>
    /// Writes the mode and the custom profile of a kind as entries before a change, so that its custom values survive
    /// whatever the change writes to the active entries. The first change of a configuration saved before the profiles existed goes through here.
    /// </summary>
    private static KindState SaveCurrentCustomProfile(JsonObject secrets, ModelKind kind)
    {
        KindState state = ReadKind(secrets, kind);
        Set(secrets, KindSection(kind), ModeEntry, ModeValue(state.Mode));
        WriteProfile(secrets, kind, ModelMode.Custom, state.Custom);
        return state;
    }

    /// <summary>The profile held by the active <c>AzureOpenAI:*</c> entries; values are kept exactly as stored.</summary>
    private static ModelProfile ActiveProfile(JsonObject secrets, ModelKind kind)
    {
        if (kind == ModelKind.Chat)
        {
            return new ModelProfile(
                AzureOpenAIProvider,
                NullIfBlank(Get(secrets, EndpointName)),
                NullIfBlank(Get(secrets, DeploymentName)),
                NullIfBlank(Get(secrets, ApiKeyName)),
                SameResourceAsLlm: false);
        }

        // No embedding endpoint: the resource of the LLM. An embedding key without an endpoint is not used by Lab07, so it is not part of the profile.
        string? endpoint = NullIfBlank(Get(secrets, EmbeddingEndpointName));
        return new ModelProfile(
            AzureOpenAIProvider,
            endpoint,
            NullIfBlank(Get(secrets, EmbeddingDeploymentName)),
            endpoint is null ? null : NullIfBlank(Get(secrets, EmbeddingApiKeyName)),
            SameResourceAsLlm: endpoint is null);
    }

    private static ModelProfile ReadProfile(JsonObject secrets, ModelKind kind, ModelMode mode)
    {
        string section = ProfileSection(kind, mode);
        string? endpoint = NullIfBlank(Get(secrets, section, ProfileEndpointEntry));

        // Only the custom embedding profile can use the resource of the LLM; a trainer embedding profile always names its resource.
        bool sameResource = kind == ModelKind.Embedding && mode == ModelMode.Custom
            && (bool.TryParse(Get(secrets, section, SameResourceEntry), out bool same) ? same : endpoint is null);

        DateTimeOffset? retrievedAt = mode == ModelMode.Trainer
            && DateTimeOffset.TryParse(Get(secrets, section, RetrievedAtEntry), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset date)
            ? date
            : null;

        bool trainer = mode == ModelMode.Trainer;
        return new ModelProfile(
            NullIfBlank(Get(secrets, section, ProviderEntry)),
            sameResource ? null : endpoint,
            NullIfBlank(Get(secrets, section, ProfileDeploymentEntry)),
            sameResource ? null : NullIfBlank(Get(secrets, section, ProfileApiKeyEntry)),
            sameResource,
            retrievedAt,
            trainer ? NullIfBlank(Get(secrets, section, ConfigIdEntry)) : null,
            trainer ? NullIfBlank(Get(secrets, section, ConfigNameEntry)) : null,
            trainer ? NullIfBlank(Get(secrets, section, ConfigUpdatedAtEntry)) : null);
    }

    private static void WriteProfile(JsonObject secrets, ModelKind kind, ModelMode mode, ModelProfile profile)
    {
        string section = ProfileSection(kind, mode);
        Set(secrets, section, ProviderEntry, profile.Provider ?? AzureOpenAIProvider);
        Set(secrets, section, ProfileEndpointEntry, profile.SameResourceAsLlm ? null : profile.Endpoint);
        Set(secrets, section, ProfileDeploymentEntry, profile.Deployment);
        Set(secrets, section, ProfileApiKeyEntry, profile.SameResourceAsLlm ? null : profile.ApiKey);

        if (kind == ModelKind.Embedding && mode == ModelMode.Custom)
        {
            Set(secrets, section, SameResourceEntry, profile.SameResourceAsLlm ? "true" : "false");
        }

        if (mode == ModelMode.Trainer)
        {
            Set(secrets, section, RetrievedAtEntry, profile.RetrievedAt?.ToString("O", CultureInfo.InvariantCulture));
            Set(secrets, section, ConfigIdEntry, profile.ConfigId);
            Set(secrets, section, ConfigNameEntry, profile.ConfigName);
            Set(secrets, section, ConfigUpdatedAtEntry, profile.ConfigUpdatedAt);
        }
    }

    /// <summary>
    /// Writes a profile to the active entries the labs read; an absent value removes the entry.
    /// LLM: Endpoint, ChatDeploymentName, APIKey (absent → Microsoft Entra ID). Embedding on the resource of the LLM: only EmbeddingDeploymentName,
    /// the embedding endpoint and key are removed. Embedding on its own resource: the three embedding entries (no key → Microsoft Entra ID).
    /// </summary>
    private static void Apply(JsonObject secrets, ModelKind kind, ModelProfile profile)
    {
        foreach ((string name, string? value) in ActiveEntries(kind, profile))
        {
            Set(secrets, name, value);
        }
    }

    private static (string Name, string? Value)[] ActiveEntries(ModelKind kind, ModelProfile profile) =>
        kind == ModelKind.Chat
            ? [(EndpointName, profile.Endpoint), (DeploymentName, profile.Deployment), (ApiKeyName, profile.ApiKey)]
            : [
                (EmbeddingEndpointName, profile.SameResourceAsLlm ? null : profile.Endpoint),
                (EmbeddingDeploymentName, profile.Deployment),
                (EmbeddingApiKeyName, profile.SameResourceAsLlm ? null : profile.ApiKey),
            ];

    /// <summary>What a switch to this profile still needs. The key is optional (Microsoft Entra ID).</summary>
    private static IReadOnlyList<string> MissingFields(ModelProfile profile)
    {
        List<string> missing = [];
        if (!profile.SameResourceAsLlm && profile.Endpoint is null)
        {
            missing.Add("endpoint");
        }

        if (profile.Deployment is null)
        {
            missing.Add("deployment");
        }

        return missing;
    }

    /// <summary>Same validations as the settings form, plus the provider. Absent values are not errors here.</summary>
    private static Dictionary<string, string> ValidateProfile(string? provider, string? endpoint, string? deployment, string? apiKey)
    {
        Dictionary<string, string> errors = [];
        if (!IsSupportedProvider(provider))
        {
            errors["provider"] = "unsupportedProvider";
        }

        if (endpoint is not null && ValidateEndpoint(endpoint) is { } endpointError)
        {
            errors["endpoint"] = endpointError;
        }

        if (deployment is not null && ValidateDeployment(deployment) is { } deploymentError)
        {
            errors["deployment"] = deploymentError;
        }

        if (apiKey is not null && ValidateApiKey(apiKey) is { } apiKeyError)
        {
            errors["apiKey"] = apiKeyError;
        }

        return errors;
    }

    /// <summary>A missing provider means Azure OpenAI.</summary>
    private static bool IsSupportedProvider(string? provider) =>
        provider is null || provider.Equals(AzureOpenAIProvider, StringComparison.OrdinalIgnoreCase);

    private static ModelKindStatus KindStatus(JsonObject secrets, ModelKind kind, ActiveModelStatus active)
    {
        KindState state = ReadKind(secrets, kind);
        ModelProfile selected = state.Mode == ModelMode.Custom ? state.Custom : state.Trainer;
        bool applied = ActiveEntries(kind, selected).All(entry => string.Equals(NullIfBlank(Get(secrets, entry.Name)), entry.Value, StringComparison.Ordinal));

        return new ModelKindStatus(
            kind,
            state.Mode,
            applied,
            ProfileStatus(kind, ModelMode.Custom, state.Custom),
            ProfileStatus(kind, ModelMode.Trainer, state.Trainer),
            active);
    }

    private static ModelProfileStatus ProfileStatus(ModelKind kind, ModelMode mode, ModelProfile profile)
    {
        IReadOnlyList<string> missing = MissingFields(profile);
        return new ModelProfileStatus(
            profile.Provider ?? (mode == ModelMode.Custom ? AzureOpenAIProvider : null),
            profile.Endpoint,
            profile.Deployment,
            new StoredKeyStatus(profile.ApiKey is not null, KeyHint(profile.ApiKey)),
            kind == ModelKind.Embedding && mode == ModelMode.Custom ? profile.SameResourceAsLlm : null,
            profile.RetrievedAt,
            Complete: missing.Count == 0 && IsSupportedProvider(profile.Provider),
            missing,
            profile.ConfigId,
            profile.ConfigName,
            profile.ConfigUpdatedAt);
    }

    private ActiveModelStatus ResolveActiveChat(JsonObject secrets, IReadOnlyDictionary<string, string> environment)
    {
        SettingStatus endpoint = Resolve(EndpointName, secrets, environment);
        SettingStatus deployment = Resolve(DeploymentName, secrets, environment);
        ApiKeyStatus apiKey = ResolveApiKey(ApiKeyName, secrets, environment);

        return new ActiveModelStatus(
            endpoint,
            deployment,
            apiKey,
            SameResourceAsLlm: null,
            UsesApiKey(apiKey) ? "apiKey" : "entraId",
            Configured: !string.IsNullOrWhiteSpace(endpoint.EffectiveValue) && !string.IsNullOrWhiteSpace(deployment.EffectiveValue));
    }

    /// <summary>Same resolution as Lab07: an embedding endpoint in effect means its own resource (its own key or Microsoft Entra ID), otherwise the LLM's.</summary>
    private ActiveModelStatus ResolveActiveEmbedding(JsonObject secrets, IReadOnlyDictionary<string, string> environment, ActiveModelStatus chat)
    {
        SettingStatus endpoint = Resolve(EmbeddingEndpointName, secrets, environment);
        SettingStatus deployment = Resolve(EmbeddingDeploymentName, secrets, environment);
        ApiKeyStatus apiKey = ResolveApiKey(EmbeddingApiKeyName, secrets, environment);
        bool sameResource = string.IsNullOrWhiteSpace(endpoint.EffectiveValue);

        return new ActiveModelStatus(
            endpoint,
            deployment,
            apiKey,
            sameResource,
            sameResource ? chat.Authentication : UsesApiKey(apiKey) ? "apiKey" : "entraId",
            Configured: !string.IsNullOrWhiteSpace(deployment.EffectiveValue) && (!sameResource || chat.Configured));
    }

    private static string KindSection(ModelKind kind) => $"{ModelsSection}:{(kind == ModelKind.Chat ? "Chat" : "Embedding")}";

    private static string ProfileSection(ModelKind kind, ModelMode mode) => $"{KindSection(kind)}:{(mode == ModelMode.Custom ? "Custom" : "Trainer")}";

    private static string KindValue(ModelKind kind) => kind == ModelKind.Chat ? "chat" : "embedding";

    private static string ModeValue(ModelMode mode) => mode == ModelMode.Custom ? "custom" : "trainer";

    private static ModelMode? ParseMode(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "custom" => ModelMode.Custom,
        "trainer" => ModelMode.Trainer,
        _ => null,
    };

    /// <summary>Unlike <see cref="Normalize"/>, keeps a stored value exactly as it is (never trimmed), so that re-applying it changes nothing.</summary>
    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private sealed record KindState(ModelMode Mode, ModelProfile Custom, ModelProfile Trainer);
}
