namespace AIAgentWebApi;

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
    /// The agent behind the API, registered once in dependency injection and used by every request
    /// (scenarios 2, 3 and 4, and your own calls with <c>--serve</c>).
    /// </summary>
    public static class IntranetAssistant
    {
        public const string Name = "IntranetAssistant";

        public const string Instructions =
            "You are the assistant of the company intranet. Answer in one or two sentences. " +
            "Use get_my_profile for any question about the user's team or office.";
    }
}

/// <summary>The messages that the scenarios send to the API (provided code at the end of Program.cs).</summary>
public static class Questions
{
    /// <summary>Scenario 1: sent without a token, refused (401) before the agent runs.</summary>
    public const string HelloWhichTeam = "Hello, which team am I in?";

    /// <summary>Scenario 2: Alice starts a conversation; the tool reads her profile from her token.</summary>
    public const string WhichTeamAndRememberFavoriteRoom =
        "Which team am I in? Please also remember that my favorite meeting room is ROOM-B.";

    /// <summary>Scenario 3: Alice, in a new HTTP request of the same conversation (the API loads the saved session).</summary>
    public const string WhichRoomDoIPrefer = "Which meeting room do I prefer?";

    /// <summary>Scenario 4: Bob, with the conversation id of Alice: he gets his own conversation and his own profile.</summary>
    public const string WhichRoomAndWhichTeam = "Which meeting room do I prefer, and which team am I in?";
}
