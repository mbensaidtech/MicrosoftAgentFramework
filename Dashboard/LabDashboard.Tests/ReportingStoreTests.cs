using System.Text.Json;
using System.Text.Json.Nodes;
using LabDashboard.Catalog;
using LabDashboard.Execution;
using LabDashboard.Reporting;

namespace LabDashboard.Tests;

/// <summary>Identity file, outbox and progress snapshot: everything works in a temporary folder.</summary>
public sealed class ReportingStoreTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("labbench-reporting-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Creates_the_identity_with_a_uuid_private_to_the_user_and_reloads_it()
    {
        string file = Path.Combine(_root, "data", "identity.json");
        IdentityStore store = new(file);
        Assert.Null(store.Current);

        DevIdentity created = store.Create("john-dev");

        Assert.True(Guid.TryParseExact(created.UserId, "D", out _));
        Assert.Equal("john-dev", created.Username);
        Assert.Null(created.DevToken);
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
        }

        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(file)!, "*.tmp"));
        Assert.Equal(created, new IdentityStore(file).Current);
    }

    [Fact]
    public void Updates_keep_the_user_id_and_are_written_atomically()
    {
        string file = Path.Combine(_root, "identity.json");
        IdentityStore store = new(file);
        DevIdentity created = store.Create("john-dev");

        DevIdentity? updated = store.Update(i => i with { Username = "jane", DevToken = "secret-token" });

        Assert.Equal(created.UserId, updated!.UserId);
        Assert.Equal("jane", new IdentityStore(file).Current!.Username);
        Assert.Equal("secret-token", new IdentityStore(file).Current!.DevToken);
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Fact]
    public void An_unreadable_identity_file_is_kept_aside_and_a_new_identity_is_asked()
    {
        string file = Path.Combine(_root, "identity.json");
        File.WriteAllText(file, "{ not json");

        IdentityStore store = new(file);

        Assert.Null(store.Current);
        Assert.True(File.Exists(file + ".corrupt"));
    }

    [Theory]
    [InlineData(null, "required")]
    [InlineData("", "required")]
    [InlineData("a", "tooShort")]
    [InlineData("john dev", "invalid")]
    [InlineData("jöhn", "invalid")]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456", "tooLong")]
    [InlineData("john-dev", null)]
    [InlineData("j.d_2", null)]
    public void Validates_the_username_like_the_admin_api(string? username, string? code) =>
        Assert.Equal(code, IdentityStore.ValidateUsername(username));

    [Fact]
    public void Outbox_persists_events_in_order_and_reloads_them()
    {
        string file = Path.Combine(_root, "outbox.json");
        ReportingOutbox outbox = new(file);
        outbox.Enqueue(Event("1", "run.started"));
        outbox.Enqueue(Event("2", "run.finished", new JsonObject { ["status"] = "passed" }));
        outbox.Persist();

        ReportingOutbox reloaded = new(file);

        Assert.Equal(["1", "2"], reloaded.PeekBatch().Select(e => e.EventId));
        Assert.Equal("passed", reloaded.PeekBatch()[1].Payload!["status"]!.GetValue<string>());
        Assert.Equal("lab-1", reloaded.PeekBatch()[0].LabId);
    }

    [Fact]
    public void Outbox_drops_the_oldest_events_beyond_its_capacity_and_batches_one_hundred()
    {
        ReportingOutbox outbox = new(Path.Combine(_root, "outbox.json"));
        for (int i = 0; i < ReportingOutbox.Capacity + 25; i++)
        {
            outbox.Enqueue(Event(i.ToString(), "lab.opened"));
        }

        Assert.Equal(ReportingOutbox.Capacity, outbox.Count);
        IReadOnlyList<ReportingEvent> batch = outbox.PeekBatch();
        Assert.Equal(ReportingOutbox.BatchSize, batch.Count);
        Assert.Equal("25", batch[0].EventId); // 0..24 were dropped

        outbox.Remove(batch.Select(e => e.EventId));
        Assert.Equal(ReportingOutbox.Capacity - ReportingOutbox.BatchSize, outbox.Count);
        Assert.Equal("125", outbox.PeekBatch()[0].EventId);
    }

    [Fact]
    public void Snapshot_uses_the_completion_rule_of_the_local_ui()
    {
        DateTimeOffset t0 = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);
        // History is newest first.
        RunRecord[] runs =
        [
            Run("r4", RunTarget.Solution, RunStatus.Passed, t0.AddMinutes(30), tokens: 900),
            Run("r3", RunTarget.Start, RunStatus.Failed, t0.AddMinutes(20), tokens: 100),
            Run("r2", RunTarget.Start, RunStatus.Passed, t0.AddMinutes(10), tokens: 300),
            Run("r1", RunTarget.Start, RunStatus.Failed, t0, tokens: null),
        ];

        LabProgressSnapshot snapshot = ProgressSnapshot.ForLab("lab-1", runs);

        Assert.Equal("completed", snapshot.State);
        Assert.Equal(3, snapshot.RunCount);
        Assert.Equal(1, snapshot.SolutionRunCount);
        Assert.Equal(t0.AddMinutes(10), snapshot.CompletedAt);
        Assert.Equal(t0, snapshot.FirstRunAt);
        Assert.Equal(t0.AddMinutes(30), snapshot.LastRunAt);
        Assert.Equal(RunStatus.Passed, snapshot.LastRunStatus);
        Assert.Equal(RunTarget.Solution, snapshot.LastRunTarget);
        Assert.Equal(400, snapshot.TotalTokens); // Start runs only

        Assert.Equal("inProgress", ProgressSnapshot.ForLab("lab-2", [Run("x", RunTarget.Start, RunStatus.Failed, t0)]).State);
        Assert.Equal("notStarted", ProgressSnapshot.ForLab("lab-3", [Run("y", RunTarget.Solution, RunStatus.Passed, t0)]).State);
        Assert.Equal("notStarted", ProgressSnapshot.ForLab("lab-4", []).State);
    }

    [Fact]
    public void Snapshot_payload_uses_the_json_names_of_the_admin_api()
    {
        DateTimeOffset t0 = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);
        JsonNode node = JsonSerializer.SerializeToNode(ProgressSnapshot.ForLab("lab-1", [Run("r1", RunTarget.Start, RunStatus.TimedOut, t0)]), ProgressSnapshot.JsonOptions)!;

        Assert.Equal("lab-1", node["labId"]!.GetValue<string>());
        Assert.Equal("inProgress", node["state"]!.GetValue<string>());
        Assert.Equal("timedOut", node["lastRunStatus"]!.GetValue<string>());
        Assert.Equal("start", node["lastRunTarget"]!.GetValue<string>());
        Assert.Null(node["completedAt"]);
    }

    private static ReportingEvent Event(string id, string type, JsonNode? payload = null) =>
        new(id, type, DateTimeOffset.Now, "lab-1", payload ?? new JsonObject());

    private static RunRecord Run(string id, RunTarget target, RunStatus status, DateTimeOffset startedAt, long? tokens = null) => new()
    {
        RunId = id,
        LabId = "lab-1",
        Target = target,
        Status = status,
        Summary = status.ToString(),
        StartedAt = startedAt,
        DurationMs = 1000,
        TokenUsage = tokens is null ? null : new TokenUsageSummary([], null, null, null, tokens),
    };
}
