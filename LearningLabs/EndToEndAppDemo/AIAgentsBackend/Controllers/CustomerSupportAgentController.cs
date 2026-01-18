using AIAgentsBackend.Agents.Factory;
using AIAgentsBackend.Controllers.Base;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Mvc;

namespace AIAgentsBackend.Controllers;

/// <summary>
/// Controller for the Customer Support Agent.
/// Inherits common chat/stream logic from ChatClientAgentControllerBase.
/// </summary>
[ApiController]
[Route("api/agents/customer-support")]
public class CustomerSupportAgentController : ChatClientAgentControllerBase
{
    private readonly IAgentFactory agentFactory;

    public CustomerSupportAgentController(IAgentFactory agentFactory)
    {
        this.agentFactory = agentFactory;
    }

    protected override string AgentName => "CustomerSupportAgent";

    protected override ChatClientAgent GetAgent()
    {
        return agentFactory.GetCustomerSupportAgent().Agent;
    }
}
