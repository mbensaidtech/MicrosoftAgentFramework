using MongoDB.Bson;
using MongoDB.Driver;

namespace CommonUtilities;

/// <summary>
/// Utility class to verify MongoDB connectivity and health.
/// </summary>
public static class MongoDbHealthCheck
{
    /// <summary>
    /// Performs a full health check and displays results to console.
    /// Exits the application if MongoDB is not accessible.
    /// </summary>
    /// <param name="connectionString">MongoDB connection string</param>
    /// <param name="databaseName">Database name to check</param>
    /// <param name="timeoutSeconds">Connection timeout in seconds (default: 5)</param>
    public static async Task EnsureMongoDbIsRunningAsync(string connectionString, string databaseName, int timeoutSeconds = 5)
    {
        ColoredConsole.WriteInfoLine("Checking MongoDB connectivity...");

        try
        {
            var settings = MongoClientSettings.FromConnectionString(connectionString);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(timeoutSeconds);
            settings.ConnectTimeout = TimeSpan.FromSeconds(timeoutSeconds);
            
            var client = new MongoClient(settings);
            
            // Ping the server to check connectivity
            var adminDb = client.GetDatabase("admin");
            await adminDb.RunCommandAsync((Command<BsonDocument>)"{ping:1}");
            ColoredConsole.WriteSuccessLine("MongoDB server is running.");

            // Check if database is accessible
            var database = client.GetDatabase(databaseName);
            await database.ListCollectionNamesAsync();
            ColoredConsole.WriteSuccessLine($"Database '{databaseName}' is accessible.");
            ColoredConsole.WriteEmptyLine();
        }
        catch (Exception)
        {
            ColoredConsole.WriteErrorLine("MongoDB is not running or not accessible!");
            ColoredConsole.WriteWarningLine("Please start MongoDB using: docker-compose up -d");
            ColoredConsole.WriteSecondaryLogLine("Navigate to the MongoDB folder and run the command above.");
            Environment.Exit(1);
        }
    }
}
