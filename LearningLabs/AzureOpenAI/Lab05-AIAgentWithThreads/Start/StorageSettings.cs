namespace AIAgentWithThreads;

/// <summary>
/// Where the lab stores its data (configuration section <c>Storage</c>).
/// </summary>
public class StorageSettings
{
    /// <summary>The storage to use. Default: <see cref="StorageMode.MongoDb"/> (the container of MongoDB/docker-compose.yml).</summary>
    public StorageMode Mode { get; set; } = StorageMode.MongoDb;

    /// <summary>
    /// Folder of the data of the <see cref="StorageMode.EmbeddedMongoDb"/> and <see cref="StorageMode.JsonFile"/> modes.
    /// Empty: a <c>MicrosoftAgentFrameworkLabs</c> folder in the local application data folder of the user.
    /// </summary>
    public string DataDirectory { get; set; } = string.Empty;

    /// <summary>The folder of the data of a database, created if needed.</summary>
    public string GetDataDirectory(string databaseName)
    {
        string root = string.IsNullOrWhiteSpace(DataDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MicrosoftAgentFrameworkLabs")
            : DataDirectory;
        return Directory.CreateDirectory(Path.Combine(root, databaseName)).FullName;
    }
}

/// <summary>
/// The storages the lab can use. None of them needs administrator rights.
/// </summary>
public enum StorageMode
{
    /// <summary>A MongoDB server reached with <c>MongoDb:ConnectionString</c>: the Docker container of the lab, a local installation or MongoDB Atlas.</summary>
    MongoDb,

    /// <summary>A MongoDB server started by the lab itself (EphemeralMongo): no Docker, no installation. The first run downloads MongoDB.</summary>
    EmbeddedMongoDb,

    /// <summary>A JSON file on the disk: no MongoDB at all. The fallback when MongoDB cannot run on the computer.</summary>
    JsonFile
}
