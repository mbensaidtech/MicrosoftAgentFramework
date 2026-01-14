using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace A2AClient;

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

    public static RemoteAuthAgentSettings GetRemoteAuthAgentSettings()
    {
        return Configuration.GetSection("RemoteAuthAgentSettings").Get<RemoteAuthAgentSettings>()
            ?? throw new InvalidOperationException("RemoteAuthAgentSettings configuration section is missing");
    }
}
