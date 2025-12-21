namespace AIAgentWithMCPClient.Models;

/// <summary>
/// Represents a Hugging Face model from the MCP search results
/// </summary>
public class HuggingFaceModel
{
    public required string Name { get; set; }

    public required string Task { get; set; }

    public required string Library { get; set; }

    public required string Link { get; set; }
}

/// <summary>
/// Represents a collection of Hugging Face models from a search
/// </summary>
public class HuggingFaceSearchResult
{
    public List<HuggingFaceModel> Models { get; set; } = [];
}
