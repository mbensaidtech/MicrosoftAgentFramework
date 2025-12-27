namespace AIAgentWithThreads.Models;

/// <summary>
/// Represents the state of an agent thread.
/// Used to serialize/deserialize thread state for persistence.
/// </summary>
public sealed class AgentThreadState
{
    /// <summary>
    /// Gets or sets the store state (thread ID).
    /// </summary>
    public string? StoreState { get; set; }
}

