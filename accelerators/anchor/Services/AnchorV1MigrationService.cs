using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sunfish.Kernel.Runtime.Teams;

namespace Sunfish.Anchor.Services;

/// <summary>
/// Wave 6.7 v1→v2 data migration hosted service. On first launch of v2 Anchor
/// (and v2 local-node-host), detects the legacy single-team layout under
/// <c>{DataDirectory}/sunfish.db</c> + <c>{DataDirectory}/events/</c> +
/// <c>{DataDirectory}/buckets/</c> and migrates it in-place to the v2
/// per-team shape <c>{DataDirectory}/teams/{legacy_team_id}/</c> per
/// ADR 0032 lines 183-187.
/// </summary>
/// <remarks>
/// <para>
/// <b>Execution order.</b> Registered BEFORE
/// <see cref="AnchorBootstrapHostedService"/> in the MAUI composition root,
/// because bootstrap materializes the default team against the v2 layout and
/// needs the migration to have already happened. <c>StartAsync</c> is
/// synchronous-enough-to-block-the-bootstrap: it runs the migration inline
/// before returning.
/// </para>
/// <para>
/// <b>Safety ordering.</b> The migration performs filesystem work in an order
/// that minimizes the window in which data is neither at its original
/// location nor at the destination:
/// <list type="number">
///   <item><description>Copy the v1 top-level files into
///     <c>{DataDirectory}/legacy-backup/</c> first. The backup copy is the
///     "safety net" that ADR 0032 line 186 promises to keep for "one minor
///     version cycle" before deletion.</description></item>
///   <item><description>Copy the v1 top-level files into
///     <c>{DataDirectory}/teams/{legacy_team_id}/</c>. After this step the
///     data exists in THREE places: original, backup, and new v2
///     location.</description></item>
///   <item><description>Write the <c>.migration-v2</c> marker file so
///     subsequent launches skip migration.</description></item>
///   <item><description>Delete the v1 top-level files. After this step the
///     data exists in TWO places: backup + new v2 location.</description></item>
/// </list>
/// If the process is interrupted at any point, re-running the service is
/// idempotent — the marker file plus the presence of the v2 layout short-circuit
/// the check.
/// </para>
/// <para>
/// <b>Legacy team id.</b> Anchor v1 did not persist a <c>NodeId</c> (see
/// <c>AnchorSessionService</c>: the id is derived from the attestation public
/// key at onboarding and kept in memory). The legacy team id is therefore
/// synthesized from the install's root seed via
/// <c>legacyTeamIdProvider</c> — callers typically pass the first 16 bytes of
/// the root Ed25519 public key, which mirrors the NodeId-derivation convention
/// already used by the composition root. Because the root seed is
/// install-scoped and deterministic, the same machine always produces the
/// same legacy team id across relaunches; the marker file records the value
/// so subsequent launches never re-derive it.
/// </para>
/// <para>
/// <b>Keystore.</b> The legacy team keeps its v1 keystore slot name
/// <c>"sunfish-primary"</c> (see <see cref="TeamPaths.LegacyKeystoreKeyName"/>)
/// rather than re-keying the SQLCipher database via <c>PRAGMA rekey</c>.
/// This is Option 1 of the Wave 6.7 keystore-resolution decision — the legacy
/// DB is never touched by a key-change operation, so the migration cannot
/// corrupt it mid-way. Every team added after migration uses
/// <see cref="TeamPaths.KeystoreKeyName(TeamId)"/>.
/// </para>
/// <para>
/// <b>Testability.</b> The service takes a <c>dataDirectory</c> path and a
/// <c>legacyTeamIdProvider</c> delegate in its ctor rather than resolving
/// them from MAUI's <c>FileSystem.AppDataDirectory</c> directly. The MAUI
/// composition root wires those values via
/// <c>AnchorRootSeedReader.GetDefaultDataDirectory()</c>; tests pass temp
/// directories and deterministic team ids.
/// </para>
/// </remarks>
public sealed class AnchorV1MigrationService : IHostedService
{
    /// <summary>
    /// File name of the legacy v1 SQLCipher database that lived at the
    /// top level of <c>DataDirectory</c> before Wave 6.3.
    /// </summary>
    public const string LegacyDatabaseFileName = "sunfish.db";

    /// <summary>
    /// Directory name of the legacy v1 append-only event log.
    /// </summary>
    public const string LegacyEventsDirectoryName = "events";

    /// <summary>
    /// Directory name of the legacy v1 bucket manifests.
    /// </summary>
    public const string LegacyBucketsDirectoryName = "buckets";

    /// <summary>
    /// Name of the marker file written at the install-level data directory
    /// once migration completes. Contains two lines: an ISO-8601 UTC
    /// timestamp and the <c>legacy_team_id</c> GUID in "D" format.
    /// Subsequent launches short-circuit on the presence of this file.
    /// </summary>
    public const string MarkerFileName = ".migration-v2";

    private readonly string _dataDirectory;
    private readonly Func<TeamId> _legacyTeamIdProvider;
    private readonly ILogger<AnchorV1MigrationService> _logger;

    /// <summary>Construct the migration service.</summary>
    /// <param name="dataDirectory">Install-level data directory. Typically
    /// <c>FileSystem.AppDataDirectory</c> (MAUI) or
    /// <c>LocalNodeOptions.DataDirectory</c>. Must be non-null and non-empty.</param>
    /// <param name="legacyTeamIdProvider">Callback that produces the
    /// <see cref="TeamId"/> to assign the legacy team when migration runs.
    /// Invoked at most once per migration; the result is persisted to the
    /// marker file so subsequent launches don't re-derive it. Must not
    /// return <c>null</c> team id.</param>
    /// <param name="logger">Logger for migration progress + completion.</param>
    public AnchorV1MigrationService(
        string dataDirectory,
        Func<TeamId> legacyTeamIdProvider,
        ILogger<AnchorV1MigrationService> logger)
    {
        ArgumentException.ThrowIfNullOrEmpty(dataDirectory);
        ArgumentNullException.ThrowIfNull(legacyTeamIdProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _dataDirectory = dataDirectory;
        _legacyTeamIdProvider = legacyTeamIdProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        MigrateIfNeeded(cancellationToken);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        // No-op — migration is a one-shot StartAsync activity.
        return Task.CompletedTask;
    }

    /// <summary>
    /// Entry point for the migration logic. Exposed as <c>internal</c> so
    /// the test suite can invoke it directly against a temp directory
    /// without building a full <see cref="IHost"/>.
    /// </summary>
    internal void MigrateIfNeeded(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Short-circuit 1: if the marker file exists, we've already migrated.
        var markerPath = Path.Combine(_dataDirectory, MarkerFileName);
        if (File.Exists(markerPath))
        {
            _logger.LogDebug(
                "V1→V2 migration already completed (marker present at {Marker}); skipping.",
                markerPath);
            return;
        }

        // Short-circuit 2: if the teams/ subdirectory exists already, we're
        // either fully migrated (marker was manually deleted) or this is a
        // fresh v2 install that never had v1 data. Either way, nothing to do.
        var teamsRoot = Path.Combine(_dataDirectory, "teams");
        if (Directory.Exists(teamsRoot))
        {
            _logger.LogDebug(
                "V2 teams/ layout already present at {Path}; skipping migration.",
                teamsRoot);
            return;
        }

        // Detect v1 layout: at minimum the sunfish.db file must exist at the
        // top level. events/ and buckets/ are optional — early v1 installs
        // may only have materialized the DB.
        var legacyDbPath = Path.Combine(_dataDirectory, LegacyDatabaseFileName);
        var legacyEventsPath = Path.Combine(_dataDirectory, LegacyEventsDirectoryName);
        var legacyBucketsPath = Path.Combine(_dataDirectory, LegacyBucketsDirectoryName);

        if (!File.Exists(legacyDbPath))
        {
            _logger.LogDebug(
                "No v1 layout detected at {DataDirectory} (no {Db}); fresh v2 install.",
                _dataDirectory,
                LegacyDatabaseFileName);
            return;
        }

        var legacyTeamId = _legacyTeamIdProvider();
        _logger.LogInformation(
            "V1 layout detected at {DataDirectory}; migrating to v2 under team id {TeamId}.",
            _dataDirectory,
            legacyTeamId);

        // Step 1 — safety copy into legacy-backup/. This runs first so a crash
        // between step 1 and step 2 still leaves the original data in place
        // (the backup is redundant with the original until step 4).
        var backupRoot = TeamPaths.LegacyBackupDirectory(_dataDirectory);
        Directory.CreateDirectory(backupRoot);
        CopyFileIfPresent(legacyDbPath, Path.Combine(backupRoot, LegacyDatabaseFileName));
        CopyDirectoryIfPresent(legacyEventsPath, Path.Combine(backupRoot, LegacyEventsDirectoryName));
        CopyDirectoryIfPresent(legacyBucketsPath, Path.Combine(backupRoot, LegacyBucketsDirectoryName));

        // Step 2 — copy into the v2 team root. We use Copy rather than Move
        // because Move across directories on Windows is sometimes a copy
        // anyway, and Copy gives us a cleaner rollback story if step 3's
        // marker write fails.
        var teamRoot = TeamPaths.TeamRoot(_dataDirectory, legacyTeamId);
        Directory.CreateDirectory(teamRoot);
        CopyFileIfPresent(legacyDbPath, TeamPaths.DatabasePath(_dataDirectory, legacyTeamId));
        CopyDirectoryIfPresent(
            legacyEventsPath,
            TeamPaths.EventLogDirectory(_dataDirectory, legacyTeamId));
        CopyDirectoryIfPresent(
            legacyBucketsPath,
            TeamPaths.BucketsDirectory(_dataDirectory, legacyTeamId));

        // Step 3 — write the marker before deleting the originals so that a
        // crash between steps 3 and 4 still records the migration as
        // completed. A subsequent launch would see the marker + the v2
        // layout and skip the originals cleanup; the originals would linger
        // harmlessly until the next minor-version cleanup pass.
        var markerContents = string.Join(
            '\n',
            DateTimeOffset.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            legacyTeamId.Value.ToString("D"));
        File.WriteAllText(markerPath, markerContents);

        // Step 4 — delete the originals only after the backup + v2 copies +
        // marker are all persisted. A crash mid-delete is safe: the backup
        // plus v2 copies are already on disk, and the marker ensures
        // re-running the service is a no-op.
        if (File.Exists(legacyDbPath))
        {
            File.Delete(legacyDbPath);
        }
        if (Directory.Exists(legacyEventsPath))
        {
            Directory.Delete(legacyEventsPath, recursive: true);
        }
        if (Directory.Exists(legacyBucketsPath))
        {
            Directory.Delete(legacyBucketsPath, recursive: true);
        }

        _logger.LogInformation(
            "V1→V2 migration complete. Legacy team id: {TeamId}. Backup at {Backup}.",
            legacyTeamId,
            backupRoot);
    }

    private static void CopyFileIfPresent(string source, string destination)
    {
        if (!File.Exists(source))
        {
            return;
        }
        var destDir = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(destDir))
        {
            Directory.CreateDirectory(destDir);
        }
        File.Copy(source, destination, overwrite: true);
    }

    private static void CopyDirectoryIfPresent(string source, string destination)
    {
        if (!Directory.Exists(source))
        {
            return;
        }
        Directory.CreateDirectory(destination);
        foreach (var filePath in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, filePath);
            var target = Path.Combine(destination, relative);
            var targetDir = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }
            File.Copy(filePath, target, overwrite: true);
        }
    }
}
