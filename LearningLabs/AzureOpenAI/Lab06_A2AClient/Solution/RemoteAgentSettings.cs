namespace A2AClient;

public class RemoteAgentSettings
{
    /// <summary>URL of the remote agent on the A2A server, e.g. http://localhost:5000/a2a/authAgent</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Name given to the agent on the client side (direct configuration only: with discovery it comes from the agent card).</summary>
    public string? Name { get; set; }

    /// <summary>Description given to the agent on the client side (direct configuration only).</summary>
    public string? Description { get; set; }
}
