using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace AIAgentWithMCPClient;

/// <summary>
/// Helper class for loading configuration.
/// </summary>
public static class ConfigurationHelper
{
    private static IConfiguration? _configuration;

    public static IConfiguration Configuration => _configuration ??= BuildConfiguration();

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

    public static MCPServerSettings GetMCPServerSettings(string serverName)
    {
        return Configuration.GetSection($"MCPServers:{serverName}").Get<MCPServerSettings>()
            ?? throw new InvalidOperationException($"MCPServers:{serverName} configuration section is missing");
    }
}
