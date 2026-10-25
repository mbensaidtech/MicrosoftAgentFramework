using System.ClientModel;
using System.ClientModel.Primitives;
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

// TODO 3: Get a ChatClient (Chat Completions API) for the deployment: every agent of this lab uses it
// ChatClient chatClient = ...

// TODO 4: Load the after-sales rules (MockedData/sav-rules.yml) and create the after-sales expert, used by scenarios 1 and 2
// Hint: string savRules = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "MockedData", "sav-rules.yml"));
//       chatClient.AsAIAgent(instructions: <see the README, the rules are appended to the instructions>, name: "AfterSalesExpert")
// AIAgent afterSalesAgent = ...

#endregion

#region Scenario 1: Delegation by code - a reviewer agent checks the answer

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 1: Delegation by code - a reviewer agent checks the answer ===");

    // TODO 5: Create the reviewer agent: it knows the same rules, and scores the answers (see the README for the instructions)
    // AIAgent reviewerAgent = ...

    // TODO 6: Have the after-sales expert answer the customer (a direct call), then display the answer and the token usage
    // Hint: const string CustomerQuestion = "I bought headphones 10 days ago. ..." (see the README);
    //       ColoredConsole.WriteUserLine($"Customer: {CustomerQuestion}");
    //       await afterSalesAgent.RunAsync(CustomerQuestion).WithSpinner("Running the after-sales expert");
    //       ColoredConsole.WriteAssistantLine($"{afterSalesAgent.Name}: {answer.Text}");
    //       WriteTokenUsage($"Token Usage ({afterSalesAgent.Name})", answer)
    // AgentResponse answer = ...

    // TODO 7: Write the decision rule of the code: a fixed rule on the score of the review
    // Hint: const int MinimumScore = 7;
    //       string Decide(QualityReview review) => review.Score is >= MinimumScore and <= 10
    //           ? "Decision: sent to the customer" : $"Decision: held back for a human agent (score below {MinimumScore})";

    // TODO 8: Give the answer to the reviewer and get a typed review back, then display the review, the decision and the token usage
    // Hint: await reviewerAgent.RunAsync<QualityReview>($"Customer question: {CustomerQuestion}\nAnswer to review: {answer.Text}")
    //       .WithSpinner("Running the reviewer"); WriteReview(review.Result);
    //       ColoredConsole.WritePrimaryLogLine(Decide(review.Result)); WriteTokenUsage($"Token Usage ({reviewerAgent.Name})", review)
    // AgentResponse<QualityReview> review = ...

    // TODO 9: Same review for a wrong answer written by hand (see the README): the decision must hold it back
    // Hint: ColoredConsole.WriteAssistantLine($"Answer written by hand (wrong on purpose): {WrongAnswer}"), then as TODO 8
    // AgentResponse<QualityReview> wrongReview = ...
}

#endregion

#region Scenario 2: Delegation by the model - specialist agents as tools

if (ShouldRunScenario(2))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 2: Delegation by the model - specialist agents as tools ===");

    // TODO 10: Create the order specialist, with its own tool get_order (OrderTools.GetOrder, in Tools/OrderTools.cs)
    // Hint: chatClient.AsAIAgent(instructions: ..., name: "OrderSpecialist", tools: [AIFunctionFactory.Create(OrderTools.GetOrder, "get_order")])
    // AIAgent orderAgent = ...

    // TODO 11: Turn the two specialists into tools with a name and a description, then display what the model will see
    // Hint: orderAgent.AsAIFunction(new AIFunctionFactoryOptions { Name = "ask_order_specialist", Description = "..." }),
    //       same for afterSalesAgent ("ask_after_sales_expert"), then WriteAgentTools([orderTool, afterSalesTool])
    // AIFunction orderTool = ...
    // AIFunction afterSalesTool = ...

    // TODO 12: Create the front desk agent, with the two specialists as tools
    // Hint: chatClient.AsAIAgent(instructions: ..., name: "FrontDesk", tools: [orderTool, afterSalesTool])
    // AIAgent frontDeskAgent = ...

    // TODO 13: Run the front desk with the customer message (see the README)
    // Hint: ColoredConsole.WriteUserLine($"Customer: {CustomerMessage}"); await frontDeskAgent.RunAsync(CustomerMessage).WithSpinner("Running the front desk")
    // AgentResponse response = ...

    // TODO 14: Display the delegations, then the answer
    // Hint: WriteDelegations(response); ColoredConsole.WriteAssistantLine($"{frontDeskAgent.Name}: {response.Text}")

    // TODO 15: Display the token usage of the front desk (the runs of the specialists are not included)
    // Hint: WriteTokenUsage($"Token Usage ({frontDeskAgent.Name} only, without the specialists)", response)
}

#endregion

#region Scenario 3: Keep the context of a specialist with a session

if (ShouldRunScenario(3))
{
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WriteInfoLine("=== Scenario 3: Keep the context of a specialist with a session ===");

    // TODO 16: Create the ticket specialist, with a description (see the README for the instructions)
    // Hint: chatClient.AsAIAgent(instructions: ..., name: "TicketSpecialist", description: "...")
    // AIAgent ticketAgent = ...

    // TODO 17: Turn it into two tools, without and with a session, and display the name and the description of the tool
    // Hint: ticketAgent.AsAIFunction(); AgentSession ticketSession = await ticketAgent.CreateSessionAsync();
    //       ticketAgent.AsAIFunction(session: ticketSession);
    //       ColoredConsole.WritePrimaryLogLine($"Tool {toolWithoutSession.Name}: {toolWithoutSession.Description}")
    // AIFunction toolWithoutSession = ...
    // AIFunction toolWithSession = ...

    // TODO 18: Give the same two facts to each tool, one call per fact, and display the ticket returned by the last call
    // Hint: string[] caseFacts = ["Order ORD-1042: the laptop screen arrived broken.", "The customer wants a replacement."];
    //       for each tool: ticket = await tool.InvokeAsync(new AIFunctionArguments { ["query"] = fact }).AsTask().WithSpinner("Calling the specialist");
    //       then WriteTicket("Ticket after the second call - ...", ticket?.ToString() ?? string.Empty, "ORD-1042") (see the README)

    // TODO 19: Say why there is no token usage here
    // Hint: ColoredConsole.WriteSecondaryLogLine("Token usage: not available, an agent tool returns only the text of the answer.")
}

#endregion
