using System.ClientModel;
using System.ClientModel.Primitives;
using System.Reflection;
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

// Step 2: Create an OpenAIClient that targets the Azure OpenAI v1 endpoint (https://<resource>.openai.azure.com/openai/v1/)
// - API key when one is configured
// - otherwise Microsoft Entra ID: DefaultAzureCredential + BearerTokenPolicy (scope https://ai.azure.com/.default)
// WARNING: DefaultAzureCredential is convenient for development. In production, prefer a specific credential
// (e.g. ManagedIdentityCredential) to avoid latency and unintended credential probing.
OpenAIClientOptions clientOptions = new() { Endpoint = AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint) };

// The OpenAIClient(AuthenticationPolicy, ...) constructor is still flagged [Experimental] (OPENAI001) by the OpenAI SDK,
// although it is the pattern documented by Microsoft for Entra ID with the Azure OpenAI v1 API.
#pragma warning disable OPENAI001
OpenAIClient client = !string.IsNullOrWhiteSpace(settings.APIKey)
    ? new OpenAIClient(new ApiKeyCredential(settings.APIKey), clientOptions)
    : new OpenAIClient(new BearerTokenPolicy(new DefaultAzureCredential(), "https://ai.azure.com/.default"), clientOptions);
#pragma warning restore OPENAI001

// Step 3: Get a ChatClient (Chat Completions API) for the deployment
ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);

#endregion

#region Scenario 1: Function tools calling - basic

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: Function tools calling - basic ===");

    // Step 1: Turn methods of a CompanyTools instance into tools, with the names the model will see
    var companyTools = new CompanyTools();
    List<AITool> tools =
    [
        AIFunctionFactory.Create(companyTools.GetEmployeeInfo, "get_employee_info"),
        AIFunctionFactory.Create(companyTools.GetMeetingRooms, "get_meeting_rooms"),
        AIFunctionFactory.Create(companyTools.BookMeetingRoom, "book_meeting_room")
    ];

    // Step 2: Display what the model receives for each tool: its name and the description taken from [Description]
    ColoredConsole.WritePrimaryLogLine("Tools available to the agent:");
    foreach (AITool tool in tools)
    {
        ColoredConsole.WriteSecondaryLogLine($"- {tool.Name}: {tool.Description}");
    }

    // Step 3: Create the agent with its tools
    AIAgent companyAgent = chatClient.AsAIAgent(
        instructions: "You are a helpful assistant that can help with company tasks.",
        name: "CompanyAssistant",
        tools: tools);

    // Step 4: Run the agent (with spinner to show loading): it calls get_employee_info, then answers with the result
    AgentResponse response = await companyAgent.RunAsync("Get the information of the employee with the ID EMP001").WithSpinner("Running agent");
    ColoredConsole.WriteSecondaryLogLine(response.Text);

    // Step 5: Display token usage (total of every model call made during the run)
    WriteTokenUsage(response);
}

#endregion

#region Scenario 2: Function tools calling - using reflection

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Function tools calling - using reflection ===");

    // Step 1: Get all public instance methods declared by CompanyTools (private helpers are not exposed)
    var companyTools = new CompanyTools();
    MethodInfo[] methods = typeof(CompanyTools).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

    // Step 2: Create one tool per method; the target instance is passed next to the MethodInfo
    // (no name given: the tool is named after the method)
    List<AITool> tools = [.. methods.Select(method => AIFunctionFactory.Create(method, companyTools))];

    // Step 3: Display the tools that will be given to the agent
    ColoredConsole.WriteSecondaryLogLine($"Tools that will be available to the agent: {string.Join(", ", tools.Select(tool => tool.Name))}");

    // Step 4: Create the agent with the discovered tools
    AIAgent companyAgent = chatClient.AsAIAgent(
        instructions: "You are a helpful assistant that can help with company tasks.",
        name: "CompanyAssistant",
        tools: tools);

    // Step 5: Run the agent (with spinner to show loading): it lists the rooms, then books one
    AgentResponse response = await companyAgent.RunAsync(
        "What are the available meeting rooms? Then book the room ROOM-A for the employee with the ID EMP001 on 2025-12-16 from 10:00 to 11:00 for a 'Sprint Review'.").WithSpinner("Running agent");
    ColoredConsole.WriteSecondaryLogLine(response.Text);

    // Step 6: Display token usage
    WriteTokenUsage(response);
}

#endregion

#region Scenario 3: Function tools calling - static tools with dependency injection

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: Function tools calling - static tools with DI ===");

    // Step 1: Register the services needed by the tools in a ServiceCollection
    ServiceCollection services = new();
    services.AddSingleton<INotificationRepository, InMemoryNotificationRepository>();

    // Step 2: Build the service provider
    IServiceProvider serviceProvider = services.BuildServiceProvider();

    // Step 3: Create the agent with static tools and give it the service provider:
    // the IServiceProvider parameter of each tool is filled by the framework and hidden from the model
    AIAgent notificationAgent = chatClient.AsAIAgent(
        instructions: "You are a helpful assistant that can send and retrieve notifications.",
        name: "NotificationAssistant",
        tools:
        [
            AIFunctionFactory.Create(NotificationTools.SendNotification, "send_notification"),
            AIFunctionFactory.Create(NotificationTools.GetNotificationsForRecipient, "get_notifications_for_recipient")
        ],
        services: serviceProvider);

    // Step 4: Run the agent (with spinner to show loading): send a notification, then read it back
    AgentResponse response = await notificationAgent.RunAsync(
        "Send a notification to 'Mohammed' with the message 'Meeting at 3pm tomorrow'. Then show me all notifications for Mohammed.").WithSpinner("Running agent");
    ColoredConsole.WriteSecondaryLogLine(response.Text);

    // Step 5: Display token usage
    WriteTokenUsage(response);
}

#endregion

#region Scenario 4: Function calling middleware - trace every tool call

if (ShouldRunScenario(4))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 4: Function calling middleware ===");

    // Step 1: Create the same agent as in scenario 1
    var companyTools = new CompanyTools();
    AIAgent companyAgent = chatClient.AsAIAgent(
        instructions: "You are a helpful assistant that can help with company tasks.",
        name: "CompanyAssistant",
        tools:
        [
            AIFunctionFactory.Create(companyTools.GetEmployeeInfo, "get_employee_info"),
            AIFunctionFactory.Create(companyTools.GetMeetingRooms, "get_meeting_rooms"),
            AIFunctionFactory.Create(companyTools.BookMeetingRoom, "book_meeting_room")
        ]);

    // Step 2: Wrap it with the function calling middleware defined at the end of this file
    AIAgent tracedAgent = companyAgent
        .AsBuilder()
        .Use(FunctionCallMiddleware)
        .Build();

    // Step 3: Run the wrapped agent (no spinner: the middleware writes to the console during the run)
    AgentResponse response = await tracedAgent.RunAsync(
        "Get the information of the employee EMP003, then book the room ROOM-B for this employee on 2026-10-15 from 14:00 to 15:00 for a 'Design Review'.");
    ColoredConsole.WriteSecondaryLogLine(response.Text);

    // Step 4: Display token usage
    WriteTokenUsage(response);
}

#endregion

// Function calling middleware: called for every tool call made by the agent.
// It sees the function and the arguments chosen by the model, calls the next step of the pipeline (in the end, the tool),
// and can inspect or replace the result before it is sent back to the model.
async ValueTask<object?> FunctionCallMiddleware(
    AIAgent agent,
    FunctionInvocationContext context,
    Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
    CancellationToken cancellationToken)
{
    string arguments = string.Join(", ", context.Arguments.Select(argument => $"{argument.Key}: {argument.Value}"));
    ColoredConsole.WriteWarningLine($"[Middleware] Calling {context.Function.Name}({arguments})");

    object? result = await next(context, cancellationToken);

    ColoredConsole.WriteWarningLine($"[Middleware] {context.Function.Name} returned: {result?.ToString()?.Split('\n')[0]}");
    return result;
}
