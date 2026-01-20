namespace AIAgentsBackend.Services;

/// <summary>
/// Service for validating signed context IDs.
/// </summary>
public interface IContextIdValidator
{
    /// <summary>
    /// Validates a signed context ID.
    /// </summary>
    /// <param name="contextId">The context ID (e.g., "username|timestamp").</param>
    /// <param name="signature">The HMAC signature of the context ID.</param>
    /// <returns>True if the signature is valid, false otherwise.</returns>
    bool ValidateSignature(string contextId, string signature);

    /// <summary>
    /// Generates a signature for a context ID (for testing purposes).
    /// </summary>
    /// <param name="contextId">The context ID to sign.</param>
    /// <returns>The HMAC signature.</returns>
    string GenerateSignature(string contextId);

    /// <summary>
    /// Extracts the username from a signed context ID.
    /// </summary>
    /// <param name="contextId">The context ID (e.g., "username|timestamp").</param>
    /// <returns>The username or null if invalid format.</returns>
    string? ExtractUsername(string contextId);
}

