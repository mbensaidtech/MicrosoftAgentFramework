using System.ComponentModel;

namespace UseAgentAsTool.Models;

/// <summary>
/// The review returned by the reviewer agent (structured output of scenario 1).
/// The [Description] attributes are copied into the JSON schema sent to the model.
/// </summary>
public class QualityReview
{
    [Description("Score of the answer, from 1 (contradicts the rules) to 10 (correct according to the rules and answers the question)")]
    public int Score { get; set; }

    [Description("The main problem of the answer, or why it is correct, in one sentence")]
    public required string Feedback { get; set; }
}
