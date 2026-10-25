using Microsoft.Agents.AI;

namespace AIAgentEvaluation;

/// <summary>
/// The evaluation dataset: the questions a customer could ask, and for each one the expected answer (a text the answer
/// must contain) and the expected tool calls. The three lists are paired by position, as EvaluateAsync expects.
/// Like a test suite, it grows each time a bug is found: add the question that failed, with its expected answer.
/// </summary>
public static class EvaluationDataset
{
    public static readonly string[] Questions =
    [
        "Where is my order ORD-1042?",
        "Which carrier delivers my order ORD-1042?",
        "Where can I pick up my order ORD-1043?",
        "How many days do I have to return an item?"
    ];

    /// <summary>A fact each answer must contain: only the tool knows the first three, the instructions give the fourth.</summary>
    public static readonly string[] ExpectedOutputs =
    [
        "TRK-48213",
        "ParcelExpress",
        "LCK-305",
        "30"
    ];

    /// <summary>The tool calls each question needs: the right tool, with the right order number. None for the return policy.</summary>
    public static readonly ExpectedToolCall[][] ExpectedToolCalls =
    [
        [new ExpectedToolCall("get_order_status", new Dictionary<string, object> { ["orderId"] = "ORD-1042" })],
        [new ExpectedToolCall("get_order_status", new Dictionary<string, object> { ["orderId"] = "ORD-1042" })],
        [new ExpectedToolCall("get_order_status", new Dictionary<string, object> { ["orderId"] = "ORD-1043" })],
        []
    ];
}
