using System;
using System.Diagnostics.CodeAnalysis;

namespace Sunfish.Foundation.UI;

/// <summary>
/// Round-trip helpers between <see cref="SyncState"/> and the canonical
/// lowercase string identifiers per ADR 0036-A1.2.
/// </summary>
public static class SyncStateExtensions
{
    /// <summary>
    /// Returns the canonical lowercase identifier for the sync state
    /// (e.g., <c>"healthy"</c> for <see cref="SyncState.Healthy"/>).
    /// </summary>
    public static string ToCanonicalIdentifier(this SyncState state) => state switch
    {
        SyncState.Healthy => "healthy",
        SyncState.Stale => "stale",
        SyncState.Offline => "offline",
        SyncState.Conflict => "conflict",
        SyncState.Quarantine => "quarantine",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown SyncState value."),
    };

    /// <summary>
    /// Parses a canonical lowercase identifier back into a
    /// <see cref="SyncState"/>. Returns <c>true</c> on success;
    /// <c>false</c> if the identifier is null, empty, or unrecognized.
    /// Comparison is ordinal — the canonical wire form is lowercase per
    /// ADR 0036-A1.2 + parsing of mixed/upper case is intentionally
    /// rejected so that drift in external consumers surfaces here.
    /// </summary>
    public static bool TryFromCanonicalIdentifier(string? identifier, [NotNullWhen(true)] out SyncState state)
    {
        switch (identifier)
        {
            case "healthy": state = SyncState.Healthy; return true;
            case "stale": state = SyncState.Stale; return true;
            case "offline": state = SyncState.Offline; return true;
            case "conflict": state = SyncState.Conflict; return true;
            case "quarantine": state = SyncState.Quarantine; return true;
            default: state = default; return false;
        }
    }
}
