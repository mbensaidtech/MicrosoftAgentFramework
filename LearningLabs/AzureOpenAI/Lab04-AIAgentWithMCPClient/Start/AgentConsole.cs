using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Responses;
using CommonUtilities;

namespace AIAgentWithMCPClient;

/// <summary>
/// Console output helpers shared by the scenarios, so that Program.cs focuses on MCP.
/// </summary>
public static class AgentConsole
{
    /// <summary>
    /// Displays the token usage of an agent run.
    /// With MCP tools, one run makes several model calls (the model asks for the tools, then answers
    /// with their results): the usage of the response is the total of all these calls.
    /// </summary>
    public static void WriteTokenUsage(AgentResponse response)
    {
        ColoredConsole.WriteDividerLine();
        ColoredConsole.WritePrimaryLogLine("Token Usage:");
        ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {response.Usage?.InputTokenCount}");
        ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {response.Usage?.OutputTokenCount}");
        ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {response.Usage?.TotalTokenCount}");
    }

    /// <summary>
    /// Scenarios 4 and 5: displays the tool calls of a run and <b>who ran them</b>, from the contents kept in the response messages:
    /// <list type="bullet">
    /// <item><see cref="FunctionCallContent"/>: a function call requested by the model and run by this application
    /// (the <c>McpClientTool</c> sends <c>tools/call</c> to the MCP server);</item>
    /// <item><see cref="McpServerToolCallContent"/>: a call to a hosted MCP server, run by the service (Responses API);</item>
    /// <item>a <c>mcp_list_tools</c> item: the service listed the tools of the hosted MCP server itself.</item>
    /// </list>
    /// </summary>
    public static void WriteToolCalls(AgentResponse response)
    {
        ColoredConsole.WritePrimaryLogLine("Tool calls of the run:");
        int count = 0;
        foreach (AIContent content in response.Messages.SelectMany(message => message.Contents))
        {
            switch (content)
            {
                case FunctionCallContent call:
                    ColoredConsole.WriteSecondaryLogLine($"- {call.Name}({FormatArguments(call.Arguments)}) - run by this application, through its MCP client");
                    count++;
                    break;
                case McpServerToolCallContent call:
                    ColoredConsole.WriteSecondaryLogLine($"- {call.ServerName}/{call.Name}({FormatArguments(call.Arguments)}) - run by the service (hosted MCP)");
                    count++;
                    break;
                default:
                    // The tool discovery of a hosted MCP server has no Microsoft.Extensions.AI type yet: read the OpenAI item.
                    // McpToolDefinitionListItem is still flagged [Experimental] (OPENAI001) by the OpenAI SDK.
#pragma warning disable OPENAI001
                    if (content.RawRepresentation is McpToolDefinitionListItem toolList)
                    {
                        ColoredConsole.WriteSecondaryLogLine(
                            $"- MCP tools listed by the service ({toolList.ServerLabel}): {string.Join(", ", toolList.ToolDefinitions.Select(tool => tool.Name))}");
                    }
#pragma warning restore OPENAI001
                    break;
            }
        }

        if (count == 0)
        {
            ColoredConsole.WriteWarningLine("- (no tool called)");
        }
    }

    private static string FormatArguments(IDictionary<string, object?>? arguments) =>
        string.Join(", ", arguments?.Select(argument => $"{argument.Key}: {argument.Value}") ?? []);
}
