using System.Buffers;
using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LabDashboard.Reporting;

namespace LabDashboard.TrainerModels;

/// <summary>
/// The unlock key released once by <c>POST /api/v1/model-configs/unlock</c> (32 bytes), held as bytes the caller wipes with
/// <see cref="Dispose"/> as soon as it has derived the envelope key (Rule 18). It never exists as a string in this process,
/// and <see cref="ToString"/> never prints it.
/// </summary>
internal sealed class AdminUnlockKey : IDisposable
{
    private readonly byte[] _key;

    /// <summary>Takes ownership of <paramref name="key"/>: it is wiped by <see cref="Dispose"/>.</summary>
    internal AdminUnlockKey(string envelopeId, byte[] key)
    {
        EnvelopeId = envelopeId;
        _key = key;
    }

    /// <summary>The envelope the key was released for (checked against the request).</summary>
    public string EnvelopeId { get; }

    public bool IsWiped { get; private set; }

    /// <summary>The key itself. Throws once wiped.</summary>
    internal ReadOnlySpan<byte> Bytes
    {
        get
        {
            ObjectDisposedException.ThrowIf(IsWiped, this);
            return _key;
        }
    }

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(_key);
        IsWiped = true;
    }

    public override string ToString() => $"AdminUnlockKey {{ EnvelopeId = {EnvelopeId}, Key = redacted }}";
}

/// <summary>
/// The typed client of the Admin API contract v1 for the AI model configurations, server to server only: the overview, the sealed
/// retrieval and the raw unlock call. It is a protocol layer: no cryptography (T11), no decision about what to show (the overview model).
/// <list type="bullet">
/// <item>Every call sends <c>X-API-Key</c> (the reporting's resolution) and <c>Authorization: Bearer &lt;dev token&gt;</c>, read from the
/// identity at each call. Without an identity or a dev token: <see cref="TrainerConfigurationErrorKind.NotConfigured"/> (<c>workshop</c>), no call.</item>
/// <item>HTTPS only, except <c>http://localhost</c> / loopback for a local admin; redirects are not followed (a custom header would follow them).</item>
/// <item>Every documented answer becomes a result with the error kinds of Rule 15, never an exception; only the caller's cancellation throws.</item>
/// <item>No retry, ever (Rule 19). After a 429, the routes under that limit (every GET; the sealed GET only for <c>tooManyEnvelopes</c>; unlock)
/// answer <see cref="TrainerConfigurationErrorKind.RateLimited"/> locally until <c>Retry-After</c> is over, without calling the server.</item>
/// <item>The log gets the operation, the HTTP status, the error code, the reason and the envelope id; never a body, the proof or the unlock key.</item>
/// </list>
/// </summary>
internal sealed class AdminModelConfigsClient : IDisposable
{
    /// <summary>A larger answer is not the contract's: <see cref="TrainerConfigurationErrorKind.InvalidResponse"/>.</summary>
    public const int MaxResponseBytes = 1024 * 1024;

    /// <summary>The proof is an HMAC-SHA256 (Rule 18): 32 bytes, 43 base64url characters on the wire.</summary>
    public const int ProofLength = 32;

    /// <summary>The unlock key: 43 base64url characters, 32 bytes.</summary>
    public const int UnlockKeyLength = 32;

    /// <summary>
    /// <c>details.reason</c> of a 429 on the sealed GET: more than 5 envelopes per minute. The other reasons of contract v1 are <c>lockedOut</c> and
    /// <c>accessCodeFrozen</c> (unlock); any other identifier is kept as received (forward compatibility).
    /// </summary>
    public const string TooManyEnvelopesReason = "tooManyEnvelopes";

    private const int UnlockKeyTextLength = 43;

    private readonly Uri _baseUri;
    private readonly Func<string?> _apiKey;
    private readonly Func<DevIdentity?> _identity;
    private readonly ILogger _logger;
    private readonly TimeSpan _timeout;
    private readonly TimeProvider _time;
    private readonly HttpClient _http;
    private readonly object _gate = new();
    private readonly Dictionary<RateLimitScope, (DateTimeOffset Until, string? Reason)> _rateLimited = [];

    public AdminModelConfigsClient(
        Uri serverUrl,
        string apiKey,
        Func<DevIdentity?> identity,
        ILogger logger,
        TimeSpan timeout,
        TimeProvider? time = null,
        HttpMessageHandler? handler = null)
        : this(serverUrl, FixedApiKey(apiKey), identity, logger, timeout, time, handler)
    {
    }

    /// <summary>
    /// The admin API key read at each call (Business Rule 22: an invitation entered in the settings applies without a restart).
    /// While it returns null, every call answers <see cref="TrainerConfigurationErrorKind.NotConfigured"/> (<c>apiKey</c>) without any request.
    /// </summary>
    public AdminModelConfigsClient(
        Uri serverUrl,
        Func<string?> apiKey,
        Func<DevIdentity?> identity,
        ILogger logger,
        TimeSpan timeout,
        TimeProvider? time = null,
        HttpMessageHandler? handler = null)
    {
        ArgumentNullException.ThrowIfNull(serverUrl);
        ArgumentNullException.ThrowIfNull(apiKey);
        if (!IsAllowedServerUrl(serverUrl))
        {
            throw new ArgumentException("The admin dashboard URL must be HTTPS (http:// only for localhost).", nameof(serverUrl));
        }

        _baseUri = new Uri(serverUrl.AbsoluteUri.TrimEnd('/') + "/");
        _apiKey = apiKey;
        _identity = identity;
        _logger = logger;
        _timeout = timeout > TimeSpan.Zero ? timeout : TimeSpan.FromSeconds(10);
        _time = time ?? TimeProvider.System;

        // The timeout is applied per call, body included (ResponseHeadersRead), so the client itself has none.
        _http = new HttpClient(handler ?? new SocketsHttpHandler { AllowAutoRedirect = false }, disposeHandler: handler is null)
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        _http.DefaultRequestHeaders.UserAgent.TryParseAdd($"LabDashboard/{ReportingClient.DashboardVersion}");
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private enum Operation
    {
        Overview,
        Sealed,
        Unlock,
    }

    /// <summary>
    /// The rate limits of the contract, per developer: 30 GET per minute (<see cref="Reads"/>: the overview and the sealed GET), 5 envelopes per
    /// minute (<see cref="Envelopes"/>: the sealed GET only, 429 <c>tooManyEnvelopes</c>), 20 unlock per minute and the code lockouts (<see cref="Unlock"/>).
    /// </summary>
    private enum RateLimitScope
    {
        Reads,
        Envelopes,
        Unlock,
    }

    /// <summary>HTTPS, or HTTP on a loopback host (<c>localhost</c>, <c>127.0.0.1</c>, <c>[::1]</c>) for a local admin; no user info, query or fragment.</summary>
    public static bool IsAllowedServerUrl(string? url) => Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && IsAllowedServerUrl(uri);

    public static bool IsAllowedServerUrl(Uri uri) =>
        uri.IsAbsoluteUri
        && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback);

    /// <summary><c>GET /api/v1/model-configs</c>: the configurations and the defaults, never a key. Called on the AI models tab or Refresh only.</summary>
    public async Task<TrainerOutcome<AdminModelConfigs>> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        Response? response = null;
        try
        {
            (response, TrainerConfigurationError? failure, _) = await SendAsync(Operation.Overview, HttpMethod.Get, AdminModelConfigsContract.OverviewPath, null, null, cancellationToken);
            if (failure is not null)
            {
                return TrainerOutcome<AdminModelConfigs>.Failure(failure);
            }

            AdminModelConfigs? document = AdminModelConfigsParser.Parse(response!.Body, withEnvelope: false);
            if (document is null)
            {
                return TrainerOutcome<AdminModelConfigs>.Failure(InvalidBody(Operation.Overview, response));
            }

            _logger.LogDebug("Admin model configurations: overview received ({Count} configurations).", document.Configs.Count);
            return TrainerOutcome<AdminModelConfigs>.Success(document with { Sealed = null });
        }
        finally
        {
            response?.Wipe();
        }
    }

    /// <summary>
    /// <c>GET /api/v1/model-configs?sealed=1</c>, on Unlock only: the envelope with the configurations, bound to the identity's <c>userId</c>
    /// (lowercase); or <see cref="TrainerConfigurationErrorKind.SealedUnavailable"/> with the <c>sealedUnavailableReason</c>.
    /// </summary>
    public async Task<TrainerOutcome<AdminSealedConfiguration>> GetSealedAsync(CancellationToken cancellationToken = default)
    {
        Response? response = null;
        try
        {
            (response, TrainerConfigurationError? failure, DevIdentity? identity) =
                await SendAsync(Operation.Sealed, HttpMethod.Get, AdminModelConfigsContract.SealedPath, null, null, cancellationToken);
            if (failure is not null)
            {
                return TrainerOutcome<AdminSealedConfiguration>.Failure(failure);
            }

            AdminModelConfigs? document = AdminModelConfigsParser.Parse(response!.Body, withEnvelope: true);
            if (document is null)
            {
                return TrainerOutcome<AdminSealedConfiguration>.Failure(InvalidBody(Operation.Sealed, response));
            }

            if (document.Sealed is not { } envelope)
            {
                string reason = AdminModelConfigsContract.Code(document.SealedUnavailableReason) ?? "unavailable";
                _logger.LogInformation("Admin model configurations: no envelope ({Reason}).", reason);
                return TrainerOutcome<AdminSealedConfiguration>.Failure(new TrainerConfigurationError(TrainerConfigurationErrorKind.SealedUnavailable) { Reason = reason });
            }

            _logger.LogInformation("Admin model configurations: envelope {EnvelopeId} received, valid until {ExpiresAt}.", envelope.EnvelopeId, envelope.ExpiresAt);
            return TrainerOutcome<AdminSealedConfiguration>.Success(new AdminSealedConfiguration(document, DevId(identity!)));
        }
        finally
        {
            response?.Wipe();
        }
    }

    /// <summary>
    /// <c>POST /api/v1/model-configs/unlock</c> with exactly <c>{ envelopeId, proof }</c>. <paramref name="proof"/>: the 32 bytes of the HMAC of
    /// Rule 18, sent as base64url; the request body is wiped once sent. Success: the unlock key, as bytes the caller wipes. A wrong code is
    /// <see cref="TrainerConfigurationErrorKind.InvalidCode"/> with the attempts left; a dead envelope <see cref="TrainerConfigurationErrorKind.EnvelopeExpired"/>.
    /// A malformed <paramref name="envelopeId"/> or <paramref name="proof"/> is a programming error: <see cref="ArgumentException"/>, nothing sent.
    /// </summary>
    public async Task<TrainerOutcome<AdminUnlockKey>> UnlockAsync(string envelopeId, ReadOnlyMemory<byte> proof, CancellationToken cancellationToken = default)
    {
        if (!AdminModelConfigsContract.IsUuid(envelopeId))
        {
            throw new ArgumentException("The envelope id is not a UUID.", nameof(envelopeId));
        }

        if (proof.Length != ProofLength)
        {
            throw new ArgumentException($"The proof must be {ProofLength} bytes.", nameof(proof));
        }

        Response? response = null;
        byte[] body = UnlockBody(envelopeId, proof.Span);
        try
        {
            (response, TrainerConfigurationError? failure, _) =
                await SendAsync(Operation.Unlock, HttpMethod.Post, AdminModelConfigsContract.UnlockPath, body, envelopeId, cancellationToken);
            if (failure is not null)
            {
                return TrainerOutcome<AdminUnlockKey>.Failure(failure);
            }

            AdminUnlockKey? key = ReadUnlockKey(response!.Body.Span, envelopeId);
            if (key is null)
            {
                return TrainerOutcome<AdminUnlockKey>.Failure(InvalidBody(Operation.Unlock, response, envelopeId));
            }

            _logger.LogInformation("Admin model configurations: unlock key released for envelope {EnvelopeId}.", envelopeId);
            return TrainerOutcome<AdminUnlockKey>.Success(key);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(body);
            response?.Wipe();
        }
    }

    public void Dispose() => _http.Dispose();

    // ---------- exchange ----------

    /// <summary>
    /// One call: identity, local rate limit, request, answer. A failure (no identity, rate limited, network, any status but 200) is returned
    /// already mapped and logged; a 200 is returned with its body for the operation to read.
    /// </summary>
    private async Task<(Response? Response, TrainerConfigurationError? Failure, DevIdentity? Identity)> SendAsync(
        Operation operation, HttpMethod method, string path, byte[]? body, string? envelopeId, CancellationToken cancellationToken)
    {
        DevIdentity? identity = _identity();
        if (identity is not { UserId.Length: > 0 } || string.IsNullOrWhiteSpace(identity.DevToken))
        {
            return (null, new TrainerConfigurationError(TrainerConfigurationErrorKind.NotConfigured, [TrainerConfigurationService.WorkshopSetting]), null);
        }

        if (_apiKey()?.Trim() is not { Length: > 0 } apiKey)
        {
            return (null, new TrainerConfigurationError(TrainerConfigurationErrorKind.NotConfigured, [TrainerConfigurationService.ApiKeySetting]), identity);
        }

        if (StillRateLimited(operation) is { } waiting)
        {
            return (null, waiting, identity);
        }

        using HttpRequestMessage request = new(method, new Uri(_baseUri, path));
        request.Headers.TryAddWithoutValidation(ReportingClient.ApiKeyHeader, apiKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", identity.DevToken.Trim());
        if (body is not null)
        {
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        }

        Response? response = null;
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        try
        {
            using HttpResponseMessage message = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            response = await ReadAsync(message, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (null, NetworkFailure(operation, envelopeId, "timeout"), identity);
        }
        catch (HttpRequestException ex)
        {
            return (null, NetworkFailure(operation, envelopeId, ex.HttpRequestError.ToString()), identity);
        }
        catch (IOException)
        {
            return (null, NetworkFailure(operation, envelopeId, "io"), identity);
        }

        if (response.TooLarge)
        {
            return (null, InvalidBody(operation, response, envelopeId), identity);
        }

        if (response.Status == HttpStatusCode.OK)
        {
            return (response, null, identity);
        }

        try
        {
            return (null, Failure(operation, response, envelopeId), identity);
        }
        finally
        {
            response.Wipe();
        }
    }

    /// <summary>Maps a documented error answer (or any status but 200) to its error kind, logs it, and remembers a 429.</summary>
    private TrainerConfigurationError Failure(Operation operation, Response response, string? envelopeId)
    {
        ErrorBody body = ErrorBody.Read(response.Body.Span);
        int status = (int)response.Status;
        TrainerConfigurationError error = status switch
        {
            400 => new TrainerConfigurationError(TrainerConfigurationErrorKind.Unknown) { Reason = body.Error ?? "validation" },
            401 when body.Error == "invalidAccessCode" => new TrainerConfigurationError(TrainerConfigurationErrorKind.InvalidCode) { AttemptsLeft = body.AttemptsLeft },
            401 => new TrainerConfigurationError(TrainerConfigurationErrorKind.Unauthorized)
            {
                Credential = body.Error switch
                {
                    "invalidApiKey" => TrainerConfigurationError.ApiKeyCredential,
                    "unauthorized" => TrainerConfigurationError.DevTokenCredential,
                    // No usable body: the contract sends WWW-Authenticate with invalidApiKey only.
                    _ => response.WwwAuthenticate ? TrainerConfigurationError.ApiKeyCredential : null,
                },
            },
            403 => new TrainerConfigurationError(TrainerConfigurationErrorKind.Forbidden),
            404 => new TrainerConfigurationError(TrainerConfigurationErrorKind.NotFound),
            410 when operation == Operation.Unlock => new TrainerConfigurationError(TrainerConfigurationErrorKind.EnvelopeExpired) { Reason = body.Reason },
            429 => new TrainerConfigurationError(TrainerConfigurationErrorKind.RateLimited)
            {
                RetryAfterSeconds = Max(response.RetryAfterSeconds, body.RetryAfterSeconds),
                Reason = body.Reason,
            },
            500 => new TrainerConfigurationError(TrainerConfigurationErrorKind.Unavailable) { Reason = body.Error ?? "internal" },
            503 => new TrainerConfigurationError(TrainerConfigurationErrorKind.Unavailable) { Reason = body.Error ?? "unavailable" },
            >= 500 and <= 599 => new TrainerConfigurationError(TrainerConfigurationErrorKind.Unavailable) { Reason = body.Error },
            _ => new TrainerConfigurationError(TrainerConfigurationErrorKind.Unknown) { Reason = body.Error },
        };
        error = error with { HttpStatus = status };

        if (error.Kind == TrainerConfigurationErrorKind.RateLimited && error.RetryAfterSeconds is int seconds && seconds > 0)
        {
            lock (_gate)
            {
                // tooManyEnvelopes only holds back the next envelope: the overview stays available. Any other GET 429 is the shared GET limit.
                RateLimitScope scope = operation == Operation.Unlock ? RateLimitScope.Unlock
                    : operation == Operation.Sealed && error.Reason == TooManyEnvelopesReason ? RateLimitScope.Envelopes
                    : RateLimitScope.Reads;
                _rateLimited[scope] = (_time.GetUtcNow().AddSeconds(seconds), error.Reason);
            }
        }

        // Codes only: the server's message is free text, and the bodies of the unlock call are never logged.
        _logger.LogWarning(
            "Admin model configurations: {Operation}{Envelope} answered HTTP {Status} (error {Error}, reason {Reason}, field {Field}): {Kind}.",
            Name(operation), EnvelopeText(envelopeId), status, body.Error ?? "none", body.Reason ?? "none", body.Field ?? "none", error.Kind);
        return error;
    }

    /// <summary>The longest wait still running among the limits that apply to <paramref name="operation"/>; null when it can be called.</summary>
    private TrainerConfigurationError? StillRateLimited(Operation operation)
    {
        RateLimitScope[] scopes = operation switch
        {
            Operation.Overview => [RateLimitScope.Reads],
            Operation.Sealed => [RateLimitScope.Reads, RateLimitScope.Envelopes],
            _ => [RateLimitScope.Unlock],
        };

        lock (_gate)
        {
            DateTimeOffset now = _time.GetUtcNow();
            (DateTimeOffset Until, string? Reason)? longest = null;
            foreach (RateLimitScope scope in scopes)
            {
                if (!_rateLimited.TryGetValue(scope, out (DateTimeOffset Until, string? Reason) limit))
                {
                    continue;
                }

                if (limit.Until <= now)
                {
                    _rateLimited.Remove(scope);
                }
                else if (longest is null || limit.Until > longest.Value.Until)
                {
                    longest = limit;
                }
            }

            return longest is { } wait
                ? new TrainerConfigurationError(TrainerConfigurationErrorKind.RateLimited)
                {
                    RetryAfterSeconds = (int)Math.Ceiling((wait.Until - now).TotalSeconds),
                    Reason = wait.Reason,
                }
                : null;
        }
    }

    private TrainerConfigurationError NetworkFailure(Operation operation, string? envelopeId, string cause)
    {
        _logger.LogWarning("Admin model configurations: {Operation}{Envelope} failed ({Cause}): network.", Name(operation), EnvelopeText(envelopeId), cause);
        return new TrainerConfigurationError(TrainerConfigurationErrorKind.Network);
    }

    private TrainerConfigurationError InvalidBody(Operation operation, Response response, string? envelopeId = null)
    {
        _logger.LogWarning("Admin model configurations: {Operation}{Envelope} answered HTTP {Status} with a body that is not the contract's: invalidResponse.",
            Name(operation), EnvelopeText(envelopeId), (int)response.Status);
        return new TrainerConfigurationError(TrainerConfigurationErrorKind.InvalidResponse) { HttpStatus = (int)response.Status };
    }

    private static Func<string?> FixedApiKey(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        string key = apiKey.Trim();
        return () => key;
    }

    private static string Name(Operation operation) => operation switch
    {
        Operation.Overview => "overview",
        Operation.Sealed => "sealed retrieval",
        _ => "unlock",
    };

    private static string EnvelopeText(string? envelopeId) => envelopeId is null ? "" : $" of envelope {envelopeId}";

    private static int? Max(int? first, int? second) => first is null ? second : second is null ? first : Math.Max(first.Value, second.Value);

    /// <summary>The <c>devId</c> of the protocol: the identity's <c>userId</c>, lowercase as registered (Rule 16).</summary>
    private static string DevId(DevIdentity identity) => identity.UserId.Trim().ToLowerInvariant();

    // ---------- bodies ----------

    /// <summary>
    /// <c>{"envelopeId":"&lt;uuid&gt;","proof":"&lt;43 base64url characters&gt;"}</c>, written straight into one array that the caller wipes:
    /// the proof never exists as a string. The envelope id is a checked UUID, so it needs no JSON escaping.
    /// </summary>
    private static byte[] UnlockBody(string envelopeId, ReadOnlySpan<byte> proof)
    {
        ReadOnlySpan<byte> head = "{\"envelopeId\":\""u8;
        ReadOnlySpan<byte> middle = "\",\"proof\":\""u8;
        ReadOnlySpan<byte> tail = "\"}"u8;
        int proofLength = Base64Url.GetEncodedLength(proof.Length);
        byte[] body = new byte[head.Length + envelopeId.Length + middle.Length + proofLength + tail.Length];

        Span<byte> span = body;
        head.CopyTo(span);
        span = span[head.Length..];
        span = span[Encoding.ASCII.GetBytes(envelopeId, span)..];
        middle.CopyTo(span);
        span = span[middle.Length..];
        span = span[Base64Url.EncodeToUtf8(proof, span)..];
        tail.CopyTo(span);
        return body;
    }

    /// <summary>
    /// Reads <c>{ envelopeId, unlockKey }</c> without ever making a string of the key: 43 base64url characters decoded straight into 32 bytes.
    /// Null when the body is not that, or names another envelope.
    /// </summary>
    private static AdminUnlockKey? ReadUnlockKey(ReadOnlySpan<byte> json, string envelopeId)
    {
        byte[]? key = null;
        string? receivedEnvelopeId = null;
        try
        {
            Utf8JsonReader reader = new(json, new JsonReaderOptions { MaxDepth = 32 });
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return null;
            }

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    return Discard(ref key);
                }

                if (reader.ValueTextEquals("unlockKey"u8))
                {
                    if (!reader.Read() || reader.TokenType != JsonTokenType.String || key is not null || (key = DecodeUnlockKey(ref reader)) is null)
                    {
                        return Discard(ref key);
                    }
                }
                else if (reader.ValueTextEquals("envelopeId"u8))
                {
                    if (!reader.Read() || reader.TokenType != JsonTokenType.String)
                    {
                        return Discard(ref key);
                    }

                    receivedEnvelopeId = reader.GetString();
                }
                else
                {
                    // Unknown properties are ignored (forward compatibility).
                    reader.Read();
                    reader.Skip();
                }
            }

            if (reader.TokenType != JsonTokenType.EndObject || reader.Read())
            {
                return Discard(ref key);
            }
        }
        catch (JsonException)
        {
            return Discard(ref key);
        }

        return key is not null && string.Equals(receivedEnvelopeId, envelopeId, StringComparison.OrdinalIgnoreCase)
            ? new AdminUnlockKey(envelopeId, key)
            : Discard(ref key);
    }

    private static AdminUnlockKey? Discard(ref byte[]? key)
    {
        if (key is not null)
        {
            CryptographicOperations.ZeroMemory(key);
            key = null;
        }

        return null;
    }

    private static byte[]? DecodeUnlockKey(ref Utf8JsonReader reader)
    {
        Span<byte> text = stackalloc byte[64];
        try
        {
            ReadOnlySpan<byte> raw = reader.ValueSpan;
            int length;
            if (reader.ValueIsEscaped)
            {
                // Unescaping never makes a value longer.
                if (raw.Length > text.Length)
                {
                    return null;
                }

                length = reader.CopyString(text);
            }
            else
            {
                if (raw.Length != UnlockKeyTextLength)
                {
                    return null;
                }

                raw.CopyTo(text);
                length = raw.Length;
            }

            if (length != UnlockKeyTextLength)
            {
                return null;
            }

            byte[] key = new byte[UnlockKeyLength];
            OperationStatus status = Base64Url.DecodeFromUtf8(text[..length], key, out int consumed, out int written);
            if (status == OperationStatus.Done && consumed == length && written == UnlockKeyLength)
            {
                return key;
            }

            CryptographicOperations.ZeroMemory(key);
            return null;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(text);
        }
    }

    /// <summary>Reads the answer: status, the headers the mapping needs, and the body, at most <see cref="MaxResponseBytes"/>.</summary>
    private async Task<Response> ReadAsync(HttpResponseMessage message, CancellationToken cancellationToken)
    {
        RetryConditionHeaderValue? retryAfter = message.Headers.RetryAfter;
        TimeSpan? delay = retryAfter?.Delta ?? (retryAfter?.Date is { } date ? date - _time.GetUtcNow() : null);
        int? retryAfterSeconds = delay is { } wait ? (int)Math.Ceiling(Math.Max(0, wait.TotalSeconds)) : null;
        bool wwwAuthenticate = message.Headers.WwwAuthenticate.Count > 0;

        if (message.Content.Headers.ContentLength is > MaxResponseBytes)
        {
            return new Response(message.StatusCode, [], 0, TooLarge: true, retryAfterSeconds, wwwAuthenticate);
        }

        long? declared = message.Content.Headers.ContentLength;
        byte[] buffer = new byte[declared is > 0 ? (int)declared.Value + 1 : 4096];
        int length = 0;
        try
        {
            await using Stream stream = await message.Content.ReadAsStreamAsync(cancellationToken);
            while (true)
            {
                if (length == buffer.Length)
                {
                    if (buffer.Length > MaxResponseBytes)
                    {
                        CryptographicOperations.ZeroMemory(buffer);
                        return new Response(message.StatusCode, [], 0, TooLarge: true, retryAfterSeconds, wwwAuthenticate);
                    }

                    // Growing never leaves a copy of the body behind (the unlock answer holds the key).
                    byte[] larger = new byte[Math.Min(buffer.Length * 2, MaxResponseBytes + 1)];
                    buffer.CopyTo(larger, 0);
                    CryptographicOperations.ZeroMemory(buffer);
                    buffer = larger;
                }

                int read = await stream.ReadAsync(buffer.AsMemory(length), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                length += read;
            }
        }
        catch
        {
            CryptographicOperations.ZeroMemory(buffer);
            throw;
        }

        if (length > MaxResponseBytes)
        {
            CryptographicOperations.ZeroMemory(buffer);
            return new Response(message.StatusCode, [], 0, TooLarge: true, retryAfterSeconds, wwwAuthenticate);
        }

        return new Response(message.StatusCode, buffer, length, TooLarge: false, retryAfterSeconds, wwwAuthenticate);
    }

    /// <summary>An answer of the admin API. <see cref="Wipe"/> clears the body: the unlock answer holds the key.</summary>
    private sealed record Response(HttpStatusCode Status, byte[] Buffer, int Length, bool TooLarge, int? RetryAfterSeconds, bool WwwAuthenticate)
    {
        public ReadOnlyMemory<byte> Body => Buffer.AsMemory(0, Length);

        public void Wipe() => CryptographicOperations.ZeroMemory(Buffer);

        public override string ToString() => $"Response {{ Status = {(int)Status} }}";
    }

    /// <summary>The error body <c>{ error, message, details? }</c>: only the codes and numbers; <c>message</c> is never read.</summary>
    private readonly record struct ErrorBody(string? Error, string? Reason, string? Field, int? AttemptsLeft, int? RetryAfterSeconds)
    {
        public static ErrorBody Read(ReadOnlySpan<byte> json)
        {
            if (json.IsEmpty)
            {
                return default;
            }

            try
            {
                Utf8JsonReader reader = new(json, new JsonReaderOptions { MaxDepth = 32 });
                using JsonDocument document = JsonDocument.ParseValue(ref reader);
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return default;
                }

                JsonElement details = root.TryGetProperty("details", out JsonElement value) && value.ValueKind == JsonValueKind.Object ? value : default;
                return new ErrorBody(
                    Code(root, "error"),
                    Code(details, "reason"),
                    Code(details, "field"),
                    Number(details, "attemptsLeft") is { } attempts ? Math.Clamp(attempts, 0, 1000) : null,
                    Number(details, "retryAfterSeconds") is { } seconds ? Math.Max(0, seconds) : null);
            }
            catch (JsonException)
            {
                return default;
            }
        }

        private static string? Code(JsonElement element, string name) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? AdminModelConfigsContract.Code(value.GetString())
                : null;

        private static int? Number(JsonElement element, string name) =>
            element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number
            && value.TryGetDouble(out double number) && number is >= int.MinValue and <= int.MaxValue
                ? (int)Math.Ceiling(number)
                : null;
    }
}
