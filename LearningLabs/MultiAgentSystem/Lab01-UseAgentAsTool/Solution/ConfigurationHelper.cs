using Microsoft.Extensions.Configuration;
using UseAgentAsTool.Configuration;

namespace UseAgentAsTool;

public static class ConfigurationHelper
{
    private static IConfiguration? _configuration;

    public static IConfiguration Configuration => _configuration ??= BuildConfiguration();

    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();
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
