namespace AIAgentsBackend.Services;

/// <summary>
/// Validates and signs context IDs for secure conversation tracking.
/// </summary>
public interface IContextIdValidator
{
    /// <summary>
    /// Checks if the signature matches the context ID.
    /// </summary>
    bool ValidateSignature(string contextId, string signature);

    /// <summary>
    /// Creates a signature for a context ID.
    /// </summary>
    string GenerateSignature(string contextId);

    /// <summary>
    /// Gets the username from a context ID like "username|timestamp".
    /// </summary>
    string? ExtractUsername(string contextId);
}
