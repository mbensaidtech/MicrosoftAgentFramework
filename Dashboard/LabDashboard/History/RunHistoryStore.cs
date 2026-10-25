using System.Text.Json;
using System.Text.Json.Serialization;
using LabDashboard.Execution;

namespace LabDashboard.History;

/// <summary>
/// Local run history stored in a single JSON file (newest run first, capped per lab).
/// Writes are serialized and atomic (temp file + move), so a crash never leaves a truncated file.
/// </summary>
public sealed class RunHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _file;
    private readonly int _runsPerLab;
    private readonly ILogger<RunHistoryStore> _logger;
    private Dictionary<string, List<RunRecord>> _runs;

    public RunHistoryStore(DashboardOptions options, IHostEnvironment environment, ILogger<RunHistoryStore> logger)
    {
        string directory = Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.DataDirectory));
        Directory.CreateDirectory(directory);
        _file = Path.Combine(directory, "history.json");
        _runsPerLab = options.HistoryRunsPerLab;
        _logger = logger;
        _runs = Load();
    }

    public IReadOnlyList<RunRecord> ForLab(string labId)
    {
        lock (_runs)
        {
            return _runs.TryGetValue(labId, out List<RunRecord>? runs) ? runs.ToList() : [];
        }
    }

    public async Task AddAsync(RunRecord record)
    {
        await _gate.WaitAsync();
        try
        {
            string json;
            lock (_runs)
            {
                if (!_runs.TryGetValue(record.LabId, out List<RunRecord>? runs))
                {
                    runs = [];
                    _runs[record.LabId] = runs;
                }

                runs.Insert(0, record);
                if (runs.Count > _runsPerLab)
                {
                    runs.RemoveRange(_runsPerLab, runs.Count - _runsPerLab);
                }

                json = JsonSerializer.Serialize(_runs, JsonOptions);
            }

            await WriteAsync(json);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Removes every run of the given labs in one atomic write and returns the number of runs removed per lab.
    /// When the file cannot be written, the runs stay in memory too (the history shown never differs from the file).
    /// </summary>
    public async Task<IReadOnlyDictionary<string, int>> RemoveLabsAsync(IReadOnlyCollection<string> labIds)
    {
        await _gate.WaitAsync();
        try
        {
            Dictionary<string, int> removed = new(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, List<RunRecord>> remaining;
            string json;
            lock (_runs)
            {
                remaining = new(_runs, StringComparer.OrdinalIgnoreCase);
                foreach (string labId in labIds)
                {
                    removed[labId] = remaining.Remove(labId, out List<RunRecord>? runs) ? runs.Count : 0;
                }

                if (removed.Values.All(count => count == 0))
                {
                    return removed;
                }

                json = JsonSerializer.Serialize(remaining, JsonOptions);
            }

            await WriteAsync(json);
            lock (_runs)
            {
                foreach (string labId in labIds)
                {
                    _runs.Remove(labId);
                }
            }

            return removed;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task WriteAsync(string json)
    {
        string temp = _file + ".tmp";
        await File.WriteAllTextAsync(temp, json);
        File.Move(temp, _file, overwrite: true);
    }

    private Dictionary<string, List<RunRecord>> Load()
    {
        if (!File.Exists(_file))
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            Dictionary<string, List<RunRecord>>? runs = JsonSerializer.Deserialize<Dictionary<string, List<RunRecord>>>(File.ReadAllText(_file), JsonOptions);
            return new(runs ?? [], StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException ex)
        {
            // Keep the unreadable file for inspection and start a fresh history instead of failing to start.
            string backup = _file + ".corrupt";
            File.Copy(_file, backup, overwrite: true);
            _logger.LogWarning(ex, "Run history is unreadable; it was saved to {Backup} and reset.", backup);
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }
}
