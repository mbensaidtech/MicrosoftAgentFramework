using System.ComponentModel;

namespace OrchestrationSequential.Tools;

// ============================================
// PROVIDED - do not edit
// ============================================

/// <summary>
/// The mail system of the team (mocked: nothing is sent). Sending an email is an action with a side effect that cannot be undone:
/// in scenario 3, the tool is wrapped in an <c>ApprovalRequiredAIFunction</c>, so the workflow asks before it runs.
/// </summary>
public static class EmailTools
{
    [Description("Sends an email to the team.")]
    public static string SendEmail(
        [Description("The recipients, separated by commas.")] string to,
        [Description("The subject of the email.")] string subject,
        [Description("The body of the email.")] string body)
    {
        return $"Email sent to {to} (subject: {subject}, {body.Length} characters).";
    }
}
