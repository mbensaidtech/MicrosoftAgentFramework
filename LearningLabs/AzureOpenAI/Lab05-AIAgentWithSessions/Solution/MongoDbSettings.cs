namespace AIAgentWithSessions;

public class MongoDbSettings
{
    /// <summary>
    /// MongoDB connection string, used by the <see cref="StorageMode.MongoDb"/> mode.
    /// The default value targets the container of MongoDB/docker-compose.yml.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Name of the database that stores the chat history (also the name of its data folder in the other modes).</summary>
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>
    /// <see cref="StorageMode.EmbeddedMongoDb"/> mode only, optional: a folder that already contains the MongoDB binaries (<c>mongod</c>),
    /// when they cannot be downloaded from mongodb.org. Empty: the binaries are downloaded at the first run.
    /// </summary>
    public string BinaryDirectory { get; set; } = string.Empty;
}
