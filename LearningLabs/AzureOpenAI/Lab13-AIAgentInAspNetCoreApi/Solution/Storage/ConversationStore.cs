using System.Collections.Concurrent;
using System.Text.Json;

namespace AIAgentWebApi.Storage;

/// <summary>
/// Where the API keeps the conversations between two HTTP requests: the serialized sessions of the agent
/// (<c>SerializeSessionAsync</c>), under a key chosen by the endpoint. In memory for the lab: the conversations are lost
/// when the program stops. In production, use a database (Cosmos DB, SQL, Redis...) with the same key.
/// </summary>
public sealed class ConversationStore
{
    private readonly ConcurrentDictionary<string, JsonElement> _sessions = new(StringComparer.Ordinal);

    public bool TryGet(string key, out JsonElement serializedSession) => _sessions.TryGetValue(key, out serializedSession);

    public void Save(string key, JsonElement serializedSession) => _sessions[key] = serializedSession;
}
