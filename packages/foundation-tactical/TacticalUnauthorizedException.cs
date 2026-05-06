using System;

namespace Sunfish.Foundation.Tactical;

/// <summary>
/// Thrown when a caller invokes an
/// <see cref="ITacticalCommandService"/> or
/// <see cref="ITacticalDataProvider"/> operation without the required
/// <c>Sunfish.Foundation.Ship.Common.ShipAction</c> permission
/// per ADR 0081 §8. Inherits
/// <see cref="UnauthorizedAccessException"/> so cohort retry-policy
/// patterns that suppress that base type continue to apply — and so
/// retry logic MUST NOT swallow the throw.
/// </summary>
public sealed class TacticalUnauthorizedException : UnauthorizedAccessException
{
    /// <summary>Initialize with a denial message.</summary>
    public TacticalUnauthorizedException(string message) : base(message) { }
}
