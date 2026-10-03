using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace LabDashboard.Reporting;

/// <summary>
/// The identity of the developer on the admin dashboard: a stable <see cref="UserId"/> generated once by this dashboard,
/// the display <see cref="Username"/>, and the token returned by the server at registration (the dev token).
/// </summary>
public sealed record DevIdentity
{
    public required string UserId { get; init; }
    public required string Username { get; init; }

    /// <summary>Secret returned by the server; proves that the sender owns <see cref="UserId"/>. Never shown in the UI.</summary>
    public string? DevToken { get; init; }

    public DateTimeOffset? RegisteredAt { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
}

/// <summary>
/// Reads and writes <c>Dashboard/.data/identity.json</c> (git-ignored). Deleting the file creates a new developer on the server.
/// The file holds the dev token, so it is private to the user; writes are atomic (temporary file + move).
/// </summary>
public sealed partial class IdentityStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly object _gate = new();
    private readonly ILogger _logger;
    private DevIdentity? _current;

    public IdentityStore(string file, ILogger? logger = null)
    {
        File = file;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        _current = Load();
    }

    public string File { get; }

    /// <summary>Marker written when the developer chose to work outside a workshop: the welcome dialog is not shown again.</summary>
    public string StandaloneFile => Path.Combine(Path.GetDirectoryName(File)!, "standalone");

    /// <summary>True when the developer skipped the workshop at the first launch (and has not joined one since).</summary>
    public bool Standalone => Current is null && System.IO.File.Exists(StandaloneFile);

    /// <summary>Records the "work on my own" choice. Joining a workshop later (<see cref="Create"/>) removes it.</summary>
    public void MarkStandalone()
    {
        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StandaloneFile)!);
            System.IO.File.WriteAllText(StandaloneFile, "The developer chose to work outside a workshop. Delete this file to see the welcome dialog again.\n");
        }
    }

    public DevIdentity? Current
    {
        get { lock (_gate) { return _current; } }
    }

    public static IdentityStore ForOptions(DashboardOptions options, IHostEnvironment environment, ILogger<IdentityStore> logger)
    {
        string directory = Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.DataDirectory));
        return new IdentityStore(Path.Combine(directory, "identity.json"), logger);
    }

    /// <summary>Creates the identity: a new UUID v4 and the username chosen by the developer.</summary>
    public DevIdentity Create(string username)
    {
        lock (_gate)
        {
            DevIdentity identity = new() { UserId = Guid.NewGuid().ToString("D"), Username = username };
            Save(identity);
            try { System.IO.File.Delete(StandaloneFile); } catch (IOException) { }
            return identity;
        }
    }

    /// <summary>Applies a change and saves it. Returns null (and changes nothing) when there is no identity yet.</summary>
    public DevIdentity? Update(Func<DevIdentity, DevIdentity> change)
    {
        lock (_gate)
        {
            if (_current is null)
            {
                return null;
            }

            DevIdentity updated = change(_current);
            if (updated != _current)
            {
                Save(updated);
            }

            return updated;
        }
    }

    /// <summary>Username rules of the admin API: 2–32 characters, letters, digits, '.', '_' and '-'. Codes: required, tooShort, tooLong, invalid.</summary>
    public static string? ValidateUsername(string? username)
    {
        string value = username?.Trim() ?? "";
        return value.Length == 0 ? "required"
            : value.Length < 2 ? "tooShort"
            : value.Length > 32 ? "tooLong"
            : UsernamePattern().IsMatch(value) ? null
            : "invalid";
    }

    [GeneratedRegex("^[A-Za-z0-9._-]{2,32}$")]
    private static partial Regex UsernamePattern();

    private DevIdentity? Load()
    {
        if (!System.IO.File.Exists(File))
        {
            return null;
        }

        try
        {
            DevIdentity? identity = JsonSerializer.Deserialize<DevIdentity>(System.IO.File.ReadAllText(File), JsonOptions);
            return identity is { UserId.Length: > 0, Username.Length: > 0 } ? identity : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Keep the unreadable file for inspection; the developer is asked for a username again.
            string backup = File + ".corrupt";
            try { System.IO.File.Copy(File, backup, overwrite: true); } catch (IOException) { }
            _logger.LogWarning(ex, "The identity file is unreadable; it was saved to {Backup} and a new identity will be created.", backup);
            return null;
        }
    }

    /// <summary>Atomic write; the file is created private to the user (600) on macOS/Linux because it holds the dev token.</summary>
    private void Save(DevIdentity identity)
    {
        string directory = Path.GetDirectoryName(File)!;
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $".identity.{Guid.NewGuid():N}.tmp");
        FileStreamOptions options = new() { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        try
        {
            using (FileStream stream = new(temporary, options))
            {
                JsonSerializer.Serialize(stream, identity, JsonOptions);
            }

            System.IO.File.Move(temporary, File, overwrite: true);
            _current = identity;
        }
        catch
        {
            try { System.IO.File.Delete(temporary); } catch (IOException) { }
            throw;
        }
    }
}
