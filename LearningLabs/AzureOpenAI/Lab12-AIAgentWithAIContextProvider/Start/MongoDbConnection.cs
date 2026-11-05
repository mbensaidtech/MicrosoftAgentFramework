using System.Net;
using System.Runtime.InteropServices;
using CommonUtilities;
using EphemeralMongo;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AIAgentWithAIContextProvider;

/// <summary>
/// Connects to the MongoDB database of the configuration:
/// <list type="bullet">
/// <item><description><see cref="StorageMode.MongoDb"/>: a server reached with <c>MongoDb:ConnectionString</c> (Docker container, local installation, Atlas).</description></item>
/// <item><description><see cref="StorageMode.EmbeddedMongoDb"/>: a server started by the lab with EphemeralMongo, without Docker and without installation.
/// <c>mongod</c> is downloaded once from mongodb.org into the local application data folder of the user, then runs as a child process
/// of the lab, on 127.0.0.1 only. Its data folder is kept: the data survive the application, like with the container.</description></item>
/// </list>
/// </summary>
internal static class MongoDbConnection
{
    private static readonly SemaphoreSlim EmbeddedServerLock = new(1, 1);
    private static IMongoRunner? _embeddedServer;
    private static FileStream? _embeddedServerOwnership;
    private static readonly List<PosixSignalRegistration> SignalRegistrations = [];

    // File of the data folder where the address of the running server is saved.
    private const string ServerAddressFile = "lab-server-address.txt";

    public static async Task<IMongoDatabase> ConnectAsync(StorageSettings storage, MongoDbSettings settings, CancellationToken cancellationToken = default)
    {
        bool embedded = storage.Mode == StorageMode.EmbeddedMongoDb;
        string connectionString = embedded
            ? await StartEmbeddedServerAsync(Path.Combine(storage.GetDataDirectory(settings.DatabaseName), "mongodb"), settings.BinaryDirectory, cancellationToken)
            : settings.ConnectionString;

        MongoClientSettings clientSettings = MongoClientSettings.FromConnectionString(connectionString);
        // Fail fast (instead of the 30 seconds default) when MongoDB is not started.
        clientSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);

        IMongoDatabase database = new MongoClient(clientSettings).GetDatabase(settings.DatabaseName);
        try
        {
            await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
        }
        catch (TimeoutException exception) when (!embedded)
        {
            throw new InvalidOperationException(
                "MongoDB is not reachable with the connection string of 'MongoDb:ConnectionString'. " +
                "Start it with 'docker compose up -d' in the MongoDB folder of the lab. " +
                "Without Docker, set 'Storage:Mode' to 'EmbeddedMongoDb' (or 'JsonFile'): see the README.", exception);
        }

        return database;
    }

    /// <summary>
    /// Starts the embedded server at the first call, and returns its connection string. The server is shared by all the connections
    /// of the application and stopped with it.
    /// </summary>
    private static async Task<string> StartEmbeddedServerAsync(string dataDirectory, string binaryDirectory, CancellationToken cancellationToken)
    {
        await EmbeddedServerLock.WaitAsync(cancellationToken);
        try
        {
            if (_embeddedServer is not null)
            {
                return _embeddedServer.ConnectionString;
            }

            Directory.CreateDirectory(dataDirectory);
            _embeddedServerOwnership = TakeOwnership(dataDirectory);
            await StopLeftoverServerAsync(dataDirectory, cancellationToken);

            MongoRunnerOptions options = new()
            {
                DataDirectory = dataDirectory,
                BinaryDirectory = string.IsNullOrWhiteSpace(binaryDirectory) ? null : binaryDirectory,
                AdditionalArguments = ["--quiet"],
                // Behind an authenticating corporate proxy: send the credentials of the signed-in user to the proxy (download of MongoDB).
                Transport = new HttpTransport(new HttpClient(new HttpClientHandler { DefaultProxyCredentials = CredentialCache.DefaultCredentials }))
            };

            try
            {
                _embeddedServer = await MongoRunner.RunAsync(options, cancellationToken)
                    .WithSpinner("Starting the embedded MongoDB (the first run downloads it from mongodb.org, about 1 minute)");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new InvalidOperationException(
                    "The embedded MongoDB could not start. At the first run, it downloads MongoDB from mongodb.org: check that the site is " +
                    "reachable (proxy, VPN), or set 'MongoDb:BinaryDirectory' to a folder that contains mongod. " +
                    "Otherwise, set 'Storage:Mode' to 'JsonFile': see the README.", exception);
            }

            // Save the address of the server, to stop it at the next run if this run is killed (see StopLeftoverServerAsync).
            await File.WriteAllTextAsync(Path.Combine(dataDirectory, ServerAddressFile), _embeddedServer.ConnectionString, cancellationToken);

            // Stop the server with the application: at the end, and on Ctrl+C or a stop request (signals that end the application
            // without the ProcessExit event). On Windows, EphemeralMongo also stops it when the application is killed.
            AppDomain.CurrentDomain.ProcessExit += (_, _) => _embeddedServer?.Dispose();
            foreach (PosixSignal signal in new[] { PosixSignal.SIGINT, PosixSignal.SIGTERM, PosixSignal.SIGQUIT })
            {
                SignalRegistrations.Add(PosixSignalRegistration.Create(signal, _ => _embeddedServer?.Dispose()));
            }

            ColoredConsole.WriteSecondaryLogLine($"Embedded MongoDB started: {_embeddedServer.ConnectionString} (data in {dataDirectory})");
            return _embeddedServer.ConnectionString;
        }
        finally
        {
            EmbeddedServerLock.Release();
        }
    }

    /// <summary>
    /// Locks a file of the data folder for the whole life of the application (the system releases it when the application ends,
    /// even when it is killed), so that two running labs never use the same data folder.
    /// </summary>
    private static FileStream TakeOwnership(string dataDirectory)
    {
        try
        {
            return new FileStream(Path.Combine(dataDirectory, "lab.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException exception)
        {
            throw new InvalidOperationException(
                $"The embedded MongoDB of '{dataDirectory}' is already used by another running lab: stop it first.", exception);
        }
    }

    /// <summary>
    /// On macOS and Linux, the mongod of a run that was killed (not stopped) keeps running and holds the data folder:
    /// it locks mongod.lock until it stops. No running lab owns that server (see <see cref="TakeOwnership"/>): ask it to stop,
    /// at the address saved by the run that started it.
    /// </summary>
    private static async Task StopLeftoverServerAsync(string dataDirectory, CancellationToken cancellationToken)
    {
        string addressFile = Path.Combine(dataDirectory, ServerAddressFile);
        if (!IsLockedByServer(dataDirectory) || !File.Exists(addressFile))
        {
            return;
        }

        MongoClientSettings clientSettings = MongoClientSettings.FromConnectionString(await File.ReadAllTextAsync(addressFile, cancellationToken));
        clientSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(2);
        try
        {
            await new MongoClient(clientSettings).GetDatabase("admin")
                .RunCommandAsync<BsonDocument>(new BsonDocument { { "shutdown", 1 }, { "force", true } }, cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (exception is MongoException or TimeoutException or IOException)
        {
            // Expected: the server closes the connection when it stops (or it was not reachable any more).
        }

        for (int attempt = 0; attempt < 50 && IsLockedByServer(dataDirectory); attempt++)
        {
            await Task.Delay(200, cancellationToken);
        }
    }

    /// <summary>Whether a running mongod holds the data folder (it locks mongod.lock: the file cannot be read).</summary>
    private static bool IsLockedByServer(string dataDirectory)
    {
        string lockFile = Path.Combine(dataDirectory, "mongod.lock");
        if (!File.Exists(lockFile))
        {
            return false;
        }

        try
        {
            using FileStream _ = File.OpenRead(lockFile);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }
}
