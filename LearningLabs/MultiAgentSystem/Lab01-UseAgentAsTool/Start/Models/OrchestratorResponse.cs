using System.ComponentModel;

namespace UseAgentAsTool.Models;

/// <summary>
/// Structured response from the orchestrator agent.
/// </summary>
public class OrchestratorResponse
{
    /// <summary>
    /// The final official response to show to the user.
    /// </summary>
    [Description("The final official response to show to the customer, based on the SAV agent's answer")]
    public required string OfficialResponse { get; set; }

    /// <summary>
    /// The reformulation judge evaluation.
    /// </summary>
    [Description("The evaluation from the reformulation judge")]
    public required JudgeEvaluation ReformulationJudge { get; set; }

    /// <summary>
    /// The SAV judge evaluation.
    /// </summary>
    [Description("The evaluation from the SAV judge")]
    public required JudgeEvaluation SavJudge { get; set; }
}

/// <summary>
/// Represents a judge evaluation with score and feedback.
/// </summary>
public class JudgeEvaluation
{
    /// <summary>
    /// Score from 1 to 10.
    /// </summary>
    [Description("Score from 1 to 10")]
    public int Score { get; set; }

    /// <summary>
    /// Detailed feedback or evaluation comments.
    /// </summary>
    [Description("Detailed feedback or evaluation comments")]
    public required string Feedback { get; set; }
}

