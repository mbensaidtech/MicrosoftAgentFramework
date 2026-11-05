namespace A2AClient;

// ============================================
// PROVIDED - do not edit
// ============================================
// The names, instructions and questions of the agents of this lab.
// They are given so that everyone works with the same agents and gets comparable answers:
// use them in Program.cs instead of writing your own texts, and focus on the Agent Framework code.
// The remote agents (AuthAgent, CustomerToneAgent) are defined by Lab06_A2AServer: only the local agent is here.

/// <summary>The agents of the lab: the name and the instructions to give to <c>AsAIAgent(...)</c>.</summary>
public static class Agents
{
    /// <summary>Scenario 3: the local agent, which calls the remote AuthAgent as a tool.</summary>
    public static class Assistant
    {
        public const string Name = "Assistant";

        public const string Instructions = "You are a helpful assistant. Use the available tools to answer the user's questions.";
    }
}

/// <summary>The questions to ask the agents.</summary>
public static class Questions
{
    /// <summary>Scenario 1, and scenario 4 (first turn): asks the remote AuthAgent for a new API key.</summary>
    public const string GenerateApiKey = "Generate a new API key";

    /// <summary>
    /// Scenario 1: asks the remote AuthAgent to validate a key (the generated key, then a tampered copy of it).
    /// A format string: <c>{0}</c> is the key, given with
    /// <c>string.Format(CultureInfo.InvariantCulture, Questions.ValidateApiKey, apiKey)</c>.
    /// </summary>
    public const string ValidateApiKey = "Validate this API key: {0}";

    /// <summary>
    /// Scenario 2: asks the remote CustomerToneAgent for the tone of a customer message.
    /// A format string: <c>{0}</c> is the customer message, given with
    /// <c>string.Format(CultureInfo.InvariantCulture, Questions.ToneOfCustomerMessage, CustomerMessages.WaitingForOrder)</c>.
    /// </summary>
    public const string ToneOfCustomerMessage = "What is the tone of this customer message: \"{0}\"";

    /// <summary>Scenario 3: the local agent calls the remote AuthAgent (as a tool) to generate a key, then to check it.</summary>
    public const string GenerateAndCheckApiKey =
        "I need a new API key. Generate one, check that it is valid, and give me the key and the result of the check.";

    /// <summary>
    /// Scenario 4: the follow-up question, asked in the session of the first turn, then without it. It refers to the key
    /// of the first turn without giving it: only an agent that has the conversation can give back the same key.
    /// </summary>
    public const string KeyYouJustGenerated =
        "Give me again the API key you just generated, exactly as it is, and check that it is valid.";
}

/// <summary>The customer messages whose tone the remote CustomerToneAgent detects.</summary>
public static class CustomerMessages
{
    /// <summary>Scenario 2: inserted in <see cref="Questions.ToneOfCustomerMessage"/>.</summary>
    public const string WaitingForOrder = "I have been waiting for my order for two weeks and nobody answers my emails!";
}
