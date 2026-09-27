namespace AIAgentWithMCPClient;

public class MCPServerSettings
{
    /// <summary>URL of the remote MCP server (Streamable HTTP), e.g. https://huggingface.co/mcp</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Optional access token sent as <c>Authorization: Bearer &lt;token&gt;</c>. It is a secret: keep it in user secrets
    /// or an environment variable, never in appsettings.json. When empty, the client connects anonymously.</summary>
    public string? BearerToken { get; set; }
}
