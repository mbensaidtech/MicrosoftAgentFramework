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

// TODO 1: Create AzureOpenAIClient with managed identity authentication
// Hint: Use DefaultAzureCredential for authentication
// AzureOpenAIClient client = new AzureOpenAIClient(...)

// TODO 2: Get a ChatClient for the specific deployment
// Hint: Use client.GetChatClient() with the deployment name from settings
// ChatClient chatClient = ...

#endregion

#region Function Tools Calling with Human Approval

ColoredConsole.WriteDividerLine();
ColoredConsole.WriteInfoLine("=== Function Tools Calling with Human Approval ===");
ColoredConsole.WriteSecondaryLogLine("This scenario demonstrates how to require human approval before executing sensitive operations.");
ColoredConsole.WriteEmptyLine();

// TODO 3: Create tools list with approval requirement.
// Hint: Wrap the sensitive function with ApprovalRequiredAIFunction to require user approval
// Use AIFunctionFactory.Create to create the function from SensitiveTools.DeleteEmployeeData
// List<AITool> tools = [new ApprovalRequiredAIFunction(...)];

// TODO 4: Create agent with tools
// Hint: Use chatClient.CreateAIAgent() with instructions and tools parameters
// ChatClientAgent agent = ...

// TODO 5: Create a new thread for the conversation
// Hint: Use agent.GetNewThread() - the thread maintains context for the approval flow
// var thread = ...

// TODO 6: Run the agent with a prompt and thread
// Hint: Use agent.RunAsync(prompt, thread) to run with conversation context
// AgentRunResponse response = await agent.RunAsync("delete the employee with the ID EMP001", thread);

// TODO 7: Get user input requests from the response
// Hint: Check response.UserInputRequests.ToList() to get any pending approval requests
// List<UserInputRequestContent> userInputRequests = ...

// TODO 8: Check if there are any approval requests and process them
// if (userInputRequests.Any())
// {
//     // TODO 8.1: Process each function approval request
//     // Hint: Use userInputRequests.OfType<FunctionApprovalRequestContent>() to filter for function approvals
//     // For each approval request:
//     //   - Display the function name and arguments to the user
//     //   - Ask for approval (Y/N)
//     //   - Create a response message with functionApprovalRequest.CreateResponse(approved)
//     
//     // TODO 8.2: Continue the agent conversation with the user's approval responses
//     // Hint: Use agent.RunAsync(userResponses, thread) to continue the conversation
// }

// TODO 9: Display the final response
// Hint: Use ColoredConsole.WriteEmptyLine(), ColoredConsole.WritePrimaryLogLine("Agent Response:") and response.ToString()

// TODO 10: Display token usage
// Hint: Use ColoredConsole.WriteEmptyLine(), ColoredConsole.WriteBurgundyLine("Token Usage:") 
// and response.Usage properties (InputTokenCount, OutputTokenCount, TotalTokenCount)

#endregion

