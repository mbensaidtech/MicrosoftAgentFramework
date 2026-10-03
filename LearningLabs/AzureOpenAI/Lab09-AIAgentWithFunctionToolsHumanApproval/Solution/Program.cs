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

#region Scenario 1: Human approval at the console

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: Human approval at the console ===");

    // Step 1: Create the sensitive tool and wrap it in an ApprovalRequiredAIFunction.
    // The wrapper only marks the function: the agent (its FunctionInvokingChatClient) stops the run and asks for approval
    // instead of invoking it.
    AIFunction deleteEmployeeData = AIFunctionFactory.Create(HrTools.DeleteEmployeeData, "delete_employee_data");
    List<AITool> tools = [new ApprovalRequiredAIFunction(deleteEmployeeData)];

    // Step 2: Create the agent with the tool (same as Lab 03)
    AIAgent hrAgent = chatClient.AsAIAgent(instructions: AgentInstructions, name: "HrAssistant", tools: tools);

    // Step 3: Create a session and run the agent in it.
    // The session is mandatory: the framework records in it the approval requests it surfaces,
    // and only an approval sent back in the same session is honored (approval binding).
    AgentSession session = await hrAgent.CreateSessionAsync();
    const string prompt = "Delete all the data of the employee with the ID EMP001.";
    ColoredConsole.WritePrimaryLogLine($"User: {prompt}");
    AgentResponse response = await hrAgent.RunAsync(prompt, session).WithSpinner("Running agent");
    List<AgentResponse> runs = [response];

    // Step 4: The run completes without invoking the tool: its messages contain a ToolApprovalRequestContent per call to approve
    List<ToolApprovalRequestContent> approvalRequests = response.Messages
        .SelectMany(message => message.Contents)
        .OfType<ToolApprovalRequestContent>()
        .ToList();
    ColoredConsole.WriteInfoLine($"Agent run paused: {approvalRequests.Count} approval request(s) pending");

    // Step 5: Approval loop - ask the user for each request, answer the agent in the same session, and check again
    // until the agent no longer asks for approval (the sample pattern: Agent_Step01_UsingFunctionToolsWithApprovals)
    while (approvalRequests.Count > 0)
    {
        List<ChatMessage> approvalMessages = approvalRequests.ConvertAll(request =>
        {
            // Step 5.1: Show what the agent wants to do (request.ToolCall is the FunctionCallContent chosen by the model)
            WriteApprovalRequest(request);

            // Step 5.2: Ask the user
            ColoredConsole.WritePrimaryLogLine("Please reply Y to approve, or anything else to reject:");
            bool approved = string.Equals(Console.ReadLine()?.Trim(), "Y", StringComparison.OrdinalIgnoreCase);
            if (approved)
            {
                ColoredConsole.WriteSuccessLine("Function call approved by user.");
            }
            else
            {
                ColoredConsole.WriteErrorLine("Function call rejected by user.");
            }

            // Step 5.3: Create the response to this request, in a user message
            return new ChatMessage(ChatRole.User, [request.CreateResponse(approved)]);
        });

        // Step 5.4: Continue the conversation with the decisions: an approved call is invoked now, a rejected one is not
        response = await hrAgent.RunAsync(approvalMessages, session).WithSpinner("Running agent");
        runs.Add(response);
        approvalRequests = response.Messages.SelectMany(message => message.Contents).OfType<ToolApprovalRequestContent>().ToList();
    }

    // Step 6: Display what the tools returned (or the rejection the model received) and the final answer
    WriteToolResults(runs);
    ColoredConsole.WritePrimaryLogLine("Agent answer:");
    ColoredConsole.WriteSecondaryLogLine(response.Text);

    // Step 7: Display the token usage of all the runs of the flow
    WriteTokenUsage(runs);
}

#endregion

#region Scenario 2: Several approval requests decided by a policy

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Several approval requests decided by a policy ===");

    // Step 1: Two tools: a harmless one that runs without approval, and the sensitive one that requires it
    List<AITool> tools =
    [
        AIFunctionFactory.Create(HrTools.GetEmployeeInfo, "get_employee_info"),
        new ApprovalRequiredAIFunction(AIFunctionFactory.Create(HrTools.DeleteEmployeeData, "delete_employee_data"))
    ];

    // Step 2: Create the agent
    AIAgent hrAgent = chatClient.AsAIAgent(instructions: AgentInstructions, name: "HrAssistant", tools: tools);

    // Step 3: Create a session and run the agent: the request concerns two employees
    AgentSession session = await hrAgent.CreateSessionAsync();
    const string prompt = "The employees EMP001 and EMP002 asked us to erase their data. Look up both employees, then delete the data of each of them.";
    ColoredConsole.WritePrimaryLogLine($"User: {prompt}");
    AgentResponse response = await hrAgent.RunAsync(prompt, session).WithSpinner("Running agent");
    List<AgentResponse> runs = [response];

    // Step 4: Only the calls to delete_employee_data come back as approval requests; get_employee_info runs by itself
    List<ToolApprovalRequestContent> approvalRequests = response.Messages
        .SelectMany(message => message.Contents)
        .OfType<ToolApprovalRequestContent>()
        .ToList();
    ColoredConsole.WriteInfoLine($"Agent run paused: {approvalRequests.Count} approval request(s) pending");

    // Step 5: Approval loop - the decision is taken by DeletionPolicy (code) instead of a person,
    // with a reason that the model receives when the call is rejected
    while (approvalRequests.Count > 0)
    {
        List<ChatMessage> approvalMessages = approvalRequests.ConvertAll(request =>
        {
            FunctionCallContent call = (FunctionCallContent)request.ToolCall;
            ApprovalDecision decision = DeletionPolicy.Decide(call);

            ColoredConsole.WriteWarningLine($"Approval requested for {FormatCall(call)}");
            if (decision.Approved)
            {
                ColoredConsole.WriteSuccessLine($"Policy decision: approved - {decision.Reason}");
            }
            else
            {
                ColoredConsole.WriteErrorLine($"Policy decision: rejected - {decision.Reason}");
            }

            return new ChatMessage(ChatRole.User, [request.CreateResponse(decision.Approved, decision.Reason)]);
        });

        response = await hrAgent.RunAsync(approvalMessages, session).WithSpinner("Running agent");
        runs.Add(response);
        approvalRequests = response.Messages.SelectMany(message => message.Contents).OfType<ToolApprovalRequestContent>().ToList();
    }

    // Step 6: Display the tool results: the approved deletion was executed, the rejected one was replaced by the reason
    WriteToolResults(runs);
    ColoredConsole.WritePrimaryLogLine("Agent answer:");
    ColoredConsole.WriteSecondaryLogLine(response.Text);

    // Step 7: Display the token usage of all the runs of the flow
    WriteTokenUsage(runs);
}

#endregion
