using Azure.Identity;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using AIExtensions = Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using CommonUtilities;
using UseAgentAsTool;
using UseAgentAsTool.AgentCreation;
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

// TODO 1: Create AzureOpenAIClient with managed identity authentication
// HINT: Use 'new AzureOpenAIClient(new Uri(settings.Endpoint), new DefaultAzureCredential())'
// AzureOpenAIClient openAIClient = ...

// TODO 2: Create the agent factory
// HINT: Pass the openAIClient to the AgentFactory constructor
// var agentFactory = ...

#endregion

ColoredConsole.WriteDividerLine();

#region Scenario 1: Multi-Agent SAV System with Quality Evaluation

if (ShouldRunScenario(1))
{
    ColoredConsole.WriteInfoLine("=== Scenario 1: Multi-Agent SAV System with Quality Evaluation ===");

    // TODO 3: Create all specialized agents using the factory
    // HINT: Use agentFactory.CreateReformulatorAgent(), CreateReformulatorJudgeAgent(), etc.
    // var reformulatorAgent = ...
    // var reformulatorJudgeAgent = ...
    // var savAgent = ...
    // var savJudgeAgent = ...

    // TODO 4: Create tools that wrap each agent using AsAIFunction
    // HINT: Use the pattern: agent.AsAIFunction(new AIExtensions.AIFunctionFactoryOptions { Name = "...", Description = "..." })
    
    // Tool 1: Reformulate message to keywords
    // var reformulateTool = reformulatorAgent.AsAIFunction(new AIExtensions.AIFunctionFactoryOptions
    // {
    //     Name = "reformulate_message",
    //     Description = "Reformulates a message to keywords",
    // });

    // Tool 2: Judge the reformulation quality
    // var judgeReformulationTool = ...

    // Tool 3: Answer SAV question
    // var answerSAVTool = ...

    // Tool 4: Judge the SAV answer quality
    // var judgeSAVTool = ...

    // TODO 5: Create orchestrator agent with all tools
    // HINT: Use agentFactory.CreateOrchestratorAgent(reformulateTool, judgeReformulationTool, answerSAVTool, judgeSAVTool)
    // ChatClientAgent orchestratorAgent = ...

    // TODO 6: Run the orchestrator with a customer question
    var customerQuestion = "I bought a laptop 2 weeks ago and the screen is broken. What can I do? Can I get a refund or exchange?";
    
    ColoredConsole.WritePrimaryLogLine("Customer Question:");
    ColoredConsole.WriteSecondaryLogLine(customerQuestion);
    ColoredConsole.WriteDividerLine();

    // HINT: Use orchestratorAgent.RunAsync<OrchestratorResponse>(customerQuestion).WithSpinner("Agent is thinking")
    // AgentRunResponse<OrchestratorResponse> response = ...

    // TODO 7: Display the structured response
    // HINT: Access response.Result.OfficialResponse, response.Result.ReformulationJudge, response.Result.SavJudge
    // ColoredConsole.WritePrimaryLogLine("=== Official Response (for customer) ===");
    // ColoredConsole.WriteSecondaryLogLine(response.Result.OfficialResponse);
    
    // ColoredConsole.WriteDividerLine();
    // ColoredConsole.WritePrimaryLogLine("=== Reformulation Judge Evaluation ===");
    // ColoredConsole.WriteSecondaryLogLine($"Score: {response.Result.ReformulationJudge.Score}/10");
    // ColoredConsole.WriteSecondaryLogLine($"Feedback: {response.Result.ReformulationJudge.Feedback}");
    
    // ColoredConsole.WriteDividerLine();
    // ColoredConsole.WritePrimaryLogLine("=== SAV Judge Evaluation ===");
    // ColoredConsole.WriteSecondaryLogLine($"Score: {response.Result.SavJudge.Score}/10");
    // ColoredConsole.WriteSecondaryLogLine($"Feedback: {response.Result.SavJudge.Feedback}");

    // TODO 8: Display token usage
    // HINT: Access response.Usage?.InputTokenCount, OutputTokenCount, TotalTokenCount
    // ColoredConsole.WriteDividerLine();
    // ColoredConsole.WritePrimaryLogLine("Token Usage:");
    // ColoredConsole.WriteSecondaryLogLine($"  Input tokens: {response.Usage?.InputTokenCount}");
    // ColoredConsole.WriteSecondaryLogLine($"  Output tokens: {response.Usage?.OutputTokenCount}");
    // ColoredConsole.WriteSecondaryLogLine($"  Total tokens: {response.Usage?.TotalTokenCount}");
}

#endregion
