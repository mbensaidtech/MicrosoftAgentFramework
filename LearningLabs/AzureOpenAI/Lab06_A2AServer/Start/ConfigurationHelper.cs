using Microsoft.Extensions.Configuration;

namespace A2AServer;

/// <summary>
/// Helper class for loading configuration.
/// Sources, from lowest to highest priority: appsettings.json, user secrets, environment variables
/// (e.g. <c>AzureOpenAI__APIKey</c>, <c>APIKeySettings__SecretKey</c>, <c>A2AServer__BaseUrl</c>).
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
    /// Get the settings of the A2A server (address it listens on and advertises in the agent cards).
    /// </summary>
    /// <returns>The A2A server settings.</returns>
    public static A2AServerSettings GetA2AServerSettings()
    {
        A2AServerSettings settings = Configuration.GetSection("A2AServer").Get<A2AServerSettings>()
            ?? throw new InvalidOperationException("A2AServer configuration section is missing.");

        EnsureConfigured(settings.BaseUrl, "A2AServer:BaseUrl");
        if (!Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out Uri? baseUrl) || (baseUrl.Scheme != Uri.UriSchemeHttp && baseUrl.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("'A2AServer:BaseUrl' must be an absolute http:// or https:// URL, e.g. http://localhost:5000.");
        }

        settings.BaseUrl = settings.BaseUrl.TrimEnd('/');
        return settings;
    }

    /// <summary>
    /// Get the settings used to sign the API keys. The secret is optional (see <see cref="APIKeySettings.SecretKey"/>).
    /// </summary>
    /// <returns>The API key settings.</returns>
    public static APIKeySettings GetAPIKeySettings()
    {
        APIKeySettings settings = Configuration.GetSection("APIKeySettings").Get<APIKeySettings>() ?? new APIKeySettings();

        if (settings.SecretKey?.Contains("YOUR-", StringComparison.Ordinal) == true)
        {
            throw new InvalidOperationException(
                "'APIKeySettings:SecretKey' still contains a placeholder. Remove it to use a random secret, or set your own with " +
                "'dotnet user-secrets set \"APIKeySettings:SecretKey\" <value>'.");
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
