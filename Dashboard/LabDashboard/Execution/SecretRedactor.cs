namespace LabDashboard.Execution;

/// <summary>
/// Replaces secrets (the Azure OpenAI API key) by <see cref="Mask"/> in the output of a run,
/// before it is streamed to the browser or saved to the history.
/// </summary>
public sealed class SecretRedactor
{
    public const string Mask = "••••";

    /// <summary>Shorter values are not treated as secrets: masking them would corrupt ordinary output.</summary>
    public const int MinimumSecretLength = 8;

    private readonly string[] _secrets;

    public SecretRedactor(IEnumerable<string?> secrets)
    {
        // Longest first, so that a secret containing another one is masked as a whole.
        _secrets = secrets
            .Where(s => s is { Length: >= MinimumSecretLength })
            .Select(s => s!)
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(s => s.Length)
            .ToArray();
    }

    public static SecretRedactor None { get; } = new([]);

    public string Redact(string text)
    {
        foreach (string secret in _secrets)
        {
            if (text.Contains(secret, StringComparison.Ordinal))
            {
                text = text.Replace(secret, Mask, StringComparison.Ordinal);
            }
        }

        return text;
    }

    /// <summary>Redactor for text that arrives in pieces (streamed output), where a secret can be split across two pieces.</summary>
    public Streaming CreateStreaming() => new(this);

    /// <summary>Length of the longest end of <paramref name="text"/> that could be the beginning of a secret.</summary>
    private int PossibleSecretStart(string text)
    {
        int longest = 0;
        foreach (string secret in _secrets)
        {
            for (int length = Math.Min(secret.Length - 1, text.Length); length > longest; length--)
            {
                if (text.AsSpan(text.Length - length).SequenceEqual(secret.AsSpan(0, length)))
                {
                    longest = length;
                    break;
                }
            }
        }

        return longest;
    }

    /// <summary>
    /// Holds back the end of a piece while it could still be the beginning of a secret, and releases it
    /// (masked if needed) with the next piece or at the end of the line.
    /// </summary>
    public sealed class Streaming(SecretRedactor owner)
    {
        private string _pending = "";

        public string Push(string text, bool endOfLine)
        {
            string combined = owner.Redact(_pending + text);
            if (endOfLine || owner._secrets.Length == 0)
            {
                _pending = "";
                return combined;
            }

            int held = owner.PossibleSecretStart(combined);
            _pending = combined[^held..];
            return combined[..^held];
        }
    }
}
