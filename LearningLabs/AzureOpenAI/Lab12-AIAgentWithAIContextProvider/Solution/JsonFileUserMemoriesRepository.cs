using System.Text.Json;

namespace AIAgentWithAIContextProvider;

/// <summary>
/// Stores the long-term memories of the users in a JSON file (<c>user_memories.json</c>): the fallback when MongoDB cannot run
/// on the computer. The file holds the memories of all the users, in the order they were stored. It is read and written again
/// at each call: fine for a lab, not for a real application (no index, no concurrent writers).
/// </summary>
public sealed class JsonFileUserMemoriesRepository(string filePath) : IUserMemoriesRepository
{
    private static readonly JsonSerializerOptions FileOptions = new() { WriteIndented = true };
    private static readonly SemaphoreSlim FileLock = new(1, 1);

    public string Description => $"JSON file {filePath}";

    public async Task<List<string>> GetMemoriesAsync(string userId, CancellationToken cancellationToken = default)
    {
        await FileLock.WaitAsync(cancellationToken);
        try
        {
            List<UserMemory> memories = await ReadAsync(cancellationToken);
            return memories.Where(memory => memory.UserId == userId).Select(memory => memory.Memory).ToList();
        }
        finally
        {
            FileLock.Release();
        }
    }

    public async Task AddMemoriesAsync(string userId, IEnumerable<string> memories, CancellationToken cancellationToken = default)
    {
        DateTime now = DateTime.UtcNow;
        List<UserMemory> newMemories = memories.Select(memory => new UserMemory(userId, memory, now)).ToList();
        if (newMemories.Count == 0)
        {
            return;
        }

        await FileLock.WaitAsync(cancellationToken);
        try
        {
            List<UserMemory> storedMemories = await ReadAsync(cancellationToken);
            storedMemories.AddRange(newMemories);
            await WriteAsync(storedMemories, cancellationToken);
        }
        finally
        {
            FileLock.Release();
        }
    }

    public async Task<long> DeleteMemoriesAsync(string userId, CancellationToken cancellationToken = default)
    {
        await FileLock.WaitAsync(cancellationToken);
        try
        {
            List<UserMemory> storedMemories = await ReadAsync(cancellationToken);
            int deleted = storedMemories.RemoveAll(memory => memory.UserId == userId);
            if (deleted > 0)
            {
                await WriteAsync(storedMemories, cancellationToken);
            }

            return deleted;
        }
        finally
        {
            FileLock.Release();
        }
    }

    private async Task<List<UserMemory>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        await using FileStream stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<List<UserMemory>>(stream, FileOptions, cancellationToken) ?? [];
    }

    private async Task WriteAsync(List<UserMemory> memories, CancellationToken cancellationToken)
    {
        // Write a temporary file, then replace the file: a crash during the write never leaves a half-written file.
        string temporaryFile = filePath + ".tmp";
        await File.WriteAllTextAsync(temporaryFile, JsonSerializer.Serialize(memories, FileOptions), cancellationToken);
        File.Move(temporaryFile, filePath, overwrite: true);
    }

    /// <summary>A memory, as stored in the file.</summary>
    private sealed record UserMemory(string UserId, string Memory, DateTime CreatedAt);
}
