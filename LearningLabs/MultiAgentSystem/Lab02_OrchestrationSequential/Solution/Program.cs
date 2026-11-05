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

// Step 3: Get a ChatClient (Chat Completions API) for the deployment: every agent of this lab uses it
ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);

// Step 4: Load the meeting transcript: the input of every workflow of this lab, as a list of chat messages
string transcript = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "meeting-transcript.txt"));
List<ChatMessage> meeting = [new(ChatRole.User, transcript)];

// Step 5: Create the action items extractor, used by the three scenarios. An agent keeps no conversation by itself:
// each workflow gives it its own session, so the same agent can be a step of several workflows
AIAgent actionItemsAgent = chatClient.AsAIAgent(
    instructions: Agents.ActionItemsExtractor.Instructions,
    name: Agents.ActionItemsExtractor.Name);

#endregion

#region Scenario 1: A sequential workflow - build it, run it, read its output

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: A sequential workflow - build it, run it, read its output ===");

    // Step 1: Create the first step of the pipeline: the summarizer
    AIAgent summarizerAgent = chatClient.AsAIAgent(
        instructions: Agents.MeetingSummarizer.Instructions,
        name: Agents.MeetingSummarizer.Name);

    // Step 2: Build the sequential workflow: the agents run in the order of the list, each one after the previous one
    Workflow workflow = AgentWorkflowBuilder.BuildSequential("meeting-notes", [summarizerAgent, actionItemsAgent]);
    WritePipeline(workflow);

    // Step 3: Start the run with the input messages. The agents keep the messages they receive until they get a TurnToken:
    // send one to start the first agent (emitEvents: false - no streaming updates, only the output of the workflow)
    await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, meeting);
    await run.TrySendMessageAsync(new TurnToken(emitEvents: false));

    // Step 4: Watch the events of the run until it has nothing left to do. The output of a sequential workflow
    // is the list of the messages of the conversation: the input, then the messages of each agent
    await foreach (WorkflowEvent evt in run.WatchStreamAsync())
    {
        switch (evt)
        {
            case WorkflowOutputEvent output:
                WriteWorkflowOutput(output.As<List<ChatMessage>>() ?? []);
                break;
            case ExecutorFailedEvent failed:
                WriteExecutorFailed(failed);
                break;
            case WorkflowErrorEvent error:
                WriteWorkflowError(error);
                break;
        }
    }

    // Step 5: No token usage here: without streaming events, the run gives only the output messages
    ColoredConsole.WriteSecondaryLogLine("Token usage: not available without the streaming events (see scenario 2).");
}

#endregion

#region Scenario 2: Follow the pipeline - what each agent receives

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Follow the pipeline - what each agent receives ===");

    // Step 1: Create the two next steps: the writer of the follow-up email, and its translator
    AIAgent writerAgent = chatClient.AsAIAgent(
        instructions: Agents.FollowUpWriter.Instructions,
        name: Agents.FollowUpWriter.Name);
    AIAgent translatorAgent = chatClient.AsAIAgent(
        instructions: Agents.Translator.Instructions,
        name: Agents.Translator.Name);

    // Step 2: Build the same pipeline twice:
    // - by default, each agent receives the whole conversation: the input and the messages of every previous agent
    // - with chainOnlyAgentResponses: true, each agent receives only the messages of the previous agent
    (string Heading, Workflow Workflow)[] variants =
    [
        ("Context: the whole conversation (default)",
            AgentWorkflowBuilder.BuildSequential("meeting-follow-up", [actionItemsAgent, writerAgent, translatorAgent])),
        ("Context: the previous agent only (chainOnlyAgentResponses: true)",
            AgentWorkflowBuilder.BuildSequential("meeting-follow-up-chained", chainOnlyAgentResponses: true, [actionItemsAgent, writerAgent, translatorAgent]))
    ];

    foreach ((string heading, Workflow workflow) in variants)
    {
        ColoredConsole.WriteInfoLine($"--- {heading} ---");
        WritePipeline(workflow);

        // Step 3: Run it, and ask for the streaming events this time (emitEvents: true): the agents stream their answers
        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, meeting);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        // Step 4: Follow the events. AgentResponseUpdateEvent derives from WorkflowOutputEvent: test it first
        PipelineTrace trace = new();
        List<ChatMessage> output = [];
        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            switch (evt)
            {
                case ExecutorInvokedEvent invoked:
                    trace.OnExecutorInvoked(invoked);
                    break;
                case AgentResponseUpdateEvent update:
                    trace.OnAgentUpdate(update);
                    break;
                case WorkflowOutputEvent workflowOutput:
                    output = workflowOutput.As<List<ChatMessage>>() ?? [];
                    break;
                case ExecutorFailedEvent failed:
                    WriteExecutorFailed(failed);
                    break;
                case WorkflowErrorEvent error:
                    WriteWorkflowError(error);
                    break;
            }
        }

        // Step 5: Display what each executor received and the token usage of each agent, then the authors of the output
        // (the texts were streamed above)
        trace.WriteReport();
        WriteWorkflowOutputAuthors(output);
    }
}

#endregion

#region Scenario 3: Human in the loop - approve a tool call before it runs

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: Human in the loop - approve a tool call before it runs ===");

    // Step 1: Create the writer, and the sender with its tool send_email wrapped in an ApprovalRequiredAIFunction:
    // the agent can ask for the call, but the call runs only once it is approved
    AIAgent writerAgent = chatClient.AsAIAgent(
        instructions: Agents.FollowUpWriter.Instructions,
        name: Agents.FollowUpWriter.Name);
    AIAgent senderAgent = chatClient.AsAIAgent(
        instructions: Agents.MinutesSender.Instructions,
        name: Agents.MinutesSender.Name,
        tools: [new ApprovalRequiredAIFunction(AIFunctionFactory.Create(EmailTools.SendEmail, "send_email"))]);

    // Step 2: Build the pipeline: nothing to configure, a sequential workflow supports tool approvals
    Workflow workflow = AgentWorkflowBuilder.BuildSequential("meeting-minutes", [actionItemsAgent, writerAgent, senderAgent]);
    WritePipeline(workflow);

    // Step 3: Run it as in scenario 1
    await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, meeting);
    await run.TrySendMessageAsync(new TurnToken(emitEvents: false));

    // Step 4: When the sender asks to call send_email, the workflow pauses and emits a RequestInfoEvent.
    // The code plays the reviewer: it reads the request, answers it with SendResponseAsync, and the workflow resumes
    await foreach (WorkflowEvent evt in run.WatchStreamAsync())
    {
        switch (evt)
        {
            case RequestInfoEvent request when request.Request.TryGetDataAs(out ToolApprovalRequestContent? approvalRequest):
                WriteApprovalRequest(approvalRequest);
                WriteRunStatus(await run.GetStatusAsync());
                ToolApprovalResponseContent approval = approvalRequest.CreateResponse(approved: true);
                WriteApprovalResponse(approval);
                await run.SendResponseAsync(request.Request.CreateResponse(approval));
                break;
            case WorkflowOutputEvent output:
                WriteWorkflowOutput(output.As<List<ChatMessage>>() ?? []);
                break;
            case ExecutorFailedEvent failed:
                WriteExecutorFailed(failed);
                break;
            case WorkflowErrorEvent error:
                WriteWorkflowError(error);
                break;
        }
    }

    // Step 5: The run has nothing left to do
    WriteRunStatus(await run.GetStatusAsync());
}

#endregion
