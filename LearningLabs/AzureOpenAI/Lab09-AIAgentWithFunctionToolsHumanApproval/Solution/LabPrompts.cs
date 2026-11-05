namespace AIAgentWithFunctionToolsHumanApproval;

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
    /// Scenarios 1, 2 and 3: the same HR assistant in every scenario, only the way the approval decision is taken changes.
    /// The instructions are displayed at the start of Program.cs.
    /// </summary>
    public static class HrAssistant
    {
        public const string Name = "HrAssistant";

        public const string Instructions =
            "You are an HR assistant. Use the tools to look up employees and to delete employee data when asked. " +
            "Only report a deletion as done when the delete_employee_data tool confirmed it; if a deletion was rejected, say so and give the reason.";
    }
}

/// <summary>The questions to ask the agents.</summary>
public static class Questions
{
    /// <summary>Scenario 1: one call to delete_employee_data, approved or rejected by you at the console.</summary>
    public const string DeleteDataOfEmp001 = "Delete all the data of the employee with the ID EMP001.";

    /// <summary>
    /// Scenario 2: two employees, so two calls to delete_employee_data, each one decided by DeletionPolicy
    /// (EMP001 is still employed, EMP002 left the company).
    /// </summary>
    public const string EraseDataOfEmp001AndEmp002 =
        "The employees EMP001 and EMP002 asked us to erase their data. Look up both employees, then delete the data of each of them.";

    /// <summary>Scenario 3: a forbidden topic (the salary): the guardrail answers by itself, the agent is never called.</summary>
    public const string SalaryOfEmp002 = "What is the salary of the employee EMP002?";

    /// <summary>Scenario 3: a phone number and an e-mail address, masked by the guardrail before the agent sees them.</summary>
    public const string CallMeBackWithPersonalData =
        "My phone number is +33 6 12 34 56 78 and my e-mail is ada.lovelace@example.com. " +
        "Call me back with the status of the employee EMP001.";
}

/// <summary>The answers of the guardrail middleware (HrGuardrail, at the end of Program.cs).</summary>
public static class GuardrailAnswers
{
    /// <summary>Scenario 3: the answer of the guardrail to a forbidden topic, given without calling the agent.</summary>
    public const string ForbiddenTopic = "I can't help with salaries or passwords. Please contact the HR department directly.";
}
