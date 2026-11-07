using Microsoft.Extensions.Configuration;
using CommonUtilities;
using AIAgentWithSessions.Stores;

namespace AIAgentWithSessions;

/// <summary>
/// Helper class for loading configuration.
/// Sources, from lowest to highest priority: appsettings.json, user secrets, environment variables
/// (e.g. <c>AzureOpenAI__APIKey</c>).
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
    /// Opens the storage of the chat history chosen by <c>Storage:Mode</c>: MongoDB (Docker container, local installation or Atlas),
    /// a MongoDB started by the lab (no Docker), or a JSON file (no MongoDB). Each call opens a new connection.
    /// </summary>
    /// <returns>The store of the chat history.</returns>
    public static async Task<IChatHistoryStore> CreateChatHistoryStoreAsync(CancellationToken cancellationToken = default)
    {
        StorageSettings storage = Configuration.GetSection("Storage").Get<StorageSettings>() ?? new StorageSettings();
        MongoDbSettings settings = Configuration.GetSection("MongoDb").Get<MongoDbSettings>()
            ?? throw new InvalidOperationException("MongoDb configuration section is missing.");

        EnsureConfigured(settings.DatabaseName, "MongoDb:DatabaseName");

        IChatHistoryStore store;
        switch (storage.Mode)
        {
            case StorageMode.JsonFile:
                store = new JsonFileChatHistoryStore(Path.Combine(storage.GetDataDirectory(settings.DatabaseName), "chat_history.json"));
                break;

            case StorageMode.EmbeddedMongoDb:
                store = new MongoChatHistoryStore(
                    await MongoDbConnection.ConnectAsync(storage, settings, cancellationToken),
                    $"embedded MongoDB, database '{settings.DatabaseName}'");
                break;

            default:
                EnsureConfigured(settings.ConnectionString, "MongoDb:ConnectionString");
                store = new MongoChatHistoryStore(
                    await MongoDbConnection.ConnectAsync(storage, settings, cancellationToken),
                    $"MongoDB, database '{settings.DatabaseName}'");
                break;
        }

        ColoredConsole.WriteSecondaryLogLine($"Chat history storage: {store.Description} (Storage:Mode = {storage.Mode})");
        return store;
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
