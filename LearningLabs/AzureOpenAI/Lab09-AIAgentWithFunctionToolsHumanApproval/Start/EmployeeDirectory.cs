namespace AIAgentWithFunctionToolsHumanApproval;

/// <summary>An employee of the (simulated) HR directory.</summary>
/// <param name="Id">Employee ID, e.g. EMP001.</param>
/// <param name="Name">Full name.</param>
/// <param name="Department">Department.</param>
/// <param name="LeftOn">Date the employee left the company, or <see langword="null"/> when still employed.</param>
public sealed record Employee(string Id, string Name, string Department, DateOnly? LeftOn);

/// <summary>
/// The HR directory used by the tools (<see cref="Tools.HrTools"/>) and by the approval policy (<see cref="DeletionPolicy"/>).
/// In-memory data: nothing is really deleted by this lab.
/// </summary>
public static class EmployeeDirectory
{
    private static readonly Dictionary<string, Employee> Employees = new(StringComparer.OrdinalIgnoreCase)
    {
        ["EMP001"] = new("EMP001", "Alice Martin", "Engineering", new DateOnly(2025, 12, 31)),
        ["EMP002"] = new("EMP002", "Bob Lee", "Finance", null),
        ["EMP003"] = new("EMP003", "Chen Wei", "Marketing", new DateOnly(2026, 3, 15)),
    };

    /// <summary>Finds an employee by ID (case-insensitive), or returns <see langword="null"/>.</summary>
    public static Employee? Find(string employeeId) =>
        Employees.TryGetValue(employeeId.Trim(), out Employee? employee) ? employee : null;
}
