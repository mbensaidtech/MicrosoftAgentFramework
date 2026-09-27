using System.ComponentModel;

namespace AIAgentWithMCPClient.Models;

/// <summary>
/// A Hugging Face model found by the agent with the MCP tools.
/// The [Description] attributes are copied into the JSON schema of the structured output: they tell the model what to put in each property.
/// </summary>
[Description("A model hosted on the Hugging Face Hub")]
public class HuggingFaceModel
{
    [Description("Model id on the Hub, in the form owner/name, e.g. sentence-transformers/all-MiniLM-L6-v2")]
    public required string Name { get; set; }

    [Description("Pipeline task of the model, e.g. sentence-similarity or feature-extraction")]
    public required string Task { get; set; }

    [Description("Library used to run the model, e.g. sentence-transformers or transformers")]
    public required string Library { get; set; }

    [Description("URL of the model page on the Hub, as returned by the tool")]
    public required string Link { get; set; }
}

/// <summary>
/// The result of a model search: the structured output of the agent.
/// </summary>
[Description("Hugging Face models matching the search")]
public class HuggingFaceSearchResult
{
    [Description("Models found by the search tool, in the order returned by the tool")]
    public List<HuggingFaceModel> Models { get; set; } = [];
}
