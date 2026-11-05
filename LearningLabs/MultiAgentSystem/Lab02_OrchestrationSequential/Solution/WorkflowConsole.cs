using System.Text.RegularExpressions;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using CommonUtilities;

namespace OrchestrationSequential;

// ============================================
// PROVIDED - do not edit
// ============================================

/// <summary>
/// Console output helpers shared by the scenarios, so that Program.cs focuses on the workflow: how it is built, run and observed.
/// </summary>
public static partial class WorkflowConsole
{
    /// <summary>
    /// Displays the path of a sequential workflow, from its start executor along its edges: one executor per agent,
    /// then <c>OutputMessages</c>, the executor added by <c>BuildSequential</c> that yields the output of the workflow.
    /// </summary>
    public static void WritePipeline(Workflow workflow)
    {
        Dictionary<string, HashSet<Microsoft.Agents.AI.Workflows.Checkpointing.EdgeInfo>> edges = workflow.ReflectEdges();
        List<string> path = [];
        string? executorId = workflow.StartExecutorId;
        while (executorId is not null && !path.Contains(executorId))
        {
            path.Add(executorId);
            executorId = edges.TryGetValue(executorId, out var outgoing)
                ? outgoing.SelectMany(edge => edge.Connection.SinkIds).FirstOrDefault()
                : null;
        }

        ColoredConsole.WritePrimaryLogLine($"Workflow {workflow.Name}: {string.Join(" -> ", path.Select(AgentNameOf))}");
    }

    /// <summary>
    /// Displays the output of a sequential workflow: the list of messages yielded by its last executor.
    /// The input is shortened; the messages of the agents are displayed in full, with their author.
    /// </summary>
    public static void WriteWorkflowOutput(IReadOnlyList<ChatMessage> messages)
    {
        ColoredConsole.WritePrimaryLogLine($"Workflow output ({messages.Count} {(messages.Count == 1 ? "message" : "messages")}):");
        foreach (ChatMessage message in messages)
        {
            string author = message.AuthorName
                ?? (message.Contents.OfType<ToolApprovalResponseContent>().Any() ? "(the answer of the code)" : "(input)");
            string text = Describe(message);
            if (message.Role == ChatRole.User && message.AuthorName is null && text.Length > 80)
            {
                text = $"{FirstLine(text)} ... ({text.Length} characters)";
            }

            string[] lines = text.Split('\n');
            ColoredConsole.WriteSecondaryLogLine($"  [{message.Role}] {author}: {lines[0].TrimEnd()}");
            foreach (string line in lines.Skip(1))
            {
                ColoredConsole.WriteSecondaryLogLine($"      {line.TrimEnd()}");
            }
        }
    }

    /// <summary>
    /// Displays the output of a sequential workflow in one line: the number of messages and their authors
    /// (their texts were already streamed, scenario 2).
    /// </summary>
    public static void WriteWorkflowOutputAuthors(IReadOnlyList<ChatMessage> messages)
    {
        string authors = string.Join(", ", messages.Select(message => message.AuthorName ?? message.Role.ToString()));
        ColoredConsole.WritePrimaryLogLine(
            $"Workflow output ({messages.Count} {(messages.Count == 1 ? "message" : "messages")}): {authors}");
    }

    /// <summary>Displays an executor that failed: the workflow stops, its output never comes.</summary>
    public static void WriteExecutorFailed(ExecutorFailedEvent failed) =>
        ColoredConsole.WriteErrorLine($"[Error] Executor {AgentNameOf(failed.ExecutorId)} failed: {failed.Data?.Message ?? "unknown error"}");

    /// <summary>Displays an error of the workflow itself.</summary>
    public static void WriteWorkflowError(WorkflowErrorEvent error) =>
        ColoredConsole.WriteErrorLine($"[Error] The workflow failed: {error.Exception?.Message ?? "unknown error"}");

    /// <summary>Displays the state of a run, for example while it waits for the answer to a request.</summary>
    public static void WriteRunStatus(RunStatus status) =>
        ColoredConsole.WritePrimaryLogLine($"Run status: {status}");

    /// <summary>
    /// Displays a tool call that waits for an approval (scenario 3): the agent that asks, the tool and its arguments
    /// (a long text argument is shortened to its length).
    /// </summary>
    public static void WriteApprovalRequest(ToolApprovalRequestContent request)
    {
        if (request.ToolCall is not FunctionCallContent call)
        {
            ColoredConsole.WriteWarningLine($"[Approval] Request {request.RequestId} for a tool call");
            return;
        }

        IEnumerable<string> arguments = (call.Arguments ?? new Dictionary<string, object?>()).Select(argument =>
        {
            string value = argument.Value?.ToString() ?? string.Empty;
            return value.Length > 80 || value.Contains('\n')
                ? $"{argument.Key}: ({value.Length} characters)"
                : $"{argument.Key}: \"{value}\"";
        });
        ColoredConsole.WriteWarningLine($"[Approval] {call.Name}({string.Join(", ", arguments)}) waits for an approval");
    }

    /// <summary>Displays the answer given to an approval request (scenario 3).</summary>
    public static void WriteApprovalResponse(ToolApprovalResponseContent response)
    {
        string tool = (response.ToolCall as FunctionCallContent)?.Name ?? "the tool";
        ColoredConsole.WriteWarningLine($"[Approval] {tool}: {(response.Approved ? "approved" : "rejected")}");
    }

    /// <summary>
    /// The name of the agent of an executor: the executor of an agent is identified by the name of the agent followed by
    /// <c>_</c> and the id of the agent (a GUID without dashes).
    /// </summary>
    public static string AgentNameOf(string executorId) => AgentIdSuffix().Replace(executorId, string.Empty);

    internal static string Describe(ChatMessage message)
    {
        List<string> parts = [];
        foreach (AIContent content in message.Contents)
        {
            switch (content)
            {
                case TextContent text when !string.IsNullOrWhiteSpace(text.Text):
                    parts.Add(text.Text.Trim());
                    break;
                case FunctionCallContent call:
                    parts.Add($"(calls {call.Name})");
                    break;
                case FunctionResultContent result:
                    parts.Add($"(result: {result.Result})");
                    break;
                case ToolApprovalRequestContent:
                    parts.Add("(asks for an approval)");
                    break;
                case ToolApprovalResponseContent approval:
                    parts.Add($"(approval: {(approval.Approved ? "approved" : "rejected")})");
                    break;
            }
        }

        return string.Join(' ', parts);
    }

    private static string FirstLine(string text) =>
        text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;

    [GeneratedRegex("_[0-9a-f]{32}$")]
    private static partial Regex AgentIdSuffix();
}

/// <summary>
/// Follows a run of a sequential workflow from its events (scenario 2): what each executor received, the text each agent streamed,
/// and the token usage of each agent, computed from its streamed updates.
/// </summary>
public sealed class PipelineTrace
{
    private readonly List<string> _executors = [];
    private readonly Dictionary<string, List<ChatMessage>> _received = [];
    private readonly Dictionary<string, List<AgentResponseUpdate>> _updates = [];
    private string? _streamingExecutorId;

    /// <summary>
    /// Records the messages an executor received. In a workflow, an executor is invoked once per message it receives:
    /// the messages of the previous step (lists of chat messages), then the <c>TurnToken</c> that tells an agent to take its turn.
    /// </summary>
    public void OnExecutorInvoked(ExecutorInvokedEvent invoked)
    {
        if (invoked.Data is IEnumerable<ChatMessage> messages)
        {
            this.Received(invoked.ExecutorId).AddRange(messages);
        }
    }

    /// <summary>Writes the text of an update streamed by an agent, with the name of the agent when another agent starts.</summary>
    public void OnAgentUpdate(AgentResponseUpdateEvent update)
    {
        if (update.ExecutorId != this._streamingExecutorId)
        {
            if (this._streamingExecutorId is not null)
            {
                Console.WriteLine();
            }

            this._streamingExecutorId = update.ExecutorId;
            ColoredConsole.WriteAssistantLine($"{WorkflowConsole.AgentNameOf(update.ExecutorId)}:");
        }

        Console.Write(update.Update.Text);
        this.Received(update.ExecutorId);
        if (!this._updates.TryGetValue(update.ExecutorId, out List<AgentResponseUpdate>? updates))
        {
            this._updates[update.ExecutorId] = updates = [];
        }

        updates.Add(update.Update);
    }

    /// <summary>
    /// Displays, for each executor in the order of the pipeline, the messages it received (count and authors), and for each agent
    /// the token usage of its turn. The input tokens of an agent grow with what it receives.
    /// </summary>
    public void WriteReport()
    {
        if (this._streamingExecutorId is not null)
        {
            Console.WriteLine();
            this._streamingExecutorId = null;
        }

        foreach (string executorId in this._executors)
        {
            List<ChatMessage> received = this._received[executorId];
            string authors = string.Join(", ", received.Select(message => message.AuthorName ?? message.Role.ToString()));
            ColoredConsole.WritePrimaryLogLine(
                $"[Context] {WorkflowConsole.AgentNameOf(executorId)} received {received.Count} {(received.Count == 1 ? "message" : "messages")}: {authors}");

            if (this._updates.TryGetValue(executorId, out List<AgentResponseUpdate>? updates))
            {
                UsageDetails? usage = updates.ToAgentResponse().Usage;
                ColoredConsole.WriteSecondaryLogLine($"  Token Usage ({WorkflowConsole.AgentNameOf(executorId)}):");
                ColoredConsole.WriteSecondaryLogLine($"    Input tokens: {usage?.InputTokenCount}");
                ColoredConsole.WriteSecondaryLogLine($"    Output tokens: {usage?.OutputTokenCount}");
                ColoredConsole.WriteSecondaryLogLine($"    Total tokens: {usage?.TotalTokenCount}");
            }
        }
    }

    private List<ChatMessage> Received(string executorId)
    {
        if (!this._received.TryGetValue(executorId, out List<ChatMessage>? messages))
        {
            this._executors.Add(executorId);
            this._received[executorId] = messages = [];
        }

        return messages;
    }
}
