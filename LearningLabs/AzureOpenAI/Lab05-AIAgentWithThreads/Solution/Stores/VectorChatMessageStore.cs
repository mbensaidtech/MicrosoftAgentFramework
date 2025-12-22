using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;

namespace AIAgentWithThreads.Stores;
internal sealed class VectorChatMessageStore : ChatMessageStore
{
    private readonly VectorStore _vectorStore;

        public VectorChatMessageStore(VectorStore vectorStore, JsonElement serializedStoreState, JsonSerializerOptions? jsonSerializerOptions = null)
        {
            this._vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));

            if (serializedStoreState.ValueKind is JsonValueKind.String)
            {
                // Here we can deserialize the thread id so that we can access the same messages as before the suspension.
                this.ThreadDbKey = serializedStoreState.Deserialize<string>();
            }
        }

        public string? ThreadDbKey { get; private set; }

        public override async Task AddMessagesAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default)
        {
            this.ThreadDbKey ??= Guid.NewGuid().ToString("N");

            var collection = this._vectorStore.GetCollection<string, ChatHistoryItem>("ChatHistory");
            await collection.EnsureCollectionExistsAsync(cancellationToken);

            await collection.UpsertAsync(messages.Select(x => new ChatHistoryItem()
            {
                Key = this.ThreadDbKey + x.MessageId,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ThreadId = this.ThreadDbKey,
                SerializedMessage = JsonSerializer.Serialize(x),
                MessageText = x.Text
            }), cancellationToken);
        }

        public override async Task<IEnumerable<ChatMessage>> GetMessagesAsync(CancellationToken cancellationToken = default)
        {
            var collection = this._vectorStore.GetCollection<string, ChatHistoryItem>("ChatHistory");
            await collection.EnsureCollectionExistsAsync(cancellationToken);

            var records = await collection
                .GetAsync(
                    x => x.ThreadId == this.ThreadDbKey, 10,
                    new() { OrderBy = x => x.Descending(y => y.Timestamp) },
                    cancellationToken)
                .ToListAsync(cancellationToken);

            var messages = records.ConvertAll(x => JsonSerializer.Deserialize<ChatMessage>(x.SerializedMessage!)!)
;
            messages.Reverse();
            return messages;
        }

        public override JsonElement Serialize(JsonSerializerOptions? jsonSerializerOptions = null) =>
            // We have to serialize the thread id, so that on deserialization we can retrieve the messages using the same thread id.
            JsonSerializer.SerializeToElement(this.ThreadDbKey);
}