using System.ComponentModel;
using System.Globalization;

namespace AIAgentWithFunctionToolsHumanApproval.Tools;

/// <summary>
/// HR tools given to the agent. <see cref="GetEmployeeInfo"/> is harmless and runs without approval;
/// <see cref="DeleteEmployeeData"/> is sensitive: Program.cs wraps it in an <c>ApprovalRequiredAIFunction</c>
/// so that the agent pauses and asks for approval before it is executed.
/// </summary>
public static class HrTools
{
    [Description("Gets the name, department and employment status of an employee from the HR directory.")]
    public static string GetEmployeeInfo(
        [Description("The employee ID, e.g. EMP001")] string employeeId)
    {
        Employee? employee = EmployeeDirectory.Find(employeeId);
        if (employee is null)
        {
            return $"No employee found with the ID '{employeeId}'.";
        }

        string status = employee.LeftOn is { } leftOn
            ? $"left the company on {leftOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}"
            : "currently employed";

        return $"Employee {employee.Id}: {employee.Name}, {employee.Department}, {status}.";
    }

    [Description("Permanently deletes all the data of an employee. This is a SENSITIVE operation that cannot be undone.")]
    public static string DeleteEmployeeData(
        [Description("The employee ID to delete all data for")] string employeeId)
    {
        // Simulation - in a real application this would delete the data for good.
        return $"Sensitive operation executed: All data for employee '{employeeId}' has been permanently deleted. This action cannot be undone.";
    }
}
