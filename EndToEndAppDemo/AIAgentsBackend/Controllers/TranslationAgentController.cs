using AIAgentsBackend.Agents.Factory;
using AIAgentsBackend.Controllers.Base;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Mvc;

namespace AIAgentsBackend.Controllers;

/// <summary>
/// Controller for the Translation Agent.
/// Inherits common chat/stream logic from AIAgentControllerBase.
/// Uses AIAgent with structured JSON output.
/// </summary>
[ApiController]
[Route("api/agents/translation")]
public class TranslationAgentController : AIAgentControllerBase
{
    private readonly IAgentFactory agentFactory;

    public TranslationAgentController(IAgentFactory agentFactory)
    {
        this.agentFactory = agentFactory;
    }

    protected override string AgentName => "TranslationAgent";

    protected override AIAgent GetAgent()
    {
        return agentFactory.GetTranslationAgent().Agent;
    }
}
