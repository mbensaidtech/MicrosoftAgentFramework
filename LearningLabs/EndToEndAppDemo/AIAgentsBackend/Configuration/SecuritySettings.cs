namespace AIAgentsBackend.Configuration;

/// <summary>
/// Security settings for the application.
/// </summary>
public class SecuritySettings
{
    /// <summary>
    /// Secret key for signing context IDs. Must be at least 32 characters.
    /// </summary>
    public string ContextIdSigningKey { get; set; } = string.Empty;
}
