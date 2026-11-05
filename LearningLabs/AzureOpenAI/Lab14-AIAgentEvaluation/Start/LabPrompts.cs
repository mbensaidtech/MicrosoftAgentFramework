namespace AIAgentEvaluation;

// ============================================
// PROVIDED - do not edit
// ============================================
// The names, instructions and questions of the agents of this lab.
// They are given so that everyone works with the same agents and gets comparable answers:
// use them in Program.cs instead of writing your own texts, and focus on the Agent Framework code.

// The questions of the evaluation are not here: they are the dataset of EvaluationDataset.cs, with their expected outputs.

/// <summary>The agents of the lab: the name and the instructions to give to <c>AsAIAgent(...)</c>.</summary>
public static class Agents
{
    /// <summary>
    /// Scenarios 1, 2 and 3: the customer support agent under test, with the tool get_order_status.
    /// Scenario 3 creates it twice, with the same name and the same tool: once with <see cref="Instructions"/> (the reference)
    /// and once with <see cref="CandidateInstructions"/> (the new version, whose regression the evaluation detects).
    /// </summary>
    public static class Support
    {
        public const string Name = "SupportAgent";

        /// <summary>The reference version of the instructions (scenarios 1, 2 and 3), printed at the start of Program.cs.</summary>
        public const string Instructions = """
            You are the customer support assistant of an online store.
            For any question about an order, call get_order_status with the order number, and answer with the facts it returns:
            tracking number, carrier, locker code, dates.
            Returns are accepted within 30 days of delivery.
            Answer in at most three sentences.
            """;

        /// <summary>
        /// Scenario 3: a new version of the instructions, with a new security requirement. It looks harmless, but the agent
        /// now asks for an email address instead of calling get_order_status: the checks of the dataset catch the regression.
        /// </summary>
        public const string CandidateInstructions = """
            You are the customer support assistant of an online store.
            Security first: before looking up an order, ask the customer for the email address used for the order,
            and do not call get_order_status until the customer has given it.
            Returns are accepted within 30 days of delivery.
            Answer in at most three sentences.
            """;
    }
}
