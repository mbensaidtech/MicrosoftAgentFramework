using System.Diagnostics.CodeAnalysis;
using LabDashboard.Reporting;
using LabDashboard.Settings;

namespace LabDashboard.TrainerModels;

/// <summary>
/// The single internal integration point of the trainer configuration (Business Rules 13–20): the "overview" of the trainer's configurations,
/// "retrieve" a sealed configuration, then "unlock" it with the trainer's code. The adapters are chosen once, at startup, in this order:
/// <list type="number">
/// <item>the mock, when <c>Dashboard:TrainerModels:Mock:Enabled</c> is set (development and tests), whatever the rest of the configuration;</item>
/// <item>otherwise <see cref="TrainerConfigurationErrorKind.NotConfigured"/> when the admin dashboard URL is absent or not HTTPS
/// (<see cref="ReportingClient.ResolveServerUrl"/>);</item>
/// <item>otherwise the Admin Dashboard adapter (contract v1): <see cref="AdminTrainerConfigurationSource"/> and <see cref="AdminEnvelopeDecryptor"/>.</item>
/// </list>
/// The admin API key and the identity are read at each call, the key exactly like the reporting (<see cref="ReportingClient.ResolveApiKey"/>, including
/// the key of the trainer's invitation in the user secrets): until both are there (an identity with a dev token: the candidate joined the workshop),
/// every step answers <see cref="TrainerConfigurationErrorKind.NotConfigured"/> with <c>apiKey</c> and/or <c>workshop</c>, without any network call.
/// Expected failures are results, never exceptions; an adapter that throws anyway gives <see cref="TrainerConfigurationErrorKind.Unknown"/>.
/// The code (kept for one call, normalized: Rule 13), the sealed payload and the unlocked keys never reach a log, a field of this service or the text of a result.
/// </summary>
internal sealed class TrainerConfigurationService : IDisposable
{
    public const int CodeLength = TrainerCodeFormat.Length;

    /// <summary>Names of the missing settings in <see cref="TrainerConfigurationError.Missing"/>.</summary>
    public const string ServerUrlSetting = "serverUrl";

    public const string ApiKeySetting = "apiKey";

    /// <summary>The candidate has not joined the workshop: no identity, or no dev token yet.</summary>
    public const string WorkshopSetting = "workshop";

    private readonly ITrainerConfigurationSource _source;
    private readonly ITrainerConfigurationDecryptor _decryptor;
    private readonly ITrainerModelsOverviewSource _overview;
    private readonly IReadOnlyList<string> _missing;
    private readonly Func<string?>? _apiKey;
    private readonly Func<DevIdentity?>? _identity;
    private readonly IDisposable? _owned;
    private readonly ILogger _logger;
    private readonly TimeProvider _time;

    public TrainerConfigurationService(
        DashboardOptions options,
        IdentityStore identity,
        AzureOpenAISettingsStore secrets,
        ILogger<TrainerConfigurationService> logger,
        TimeProvider? time = null,
        Func<string, string?>? environment = null,
        HttpMessageHandler? handler = null)
        : this(options, () => identity.Current, logger, time, environment, handler,
            storedApiKey: () => secrets.GetEntry(ReportingClient.WorkshopKeySection, ReportingClient.ApiKeyName))
    {
    }

    /// <summary>
    /// Tests: the identity as a function, the HTTP handler of the Admin Dashboard adapter (a fake: never the network), and the admin API key of the
    /// invitation as stored in the user secrets (<paramref name="storedApiKey"/>).
    /// </summary>
    internal TrainerConfigurationService(
        DashboardOptions options,
        Func<DevIdentity?> identity,
        ILogger logger,
        TimeProvider? time = null,
        Func<string, string?>? environment = null,
        HttpMessageHandler? handler = null,
        Func<string?>? storedApiKey = null)
        : this(Select(options, identity, environment ?? Environment.GetEnvironmentVariable, storedApiKey, logger, time ?? TimeProvider.System, handler), logger, time)
    {
    }

    /// <summary>Tests: any source and decryptor (a spy, one that throws…), behind the same checks. No identity is required.</summary>
    internal TrainerConfigurationService(
        ITrainerConfigurationSource source,
        ITrainerConfigurationDecryptor decryptor,
        ILogger logger,
        bool simulated = false,
        IReadOnlyList<string>? missing = null,
        TimeProvider? time = null,
        ITrainerModelsOverviewSource? overview = null)
        : this(new Adapters(source, decryptor, overview ?? new TrainerConfigurationNotConfigured(missing ?? []), simulated, missing ?? [], ApiKey: null, Identity: null, Owned: null), logger, time)
    {
    }

    private TrainerConfigurationService(Adapters adapters, ILogger logger, TimeProvider? time)
    {
        _source = adapters.Source;
        _decryptor = adapters.Decryptor;
        _overview = adapters.Overview;
        Simulated = adapters.Simulated;
        _missing = adapters.Missing;
        _apiKey = adapters.ApiKey;
        _identity = adapters.Identity;
        _owned = adapters.Owned;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>True when the mock answers: the UI shows the "Simulated trainer configuration" notice.</summary>
    public bool Simulated { get; }

    /// <summary>
    /// What the admin API configuration lacks, in this order: <c>serverUrl</c> (absent or not HTTPS), <c>apiKey</c>, <c>workshop</c>. The last two are
    /// read now: entering the trainer's invitation or joining the workshop removes them without a restart. Empty when everything is there.
    /// Ignored while the mock answers.
    /// </summary>
    public IReadOnlyList<string> MissingConfiguration
    {
        get
        {
            List<string> missing = [.. _missing];
            if (_apiKey is not null && _apiKey() is null)
            {
                missing.Add(ApiKeySetting);
            }

            if (_identity is not null && !HasWorkshopIdentity(_identity()))
            {
                missing.Add(WorkshopSetting);
            }

            return missing;
        }
    }

    /// <summary>
    /// Whether the typed code has the format of Business Rule 13 once normalized (trim, upper-case, no spaces or dashes: exactly 5 characters of
    /// <see cref="TrainerCodeFormat.Alphabet"/>). <see cref="UnlockAsync"/> uses the envelope's own <c>code.length</c> / <c>code.alphabet</c> when it has them.
    /// </summary>
    public static bool IsValidCode([NotNullWhen(true)] string? code) => TrainerCodeFormat.Normalize(code) is not null;

    /// <summary>
    /// The trainer's configurations per kind, without any code (Rule 17). Call it when the AI models tab opens or on Refresh, never in a loop
    /// (Rule 19). Never throws for an expected failure; cancelling <paramref name="cancellationToken"/> does throw.
    /// </summary>
    public async Task<TrainerOutcome<TrainerModelsOverview>> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        if (NotConfigured() is { } notConfigured)
        {
            return TrainerOutcome<TrainerModelsOverview>.Failure(notConfigured);
        }

        try
        {
            return await _overview.GetOverviewAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogUnexpected("overview", ex);
            return TrainerOutcome<TrainerModelsOverview>.Failure(TrainerConfigurationErrorKind.Unknown);
        }
    }

    /// <summary>
    /// Gets the sealed trainer configuration: the envelope, on Unlock only (Rule 19). Never throws for an expected failure;
    /// cancelling <paramref name="cancellationToken"/> does throw.
    /// </summary>
    public async Task<TrainerOutcome<SealedTrainerConfiguration>> RetrieveAsync(CancellationToken cancellationToken = default)
    {
        if (NotConfigured() is { } notConfigured)
        {
            return TrainerOutcome<SealedTrainerConfiguration>.Failure(notConfigured);
        }

        TrainerOutcome<SealedTrainerPayload> retrieved;
        try
        {
            retrieved = await _source.RetrieveAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogUnexpected("retrieve", ex);
            return TrainerOutcome<SealedTrainerConfiguration>.Failure(TrainerConfigurationErrorKind.Unknown);
        }

        if (!retrieved.Succeeded)
        {
            return TrainerOutcome<SealedTrainerConfiguration>.Failure(retrieved.Error);
        }

        return retrieved.Value.IsEmpty
            ? TrainerOutcome<SealedTrainerConfiguration>.Failure(TrainerConfigurationErrorKind.InvalidResponse)
            : TrainerOutcome<SealedTrainerConfiguration>.Success(new SealedTrainerConfiguration(retrieved.Value, _time.GetUtcNow(), Simulated));
    }

    /// <summary>
    /// Opens a retrieved configuration with the trainer's code. The code is checked first (<see cref="TrainerUnlockStatus.InvalidCodeFormat"/>:
    /// nothing else is tried), then handed to the decryptor for this call only. The result holds the typed profiles, never the code.
    /// </summary>
    public async Task<TrainerUnlockResult> UnlockAsync(SealedTrainerConfiguration configuration, string? code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        (int length, string alphabet) = TrainerCodeFormat.For(configuration.Payload);
        if (TrainerCodeFormat.Normalize(code, length, alphabet) is not { } normalized)
        {
            return TrainerUnlockResult.InvalidCodeFormat;
        }

        // A configuration sealed by the mock is never handed to the real decryptor, nor the opposite.
        if (configuration.Simulated != Simulated)
        {
            return TrainerUnlockResult.Failed(new TrainerConfigurationError(TrainerConfigurationErrorKind.InvalidResponse));
        }

        TrainerUnsealResult unsealed;
        try
        {
            unsealed = await _decryptor.UnsealAsync(configuration.Payload, normalized, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogUnexpected("unlock", ex);
            return TrainerUnlockResult.Failed(new TrainerConfigurationError(TrainerConfigurationErrorKind.Unknown));
        }

        // A retry that needed a new envelope hands it over, on success and on failure: the caller replaces the one it holds.
        SealedTrainerConfiguration? renewed = unsealed.Renewed is null ? null : new SealedTrainerConfiguration(unsealed.Renewed, _time.GetUtcNow(), Simulated);
        string? envelopeId = (renewed ?? configuration).Payload.Admin?.Envelope.EnvelopeId;
        if (!unsealed.Succeeded)
        {
            return TrainerUnlockResult.Failed(unsealed.Error, renewed, unsealed.EnvelopeGone, envelopeId);
        }

        // Field formats are validated when the profiles are saved (AzureOpenAISettingsStore); here only what makes a configuration usable at all.
        return IsUsable(unsealed.Profiles)
            ? TrainerUnlockResult.Unlocked(new TrainerConfigurationBundle(unsealed.Profiles, (renewed ?? configuration).RetrievedAt, configuration.Simulated), renewed, envelopeId)
            : TrainerUnlockResult.Failed(new TrainerConfigurationError(TrainerConfigurationErrorKind.InvalidResponse), renewed, unsealed.EnvelopeGone, envelopeId);
    }

    public void Dispose() => _owned?.Dispose();

    /// <summary>The <see cref="TrainerConfigurationErrorKind.NotConfigured"/> answer when something is missing (never while the mock answers).</summary>
    private TrainerConfigurationError? NotConfigured()
    {
        if (Simulated)
        {
            return null;
        }

        IReadOnlyList<string> missing = MissingConfiguration;
        return missing.Count > 0 ? new TrainerConfigurationError(TrainerConfigurationErrorKind.NotConfigured, missing) : null;
    }

    /// <summary>Joined the workshop: an identity with the dev token the admin returned at registration.</summary>
    private static bool HasWorkshopIdentity(DevIdentity? identity) =>
        identity is { UserId.Length: > 0 } && !string.IsNullOrWhiteSpace(identity.DevToken);

    /// <summary>At least one profile, one per kind at most, each with a provider, an endpoint and a deployment (the key is optional: Microsoft Entra ID).</summary>
    private static bool IsUsable(IReadOnlyList<TrainerModelProfile> profiles) =>
        profiles.Count > 0
        && profiles.All(p => p is not null && Enum.IsDefined(p.Kind)
            && !string.IsNullOrWhiteSpace(p.Provider) && !string.IsNullOrWhiteSpace(p.Endpoint) && !string.IsNullOrWhiteSpace(p.Deployment))
        && profiles.Select(p => p.Kind).Distinct().Count() == profiles.Count;

    /// <summary>Only the exception type: its message could quote what an adapter was reading (payload, answer of the server).</summary>
    private void LogUnexpected(string step, Exception exception) =>
        _logger.LogWarning("Trainer configuration: the {Step} step failed unexpectedly ({ExceptionType}); reported as 'unknown'.", step, exception.GetType().FullName);

    private static Adapters Select(
        DashboardOptions options, Func<DevIdentity?> identity, Func<string, string?> environment, Func<string?>? storedApiKey,
        ILogger logger, TimeProvider time, HttpMessageHandler? handler)
    {
        string? serverUrl = ReportingClient.ResolveServerUrl(options.Reporting);
        Func<string?> apiKey = () => ReportingClient.ResolveApiKey(options.Reporting, environment, storedApiKey);
        List<string> missing = [];
        if (serverUrl is null)
        {
            missing.Add(ServerUrlSetting);
        }
        else if (!AdminModelConfigsClient.IsAllowedServerUrl(serverUrl))
        {
            // The dev token and the API key never travel in clear: an http:// admin is only accepted on this machine.
            missing.Add(ServerUrlSetting);
            logger.LogWarning("Dashboard:Reporting:ServerUrl is not an HTTPS URL: the trainer configuration is never requested from it (http:// is only accepted for localhost).");
        }

        if (options.TrainerModels.Mock is { Enabled: true } mockOptions)
        {
            MockTrainerConfiguration mock = new(mockOptions, logger);
            logger.LogWarning("Simulated trainer configuration is on (Dashboard:TrainerModels:Mock:Enabled): it returns fake values and never calls the admin dashboard.");
            return new Adapters(mock, mock, mock, Simulated: true, missing.AsReadOnly(), apiKey, identity, Owned: null);
        }

        if (missing.Count > 0)
        {
            TrainerConfigurationNotConfigured notConfigured = new(missing.AsReadOnly());
            return new Adapters(notConfigured, notConfigured, notConfigured, Simulated: false, missing.AsReadOnly(), apiKey, identity, Owned: null);
        }

        AdminModelConfigsClient client = new(new Uri(serverUrl!), apiKey, identity, logger,
            TimeSpan.FromSeconds(Math.Max(1, options.Reporting.RequestTimeoutSeconds)), time, handler);
        AdminTrainerConfigurationSource source = new(client);
        return new Adapters(source, new AdminEnvelopeDecryptor(client, logger), source, Simulated: false, missing.AsReadOnly(), apiKey, identity, client);
    }

    /// <summary><see cref="Missing"/>: what is missing for good (the server URL); <see cref="ApiKey"/> and <see cref="Identity"/> are read at each call.</summary>
    private sealed record Adapters(
        ITrainerConfigurationSource Source,
        ITrainerConfigurationDecryptor Decryptor,
        ITrainerModelsOverviewSource Overview,
        bool Simulated,
        IReadOnlyList<string> Missing,
        Func<string?>? ApiKey,
        Func<DevIdentity?>? Identity,
        IDisposable? Owned);
}
