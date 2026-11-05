using System.Text.Json;
using Microsoft.Extensions.AI;

namespace AIAgentWithThreads.Stores;

/// <summary>
/// Stores the chat history in a JSON file (<c>chat_history.json</c>): the fallback when MongoDB cannot run on the computer.
/// The file holds the messages of all the sessions, in the order they were stored. It is read and written again at each call:
/// fine for a lab, not for a real application (no index, no concurrent writers).
/// </summary>
public sealed class JsonFileChatHistoryStore(string filePath) : IChatHistoryStore
{
    private static readonly JsonSerializerOptions FileOptions = new() { WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
    private static readonly SemaphoreSlim FileLock = new(1, 1);

    public string Description => $"JSON file {filePath}";

    public async Task<List<ChatMessage>> GetLastMessagesAsync(string sessionKey, int maxMessages, CancellationToken cancellationToken = default)
    {
        await FileLock.WaitAsync(cancellationToken);
        try
        {
            List<StoredMessage> storedMessages = await ReadAsync(cancellationToken);
            return storedMessages
                .Where(stored => stored.SessionId == sessionKey)
                .TakeLast(maxMessages)
                .Select(stored => stored.Message)
                .ToList();
        }
        finally
        {
            FileLock.Release();
        }
    }

    public async Task AddMessagesAsync(string sessionKey, IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        await FileLock.WaitAsync(cancellationToken);
        try
        {
            List<StoredMessage> storedMessages = await ReadAsync(cancellationToken);
            DateTime now = DateTime.UtcNow;
            storedMessages.AddRange(messages.Select(message => new StoredMessage(sessionKey, now, message)));

            // Write a temporary file, then replace the file: a crash during the write never leaves a half-written file.
            string temporaryFile = filePath + ".tmp";
            await File.WriteAllTextAsync(temporaryFile, JsonSerializer.Serialize(storedMessages, FileOptions), cancellationToken);
            File.Move(temporaryFile, filePath, overwrite: true);
        }
        finally
        {
            FileLock.Release();
        }
    }

    private async Task<List<StoredMessage>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        await using FileStream stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<List<StoredMessage>>(stream, FileOptions, cancellationToken) ?? [];
    }

    /// <summary>A message of the chat history, as stored in the file.</summary>
    private sealed record StoredMessage(string SessionId, DateTime Timestamp, ChatMessage Message);
}
