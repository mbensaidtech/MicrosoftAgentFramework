using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using UseAgentAsTool.Configuration;

namespace UseAgentAsTool;

/// <summary>
/// Helper class for loading configuration.
/// </summary>
public static class ConfigurationHelper
{
    private static IConfiguration? configuration;

    public static IConfiguration Configuration => configuration ??= BuildConfiguration();

    private static IConfiguration BuildConfiguration()
    {
        var builder = Host.CreateApplicationBuilder();
        return builder.Configuration;
    }

    public static AzureOpenAISettings GetAzureOpenAISettings()
    {
        return Configuration.GetSection("AzureOpenAI").Get<AzureOpenAISettings>()
            ?? throw new InvalidOperationException("AzureOpenAI configuration section is missing");
    }

    public static AgentSettings GetAgentSettings(AgentType agentType)
    {
        var agentName = agentType.ToString();
        return Configuration.GetSection($"Agents:{agentName}").Get<AgentSettings>()
            ?? throw new InvalidOperationException($"Agent '{agentName}' configuration is missing");
    }
}
