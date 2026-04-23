namespace Sunfish.Kernel.Runtime.Teams;

/// <summary>
/// Strongly-typed identifier for a Sunfish team (tenant). Wraps a <see cref="Guid"/>
/// so the multi-team APIs (ITeamContextFactory, IActiveTeamAccessor, per-team subkey
/// derivation) cannot be accidentally confused with other Guid-valued identifiers
/// (NodeId, StreamId, BucketId, etc.). Per ADR 0032.
/// </summary>
/// <remarks>
/// The string form (see <see cref="ToString"/>) is the "team_id" used by the
/// per-team subkey derivation function in <c>ITeamSubkeyDerivation</c> — the same
/// string goes into the HKDF info parameter <c>"sunfish-team-subkey-v1:" + team_id</c>.
/// Deterministic round-trip via <see cref="Parse(string)"/>.
/// </remarks>
public readonly record struct TeamId(Guid Value)
{
    /// <summary>Allocates a fresh random <see cref="TeamId"/>.</summary>
    public static TeamId New() => new(Guid.NewGuid());

    /// <summary>Parses the standard GUID string form produced by <see cref="ToString"/>.</summary>
    /// <exception cref="FormatException"><paramref name="s"/> is not a valid GUID.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="s"/> is <c>null</c>.</exception>
    public static TeamId Parse(string s) => new(Guid.Parse(s));

    /// <summary>Canonical string form — the GUID's default "D" format (e.g. <c>"aaaa...-...-...-...-bbbb"</c>).</summary>
    public override string ToString() => Value.ToString();
}
