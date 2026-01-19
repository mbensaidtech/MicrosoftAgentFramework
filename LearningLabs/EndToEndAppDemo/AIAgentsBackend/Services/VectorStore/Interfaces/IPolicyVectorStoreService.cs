using Microsoft.Extensions.VectorData;
using AIAgentsBackend.Models.VectorStore;

namespace AIAgentsBackend.Services.VectorStore.Interfaces;

/// <summary>
/// Interface for policy vector store services.
/// Defines the contract for searching policy documents.
/// </summary>
public interface IPolicyVectorStoreService
{
    /// <summary>
    /// Initializes the vector store with policy data.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for policy sections similar to the given query.
    /// </summary>
    Task<IReadOnlyList<VectorSearchResult<PolicySectionRecord>>> SearchAsync(
        string query,
        int topK = 3,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches and returns formatted string results.
    /// </summary>
    Task<List<string>> SearchFormattedAsync(
        string query,
        int topK = 3,
        CancellationToken cancellationToken = default);
}
