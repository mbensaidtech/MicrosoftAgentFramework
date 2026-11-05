namespace DataFormatComparison;

// ============================================
// PROVIDED - do not edit
// ============================================
// The names, instructions and questions of the agents of this lab.
// They are given so that everyone works with the same agents and gets comparable answers:
// use them in Program.cs instead of writing your own texts, and focus on the Agent Framework code.

/// <summary>The agents of the lab: the name and the instructions to give to <c>AsAIAgent(...)</c>.</summary>
public static class Agents
{
    /// <summary>Scenario 1: the agent whose tool returns the hotels as objects (JSON), and which answers with structured output.</summary>
    public static class HotelJson
    {
        public const string Name = "HotelJsonAgent";

        public const string Instructions =
            "You are a hotel data assistant. Call get_all_hotels to get the hotels, then answer with every hotel that matches the request, in the requested order.";
    }

    /// <summary>
    /// Scenario 2: the agent whose tool returns the hotels as CSV text, and which is asked to answer in CSV.
    /// The instructions contain the header line of the CSV (<see cref="DataFormatComparison.HotelCsv.Header"/>, a constant).
    /// </summary>
    public static class HotelCsv
    {
        public const string Name = "HotelCsvAgent";

        public const string Instructions = $"""
            You are a hotel data assistant. Call get_all_hotels to get the hotels as CSV, then answer with every hotel that matches the request, in the requested order.
            Answer ONLY in CSV, with exactly this header line and then one line per hotel, with the same columns and values as the tool result:
            {DataFormatComparison.HotelCsv.Header}
            No explanation, no markdown, no code fences.
            """;
    }
}

/// <summary>The questions to ask the agents.</summary>
public static class Questions
{
    /// <summary>Scenarios 1 and 2: the same question on the same data, to compare the two formats.</summary>
    public const string HotelsUnder100Usd =
        "Which hotels cost less than 100 USD per night? Give all of them, ordered by price per night, cheapest first.";
}
