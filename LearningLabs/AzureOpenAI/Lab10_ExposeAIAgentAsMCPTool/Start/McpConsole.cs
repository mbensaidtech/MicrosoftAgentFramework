using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using CommonUtilities;

namespace ExposeAIAgentAsMCPTool;

/// <summary>
/// Console output helpers shared by the scenarios, so that Program.cs focuses on exposing the agent with MCP.
/// </summary>
public static class McpConsole
{
    /// <summary>
    /// Displays the tools an MCP client sees: name, description and JSON schema of the input.
    /// This is all a model (or MCP Inspector) knows about the agent behind the tool.
    /// </summary>
    public static void WriteTools(IList<McpClientTool> tools)
    {
        ColoredConsole.WritePrimaryLogLine($"MCP tools available ({tools.Count}):");
        foreach (McpClientTool tool in tools)
        {
            ColoredConsole.WriteSecondaryLogLine($"- {tool.Name}: {tool.Description}");
            ColoredConsole.WriteSecondaryLogLine($"  Input schema: {tool.JsonSchema.GetRawText()}");
        }
    }

    /// <summary>
    /// Displays the result of an MCP tools/call request: the text blocks returned by the server.
    /// </summary>
    public static void WriteToolResult(CallToolResult result)
    {
        if (result.IsError == true)
        {
            ColoredConsole.WriteErrorLine("The tool reported an error:");
        }
        else
        {
            ColoredConsole.WritePrimaryLogLine("Tool result:");
        }

        foreach (TextContentBlock text in result.Content.OfType<TextContentBlock>())
        {
            ColoredConsole.WriteSecondaryLogLine(DecodeJsonString(text.Text));
        }
    }

    /// <summary>
    /// Displays the tools an agent called during a run and what they returned,
    /// by matching each FunctionResultContent with its FunctionCallContent (same CallId).
    /// </summary>
    public static void WriteToolCalls(AgentResponse response)
    {
        List<AIContent> contents = [.. response.Messages.SelectMany(message => message.Contents)];
        List<FunctionCallContent> calls = [.. contents.OfType<FunctionCallContent>()];
        if (calls.Count == 0)
        {
            ColoredConsole.WriteWarningLine("Tool calls: none");
            return;
        }

        foreach (FunctionCallContent call in calls)
        {
            string arguments = string.Join(", ", call.Arguments?.Select(argument => $"{argument.Key}: {argument.Value}") ?? []);
            ColoredConsole.WritePrimaryLogLine($"Tool called: {call.Name}({arguments})");

            FunctionResultContent? result = contents.OfType<FunctionResultContent>().FirstOrDefault(r => r.CallId == call.CallId);
            ColoredConsole.WriteSecondaryLogLine($"Tool result: {FormatResult(result?.Result)}");
        }
    }

    /// <summary>
    /// Displays the token usage of an agent run (total of every model call of the run).
    /// </summary>
    public static void WriteTokenUsage(AgentResponse response)
    {
        ColoredConsole.WriteDividerLine();
        ColoredConsole.WritePrimaryLogLine("Token Usage:");
        ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {response.Usage?.InputTokenCount}");
        ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {response.Usage?.OutputTokenCount}");
        ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {response.Usage?.TotalTokenCount}");
    }

    // An McpClientTool returns the content sent by the MCP server: a TextContent for a text block.
    private static string FormatResult(object? result) => result switch
    {
        null => "(none)",
        TextContent content => DecodeJsonString(content.Text),
        string text => DecodeJsonString(text),
        JsonElement { ValueKind: JsonValueKind.String } json => DecodeJsonString(json.GetString() ?? string.Empty),
        JsonElement json => json.GetRawText(),
        _ => result.ToString() ?? string.Empty
    };

    /// <summary>
    /// AIFunctionFactory serializes the return value of a function to JSON: the answer of the agent (a string) reaches
    /// the MCP client as a JSON string, with quotes and escaped line breaks ("...\n..."). It is decoded here for display only.
    /// </summary>
    private static string DecodeJsonString(string text)
    {
        if (text.Length < 2 || text[0] != '"' || text[^1] != '"')
        {
            return text;
        }

        try
        {
            return JsonSerializer.Deserialize<string>(text) ?? text;
        }
        catch (JsonException)
        {
            return text;
        }
    }
}
