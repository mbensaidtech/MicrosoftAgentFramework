using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CommonUtilities;

namespace AIAgentWithFunctionToolsHumanApproval;

/// <summary>
/// Console output helpers shared by the scenarios, so that Program.cs focuses on the approval flow.
/// </summary>
public static class ApprovalConsole
{
    /// <summary>Formats a function call as <c>name(arg=value, ...)</c>, as chosen by the model.</summary>
    public static string FormatCall(FunctionCallContent call)
    {
        string arguments = call.Arguments is null
            ? string.Empty
            : string.Join(", ", call.Arguments.Select(argument => $"{argument.Key}={argument.Value}"));

        return $"{call.Name}({arguments})";
    }

    /// <summary>
    /// Displays an approval request: the function the agent wants to call and its arguments
    /// (<see cref="ToolApprovalRequestContent.ToolCall"/> is the <see cref="FunctionCallContent"/> chosen by the model).
    /// </summary>
    public static void WriteApprovalRequest(ToolApprovalRequestContent request)
    {
        FunctionCallContent call = (FunctionCallContent)request.ToolCall;
        string arguments = call.Arguments is null
            ? string.Empty
            : string.Join(", ", call.Arguments.Select(argument => $"{argument.Key}={argument.Value}"));

        ColoredConsole.WriteWarningLine("APPROVAL REQUIRED");
        ColoredConsole.WritePrimaryLogLine("The agent would like to invoke the following sensitive function:");
        ColoredConsole.WriteSecondaryLogLine($"  Function: {call.Name}");
        ColoredConsole.WriteSecondaryLogLine($"  Arguments: {arguments}");
    }

    /// <summary>
    /// Displays the result of every tool call made during the given runs, as the model received it:
    /// the value returned by the tool, or <c>Tool call invocation rejected.</c> (+ the reason) when the call was rejected.
    /// </summary>
    public static void WriteToolResults(IEnumerable<AgentResponse> runs)
    {
        List<AIContent> contents = runs.SelectMany(run => run.Messages).SelectMany(message => message.Contents).ToList();

        // The FunctionCallContent (name) and the FunctionResultContent (result) of a call share the same CallId.
        Dictionary<string, string> callNames = contents
            .OfType<FunctionCallContent>()
            .GroupBy(call => call.CallId)
            .ToDictionary(group => group.Key, group => group.First().Name);

        List<FunctionResultContent> results = contents.OfType<FunctionResultContent>().ToList();

        ColoredConsole.WritePrimaryLogLine(results.Count == 0 ? "Tool results: none (no tool was executed)" : "Tool results:");
        foreach (FunctionResultContent result in results)
        {
            string name = callNames.TryGetValue(result.CallId, out string? callName) ? callName : "unknown tool";
            ColoredConsole.WriteSecondaryLogLine($"Tool result ({name}): {FormatResult(result.Result)}");
        }
    }

    /// <summary>
    /// Displays the token usage of an approval flow: every <c>RunAsync</c> is a run with its own usage,
    /// and an approval flow takes at least two runs (the request, then the continuation after the decision).
    /// </summary>
    public static void WriteTokenUsage(IReadOnlyList<AgentResponse> runs)
    {
        UsageDetails total = new();
        foreach (AgentResponse run in runs)
        {
            if (run.Usage is not null)
            {
                total.Add(run.Usage);
            }
        }

        ColoredConsole.WriteDividerLine();
        ColoredConsole.WritePrimaryLogLine($"Token Usage ({runs.Count} runs):");
        ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {total.InputTokenCount}");
        ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {total.OutputTokenCount}");
        ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {total.TotalTokenCount}");
    }

    // A tool returning a string is stored as a JSON string (JsonElement); a rejection is stored as a plain string.
    private static string FormatResult(object? result) => result switch
    {
        null => "(null)",
        string text => text,
        JsonElement { ValueKind: JsonValueKind.String } element => element.GetString() ?? string.Empty,
        JsonElement element => element.GetRawText(),
        _ => JsonSerializer.Serialize(result),
    };
}
