using Azure.Identity;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using OpenAI.Chat;
using CommonUtilities;
using AIAgentWithFunctionTools.Tools;
using AIAgentWithFunctionTools.Repositories;
using System.Reflection;
using System.ClientModel;
using AIAgentWithFunctionTools;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// Set to: [1], [2], [3] or [1, 2, 3] to run specific scenarios
HashSet<int> scenariosToRun = [1, 2, 3];
// ============================================

bool ShouldRunScenario(int scenario) => scenariosToRun.Count == 0 || scenariosToRun.Contains(scenario);

#region Setup: Configuration and Client Initialization

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
ColoredConsole.WritePrimaryLogLine("Azure OpenAI Settings: ");
ColoredConsole.WriteSecondaryLogLine($"Azure OpenAI Endpoint: {settings.Endpoint}");
ColoredConsole.WriteSecondaryLogLine($"Azure OpenAI Chat Deployment Name: {settings.ChatDeploymentName}");

// Step 2: Create AzureOpenAIClient (API key or DefaultAzureCredential)
AzureOpenAIClient client = !string.IsNullOrEmpty(settings.APIKey)
    ? new AzureOpenAIClient(new Uri(settings.Endpoint), new ApiKeyCredential(settings.APIKey))
    : new AzureOpenAIClient(new Uri(settings.Endpoint), new DefaultAzureCredential());

// Step 3: Get a ChatClient for the specific deployment
ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);

#endregion


#region Scenario 1: Function tools calling - basic.

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: Function tools calling - basic ===");

    // Step 1: Create agent with tools
    var companyTools = new CompanyTools();

    ChatClientAgent basicFunctionToolsAgent = chatClient.CreateAIAgent(instructions: "you are a helpful assistant that can help with company tasks", tools:[
     AIFunctionFactory.Create(companyTools.GetEmployeeInfo,"get_employee_info"),
     AIFunctionFactory.Create(companyTools.GetMeetingRooms,"get_meeting_rooms"),
     AIFunctionFactory.Create(companyTools.BookMeetingRoom,"book_meeting_room")]);

    // Step 2: Run the agent with a prompt (with spinner to show loading)
    AgentRunResponse basicFunctionToolsResponse = await basicFunctionToolsAgent.RunAsync("Get the information of the employee with the ID EMP001").WithSpinner("Running agent");

    // Step 3: Display the response
    ColoredConsole.WriteSecondaryLogLine(basicFunctionToolsResponse.ToString());

    // Step 4: Display token usage
    ColoredConsole.WriteSectionSeparator();
    ColoredConsole.WriteBurgundyLine("Token Usage: ");
    ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {basicFunctionToolsResponse.Usage?.InputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {basicFunctionToolsResponse.Usage?.OutputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {basicFunctionToolsResponse.Usage?.TotalTokenCount}");
    
}

#endregion

#region Scenario 2: Function tools calling - using reflection.

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Function tools calling - using reflection ===");

    // Step 1: Create tools list using reflection
    // Step 1.0: Create an instance of CompanyTools
    var companyTools = new CompanyTools();
    // Step 1.1: Get all public instance methods from CompanyTools using reflection
    MethodInfo[] methods = typeof(CompanyTools).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
    // Step 1.2: Create a list of AITool from the methods using AIFunctionFactory.Create
    List<AITool> tools = methods.Select(method => AIFunctionFactory.Create(method, companyTools)).Cast<AITool>().ToList();
    // Step 1.3: Display the tools that we will inject to the agent
    ColoredConsole.WriteSecondaryLogLine($"Tools that will be available to the agent: {string.Join(", ", tools.Select(tool => tool.Name))}");

    // Step 2: Create agent with tools
    ChatClientAgent reflectionFunctionToolsAgent = chatClient.CreateAIAgent(instructions: "you are a helpful assistant that can help with company tasks", tools: tools);

    // Step 3: Run the agent with a prompt (with spinner to show loading)
    AgentRunResponse reflectionFunctionToolsResponse = await reflectionFunctionToolsAgent.RunAsync("what are the available meeting rooms? and book the room ROOM-A for the employee with the ID EMP001 for the date 2025-12-16 from 10:00 to 11:00").WithSpinner("Running agent");

    // Step 4: Display the response
    ColoredConsole.WriteSecondaryLogLine(reflectionFunctionToolsResponse.ToString());

    // Step 5: Display token usage
    ColoredConsole.WriteSectionSeparator();
    ColoredConsole.WriteBurgundyLine("Token Usage: ");
    ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {reflectionFunctionToolsResponse.Usage?.InputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {reflectionFunctionToolsResponse.Usage?.OutputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {reflectionFunctionToolsResponse.Usage?.TotalTokenCount}");
}

#endregion

#region Scenario 3: Function tools calling - static tools with dependency injection using ServiceCollection.

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: Function tools calling - static tools with DI ===");

    // Step 1: Configure services using ServiceCollection
    var services = new ServiceCollection();
    services.AddSingleton<INotificationRepository, InMemoryNotificationRepository>();
    
    // Step 2: Build the ServiceProvider
    var serviceProvider = services.BuildServiceProvider();

    ChatClientAgent notificationAgent = chatClient.CreateAIAgent(
        instructions: "You are a helpful assistant that can send and retrieve notifications.", 
        tools: [
        AIFunctionFactory.Create(NotificationTools.SendNotification, "send_notification"),
        AIFunctionFactory.Create(NotificationTools.GetNotificationsForRecipient, "get_notifications_for_recipient")
    ], services: serviceProvider);

    // Step 3: Run the agent - send a notification and then retrieve it (with spinner to show loading)
    AgentRunResponse notificationResponse = await notificationAgent.RunAsync(
        "Send a notification to 'Mohammed' with the message 'Meeting at 3pm tomorrow'. Then show me all notifications for Mohammed.").WithSpinner("Running agent");

    // Step 4: Display the response
    ColoredConsole.WriteSecondaryLogLine(notificationResponse.ToString());

    // Step 5: Display token usage
    ColoredConsole.WriteSectionSeparator();
    ColoredConsole.WriteBurgundyLine("Token Usage: ");
    ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {notificationResponse.Usage?.InputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {notificationResponse.Usage?.OutputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {notificationResponse.Usage?.TotalTokenCount}");
}

#endregion