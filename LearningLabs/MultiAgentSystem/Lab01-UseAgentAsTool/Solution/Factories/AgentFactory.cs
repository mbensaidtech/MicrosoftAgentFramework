using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using OpenAI;
using UseAgentAsTool.Configuration;

namespace UseAgentAsTool.Factories;

/// <summary>
/// Factory for creating AI agents from configuration.
/// Each agent can use a different deployment.
/// </summary>
public class AgentFactory
{
    private readonly AzureOpenAIClient _openAIClient;
    private readonly string _defaultDeploymentName;
    
    /// <summary>
    /// SAV rules loaded from the YAML file.
    /// </summary>
    public string SavRules { get; }

    public AgentFactory(AzureOpenAIClient openAIClient)
    {
        _openAIClient = openAIClient;
        _defaultDeploymentName = ConfigurationHelper.GetAzureOpenAISettings().DefaultDeploymentName;
        SavRules = LoadSavRules();
    }

    private static string LoadSavRules()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "MockedData", "sav-rules.yml");
        return File.ReadAllText(path);
    }

    #region Named Agent Creation Methods

    /// <summary>
    /// Creates the Reformulator agent that extracts keywords from messages.
    /// </summary>
    public ChatClientAgent CreateReformulatorAgent()
    {
        return CreateAgent(AgentType.Reformulator);
    }

    /// <summary>
    /// Creates the Reformulator Judge agent that evaluates keyword extraction quality.
    /// </summary>
    public ChatClientAgent CreateReformulatorJudgeAgent()
    {
        return CreateAgent(AgentType.ReformulatorJudge);
    }

    /// <summary>
    /// Creates the SAV agent that answers after-sales service questions.
    /// Includes SAV rules in the instructions.
    /// </summary>
    public ChatClientAgent CreateSAVAgent()
    {
        return CreateAgentWithSavRules(AgentType.SAVAgent);
    }

    /// <summary>
    /// Creates the SAV Judge agent that evaluates SAV responses.
    /// Includes SAV rules in the instructions.
    /// </summary>
    public ChatClientAgent CreateSAVJudgeAgent()
    {
        return CreateAgentWithSavRules(AgentType.SAVJudge);
    }

    /// <summary>
    /// Creates the Orchestrator agent that coordinates all other agents.
    /// </summary>
    public ChatClientAgent CreateOrchestratorAgent(params AITool[] tools)
    {
        return CreateAgent(AgentType.Orchestrator, tools);
    }

    #endregion

    #region Generic Methods

    /// <summary>
    /// Creates an agent from the specified agent type with optional tools.
    /// </summary>
    public ChatClientAgent CreateAgent(AgentType agentType, params AITool[] tools)
    {
        var settings = ConfigurationHelper.GetAgentSettings(agentType);
        var chatClient = GetChatClientForAgent(settings);
        
        return CreateAgentFromSettings(chatClient, settings, tools);
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Creates an agent with SAV rules appended to its instructions.
    /// </summary>
    private ChatClientAgent CreateAgentWithSavRules(AgentType agentType)
    {
        var settings = ConfigurationHelper.GetAgentSettings(agentType);
        var chatClient = GetChatClientForAgent(settings);
        
        var instructionsWithRules = $"""
            {settings.Instructions}

            ## SAV Rules Reference
            Use the following rules to guide your responses:

            {SavRules}
            """;

        ChatClientAgent agent = (ChatClientAgent) chatClient.CreateAIAgent(
            instructions: instructionsWithRules,
            name: settings.Name);

        return agent;
    }

    /// <summary>
    /// Gets a ChatClient for the agent's deployment.
    /// Uses agent-specific deployment if configured, otherwise uses default.
    /// </summary>
    private ChatClient GetChatClientForAgent(AgentSettings settings)
    {
        var deploymentName = settings.DeploymentName ?? _defaultDeploymentName;
        return _openAIClient.GetChatClient(deploymentName);
    }

    /// <summary>
    /// Creates an agent from settings with optional tools.
    /// </summary>
    private static ChatClientAgent CreateAgentFromSettings(ChatClient chatClient, AgentSettings settings, params AITool[] tools)
    {
        if (tools.Length > 0)
        {
            return (ChatClientAgent) chatClient.CreateAIAgent(
                instructions: settings.Instructions,
                name: settings.Name,
                tools: tools.ToList());
        }

        return (ChatClientAgent) chatClient.CreateAIAgent(
            instructions: settings.Instructions,
            name: settings.Name);
    }

    #endregion
}
