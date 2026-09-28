using Microsoft.Extensions.Configuration;

namespace A2AClient;

/// <summary>
/// Helper class for loading configuration.
/// Sources, from lowest to highest priority: appsettings.json, user secrets, environment variables
/// (e.g. <c>AzureOpenAI__APIKey</c>, <c>RemoteAgents__AuthAgent__Url</c>).
/// </summary>
public static class ConfigurationHelper
{
    private static IConfiguration? _configuration;

    public static IConfiguration Configuration => _configuration ??= BuildConfiguration();

    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets<AzureOpenAISettings>(optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    /// <summary>
    /// Get the Azure OpenAI settings from the configuration.
    /// </summary>
    /// <returns>The Azure OpenAI settings.</returns>
    public static AzureOpenAISettings GetAzureOpenAISettings()
    {
        AzureOpenAISettings settings = Configuration.GetSection("AzureOpenAI").Get<AzureOpenAISettings>()
            ?? throw new InvalidOperationException("AzureOpenAI configuration section is missing.");

        EnsureConfigured(settings.Endpoint, "AzureOpenAI:Endpoint");
        EnsureConfigured(settings.ChatDeploymentName, "AzureOpenAI:ChatDeploymentName");

        return settings;
    }

    /// <summary>
    /// Get the settings of a remote agent from the <c>RemoteAgents:&lt;agentName&gt;</c> section of the configuration.
    /// </summary>
    /// <param name="agentName">Name of the agent in the configuration, e.g. <c>AuthAgent</c>.</param>
    /// <returns>The remote agent settings.</returns>
    public static RemoteAgentSettings GetRemoteAgentSettings(string agentName)
    {
        string section = $"RemoteAgents:{agentName}";
        RemoteAgentSettings settings = Configuration.GetSection(section).Get<RemoteAgentSettings>()
            ?? throw new InvalidOperationException($"{section} configuration section is missing.");

        EnsureConfigured(settings.Url, $"{section}:Url");
        if (!Uri.TryCreate(settings.Url, UriKind.Absolute, out Uri? url) || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"'{section}:Url' must be an absolute http:// or https:// URL, e.g. http://localhost:5000/a2a/authAgent.");
        }

        settings.Url = settings.Url.TrimEnd('/');
        return settings;
    }

    private static void EnsureConfigured(string value, string key)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains("YOUR-", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"'{key}' is not configured. Set it in appsettings.json, with 'dotnet user-secrets set \"{key}\" <value>', " +
                $"or with the environment variable '{key.Replace(":", "__")}'.");
        }
    }
}
