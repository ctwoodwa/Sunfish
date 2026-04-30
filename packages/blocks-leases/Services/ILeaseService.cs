using Sunfish.Blocks.Leases.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Kernel.Signatures.Models;

namespace Sunfish.Blocks.Leases.Services;

/// <summary>
/// Contract for managing lease records.
/// Implementations may be in-memory (for testing/demo) or persistence-backed (production).
/// DocuSign integration and full §6.1 workflow surface are deferred to follow-up passes.
/// </summary>
public interface ILeaseService
{
    /// <summary>
    /// Creates a new lease from <paramref name="request"/> and returns the persisted record.
    /// The new lease is always created in <see cref="LeasePhase.Draft"/>.
    /// </summary>
    /// <param name="request">The creation payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created <see cref="Lease"/> with an assigned <see cref="LeaseId"/>.</returns>
    ValueTask<Lease> CreateAsync(CreateLeaseRequest request, CancellationToken ct = default);

    /// <summary>
    /// Returns the lease with the specified <paramref name="id"/>, or <see langword="null"/>
    /// if no such lease exists.
    /// </summary>
    /// <param name="id">The lease identifier to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<Lease?> GetAsync(LeaseId id, CancellationToken ct = default);

    /// <summary>
    /// Streams all leases matching <paramref name="query"/>.
    /// Pass <see cref="ListLeasesQuery.Empty"/> to return all leases.
    /// </summary>
    /// <param name="query">Optional filter criteria.</param>
    /// <param name="ct">Cancellation token.</param>
    IAsyncEnumerable<Lease> ListAsync(ListLeasesQuery query, CancellationToken ct = default);

    /// <summary>
    /// Transitions a lease to <paramref name="newPhase"/>. Allowed transitions per W#27
    /// Phase 1 (consumes the public <c>TransitionTable&lt;LeasePhase&gt;</c> primitive
    /// from <c>blocks-maintenance</c>; ADR 0053 amendment A5).
    /// </summary>
    /// <param name="id">The lease to transition.</param>
    /// <param name="newPhase">Target phase.</param>
    /// <param name="actor">Actor performing the transition.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the transition is not allowed from the current phase.</exception>
    ValueTask<Lease> TransitionPhaseAsync(LeaseId id, LeasePhase newPhase, ActorId actor, CancellationToken ct = default);

    /// <summary>
    /// W#27 Phase 2: appends a document revision to the lease's
    /// append-only revision log. Returns the persisted lease with the
    /// new <see cref="Models.LeaseDocumentVersionId"/> appended to
    /// <see cref="Lease.DocumentVersions"/>.
    /// </summary>
    ValueTask<Lease> AppendDocumentVersionAsync(LeaseId id, LeaseDocumentVersion revision, ActorId actor, CancellationToken ct = default);

    /// <summary>
    /// W#27 Phase 3: records one party's signature on the latest
    /// document revision of the lease. The party MUST be in
    /// <see cref="Lease.Tenants"/>; the document version MUST be the
    /// latest. Returns the persisted lease with the new signature
    /// appended to <see cref="Lease.PartySignatures"/>.
    /// </summary>
    ValueTask<Lease> RecordPartySignatureAsync(LeaseId id, PartyId party, SignatureEventId signatureEvent, ActorId actor, CancellationToken ct = default);

    /// <summary>
    /// W#27 Phase 3: sets the landlord's attestation signature for the
    /// lease (per ADR 0054). Distinct from per-party signatures;
    /// required for the AwaitingSignature → Executed transition.
    /// </summary>
    ValueTask<Lease> SetLandlordAttestationAsync(LeaseId id, SignatureEventId signatureEvent, ActorId actor, CancellationToken ct = default);
}
