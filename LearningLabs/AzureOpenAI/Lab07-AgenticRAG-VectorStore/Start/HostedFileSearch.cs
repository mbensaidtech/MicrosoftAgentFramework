using OpenAI.VectorStores;

namespace AgenticRAG;

/// <summary>
/// Scenario 6: waits until the vector store of the service has processed its files.
/// The service chunks and embeds the files asynchronously: the file search finds nothing in a file that is not processed yet.
/// </summary>
// VectorStoreClient is still flagged [Experimental] (OPENAI001) by the OpenAI SDK.
#pragma warning disable OPENAI001
public static class HostedFileSearch
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Polls the vector store until its status is no longer <c>in_progress</c>, and returns it.
    /// Fails when a file could not be processed, or after two minutes.
    /// </summary>
    public static async Task<VectorStore> WaitUntilIndexedAsync(VectorStoreClient vectorStoreClient, string vectorStoreId, CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        VectorStore vectorStore = await vectorStoreClient.GetVectorStoreAsync(vectorStoreId, timeout.Token);
        while (vectorStore.Status == VectorStoreStatus.InProgress)
        {
            await Task.Delay(PollInterval, timeout.Token);
            vectorStore = await vectorStoreClient.GetVectorStoreAsync(vectorStoreId, timeout.Token);
        }

        if (vectorStore.FileCounts.Failed > 0 || vectorStore.Status != VectorStoreStatus.Completed)
        {
            throw new InvalidOperationException(
                $"The vector store '{vectorStoreId}' is {vectorStore.Status}: {vectorStore.FileCounts.Completed} files processed, {vectorStore.FileCounts.Failed} failed.");
        }

        return vectorStore;
    }
}
#pragma warning restore OPENAI001
