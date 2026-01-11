using AIAgentWithAIContextProvider.Models;

namespace AIAgentWithAIContextProvider;

public interface IUserMemoriesRepository
{
    Task<List<UserMemory>> GetUserMemoriesAsync(string userId);
    Task AddUserMemoriesAsync(List<UserMemory> userMemories);
    Task DeleteUserMemoriesAsync(string userId);
}
