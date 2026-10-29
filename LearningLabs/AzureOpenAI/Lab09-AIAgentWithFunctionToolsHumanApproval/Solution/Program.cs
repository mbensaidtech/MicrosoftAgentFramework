using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.RegularExpressions;
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
// dotnet run                          -> the implemented scenarios (a scenario whose code is still the delivered one is left out)
// dotnet run -- --scenarios 2         -> scenario 2 only (and the scenarios it requires)
// dotnet run -- --scenarios 1,3       -> scenarios 1 and 3
// dotnet run -- --scenarios all       -> every scenario, implemented or not
// dotnet run -- --list-scenarios      -> the scenarios of this lab and their state
ScenarioSelection scenarioSelection = ScenarioSelection.FromArgs(args);
// ============================================

bool ShouldRunScenario(int scenario) => scenarioSelection.ShouldRun(scenario);

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

#region Scenario 3: Guardrails - an agent run middleware checks the request and the answer

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: Guardrails with an agent run middleware ===");

    // Step 1: Create the HR agent of scenario 2 (the deletion still requires an approval)
    List<AITool> tools =
    [
        AIFunctionFactory.Create(HrTools.GetEmployeeInfo, "get_employee_info"),
        new ApprovalRequiredAIFunction(AIFunctionFactory.Create(HrTools.DeleteEmployeeData, "delete_employee_data"))
    ];
    AIAgent hrAgent = chatClient.AsAIAgent(instructions: AgentInstructions, name: "HrAssistant", tools: tools);

    // Step 2: Wrap it in the guardrail middleware (HrGuardrail, at the end of this file). The agent itself is not changed.
    // Only the non-streaming function is given, as in the official sample: a RunStreamingAsync of the guarded agent then
    // runs in non-streaming mode, so that the guardrail always sees the whole answer.
    AIAgent guardedAgent = hrAgent.AsBuilder().Use(runFunc: HrGuardrail.RunAsync, runStreamingFunc: null).Build();

    // Step 3: A forbidden topic: the middleware answers itself, the agent (and the model) is never called
    const string forbiddenQuestion = "What is the salary of the employee EMP002?";
    ColoredConsole.WritePrimaryLogLine($"User: {forbiddenQuestion}");
    AgentResponse blockedResponse = await guardedAgent.RunAsync(forbiddenQuestion);
    ColoredConsole.WritePrimaryLogLine($"Agent: {blockedResponse.Text}");
    ColoredConsole.WriteSecondaryLogLine(blockedResponse.Usage is null
        ? "Token usage: none, the model was not called"
        : $"Token usage: {blockedResponse.Usage.TotalTokenCount}");

    // Step 4: Personal data in the request: masked before the agent sees it (and checked in the answer)
    const string personalDataQuestion = "My phone number is +33 6 12 34 56 78 and my e-mail is ada.lovelace@example.com. " +
                                        "Call me back with the status of the employee EMP001.";
    ColoredConsole.WritePrimaryLogLine($"User: {personalDataQuestion}");
    AgentResponse maskedResponse = await guardedAgent.RunAsync(personalDataQuestion);
    ColoredConsole.WritePrimaryLogLine($"Agent: {maskedResponse.Text}");
    WriteTokenUsage([maskedResponse]);
}

#endregion

#region Guardrail middleware: checks the request and the answer of every run of the agent it wraps

/// <summary>
/// An agent run middleware (official sample Agent_Step11_Middleware): <see cref="RunAsync"/> is called around every run
/// of the agent it wraps. It receives the messages of the run and the inner agent: it can answer by itself (without calling
/// the agent), change the request before the agent sees it, and change the response before the caller sees it.
/// </summary>
internal static class HrGuardrail
{
    private static readonly string[] ForbiddenTopics = ["salary", "salaries", "password"];

    public static async Task<AgentResponse> RunAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        CancellationToken cancellationToken)
    {
        List<ChatMessage> request = [.. messages];

        // 1. A forbidden topic: answer directly, the agent and the model are never called
        string? topic = ForbiddenTopics.FirstOrDefault(forbidden =>
            request.Any(message => message.Role == ChatRole.User && message.Text.Contains(forbidden, StringComparison.OrdinalIgnoreCase)));
        if (topic is not null)
        {
            ColoredConsole.WriteWarningLine($"[Guardrail] Request blocked: forbidden topic '{topic}'. The agent was not called.");
            return new AgentResponse([new ChatMessage(ChatRole.Assistant, "I can't help with salaries or passwords. Please contact the HR department directly.")]);
        }

        // 2. Personal data in the request: masked before the agent (and the model) sees it
        int maskedInRequest = MaskPersonalData(request);
        ColoredConsole.WriteWarningLine($"[Guardrail] Request: {maskedInRequest} personal data item(s) masked before the agent sees them");

        AgentResponse response = await innerAgent.RunAsync(request, session, options, cancellationToken);

        // 3. Personal data in the answer: masked before the caller sees it
        int maskedInAnswer = MaskPersonalData(response.Messages);
        ColoredConsole.WriteWarningLine($"[Guardrail] Answer: {maskedInAnswer} personal data item(s) masked");
        return response;
    }

    /// <summary>
    /// Masks the e-mail addresses and the international phone numbers of the text of the messages, and returns how many were masked.
    /// Masked in place: only the text changes, the other contents (tool calls, approval requests...) stay as they are.
    /// Simple patterns for the lab: a real application uses a dedicated detection service.
    /// </summary>
    public static int MaskPersonalData(IEnumerable<ChatMessage> messages)
    {
        int masked = 0;
        foreach (TextContent text in messages.SelectMany(message => message.Contents).OfType<TextContent>())
        {
            text.Text = Regex.Replace(text.Text, @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", _ => { masked++; return "[e-mail masked]"; });
            text.Text = Regex.Replace(text.Text, @"\+\d{1,3}(?:[ .-]?\d){8,12}", _ => { masked++; return "[phone masked]"; });
        }

        return masked;
    }
}

#endregion
