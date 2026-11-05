using Microsoft.Extensions.Configuration;
using CommonUtilities;

namespace AIAgentWithAIContextProvider;

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
    /// Opens the repository of the user memories chosen by <c>Storage:Mode</c>: MongoDB (Docker container, local installation or Atlas),
    /// a MongoDB started by the lab (no Docker), or a JSON file (no MongoDB).
    /// </summary>
    /// <returns>The repository of the user memories.</returns>
    public static async Task<IUserMemoriesRepository> CreateUserMemoriesRepositoryAsync(CancellationToken cancellationToken = default)
    {
        StorageSettings storage = Configuration.GetSection("Storage").Get<StorageSettings>() ?? new StorageSettings();
        MongoDbSettings settings = Configuration.GetSection("MongoDb").Get<MongoDbSettings>()
            ?? throw new InvalidOperationException("MongoDb configuration section is missing.");

        EnsureConfigured(settings.DatabaseName, "MongoDb:DatabaseName");

        IUserMemoriesRepository repository;
        switch (storage.Mode)
        {
            case StorageMode.JsonFile:
                repository = new JsonFileUserMemoriesRepository(Path.Combine(storage.GetDataDirectory(settings.DatabaseName), "user_memories.json"));
                break;

            case StorageMode.EmbeddedMongoDb:
                repository = new MongoUserMemoriesRepository(
                    await MongoDbConnection.ConnectAsync(storage, settings, cancellationToken),
                    $"embedded MongoDB, database '{settings.DatabaseName}'");
                break;

            default:
                EnsureConfigured(settings.ConnectionString, "MongoDb:ConnectionString");
                repository = new MongoUserMemoriesRepository(
                    await MongoDbConnection.ConnectAsync(storage, settings, cancellationToken),
                    $"MongoDB, database '{settings.DatabaseName}'");
                break;
        }

        ColoredConsole.WriteSecondaryLogLine($"Memories storage: {repository.Description} (Storage:Mode = {storage.Mode})");
        return repository;
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
