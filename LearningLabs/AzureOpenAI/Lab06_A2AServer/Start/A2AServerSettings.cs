namespace A2AServer;

public class A2AServerSettings
{
    /// <summary>Address the server listens on, also advertised in the agent cards, e.g. http://localhost:5000</summary>
    public string BaseUrl { get; set; } = string.Empty;
}
