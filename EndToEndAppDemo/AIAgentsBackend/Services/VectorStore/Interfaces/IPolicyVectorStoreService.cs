using Microsoft.Extensions.VectorData;
using AIAgentsBackend.Models.VectorStore;

namespace AIAgentsBackend.Services.VectorStore.Interfaces;

/// <summary>
/// Searches policy documents using vector embeddings.
/// </summary>
public interface IPolicyVectorStoreService
{
    /// <summary>
    /// Loads policy data and creates embeddings in the vector store.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds policy sections that match the query.
    /// </summary>
    Task<IReadOnlyList<VectorSearchResult<PolicySectionRecord>>> SearchAsync(
        string query,
        int topK = 3,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds matching policies and returns them as readable text.
    /// </summary>
    Task<List<string>> SearchFormattedAsync(
        string query,
        int topK = 3,
        CancellationToken cancellationToken = default);
}
