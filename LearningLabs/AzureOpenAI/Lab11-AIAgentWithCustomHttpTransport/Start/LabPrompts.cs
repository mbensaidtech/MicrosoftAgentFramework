namespace AIAgentWithCustomHttpTransport;

// ============================================
// PROVIDED - do not edit
// ============================================
// The names, instructions and questions of the agents of this lab.
// They are given so that everyone works with the same agents and gets comparable answers:
// use them in Program.cs instead of writing your own texts, and focus on the Agent Framework code.

/// <summary>
/// The agents of the lab: the name and the instructions to give to <c>AsAIAgent(...)</c>, and the description of the agent
/// called as a tool. The basic agent of scenario 1 has no name and no instructions on purpose.
/// </summary>
public static class Agents
{
    /// <summary>
    /// Scenarios 2 and 3: the agent with the get_weather function tool. Its instructions become the "system" message of
    /// each HTTP request. Scenario 3 also gives its description: the travel assistant calls it as a tool
    /// (<c>weatherAgent.AsAIFunction()</c>), and the description is the one of that tool.
    /// </summary>
    public static class Weather
    {
        public const string Name = "WeatherAgent";

        public const string Instructions = "You are a helpful assistant. Use the tools to answer.";

        public const string Description = "Gives the weather forecast for a city.";
    }

    /// <summary>Scenario 3: the travel assistant, which calls the weather agent as a tool.</summary>
    public static class TravelAssistant
    {
        public const string Name = "TravelAssistant";

        public const string Instructions = "You help travelers prepare their trips. Use your tools, then answer in two sentences.";
    }
}

/// <summary>The questions to ask the agents.</summary>
public static class Questions
{
    /// <summary>Scenario 1: one run, one HTTP request.</summary>
    public const string CapitalOfFrance = "Hello, what is the capital of France?";

    /// <summary>Scenario 2: one run, two HTTP requests (the model asks for get_weather, then answers with its result).</summary>
    public const string WeatherInAmsterdam = "What is the weather like in Amsterdam?";

    /// <summary>Scenario 3: the question of the traveler, which the travel assistant answers with the weather agent.</summary>
    public const string UmbrellaInAmsterdam = "I am going to Amsterdam tomorrow. Do I need an umbrella?";
}
