using System.ClientModel;
using System.ClientModel.Primitives;
using System.ComponentModel;
using System.Globalization;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using OpenAI.Chat;
using CommonUtilities;

using AIAgentWithFunctionTools;
using AIAgentWithFunctionTools.Repositories;
using AIAgentWithFunctionTools.Tools;
using static AIAgentWithFunctionTools.AgentConsole;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// Set to: [1], [2], [3], [4] or [1, 2, 3, 4] to run specific scenarios
HashSet<int> scenariosToRun = [1, 2, 3, 4];
// ============================================

bool ShouldRunScenario(int scenario) => scenariosToRun.Count == 0 || scenariosToRun.Contains(scenario);

#region Setup: Configuration and Client Initialization

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
Console.WriteLine($"Endpoint: {settings.Endpoint}");
Console.WriteLine($"Deployment: {settings.ChatDeploymentName}");

// TODO 1: Create the OpenAIClientOptions that point to the Azure OpenAI v1 endpoint
// Hint: AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint) turns "https://<resource>.openai.azure.com/" into ".../openai/v1/"
// OpenAIClientOptions clientOptions = ...

// TODO 2: Create the OpenAIClient
// - If settings.APIKey is set: authenticate with an ApiKeyCredential
// - Otherwise: authenticate with Microsoft Entra ID using a BearerTokenPolicy built from
//   DefaultAzureCredential and the scope "https://ai.azure.com/.default"
// Note: the AuthenticationPolicy constructor is flagged experimental (OPENAI001), keep the pragma around it.
#pragma warning disable OPENAI001
// OpenAIClient client = ...
#pragma warning restore OPENAI001

// TODO 3: Get a ChatClient (Chat Completions API) for the deployment
// ChatClient chatClient = ...

#endregion

#region Scenario 1: Function tools calling - basic

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: Function tools calling - basic ===");

    // TODO 4: Create a CompanyTools instance and turn three of its methods into tools
    // Hint: AIFunctionFactory.Create(companyTools.GetEmployeeInfo, "get_employee_info"), same for GetMeetingRooms
    //       ("get_meeting_rooms") and BookMeetingRoom ("book_meeting_room")
    // var companyTools = ...
    // List<AITool> tools = [...];

    // TODO 5: Display the name and the description of each tool (the description comes from [Description] in CompanyTools.cs)
    // Hint: ColoredConsole.WritePrimaryLogLine("Tools available to the agent:"), then for each tool:
    //       ColoredConsole.WriteSecondaryLogLine($"- {tool.Name}: {tool.Description}")

    // TODO 6: Create the agent with its tools
    // Hint: chatClient.AsAIAgent(instructions: "You are a helpful assistant that can help with company tasks.", name: "CompanyAssistant", tools: tools)
    // AIAgent companyAgent = ...

    // TODO 7: Run the agent and display the answer
    // Hint: await companyAgent.RunAsync("Get the information of the employee with the ID EMP001").WithSpinner("Running agent"),
    //       then ColoredConsole.WriteSecondaryLogLine(response.Text)
    // AgentResponse response = ...

    // TODO 8: Display token usage
    // Hint: WriteTokenUsage(response)
}

#endregion

#region Scenario 2: Function tools calling - a plugin that declares its tools

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Function tools calling - a plugin that declares its tools ===");

    // TODO 9: Create the plugin (MeetingRoomPlugin, at the end of this file) from a new CompanyTools, get its tools,
    // and display their names
    // Hint: MeetingRoomPlugin plugin = new(new CompanyTools()); List<AITool> tools = [.. plugin.AsAITools()];
    //       ColoredConsole.WriteSecondaryLogLine($"Tools declared by the plugin: {string.Join(", ", tools.Select(tool => tool.Name))}")

    // TODO 10 and TODO 11: complete MeetingRoomPlugin at the end of this file

    // TODO 12: Create the agent with the tools of the plugin (same instructions and name as TODO 6)
    // AIAgent companyAgent = ...

    // TODO 13: Run the agent (see the README for the prompt: one booking in the past, one tomorrow), then display
    // what the tools returned with WriteToolResults(response), and the answer
    // Hint: string tomorrow = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    // AgentResponse response = ...

    // TODO 14: Display token usage
}

#endregion

#region Scenario 3: Function tools calling - static tools with dependency injection

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: Function tools calling - static tools with DI ===");

    // TODO 15: Register the services needed by the tools in a ServiceCollection
    // Hint: services.AddSingleton<INotificationRepository, InMemoryNotificationRepository>()
    // ServiceCollection services = new();

    // TODO 16: Build the service provider
    // IServiceProvider serviceProvider = ...

    // TODO 17: Create the agent with the static tools of NotificationTools and the service provider
    // Hint: tools: [AIFunctionFactory.Create(NotificationTools.SendNotification, "send_notification"), ...],
    //       services: serviceProvider
    // AIAgent notificationAgent = ...

    // TODO 18: Run the agent and display the answer (see the README for the prompt)
    // AgentResponse response = ...

    // TODO 19: Display token usage
}

#endregion

#region Scenario 4: Function calling middleware - trace every tool call

if (ShouldRunScenario(4))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 4: Function calling middleware ===");

    // TODO 20: Write the FunctionCallMiddleware local function at the end of this file (see the skeleton there)

    // TODO 21: Create the same agent as in scenario 1, then wrap it with the middleware
    // Hint: companyAgent.AsBuilder().Use(FunctionCallMiddleware).Build()
    // AIAgent tracedAgent = ...

    // TODO 22: Run the wrapped agent and display the answer (see the README for the prompt)
    // Don't use .WithSpinner() here: the middleware writes to the console during the run
    // AgentResponse response = ...

    // TODO 23: Display token usage
}

#endregion

// TODO 20: Function calling middleware, called for every tool call made by the agent
// async ValueTask<object?> FunctionCallMiddleware(
//     AIAgent agent,
//     FunctionInvocationContext context,
//     Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
//     CancellationToken cancellationToken)
// {
//     1. Write "[Middleware] Calling <function name>(<arguments>)" (context.Function.Name, context.Arguments)
//     2. object? result = await next(context, cancellationToken);
//     3. Write "[Middleware] <function name> returned: <first line of the result>"
//     4. return result;
// }


#region Plugin: a class that declares its tools and checks the arguments chosen by the model

/// <summary>
/// A plugin (official sample Agent_Step12_Plugins): the class declares which of its methods are tools (<see cref="AsAITools"/>),
/// instead of exposing every public method by reflection. It is also where the arguments chosen by the model are checked
/// before they reach the company systems (<see cref="CompanyTools"/>): for the application, they are untrusted input.
/// </summary>
internal sealed class MeetingRoomPlugin(CompanyTools companyTools)
{
    [Description("Lists all available meeting rooms with their capacity and features.")]
    public string GetMeetingRooms() => companyTools.GetMeetingRooms();

    [Description("Books a meeting room for a specific date, time, and subject.")]
    public string BookMeetingRoom(
        [Description("The room ID (e.g., ROOM-A)")] string roomId,
        [Description("The employee ID making the booking")] string employeeId,
        [Description("The date (format: yyyy-MM-dd)")] string date,
        [Description("Start time (format: HH:mm)")] string startTime,
        [Description("End time (format: HH:mm)")] string endTime,
        [Description("Meeting subject/title")] string subject)
    {
        // TODO 11: The arguments come from the model: check the date before booking, and explain the problem in the
        // returned text so that the model can react
        // - not a date in the format yyyy-MM-dd (DateOnly.TryParseExact with CultureInfo.InvariantCulture): "Cannot book: '...' is not a date in the format yyyy-MM-dd."
        // - a day before today (DateOnly.FromDateTime(DateTime.Today)): "Cannot book: <date> is in the past (today is <today>)."

        return companyTools.BookMeetingRoom(roomId, employeeId, date, startTime, endTime, subject);
    }

    /// <summary>The tools of the plugin: the model can only call the methods listed here.</summary>
    public IEnumerable<AITool> AsAITools()
    {
        // TODO 10: Declare the tools of the plugin: GetMeetingRooms as "get_meeting_rooms", BookMeetingRoom as "book_meeting_room"
        // Hint: yield return AIFunctionFactory.Create(GetMeetingRooms, "get_meeting_rooms");
        yield break;
    }
}

#endregion
