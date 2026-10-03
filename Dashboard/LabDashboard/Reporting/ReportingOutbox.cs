using System.Text.Json;
using System.Text.Json.Nodes;

namespace LabDashboard.Reporting;

/// <summary>One event reported to the admin dashboard (the envelope of <c>POST /api/v1/events</c>).</summary>
public sealed record ReportingEvent(string EventId, string Type, DateTimeOffset OccurredAt, string? LabId, JsonNode? Payload);

/// <summary>
/// Events waiting to be delivered, in order. Kept in memory and persisted to <c>Dashboard/.data/outbox.json</c>
/// so that nothing is lost when the server is down or the dashboard is restarted. Capped: the oldest events are dropped
/// (the next progress snapshot restores a consistent state on the server).
/// </summary>
public sealed class ReportingOutbox
{
    public const int Capacity = 500;
    public const int BatchSize = 100;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = false };

    private readonly object _gate = new();
    private readonly ILogger _logger;
    private readonly List<ReportingEvent> _events;
    private bool _dirty;
    private bool _overflowing;

    public ReportingOutbox(string file, ILogger? logger = null)
    {
        File = file;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        _events = Load();
    }

    public string File { get; }

    public int Count
    {
        get { lock (_gate) { return _events.Count; } }
    }

    public void Enqueue(ReportingEvent reportingEvent)
    {
        lock (_gate)
        {
            _events.Add(reportingEvent);
            if (_events.Count > Capacity)
            {
                _events.RemoveRange(0, _events.Count - Capacity);
                if (!_overflowing)
                {
                    // One line per overflow episode, not one per dropped event.
                    _overflowing = true;
                    _logger.LogWarning("Reporting outbox is full ({Capacity} events): the oldest events are dropped until the server is reachable again.", Capacity);
                }
            }

            _dirty = true;
        }
    }

    /// <summary>The oldest events, at most <see cref="BatchSize"/>, in the order they were enqueued.</summary>
    public IReadOnlyList<ReportingEvent> PeekBatch()
    {
        lock (_gate)
        {
            return _events.Take(BatchSize).ToList();
        }
    }

    public void Remove(IEnumerable<string> eventIds)
    {
        HashSet<string> ids = eventIds.ToHashSet(StringComparer.Ordinal);
        lock (_gate)
        {
            int removed = _events.RemoveAll(e => ids.Contains(e.EventId));
            if (removed > 0)
            {
                _dirty = true;
                if (_events.Count < Capacity / 2)
                {
                    _overflowing = false;
                }
            }
        }
    }

    /// <summary>Writes the file when something changed since the last write (temporary file + move).</summary>
    public void Persist()
    {
        string json;
        lock (_gate)
        {
            if (!_dirty)
            {
                return;
            }

            json = JsonSerializer.Serialize(_events, JsonOptions);
            _dirty = false;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(File)!);
            string temporary = File + ".tmp";
            System.IO.File.WriteAllText(temporary, json);
            System.IO.File.Move(temporary, File, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            lock (_gate) { _dirty = true; }
            _logger.LogWarning(ex, "Could not write the reporting outbox {File}.", File);
        }
    }

    private List<ReportingEvent> Load()
    {
        if (!System.IO.File.Exists(File))
        {
            return [];
        }

        try
        {
            List<ReportingEvent>? events = JsonSerializer.Deserialize<List<ReportingEvent>>(System.IO.File.ReadAllText(File), JsonOptions);
            return events?.Where(e => e is { EventId.Length: > 0, Type.Length: > 0 }).TakeLast(Capacity).ToList() ?? [];
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "The reporting outbox {File} is unreadable and was reset.", File);
            return [];
        }
    }
}
