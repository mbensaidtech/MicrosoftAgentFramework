using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using CommonUtilities;

namespace OrchestrationConcurrent;

// ============================================
// PROVIDED - do not edit
// ============================================

/// <summary>
/// Console output helpers shared by the scenarios, so that Program.cs focuses on the workflow: how it is built, run and observed.
/// </summary>
public static partial class WorkflowConsole
{
    /// <summary>
    /// Displays the graph of a workflow level by level, from its start executor along its edges: the executors of a level
    /// receive their messages at the same time (a fan-out), and are written between brackets.
    /// In a concurrent workflow: <c>Start</c>, then one executor per agent, then one batcher per agent (it collects the messages
    /// of its agent), then <c>ConcurrentEnd</c>, which waits for all of them (a fan-in) and yields the output.
    /// </summary>
    public static void WritePipeline(Workflow workflow)
    {
        Dictionary<string, HashSet<Microsoft.Agents.AI.Workflows.Checkpointing.EdgeInfo>> edges = workflow.ReflectEdges();
        HashSet<string> seen = [workflow.StartExecutorId];
        List<List<string>> levels = [[workflow.StartExecutorId]];
        while (true)
        {
            List<string> next = levels[^1]
                .SelectMany(id => edges.TryGetValue(id, out var outgoing) ? outgoing.SelectMany(edge => edge.Connection.SinkIds) : [])
                .Where(seen.Add)
                .ToList();
            if (next.Count == 0)
            {
                break;
            }

            levels.Add(next);
        }

        IEnumerable<string> parts = levels.Select(level => level.Count == 1
            ? AgentNameOf(level[0])
            : $"[{string.Join(" | ", level.Select(AgentNameOf))}]");
        ColoredConsole.WritePrimaryLogLine($"Workflow {workflow.Name}: {string.Join(" -> ", parts)}");
    }

    /// <summary>Displays the output of a workflow: each message with its role and its author, in the order of the list.</summary>
    public static void WriteWorkflowOutput(IReadOnlyList<ChatMessage> messages)
    {
        ColoredConsole.WritePrimaryLogLine($"Workflow output ({messages.Count} {(messages.Count == 1 ? "message" : "messages")}):");
        foreach (ChatMessage message in messages)
        {
            string[] lines = message.Text.Trim().Split('\n');
            ColoredConsole.WriteSecondaryLogLine($"  [{message.Role}] {message.AuthorName ?? "(no author)"}: {lines[0].TrimEnd()}");
            foreach (string line in lines.Skip(1))
            {
                ColoredConsole.WriteSecondaryLogLine($"      {line.TrimEnd()}");
            }
        }
    }

    /// <summary>Displays the authors of the output of a workflow in one line, in the order of the list.</summary>
    public static void WriteWorkflowOutputAuthors(IReadOnlyList<ChatMessage> messages)
    {
        string authors = string.Join(", ", messages.Select(message => message.AuthorName ?? message.Role.ToString()));
        ColoredConsole.WritePrimaryLogLine(
            $"Workflow output ({messages.Count} {(messages.Count == 1 ? "message" : "messages")}, in the order they arrived): {authors}");
    }

    /// <summary>Displays the answer of an agent run by the code (scenario 3), then its token usage.</summary>
    public static void WriteAgentAnswer(string agentName, AgentResponse response)
    {
        ColoredConsole.WriteAssistantLine($"{agentName}: {response.Text}");
        ColoredConsole.WritePrimaryLogLine($"Token Usage ({agentName}):");
        ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {response.Usage?.InputTokenCount}");
        ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {response.Usage?.OutputTokenCount}");
        ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {response.Usage?.TotalTokenCount}");
    }

    /// <summary>Displays an executor that failed: the workflow stops, its output never comes.</summary>
    public static void WriteExecutorFailed(ExecutorFailedEvent failed) =>
        ColoredConsole.WriteErrorLine($"[Error] Executor {AgentNameOf(failed.ExecutorId)} failed: {failed.Data?.Message ?? "unknown error"}");

    /// <summary>Displays an error of the workflow itself.</summary>
    public static void WriteWorkflowError(WorkflowErrorEvent error) =>
        ColoredConsole.WriteErrorLine($"[Error] The workflow failed: {error.Exception?.Message ?? "unknown error"}");

    /// <summary>
    /// The name of the agent of an executor: the executor of an agent is identified by the name of the agent followed by
    /// <c>_</c> and the id of the agent (a GUID without dashes); its batcher by <c>Batcher/</c> and the id of the executor.
    /// </summary>
    public static string AgentNameOf(string executorId) => AgentIdSuffix().Replace(executorId, string.Empty);

    [GeneratedRegex("_[0-9a-f]{32}$")]
    private static partial Regex AgentIdSuffix();
}

/// <summary>
/// Follows a run of a concurrent workflow from its events (scenario 2): when each agent started and finished its turn,
/// what it received, and its token usage, computed from its streamed updates.
/// </summary>
public sealed class ParallelTrace
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly List<string> _agents = [];
    private readonly Dictionary<string, List<ChatMessage>> _received = [];
    private readonly Dictionary<string, TimeSpan> _started = [];
    private readonly Dictionary<string, TimeSpan> _finished = [];
    private readonly Dictionary<string, List<AgentResponseUpdate>> _updates = [];

    /// <summary>
    /// Records what an executor received. An agent executor is invoked once per message: the input (a list of chat messages),
    /// then the <c>TurnToken</c> that starts its turn.
    /// </summary>
    public void OnExecutorInvoked(ExecutorInvokedEvent invoked)
    {
        if (!IsAgent(invoked.ExecutorId))
        {
            return;
        }

        if (invoked.Data is IEnumerable<ChatMessage> messages)
        {
            this.Agent(invoked.ExecutorId).AddRange(messages);
        }
        else if (invoked.Data is TurnToken)
        {
            this.Agent(invoked.ExecutorId);
            this._started[invoked.ExecutorId] = this._clock.Elapsed;
            ColoredConsole.WriteWarningLine($"[{Seconds(this._clock.Elapsed)}] {WorkflowConsole.AgentNameOf(invoked.ExecutorId)} starts its turn");
        }
    }

    /// <summary>Records the end of the turn of an agent: the executor completed the handling of its <c>TurnToken</c>.</summary>
    public void OnExecutorCompleted(ExecutorCompletedEvent completed)
    {
        if (IsAgent(completed.ExecutorId) && this._started.ContainsKey(completed.ExecutorId) && !this._finished.ContainsKey(completed.ExecutorId))
        {
            this._finished[completed.ExecutorId] = this._clock.Elapsed;
            ColoredConsole.WriteWarningLine($"[{Seconds(this._clock.Elapsed)}] {WorkflowConsole.AgentNameOf(completed.ExecutorId)} has answered");
        }
    }

    /// <summary>Keeps an update streamed by an agent (its text is in the output of the workflow).</summary>
    public void OnAgentUpdate(AgentResponseUpdateEvent update)
    {
        this.Agent(update.ExecutorId);
        if (!this._updates.TryGetValue(update.ExecutorId, out List<AgentResponseUpdate>? updates))
        {
            this._updates[update.ExecutorId] = updates = [];
        }

        updates.Add(update.Update);
    }

    /// <summary>
    /// Displays, for each agent, what it received, how long its turn lasted and its token usage; then the time of the run,
    /// compared with the time the same turns would take one after the other.
    /// </summary>
    public void WriteReport()
    {
        TimeSpan sum = TimeSpan.Zero;
        foreach (string executorId in this._agents)
        {
            string name = WorkflowConsole.AgentNameOf(executorId);
            List<ChatMessage> received = this._received[executorId];
            string authors = string.Join(", ", received.Select(message => message.AuthorName ?? message.Role.ToString()));
            ColoredConsole.WritePrimaryLogLine($"[Context] {name} received {received.Count} {(received.Count == 1 ? "message" : "messages")}: {authors}");

            if (this._started.TryGetValue(executorId, out TimeSpan start) && this._finished.TryGetValue(executorId, out TimeSpan end))
            {
                sum += end - start;
                ColoredConsole.WriteSecondaryLogLine($"  Turn: from {Seconds(start)} to {Seconds(end)} ({Seconds(end - start)})");
            }

            if (this._updates.TryGetValue(executorId, out List<AgentResponseUpdate>? updates))
            {
                UsageDetails? usage = updates.ToAgentResponse().Usage;
                ColoredConsole.WriteSecondaryLogLine($"  Token Usage ({name}):");
                ColoredConsole.WriteSecondaryLogLine($"    Input tokens: {usage?.InputTokenCount}");
                ColoredConsole.WriteSecondaryLogLine($"    Output tokens: {usage?.OutputTokenCount}");
                ColoredConsole.WriteSecondaryLogLine($"    Total tokens: {usage?.TotalTokenCount}");
            }
        }

        // The turns overlap when the last one to start started before the first one to finish finished
        bool overlap = this._started.Count > 1 && this._finished.Count == this._started.Count
            && this._started.Values.Max() < this._finished.Values.Min();
        ColoredConsole.WritePrimaryLogLine(
            $"[Timing] Run: {Seconds(this._clock.Elapsed)}; the same turns one after the other: about {Seconds(sum)}");
        ColoredConsole.WritePrimaryLogLine($"[Timing] All the agents worked at the same time: {(overlap ? "yes" : "no")}");
    }

    private static bool IsAgent(string executorId) => executorId != WorkflowConsole.AgentNameOf(executorId) && !executorId.Contains('/');

    private List<ChatMessage> Agent(string executorId)
    {
        if (!this._received.TryGetValue(executorId, out List<ChatMessage>? messages))
        {
            this._agents.Add(executorId);
            this._received[executorId] = messages = [];
        }

        return messages;
    }

    private static string Seconds(TimeSpan time) => $"{time.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)} s";
}
