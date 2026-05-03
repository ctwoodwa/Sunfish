namespace Sunfish.Foundation.UI;

/// <summary>
/// Canonical sync-state enum per ADR 0036's encoding contract (A1.1).
/// 5-value set; PascalCase form of the canonical lowercase identifiers
/// (<c>healthy</c> / <c>stale</c> / <c>offline</c> / <c>conflict</c> /
/// <c>quarantine</c>). Round-trips via
/// <see cref="Sunfish.Foundation.Crypto.CanonicalJson.Serialize"/> as
/// the lowercase string forms (per A1.2) when paired with the
/// <see cref="System.Text.Json.Serialization.JsonStringEnumConverter"/>
/// configured with <see cref="System.Text.Json.JsonNamingPolicy.CamelCase"/>
/// — single-word identifiers are flat-case-identical, producing the
/// lowercase canonical wire form.
/// </summary>
public enum SyncState
{
    /// <summary>Sync is up-to-date and reachable. Canonical identifier <c>"healthy"</c>.</summary>
    Healthy,

    /// <summary>Sync is reachable but stale (last successful sync is past the freshness window). Canonical identifier <c>"stale"</c>.</summary>
    Stale,

    /// <summary>The peer / replica is currently unreachable. Canonical identifier <c>"offline"</c>.</summary>
    Offline,

    /// <summary>A merge produced a conflict that needs operator review. Canonical identifier <c>"conflict"</c>.</summary>
    Conflict,

    /// <summary>The replica has been quarantined (e.g., after a security or integrity violation). Canonical identifier <c>"quarantine"</c>.</summary>
    Quarantine,
}
