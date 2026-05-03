using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Kernel.Signatures.Models;

/// <summary>
/// Append-only revocation event for a previously-captured
/// <see cref="SignatureEvent"/>. Per ADR 0054 amendments A4 + A5,
/// concurrent revocations under the AP/CRDT model merge by
/// last-revocation-wins (latest <see cref="RevokedAt"/> in partial
/// order; ties broken by total-order on <see cref="Id"/>).
/// </summary>
public sealed record SignatureRevocation
{
    /// <summary>Stable identifier for this revocation event (drives total-order tie-break).</summary>
    public required RevocationEventId Id { get; init; }

    /// <summary>The signature event being revoked.</summary>
    public required SignatureEventId SignatureEvent { get; init; }

    /// <summary>UTC timestamp of revocation.</summary>
    public required DateTimeOffset RevokedAt { get; init; }

    /// <summary>Actor recording the revocation.</summary>
    public required ActorId RevokedBy { get; init; }

    /// <summary>Categorical reason for revocation.</summary>
    public required RevocationReason Reason { get; init; }

    /// <summary>Free-text note; required when <see cref="Reason"/> is <see cref="RevocationReason.Other"/>.</summary>
    public string? Note { get; init; }
}
