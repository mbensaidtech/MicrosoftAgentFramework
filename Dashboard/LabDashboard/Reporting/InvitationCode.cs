using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LabDashboard.Reporting;

/// <summary>The two keys of an invitation. <see cref="ToString"/> never prints them.</summary>
internal sealed record Invitation(string ApiKey, string WorkshopKey)
{
    public override string ToString() => "Invitation { ApiKey = set, WorkshopKey = set }";
}

/// <summary>
/// The invitation code the trainer e-mails to the candidates (Business Rules 21–23): <c>LB1.</c> followed by the base64url (no padding) of the UTF-8 JSON
/// <c>{"apiKey":"&lt;admin API key&gt;","workshopKey":"&lt;workshop key&gt;"}</c>, 1,024 characters at most. The candidate pastes it where the workshop
/// key is typed; a value that does not start like an invitation (<c>LB</c>, a version number, a dot) is a plain workshop key, as before.
/// Both keys are header values: visible ASCII characters, no whitespace. Nothing here logs or keeps a key.
/// </summary>
internal static partial class InvitationCode
{
    public const string Prefix = "LB1.";
    public const int MaxLength = 1024;

    // Error codes of the workshop key field (and of the trainer's command for a key).
    public const string InvalidError = "invalid";
    public const string UnsupportedVersionError = "unsupportedVersion";
    public const string RequiredError = "required";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>Whether the value is meant as an invitation (any version), rather than a plain workshop key.</summary>
    public static bool IsInvitation(string value) => VersionPrefix().IsMatch(value.TrimStart());

    /// <summary>
    /// The two keys of an invitation, or null with <paramref name="error"/>: <c>unsupportedVersion</c> for another version prefix (<c>LB2.</c>…),
    /// <c>invalid</c> for anything that cannot be decoded or lacks a usable key. Whitespace inside the code (a line wrapped by a mail client) is ignored.
    /// </summary>
    public static Invitation? Parse(string value, out string? error)
    {
        string code = string.Concat(value.Where(c => !char.IsWhiteSpace(c)));
        error = !code.StartsWith(Prefix, StringComparison.Ordinal) ? (IsInvitation(code) ? UnsupportedVersionError : InvalidError)
            : code.Length > MaxLength ? InvalidError
            : null;
        if (error is not null)
        {
            return null;
        }

        string? apiKey = null;
        string? workshopKey = null;
        try
        {
            byte[] json = Base64Url.DecodeFromChars(code.AsSpan(Prefix.Length));
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                apiKey = StringProperty(document.RootElement, "apiKey");
                workshopKey = StringProperty(document.RootElement, "workshopKey");
            }
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            // Not base64url, not UTF-8 or not JSON: an invalid code, whatever it holds.
        }

        if (ValidateKey(apiKey) is not null || ValidateKey(workshopKey) is not null)
        {
            error = InvalidError;
            return null;
        }

        return new Invitation(apiKey!, workshopKey!);
    }

    /// <summary>The invitation code of two keys (the trainer's command). Throws <see cref="ArgumentException"/> when a key is not valid (<see cref="ValidateKey"/>).</summary>
    public static string Create(string apiKey, string workshopKey)
    {
        if (ValidateKey(apiKey) is { } apiKeyError)
        {
            throw new ArgumentException($"The admin API key is {apiKeyError}.", nameof(apiKey));
        }

        if (ValidateKey(workshopKey) is { } workshopKeyError)
        {
            throw new ArgumentException($"The workshop key is {workshopKeyError}.", nameof(workshopKey));
        }

        string code = Prefix + Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(new { apiKey, workshopKey }, JsonOptions));
        return code.Length <= MaxLength ? code : throw new ArgumentException($"The keys are too long: an invitation code has {MaxLength} characters at most.");
    }

    /// <summary>Null for a usable key; <c>required</c> when empty, <c>invalid</c> when it holds whitespace or anything but visible ASCII characters.</summary>
    public static string? ValidateKey(string? key) =>
        string.IsNullOrEmpty(key) ? RequiredError
        : key.All(c => c is > ' ' and <= '~') ? null
        : InvalidError;

    private static string? StringProperty(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    [GeneratedRegex(@"^LB[0-9]+\.", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPrefix();
}
