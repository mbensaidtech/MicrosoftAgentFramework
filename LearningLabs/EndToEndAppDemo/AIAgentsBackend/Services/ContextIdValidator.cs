using System.Security.Cryptography;
using System.Text;
using AIAgentsBackend.Configuration;
using Microsoft.Extensions.Options;

namespace AIAgentsBackend.Services;

/// <summary>
/// Service for validating signed context IDs using HMAC-SHA256.
/// </summary>
public class ContextIdValidator : IContextIdValidator
{
    private readonly byte[] _secretKey;
    private readonly ILogger<ContextIdValidator> _logger;

    public ContextIdValidator(IOptions<SecuritySettings> securitySettings, ILogger<ContextIdValidator> logger)
    {
        var settings = securitySettings.Value ?? throw new ArgumentNullException(nameof(securitySettings));
        
        if (string.IsNullOrWhiteSpace(settings.ContextIdSigningKey))
        {
            throw new InvalidOperationException("ContextIdSigningKey is not configured in SecuritySettings");
        }

        _secretKey = Encoding.UTF8.GetBytes(settings.ContextIdSigningKey);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Validates a signed context ID using HMAC-SHA256.
    /// </summary>
    public bool ValidateSignature(string contextId, string signature)
    {
        if (string.IsNullOrWhiteSpace(contextId))
        {
            _logger.LogWarning("ContextId is null or empty");
            return false;
        }

        if (string.IsNullOrWhiteSpace(signature))
        {
            _logger.LogWarning("Signature is null or empty");
            return false;
        }

        try
        {
            var expectedSignature = GenerateSignature(contextId);
            var isValid = string.Equals(signature, expectedSignature, StringComparison.Ordinal);

            if (!isValid)
            {
                _logger.LogWarning("Invalid signature for contextId: {ContextId}", contextId);
            }
            else
            {
                _logger.LogDebug("Valid signature for contextId: {ContextId}", contextId);
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating signature for contextId: {ContextId}", contextId);
            return false;
        }
    }

    /// <summary>
    /// Generates an HMAC-SHA256 signature for a context ID.
    /// </summary>
    public string GenerateSignature(string contextId)
    {
        if (string.IsNullOrWhiteSpace(contextId))
        {
            throw new ArgumentException("ContextId cannot be null or empty", nameof(contextId));
        }

        using var hmac = new HMACSHA256(_secretKey);
        var contextIdBytes = Encoding.UTF8.GetBytes(contextId);
        var hashBytes = hmac.ComputeHash(contextIdBytes);
        
        // Convert to Base64 for easy transmission
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// Extracts the username from a context ID in format "username|timestamp".
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

