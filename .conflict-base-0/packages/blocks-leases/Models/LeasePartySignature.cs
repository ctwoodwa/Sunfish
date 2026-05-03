using Sunfish.Kernel.Signatures.Models;

namespace Sunfish.Blocks.Leases.Models;

/// <summary>Identifier for a <see cref="LeasePartySignature"/>.</summary>
public readonly record struct LeasePartySignatureId(Guid Value);

/// <summary>
/// One party's signature on a specific revision of a lease document
/// per ADR 0054. W#27 Phase 3.
/// </summary>
public sealed record LeasePartySignature
{
    /// <summary>Stable identifier.</summary>
    public required LeasePartySignatureId Id { get; init; }

    /// <summary>The lease this signature belongs to.</summary>
    public required LeaseId Lease { get; init; }

    /// <summary>The signing party (typically a tenant).</summary>
    public required PartyId Party { get; init; }

    /// <summary>Reference to the captured signature event (kernel-signatures).</summary>
    public required SignatureEventId SignatureEvent { get; init; }

    /// <summary>Which document revision this signature attests to. Used by the AwaitingSignature → Executed transition guard.</summary>
    public required LeaseDocumentVersionId DocumentVersion { get; init; }

    /// <summary>UTC timestamp recorded at signature collection (mirrors the kernel signature event's <c>SignedAt</c>).</summary>
    public required DateTimeOffset SignedAt { get; init; }
}
