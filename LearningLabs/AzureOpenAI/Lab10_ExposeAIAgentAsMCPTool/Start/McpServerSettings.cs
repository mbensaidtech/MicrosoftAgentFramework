namespace ExposeAIAgentAsMCPTool;

public class McpServerSettings
{
    /// <summary>Address the HTTP MCP server of scenario 1 listens on, e.g. http://localhost:5100 (the MCP endpoint is under it).</summary>
    public string BaseUrl { get; set; } = string.Empty;
}
