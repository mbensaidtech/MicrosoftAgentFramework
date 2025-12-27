using Microsoft.Extensions.Configuration;

namespace A2AClient;

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

    public static RemoteAuthAgentSettings GetRemoteAuthAgentSettings()
    {
        return Configuration.GetSection("RemoteAuthAgentSettings").Get<RemoteAuthAgentSettings>()
            ?? throw new InvalidOperationException("RemoteAuthAgentSettings configuration section is missing");
    }
}

