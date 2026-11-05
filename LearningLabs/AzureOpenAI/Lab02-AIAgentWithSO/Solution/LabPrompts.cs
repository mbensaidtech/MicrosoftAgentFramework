namespace AIAgentWithSO;

// ============================================
// PROVIDED - do not edit
// ============================================
// The names, instructions and questions of the agents of this lab.
// They are given so that everyone works with the same agents and gets comparable answers:
// use them in Program.cs instead of writing your own texts, and focus on the Agent Framework code.

/// <summary>The agents of the lab: the name and the instructions to give to <c>AsAIAgent(...)</c>.</summary>
public static class Agents
{
    /// <summary>Scenario 1: the instructions describe the expected JSON format themselves.</summary>
    public static class RestaurantInfoJsonFormat
    {
        public const string Name = "RestaurantInfoAgent";

        public const string Instructions = """
            You are a culinary expert assistant. When asked about a restaurant, always respond with valid JSON in this exact format:
            {
                "Name": "restaurant name",
                "ChefName": "head chef name",
                "Cuisine": "French|Italian|Japanese|Chinese|Mexican|Indian|Spanish|American|Mediterranean|Thai|Korean|Vietnamese|Other",
                "MichelinStars": number (0-3),
                "AveragePricePerPerson": number (in euros),
                "City": "city name",
                "Country": "country name",
                "YearEstablished": number
            }
            Only respond with the JSON, no other text.
            """;
    }

    /// <summary>Scenarios 2, 3 and 4: simple instructions, no JSON format (the response format gives it).</summary>
    public static class RestaurantInfo
    {
        public const string Name = "RestaurantInfoAgent";

        public const string Instructions = "You are a culinary expert assistant that answers questions about restaurants.";
    }
}

/// <summary>The questions to ask the agents.</summary>
public static class Questions
{
    /// <summary>Scenario 1: the question also asks for JSON only.</summary>
    public const string LeBernardinAsJson = "Tell me about the restaurant 'Le Bernardin' in New York. Respond only with JSON.";

    /// <summary>Scenarios 2, 3 and 4.</summary>
    public const string LeBernardin = "Tell me about the restaurant 'Le Bernardin' in New York.";
}
