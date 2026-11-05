namespace FirstBasicAIAgent;

// ============================================
// PROVIDED - do not edit
// ============================================
// The names, instructions and questions of the agents of this lab.
// They are given so that everyone works with the same agents and gets comparable answers:
// use them in Program.cs instead of writing your own texts, and focus on the Agent Framework code.

/// <summary>The agents of the lab: the name and the instructions to give to <c>AsAIAgent(...)</c>.</summary>
public static class Agents
{
    /// <summary>Scenarios 2, 3 and 6.</summary>
    public static class Geography
    {
        public const string Name = "GeographyAgent";

        public const string Instructions =
            "You are a helpful geography assistant. You are able to answer questions about the geography of the world.";
    }

    /// <summary>Scenario 4.</summary>
    public static class ColorDecorator
    {
        public const string Name = "ColorDecoratorAgent";

        public const string Instructions =
            "You are a helpful color decorator assistant. You are able to answer questions about the color of the world.";
    }

    /// <summary>Scenario 5.</summary>
    public static class Storyteller
    {
        public const string Name = "StorytellerAgent";

        public const string Instructions = "You are a storyteller. You write short, vivid stories for travelers.";
    }
}

/// <summary>The system messages written by the developer for one run (never built from end-user input).</summary>
public static class SystemMessages
{
    /// <summary>Scenario 3: refines the geography agent for this run only.</summary>
    public const string GeographyExpert =
        "You are a geography expert. Provide detailed and accurate information about world geography.";
}

/// <summary>The questions to ask the agents.</summary>
public static class Questions
{
    /// <summary>Scenario 1.</summary>
    public const string CapitalOfFrance = "Hello, what is the capital of France?";

    /// <summary>Scenario 2.</summary>
    public const string SurfaceAreaOfFrance = "Hello, what is the surface area of France?";

    /// <summary>Scenario 3.</summary>
    public const string NeighborsOfFrance =
        "What are the neighboring countries of France? give me the countries in a list without any other text.";

    /// <summary>Scenario 4.</summary>
    public const string ColorsMatchingBlue =
        "Hello, what are colors that match with the color blue? give me the colors in a list without any other text.";

    /// <summary>Scenario 5.</summary>
    public const string StoryAboutParis = "Tell me a four-sentence story about a trip to Paris.";

    /// <summary>Scenario 6: the first question.</summary>
    public const string SurfaceAreaOfItaly = "Hello, what is the surface area of Italy?";

    /// <summary>Scenario 6: the follow-up question, which only makes sense after the first one ("its" is Italy).</summary>
    public const string ItsPopulation = "And what is its population?";
}
