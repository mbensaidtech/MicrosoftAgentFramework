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

// TODO 4: Load the after-sales rules (MockedData/sav-rules.yml) and create the three specialists of Agents (provided in LabPrompts.cs):
// Agents.ReturnsSpecialist, Agents.RefundSpecialist and Agents.ShippingSpecialist, in an array used by the three scenarios
// Hint: string savRules = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "MockedData", "sav-rules.yml"));
//       AIAgent[] specialists = [chatClient.AsAIAgent(instructions: string.Format(CultureInfo.InvariantCulture,
//           Agents.ReturnsSpecialist.Instructions, savRules), name: Agents.ReturnsSpecialist.Name), ...the two others];

// TODO 5: The input of every workflow: the customer message CustomerMessages.MissingChargerAndWrongSleeve, as a list of chat messages
// Hint: List<ChatMessage> customerMessage = [new(ChatRole.User, CustomerMessages.MissingChargerAndWrongSleeve)];
ColoredConsole.WriteUserLine($"Customer: {CustomerMessages.MissingChargerAndWrongSleeve}");

#endregion

#region Scenario 1: A concurrent workflow - fan out, then aggregate

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: A concurrent workflow - fan out, then aggregate ===");

    // TODO 6: Build the concurrent workflow "after-sales" with the three specialists, and display its graph
    // Hint: Workflow workflow = AgentWorkflowBuilder.BuildConcurrent("after-sales", specialists);
    //       WritePipeline(workflow);

    // TODO 7: Start the run with the customer message, then send the TurnToken (emitEvents: false)
    // Hint: await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, customerMessage);
    //       await run.TrySendMessageAsync(new TurnToken(emitEvents: false));

    // TODO 8: Watch the events of the run: display the output of the workflow, and the errors
    // Hint: await foreach (WorkflowEvent evt in run.WatchStreamAsync()) with a switch on evt:
    //       - WorkflowOutputEvent output: WriteWorkflowOutput(output.As<List<ChatMessage>>() ?? []);
    //       - ExecutorFailedEvent failed: WriteExecutorFailed(failed);
    //       - WorkflowErrorEvent error: WriteWorkflowError(error);
}

#endregion

#region Scenario 2: Follow the parallel run - time, context and cost of each agent

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Follow the parallel run - time, context and cost of each agent ===");

    // TODO 9: Build the same workflow, start the run, and ask for the streaming events this time
    // Hint: as TODO 6 (without WritePipeline) and TODO 7, with new TurnToken(emitEvents: true)

    // TODO 10: Follow the events with a ParallelTrace (provided). AgentResponseUpdateEvent derives from WorkflowOutputEvent: test it first
    // Hint: ParallelTrace trace = new(); List<ChatMessage> output = [];
    //       switch on evt: ExecutorInvokedEvent invoked -> trace.OnExecutorInvoked(invoked);
    //                      ExecutorCompletedEvent completed -> trace.OnExecutorCompleted(completed);
    //                      AgentResponseUpdateEvent update -> trace.OnAgentUpdate(update);
    //                      WorkflowOutputEvent workflowOutput -> output = workflowOutput.As<List<ChatMessage>>() ?? [];
    //                      ExecutorFailedEvent / WorkflowErrorEvent -> as in TODO 8

    // TODO 11: Display what each agent received, its time and its cost, then the order of the output
    // Hint: trace.WriteReport(); WriteWorkflowOutputAuthors(output);
}

#endregion

#region Scenario 3: Combine the answers - a custom aggregator, then one reply

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: Combine the answers - a custom aggregator, then one reply ===");

    // TODO 12: Write the aggregator MergeAnswers: it receives one list per agent, in the order the agents finished
    // (the message the agent received, then its answer). Keep the answer of each agent (the last message of its list),
    // order the answers as the specialists array, and merge them into one message for the reply writer
    // Hint: List<ChatMessage> MergeAnswers(IList<List<ChatMessage>> results)
    //       {
    //           IEnumerable<ChatMessage> answers = results.Where(messages => messages.Count > 0).Select(messages => messages[^1])
    //               .OrderBy(answer => Array.FindIndex(specialists, agent => agent.Name == answer.AuthorName));
    //           string merged = string.Join("\n\n", answers.Select(answer => $"{answer.AuthorName}: {answer.Text.Trim()}"));
    //           return [new ChatMessage(ChatRole.User, $"Answers of the specialists:\n\n{merged}") { AuthorName = "Specialists" }];
    //       }

    // TODO 13: Build the concurrent workflow "after-sales-merged" with this aggregator, display its graph, and start the run
    // Hint: AgentWorkflowBuilder.BuildConcurrent("after-sales-merged", specialists, MergeAnswers), then as TODO 7

    // TODO 14: Watch the events: keep the output in a variable merged, and display it; handle the errors as in TODO 8
    // Hint: List<ChatMessage> merged = []; ... case WorkflowOutputEvent output: merged = output.As<List<ChatMessage>>() ?? []; WriteWorkflowOutput(merged);

    // TODO 15: Once the specialists have all answered, create the reply writer of Agents.ReplyWriter, run it with the customer message
    // and the merged answers, and display its reply
    // Hint: if (merged.Count > 0) { AIAgent writerAgent = chatClient.AsAIAgent(instructions: ..., name: ...);
    //       AgentResponse reply = await writerAgent.RunAsync([.. customerMessage, .. merged]).WithSpinner("Running the reply writer");
    //       WriteAgentAnswer(Agents.ReplyWriter.Name, reply); }
}

#endregion
