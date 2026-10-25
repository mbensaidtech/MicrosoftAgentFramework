using System.ClientModel;
using System.ClientModel.Primitives;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using OpenAI;
using OpenAI.Chat;
using CommonUtilities;

using AIAgentEvaluation;
using AIAgentEvaluation.Tools;
using static AIAgentEvaluation.EvaluationConsole;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// Set to: [1], [2], [3] or [1, 2, 3] to run specific scenarios
HashSet<int> scenariosToRun = [1, 2, 3];
// ============================================

bool ShouldRunScenario(int scenario) => scenariosToRun.Count == 0 || scenariosToRun.Contains(scenario);

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

// TODO 3: Get a ChatClient (Chat Completions API) for the deployment
// ChatClient chatClient = ...

// Step 4: The instructions of the support agent under test (the "reference" version)
const string ReferenceInstructions = """
    You are the customer support assistant of an online store.
    For any question about an order, call get_order_status with the order number, and answer with the facts it returns:
    tracking number, carrier, locker code, dates.
    Returns are accepted within 30 days of delivery.
    Answer in at most three sentences.
    """;
ColoredConsole.WriteSecondaryLogLine($"Instructions of the agent under test:\n{ReferenceInstructions}");

#endregion

#region Scenario 1: Evaluate an agent with deterministic checks

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: Evaluate an agent with deterministic checks ===");

    // TODO 4: Create the agent under test: instructions ReferenceInstructions, name "SupportAgent", and the tool get_order_status
    // Hint: chatClient.AsAIAgent(instructions: ..., name: ..., tools: [AIFunctionFactory.Create(OrderTools.GetOrderStatus, "get_order_status")])
    // AIAgent supportAgent = ...

    // TODO 5: Create a local evaluator with four checks: EvalChecks.NonEmpty(), EvalChecks.ContainsExpected(),
    // EvalChecks.ToolCallArgsMatch() and a custom check "is_concise" (fewer than 80 words)
    // Hint: FunctionEvaluator.Create("is_concise", (string response) => response.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 80)
    // LocalEvaluator checks = new(...);

    // TODO 6: Run the agent on the dataset and evaluate each answer
    // Hint: await supportAgent.EvaluateAsync(EvaluationDataset.Questions, checks, expectedOutput: EvaluationDataset.ExpectedOutputs,
    //       expectedToolCalls: EvaluationDataset.ExpectedToolCalls).WithSpinner("Evaluating the agent")
    // AgentEvaluationResults results = ...

    // TODO 7: Display the results of every check for every question, then the summary
    // Hint: WriteResults(results)
}

#endregion

#region Scenario 2: Score the answers with an LLM judge

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Score the answers with an LLM judge ===");

    // TODO 8: Create the same agent as in TODO 4, run it once per question and keep the responses
    // Hint: List<AgentResponse> responses = []; for each question: responses.Add(await supportAgent.RunAsync(question).WithSpinner("Running the agent"));

    // TODO 9: Create the judge: a ChatConfiguration over the chat client of the deployment, and say which model judges
    // Hint: ChatConfiguration judge = new(chatClient.AsIChatClient());
    //       ColoredConsole.WritePrimaryLogLine("Judge: the chat client of the deployment (the same model as the agent)");

    // TODO 10: Score the responses with the LLM-as-judge evaluators Relevance and Coherence, then display the scores
    // Hint: await supportAgent.EvaluateAsync(responses, EvaluationDataset.Questions, new CompositeEvaluator(new RelevanceEvaluator(), new CoherenceEvaluator()),
    //       chatConfiguration: judge).WithSpinner("Scoring the answers"); then WriteResults(scores)
}

#endregion

#region Scenario 3: Catch a regression of the instructions

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: Catch a regression of the instructions ===");

    // TODO 11: Create the reference agent (ReferenceInstructions) and the candidate agent (the candidate instructions of the README),
    // with the same name and the same tool
    // AIAgent referenceAgent = ...
    // AIAgent candidateAgent = ...

    // TODO 12: Evaluate both agents on the dataset with the same checks: NonEmpty, ContainsExpected, ToolCallArgsMatch
    // AgentEvaluationResults reference = ...
    // AgentEvaluationResults candidate = ...

    // TODO 13: Compare the two runs and report the regressions (questions that pass with the reference and fail with the candidate)
    // Hint: WriteComparison("reference", reference, "candidate", candidate); then count with Passed(item) on reference.Items and candidate.Items,
    //       and write "Regression detected: ..." (ColoredConsole.WriteErrorLine) or "No regression: ..." (ColoredConsole.WriteSuccessLine)

    // TODO 14: Run the first question 3 times on the reference agent (numRepetitions: 3) and display how many passed
    // Hint: ColoredConsole.WritePrimaryLogLine($"Repetitions of Q1: {repeated.Passed}/{repeated.Total} passed")
}

#endregion
