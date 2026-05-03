namespace Sunfish.Kernel.Audit;

/// <summary>
/// Append-only domain-typed audit trail. The kernel-tier substrate that ADR 0046
/// sub-pattern #48f's recovery audit trail, future capability-delegation audits,
/// and the Phase 2 commercial-scope payment / bookkeeper / tax-advisor /
/// IRS-export audits all consume.
/// </summary>
/// <remarks>
/// <para>
/// <b>Layering.</b> Per ADR 0049, <c>IAuditTrail</c> is parallel to
/// <c>Kernel.Ledger</c> — own contracts, own typed event stream
/// (<see cref="IAuditEventStream"/>), kernel <c>IEventLog</c> as the durability
/// substrate. Other consumers MAY read audit records via this contract; they
/// MUST NOT bypass it and read directly from the event log. The contract is
/// the access path; the substrate is implementation detail.
/// </para>
/// <para>
/// <b>Append-only.</b> The contract has no update or delete. GDPR Article 17
/// erasure semantics are deferred to a future <c>IComplianceQuery</c> surface
/// (per ADR 0049 §"Open questions") that decides whether a given record can be
/// erased — the substrate itself stays append-only and the compliance layer
/// decides what's visible to which caller.
/// </para>
/// <para>
/// <b>Signature verification is hybrid in v0.</b>
/// <see cref="AppendAsync"/> verifies the payload's
/// <see cref="Sunfish.Foundation.Crypto.SignedOperation{T}"/> envelope
/// (single-issuer Ed25519) and rejects records with invalid envelope
/// signatures via <see cref="AuditSignatureException"/>. The multi-party
/// <see cref="AuditRecord.AttestingSignatures"/> are NOT algorithmically
/// verified at the kernel boundary in v0 — verification of attestations
/// is the producer's responsibility (e.g., RecoveryCoordinator already
/// verifies trustee attestations via TrusteeAttestation.Verify before
/// constructing the AuditRecord). ADR 0049 §"Open questions" tracks
/// promotion of attestation verification to a kernel-tier check.
/// </para>
/// </remarks>
public interface IAuditTrail
{
    /// <summary>
    /// Append a new audit record. Verifies the payload's
    /// <see cref="Sunfish.Foundation.Crypto.SignedOperation{T}"/> envelope,
    /// persists the record to the kernel <c>IEventLog</c>, and publishes it
    /// on the in-process <see cref="IAuditEventStream"/>. Multi-party
    /// <see cref="AuditRecord.AttestingSignatures"/> are stored as-supplied
    /// without kernel-tier verification in v0 — see interface remarks.
    /// </summary>
    /// <param name="record">The record to append. Must have a non-default <see cref="AuditRecord.TenantId"/> per <see cref="Sunfish.Foundation.MultiTenancy.IMustHaveTenant"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="AuditSignatureException">Thrown if the payload's <c>SignedOperation</c> envelope fails verification.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="record"/> has a default <see cref="AuditRecord.TenantId"/>.</exception>
    ValueTask AppendAsync(AuditRecord record, CancellationToken ct = default);

    /// <summary>
    /// Stream every audit record that matches the query, in append order.
    /// </summary>
    /// <param name="query">Filter. <see cref="AuditQuery.TenantId"/> is mandatory — audit reads are tenant-scoped.</param>
    /// <param name="ct">Cancellation token. Cancelling ends enumeration cleanly.</param>
    IAsyncEnumerable<AuditRecord> QueryAsync(AuditQuery query, CancellationToken ct = default);
}

/// <summary>
/// Thrown by <see cref="IAuditTrail.AppendAsync"/> when one or more signatures
/// on the record fail verification.
/// </summary>
public sealed class AuditSignatureException : Exception
{
    /// <inheritdoc />
    public AuditSignatureException(string message) : base(message) { }

    /// <inheritdoc />
    public AuditSignatureException(string message, Exception inner) : base(message, inner) { }
}
