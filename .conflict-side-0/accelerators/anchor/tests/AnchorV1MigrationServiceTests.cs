using Microsoft.Extensions.Logging.Abstractions;
using Sunfish.Anchor.Services;
using Sunfish.Kernel.Runtime.Teams;

namespace Sunfish.Anchor.Tests;

/// <summary>
/// Wave 6.7 tests for <see cref="AnchorV1MigrationService"/>. Each test owns
/// its own temp directory so the filesystem assertions are independent; the
/// legacy team id is pinned to a deterministic GUID so marker-file reads
/// stay stable across runs.
/// </summary>
/// <remarks>
/// We exercise the public <see cref="AnchorV1MigrationService.StartAsync"/>
/// path (not the <c>internal MigrateIfNeeded</c> helper) so the tests also
/// pin the <c>IHostedService</c> shape: <c>StartAsync</c> must block on the
/// migration before returning, otherwise the bootstrap hosted service that
/// registers after it would race against an unfinished move.
/// </remarks>
public sealed class AnchorV1MigrationServiceTests : IDisposable
{
    private static readonly TeamId LegacyTeamId =
        new(new Guid("deadbeef-dead-beef-dead-beefdeadbeef"));

    private readonly string _tempRoot;

    public AnchorV1MigrationServiceTests()
    {
        _tempRoot = Path.Combine(
            Path.GetTempPath(),
            "sunfish-migration-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup — Windows sometimes holds briefly on temp
            // directories; a leaked dir per run is acceptable.
        }
    }

    private AnchorV1MigrationService BuildService(Func<TeamId>? teamIdProvider = null)
    {
        return new AnchorV1MigrationService(
            dataDirectory: _tempRoot,
            legacyTeamIdProvider: teamIdProvider ?? (() => LegacyTeamId),
            logger: NullLogger<AnchorV1MigrationService>.Instance);
    }

    [Fact]
    public async Task No_op_when_no_legacy_files_present()
    {
        // Fresh install — data directory exists but has no v1 files.
        var service = BuildService();

        await service.StartAsync(CancellationToken.None);

        // No v2 teams/ dir, no legacy-backup/ dir, no marker — the migration
        // should detect "no v1 layout" and exit silently.
        Assert.False(Directory.Exists(Path.Combine(_tempRoot, "teams")));
        Assert.False(Directory.Exists(Path.Combine(_tempRoot, "legacy-backup")));
        Assert.False(File.Exists(Path.Combine(_tempRoot, AnchorV1MigrationService.MarkerFileName)));
    }

    [Fact]
    public async Task Migrates_legacy_layout_to_teams_subfolder()
    {
        // Seed a v1 layout: sunfish.db + events/ + buckets/ at the top level.
        SeedV1Layout(includeEvents: true, includeBuckets: true);

        var service = BuildService();
        await service.StartAsync(CancellationToken.None);

        // v2 layout exists under teams/{legacy_team_id}/.
        var teamRoot = TeamPaths.TeamRoot(_tempRoot, LegacyTeamId);
        Assert.True(Directory.Exists(teamRoot));
        Assert.True(File.Exists(TeamPaths.DatabasePath(_tempRoot, LegacyTeamId)));
        Assert.True(Directory.Exists(TeamPaths.EventLogDirectory(_tempRoot, LegacyTeamId)));
        Assert.True(Directory.Exists(TeamPaths.BucketsDirectory(_tempRoot, LegacyTeamId)));

        // Event log + buckets content round-tripped.
        Assert.Equal(
            "event-payload",
            File.ReadAllText(Path.Combine(
                TeamPaths.EventLogDirectory(_tempRoot, LegacyTeamId), "epoch-0.log")));
        Assert.Equal(
            "bucket-payload",
            File.ReadAllText(Path.Combine(
                TeamPaths.BucketsDirectory(_tempRoot, LegacyTeamId), "records.yaml")));

        // legacy-backup/ has the same originals.
        var backup = TeamPaths.LegacyBackupDirectory(_tempRoot);
        Assert.True(Directory.Exists(backup));
        Assert.True(File.Exists(Path.Combine(backup, "sunfish.db")));
        Assert.True(File.Exists(Path.Combine(backup, "events", "epoch-0.log")));
        Assert.True(File.Exists(Path.Combine(backup, "buckets", "records.yaml")));

        // Original top-level files are gone after a successful migration.
        Assert.False(File.Exists(Path.Combine(_tempRoot, "sunfish.db")));
        Assert.False(Directory.Exists(Path.Combine(_tempRoot, "events")));
        Assert.False(Directory.Exists(Path.Combine(_tempRoot, "buckets")));
    }

    [Fact]
    public async Task Second_launch_after_migration_is_noop()
    {
        // Seed the post-migration state explicitly: marker + teams/ layout +
        // legacy-backup/ present, top-level files removed.
        var teamRoot = TeamPaths.TeamRoot(_tempRoot, LegacyTeamId);
        Directory.CreateDirectory(teamRoot);
        File.WriteAllBytes(TeamPaths.DatabasePath(_tempRoot, LegacyTeamId), new byte[] { 0x1, 0x2 });
        var backup = TeamPaths.LegacyBackupDirectory(_tempRoot);
        Directory.CreateDirectory(backup);
        File.WriteAllBytes(Path.Combine(backup, "sunfish.db"), new byte[] { 0x1, 0x2 });
        var markerPath = Path.Combine(_tempRoot, AnchorV1MigrationService.MarkerFileName);
        File.WriteAllText(markerPath, "2026-01-01T00:00:00Z\n" + LegacyTeamId.Value.ToString("D"));

        // Snapshot the db bytes before. If the service were to re-copy the
        // (absent) legacy file, it would either throw or truncate.
        var expectedBytes = File.ReadAllBytes(TeamPaths.DatabasePath(_tempRoot, LegacyTeamId));

        // Counting provider — must NOT be invoked on a no-op launch.
        var invocationCount = 0;
        var service = BuildService(teamIdProvider: () =>
        {
            invocationCount++;
            return LegacyTeamId;
        });

        await service.StartAsync(CancellationToken.None);

        Assert.Equal(0, invocationCount); // marker short-circuit hit first
        Assert.Equal(expectedBytes, File.ReadAllBytes(TeamPaths.DatabasePath(_tempRoot, LegacyTeamId)));
    }

    [Fact]
    public async Task Migration_creates_marker_file_with_legacy_team_id()
    {
        SeedV1Layout(includeEvents: false, includeBuckets: false);

        var service = BuildService();
        await service.StartAsync(CancellationToken.None);

        var markerPath = Path.Combine(_tempRoot, AnchorV1MigrationService.MarkerFileName);
        Assert.True(File.Exists(markerPath));

        var lines = File.ReadAllLines(markerPath);
        Assert.Equal(2, lines.Length);

        // Line 1: ISO-8601 UTC timestamp. Round-trip parse to validate shape.
        var parsedTimestamp = DateTimeOffset.Parse(
            lines[0], System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(
            (DateTimeOffset.UtcNow - parsedTimestamp).Duration() < TimeSpan.FromMinutes(5),
            "marker timestamp should be recent");

        // Line 2: legacy team id in GUID "D" format.
        Assert.Equal(LegacyTeamId.Value.ToString("D"), lines[1]);
    }

    [Fact]
    public async Task Second_launch_without_marker_but_teams_present_is_noop()
    {
        // Guard against the operator manually deleting the marker — if the
        // teams/ directory exists we must not re-run the migration (that
        // would overwrite the active v2 state with stale v1 data if somehow
        // the top-level files also got recreated).
        var teamRoot = TeamPaths.TeamRoot(_tempRoot, LegacyTeamId);
        Directory.CreateDirectory(teamRoot);
        File.WriteAllBytes(TeamPaths.DatabasePath(_tempRoot, LegacyTeamId), new byte[] { 0xA });

        // Also seed a fake "v1" top-level db to tempt the migrator.
        File.WriteAllBytes(Path.Combine(_tempRoot, "sunfish.db"), new byte[] { 0xB });

        var service = BuildService();
        await service.StartAsync(CancellationToken.None);

        // teams/ short-circuit hit; the top-level sunfish.db is left
        // untouched (didn't get moved into the backup) and no marker was
        // written — this is an operator-recoverable state, not a migration.
        Assert.Equal(new byte[] { 0xA }, File.ReadAllBytes(TeamPaths.DatabasePath(_tempRoot, LegacyTeamId)));
        Assert.Equal(new byte[] { 0xB }, File.ReadAllBytes(Path.Combine(_tempRoot, "sunfish.db")));
        Assert.False(File.Exists(Path.Combine(_tempRoot, AnchorV1MigrationService.MarkerFileName)));
    }

    [Fact]
    public async Task Partial_migration_failure_preserves_legacy_backup()
    {
        // Simulate interruption between step 2 (copy to teams/) and step 3
        // (marker write) by seeding the post-step-2 state on disk: legacy
        // originals still present, backup + teams/ also present, no marker.
        // Re-running the service must detect the teams/ directory via the
        // short-circuit and NOT re-copy or delete the originals. The user
        // is then left with a consistent state they can inspect.
        var teamRoot = TeamPaths.TeamRoot(_tempRoot, LegacyTeamId);
        Directory.CreateDirectory(teamRoot);
        File.WriteAllBytes(TeamPaths.DatabasePath(_tempRoot, LegacyTeamId), new byte[] { 0xC });

        var backup = TeamPaths.LegacyBackupDirectory(_tempRoot);
        Directory.CreateDirectory(backup);
        File.WriteAllBytes(Path.Combine(backup, "sunfish.db"), new byte[] { 0xC });

        File.WriteAllBytes(Path.Combine(_tempRoot, "sunfish.db"), new byte[] { 0xC });

        var service = BuildService();
        await service.StartAsync(CancellationToken.None);

        // Backup and originals both retained — idempotent retry.
        Assert.True(File.Exists(Path.Combine(backup, "sunfish.db")));
        Assert.True(File.Exists(Path.Combine(_tempRoot, "sunfish.db")));
        Assert.True(File.Exists(TeamPaths.DatabasePath(_tempRoot, LegacyTeamId)));
    }

    // --- Helpers --------------------------------------------------------

    private void SeedV1Layout(bool includeEvents, bool includeBuckets)
    {
        File.WriteAllBytes(
            Path.Combine(_tempRoot, "sunfish.db"),
            new byte[] { 0xCA, 0xFE, 0xBA, 0xBE });

        if (includeEvents)
        {
            var events = Path.Combine(_tempRoot, "events");
            Directory.CreateDirectory(events);
            File.WriteAllText(Path.Combine(events, "epoch-0.log"), "event-payload");
        }

        if (includeBuckets)
        {
            var buckets = Path.Combine(_tempRoot, "buckets");
            Directory.CreateDirectory(buckets);
            File.WriteAllText(Path.Combine(buckets, "records.yaml"), "bucket-payload");
        }
    }
}
