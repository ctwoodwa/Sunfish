using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Kernel.Audit;

/// <summary>
/// A tenant-scoped, append-only audit record. Each record describes a single
/// security- or compliance-relevant event (a recovery initiation, a capability
/// delegation, a payment authorization, etc.) and carries one or more
/// signatures from the principals attesting to the event.
/// </summary>
/// <remarks>
/// <para>
/// <b>Persisted format is <c>v0</c>.</b> Per ADR 0049 §"Trust impact" and the
/// dependency it names on ADR 0004's algorithm-agility refactor: the
/// <see cref="AttestingSignatures"/> field uses
/// <see cref="Sunfish.Foundation.Crypto.Signature"/>, which is currently
/// algorithm-locked to Ed25519 (64-byte fixed). Audit records are exactly the
/// long-retention data class that needs algorithm-agility before format
/// commitment — a 7-year-retained IRS audit record or recovery attestation
/// written today against fixed Ed25519 will need migration when PQC signatures
/// ship per ADR 0004's dual-sign window. <see cref="FormatVersion"/> is set to
/// <c>0</c> to mark this format as not-yet-stable; <c>v1</c> will introduce an
/// algorithm-tagged signature envelope and the migration path between the two.
/// </para>
/// <para>
/// <b>Signing scope.</b> The <see cref="Payload"/> field is itself a
/// <see cref="SignedOperation{T}"/> — the payload's signature authenticates the
/// event content. The <see cref="AttestingSignatures"/> list is the additional
/// multi-party attestation per ADR 0046 sub-pattern #48f (e.g., the trustee
/// quorum that attested a recovery completion).
/// <see cref="IAuditTrail.AppendAsync"/> verifies all signatures before
/// persistence; consumers of <see cref="IAuditTrail.QueryAsync"/> may treat
/// signatures as pre-validated.
/// </para>
/// </remarks>
/// <param name="AuditId">Stable identifier for this record. Distinct from the payload's nonce — the nonce prevents replay at the signing layer; the AuditId is the stable identifier the audit log indexes against.</param>
/// <param name="TenantId">The tenant this audit record is scoped to. Required (non-default) per <see cref="IMustHaveTenant"/>.</param>
/// <param name="EventType">Discriminator for the kind of event being recorded. See <see cref="AuditEventType"/>.</param>
/// <param name="OccurredAt">Wall-clock time at which the audited event occurred (not the time at which the record was written).</param>
/// <param name="Payload">The signed payload describing what happened. The payload's own signature authenticates its contents.</param>
/// <param name="AttestingSignatures">Multi-party signatures attesting to the event (e.g., trustee quorum signatures for a recovery completion). Empty for events that don't require multi-party attestation.</param>
/// <param name="FormatVersion">Persisted format version. Currently fixed at <c>0</c>; see remarks on the algorithm-agility dependency.</param>
public sealed record AuditRecord(
    Guid AuditId,
    TenantId TenantId,
    AuditEventType EventType,
    DateTimeOffset OccurredAt,
    SignedOperation<AuditPayload> Payload,
    IReadOnlyList<Signature> AttestingSignatures,
    int FormatVersion = AuditRecord.CurrentFormatVersion) : IMustHaveTenant
{
    /// <summary>
    /// The current persisted format version. <c>0</c> indicates the format is
    /// not yet considered forward-stable — see remarks on
    /// <see cref="AuditRecord"/> regarding the ADR 0004 algorithm-agility
    /// dependency.
    /// </summary>
    public const int CurrentFormatVersion = 0;
}

/// <summary>
/// Free-form payload body for an <see cref="AuditRecord"/>. Audit consumers
/// interpret the payload based on <see cref="AuditRecord.EventType"/>; the
/// audit substrate itself does not constrain payload shape.
/// </summary>
/// <param name="Body">Type-specific payload body. Treated as opaque by the audit substrate; consumers (compliance projections, retention reporters) interpret it.</param>
public sealed record AuditPayload(IReadOnlyDictionary<string, object?> Body);
