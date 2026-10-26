using System.Buffers.Text;
using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using LabDashboard.Reporting;

namespace LabDashboard.Tests;

/// <summary>
/// The trainer's invitation code (Business Rules 21–23): the <c>LB1.</c> format, what is refused (<c>invalid</c>, <c>unsupportedVersion</c>), and the
/// trainer's command, called directly and as <c>dotnet LabDashboard.dll invitation</c> (no dashboard started, keys never echoed). No network.
/// </summary>
public sealed class InvitationCodeTests
{
    private const string ApiKey = "lbk_invitation_admin_api_key_5c2e";
    private const string WorkshopKey = "ws-invitation-key-91b7";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // ---------- format ----------

    [Fact]
    public void A_code_is_LB1_and_the_unpadded_base64url_of_the_json_of_both_keys_and_it_parses_back()
    {
        string code = InvitationCode.Create(ApiKey, WorkshopKey);

        Assert.StartsWith("LB1.", code, StringComparison.Ordinal);
        Assert.DoesNotContain('=', code);
        JsonNode json = JsonNode.Parse(Base64Url.DecodeFromChars(code.AsSpan(4)))!;
        Assert.Equal(ApiKey, json["apiKey"]!.GetValue<string>());
        Assert.Equal(WorkshopKey, json["workshopKey"]!.GetValue<string>());

        Invitation invitation = InvitationCode.Parse(code, out string? error)!;
        Assert.Null(error);
        Assert.Equal(new Invitation(ApiKey, WorkshopKey), invitation);
        Assert.True(InvitationCode.IsInvitation(code));
    }

    [Fact]
    public void A_code_built_elsewhere_from_the_documented_format_is_accepted_in_any_property_order_with_unknown_properties()
    {
        string json = $$"""{ "note": "workshop of 2026-10-27", "workshopKey": "{{WorkshopKey}}", "apiKey": "{{ApiKey}}" }""";
        string code = InvitationCode.Prefix + Base64Url.EncodeToString(Encoding.UTF8.GetBytes(json));

        Assert.Equal(new Invitation(ApiKey, WorkshopKey), InvitationCode.Parse(code, out _));
    }

    [Fact]
    public void Whitespace_around_or_inside_a_pasted_code_is_ignored()
    {
        string code = InvitationCode.Create(ApiKey, WorkshopKey);
        string wrapped = $"  {code[..20]}\r\n  {code[20..]}\n";

        Assert.True(InvitationCode.IsInvitation(wrapped));
        Assert.Equal(new Invitation(ApiKey, WorkshopKey), InvitationCode.Parse(wrapped, out _));
    }

    public static TheoryData<string> InvalidCodes()
    {
        static string Code(string json) => InvitationCode.Prefix + Base64Url.EncodeToString(Encoding.UTF8.GetBytes(json));
        return
        [
            "LB1.",
            "LB1.***",
            "LB1.e30=x",
            Code("not json"),
            Code("[]"),
            Code("{}"),
            Code("""{"apiKey":"lbk_a"}"""),
            Code("""{"workshopKey":"ws"}"""),
            Code("""{"apiKey":"","workshopKey":"ws"}"""),
            Code("""{"apiKey":"lbk_a","workshopKey":null}"""),
            Code("""{"apiKey":"lbk a","workshopKey":"ws"}"""),
            Code("""{"apiKey":"lbk_a","workshopKey":"ws\t"}"""),
            Code("""{"apiKey":"lbk_é","workshopKey":"ws"}"""),
            Code("""{"apiKey":1,"workshopKey":"ws"}"""),
            InvitationCode.Prefix + Base64Url.EncodeToString([0xFF, 0xFE, 0x7B, 0x7D]),
            Code($$"""{"apiKey":"{{new string('k', 800)}}","workshopKey":"ws"}"""),
        ];
    }

    [Theory]
    [MemberData(nameof(InvalidCodes))]
    public void A_code_that_cannot_be_decoded_or_lacks_a_usable_key_is_invalid(string code)
    {
        Assert.True(InvitationCode.IsInvitation(code));
        Assert.Null(InvitationCode.Parse(code, out string? error));
        Assert.Equal("invalid", error);
    }

    [Theory]
    [InlineData("LB2.e30")]
    [InlineData("LB10.eyJhcGlLZXkiOiJsYmtfYSJ9")]
    [InlineData("LB0.e30")]
    public void Another_version_is_unsupported(string code)
    {
        Assert.True(InvitationCode.IsInvitation(code));
        Assert.Null(InvitationCode.Parse(code, out string? error));
        Assert.Equal("unsupportedVersion", error);
    }

    [Theory]
    [InlineData("workshop-key-2026")]
    [InlineData("LB.e30")]
    [InlineData("LBX.e30")]
    [InlineData("lb1.e30")]
    [InlineData("LB1")]
    [InlineData("xLB1.e30")]
    public void A_value_that_does_not_start_like_an_invitation_is_a_plain_workshop_key(string value) =>
        Assert.False(InvitationCode.IsInvitation(value));

    [Theory]
    [InlineData("", "ws")]
    [InlineData("lbk_a", "")]
    [InlineData("lbk a", "ws")]
    [InlineData("lbk_a", " ws")]
    [InlineData("lbk_a\n", "ws")]
    [InlineData("lbk_é", "ws")]
    public void Creating_a_code_refuses_an_empty_key_or_one_with_whitespace_and_never_quotes_it(string apiKey, string workshopKey)
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() => InvitationCode.Create(apiKey, workshopKey));

        foreach (string value in new[] { apiKey, workshopKey }.Where(v => v.Trim().Length > 1))
        {
            Assert.DoesNotContain(value.Trim(), error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void An_invitation_never_prints_its_keys()
    {
        string text = new Invitation(ApiKey, WorkshopKey).ToString();

        Assert.DoesNotContain(ApiKey, text, StringComparison.Ordinal);
        Assert.DoesNotContain(WorkshopKey, text, StringComparison.Ordinal);
    }

    // ---------- the trainer's command ----------

    [Fact]
    public void The_command_prints_only_a_code_that_the_dashboard_accepts_and_never_echoes_the_keys()
    {
        Queue<string> answers = new([ApiKey, WorkshopKey]);
        List<string> prompts = [];
        StringWriter output = new();
        StringWriter messages = new();

        int exitCode = InvitationCommand.Run([], prompt => { prompts.Add(prompt); return answers.Dequeue(); }, output, messages);

        Assert.Equal(0, exitCode);
        string code = Assert.Single(output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal(new Invitation(ApiKey, WorkshopKey), InvitationCode.Parse(code, out _));
        Assert.Equal(2, prompts.Count);
        Assert.All(prompts, prompt => Assert.Contains("not echoed", prompt, StringComparison.Ordinal));
        Assert.DoesNotContain(ApiKey, messages.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(WorkshopKey, messages.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, WorkshopKey)]
    [InlineData("", WorkshopKey)]
    [InlineData(" ", WorkshopKey)]
    [InlineData(" lbk_padded", WorkshopKey)]
    [InlineData("lbk with space", WorkshopKey)]
    [InlineData(ApiKey, null)]
    [InlineData(ApiKey, "")]
    [InlineData(ApiKey, "ws\tkey")]
    public void The_command_refuses_an_empty_value_or_a_value_with_whitespace_and_prints_no_code(string? apiKey, string? workshopKey)
    {
        Queue<string?> answers = new([apiKey, workshopKey]);
        StringWriter output = new();
        StringWriter messages = new();

        int exitCode = InvitationCommand.Run([], _ => answers.Dequeue(), output, messages);

        Assert.Equal(1, exitCode);
        Assert.Empty(output.ToString());
        Assert.Contains("no invitation code was created", messages.ToString(), StringComparison.OrdinalIgnoreCase);
        foreach (string value in new[] { apiKey, workshopKey }.OfType<string>().Select(v => v.Trim()).Where(v => v.Length > 1))
        {
            Assert.DoesNotContain(value, messages.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_command_never_takes_the_keys_as_arguments()
    {
        StringWriter output = new();
        StringWriter messages = new();
        int prompts = 0;

        int exitCode = InvitationCommand.Run([ApiKey, WorkshopKey], _ => { prompts++; return null; }, output, messages);

        Assert.Equal(2, exitCode);
        Assert.Equal(0, prompts);
        Assert.Empty(output.ToString());
        Assert.DoesNotContain(ApiKey, messages.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_dashboard_program_runs_the_command_with_piped_keys_and_starts_no_dashboard()
    {
        string dashboard = Path.Combine(AppContext.BaseDirectory, "LabDashboard.dll");
        string dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host && File.Exists(host) ? host : "dotnet";
        ProcessStartInfo start = new(dotnet)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // A directory without any appsettings: the command must not need the dashboard configuration.
            WorkingDirectory = Path.GetTempPath(),
        };
        start.ArgumentList.Add(dashboard);
        start.ArgumentList.Add(InvitationCommand.Name);

        using Process process = Process.Start(start)!;
        await process.StandardInput.WriteLineAsync(ApiKey.AsMemory(), Token);
        await process.StandardInput.WriteLineAsync(WorkshopKey.AsMemory(), Token);
        process.StandardInput.Close();
        Task<string> output = process.StandardOutput.ReadToEndAsync(Token);
        Task<string> messages = process.StandardError.ReadToEndAsync(Token);
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(timeout.Token);

        Assert.Equal(0, process.ExitCode);
        string code = Assert.Single((await output).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        Assert.Equal(new Invitation(ApiKey, WorkshopKey), InvitationCode.Parse(code, out _));
        string notes = await messages;
        Assert.DoesNotContain("Lab dashboard running", notes, StringComparison.Ordinal);
        Assert.DoesNotContain(ApiKey, notes, StringComparison.Ordinal);
        Assert.DoesNotContain(WorkshopKey, notes, StringComparison.Ordinal);
    }
}
