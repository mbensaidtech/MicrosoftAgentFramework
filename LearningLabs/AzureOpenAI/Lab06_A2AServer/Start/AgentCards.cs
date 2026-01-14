using A2A;
namespace A2AServer;

/// <summary>
/// Contains static methods for creating agent cards for each agent type.
/// </summary>
public static class AgentCards
{
    public static AgentCard CreateCustomerToneAgentCard()
    {
        return new AgentCard
        {
            Name = "CustomerToneAgent",
            Description = "A customer tone assistant that can detect the tone of a customer's message.",
            Version = "1.0.0",
            DefaultInputModes = ["text"],
            DefaultOutputModes = ["text"],
            Capabilities = new AgentCapabilities() {

                Streaming = false,
                PushNotifications = false,
            },
            Skills = [],
            Url="http://localhost:5000/a2a/customerToneAgent"
        };
    }
    public static AgentCard CreateAuthAgentCard()
    {

        var generateAPIKey = new AgentSkill() {
            Id="auth_agent_generate_api_key",
            Name = "GenerateAPIKey",
            Description = "Generates a new random API key that starts with 'Meknes'. The key includes a cryptographic signature for validation.",
            Tags = ["api", "key", "security", "authentication"],
            Examples = [
                "Generate a new API key",
                "Create an API key for me",
                "I need a new API key"
            ],
        };

        var validateAPIKey = new AgentSkill() {
            Id="auth_agent_validate_api_key",
            Name = "ValidateAPIKey",
            Description = "Validates an API key by checking if it starts with 'Meknes' and verifying its cryptographic signature.",
            Tags = ["api", "key", "security", "validation", "authentication"],
            Examples = [
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
            DefaultInputModes = ["text"],
            DefaultOutputModes = ["text"],
            Capabilities = new AgentCapabilities() {

                Streaming = false,
                PushNotifications = false,
            },
            Skills = [ generateAPIKey, validateAPIKey],
            Url="http://localhost:5000/a2a/authAgent"
        };
    }

   
}

