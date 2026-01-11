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
        // TODO: Call the repository to get user memories for _userId

        //Step 2: Return the user memories as instructions
        // TODO: Return an AIContext with Instructions containing the joined memories

        throw new NotImplementedException("Complete the implementation of InvokingAsync");
    }

    public override async ValueTask InvokedAsync(InvokedContext context, CancellationToken cancellationToken = default)
    {
        //Step 1: Get the last user message
        // TODO: Get the last message with Role == ChatRole.User from context.RequestMessages

        //Step 2: Create the messages for the user data extractor agent
        // TODO: Create a List<ChatMessage> with:
        //       - A system message containing existing memories and the userId
        //       - The last user message

        //Step 3: Run the user data extractor agent
        // TODO: Run the extractor agent with structured output to get List<UserMemory>

        //Step 4: Add the new user memories to database
        // TODO: Save the extracted memories using the repository
    }
}
