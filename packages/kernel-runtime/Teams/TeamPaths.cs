using System.IO;
using System.Runtime.InteropServices;

namespace Sunfish.Kernel.Runtime.Teams;

/// <summary>
/// Static helper for the on-disk path conventions used by per-team services
/// (SQLCipher database, event log, bucket manifests, keystore entries). Pure
/// functions with no I/O — every method composes paths from the caller-provided
/// <c>dataDirectory</c> and a <see cref="TeamId"/>; none create, inspect, or
/// mutate the filesystem.
/// </summary>
/// <remarks>
/// <para>
/// Lock-in per ADR 0032 §Default (line 103 of the ADR) and the v2 migration
/// layout (line 186). The string conventions — including the nested
/// <c>teams/{team_id}/</c> segment, the literal <c>sunfish.db</c> filename,
/// the <c>events/</c> and <c>buckets/</c> subdirectory names, and the
/// <c>"sunfish:team:{team_id}:primary"</c> keystore key shape — are pinned
/// here so that the per-team service registrar (Waves 6.3.B / 6.3.C / 6.3.D)
/// and any future directory consumer always agree on layout.
/// </para>
/// <para>
/// Wave 6.3.A decomposition plan §5 declares this a static helper (not an
/// interface) deliberately — <c>ITeamDirectoryLayout</c> is NOT introduced
/// here and should only be added later if Anchor's MDM-override
/// (<c>docs/specifications/mdm-config-schema.md</c>) needs runtime substitution.
/// </para>
/// <para>
/// <b>TeamId formatting:</b> every path segment that interpolates the team id
/// uses <c>teamId.Value.ToString("D")</c> — the 36-character hyphenated GUID
/// form <c>"00000000-0000-0000-0000-000000000000"</c>. Callers MUST NEVER
/// URL-encode, hyphen-strip, uppercase, or otherwise normalize the rendered
/// team id before concatenation; doing so breaks the correspondence with the
/// HKDF info string used by <c>ITeamSubkeyDerivation</c> (kernel-security)
/// and produces divergent per-team directories on different machines.
/// </para>
/// <para>
/// <b>Trailing separator convention:</b> directory helpers (<see cref="TeamRoot"/>,
/// <see cref="EventLogDirectory"/>, <see cref="BucketsDirectory"/>,
/// <see cref="LegacyBackupDirectory"/>) return a path <em>without</em> a
/// trailing directory separator. Callers that need a trailing separator add
/// it themselves (e.g. <c>Path.Combine(dir, "manifest.yaml")</c>).
/// </para>
/// </remarks>
public static class TeamPaths
{
    private const string TeamsSegment = "teams";
    private const string EventsSegment = "events";
    private const string BucketsSegment = "buckets";
    private const string LegacyBackupSegment = "legacy-backup";
    private const string DatabaseFileName = "sunfish.db";
    private const string TransportSocketFileName = "sync.sock";
    private const string WindowsPipePrefix = @"\\.\pipe\sunfish-";

    /// <summary>
    /// Returns the on-disk root directory for the given team —
    /// <c>{dataDirectory}/teams/{teamId:D}</c>, no trailing separator.
    /// </summary>
    /// <remarks>
    /// Per ADR 0032 §Default (line 103) the per-team layout is nested under a
    /// single fixed <c>teams/</c> parent inside the install's data directory.
    /// The team id is rendered in GUID "D" format (see type remarks).
    /// </remarks>
    /// <param name="dataDirectory">Install-level data directory (e.g. the value of
    /// <c>LocalNodeOptions.DataDirectory</c> or Anchor's equivalent path accessor).</param>
    /// <param name="teamId">Team whose root directory to compute.</param>
    public static string TeamRoot(string dataDirectory, TeamId teamId)
    {
        ArgumentException.ThrowIfNullOrEmpty(dataDirectory);
        return Path.Combine(dataDirectory, TeamsSegment, teamId.Value.ToString("D"));
    }

    /// <summary>
    /// Returns the absolute path to the team's SQLCipher database file —
    /// <c>{dataDirectory}/teams/{teamId:D}/sunfish.db</c>.
    /// </summary>
    /// <remarks>
    /// Per ADR 0032 §Default (line 103). Binds to
    /// <c>EncryptionOptions.DatabasePath</c> in the per-team registrar (Wave 6.3.B).
    /// The filename <c>sunfish.db</c> is pinned; do not rename per-team.
    /// </remarks>
    public static string DatabasePath(string dataDirectory, TeamId teamId)
    {
        ArgumentException.ThrowIfNullOrEmpty(dataDirectory);
        return Path.Combine(dataDirectory, TeamsSegment, teamId.Value.ToString("D"), DatabaseFileName);
    }

    /// <summary>
    /// Returns the on-disk directory that holds the team's append-only event log —
    /// <c>{dataDirectory}/teams/{teamId:D}/events</c>, no trailing separator.
    /// </summary>
    /// <remarks>
    /// Per ADR 0032 §Default (line 103). Binds to <c>EventLogOptions.Directory</c>
    /// in the per-team registrar (Wave 6.3.B). <c>FileBackedEventLog</c> creates
    /// this directory lazily on first write — <see cref="EventLogDirectory"/>
    /// itself never touches the filesystem.
    /// </remarks>
    public static string EventLogDirectory(string dataDirectory, TeamId teamId)
    {
        ArgumentException.ThrowIfNullOrEmpty(dataDirectory);
        return Path.Combine(dataDirectory, TeamsSegment, teamId.Value.ToString("D"), EventsSegment);
    }

    /// <summary>
    /// Returns the on-disk directory that holds the team's bucket manifest YAML
    /// files — <c>{dataDirectory}/teams/{teamId:D}/buckets</c>, no trailing
    /// separator.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Per ADR 0032 §Default (line 103). Binds to the bucket loader's source
    /// directory in the per-team registrar (Wave 6.3.D).
    /// </para>
    /// <para>
    /// <b>Proposed convention, subject to block-developer-experience review:</b>
    /// the <c>buckets/</c> subdirectory layout (as opposed to, say, a single
    /// <c>buckets.yaml</c> file, or embedding bucket manifests inside
    /// SQLCipher) is the 6.3 proposal and may evolve. If it does, amend this
    /// method in one place — every caller routes through here.
    /// </para>
    /// </remarks>
    public static string BucketsDirectory(string dataDirectory, TeamId teamId)
    {
        ArgumentException.ThrowIfNullOrEmpty(dataDirectory);
        return Path.Combine(dataDirectory, TeamsSegment, teamId.Value.ToString("D"), BucketsSegment);
    }

    /// <summary>
    /// Returns the OS-keystore key name under which the team's SQLCipher
    /// Argon2id-derived encryption key is stored — literally
    /// <c>"sunfish:team:{teamId:D}:primary"</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Per ADR 0032 §Default (line 103). Binds to
    /// <c>EncryptionOptions.KeystoreKeyName</c> in the per-team registrar
    /// (Wave 6.3.B). The legacy v1 single-team name <c>"sunfish-primary"</c>
    /// is intentionally <em>not</em> produced here — v2 teams start fresh and
    /// v1→v2 migration is Wave 6.7's responsibility.
    /// </para>
    /// <para>
    /// The separator is a single colon, the segments are lowercase, and the
    /// team id uses GUID "D" format. Callers MUST NOT case-normalize or
    /// re-punctuate the returned string; the key name is matched literally
    /// against the OS keystore.
    /// </para>
    /// </remarks>
    public static string KeystoreKeyName(TeamId teamId)
    {
        return $"sunfish:team:{teamId.Value:D}:primary";
    }

    /// <summary>
    /// Returns the directory into which v1 single-team data is copied by the
    /// v1→v2 migration (Wave 6.7) before it is restructured under
    /// <c>teams/{teamId:D}/</c> — literally
    /// <c>{dataDirectory}/legacy-backup</c>, no trailing separator and no
    /// <see cref="TeamId"/> segment.
    /// </summary>
    /// <remarks>
    /// Per ADR 0032 v2 migration layout (line 186). The backup path is
    /// install-level, not per-team, because the migration operates on data
    /// that predates the per-team concept.
    /// </remarks>
    public static string LegacyBackupDirectory(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(dataDirectory);
        return Path.Combine(dataDirectory, LegacyBackupSegment);
    }

    /// <summary>
    /// Returns the platform-specific endpoint string that this team's
    /// <c>ISyncDaemonTransport</c> listens on / connects to. POSIX produces
    /// a Unix-domain-socket path <c>{dataDirectory}/teams/{teamId:D}/sync.sock</c>;
    /// Windows produces a named-pipe path <c>\\.\pipe\sunfish-{teamId:D}</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Wave 6.3.C stop-work resolution #1 (per
    /// <c>_shared/product/wave-6.3-decomposition.md</c> §Stop-work). The
    /// decomposition plan asked whether the install-level
    /// <c>ISyncDaemonTransport</c> should be shared across per-team gossip
    /// daemons (forcing a HELLO-level team-id multiplex) or whether each team
    /// should get its own transport endpoint. The decision is per-team
    /// endpoints — simpler, no protocol change — and this helper pins the
    /// convention for every subsequent consumer.
    /// </para>
    /// <para>
    /// <b>Platform split.</b> The same Unix-socket-vs-named-pipe split used
    /// by <c>UnixSocketSyncDaemonTransport</c> (kernel-sync §2.1 /
    /// ADR 0029) is mirrored here so the endpoint string produced by this
    /// helper can be passed straight into the transport's ctor without any
    /// per-caller normalization. On Windows, the full UNC-ish
    /// <c>\\.\pipe\sunfish-{teamId:D}</c> form is returned — the transport
    /// strips the prefix internally if needed.
    /// </para>
    /// <para>
    /// <b>Directory existence.</b> On POSIX the returned path points at a
    /// file inside the team root directory, which the event-log / SQLCipher
    /// registrations create lazily. The transport's
    /// <c>UnixSocketListenerHandle</c> calls <c>File.Delete</c> on any stale
    /// socket at bind time, but does NOT create the parent directory —
    /// callers that spin up the listener before the team root exists must
    /// ensure the directory is present (the event-log registration does this
    /// implicitly on first write). The listener binds lazily so in practice
    /// the directory is always present by the time gossip actually starts.
    /// </para>
    /// </remarks>
    public static string TransportEndpoint(string dataDirectory, TeamId teamId)
    {
        ArgumentException.ThrowIfNullOrEmpty(dataDirectory);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Named pipe: \\.\pipe\sunfish-{teamId:D}. The pipe namespace is
            // flat and process-wide on Windows — teamId is enough to avoid
            // collisions. dataDirectory is ignored on Windows because pipes
            // are not filesystem-anchored; still validated above so callers
            // can't accidentally pass null/empty.
            return WindowsPipePrefix + teamId.Value.ToString("D");
        }
        // POSIX Unix-domain socket: {dataDirectory}/teams/{teamId:D}/sync.sock.
        return Path.Combine(dataDirectory, TeamsSegment, teamId.Value.ToString("D"), TransportSocketFileName);
    }
}
