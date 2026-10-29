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
// dotnet run                          -> the implemented scenarios (a scenario whose code is still the delivered one is left out)
// dotnet run -- --scenarios 2         -> scenario 2 only (and the scenarios it requires)
// dotnet run -- --scenarios 1,3       -> scenarios 1 and 3
// dotnet run -- --scenarios all       -> every scenario, implemented or not
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

// Step 3: Get a ChatClient (Chat Completions API) for the deployment
ChatClient chatClient = client.GetChatClient(settings.ChatDeploymentName);

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

    // Step 1: Create the agent under test, with its tool
    AIAgent supportAgent = chatClient.AsAIAgent(
        instructions: ReferenceInstructions,
        name: "SupportAgent",
        tools: [AIFunctionFactory.Create(OrderTools.GetOrderStatus, "get_order_status")]);

    // Step 2: Create a local evaluator: checks run in the program, without any model call
    // - NonEmpty: the agent answered something
    // - ContainsExpected: the answer contains the expected output of the question
    // - ToolCallArgsMatch: the expected tool calls were made, with the expected arguments
    // - a custom check: the answer is concise (fewer than 80 words)
    LocalEvaluator checks = new(
        EvalChecks.NonEmpty(),
        EvalChecks.ContainsExpected(),
        EvalChecks.ToolCallArgsMatch(),
        FunctionEvaluator.Create("is_concise", (string response) => response.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 80));

    // Step 3: Run the agent on every question of the dataset and evaluate each answer.
    // The expected outputs and the expected tool calls are paired with the questions by position
    AgentEvaluationResults results = await supportAgent.EvaluateAsync(
        EvaluationDataset.Questions,
        checks,
        expectedOutput: EvaluationDataset.ExpectedOutputs,
        expectedToolCalls: EvaluationDataset.ExpectedToolCalls).WithSpinner("Evaluating the agent");

    // Step 4: Display the result of every check for every question, then the summary
    WriteResults(results);
}

#endregion

#region Scenario 2: Score the answers with an LLM judge

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Score the answers with an LLM judge ===");

    // Step 1: Create the same agent as in scenario 1
    AIAgent supportAgent = chatClient.AsAIAgent(
        instructions: ReferenceInstructions,
        name: "SupportAgent",
        tools: [AIFunctionFactory.Create(OrderTools.GetOrderStatus, "get_order_status")]);

    // Step 2: Run the agent once per question and keep the responses: both evaluations below score the same answers
    List<AgentResponse> responses = [];
    foreach (string question in EvaluationDataset.Questions)
    {
        responses.Add(await supportAgent.RunAsync(question).WithSpinner("Running the agent"));
    }

    // Step 3: Evaluate the responses with LLM-as-judge evaluators (Microsoft.Extensions.AI.Evaluation.Quality):
    // a model reads each question and answer and gives a score from 1 to 5 with its reason.
    // The judge is a chat client: here the same deployment as the agent (a simplification, see the README)
    ChatConfiguration judge = new(chatClient.AsIChatClient());
    ColoredConsole.WritePrimaryLogLine("Judge: the chat client of the deployment (the same model as the agent)");
    AgentEvaluationResults scores = await supportAgent.EvaluateAsync(
        responses,
        EvaluationDataset.Questions,
        new CompositeEvaluator(new RelevanceEvaluator(), new CoherenceEvaluator()),
        chatConfiguration: judge).WithSpinner("Scoring the answers");

    // Step 4: Display the scores of every answer, with the reasons of the judge
    WriteResults(scores);
}

#endregion

#region Scenario 3: Catch a regression of the instructions

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: Catch a regression of the instructions ===");

    // Step 1: A new version of the instructions, with a new security requirement. It looks harmless...
    const string CandidateInstructions = """
        You are the customer support assistant of an online store.
        Security first: before looking up an order, ask the customer for the email address used for the order,
        and do not call get_order_status until the customer has given it.
        Returns are accepted within 30 days of delivery.
        Answer in at most three sentences.
        """;

    // Step 2: The reference agent and the candidate agent: same model, same tool, different instructions
    AIAgent referenceAgent = chatClient.AsAIAgent(
        instructions: ReferenceInstructions,
        name: "SupportAgent",
        tools: [AIFunctionFactory.Create(OrderTools.GetOrderStatus, "get_order_status")]);
    AIAgent candidateAgent = chatClient.AsAIAgent(
        instructions: CandidateInstructions,
        name: "SupportAgent",
        tools: [AIFunctionFactory.Create(OrderTools.GetOrderStatus, "get_order_status")]);

    // Step 3: Evaluate both versions on the same dataset, with the same deterministic checks
    LocalEvaluator checks = new(EvalChecks.NonEmpty(), EvalChecks.ContainsExpected(), EvalChecks.ToolCallArgsMatch());
    AgentEvaluationResults reference = await referenceAgent.EvaluateAsync(
        EvaluationDataset.Questions, checks,
        expectedOutput: EvaluationDataset.ExpectedOutputs, expectedToolCalls: EvaluationDataset.ExpectedToolCalls).WithSpinner("Evaluating the reference");
    AgentEvaluationResults candidate = await candidateAgent.EvaluateAsync(
        EvaluationDataset.Questions, checks,
        expectedOutput: EvaluationDataset.ExpectedOutputs, expectedToolCalls: EvaluationDataset.ExpectedToolCalls).WithSpinner("Evaluating the candidate");

    // Step 4: Compare the two runs: a regression is a question that passed with the reference and fails with the candidate
    WriteComparison("reference", reference, "candidate", candidate);
    List<EvaluationResult> referenceItems = [.. reference.Items];
    List<EvaluationResult> candidateItems = [.. candidate.Items];
    int regressions = Enumerable.Range(0, referenceItems.Count).Count(i => Passed(referenceItems[i]) && !Passed(candidateItems[i]));
    if (regressions > 0)
    {
        ColoredConsole.WriteErrorLine($"Regression detected: {regressions} question(s) pass with the reference instructions and fail with the candidate");
    }
    else
    {
        ColoredConsole.WriteSuccessLine("No regression: every question that passed with the reference still passes");
    }

    // Step 5: Consistency: the same question, run 3 times on the reference agent (an answer can change from run to run)
    AgentEvaluationResults repeated = await referenceAgent.EvaluateAsync(
        [EvaluationDataset.Questions[0]], checks,
        expectedOutput: [EvaluationDataset.ExpectedOutputs[0]], expectedToolCalls: [EvaluationDataset.ExpectedToolCalls[0]],
        numRepetitions: 3).WithSpinner("Repeating a question");
    ColoredConsole.WritePrimaryLogLine($"Repetitions of Q1: {repeated.Passed}/{repeated.Total} passed");
}

#endregion
