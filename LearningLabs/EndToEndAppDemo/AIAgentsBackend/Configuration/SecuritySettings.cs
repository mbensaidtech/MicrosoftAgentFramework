namespace AIAgentsBackend.Configuration;

/// <summary>
/// Security configuration settings.
/// </summary>
public class SecuritySettings
{
    /// <summary>
    /// Secret key used for signing context IDs (HMAC-SHA256).
    /// This should be a strong, random string shared between frontend and backend.
    /// </summary>
    public string ContextIdSigningKey { get; set; } = string.Empty;
}

