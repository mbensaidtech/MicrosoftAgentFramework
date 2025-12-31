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
using AIAgentWithFunctionTools;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// Set to: [1], [2], or [1, 2] to run specific scenarios
HashSet<int> scenariosToRun = [3];
// ============================================

bool ShouldRunScenario(int scenario) => scenariosToRun.Count == 0 || scenariosToRun.Contains(scenario);

#region Setup: Configuration and Client Initialization

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
Console.WriteLine($"Endpoint: {settings.Endpoint}");
Console.WriteLine($"Deployment: {settings.ChatDeploymentName}");

// Step 2: Create AzureOpenAIClient with managed identity authentication
AzureOpenAIClient client = new AzureOpenAIClient(new Uri(settings.Endpoint), new DefaultAzureCredential());

// Step 3: Get a ChatClient for the specific deployment
ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);

#endregion

ColoredConsole.WriteDividerLine();

#region Scenario 1: Function tools calling - basic.

if (ShouldRunScenario(1))
{
    var companyTools = new CompanyTools();

    ChatClientAgent basicFunctionToolsAgent = chatClient.CreateAIAgent(instructions: "you are a helpful assistant that can help with company tasks", tools:[
     AIFunctionFactory.Create(companyTools.GetEmployeeInfo,"get_employee_info"),
     AIFunctionFactory.Create(companyTools.GetMeetingRooms,"get_meeting_rooms"),
     AIFunctionFactory.Create(companyTools.BookMeetingRoom,"book_meeting_room")]);

    AgentRunResponse basicFunctionToolsResponse = await basicFunctionToolsAgent.RunAsync("Get the information of the employee with the ID EMP001");

    // Step 4: Display the response
    ColoredConsole.WriteInfoLine("=== Scenario 1: Function tools calling - basic ===");
    ColoredConsole.WriteSecondaryLogLine(basicFunctionToolsResponse.ToString());

    // Step 5: Display token usage
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WritePrimaryLogLine("Token Usage: ");
    ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {basicFunctionToolsResponse.Usage?.InputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {basicFunctionToolsResponse.Usage?.OutputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {basicFunctionToolsResponse.Usage?.TotalTokenCount}");
    
    ColoredConsole.WriteDividerLine();
}

#endregion

#region Scenario 2: Function tools calling - using reflection.

if (ShouldRunScenario(2))
{
    var companyTools2 = new CompanyTools();
    MethodInfo[] methods = typeof(CompanyTools).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
    List<AITool> tools = methods.Select(method => AIFunctionFactory.Create(method, companyTools2)).Cast<AITool>().ToList();


    ColoredConsole.WriteSecondaryLogLine($"Tools that will be available to the agent: {string.Join(", ", tools.Select(tool => tool.Name))}");

    ChatClientAgent reflectionFunctionToolsAgent = chatClient.CreateAIAgent(instructions: "you are a helpful assistant that can help with company tasks", tools: tools);

    AgentRunResponse reflectionFunctionToolsResponse = await reflectionFunctionToolsAgent.RunAsync("what are the available meeting rooms? and book the room ROOM-A for the employee with the ID EMP001 for the date 2025-12-16 from 10:00 to 11:00");

    // Step 4: Display the response
    ColoredConsole.WriteInfoLine("=== Scenario 2: Function tools calling - using reflection ===");
    ColoredConsole.WriteSecondaryLogLine(reflectionFunctionToolsResponse.ToString());

    // Step 5: Display token usage
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WritePrimaryLogLine("Token Usage: ");
    ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {reflectionFunctionToolsResponse.Usage?.InputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {reflectionFunctionToolsResponse.Usage?.OutputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {reflectionFunctionToolsResponse.Usage?.TotalTokenCount}");

    ColoredConsole.WriteDividerLine();
}

#endregion

#region Scenario 3: Function tools calling with Approval step ( this feature is in evaluation phase ).
if (ShouldRunScenario(3))
{
    List<AITool> tools = [new ApprovalRequiredAIFunction(AIFunctionFactory.Create(SensitiveTools.DeleteEmployeeData, "delete_employee_data"))];

    ChatClientAgent approvalStepFunctionToolsAgent = chatClient.CreateAIAgent(instructions: "you are a helpful assistant that can help with company tasks", tools: tools);
    AgentRunResponse approvalStepFunctionToolsResponse = await approvalStepFunctionToolsAgent.RunAsync("delete the employee with the ID EMP001");

    List<UserInputRequest> userInputRequests = approvalStepFunctionToolsResponse.UserInputRequests.ToList();
    if (userInputRequests.Count > 0)
    {
        List<ChatMessage> validations = userInputRequests.OfType<FunctionApprovalRequestContent>().Select(approvalRequest =>{
            return new ChatMessage(ChatRole.User, [approvalRequest.CreateResponse(true)]);
        }).ToList();

        approvalStepFunctionToolsResponse = await approvalStepFunctionToolsAgent.RunAsync(validations);
    }
    // Step 4: Display the response
    ColoredConsole.WriteInfoLine("=== Scenario 3: Function tools calling with Approval step ===");
    ColoredConsole.WriteSecondaryLogLine(approvalStepFunctionToolsResponse.ToString());

    // Step 5: Display token usage
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WritePrimaryLogLine("Token Usage: ");
    ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {approvalStepFunctionToolsResponse.Usage?.InputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {approvalStepFunctionToolsResponse.Usage?.OutputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {approvalStepFunctionToolsResponse.Usage?.TotalTokenCount}");
}

#endregion

#region Scenario 4: Function tools calling - static tools with dependency injection using ServiceCollection.

if (ShouldRunScenario(4))
{
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

    // Step 5: Run the agent - send a notification and then retrieve it
    AgentRunResponse notificationResponse = await notificationAgent.RunAsync(
        "Send a notification to 'Mohammed' with the message 'Meeting at 3pm tomorrow'. Then show me all notifications for Mohammed.");

    // Step 6: Display the response
    ColoredConsole.WriteInfoLine("=== Scenario 4: Function tools calling - static tools with DI ===");
    ColoredConsole.WriteSecondaryLogLine(notificationResponse.ToString());

    // Step 7: Display token usage
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WritePrimaryLogLine("Token Usage: ");
    ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {notificationResponse.Usage?.InputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {notificationResponse.Usage?.OutputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {notificationResponse.Usage?.TotalTokenCount}");
}

#endregion