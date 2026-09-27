using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AIAgentWithThreads;

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
    /// Connect to the MongoDB database of the configuration and check that the server answers.
    /// </summary>
    /// <returns>The MongoDB database that stores the chat history.</returns>
    public static async Task<IMongoDatabase> ConnectToMongoDbAsync(CancellationToken cancellationToken = default)
    {
        MongoDbSettings settings = Configuration.GetSection("MongoDb").Get<MongoDbSettings>()
            ?? throw new InvalidOperationException("MongoDb configuration section is missing.");

        EnsureConfigured(settings.ConnectionString, "MongoDb:ConnectionString");
        EnsureConfigured(settings.DatabaseName, "MongoDb:DatabaseName");

        MongoClientSettings clientSettings = MongoClientSettings.FromConnectionString(settings.ConnectionString);
        // Fail fast (instead of the 30 seconds default) when MongoDB is not started.
        clientSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);

        IMongoDatabase database = new MongoClient(clientSettings).GetDatabase(settings.DatabaseName);
        try
        {
            await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
        }
        catch (TimeoutException exception)
        {
            throw new InvalidOperationException(
                $"MongoDB is not reachable with the connection string of 'MongoDb:ConnectionString'. " +
                "Start it with 'docker compose up -d' in the MongoDB folder of the lab.", exception);
        }

        return database;
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
