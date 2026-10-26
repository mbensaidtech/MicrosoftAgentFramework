using System.Globalization;
using LabDashboard.Settings;

namespace LabDashboard.TrainerModels;

/// <summary>
/// The unlock flow as the page sees it (Business Rules 13, 14, 20). <see cref="Status"/>: <c>idle</c> (nothing held), <c>codeRequired</c> (an unlock
/// is under way for <see cref="Kinds"/>: the page asks for the code until <see cref="ExpiresAt"/>), <c>unlocked</c> (the keys are saved and
/// <see cref="Models"/> is the new state of the AI models). Never a key, a code, a proof, an unlock key or the envelope.
/// </summary>
internal sealed record TrainerUnlockView(
    string Status,
    IReadOnlyList<ModelKind> Kinds,
    DateTimeOffset? ExpiresAt,
    int CodeLength,
    string CodeAlphabet,
    int? AttemptsLeft,
    TrainerConfigurationError? Error,
    ModelSettingsStatus? Models = null)
{
    public const string Idle = "idle";
    public const string CodeRequired = "codeRequired";
    public const string Unlocked = "unlocked";
}

/// <summary>
/// What the AI models tab shows of the trainer's configurations (Rule 17): the <see cref="Overview"/> per kind, read without any code (each kind
/// says whether it changed since the candidate's last unlock), or the <see cref="Error"/> that prevents it; and the unlock under way, if any.
/// </summary>
internal sealed record TrainerModelsView(bool Simulated, TrainerModelsOverview? Overview, TrainerConfigurationError? Error, TrainerUnlockView Unlock);

/// <summary>
/// The trainer flow of the AI models tab (Business Rules 9, 13, 14, 17, 19, 20), between the local routes and <see cref="TrainerConfigurationService"/>:
/// <list type="number">
/// <item><see cref="GetOverviewAsync"/>: the trainer's configurations, no code (when the tab opens or on Refresh, never polled);</item>
/// <item><see cref="StartAsync"/>: the candidate ticked the kinds and clicked Unlock: one envelope is fetched and held;</item>
/// <item><see cref="SubmitCodeAsync"/>: the access code opens it; the keys of the ticked kinds are saved at once and those kinds switch to trainer mode,
/// the custom profiles untouched; nothing decrypted waits for a confirmation (the candidate confirmed by ticking, before the code);</item>
/// <item><see cref="CancelAsync"/>: the envelope is dropped.</item>
/// </list>
/// At most one envelope is held, in memory only, until success, cancel or its expiry (<c>expiresAt</c> measured with the admin's <c>serverTime</c>, so
/// that a skewed clock does not shorten it; 10 minutes at most). The code is used for one request only and never kept, logged or returned.
/// After the last attempt of an envelope, or a <c>410 exhausted</c> / <c>used</c>, the kinds stay ticked and the next code fetches a new envelope first.
/// </summary>
internal sealed class TrainerUnlockFlow : IDisposable
{
    /// <summary>The longest an envelope is held (the contract's envelopes live 10 minutes; the mock's never expire).</summary>
    public static readonly TimeSpan MaxHold = TimeSpan.FromMinutes(10);

    // Values of TrainerConfigurationError.Reason set here (identifiers only).
    /// <summary>The code was submitted while no unlock is under way (never started, cancelled, or expired): the candidate clicks Unlock again.</summary>
    public const string NoEnvelopeReason = "noEnvelope";

    /// <summary>A ticked kind is not usable in the trainer's current answer: the candidate reviews the configurations again.</summary>
    public const string NotApplicableReason = "notApplicable";

    private readonly TrainerConfigurationService _service;
    private readonly AzureOpenAISettingsStore _settings;
    private readonly ILogger _logger;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Pending? _pending;

    // Drops the held envelope at its deadline even when nobody looks; replaced at every hold. Only changed under _gate.
    private ITimer? _expiry;

    public TrainerUnlockFlow(TrainerConfigurationService service, AzureOpenAISettingsStore settings, ILogger<TrainerUnlockFlow> logger)
        : this(service, settings, logger, time: null)
    {
    }

    internal TrainerUnlockFlow(TrainerConfigurationService service, AzureOpenAISettingsStore settings, ILogger logger, TimeProvider? time)
    {
        _service = service;
        _settings = settings;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>The overview of the trainer's configurations, without any code, and the unlock under way. One call to the admin dashboard (Rule 19).</summary>
    public async Task<TrainerModelsView> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        TrainerOutcome<TrainerModelsOverview> outcome = await _service.GetOverviewAsync(cancellationToken);
        TrainerUnlockView unlock = View(Current());
        return outcome.Succeeded
            ? new TrainerModelsView(_service.Simulated, WithChangeDetection(outcome.Value), null, unlock)
            : new TrainerModelsView(_service.Simulated, null, outcome.Error, unlock);
    }

    /// <summary>The unlock under way, without calling anything.</summary>
    public TrainerUnlockView GetState() => View(Current());

    /// <summary>
    /// Unlock for the ticked kinds (1 or 2, distinct): drops any envelope held, fetches a new one (<c>?sealed=1</c>: the only place where an unlock
    /// starts, Rule 19) and holds it. Every ticked kind must be usable in that answer, or nothing is held (<c>notApplicable</c>: review again).
    /// </summary>
    public async Task<TrainerUnlockView> StartAsync(IReadOnlyList<ModelKind> kinds, CancellationToken cancellationToken = default)
    {
        if (kinds is not { Count: > 0 } || kinds.Distinct().Count() != kinds.Count || kinds.Any(k => !Enum.IsDefined(k)))
        {
            throw new ArgumentException("Tick one or two distinct kinds.", nameof(kinds));
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            Drop();
            TrainerOutcome<SealedTrainerConfiguration> retrieved = await _service.RetrieveAsync(cancellationToken);
            if (!retrieved.Succeeded)
            {
                return Idle(retrieved.Error);
            }

            if (NotUsable(retrieved.Value, kinds) is { } notUsable)
            {
                return Idle(notUsable);
            }

            Pending pending = Hold(new Pending([.. kinds], retrieved.Value, Deadline(retrieved.Value), AttemptsAllowed(retrieved.Value)));
            _logger.LogInformation("Trainer unlock started for {Kinds} (envelope {EnvelopeId}).", string.Join(", ", kinds), EnvelopeId(pending.Envelope));
            return View(pending);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Opens the held envelope with the access code. A code that is not of the envelope's format is refused before any call (Rule 13). Success
    /// saves the keys of the ticked kinds and switches them to trainer mode; every failure saves nothing (Rule 20 decides what is kept).
    /// Throws <see cref="AzureOpenAISettingsStoreException"/> when the user-secrets file cannot be written (the envelope is dropped anyway).
    /// </summary>
    public async Task<TrainerUnlockView> SubmitCodeAsync(string? code, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (Current() is not { } pending)
            {
                return Idle(new TrainerConfigurationError(TrainerConfigurationErrorKind.EnvelopeExpired) { Reason = NoEnvelopeReason });
            }

            (int length, string alphabet) = pending.Envelope is { } held ? TrainerCodeFormat.For(held.Payload) : (TrainerCodeFormat.Length, TrainerCodeFormat.Alphabet);
            if (TrainerCodeFormat.Normalize(code, length, alphabet) is null)
            {
                return View(pending, FormatError());
            }

            if (pending.Envelope is null)
            {
                // Rule 20: after the last attempt of an envelope, or a 410 exhausted / used, this attempt fetches a new envelope first.
                TrainerOutcome<SealedTrainerConfiguration> fresh = await _service.RetrieveAsync(cancellationToken);
                if (!fresh.Succeeded)
                {
                    // Too many envelopes (429) or a network failure: the kinds stay ticked, the candidate tries again later. Anything else: start again.
                    return fresh.Error.Kind is TrainerConfigurationErrorKind.RateLimited or TrainerConfigurationErrorKind.Network
                        ? View(pending, fresh.Error)
                        : Idle(fresh.Error);
                }

                if (NotUsable(fresh.Value, pending.Kinds) is { } notUsable)
                {
                    return Idle(notUsable);
                }

                pending = Hold(pending with { Envelope = fresh.Value, Deadline = Deadline(fresh.Value), AttemptsLeft = AttemptsAllowed(fresh.Value) });
            }

            TrainerUnlockResult result = await _service.UnlockAsync(pending.Envelope!, code, cancellationToken);
            return result.Status switch
            {
                TrainerUnlockStatus.Unlocked => await ApplyAsync(pending, result.Bundle!),
                TrainerUnlockStatus.InvalidCodeFormat => View(pending, FormatError()),
                _ => Failed(pending, result),
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>The candidate closed the code entry: the envelope is dropped, nothing is written.</summary>
    public async Task<TrainerUnlockView> CancelAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (Drop())
            {
                _logger.LogInformation("Trainer unlock cancelled: the envelope is dropped.");
            }

            return Idle(null);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        Drop();
        _gate.Dispose();
    }

    private async Task<TrainerUnlockView> ApplyAsync(Pending pending, TrainerConfigurationBundle bundle)
    {
        // The envelope is used: dropped whatever happens next (Rule 14).
        Drop();
        List<TrainerProfileUpdate> updates = bundle.ToProfileUpdates(pending.Kinds).Where(u => pending.Kinds.Contains(u.Kind)).ToList();
        if (pending.Kinds.Any(kind => updates.All(u => u.Kind != kind)))
        {
            _logger.LogWarning("Trainer unlock: the opened configuration has no usable profile for a ticked kind; nothing is saved.");
            return Idle(new TrainerConfigurationError(TrainerConfigurationErrorKind.InvalidResponse) { Reason = NotApplicableReason });
        }

        IReadOnlyDictionary<string, string> errors = await _settings.SaveTrainerProfilesAsync(updates);
        if (errors.Count > 0)
        {
            // Field names and codes only (chat.endpoint = invalid…): never a value.
            _logger.LogWarning("Trainer unlock: the settings store refused the trainer profiles ({Errors}); nothing is saved.",
                string.Join(", ", errors.Select(e => $"{e.Key}={e.Value}")));
            return Idle(new TrainerConfigurationError(TrainerConfigurationErrorKind.InvalidResponse) { Reason = TrainerModelsOverview.InvalidSettingsReason });
        }

        _logger.LogInformation("Trainer configuration unlocked: {Kinds} saved and in trainer mode.", string.Join(", ", pending.Kinds));
        return new TrainerUnlockView(TrainerUnlockView.Unlocked, pending.Kinds, null, TrainerCodeFormat.Length, TrainerCodeFormat.Alphabet, null, null,
            _settings.GetModelStatus());
    }

    /// <summary>Rule 20: what is kept after a failed attempt. Nothing is ever written here.</summary>
    private TrainerUnlockView Failed(Pending pending, TrainerUnlockResult result)
    {
        TrainerConfigurationError error = result.Error!;

        // The envelope still usable: the one a retry obtained (Renewed), none when the server no longer accepts the one held.
        SealedTrainerConfiguration? envelope = result.Renewed ?? (result.EnvelopeGone ? null : pending.Envelope);
        Pending next = envelope is null || ReferenceEquals(envelope, pending.Envelope)
            ? pending with { Envelope = envelope }
            : pending with { Envelope = envelope, Deadline = Deadline(envelope), AttemptsLeft = AttemptsAllowed(envelope) };

        switch (error.Kind)
        {
            case TrainerConfigurationErrorKind.InvalidCode:
                // Wrong code: N attempts left on this envelope; at 0, the next attempt fetches a new one.
                next = next with { AttemptsLeft = error.AttemptsLeft ?? next.AttemptsLeft };
                return View(Hold(error.AttemptsLeft == 0 ? next with { Envelope = null } : next), error);

            case TrainerConfigurationErrorKind.EnvelopeExpired when error.Reason is "exhausted" or "used":
                // The envelope cannot be used again: ask for the code again, with a new envelope at the next attempt.
                return View(Hold(next with { Envelope = null, AttemptsLeft = null }), error);

            case TrainerConfigurationErrorKind.RateLimited:
            case TrainerConfigurationErrorKind.Network:
            case TrainerConfigurationErrorKind.Unavailable:
                // Nothing was used: the same envelope can be tried again (after the wait, for a 429).
                return View(Hold(next), error);

            default:
                // configChanged (the candidate reviews the new configuration), the other 410 reasons, 401, 403, an envelope that does not open after
                // an accepted proof (tampering)…: start again from the review.
                Drop();
                return Idle(error);
        }
    }

    /// <summary>A ticked kind that this answer does not let the candidate use, as an error; null when all of them are usable.</summary>
    private static TrainerConfigurationError? NotUsable(SealedTrainerConfiguration configuration, IReadOnlyList<ModelKind> kinds) =>
        configuration.Overview is { } overview && kinds.Any(kind => !overview.For(kind).CanUse)
            ? new TrainerConfigurationError(TrainerConfigurationErrorKind.InvalidResponse) { Reason = NotApplicableReason }
            : null;

    /// <summary>
    /// When the held envelope is dropped: its lifetime as the admin dashboard counts it (<c>expiresAt</c> − <c>serverTime</c>), from the local
    /// retrieval time, within [0, <see cref="MaxHold"/>]. The mock's envelopes never expire: <see cref="MaxHold"/>.
    /// </summary>
    private static DateTimeOffset Deadline(SealedTrainerConfiguration configuration)
    {
        if (configuration.ExpiresAt is not { } expiresAt)
        {
            return configuration.RetrievedAt + MaxHold;
        }

        DateTimeOffset reference = DateTimeOffset.TryParse(configuration.Payload.Admin?.Document.ServerTime, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset serverTime)
            ? serverTime
            : configuration.RetrievedAt;
        TimeSpan lifetime = expiresAt - reference;
        return configuration.RetrievedAt + (lifetime < TimeSpan.Zero ? TimeSpan.Zero : lifetime > MaxHold ? MaxHold : lifetime);
    }

    private static int? AttemptsAllowed(SealedTrainerConfiguration configuration) => configuration.Payload.Admin?.Envelope.AttemptsAllowed;

    private static string EnvelopeId(SealedTrainerConfiguration? configuration) => configuration?.Payload.Admin?.Envelope.EnvelopeId ?? "simulated";

    private static TrainerConfigurationError FormatError() =>
        new(TrainerConfigurationErrorKind.InvalidCode) { Reason = AdminEnvelopeDecryptor.InvalidCodeFormatReason };

    /// <summary>The kind overviews with "updated by the trainer since your last unlock" (Rule 17), from the trainer profiles saved at the last unlock.</summary>
    private TrainerModelsOverview WithChangeDetection(TrainerModelsOverview overview)
    {
        ModelSettingsStatus models = _settings.GetModelStatus();
        return overview with
        {
            Chat = Detect(overview.Chat, models.Chat.Trainer),
            Embedding = Detect(overview.Embedding, models.Embedding.Trainer),
        };

        static TrainerKindOverview Detect(TrainerKindOverview kind, ModelProfileStatus trainer) =>
            kind.Default is { } current && trainer.ConfigId is not null
                ? kind with { UpdatedSinceUnlock = trainer.ConfigId != current.Id || trainer.ConfigUpdatedAt != current.UpdatedAt }
                : kind;
    }

    /// <summary>The unlock under way, unless it expired (then it is dropped).</summary>
    private Pending? Current()
    {
        Pending? pending = Volatile.Read(ref _pending);
        if (pending is not null && _time.GetUtcNow() >= pending.Deadline)
        {
            Release(pending);
            return null;
        }

        return pending;
    }

    /// <summary>Holds <paramref name="pending"/> instead of the current one (at most one envelope), until its deadline at the latest. Under _gate.</summary>
    private Pending Hold(Pending pending)
    {
        _expiry?.Dispose();
        Volatile.Write(ref _pending, pending);
        TimeSpan delay = pending.Deadline - _time.GetUtcNow();
        _expiry = _time.CreateTimer(_ => Release(pending), null, delay < TimeSpan.Zero ? TimeSpan.Zero : delay, Timeout.InfiniteTimeSpan);
        return pending;
    }

    /// <summary>Drops the unlock under way, if any. True when one was held. Under _gate (or when disposing).</summary>
    private bool Drop()
    {
        _expiry?.Dispose();
        _expiry = null;
        return Interlocked.Exchange(ref _pending, null) is not null;
    }

    /// <summary>Drops <paramref name="pending"/> once its deadline is reached, unless another unlock replaced it.</summary>
    private void Release(Pending pending)
    {
        if (_time.GetUtcNow() >= pending.Deadline && Interlocked.CompareExchange(ref _pending, null, pending) == pending)
        {
            _logger.LogInformation("Trainer unlock expired: the envelope is dropped.");
        }
    }

    private TrainerUnlockView View(Pending? pending, TrainerConfigurationError? error = null)
    {
        if (pending is null)
        {
            return Idle(error);
        }

        (int length, string alphabet) = pending.Envelope is { } held ? TrainerCodeFormat.For(held.Payload) : (TrainerCodeFormat.Length, TrainerCodeFormat.Alphabet);
        return new TrainerUnlockView(TrainerUnlockView.CodeRequired, pending.Kinds, pending.Deadline, length, alphabet, pending.AttemptsLeft, error);
    }

    private static TrainerUnlockView Idle(TrainerConfigurationError? error) =>
        new(TrainerUnlockView.Idle, [], null, TrainerCodeFormat.Length, TrainerCodeFormat.Alphabet, null, error);

    /// <summary>
    /// An unlock under way: the ticked kinds and the envelope held (null: the next attempt fetches a new one). Never printed with the envelope.
    /// </summary>
    private sealed record Pending(IReadOnlyList<ModelKind> Kinds, SealedTrainerConfiguration? Envelope, DateTimeOffset Deadline, int? AttemptsLeft)
    {
        public override string ToString() => $"Pending {{ Kinds = [{string.Join(", ", Kinds)}], Deadline = {Deadline:O}, Envelope = {Envelope is not null} }}";
    }
}
