using System.Globalization;
using Microsoft.Extensions.AI;

namespace AIAgentWithFunctionToolsHumanApproval;

/// <summary>The outcome of an approval decision, with the reason given to the model when the call is rejected.</summary>
public sealed record ApprovalDecision(bool Approved, string Reason);

/// <summary>
/// Approval policy of scenario 2: the decision is taken by code instead of a person.
/// Only the data of employees who already left the company may be deleted.
/// </summary>
public static class DeletionPolicy
{
    /// <summary>Decides whether the <c>delete_employee_data</c> call chosen by the model may be executed.</summary>
    /// <param name="call">The function call that requires approval (name + arguments chosen by the model).</param>
    public static ApprovalDecision Decide(FunctionCallContent call)
    {
        if (call.Arguments is null || !call.Arguments.TryGetValue("employeeId", out object? value) || value?.ToString() is not { Length: > 0 } employeeId)
        {
            return new ApprovalDecision(false, "The employee ID is missing from the call.");
        }

        Employee? employee = EmployeeDirectory.Find(employeeId);
        if (employee is null)
        {
            return new ApprovalDecision(false, $"No employee with the ID '{employeeId}' exists.");
        }

        if (employee.LeftOn is not { } leftOn)
        {
            return new ApprovalDecision(false, $"{employee.Id} ({employee.Name}) is still employed: only the data of former employees can be deleted.");
        }

        return new ApprovalDecision(true, $"{employee.Id} ({employee.Name}) left the company on {leftOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.");
    }
}
