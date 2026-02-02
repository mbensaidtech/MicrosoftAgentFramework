using CommonUtilities;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AIAgentsBackend.Middlewares.Agent;

/// <summary>
/// Middleware that logs function/tool calls made by AI agents.
/// Useful for debugging and monitoring agent behavior.
/// </summary>
public static class FunctionCallLoggingMiddleware
{
    /// <summary>
    /// Logs function call details including name and arguments before execution.
    /// </summary>
    /// <param name="callingAgent">The AI agent making the function call</param>
    /// <param name="context">Context information about the function being invoked</param>
    /// <param name="next">The next middleware in the pipeline</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The result of the function invocation</returns>
    public static async ValueTask<object?> LogFunctionCallAsync(
        AIAgent callingAgent,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        var toolName = context.Function.Name;
        var hasArguments = context.Arguments.Any();

        if (hasArguments)
        {
            var argumentsList = context.Arguments
                .Select(arg => $"{arg.Key}={FormatArgumentValue(arg.Value)}")
                .ToArray();
            
            ColoredConsole.WriteSecondaryLogLine($"🔧 Tool: {toolName} | Args: {string.Join(", ", argumentsList)}");
        }
        else
        {
            ColoredConsole.WriteSecondaryLogLine($"🔧 Tool: {toolName}");
        }

        return await next(context, cancellationToken);
    }

    private static string FormatArgumentValue(object? value)
    {
        if (value == null) return "null";
        if (value is string str) return $"\"{str}\"";
        return value.ToString() ?? "null";
    }
}
