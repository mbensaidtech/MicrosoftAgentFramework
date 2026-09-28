using A2A;

namespace A2AServer;

/// <summary>
/// Contains static methods for creating the agent card of each agent.
/// An agent card is the discovery document of an A2A agent: who it is, what it can do (skills),
/// and where and how to call it (supported interfaces: URL + protocol binding + protocol version).
/// </summary>
public static class AgentCards
{
    public static AgentCard CreateAuthAgentCard(string agentUrl)
    {
        var generateAPIKey = new AgentSkill
        {
            Id = "auth_agent_generate_api_key",
            Name = "GenerateAPIKey",
            Description = "Generates a new random API key that starts with 'Meknes'. The key includes a cryptographic signature for validation.",
            Tags = ["api", "key", "security", "authentication"],
            Examples =
            [
                "Generate a new API key",
                "Create an API key for me",
                "I need a new API key"
            ],
        };

        var validateAPIKey = new AgentSkill
        {
            Id = "auth_agent_validate_api_key",
            Name = "ValidateAPIKey",
            Description = "Validates an API key by checking if it starts with 'Meknes' and verifying its cryptographic signature.",
            Tags = ["api", "key", "security", "validation", "authentication"],
            Examples =
            [
                "Validate this API key: Meknes...",
                "Check if this API key is valid",
                "Verify the API key"
            ],
        };

        return new AgentCard
        {
            Name = "AuthAgent",
            Description = "An authentication agent specialized in generating and validating API keys. Only handles authentication-related tasks.",
            Version = "1.0.0",
            DefaultInputModes = ["text/plain"],
            DefaultOutputModes = ["text/plain"],
            Capabilities = new AgentCapabilities
            {
                Streaming = false,
                PushNotifications = false,
            },
            Skills = [generateAPIKey, validateAPIKey],
            SupportedInterfaces = CreateInterfaces(agentUrl),
        };
    }

    public static AgentCard CreateCustomerToneAgentCard(string agentUrl)
    {
        var detectTone = new AgentSkill
        {
            Id = "customer_tone_agent_detect_tone",
            Name = "DetectTone",
            Description = "Detects the tone of a customer's message (for example angry, neutral or satisfied).",
            Tags = ["customer", "tone", "sentiment"],
            Examples = ["What is the tone of this message: I have been waiting for two weeks!"],
        };

        return new AgentCard
        {
            Name = "CustomerToneAgent",
            Description = "A customer tone assistant that can detect the tone of a customer's message.",
            Version = "1.0.0",
            DefaultInputModes = ["text/plain"],
            DefaultOutputModes = ["text/plain"],
            Capabilities = new AgentCapabilities
            {
                Streaming = false,
                PushNotifications = false,
            },
            Skills = [detectTone],
            SupportedInterfaces = CreateInterfaces(agentUrl),
        };
    }

    // The server maps both protocol bindings of A2A v1 at the same URL (MapA2AJsonRpc + MapA2AHttpJson):
    // clients prefer HTTP+JSON and fall back to JSON-RPC.
    private static List<AgentInterface> CreateInterfaces(string agentUrl) =>
    [
        new AgentInterface { Url = agentUrl, ProtocolBinding = ProtocolBindingNames.JsonRpc, ProtocolVersion = "1.0" },
        new AgentInterface { Url = agentUrl, ProtocolBinding = ProtocolBindingNames.HttpJson, ProtocolVersion = "1.0" },
    ];
}
