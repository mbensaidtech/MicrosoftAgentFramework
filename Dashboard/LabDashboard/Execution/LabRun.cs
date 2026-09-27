using System.Diagnostics;
using System.Runtime.CompilerServices;
using LabDashboard.Catalog;

namespace LabDashboard.Execution;

/// <summary>
/// A lab run in progress or finished. Keeps every event so that a browser connecting late (or reconnecting)
/// replays the whole run before receiving the live events.
/// </summary>
public sealed class LabRun
{
    private readonly object _gate = new();
    private readonly List<RunEvent> _events = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TaskCompletionSource _changed = NewSignal();

    public LabRun(LabDefinition lab, RunTarget target)
    {
        Lab = lab;
        Target = target;
    }

    public string Id { get; } = Guid.NewGuid().ToString("N")[..12];

    public LabDefinition Lab { get; }

    public RunTarget Target { get; }

    public DateTimeOffset StartedAt { get; } = DateTimeOffset.Now;

    public CancellationTokenSource Cancellation { get; } = new();

    public bool CancelRequested { get; private set; }

    public RunRecord? Result { get; private set; }

    /// <summary>Standard input of an interactive lab (null for the other labs, and until the run starts).</summary>
    public RunInput? Input { get; set; }

    /// <summary>Set when the run timed out because nobody answered a prompt.</summary>
    public bool InputTimedOut { get; set; }

    public bool IsCompleted => Result is not null;

    public TimeSpan Elapsed => _clock.Elapsed;

    public void RequestCancel()
    {
        CancelRequested = true;
        Cancellation.Cancel();
    }

    public void Publish(RunEvent runEvent)
    {
        lock (_gate)
        {
            if (Result is not null)
            {
                return;
            }

            _events.Add(runEvent);
            Signal();
        }
    }

    public RunEvent CreateEvent(string type) => new(type, Math.Round(_clock.Elapsed.TotalSeconds, 2));

    public void Complete(RunRecord result)
    {
        lock (_gate)
        {
            _events.Add(CreateEvent("result") with { Result = result });
            Result = result;
            Signal();
        }
    }

    public async IAsyncEnumerable<RunEvent> ReadEventsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        int next = 0;
        while (true)
        {
            RunEvent[] batch;
            bool completed;
            Task changed;
            lock (_gate)
            {
                batch = _events.GetRange(next, _events.Count - next).ToArray();
                next = _events.Count;
                completed = Result is not null;
                changed = _changed.Task;
            }

            foreach (RunEvent runEvent in batch)
            {
                yield return runEvent;
            }

            if (completed)
            {
                yield break;
            }

            await changed.WaitAsync(cancellationToken);
        }
    }

    private void Signal()
    {
        TaskCompletionSource previous = _changed;
        _changed = NewSignal();
        previous.TrySetResult();
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
