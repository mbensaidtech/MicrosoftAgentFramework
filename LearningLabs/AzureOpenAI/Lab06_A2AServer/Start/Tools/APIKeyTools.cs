using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;

namespace A2AServer.Tools;

/// <summary>
/// Tools class for API key generation and validation.
/// Provides methods to generate API keys starting with "Meknes" and validate them using cryptographic signatures.
/// </summary>
public class APIKeyTools
{
    private const string KeyPrefix = "Meknes";
    private const int RandomPartLength = 32;
    private const int SignatureLength = 32; // Base64 encoded HMAC-SHA256 signature
    private readonly string _secretKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="APIKeyTools"/> class.
    /// </summary>
    /// <param name="apiKeySettings">The settings holding the optional signing secret.</param>
    public APIKeyTools(APIKeySettings apiKeySettings)
    {
        ArgumentNullException.ThrowIfNull(apiKeySettings);

        IsSecretConfigured = !string.IsNullOrWhiteSpace(apiKeySettings.SecretKey);

        // Without a configured secret, sign with a random one: the keys stay valid until the server stops.
        _secretKey = IsSecretConfigured
            ? apiKeySettings.SecretKey!
            : Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    }

    /// <summary>
    /// Gets a value indicating whether the signing secret comes from the configuration (<c>true</c>) or was generated at startup.
    /// </summary>
    public bool IsSecretConfigured { get; }

    /// <summary>
    /// Generates a random API key that starts with "Meknes" and includes a cryptographic signature.
    /// </summary>
    /// <returns>A new API key in the format: Meknes{RandomPart}.{Signature}</returns>
    [Description("Generates a new random API key that starts with 'Meknes'. The key includes a cryptographic signature for validation.")]
    public string GenerateAPIKey()
    {
        // Generate random part
        var randomBytes = new byte[RandomPartLength];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }

        // Convert to base64 URL-safe string (remove padding and replace +/= with -_)
        var randomPart = Convert.ToBase64String(randomBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        // Create the key payload (prefix + random part)
        var keyPayload = $"{KeyPrefix}{randomPart}";

        // Generate signature
        var signature = GenerateSignature(keyPayload);

        // Combine key payload and signature
        return $"{keyPayload}.{signature}";
    }

    /// <summary>
    /// Validates an API key by checking its format and verifying its cryptographic signature.
    /// </summary>
    /// <param name="apiKey">The API key to validate.</param>
    /// <returns>True if the API key is valid, false otherwise.</returns>
    [Description("Validates an API key by checking if it starts with 'Meknes' and verifying its cryptographic signature.")]
    public bool ValidateAPIKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return false;
        }

        // Check if it starts with the prefix
        if (!apiKey.StartsWith(KeyPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        // Split key and signature
        var parts = apiKey.Split('.');
        if (parts.Length != 2)
        {
            return false;
        }

        var keyPayload = parts[0];
        var providedSignature = parts[1];

        // Verify signature
        var expectedSignature = GenerateSignature(keyPayload);
        
        // Use constant-time comparison to prevent timing attacks
        return ConstantTimeEquals(providedSignature, expectedSignature);
    }

    /// <summary>
    /// Generates a cryptographic signature for the given key payload using HMAC-SHA256.
    /// </summary>
    /// <param name="keyPayload">The key payload to sign.</param>
    /// <returns>The base64 URL-safe signature.</returns>
    private string GenerateSignature(string keyPayload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(keyPayload));
        
        return Convert.ToBase64String(hashBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>
    /// Performs a constant-time comparison of two strings to prevent timing attacks.
    /// </summary>
    /// <param name="a">First string to compare.</param>
    /// <param name="b">Second string to compare.</param>
    /// <returns>True if strings are equal, false otherwise.</returns>
    private bool ConstantTimeEquals(string a, string b)
    {
        if (a == null || b == null || a.Length != b.Length)
        {
            return false;
        }

        int result = 0;
        for (int i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }

        return result == 0;
    }
}

