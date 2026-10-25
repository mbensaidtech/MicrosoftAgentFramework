using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using CommonUtilities;

namespace AIAgentWithSkills;

/// <summary>
/// Console output helpers for skills, so that Program.cs focuses on the skills API.
/// </summary>
public static class SkillsConsole
{
    /// <summary>
    /// Displays the skills found in a folder of file-based skills: what the provider advertises to the model
    /// (name and description only, the body of SKILL.md is loaded on demand).
    /// </summary>
    public static async Task WriteFileSkillsAsync(string skillsPath, AIAgent agent)
    {
        AgentFileSkillsSource source = new(skillsPath);
        foreach (AgentSkill skill in await source.GetSkillsAsync(new AgentSkillsSourceContext(agent, session: null)))
        {
            ColoredConsole.WriteSecondaryLogLine($"Skill advertised: {skill.Frontmatter.Name} - {skill.Frontmatter.Description}");
        }
    }

    /// <summary>
    /// Function calling middleware (as in Lab 03): called for every skill tool the agent actually runs
    /// (load_skill, read_skill_resource, run_skill_script), after its approval. It writes the call, its arguments
    /// and the first line of its result. A rejected call never runs, so it is never written here.
    /// </summary>
    /// <summary>Number of skill tools run since the start of the program (written by <see cref="TraceSkillCall"/>).</summary>
    public static int SkillCallCount { get; private set; }

    public static async ValueTask<object?> TraceSkillCall(
        AIAgent agent,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        object? result = await next(context, cancellationToken);
        SkillCallCount++;
        ColoredConsole.WriteWarningLine($"[Skill] {FormatCall(context.CallContent)} -> {FirstLine(result)}");
        return result;
    }

    /// <summary>Formats a tool call as name(argument: value, ...).</summary>
    public static string FormatCall(FunctionCallContent call) =>
        $"{call.Name}({string.Join(", ", (call.Arguments ?? new Dictionary<string, object?>()).Select(argument => $"{argument.Key}: {OneLine(argument.Value)}"))})";

    private static string FirstLine(object? value)
    {
        string text = OneLine(value);
        return text.Length <= 110 ? text : text[..107] + "...";
    }

    private static string OneLine(object? value) =>
        string.Join(' ', (value?.ToString() ?? string.Empty).Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
}
