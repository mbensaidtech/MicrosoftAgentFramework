using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json.Serialization;
using LabDashboard.Settings;

namespace LabDashboard.TrainerModels;

// The trainer configuration flow (Business Rules 13–20), in the steps the candidate goes through:
// - "overview" lists the trainer's configurations per kind, without any code (Rule 17);
// - "retrieve" gets the sealed payload (the envelope of the Admin Dashboard) from the trainer source;
// - "unlock" opens it with the access code given by the trainer and returns the typed profiles.
// Everything that depends on the Admin Dashboard contract (routes, authentication, payload format, cryptography) stays behind
// ITrainerModelsOverviewSource, ITrainerConfigurationSource and ITrainerConfigurationDecryptor: the Admin Dashboard adapter
// (AdminModelConfigs*.cs, contract v1) or the mock.

/// <summary>Why a step of the trainer flow failed (Business Rule 15). The dashboard API writes it in camelCase (<c>notConfigured</c>, <c>invalidCode</c>…).</summary>
public enum TrainerConfigurationErrorKind
{
    /// <summary>
    /// Something the admin API needs is missing; <see cref="TrainerConfigurationError.Missing"/> says what: <c>serverUrl</c> (absent, or not HTTPS),
    /// <c>apiKey</c>, <c>workshop</c> (the candidate has not joined the workshop: no identity or no dev token yet). Nothing was sent.
    /// </summary>
    NotConfigured,

    /// <summary>The trainer configuration cannot be retrieved. Phase 1 answer, no longer produced by the Admin Dashboard adapter; the mock can still simulate it.</summary>
    NotAvailable,

    /// <summary>The admin API refused a credential (401); <see cref="TrainerConfigurationError.Credential"/> says which: <c>apiKey</c> or <c>devToken</c>.</summary>
    Unauthorized,

    /// <summary>The participant account is archived (403).</summary>
    Forbidden,

    /// <summary>
    /// Too many requests (429). <see cref="TrainerConfigurationError.RetryAfterSeconds"/>: the wait asked by the server; <see cref="TrainerConfigurationError.Reason"/>:
    /// <c>lockedOut</c>, <c>accessCodeFrozen</c> (unlock), <c>tooManyEnvelopes</c> (sealed GET: 5 envelopes per minute), or none for the per-minute limit.
    /// The dashboard sends nothing to the routes under that limit before the wait is over.
    /// </summary>
    RateLimited,

    /// <summary>The admin dashboard cannot serve the keys now (503 <c>unavailable</c>, 500 <c>internal</c>, other 5xx); <see cref="TrainerConfigurationError.Reason"/> says which when known.</summary>
    Unavailable,

    /// <summary>The admin dashboard answered without an envelope; <see cref="TrainerConfigurationError.Reason"/> holds its <c>sealedUnavailableReason</c> (<c>noAccessCode</c>, <c>workshopClosed</c>…).</summary>
    SealedUnavailable,

    /// <summary>The admin dashboard could not be reached or did not answer in time (DNS, connection, timeout).</summary>
    Network,

    /// <summary>The admin dashboard has no such route (404): the model configuration routes are not deployed on that server.</summary>
    NotFound,

    /// <summary>The answer is not what the contract defines (malformed or unexpected body), or what it unlocks is not a usable trainer configuration.</summary>
    InvalidResponse,

    /// <summary>The access code does not unlock the configuration; <see cref="TrainerConfigurationError.AttemptsLeft"/>: the attempts left on this envelope, when the server says.</summary>
    InvalidCode,

    /// <summary>
    /// The envelope can no longer be unlocked (410); <see cref="TrainerConfigurationError.Reason"/>: <c>unknown</c>, <c>used</c>, <c>expired</c>, <c>exhausted</c>,
    /// <c>keyRotated</c>, <c>workshopChanged</c>, <c>workshopClosed</c>, <c>accessCodeChanged</c>, <c>accessCodeRevoked</c>, <c>accessCodeExpired</c>,
    /// <c>accessCodeUnreadable</c>, <c>configChanged</c> (a configuration of the envelope was deleted, disabled, re-targeted or got a new key), or any future identifier.
    /// </summary>
    EnvelopeExpired,

    /// <summary>Anything else. The dashboard log says what happened (never with a secret); the result only has the HTTP status, if any.</summary>
    Unknown,
}

/// <summary>
/// An expected failure of the trainer flow: a result, never an exception. Every member is a code or a number taken from the configuration or
/// from the error of the admin API, never a secret, so <see cref="object.ToString"/> and the JSON of the dashboard API can show all of them.
/// <see cref="Missing"/> is only filled for <see cref="TrainerConfigurationErrorKind.NotConfigured"/>: <c>serverUrl</c>, <c>apiKey</c>, <c>workshop</c>.
/// </summary>
public sealed record TrainerConfigurationError(TrainerConfigurationErrorKind Kind, IReadOnlyList<string> Missing)
{
    /// <summary>Values of <see cref="Credential"/>.</summary>
    public const string ApiKeyCredential = "apiKey";

    public const string DevTokenCredential = "devToken";

    public TrainerConfigurationError(TrainerConfigurationErrorKind kind)
        : this(kind, [])
    {
    }

    /// <summary><see cref="TrainerConfigurationErrorKind.Unauthorized"/>: the credential the admin API refused, <c>apiKey</c> or <c>devToken</c>; null when it did not say.</summary>
    public string? Credential { get; init; }

    /// <summary><see cref="TrainerConfigurationErrorKind.RateLimited"/>: how long to wait before calling that route again, in whole seconds.</summary>
    public int? RetryAfterSeconds { get; init; }

    /// <summary>
    /// The reason code of the admin API, for the kinds that have one: <c>rateLimited</c> (<c>lockedOut</c>, <c>accessCodeFrozen</c>, <c>tooManyEnvelopes</c>),
    /// <c>sealedUnavailable</c> (<c>sealedUnavailableReason</c>), <c>envelopeExpired</c> (<c>details.reason</c>), <c>unavailable</c>
    /// (<c>unavailable</c>, <c>internal</c>), <c>unknown</c> (<c>validation</c>…). A reason this dashboard does not know is kept as received (forward
    /// compatibility); only identifiers are kept, any other text is dropped.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary><see cref="TrainerConfigurationErrorKind.InvalidCode"/>: the attempts left on this envelope (0–2), when the server says.</summary>
    public int? AttemptsLeft { get; init; }

    /// <summary>The HTTP status of the answer of the admin API, when there was one.</summary>
    public int? HttpStatus { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Kind = {Kind}, Missing = [{string.Join(", ", Missing)}]");
        Append(builder, nameof(Credential), Credential);
        Append(builder, nameof(RetryAfterSeconds), RetryAfterSeconds);
        Append(builder, nameof(Reason), Reason);
        Append(builder, nameof(AttemptsLeft), AttemptsLeft);
        Append(builder, nameof(HttpStatus), HttpStatus);
        return true;
    }

    private static void Append(StringBuilder builder, string name, object? value)
    {
        if (value is not null)
        {
            builder.Append($", {name} = {value}");
        }
    }
}

/// <summary>The result of one step of the trainer flow: a value or an expected failure, never both. <see cref="ToString"/> never prints the value.</summary>
internal sealed class TrainerOutcome<T>
    where T : class
{
    private TrainerOutcome(T? value, TrainerConfigurationError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public TrainerConfigurationError? Error { get; }

    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool Succeeded => Error is null;

    public static TrainerOutcome<T> Success(T value) => new(value ?? throw new ArgumentNullException(nameof(value)), null);

    public static TrainerOutcome<T> Failure(TrainerConfigurationError error) => new(null, error ?? throw new ArgumentNullException(nameof(error)));

    public static TrainerOutcome<T> Failure(TrainerConfigurationErrorKind kind) => Failure(new TrainerConfigurationError(kind));

    public override string ToString() => Succeeded ? $"{typeof(T).Name}: succeeded" : $"{typeof(T).Name}: {Error.Kind}";
}

/// <summary>
/// What the trainer source returns, that only the decryptor can open: the envelope and configurations of the Admin Dashboard
/// (<see cref="Admin"/>, contract v1), or opaque bytes (<see cref="Content"/>, the mock). <see cref="Overview"/>: the configurations the
/// candidate reviews, recomputed from the same answer (applicability with its <c>unsealedConfigs</c>).
/// Kept in memory only, never written nor logged: <see cref="ToString"/> does not print them, and the JSON serializer does not see them (internal).
/// </summary>
internal sealed class SealedTrainerPayload
{
    private readonly byte[] _content;

    public SealedTrainerPayload(ReadOnlySpan<byte> content)
    {
        _content = content.ToArray();
    }

    /// <summary>The answer of <c>GET /api/v1/model-configs?sealed=1</c>, with an envelope.</summary>
    public SealedTrainerPayload(AdminSealedConfiguration admin)
    {
        Admin = admin ?? throw new ArgumentNullException(nameof(admin));
        Overview = admin.Overview;
        _content = [];
    }

    internal ReadOnlySpan<byte> Content => _content;

    /// <summary>The envelope, the configurations and the <c>devId</c> the envelope is bound to (Admin Dashboard adapter); null for the mock.</summary>
    internal AdminSealedConfiguration? Admin { get; }

    /// <summary>The trainer configurations per kind as this payload describes them; null when the source has none.</summary>
    internal TrainerModelsOverview? Overview { get; init; }

    internal bool IsEmpty => Admin is null && _content.Length == 0;

    public override string ToString() => "SealedTrainerPayload (opaque)";
}

/// <summary>
/// A retrieved trainer configuration, still sealed: what the dashboard holds between "retrieve" and "unlock"
/// (in memory only, until <see cref="ExpiresAt"/> at most: Business Rule 14). <see cref="Simulated"/>: produced by the mock.
/// </summary>
internal sealed class SealedTrainerConfiguration(SealedTrainerPayload payload, DateTimeOffset retrievedAt, bool simulated)
{
    internal SealedTrainerPayload Payload { get; } = payload;

    public DateTimeOffset RetrievedAt { get; } = retrievedAt;

    public bool Simulated { get; } = simulated;

    /// <summary>When the envelope stops being accepted by the admin dashboard (its <c>expiresAt</c>); null for the mock, which never expires.</summary>
    public DateTimeOffset? ExpiresAt => Payload.Admin?.Envelope.ExpiresAtUtc;

    /// <summary>The configurations per kind of this answer: what the candidate can still unlock (an unsealed default is unusable).</summary>
    public TrainerModelsOverview? Overview => Payload.Overview;

    public override string ToString() =>
        $"SealedTrainerConfiguration {{ RetrievedAt = {RetrievedAt:O}, ExpiresAt = {(ExpiresAt is { } expiresAt ? expiresAt.ToString("O") : "none")}, Simulated = {Simulated} }}";
}

/// <summary>One model of the unlocked trainer configuration. The key is never printed nor serialized.</summary>
internal sealed record TrainerModelProfile(ModelKind Kind, string Provider, string Endpoint, string Deployment, [property: JsonIgnore] string? ApiKey)
{
    /// <summary>The id of the trainer configuration this profile comes from (<c>configs[].id</c>; the mock uses its simulated ids); null for a profile built by hand.</summary>
    public string? ConfigId { get; init; }

    /// <summary>The name of that configuration, for the "trainer configuration" label of the page.</summary>
    public string? ConfigName { get; init; }

    /// <summary>Its <c>updatedAt</c>, verbatim: stored with the profile to tell "updated by the trainer since your last unlock".</summary>
    public string? ConfigUpdatedAt { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Kind = {Kind}, Provider = {Provider}, Endpoint = {Endpoint}, Deployment = {Deployment}, ApiKey = {(ApiKey is null ? "none" : "set")}");
        builder.Append($", ConfigId = {ConfigId}, ConfigName = {ConfigName}, ConfigUpdatedAt = {ConfigUpdatedAt}");
        return true;
    }
}

/// <summary>
/// The format of the access code (Business Rule 13): trim, invariant upper-case, remove spaces and dashes, then exactly
/// <see cref="Length"/> characters of <see cref="Alphabet"/>, or the <c>code.length</c> / <c>code.alphabet</c> of the envelope when it has them.
/// </summary>
internal static class TrainerCodeFormat
{
    public const int Length = 5;

    public const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    /// <summary>The normalized code, or null when it is not exactly <paramref name="length"/> characters of <paramref name="alphabet"/>.</summary>
    public static string? Normalize(string? typed, int length = Length, string alphabet = Alphabet)
    {
        if (typed is null)
        {
            return null;
        }

        string code = typed.Trim().ToUpperInvariant().Replace("-", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
        return code.Length == length && code.All(c => alphabet.Contains(c, StringComparison.Ordinal)) ? code : null;
    }

    /// <summary>An envelope's code format is only believed when it is sane (1–32 characters, an alphabet of distinct upper-case letters and digits).</summary>
    public static bool IsSane(int length, string? alphabet) =>
        length is >= 1 and <= 32 && alphabet is { Length: >= 2 and <= 36 }
        && alphabet.All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c)) && alphabet.Distinct().Count() == alphabet.Length;

    /// <summary>The format of a payload: its envelope's when it has a sane one, otherwise the contract's.</summary>
    public static (int Length, string Alphabet) For(SealedTrainerPayload payload) =>
        payload.Admin?.Envelope.Code is { } code && IsSane(code.Length, code.Alphabet) ? (code.Length, code.Alphabet) : (Length, Alphabet);
}

/// <summary>
/// What a decryptor returns: the typed profiles, or the error. <see cref="Renewed"/>: when the opening needed a new envelope (retry after
/// <c>expired</c>, <c>unknown</c> or <c>keyRotated</c>), the payload now in use, so the caller replaces the one it holds, whether the opening
/// succeeded or not. <see cref="EnvelopeGone"/>: the payload that was passed in must not be used again (the server answered 410, or it was replaced).
/// Never holds the code; <see cref="ToString"/> never prints the profiles.
/// </summary>
internal sealed class TrainerUnsealResult
{
    private TrainerUnsealResult(IReadOnlyList<TrainerModelProfile>? profiles, TrainerConfigurationError? error, SealedTrainerPayload? renewed, bool envelopeGone)
    {
        Profiles = profiles;
        Error = error;
        Renewed = renewed;
        EnvelopeGone = envelopeGone || renewed is not null;
    }

    public IReadOnlyList<TrainerModelProfile>? Profiles { get; }

    public TrainerConfigurationError? Error { get; }

    public SealedTrainerPayload? Renewed { get; }

    public bool EnvelopeGone { get; }

    [MemberNotNullWhen(true, nameof(Profiles))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool Succeeded => Error is null;

    public static TrainerUnsealResult Success(IReadOnlyList<TrainerModelProfile> profiles, SealedTrainerPayload? renewed = null) =>
        new(profiles ?? throw new ArgumentNullException(nameof(profiles)), null, renewed, envelopeGone: false);

    public static TrainerUnsealResult Failure(TrainerConfigurationError error, SealedTrainerPayload? renewed = null, bool envelopeGone = false) =>
        new(null, error ?? throw new ArgumentNullException(nameof(error)), renewed, envelopeGone);

    public static TrainerUnsealResult Failure(TrainerConfigurationErrorKind kind) => Failure(new TrainerConfigurationError(kind));

    public override string ToString() =>
        $"TrainerUnsealResult {{ {(Succeeded ? "succeeded" : Error.Kind)}, Renewed = {Renewed is not null}, EnvelopeGone = {EnvelopeGone} }}";
}

/// <summary>
/// The unlocked trainer configuration: one profile per kind the trainer provides (the LLM and/or the embedding model) and the retrieval date.
/// Held in memory only until the candidate confirms or cancels (Business Rule 14); never printed nor serialized with its keys.
/// </summary>
internal sealed class TrainerConfigurationBundle(IReadOnlyList<TrainerModelProfile> profiles, DateTimeOffset retrievedAt, bool simulated)
{
    public IReadOnlyList<TrainerModelProfile> Profiles { get; } = profiles;

    public DateTimeOffset RetrievedAt { get; } = retrievedAt;

    public bool Simulated { get; } = simulated;

    public TrainerModelProfile? Chat => Profiles.FirstOrDefault(p => p.Kind == ModelKind.Chat);

    public TrainerModelProfile? Embedding => Profiles.FirstOrDefault(p => p.Kind == ModelKind.Embedding);

    /// <summary>
    /// The profiles as <see cref="AzureOpenAISettingsStore"/> saves them, with the retrieval date. The kinds listed in <paramref name="useNow"/>
    /// switch to trainer mode in the same write; the others only get their trainer profile (Business Rule 9).
    /// </summary>
    public IReadOnlyList<TrainerProfileUpdate> ToProfileUpdates(IReadOnlyCollection<ModelKind> useNow) =>
        Profiles.Select(p => new TrainerProfileUpdate(p.Kind, p.Provider, p.Endpoint, p.Deployment, p.ApiKey, RetrievedAt, useNow.Contains(p.Kind))
        {
            ConfigId = p.ConfigId,
            ConfigName = p.ConfigName,
            ConfigUpdatedAt = p.ConfigUpdatedAt,
        }).ToList();

    public override string ToString() =>
        $"TrainerConfigurationBundle {{ Kinds = [{string.Join(", ", Profiles.Select(p => p.Kind))}], RetrievedAt = {RetrievedAt:O}, Simulated = {Simulated} }}";
}

internal enum TrainerUnlockStatus
{
    /// <summary><see cref="TrainerUnlockResult.Bundle"/> holds the configuration.</summary>
    Unlocked,

    /// <summary>The code does not have the format of Rule 13 (5 characters of the alphabet once normalized): refused before anything else, nothing was tried (distinct from a wrong code).</summary>
    InvalidCodeFormat,

    /// <summary><see cref="TrainerUnlockResult.Error"/> says why (<see cref="TrainerConfigurationErrorKind.InvalidCode"/> for a wrong code).</summary>
    Failed,
}

/// <summary>
/// The result of "unlock". It never holds the code; <see cref="ToString"/> never prints the bundle.
/// <see cref="EnvelopeId"/>: the envelope this answer is about (the one passed in, or <see cref="Renewed"/> when a retry needed a new one; null for the mock).
/// <see cref="Renewed"/>: the new sealed configuration to keep instead of the old one, on success and on failure (an <c>invalidCode</c> then counts
/// the attempts of the new envelope). <see cref="EnvelopeGone"/>: the envelope passed in must not be used again.
/// </summary>
internal sealed class TrainerUnlockResult
{
    private TrainerUnlockResult(TrainerUnlockStatus status, TrainerConfigurationBundle? bundle, TrainerConfigurationError? error,
        SealedTrainerConfiguration? renewed = null, bool envelopeGone = false, string? envelopeId = null)
    {
        Status = status;
        Bundle = bundle;
        Error = error;
        Renewed = renewed;
        EnvelopeGone = envelopeGone;
        EnvelopeId = envelopeId;
    }

    public static TrainerUnlockResult InvalidCodeFormat { get; } = new(TrainerUnlockStatus.InvalidCodeFormat, null, null);

    public TrainerUnlockStatus Status { get; }

    public TrainerConfigurationBundle? Bundle { get; }

    public TrainerConfigurationError? Error { get; }

    public SealedTrainerConfiguration? Renewed { get; }

    public bool EnvelopeGone { get; }

    public string? EnvelopeId { get; }

    public static TrainerUnlockResult Unlocked(TrainerConfigurationBundle bundle, SealedTrainerConfiguration? renewed = null, string? envelopeId = null) =>
        new(TrainerUnlockStatus.Unlocked, bundle, null, renewed, envelopeGone: renewed is not null, envelopeId);

    public static TrainerUnlockResult Failed(TrainerConfigurationError error, SealedTrainerConfiguration? renewed = null, bool envelopeGone = false, string? envelopeId = null) =>
        new(TrainerUnlockStatus.Failed, null, error, renewed, envelopeGone || renewed is not null, envelopeId);

    public override string ToString() =>
        (Error is null ? $"TrainerUnlockResult {{ Status = {Status}" : $"TrainerUnlockResult {{ Status = {Status}, Error = {Error.Kind}")
        + $", EnvelopeId = {EnvelopeId}, Renewed = {Renewed is not null}, EnvelopeGone = {EnvelopeGone} }}";
}

/// <summary>
/// Where "retrieve" gets the sealed trainer configuration. Expected failures are results with the error kinds of Rule 15, never exceptions.
/// Implementations: <see cref="AdminTrainerConfigurationSource"/> (contract v1, <c>GET /api/v1/model-configs?sealed=1</c>) and
/// <see cref="MockTrainerConfiguration"/>. Called only when the candidate clicks Unlock: never polled, never retried in a loop (Rule 19).
/// </summary>
internal interface ITrainerConfigurationSource
{
    Task<TrainerOutcome<SealedTrainerPayload>> RetrieveAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Where the overview of the trainer configurations comes from (Rule 17: no code, never a key). Called when the AI models tab opens or on
/// an explicit Refresh, never polled (Rule 19). Implementations: <see cref="AdminTrainerConfigurationSource"/> and <see cref="MockTrainerConfiguration"/>.
/// </summary>
internal interface ITrainerModelsOverviewSource
{
    Task<TrainerOutcome<TrainerModelsOverview>> GetOverviewAsync(CancellationToken cancellationToken);
}

/// <summary>
/// How "unlock" opens a sealed payload with the access code. The code reaches an implementation already normalized and validated
/// (<see cref="TrainerCodeFormat"/>); it is used for this call only, never kept, logged nor returned. A wrong code is <see cref="TrainerConfigurationErrorKind.InvalidCode"/>,
/// a payload that cannot be read <see cref="TrainerConfigurationErrorKind.InvalidResponse"/>.
/// Implementations: <see cref="AdminEnvelopeDecryptor"/> (contract v1: exactly the Admin Dashboard's protocol, no home-made cryptography; T11)
/// and <see cref="MockTrainerConfiguration"/>.
/// </summary>
internal interface ITrainerConfigurationDecryptor
{
    ValueTask<TrainerUnsealResult> UnsealAsync(SealedTrainerPayload payload, string code, CancellationToken cancellationToken);
}
