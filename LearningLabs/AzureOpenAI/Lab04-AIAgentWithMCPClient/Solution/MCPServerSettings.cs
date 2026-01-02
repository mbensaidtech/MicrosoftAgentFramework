namespace AIAgentWithMCPClient;

/// <summary>
/// Configuration settings for MCP servers.
/// </summary>
public class MCPServerSettings
{
    /// <summary>
    /// Gets or sets the MCP server endpoint.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the MCP server bearer token.
    /// </summary>
    public string BearerToken { get; set; } = string.Empty;
}
