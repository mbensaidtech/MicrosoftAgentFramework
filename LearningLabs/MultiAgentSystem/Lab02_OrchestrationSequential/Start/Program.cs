using System.ClientModel;
using System.ClientModel.Primitives;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using CommonUtilities;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

using OrchestrationSequential;
using OrchestrationSequential.Tools;
using static OrchestrationSequential.WorkflowConsole;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// dotnet run                          -> the implemented scenarios (a scenario whose code is still the delivered one is left out)
// dotnet run -- --scenarios 2         -> scenario 2 only (and the scenarios it requires)
// dotnet run -- --scenarios 1,3       -> scenarios 1 and 3
// dotnet run -- --list-scenarios      -> the scenarios of this lab and their state
ScenarioSelection scenarioSelection = ScenarioSelection.FromArgs(args);
// ============================================

bool ShouldRunScenario(int scenario) => scenarioSelection.ShouldRun(scenario);

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

// TODO 3: Get a ChatClient (Chat Completions API) for the deployment: every agent of this lab uses it
// ChatClient chatClient = ...

// TODO 4: Load the meeting transcript (Data/meeting-transcript.txt): the input of every workflow, as a list of chat messages
// Hint: string transcript = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "meeting-transcript.txt"));
//       List<ChatMessage> meeting = [new(ChatRole.User, transcript)];

// TODO 5: Create the action items extractor of Agents.ActionItemsExtractor (provided in LabPrompts.cs), used by the three scenarios
// Hint: chatClient.AsAIAgent(instructions: Agents.ActionItemsExtractor.Instructions, name: Agents.ActionItemsExtractor.Name)
// AIAgent actionItemsAgent = ...

#endregion

#region Scenario 1: A sequential workflow - build it, run it, read its output

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: A sequential workflow - build it, run it, read its output ===");

    // TODO 6: Create the summarizer of Agents.MeetingSummarizer: the first step of the pipeline
    // Hint: same as TODO 5, with Agents.MeetingSummarizer
    // AIAgent summarizerAgent = ...

    // TODO 7: Build the sequential workflow "meeting-notes": the summarizer, then the action items extractor. Display its path
    // Hint: Workflow workflow = AgentWorkflowBuilder.BuildSequential("meeting-notes", [summarizerAgent, actionItemsAgent]);
    //       WritePipeline(workflow);

    // TODO 8: Start the run with the transcript, then send a TurnToken: the agents wait for it before they take their turn
    // Hint: await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, meeting);
    //       await run.TrySendMessageAsync(new TurnToken(emitEvents: false));

    // TODO 9: Watch the events of the run: display the output of the workflow, and the errors
    // Hint: await foreach (WorkflowEvent evt in run.WatchStreamAsync()) with a switch on evt:
    //       - WorkflowOutputEvent output: WriteWorkflowOutput(output.As<List<ChatMessage>>() ?? []);
    //       - ExecutorFailedEvent failed: WriteExecutorFailed(failed);
    //       - WorkflowErrorEvent error: WriteWorkflowError(error);

    // TODO 10: Say why there is no token usage here
    // Hint: ColoredConsole.WriteSecondaryLogLine("Token usage: not available without the streaming events (see scenario 2).");
}

#endregion

#region Scenario 2: Follow the pipeline - what each agent receives

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Follow the pipeline - what each agent receives ===");

    // TODO 11: Create the two next steps: the writer of the follow-up email (Agents.FollowUpWriter) and the translator (Agents.Translator)
    // AIAgent writerAgent = ...
    // AIAgent translatorAgent = ...

    // TODO 12: Build the same pipeline (extractor, writer, translator) twice: with the default context, and with chainOnlyAgentResponses: true
    // Hint: (string Heading, Workflow Workflow)[] variants =
    //       [
    //           ("Context: the whole conversation (default)",
    //               AgentWorkflowBuilder.BuildSequential("meeting-follow-up", [actionItemsAgent, writerAgent, translatorAgent])),
    //           ("Context: the previous agent only (chainOnlyAgentResponses: true)",
    //               AgentWorkflowBuilder.BuildSequential("meeting-follow-up-chained", chainOnlyAgentResponses: true, [actionItemsAgent, writerAgent, translatorAgent]))
    //       ];

    // TODO 13: For each variant, display its heading and its path, then run it and ask for the streaming events
    // Hint: foreach ((string heading, Workflow workflow) in variants)
    //       ColoredConsole.WriteInfoLine($"--- {heading} ---"); WritePipeline(workflow);
    //       await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, meeting);
    //       await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

    // TODO 14: Follow the events with a PipelineTrace (provided). AgentResponseUpdateEvent derives from WorkflowOutputEvent: test it first
    // Hint: PipelineTrace trace = new(); List<ChatMessage> output = [];
    //       switch on evt: ExecutorInvokedEvent invoked -> trace.OnExecutorInvoked(invoked);
    //                      AgentResponseUpdateEvent update -> trace.OnAgentUpdate(update);
    //                      WorkflowOutputEvent workflowOutput -> output = workflowOutput.As<List<ChatMessage>>() ?? [];
    //                      ExecutorFailedEvent / WorkflowErrorEvent -> as in TODO 9

    // TODO 15: Display what each executor received and the token usage of each agent, then the authors of the output
    // Hint: trace.WriteReport(); WriteWorkflowOutputAuthors(output);
}

#endregion

#region Scenario 3: Human in the loop - approve a tool call before it runs

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: Human in the loop - approve a tool call before it runs ===");

    // TODO 16: Create the writer (Agents.FollowUpWriter), and the sender of Agents.MinutesSender with its tool send_email,
    // wrapped in an ApprovalRequiredAIFunction
    // Hint: tools: [new ApprovalRequiredAIFunction(AIFunctionFactory.Create(EmailTools.SendEmail, "send_email"))]
    // AIAgent writerAgent = ...
    // AIAgent senderAgent = ...

    // TODO 17: Build the sequential workflow "meeting-minutes": extractor, writer, sender. Display its path
    // Workflow workflow = ...

    // TODO 18: Start the run as in TODO 8

    // TODO 19: Watch the events. When the sender asks to call send_email, the workflow pauses and emits a RequestInfoEvent:
    // display the request and the status of the run, approve it, and send the answer: the workflow resumes
    // Hint: case RequestInfoEvent request when request.Request.TryGetDataAs(out ToolApprovalRequestContent? approvalRequest):
    //           WriteApprovalRequest(approvalRequest); WriteRunStatus(await run.GetStatusAsync());
    //           ToolApprovalResponseContent approval = approvalRequest.CreateResponse(approved: true); WriteApprovalResponse(approval);
    //           await run.SendResponseAsync(request.Request.CreateResponse(approval));
    //       then WorkflowOutputEvent, ExecutorFailedEvent and WorkflowErrorEvent as in TODO 9

    // TODO 20: Display the status of the run once it has nothing left to do
    // Hint: WriteRunStatus(await run.GetStatusAsync());
}

#endregion
