using AIAgentsBackend.Agents.Factory;
using AIAgentsBackend.Controllers.Base;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Mvc;

namespace AIAgentsBackend.Controllers;

/// <summary>
/// Controller for the History Agent.
/// Inherits common chat/stream logic from ChatClientAgentControllerBase.
/// </summary>
[ApiController]
[Route("api/agents/history")]
public class HistoryAgentController : ChatClientAgentControllerBase
{
    private readonly IAgentFactory agentFactory;

    public HistoryAgentController(IAgentFactory agentFactory)
    {
        this.agentFactory = agentFactory;
    }

    protected override string AgentName => "HistoryAgent";

    protected override ChatClientAgent GetAgent()
    {
        return agentFactory.GetHistoryAgent().Agent;
    }
}
