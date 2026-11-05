using System.ClientModel;
using System.ClientModel.Primitives;
using System.Globalization;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using CommonUtilities;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

using OrchestrationConcurrent;
using static OrchestrationConcurrent.WorkflowConsole;

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

// Step 4: Load the after-sales rules of the store and create the three specialists, used by the three scenarios.
// Each one answers the same customer message on its own topic. An agent keeps no conversation by itself:
// each workflow gives it its own session, so the same agents can work in several workflows
string savRules = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "MockedData", "sav-rules.yml"));
AIAgent[] specialists =
[
    chatClient.AsAIAgent(
        instructions: string.Format(CultureInfo.InvariantCulture, Agents.ReturnsSpecialist.Instructions, savRules),
        name: Agents.ReturnsSpecialist.Name),
    chatClient.AsAIAgent(
        instructions: string.Format(CultureInfo.InvariantCulture, Agents.RefundSpecialist.Instructions, savRules),
        name: Agents.RefundSpecialist.Name),
    chatClient.AsAIAgent(
        instructions: string.Format(CultureInfo.InvariantCulture, Agents.ShippingSpecialist.Instructions, savRules),
        name: Agents.ShippingSpecialist.Name)
];

// Step 5: The input of every workflow: the customer message, as a list of chat messages
List<ChatMessage> customerMessage = [new(ChatRole.User, CustomerMessages.MissingChargerAndWrongSleeve)];
ColoredConsole.WriteUserLine($"Customer: {CustomerMessages.MissingChargerAndWrongSleeve}");

#endregion

#region Scenario 1: A concurrent workflow - fan out, then aggregate

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: A concurrent workflow - fan out, then aggregate ===");

    // Step 1: Build the concurrent workflow: every agent receives the same input and works at the same time.
    // The default aggregator keeps the last message of each agent: its answer
    Workflow workflow = AgentWorkflowBuilder.BuildConcurrent("after-sales", specialists);
    WritePipeline(workflow);

    // Step 2: Start the run with the customer message, then send the TurnToken: the start executor passes it to every agent
    await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, customerMessage);
    await run.TrySendMessageAsync(new TurnToken(emitEvents: false));

    // Step 3: Watch the events: the output comes once every agent has answered
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
}

#endregion

#region Scenario 2: Follow the parallel run - time, context and cost of each agent

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Follow the parallel run - time, context and cost of each agent ===");

    // Step 1: Build the same workflow, and run it with the streaming events (emitEvents: true)
    Workflow workflow = AgentWorkflowBuilder.BuildConcurrent("after-sales", specialists);
    await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, customerMessage);
    await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

    // Step 2: Follow the events: when each agent starts and finishes its turn, what it receives, what it streams.
    // AgentResponseUpdateEvent derives from WorkflowOutputEvent: test it first
    ParallelTrace trace = new();
    List<ChatMessage> output = [];
    await foreach (WorkflowEvent evt in run.WatchStreamAsync())
    {
        switch (evt)
        {
            case ExecutorInvokedEvent invoked:
                trace.OnExecutorInvoked(invoked);
                break;
            case ExecutorCompletedEvent completed:
                trace.OnExecutorCompleted(completed);
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

    // Step 3: Display what each agent received, its time and its cost, then the order of the output
    trace.WriteReport();
    WriteWorkflowOutputAuthors(output);
}

#endregion

#region Scenario 3: Combine the answers - a custom aggregator, then one reply

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: Combine the answers - a custom aggregator, then one reply ===");

    // Step 1: Write the aggregator: it receives one list per agent, in the order the agents finished
    // (the message the agent received, then its answer), and returns the output of the workflow.
    // This one keeps the answer of each agent, in the order of the specialists, and merges them into one message
    // for the reply writer. It is plain code: deterministic, no model call
    List<ChatMessage> MergeAnswers(IList<List<ChatMessage>> results)
    {
        IEnumerable<ChatMessage> answers = results
            .Where(messages => messages.Count > 0)
            .Select(messages => messages[^1])
            .OrderBy(answer => Array.FindIndex(specialists, agent => agent.Name == answer.AuthorName));
        string merged = string.Join("\n\n", answers.Select(answer => $"{answer.AuthorName}: {answer.Text.Trim()}"));
        return [new ChatMessage(ChatRole.User, $"Answers of the specialists:\n\n{merged}") { AuthorName = "Specialists" }];
    }

    // Step 2: Build the concurrent workflow with this aggregator, run it, and keep its output
    Workflow workflow = AgentWorkflowBuilder.BuildConcurrent("after-sales-merged", specialists, MergeAnswers);
    WritePipeline(workflow);
    await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, customerMessage);
    await run.TrySendMessageAsync(new TurnToken(emitEvents: false));

    List<ChatMessage> merged = [];
    await foreach (WorkflowEvent evt in run.WatchStreamAsync())
    {
        switch (evt)
        {
            case WorkflowOutputEvent output:
                merged = output.As<List<ChatMessage>>() ?? [];
                WriteWorkflowOutput(merged);
                break;
            case ExecutorFailedEvent failed:
                WriteExecutorFailed(failed);
                break;
            case WorkflowErrorEvent error:
                WriteWorkflowError(error);
                break;
        }
    }

    // Step 3: The specialists do not see each other's answers. One more agent, run by the code once they have all answered,
    // writes the single reply to the customer from the customer message and the merged answers
    if (merged.Count > 0)
    {
        AIAgent writerAgent = chatClient.AsAIAgent(
            instructions: Agents.ReplyWriter.Instructions,
            name: Agents.ReplyWriter.Name);
        AgentResponse reply = await writerAgent.RunAsync([.. customerMessage, .. merged]).WithSpinner("Running the reply writer");
        WriteAgentAnswer(Agents.ReplyWriter.Name, reply);
    }
}

#endregion
