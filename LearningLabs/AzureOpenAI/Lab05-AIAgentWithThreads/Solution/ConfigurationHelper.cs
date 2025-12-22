using AIAgentWithThreads.Configuration;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace AIAgentWithThreads;

public static class ConfigurationHelper
{
    private static IConfiguration? _configuration;
    private static IMongoDatabase? _mongoDatabase;

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

    public static MongoDbConfiguration GetMongoDbConfiguration()
    {
        return Configuration.GetSection("MongoDb").Get<MongoDbConfiguration>()
            ?? throw new InvalidOperationException("MongoDb configuration section is missing");
    }

    public static IMongoDatabase GetMongoDatabase()
    {
        if (_mongoDatabase is not null)
            return _mongoDatabase;

        var config = GetMongoDbConfiguration();
        var client = new MongoClient(config.ConnectionString);
        _mongoDatabase = client.GetDatabase(config.DatabaseName);
        
        return _mongoDatabase;
    }

    public static AgentSettings GetAgent(string agentName)
    {
        return Configuration.GetSection($"AzureOpenAI:Agents:{agentName}").Get<AgentSettings>()
            ?? throw new InvalidOperationException($"Agent '{agentName}' configuration is missing");
    }
}


