namespace A2AServer;

// ============================================
// PROVIDED - do not edit
// ============================================
// The names, instructions and descriptions of the agents of this lab.
// They are given so that everyone works with the same agents and gets comparable answers:
// use them in Program.cs instead of writing your own texts, and focus on the Agent Framework code.

/// <summary>
/// The agents of the lab: the name and the instructions to give to <c>AsAIAgent(...)</c>, and the description
/// published in their agent card (<c>AgentCards.cs</c>).
/// </summary>
public static class Agents
{
    /// <summary>Scenario 1: the agent that generates and validates API keys with its tools.</summary>
    public static class Auth
    {
        /// <summary>The name of the agent, also the name of its agent card: the A2A hosting finds the agent by this name.</summary>
        public const string Name = "AuthAgent";

        public const string Instructions =
            "You are a helpful API key assistant. You are able to generate and validate API keys. " +
            "When you generate a key, return the key exactly as produced by the tool.";

        /// <summary>The description of its agent card: a client that uses the agent as a tool gives it to its model.</summary>
        public const string Description =
            "An authentication agent specialized in generating and validating API keys. Only handles authentication-related tasks.";
    }

    /// <summary>Scenario 1: the agent that detects the tone of a customer's message (no tools).</summary>
    public static class CustomerTone
    {
        /// <summary>The name of the agent, also the name of its agent card: the A2A hosting finds the agent by this name.</summary>
        public const string Name = "CustomerToneAgent";

        public const string Instructions =
            "You are a helpful customer tone assistant. You are able to detect the tone of a customer's message. " +
            "Start your answer with the tone in one word (for example: Angry, Neutral, Satisfied), then explain briefly.";

        /// <summary>The description of its agent card.</summary>
        public const string Description = "A customer tone assistant that can detect the tone of a customer's message.";
    }
}
