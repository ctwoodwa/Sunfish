using Sunfish.Foundation.Assets.Common;
using Sunfish.Kernel.Signatures.Models;

namespace Sunfish.Blocks.Leases.Models;

/// <summary>
/// Canonical lease record. Intentionally thin for the first pass; full workflow surface
/// (signature, execution, renewal, termination) is deferred to follow-up work.
/// </summary>
public sealed record Lease
{
    /// <summary>Unique identifier for this lease.</summary>
    public required LeaseId Id { get; init; }

    /// <summary>The unit covered by this lease.</summary>
    public required EntityId UnitId { get; init; }

    /// <summary>All tenant parties on this lease.</summary>
    public required IReadOnlyList<PartyId> Tenants { get; init; }

    /// <summary>The landlord party for this lease.</summary>
    public required PartyId Landlord { get; init; }

    /// <summary>Date the lease term begins (inclusive).</summary>
    public required DateOnly StartDate { get; init; }

    /// <summary>Date the lease term ends (inclusive).</summary>
    public required DateOnly EndDate { get; init; }

    /// <summary>Monthly rent amount in the base currency.</summary>
    public required decimal MonthlyRent { get; init; }

    /// <summary>Current lifecycle phase of the lease.</summary>
    public required LeasePhase Phase { get; init; }

    /// <summary>RBAC role bindings (W#27 Phase 4); references <see cref="LeasePartyRole"/> entries owned by this lease. Defaults to empty when role distinctions are not yet captured.</summary>
    public IReadOnlyList<LeasePartyRoleId> PartyRoles { get; init; } = Array.Empty<LeasePartyRoleId>();

    /// <summary>Append-only document-revision references (W#27 Phase 2). The latest version is the one signers must sign for the AwaitingSignature → Executed transition.</summary>
    public IReadOnlyList<LeaseDocumentVersionId> DocumentVersions { get; init; } = Array.Empty<LeaseDocumentVersionId>();

    /// <summary>Per-party signatures collected on document revisions (W#27 Phase 3). Empty list until parties begin signing.</summary>
    public IReadOnlyList<LeasePartySignature> PartySignatures { get; init; } = Array.Empty<LeasePartySignature>();

    /// <summary>Reference to the landlord's attestation signature (per ADR 0054). Distinct from per-party signatures; required for AwaitingSignature → Executed transition.</summary>
    public SignatureEventId? LandlordAttestation { get; init; }
}
