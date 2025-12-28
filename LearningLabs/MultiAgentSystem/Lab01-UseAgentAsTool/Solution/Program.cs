using Azure.Identity;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using AIExtensions = Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using CommonUtilities;
using UseAgentAsTool;
using UseAgentAsTool.Factories;
using UseAgentAsTool.Models;

// ============================================
// SCENARIO SELECTION - Choose which scenarios to run
// ============================================
// Set to: [1], [2], or [1, 2] to run specific scenarios
HashSet<int> scenariosToRun = [1];
// ============================================

bool ShouldRunScenario(int scenario) => scenariosToRun.Count == 0 || scenariosToRun.Contains(scenario);

#region Setup: Configuration and Client Initialization

// Step 1: Load Azure OpenAI settings from configuration
var settings = ConfigurationHelper.GetAzureOpenAISettings();
Console.WriteLine($"Endpoint: {settings.Endpoint}");
Console.WriteLine($"Default Deployment: {settings.DefaultDeploymentName}");

// Step 2: Create AzureOpenAIClient (singleton) with managed identity authentication
AzureOpenAIClient openAIClient = new AzureOpenAIClient(new Uri(settings.Endpoint), new DefaultAzureCredential());

// Step 3: Create the agent factory (ChatClient is created per agent based on deployment)
var agentFactory = new AgentFactory(openAIClient);

#endregion

ColoredConsole.WriteDividerLine();

#region Scenario 1: Multi-Agent SAV System with Quality Evaluation

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteInfoLine("=== Scenario 1: Multi-Agent SAV System with Quality Evaluation ===");

    // Step 1: Create all specialized agents
    var reformulatorAgent = agentFactory.CreateReformulatorAgent();
    var reformulatorJudgeAgent = agentFactory.CreateReformulatorJudgeAgent();
    var savAgent = agentFactory.CreateSAVAgent();
    var savJudgeAgent = agentFactory.CreateSAVJudgeAgent();

    // Step 2: Create tools that wrap each agent

    // Tool 1: Reformulate message to keywords
    var reformulateTool = reformulatorAgent.AsAIFunction(new AIExtensions.AIFunctionFactoryOptions
    {
        Name = "reformulate_message",
        Description = "Reformulates a message to keywords",
    });

    // Tool 2: Judge the reformulation quality
    var judgeReformulationTool = reformulatorJudgeAgent.AsAIFunction(new AIExtensions.AIFunctionFactoryOptions
    {
        Name = "judge_reformulation",
        Description = "Judges the quality of the reformulation",
    });

    // Tool 3: Answer SAV question
    var answerSAVTool = savAgent.AsAIFunction(new AIExtensions.AIFunctionFactoryOptions
    {
        Name = "answer_sav_question",
        Description = "Answers customer service questions about returns, refunds, exchanges, warranty, and delivery issues",
    });

    // Tool 4: Judge the SAV answer quality
    var judgeSAVTool = savJudgeAgent.AsAIFunction(new AIExtensions.AIFunctionFactoryOptions
    {
        Name = "judge_sav_answer",
        Description = "Evaluates the quality and accuracy of a customer service response",
    });

    // Step 3: Create orchestrator with all tools
    ChatClientAgent orchestratorAgent = agentFactory.CreateOrchestratorAgent(
        reformulateTool,
        judgeReformulationTool,
        answerSAVTool,
        judgeSAVTool);

    // Step 4: Run the orchestrator with a customer question (with structured output)
    var customerQuestion = "I bought a laptop 2 weeks ago and the screen is broken. What can I do? Can I get a refund or exchange?";
    
    ColoredConsole.WritePrimaryLogLine("Customer Question:");
    ColoredConsole.WriteSecondaryLogLine(customerQuestion);
    ColoredConsole.WriteDividerLine();

    AgentRunResponse<OrchestratorResponse> response = await orchestratorAgent
        .RunAsync<OrchestratorResponse>(customerQuestion)
        .WithSpinner("Agent is thinking");

    // Step 5: Display the structured response
    ColoredConsole.WritePrimaryLogLine("=== Official Response (for customer) ===");
    ColoredConsole.WriteSecondaryLogLine(response.Result.OfficialResponse);
    
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WritePrimaryLogLine("=== Reformulation Judge Evaluation ===");
    ColoredConsole.WriteSecondaryLogLine($"Score: {response.Result.ReformulationJudge.Score}/10");
    ColoredConsole.WriteSecondaryLogLine($"Feedback: {response.Result.ReformulationJudge.Feedback}");
    
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WritePrimaryLogLine("=== SAV Judge Evaluation ===");
    ColoredConsole.WriteSecondaryLogLine($"Score: {response.Result.SavJudge.Score}/10");
    ColoredConsole.WriteSecondaryLogLine($"Feedback: {response.Result.SavJudge.Feedback}");

    // Step 6: Display token usage
    ColoredConsole.WriteDividerLine();
    ColoredConsole.WritePrimaryLogLine("Token Usage:");
    ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {response.Usage?.InputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {response.Usage?.OutputTokenCount}");
    ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {response.Usage?.TotalTokenCount}");
}

#endregion
