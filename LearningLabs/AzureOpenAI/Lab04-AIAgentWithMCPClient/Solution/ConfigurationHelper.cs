using Microsoft.Extensions.Configuration;

namespace AIAgentWithMCPClient;

/// <summary>
/// Helper class for loading configuration.
/// Sources, from lowest to highest priority: appsettings.json, user secrets, environment variables
/// (e.g. <c>AzureOpenAI__APIKey</c>, <c>MCPServers__HuggingFace__BearerToken</c>).
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
    /// Get the settings of an MCP server from the <c>MCPServers:&lt;serverName&gt;</c> section of the configuration.
    /// </summary>
    /// <param name="serverName">Name of the server in the configuration, e.g. <c>HuggingFace</c>.</param>
    /// <returns>The MCP server settings.</returns>
    public static MCPServerSettings GetMCPServerSettings(string serverName)
    {
        string section = $"MCPServers:{serverName}";
        MCPServerSettings settings = Configuration.GetSection(section).Get<MCPServerSettings>()
            ?? throw new InvalidOperationException($"{section} configuration section is missing.");

        EnsureConfigured(settings.Endpoint, $"{section}:Endpoint");
        if (!Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out Uri? endpoint) || endpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException($"'{section}:Endpoint' must be an absolute https:// URL.");
        }

        if (settings.BearerToken?.Contains("YOUR-", StringComparison.Ordinal) == true)
        {
            throw new InvalidOperationException(
                $"'{section}:BearerToken' still contains a placeholder. Remove it to connect anonymously, or set your token with " +
                $"'dotnet user-secrets set \"{section}:BearerToken\" <token>'.");
        }

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
