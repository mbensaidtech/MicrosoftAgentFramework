using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace A2AServer;

/// <summary>
/// TEMPORARY WORKAROUND - remove it when Microsoft.Extensions.AI.OpenAI fixes dotnet/extensions#7790.
/// <para>
/// The A2A hosting always runs the agents in streaming mode. With Azure OpenAI, a streamed Chat Completions response can
/// contain content-filter annotations without a <c>delta</c>, and Microsoft.Extensions.AI.OpenAI 10.10.x then throws
/// <c>InvalidOperationException: The requested operation requires an element of type 'Object', but the target element has type 'Null'</c>:
/// the A2A client receives "Agent handler did not produce any response events".
/// </para>
/// <para>
/// This middleware (built with <see cref="ChatClientBuilder"/>) answers streaming requests with one non-streaming call,
/// returned as updates. Non-streaming calls are unchanged. The agents behave the same; only the answer is no longer streamed.
/// </para>
/// </summary>
public static class StreamingWorkaround
{
    public static IChatClient WithNonStreamingResponses(this IChatClient chatClient) =>
        chatClient.AsBuilder()
            .Use(getResponseFunc: null, getStreamingResponseFunc: GetStreamingResponseFromNonStreamingAsync)
            .Build();

    private static async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseFromNonStreamingAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        IChatClient innerClient,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ChatResponse response = await innerClient.GetResponseAsync(messages, options, cancellationToken);
        foreach (ChatResponseUpdate update in response.ToChatResponseUpdates())
        {
            yield return update;
        }
    }
}
