using System.ComponentModel;

namespace AIAgentWithFunctionToolsHumanApproval.Tools;

/// <summary>
/// Tools that perform sensitive operations requiring approval.
/// </summary>
public class SensitiveTools
{
    [Description("Deletes all data for an employee. This is a SENSITIVE operation that cannot be undone.")]
    public static string DeleteEmployeeData(
        [Description("The employee ID to delete all data for")] string employeeId)
    {
        // Simulation - in real scenario this would delete actual data
        return $"Sensitive operation executed: All data for employee '{employeeId}' has been permanently deleted. This action cannot be undone.";
    }
}

