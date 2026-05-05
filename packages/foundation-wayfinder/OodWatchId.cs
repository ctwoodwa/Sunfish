using System;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// Stable identifier for one OOD watch (officer-of-the-deck or
/// engineering-officer-of-the-watch rotation). Per ADR 0078 §1.
/// </summary>
/// <param name="Value">Backing GUID; tests use the "N" (32 hex char) form.</param>
public readonly record struct OodWatchId(Guid Value)
{
    /// <summary>Mints a fresh <see cref="OodWatchId"/> backed by a new random GUID.</summary>
    public static OodWatchId NewId() => new(Guid.NewGuid());

    /// <inheritdoc />
    public override string ToString() => Value.ToString("N");
}
