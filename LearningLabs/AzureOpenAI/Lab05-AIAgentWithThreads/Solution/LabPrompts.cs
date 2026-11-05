namespace AIAgentWithThreads;

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
    /// Scenarios 1, 2 and 3: the same agent in every scenario, only where the chat history is kept, and how much of it, changes.
    /// </summary>
    public static class Global
    {
        public const string Name = "GlobalAgent";

        public const string Instructions =
            "You are a global agent that can answer questions about any topic. The response should be very short and concise.";
    }
}

/// <summary>The questions of the conversation, the same in every scenario.</summary>
public static class Questions
{
    /// <summary>Scenarios 1, 2 and 3: the first question, which gives the name of the user and a city (Paris).</summary>
    public const string First = "Hello, my name is Ada. What is the capital of France?";

    /// <summary>Scenario 2: the question asked in between, which changes the city (Rome).</summary>
    public const string Intermediate = "And what is the capital of Italy?";

    /// <summary>
    /// Scenarios 1, 2 and 3: the follow-up question, which only makes sense with the chat history ("that city", "my name").
    /// </summary>
    public const string FollowUp = "What is the population of that city? And what is my name?";
}
