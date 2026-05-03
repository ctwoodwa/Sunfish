using Sunfish.Foundation.Assets.Common;
using Sunfish.Kernel.Signatures.Models;

namespace Sunfish.Blocks.Leases.Models;

/// <summary>
/// Identifier for a <see cref="LeaseDocumentVersion"/>.
/// </summary>
public readonly record struct LeaseDocumentVersionId(Guid Value);

/// <summary>
/// Append-only revision of a <see cref="Lease"/>'s document per
/// ADR 0054 amendment A1 (content-hash binding) + W#27 Phase 2.
/// Each revision has a monotonically-increasing
/// <see cref="VersionNumber"/> per lease + a SHA-256
/// <see cref="DocumentHash"/> over canonical bytes (the bytes the
/// signers' <see cref="LeasePartySignature"/> entries reference).
/// </summary>
public sealed record LeaseDocumentVersion
{
    /// <summary>Stable identifier for this revision.</summary>
    public required LeaseDocumentVersionId Id { get; init; }

    /// <summary>The lease this revision belongs to.</summary>
    public required LeaseId Lease { get; init; }

    /// <summary>Monotonically-increasing version number; first append = 1.</summary>
    public required int VersionNumber { get; init; }

    /// <summary>SHA-256 hash over the canonical document bytes (per ADR 0054 A1).</summary>
    public required ContentHash DocumentHash { get; init; }

    /// <summary>Opaque reference to the tenant-key-encrypted document blob in storage.</summary>
    public required string DocumentBlobRef { get; init; }

    /// <summary>Actor who authored this revision (operator or applicant).</summary>
    public required ActorId AuthoredBy { get; init; }

    /// <summary>UTC timestamp the revision was appended.</summary>
    public required DateTimeOffset AuthoredAt { get; init; }

    /// <summary>Free-text revision note for audit + UI rendering.</summary>
    public required string ChangeSummary { get; init; }
}
