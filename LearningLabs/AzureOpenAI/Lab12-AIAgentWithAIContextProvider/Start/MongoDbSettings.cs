namespace AIAgentWithAIContextProvider;

public class MongoDbSettings
{
    /// <summary>MongoDB connection string. The default value targets the container of MongoDB/docker-compose.yml.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Name of the database that stores the user memories.</summary>
    public string DatabaseName { get; set; } = string.Empty;
}
