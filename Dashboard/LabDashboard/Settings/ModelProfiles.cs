using System.Text;

namespace LabDashboard.Settings;

/// <summary>The two AI models of the labs. Each one has its own mode and profiles: changing one never changes the other.</summary>
public enum ModelKind
{
    /// <summary>The LLM used by every lab: <c>AzureOpenAI:Endpoint</c>, <c>ChatDeploymentName</c> and <c>APIKey</c>.</summary>
    Chat,

    /// <summary>The embedding model of Lab07: <c>AzureOpenAI:EmbeddingDeploymentName</c>, and its own endpoint and key when it does not use the resource of the LLM.</summary>
    Embedding
}

/// <summary>Which profile of a kind the labs use.</summary>
public enum ModelMode
{
    /// <summary>The developer's own Azure OpenAI deployment, typed in the settings.</summary>
    Custom,

    /// <summary>The configuration retrieved from the trainer.</summary>
    Trainer
}

/// <summary>Whether a profile holds an API key. It never carries the key itself: <see cref="Hint"/> is only its last 4 characters.</summary>
public sealed record StoredKeyStatus(bool Stored, string? Hint = null);

/// <summary>
/// A saved profile as the browser sees it (the key is reduced to <see cref="StoredKeyStatus"/>).
/// <see cref="Missing"/> lists what a switch to this profile still needs (<c>endpoint</c>, <c>deployment</c>).
/// <see cref="SameResourceAsLlm"/> is only set for the custom embedding profile; <see cref="RetrievedAt"/> (the last unlock) and the trainer
/// configuration it came from (<see cref="ConfigId"/>, <see cref="ConfigName"/>, <see cref="ConfigUpdatedAt"/>) only for trainer profiles.
/// </summary>
public sealed record ModelProfileStatus(
    string? Provider,
    string? Endpoint,
    string? Deployment,
    StoredKeyStatus ApiKey,
    bool? SameResourceAsLlm,
    DateTimeOffset? RetrievedAt,
    bool Complete,
    IReadOnlyList<string> Missing,
    string? ConfigId = null,
    string? ConfigName = null,
    string? ConfigUpdatedAt = null);

/// <summary>
/// What the labs use for a kind: the active <c>AzureOpenAI:*</c> entries, each with its effective value and its source.
/// For the embedding, <see cref="SameResourceAsLlm"/> is true when no embedding endpoint is in effect: Lab07 then uses the endpoint and the key of the LLM,
/// and <see cref="Authentication"/> / <see cref="Configured"/> follow the LLM.
/// </summary>
public sealed record ActiveModelStatus(
    SettingStatus Endpoint,
    SettingStatus Deployment,
    ApiKeyStatus ApiKey,
    bool? SameResourceAsLlm,
    string Authentication,
    bool Configured);

/// <summary>
/// One kind: its mode, its two profiles and what the labs use. <see cref="Applied"/> is false when the active entries of the file
/// no longer hold the selected profile (changed outside the dashboard while the trainer profile is selected).
/// </summary>
public sealed record ModelKindStatus(
    ModelKind Kind,
    ModelMode Mode,
    bool Applied,
    ModelProfileStatus Custom,
    ModelProfileStatus Trainer,
    ActiveModelStatus Active);

public sealed record ModelSettingsStatus(
    string SecretsFile,
    string UserSecretsId,
    string? StoreError,
    IReadOnlyList<string> Providers,
    ModelKindStatus Chat,
    ModelKindStatus Embedding);

/// <summary>
/// Values sent by the custom profile form of a kind. A missing provider means <c>azure-openai</c>, the only one accepted.
/// Empty Endpoint / Deployment remove the value; an empty ApiKey keeps the stored key.
/// SameResourceAsLlm (embedding only, true by default): only the deployment is kept, and the endpoint and the key of the LLM in use apply.
/// </summary>
public sealed record ModelProfileUpdate(string? Provider, string? Endpoint, string? Deployment, string? ApiKey, bool? SameResourceAsLlm = null)
{
    private bool PrintMembers(StringBuilder builder)
    {
        // Never print the key (a record prints every property by default).
        builder.Append($"Provider = {Provider}, Endpoint = {Endpoint}, Deployment = {Deployment}, ApiKey = {(ApiKey is null ? "none" : "set")}, SameResourceAsLlm = {SameResourceAsLlm}");
        return true;
    }
}

/// <summary>The mode requested for a kind: <c>custom</c> or <c>trainer</c>.</summary>
public sealed record ModelModeUpdate(string? Mode);

/// <summary>
/// Result of a mode switch. <see cref="Error"/> is null when switched, otherwise <c>invalid</c> (unknown mode),
/// <c>unsupportedProvider</c> or <c>profileIncomplete</c> (<see cref="Missing"/> then lists what the profile needs). Nothing is written on an error.
/// </summary>
public sealed record ModelModeOutcome(string? Error, IReadOnlyList<string> Missing)
{
    public static ModelModeOutcome Switched { get; } = new(null, []);
}

/// <summary>
/// A trainer profile to save, once retrieved and confirmed. A trainer embedding profile always names its own resource (endpoint required).
/// <see cref="Use"/> switches the kind to trainer mode in the same write ("use it"). <see cref="ConfigId"/>, <see cref="ConfigName"/> and
/// <see cref="ConfigUpdatedAt"/>: the trainer configuration it comes from (contract <c>configs[]</c>), kept to show "updated since your last unlock".
/// </summary>
internal sealed record TrainerProfileUpdate(ModelKind Kind, string? Provider, string? Endpoint, string? Deployment, string? ApiKey, DateTimeOffset RetrievedAt, bool Use)
{
    public string? ConfigId { get; init; }

    public string? ConfigName { get; init; }

    public string? ConfigUpdatedAt { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Kind = {Kind}, Provider = {Provider}, Endpoint = {Endpoint}, Deployment = {Deployment}, ApiKey = {(ApiKey is null ? "none" : "set")}, RetrievedAt = {RetrievedAt:O}, Use = {Use}");
        builder.Append($", ConfigId = {ConfigId}, ConfigName = {ConfigName}, ConfigUpdatedAt = {ConfigUpdatedAt}");
        return true;
    }
}

/// <summary>A profile as stored, key included: never returned nor logged. The Config* values only exist for trainer profiles.</summary>
internal sealed record ModelProfile(
    string? Provider,
    string? Endpoint,
    string? Deployment,
    string? ApiKey,
    bool SameResourceAsLlm,
    DateTimeOffset? RetrievedAt = null,
    string? ConfigId = null,
    string? ConfigName = null,
    string? ConfigUpdatedAt = null)
{
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Provider = {Provider}, Endpoint = {Endpoint}, Deployment = {Deployment}, ApiKey = {(ApiKey is null ? "none" : "set")}, SameResourceAsLlm = {SameResourceAsLlm}");
        return true;
    }
}
