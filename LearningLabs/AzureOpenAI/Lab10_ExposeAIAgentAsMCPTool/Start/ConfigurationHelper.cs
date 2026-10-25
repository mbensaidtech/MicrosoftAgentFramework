using Microsoft.Extensions.Configuration;

namespace ExposeAIAgentAsMCPTool;

/// <summary>
/// Helper class for loading configuration.
/// Sources, from lowest to highest priority: appsettings.json, user secrets, environment variables
/// (e.g. <c>AzureOpenAI__APIKey</c>, <c>McpServer__BaseUrl</c>).
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
    /// Get the settings of the HTTP MCP server (address it listens on).
    /// </summary>
    /// <returns>The MCP server settings.</returns>
    public static McpServerSettings GetMcpServerSettings()
    {
        McpServerSettings settings = Configuration.GetSection("McpServer").Get<McpServerSettings>()
            ?? throw new InvalidOperationException("McpServer configuration section is missing.");

        EnsureConfigured(settings.BaseUrl, "McpServer:BaseUrl");
        if (!Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out Uri? baseUrl) || (baseUrl.Scheme != Uri.UriSchemeHttp && baseUrl.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("'McpServer:BaseUrl' must be an absolute http:// or https:// URL, e.g. http://localhost:5100.");
        }

        settings.BaseUrl = settings.BaseUrl.TrimEnd('/');
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
