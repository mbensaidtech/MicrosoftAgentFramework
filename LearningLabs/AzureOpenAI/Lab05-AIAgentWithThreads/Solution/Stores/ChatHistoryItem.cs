using Microsoft.Extensions.VectorData;

namespace AIAgentWithThreads.Stores;

/// <summary>
/// Represents a chat history item for the vector store.
/// </summary>
internal sealed class ChatHistoryItem
{
    [VectorStoreKey]
    public string? Key { get; set; }
    [VectorStoreData]
    public string? ThreadId { get; set; }
    [VectorStoreData]
    public long Timestamp { get; set; }
    [VectorStoreData]
    public string? SerializedMessage { get; set; }
    [VectorStoreData]
    public string? MessageText { get; set; }
}

