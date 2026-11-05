namespace AIAgentWithSkills;

// ============================================
// PROVIDED - do not edit
// ============================================
// The names, instructions and questions of the agents of this lab.
// They are given so that everyone works with the same agents and gets comparable answers:
// use them in Program.cs instead of writing your own texts, and focus on the Agent Framework code.

// The file-based skills of scenario 1 are not here: they are the SKILL.md files of the skills/ folder.

/// <summary>The agents of the lab: the name and the instructions to give to <c>AsAIAgent(...)</c>.</summary>
public static class Agents
{
    /// <summary>Scenario 1: the agent of the file-based skills (skills/ folder).</summary>
    public static class EmployeeAssistant
    {
        public const string Name = "EmployeeAssistant";

        public const string Instructions =
            "You are the assistant of Contoso employees. Use the available skills when they match the question.";
    }

    /// <summary>Scenarios 2 and 3: the agent of the code-defined skill <see cref="Skills.ShippingQuote"/>.</summary>
    public static class ShippingAssistant
    {
        public const string Name = "ShippingAssistant";

        public const string Instructions = "You are the shipping assistant of an online store. Use the available skills.";
    }
}

/// <summary>
/// The skills defined in code: the name, the description and the instructions to give to <c>new AgentInlineSkill(...)</c>.
/// The name and the description are advertised to the model before the skill is loaded; the instructions are sent only
/// when the model loads the skill (load_skill).
/// </summary>
public static class Skills
{
    /// <summary>
    /// Scenarios 2 and 3: the shipping quote skill. Its resources ("rates", "validity") and its script ("quote") are in
    /// ShippingRates.cs; ScriptApprovalPolicy.cs only allows the "quote" script of this skill (by its name).
    /// </summary>
    public static class ShippingQuote
    {
        public const string Name = "shipping-quote";

        public const string Description =
            "Quotes the cost of shipping a parcel. Use when asked how much it costs to send a parcel, or for a shipping quote.";

        public const string Instructions = """
            Use this skill to quote the shipping cost of a parcel.
            1. Read the rates resource to find the zone of the destination.
            2. Read the validity resource to know until when the quote is valid.
            3. Run the quote script with the weight in kg and the zone, and give the quote id and the price it returns.
            """;
    }
}

/// <summary>The questions to ask the agents.</summary>
public static class Questions
{
    /// <summary>Scenario 1: matches the expense-policy skill (the agent loads it and reads its references/limits.md resource).</summary>
    public const string HotelNightInParis = "What is the maximum I can spend on a hotel night in Paris for a business trip?";

    /// <summary>Scenario 1: no skill covers it, so no skill is loaded.</summary>
    public const string CapitalOfItaly = "What is the capital of Italy?";

    /// <summary>Scenario 2: the agent loads the skill, reads both resources and runs the script.</summary>
    public const string QuoteWithValidity =
        "How much does it cost to ship a 2.5 kg parcel to Germany? Give me the quote id and until when it is valid.";

    /// <summary>Scenario 3, without auto-approval: the first skill tool call (load_skill) waits for an approval.</summary>
    public const string CostFor2Point5Kg = "How much does it cost to ship a 2.5 kg parcel to Germany?";

    /// <summary>Scenario 3: a parcel of 2.5 kg, the policy approves the script.</summary>
    public const string QuoteFor2Point5Kg = "How much does it cost to ship a 2.5 kg parcel to Germany? Give me the quote id.";

    /// <summary>Scenario 3: a parcel of 120 kg (more than 30 kg), the policy rejects the script.</summary>
    public const string QuoteFor120Kg = "How much does it cost to ship a 120 kg parcel to Germany? Give me the quote id.";
}
