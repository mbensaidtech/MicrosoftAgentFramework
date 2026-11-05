using System.ClientModel;
using System.ClientModel.Primitives;
using System.Globalization;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using CommonUtilities;

using UseAgentAsTool;
using UseAgentAsTool.Models;
using UseAgentAsTool.Tools;
using static UseAgentAsTool.AgentConsole;

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

// Step 4: Load the after-sales rules of the store (MockedData/sav-rules.yml) and create the after-sales expert,
// used by scenarios 1 and 2. An agent keeps no conversation by itself (that is the role of a session): it can be reused
string savRules = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "MockedData", "sav-rules.yml"));
AIAgent afterSalesAgent = chatClient.AsAIAgent(
    instructions: string.Format(CultureInfo.InvariantCulture, Agents.AfterSalesExpert.Instructions, savRules),
    name: Agents.AfterSalesExpert.Name);

#endregion

#region Scenario 1: Delegation by code - a reviewer agent checks the answer

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: Delegation by code - a reviewer agent checks the answer ===");

    // Step 1: Create the reviewer agent: it knows the same rules, and the code will ask it for a QualityReview (structured output)
    AIAgent reviewerAgent = chatClient.AsAIAgent(
        instructions: string.Format(CultureInfo.InvariantCulture, Agents.QualityReviewer.Instructions, savRules),
        name: Agents.QualityReviewer.Name);

    // Step 2: The after-sales expert answers the customer (a direct call: the code decides to call it)
    ColoredConsole.WriteUserLine($"Customer: {CustomerMessages.HeadphonesReturn}");
    AgentResponse answer = await afterSalesAgent.RunAsync(CustomerMessages.HeadphonesReturn).WithSpinner("Running the after-sales expert");
    ColoredConsole.WriteAssistantLine($"{afterSalesAgent.Name}: {answer.Text}");
    WriteTokenUsage($"Token Usage ({afterSalesAgent.Name})", answer);

    // Step 3: The decision belongs to the code: a fixed rule on the score of the review.
    // The review comes from a model: check it before using it (a score outside 1-10 is not trusted)
    const int MinimumScore = 7;
    string Decide(QualityReview review) => review.Score is >= MinimumScore and <= 10
        ? "Decision: sent to the customer"
        : $"Decision: held back for a human agent (score below {MinimumScore})";

    // Step 4: The code gives the answer to the reviewer, gets a typed review back, and decides
    AgentResponse<QualityReview> review = await reviewerAgent.RunAsync<QualityReview>(
        string.Format(CultureInfo.InvariantCulture, ReviewRequests.QuestionAndAnswer, CustomerMessages.HeadphonesReturn, answer.Text)).WithSpinner("Running the reviewer");
    WriteReview(review.Result);
    ColoredConsole.WritePrimaryLogLine(Decide(review.Result));
    WriteTokenUsage($"Token Usage ({reviewerAgent.Name})", review);

    // Step 5: Same review for a wrong answer written by hand: the decision of the code must hold it back
    ColoredConsole.WriteAssistantLine($"Answer written by hand (wrong on purpose): {ReviewRequests.WrongAnswer}");
    AgentResponse<QualityReview> wrongReview = await reviewerAgent.RunAsync<QualityReview>(
        string.Format(CultureInfo.InvariantCulture, ReviewRequests.QuestionAndAnswer, CustomerMessages.HeadphonesReturn, ReviewRequests.WrongAnswer)).WithSpinner("Running the reviewer");
    WriteReview(wrongReview.Result);
    ColoredConsole.WritePrimaryLogLine(Decide(wrongReview.Result));
    WriteTokenUsage($"Token Usage ({reviewerAgent.Name})", wrongReview);
}

#endregion

#region Scenario 2: Delegation by the model - specialist agents as tools

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Delegation by the model - specialist agents as tools ===");

    // Step 1: Create the order specialist. It has its own tool (get_order): the front desk will never see it
    AIAgent orderAgent = chatClient.AsAIAgent(
        instructions: Agents.OrderSpecialist.Instructions,
        name: Agents.OrderSpecialist.Name,
        tools: [AIFunctionFactory.Create(OrderTools.GetOrder, "get_order")]);

    // Step 2: Turn the two specialists into tools. The name and the description are all the model of the front desk
    // knows about them: it uses them to decide when to call each specialist, and what to write in the query
    AIFunction orderTool = orderAgent.AsAIFunction(new AIFunctionFactoryOptions
    {
        Name = "ask_order_specialist",
        Description = ToolDescriptions.AskOrderSpecialist
    });
    AIFunction afterSalesTool = afterSalesAgent.AsAIFunction(new AIFunctionFactoryOptions
    {
        Name = "ask_after_sales_expert",
        Description = ToolDescriptions.AskAfterSalesExpert
    });
    WriteAgentTools([orderTool, afterSalesTool]);

    // Step 3: Create the front desk agent, with the two specialists as tools
    AIAgent frontDeskAgent = chatClient.AsAIAgent(
        instructions: Agents.FrontDesk.Instructions,
        name: Agents.FrontDesk.Name,
        tools: [orderTool, afterSalesTool]);

    // Step 4: Run the front desk: its model decides which specialist to call, and writes the query each one receives
    ColoredConsole.WriteUserLine($"Customer: {CustomerMessages.BrokenLaptopScreen}");
    AgentResponse response = await frontDeskAgent.RunAsync(CustomerMessages.BrokenLaptopScreen).WithSpinner("Running the front desk");

    // Step 5: Display the delegations (what each specialist received and returned), then the answer
    WriteDelegations(response);
    ColoredConsole.WriteAssistantLine($"{frontDeskAgent.Name}: {response.Text}");

    // Step 6: Display token usage: only the model calls of the front desk, the runs of the specialists are not included
    WriteTokenUsage($"Token Usage ({frontDeskAgent.Name} only, without the specialists)", response);
}

#endregion

#region Scenario 3: Keep the context of a specialist with a session

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: Keep the context of a specialist with a session ===");

    // Step 1: Create the ticket specialist: it keeps the support ticket of a case up to date with the facts it receives
    AIAgent ticketAgent = chatClient.AsAIAgent(
        instructions: Agents.TicketSpecialist.Instructions,
        name: Agents.TicketSpecialist.Name,
        description: Agents.TicketSpecialist.Description);

    // Step 2: Turn it into two tools. Without options, the name and the description of the tool come from the agent.
    // - without session: a new session for each call (the default), so each call starts from nothing
    // - with a session: every call continues the same conversation with the specialist
    AIFunction toolWithoutSession = ticketAgent.AsAIFunction();
    AgentSession ticketSession = await ticketAgent.CreateSessionAsync();
    AIFunction toolWithSession = ticketAgent.AsAIFunction(session: ticketSession);
    ColoredConsole.WritePrimaryLogLine($"Tool {toolWithoutSession.Name}: {toolWithoutSession.Description}");

    // Step 3: Give the same two facts to each tool, one call per fact, then display the ticket returned by the last call.
    // The code calls the tools itself with the "query" argument, as the function calling loop does when a model calls them:
    // the result does not depend on how a model would write the queries. The calls are made one after the other:
    // a tool bound to a session must not be called concurrently
    string[] caseFacts = [CaseFacts.BrokenScreen, CaseFacts.ReplacementRequested];
    (string Heading, AIFunction Tool)[] variants =
    [
        ("Ticket after the second call - a new session for each call", toolWithoutSession),
        ("Ticket after the second call - one session for every call", toolWithSession)
    ];
    foreach ((string heading, AIFunction tool) in variants)
    {
        object? ticket = null;
        foreach (string fact in caseFacts)
        {
            ColoredConsole.WriteWarningLine($"[Call] {tool.Name}(query: \"{fact}\")");
            ticket = await tool.InvokeAsync(new AIFunctionArguments { ["query"] = fact }).AsTask().WithSpinner("Calling the specialist");
        }

        WriteTicket(heading, ticket?.ToString() ?? string.Empty, "ORD-1042");
    }

    // Step 4: No token usage here: an agent tool returns only the text of the answer
    ColoredConsole.WriteSecondaryLogLine("Token usage: not available, an agent tool returns only the text of the answer.");
}

#endregion
