using LabDashboard.Execution;

namespace LabDashboard.Tests;

public class SecretRedactorTests
{
    private const string Key = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void Masks_every_occurrence_of_a_secret()
    {
        SecretRedactor redactor = new([Key]);

        Assert.Equal($"key={SecretRedactor.Mask} again {SecretRedactor.Mask}", redactor.Redact($"key={Key} again {Key}"));
    }

    [Fact]
    public void Ignores_values_too_short_to_be_secrets()
    {
        SecretRedactor redactor = new(["abc", null, ""]);

        Assert.Equal("abc abc", redactor.Redact("abc abc"));
    }

    [Fact]
    public void Masks_a_secret_split_across_streamed_pieces()
    {
        SecretRedactor.Streaming stream = new SecretRedactor([Key]).CreateStreaming();

        string shown = stream.Push("Your key is 0123456789", endOfLine: false)
            + stream.Push("abcdef0123456789abcdef.", endOfLine: false)
            + stream.Push(" Done", endOfLine: true);

        Assert.Equal($"Your key is {SecretRedactor.Mask}. Done", shown);
    }

    [Fact]
    public void Holds_back_only_what_could_start_a_secret_and_releases_it_at_the_end_of_the_line()
    {
        SecretRedactor.Streaming stream = new SecretRedactor([Key]).CreateStreaming();

        Assert.Equal("Paris is ", stream.Push("Paris is 012", endOfLine: false));
        Assert.Equal("012 km away", stream.Push(" km away", endOfLine: true));
    }

    [Fact]
    public void Without_secrets_streamed_text_is_passed_through_immediately()
    {
        SecretRedactor.Streaming stream = SecretRedactor.None.CreateStreaming();

        Assert.Equal("Once upon", stream.Push("Once upon", endOfLine: false));
    }
}
