using MongoDB.Driver;
using AIAgentWithAIContextProvider.Models;

namespace AIAgentWithAIContextProvider;

public class UserMemoriesRepository : IUserMemoriesRepository
{
    private readonly IMongoCollection<UserMemoryDocument> _collection;

    public UserMemoriesRepository(IMongoDatabase mongoDatabase, string collectionName)
    {
        _collection = mongoDatabase.GetCollection<UserMemoryDocument>(collectionName);
    }

    public async Task<List<UserMemory>> GetUserMemoriesAsync(string userId)
    {
        var filter = Builders<UserMemoryDocument>.Filter.Eq(x => x.UserId, userId);
        var documents = await _collection.Find(filter).ToListAsync();
        
        return documents.Select(d => new UserMemory(d.UserId, d.Memory)).ToList();
    }

    public async Task AddUserMemoriesAsync(List<UserMemory> userMemories)
    {
        if (userMemories.Count == 0)
            return;

        var documents = userMemories.Select(m => new UserMemoryDocument
        {
            UserId = m.UserId,
            Memory = m.Memory
        }).ToList();
        
        await _collection.InsertManyAsync(documents);
    }

    public async Task DeleteUserMemoriesAsync(string userId)
    {
        var filter = Builders<UserMemoryDocument>.Filter.Eq(x => x.UserId, userId);
        await _collection.DeleteManyAsync(filter);
    }
}
