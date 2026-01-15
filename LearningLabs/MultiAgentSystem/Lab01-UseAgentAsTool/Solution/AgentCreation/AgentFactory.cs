using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using OpenAI;
using UseAgentAsTool.Configuration;

namespace UseAgentAsTool.AgentCreation;

/// <summary>
/// Factory for creating AI agents from configuration.
/// Each agent can use a different deployment.
/// </summary>
public class AgentFactory
{
    private readonly AzureOpenAIClient openAIClient;
    private readonly string defaultDeploymentName;
    
    /// <summary>
    /// SAV rules loaded from the YAML file.
    /// </summary>
    public string SavRules { get; }

    public AgentFactory(AzureOpenAIClient openAIClient)
    {
        this.openAIClient = openAIClient;
        defaultDeploymentName = ConfigurationHelper.GetAzureOpenAISettings().DefaultDeploymentName;
        SavRules = LoadSavRules();
    }

    private static string LoadSavRules()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "MockedData", "sav-rules.yml");
        return File.ReadAllText(path);
    }

    /// <summary>
    /// Creates a new agent builder for fluent agent configuration.
    /// </summary>
    public AgentBuilder CreateBuilder(AgentType agentType)
    {
        var settings = ConfigurationHelper.GetAgentSettings(agentType);
        var chatClient = GetChatClientForAgent(settings);
        return new AgentBuilder(chatClient, settings);
    }

    /// <summary>
    /// Creates the Reformulator agent that extracts keywords from messages.
    /// </summary>
    public ChatClientAgent CreateReformulatorAgent()
    {
        return CreateBuilder(AgentType.Reformulator).Build();
    }

    /// <summary>
    /// Creates the Reformulator Judge agent that evaluates keyword extraction quality.
    /// </summary>
    public ChatClientAgent CreateReformulatorJudgeAgent()
    {
        return CreateBuilder(AgentType.ReformulatorJudge).Build();
    }

    /// <summary>
    /// Creates the SAV agent that answers after-sales service questions.
    /// Includes SAV rules in the instructions.
    /// </summary>
    public ChatClientAgent CreateSAVAgent()
    {
        return CreateBuilder(AgentType.SAVAgent)
            .WithInstructionModifier(AppendSavRules)
            .Build();
    }

    /// <summary>
    /// Creates the SAV Judge agent that evaluates SAV responses.
    /// Includes SAV rules in the instructions.
    /// </summary>
    public ChatClientAgent CreateSAVJudgeAgent()
    {
        return CreateBuilder(AgentType.SAVJudge)
            .WithInstructionModifier(AppendSavRules)
            .Build();
    }

    /// <summary>
    /// Creates the Orchestrator agent that coordinates all other agents.
    /// </summary>
    public ChatClientAgent CreateOrchestratorAgent(params AITool[] tools)
    {
        return CreateBuilder(AgentType.Orchestrator)
            .WithTools(tools)
            .Build();
    }

    /// <summary>
    /// Appends SAV rules to the given instructions.
    /// Can be used with the builder's WithInstructionModifier method.
    /// </summary>
    public string AppendSavRules(string baseInstructions)
    {
        return $"""
            {baseInstructions}

            ## SAV Rules Reference
            Use the following rules to guide your responses:

            {SavRules}
            """;
    }

    /// <summary>
    /// Gets a ChatClient for the agent's deployment.
    /// Uses agent-specific deployment if configured, otherwise uses default.
    /// </summary>
    private ChatClient GetChatClientForAgent(AgentSettings settings)
    {
        var deploymentName = settings.DeploymentName ?? defaultDeploymentName;
        return openAIClient.GetChatClient(deploymentName);
    }

}

