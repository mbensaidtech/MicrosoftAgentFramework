namespace AIAgentWithFunctionTools;

// ============================================
// PROVIDED - do not edit
// ============================================
// The names, instructions and questions of the agents of this lab.
// They are given so that everyone works with the same agents and gets comparable answers:
// use them in Program.cs instead of writing your own texts, and focus on the Agent Framework code.

/// <summary>The agents of the lab: the name and the instructions to give to <c>AsAIAgent(...)</c>.</summary>
public static class Agents
{
    /// <summary>Scenarios 1, 2 and 4: the agent of the company tools (employees and meeting rooms).</summary>
    public static class CompanyAssistant
    {
        public const string Name = "CompanyAssistant";

        public const string Instructions = "You are a helpful assistant that can help with company tasks.";
    }

    /// <summary>Scenario 3: the agent of the notification tools.</summary>
    public static class NotificationAssistant
    {
        public const string Name = "NotificationAssistant";

        public const string Instructions = "You are a helpful assistant that can send and retrieve notifications.";
    }
}

/// <summary>The questions to ask the agents.</summary>
public static class Questions
{
    /// <summary>Scenario 1: the agent calls get_employee_info.</summary>
    public const string EmployeeInfo = "Get the information of the employee with the ID EMP001";

    /// <summary>
    /// Scenario 2: two bookings, the first one in the past (the plugin refuses it), the second one tomorrow.
    /// A format string: <c>{0}</c> is the date of tomorrow (yyyy-MM-dd), given with
    /// <c>string.Format(CultureInfo.InvariantCulture, Questions.BookPastAndTomorrowMeetings, tomorrow)</c>.
    /// </summary>
    public const string BookPastAndTomorrowMeetings =
        "What are the available meeting rooms? Then book the room ROOM-A for the employee with the ID EMP001 on 2025-12-16 from 10:00 to 11:00 for a 'Sprint Review', " +
        "and the room ROOM-B for the employee with the ID EMP002 on {0} from 14:00 to 15:00 for a 'Design Review'.";

    /// <summary>Scenario 3: the agent calls send_notification, then get_notifications_for_recipient.</summary>
    public const string SendAndShowNotifications =
        "Send a notification to 'Mohammed' with the message 'Meeting at 3pm tomorrow'. Then show me all notifications for Mohammed.";

    /// <summary>
    /// Scenario 4: two tool calls traced by the middleware, a booking in two days.
    /// A format string: <c>{0}</c> is the date in two days (yyyy-MM-dd), given with
    /// <c>string.Format(CultureInfo.InvariantCulture, Questions.EmployeeInfoAndBooking, inTwoDays)</c>.
    /// </summary>
    public const string EmployeeInfoAndBooking =
        "Get the information of the employee EMP003, then book the room ROOM-B for this employee on {0} from 14:00 to 15:00 for a 'Design Review'.";
}
