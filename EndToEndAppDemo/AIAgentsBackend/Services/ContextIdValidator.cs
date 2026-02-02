using System.Security.Cryptography;
using System.Text;
using AIAgentsBackend.Configuration;
using Microsoft.Extensions.Options;

namespace AIAgentsBackend.Services;

/// <summary>
/// Validates context IDs using HMAC-SHA256 signatures.
/// </summary>
public class ContextIdValidator : IContextIdValidator
{
    private readonly byte[] secretKey;
    private readonly ILogger<ContextIdValidator> logger;

    public ContextIdValidator(IOptions<SecuritySettings> securitySettings, ILogger<ContextIdValidator> logger)
    {
        var settings = securitySettings.Value ?? throw new ArgumentNullException(nameof(securitySettings));
        
        if (string.IsNullOrWhiteSpace(settings.ContextIdSigningKey))
        {
            throw new InvalidOperationException("ContextIdSigningKey is not configured in SecuritySettings");
        }

        secretKey = Encoding.UTF8.GetBytes(settings.ContextIdSigningKey);
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Checks if the signature matches the context ID.
    /// </summary>
    public bool ValidateSignature(string contextId, string signature)
    {
        if (string.IsNullOrWhiteSpace(contextId))
        {
            logger.LogWarning("ContextId is null or empty");
            return false;
        }

        if (string.IsNullOrWhiteSpace(signature))
        {
            logger.LogWarning("Signature is null or empty");
            return false;
        }

        try
        {
            var expectedSignature = GenerateSignature(contextId);
            var isValid = string.Equals(signature, expectedSignature, StringComparison.Ordinal);

            if (!isValid)
            {
                logger.LogWarning("Invalid signature for contextId: {ContextId}", contextId);
            }
            else
            {
                logger.LogDebug("Valid signature for contextId: {ContextId}", contextId);
            }

            return isValid;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating signature for contextId: {ContextId}", contextId);
            return false;
        }
    }

    /// <summary>
    /// Creates a signature for a context ID.
    /// </summary>
    public string GenerateSignature(string contextId)
    {
        if (string.IsNullOrWhiteSpace(contextId))
        {
            throw new ArgumentException("ContextId cannot be null or empty", nameof(contextId));
        }

        using var hmac = new HMACSHA256(secretKey);
        var contextIdBytes = Encoding.UTF8.GetBytes(contextId);
        var hashBytes = hmac.ComputeHash(contextIdBytes);
        
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// Gets the username from a context ID like "username|timestamp".
    /// </summary>
    public string? ExtractUsername(string contextId)
    {
        if (string.IsNullOrWhiteSpace(contextId))
        {
            return null;
        }

        var parts = contextId.Split('|');
        if (parts.Length < 1)
        {
            return null;
        }

        var username = parts[0].Trim();
        return string.IsNullOrWhiteSpace(username) ? null : username;
    }
}
