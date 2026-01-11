using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using AIAgentWithAIContextProvider.Models;

namespace AIAgentWithAIContextProvider;

public class UserMemoryAIContextProvider : AIContextProvider
{
    private readonly ChatClientAgent _userDataExtractorAgent;
    private readonly string _userId;
    private readonly IUserMemoriesRepository _userMemoriesRepository;
    private List<UserMemory> _userMemories = [];

    public UserMemoryAIContextProvider(
        ChatClientAgent userDataExtractorAgent, 
        string userId,
        IUserMemoriesRepository userMemoriesRepository)
    {
        _userDataExtractorAgent = userDataExtractorAgent;
        _userId = userId;
        _userMemoriesRepository = userMemoriesRepository;
    }

    public override async ValueTask<AIContext> InvokingAsync(InvokingContext context, CancellationToken cancellationToken = default)
    {
        //Step 1: Get the user memories from the repository
        _userMemories = await _userMemoriesRepository.GetUserMemoriesAsync(_userId);

        //Step 2: Return the user memories as instructions
        return new AIContext { Instructions = string.Join("\n", _userMemories.Select(x => x.Memory)) };
    }

    public override async ValueTask InvokedAsync(InvokedContext context, CancellationToken cancellationToken = default)
    {
        //Step 1: Get the last user message
        ChatMessage? lastUserMessage = context.RequestMessages.LastOrDefault(x => x.Role == ChatRole.User);
        if (lastUserMessage != null)
        {
            //Step 2: Create the messages for the user data extractor agent
            List<ChatMessage> messages = [
                new(ChatRole.System, $"this is what we know about the user: {string.Join("\n", _userMemories.Select(x => x.Memory))}. Don't extract the same info again. The userId is: {_userId}"),
                lastUserMessage,
            ];

            //Step 3: Run the user data extractor agent
            AgentRunResponse<List<UserMemory>> response = await _userDataExtractorAgent.RunAsync<List<UserMemory>>(messages);

            //Step 4: Add the new user memories to database
            await _userMemoriesRepository.AddUserMemoriesAsync(response.Result);
        }
    }
}
