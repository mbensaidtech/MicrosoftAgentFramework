namespace UseAgentAsTool.Configuration;

/// <summary>
/// Defines the available agent types.
/// </summary>
public enum AgentType
{
    /// <summary>
    /// Reformulates user messages using only keywords.
    /// </summary>
    Reformulator,

    /// <summary>
    /// Judges the quality of the Reformulator agent's work.
    /// </summary>
    ReformulatorJudge,

    /// <summary>
    /// Answers questions about SAV (After-Sales Service) on e-commerce website.
    /// </summary>
    SAVAgent,

    /// <summary>
    /// Evaluates if the SAV agent's answer is correct and helpful.
    /// </summary>
    SAVJudge,

    /// <summary>
    /// Orchestrates all agents to process user requests.
    /// </summary>
    Orchestrator
}

