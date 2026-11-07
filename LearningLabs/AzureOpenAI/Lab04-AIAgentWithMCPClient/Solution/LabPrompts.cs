namespace AIAgentWithMCPClient;

// ============================================
// PROVIDED - do not edit
// ============================================
// The names, instructions and questions of the agents of this lab.
// They are given so that everyone works with the same agents and gets comparable answers:
// use them in Program.cs instead of writing your own texts, and focus on the Agent Framework code.

/// <summary>The agents of the lab: the name and the instructions to give to <c>AsAIAgent(...)</c>.</summary>
public static class Agents
{
    /// <summary>
    /// Scenarios 2 to 5: the agent that searches the Hugging Face Hub with the tools of the MCP server
    /// (on Chat Completions in scenarios 2 and 3, on the Responses API in scenarios 4 and 5).
    /// </summary>
    public static class HuggingFaceAssistant
    {
        public const string Name = "HuggingFaceAssistant";

        public const string Instructions =
            "You are a helpful assistant that searches the Hugging Face Hub with the tools of the Hugging Face MCP server. " +
            "Only return models found by the tools.";
    }
}

/// <summary>The questions to ask the agents.</summary>
public static class Questions
{
    /// <summary>
    /// Scenarios 2 to 5: the same request, with all the MCP tools, then with the search tool only, then on the Responses API
    /// with the tool run by the application (scenario 4) or by the service (scenario 5).
    /// </summary>
    public const string TextEmbeddingModels = "Search 4 Hugging Face models for text embedding.";
}
