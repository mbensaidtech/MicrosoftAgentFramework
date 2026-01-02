using Azure.Identity;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

using CommonUtilities;
using AIAgentWithFunctionToolsHumanApproval.Tools;
using AIAgentWithFunctionToolsHumanApproval;

#region Setup: Configuration and Client Initialization

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
ColoredConsole.WriteEmptyLine();
ColoredConsole.WritePrimaryLogLine("Azure OpenAI Settings: ");
ColoredConsole.WriteSecondaryLogLine($"Azure OpenAI Endpoint: {settings.Endpoint}");
ColoredConsole.WriteSecondaryLogLine($"Azure OpenAI Chat Deployment Name: {settings.ChatDeploymentName}");

// Step 2: Create AzureOpenAIClient with managed identity authentication
AzureOpenAIClient client = new AzureOpenAIClient(new Uri(settings.Endpoint), new DefaultAzureCredential());

// Step 3: Get a ChatClient for the specific deployment
ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);

#endregion

#region Function Tools Calling with Human Approval

ColoredConsole.WriteDividerLine();
ColoredConsole.WriteInfoLine("=== Function Tools Calling with Human Approval ===");
ColoredConsole.WriteSecondaryLogLine("This scenario demonstrates how to require human approval before executing sensitive operations.");
ColoredConsole.WriteEmptyLine();

// Step 1: Create tools list with approval requirement.
// The ApprovalRequiredAIFunction wrapper ensures that the function requires user approval before execution.
List<AITool> tools = [new ApprovalRequiredAIFunction(AIFunctionFactory.Create(SensitiveTools.DeleteEmployeeData, "delete_employee_data"))];

// Step 2: Create agent with tools
ChatClientAgent agent = chatClient.CreateAIAgent(
    instructions: "You are a helpful assistant that can help with company tasks. When asked to delete employee data, use the delete_employee_data function.", 
    tools: tools);

// Step 3: Run the agent with a prompt and thread to have a conversation (with spinner to show loading).
// The thread is used to maintain conversation context for the approval flow.
var thread = agent.GetNewThread();
AgentRunResponse response = await agent.RunAsync("delete the employee with the ID EMP001", thread).WithSpinner("Running agent");

// Step 4: Handle user approval for sensitive function calls.
// Step 4.0: Get user input requests from the response.
List<UserInputRequestContent> userInputRequests = response.UserInputRequests.ToList();
if (userInputRequests.Any())
{
    // Step 4.1: Process each function approval request and get user response.
    List<ChatMessage> userResponses = userInputRequests.OfType<FunctionApprovalRequestContent>().Select(functionApprovalRequest => {
        ColoredConsole.WriteWarningLine($"APPROVAL REQUIRED");
        ColoredConsole.WritePrimaryLogLine($"The agent would like to invoke the following sensitive function:");
        ColoredConsole.WriteSecondaryLogLine($"  Function Name: {functionApprovalRequest.FunctionCall.Name}");
        ColoredConsole.WriteSecondaryLogLine($"  Arguments: {string.Join(", ", functionApprovalRequest.FunctionCall.Arguments?.Select(a => $"{a.Key}={a.Value}") ?? [])}");
        ColoredConsole.WritePrimaryLogLine("Please reply Y to approve, or any other key to reject:");
        
        bool approved = Console.ReadLine()?.ToLower() == "y";
        
        if (approved)
        {
            ColoredConsole.WriteSuccessLine("Function call approved by user.");
        }
        else
        {
            ColoredConsole.WriteErrorLine("Function call rejected by user.");
        }
        
        // Create a response message with the approval decision
        return new ChatMessage(ChatRole.User, [functionApprovalRequest.CreateResponse(approved)]);
    }).ToList();

    // Step 4.2: Continue the agent conversation with the user's approval responses (with spinner to show loading).
    response = await agent.RunAsync(userResponses, thread).WithSpinner("Executing approved function");
}

// Step 5: Display the final response
ColoredConsole.WriteEmptyLine();
ColoredConsole.WritePrimaryLogLine("Agent Response:");
ColoredConsole.WriteSecondaryLogLine(response.ToString());

// Step 6: Display token usage
ColoredConsole.WriteEmptyLine();
ColoredConsole.WriteBurgundyLine("Token Usage:");
ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {response.Usage?.InputTokenCount}");
ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {response.Usage?.OutputTokenCount}");
ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {response.Usage?.TotalTokenCount}");

#endregion

