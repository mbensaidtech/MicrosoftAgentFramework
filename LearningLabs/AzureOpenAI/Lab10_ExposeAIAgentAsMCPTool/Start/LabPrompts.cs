namespace ExposeAIAgentAsMCPTool;

// ============================================
// PROVIDED - do not edit
// ============================================
// The names, instructions and questions of the agents of this lab.
// They are given so that everyone works with the same agents and gets comparable answers:
// use them in Program.cs instead of writing your own texts, and focus on the Agent Framework code.

/// <summary>
/// The agents of the lab: the name and the instructions to give to <c>AsAIAgent(...)</c>, and the description of the agent
/// exposed as an MCP tool.
/// </summary>
public static class Agents
{
    /// <summary>
    /// Every scenario and the stdio server: the agent exposed as an MCP tool. Its name and its description become the name
    /// and the description of the MCP tool, which the clients (and their models) read to decide when to call it.
    /// </summary>
    public static class CompanyAssistant
    {
        public const string Name = "CompanyAssistant";

        public const string Instructions =
            "You are a helpful assistant that can help with company tasks. Answer with the facts returned by the tools.";

        public const string Description =
            "Answers questions about the employees and the meeting rooms of the company, and books meeting rooms.";
    }

    /// <summary>Scenario 3: the agent whose only tool is the CompanyAssistant, called through MCP.</summary>
    public static class OfficeAssistant
    {
        public const string Name = "OfficeAssistant";

        public const string Instructions =
            "You are the personal assistant of the employees. Delegate every question or task about the company " +
            "to the CompanyAssistant tool, then report its answer briefly.";
    }
}

/// <summary>The questions sent to the CompanyAssistant MCP tool (its "query" parameter) or asked to the agents.</summary>
public static class Questions
{
    /// <summary>Scenario 1: the query of the MCP tool, over Streamable HTTP.</summary>
    public const string EmployeeInfoEmp003 = "Get the information of the employee with the ID EMP003.";

    /// <summary>Scenario 2: the query of the MCP tool, over stdio.</summary>
    public const string RoomsForTenPeople = "Which meeting rooms can host at least 10 people?";

    /// <summary>
    /// Scenario 3: the request to the OfficeAssistant, which delegates it to the CompanyAssistant tool.
    /// A format string: <c>{0}</c> is the date of tomorrow (yyyy-MM-dd), given with
    /// <c>string.Format(CultureInfo.InvariantCulture, Questions.BookRoomBTomorrow, tomorrow)</c>.
    /// </summary>
    public const string BookRoomBTomorrow =
        "Book the meeting room ROOM-B for the employee EMP002 on {0} " +
        "from 10:00 to 11:00, subject: Campaign review. Tell me the name of the room and the name of the employee.";
}
