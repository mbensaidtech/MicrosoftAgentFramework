using System.ClientModel;
using System.ClientModel.Primitives;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using CommonUtilities;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

using AIAgentWithFunctionToolsHumanApproval;
using AIAgentWithFunctionToolsHumanApproval.Tools;
using static AIAgentWithFunctionToolsHumanApproval.ApprovalConsole;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// Set to: [1], [2] or [1, 2] to run specific scenarios (scenario 1 asks you to approve at the console)
HashSet<int> scenariosToRun = [1, 2];
// ============================================

bool ShouldRunScenario(int scenario) => scenariosToRun.Count == 0 || scenariosToRun.Contains(scenario);

// The same HR assistant in both scenarios: only the way the approval decision is taken changes.
const string AgentInstructions =
    "You are an HR assistant. Use the tools to look up employees and to delete employee data when asked. " +
    "Only report a deletion as done when the delete_employee_data tool confirmed it; if a deletion was rejected, say so and give the reason.";

#region Setup: Configuration and Client Initialization

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
Console.WriteLine($"Endpoint: {settings.Endpoint}");
Console.WriteLine($"Deployment: {settings.ChatDeploymentName}");
Console.WriteLine($"Agent instructions: {AgentInstructions}");

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

#region Scenario 1: Human approval at the console

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: Human approval at the console ===");

    // TODO 4: Create the sensitive tool and wrap it in an ApprovalRequiredAIFunction
    // Hint: AIFunctionFactory.Create(HrTools.DeleteEmployeeData, "delete_employee_data") gives the AIFunction;
    //       new ApprovalRequiredAIFunction(...) marks it as requiring approval
    // List<AITool> tools = [...];

    // TODO 5: Create the agent with the tool (same as Lab 03)
    // Hint: chatClient.AsAIAgent(instructions: AgentInstructions, name: "HrAssistant", tools: tools)
    // AIAgent hrAgent = ...

    // TODO 6: Create a session and run the agent in it (the session is mandatory for the approval flow)
    // Hint: await hrAgent.CreateSessionAsync(), then await hrAgent.RunAsync(prompt, session).WithSpinner("Running agent")
    //       Keep every response in a list: List<AgentResponse> runs = [response];
    const string prompt = "Delete all the data of the employee with the ID EMP001.";
    ColoredConsole.WritePrimaryLogLine($"User: {prompt}");
    // AgentSession session = ...
    // AgentResponse response = ...

    // TODO 7: Get the approval requests from the response messages
    // Hint: response.Messages.SelectMany(message => message.Contents).OfType<ToolApprovalRequestContent>().ToList()
    //       then ColoredConsole.WriteInfoLine($"Agent run paused: {approvalRequests.Count} approval request(s) pending")
    // List<ToolApprovalRequestContent> approvalRequests = ...

    // TODO 8: Approval loop: while there are approval requests
    //   8.1 For each request: WriteApprovalRequest(request), ask the user (Console.ReadLine, "Y" = approved),
    //       write "Function call approved by user." / "Function call rejected by user.",
    //       and return new ChatMessage(ChatRole.User, [request.CreateResponse(approved)])
    //   8.2 Continue the conversation: response = await hrAgent.RunAsync(approvalMessages, session).WithSpinner("Running agent");
    //       add the response to runs, then read the approval requests of this new response (same as TODO 7)
    // while (approvalRequests.Count > 0) { ... }

    // TODO 9: Display the tool results and the final answer
    // Hint: WriteToolResults(runs), then ColoredConsole.WritePrimaryLogLine("Agent answer:") and ColoredConsole.WriteSecondaryLogLine(response.Text)

    // TODO 10: Display the token usage of all the runs
    // Hint: WriteTokenUsage(runs)
}

#endregion

#region Scenario 2: Several approval requests decided by a policy

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Several approval requests decided by a policy ===");

    // TODO 11: Create two tools: get_employee_info (no approval) and delete_employee_data (ApprovalRequiredAIFunction)
    // Hint: AIFunctionFactory.Create(HrTools.GetEmployeeInfo, "get_employee_info") and the wrapped tool of TODO 4
    // List<AITool> tools = [...];

    // TODO 12: Create the agent (same as TODO 5)
    // AIAgent hrAgent = ...

    // TODO 13: Create a session, run the agent with the prompt below and keep the response in a list (same as TODO 6)
    const string prompt = "The employees EMP001 and EMP002 asked us to erase their data. Look up both employees, then delete the data of each of them.";
    ColoredConsole.WritePrimaryLogLine($"User: {prompt}");
    // AgentSession session = ...
    // AgentResponse response = ...

    // TODO 14: Get the approval requests (same as TODO 7): only the delete_employee_data calls come back as requests
    // List<ToolApprovalRequestContent> approvalRequests = ...

    // TODO 15: Approval loop decided by DeletionPolicy instead of a person
    //   15.1 For each request: FunctionCallContent call = (FunctionCallContent)request.ToolCall;
    //        ApprovalDecision decision = DeletionPolicy.Decide(call);
    //        write $"Approval requested for {FormatCall(call)}" and $"Policy decision: approved - {decision.Reason}"
    //        (or "rejected - ..."), and return new ChatMessage(ChatRole.User, [request.CreateResponse(decision.Approved, decision.Reason)])
    //   15.2 Continue the conversation and read the new approval requests (same as TODO 8.2)
    // while (approvalRequests.Count > 0) { ... }

    // TODO 16: Display the tool results and the final answer (same as TODO 9)

    // TODO 17: Display the token usage of all the runs (same as TODO 10)
}

#endregion
