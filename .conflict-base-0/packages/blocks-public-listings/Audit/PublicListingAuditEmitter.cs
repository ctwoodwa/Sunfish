using System.Collections.Immutable;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;

namespace Sunfish.Blocks.PublicListings.Audit;

/// <summary>
/// Shared audit-emission helper for blocks-public-listings services
/// (W#28 Phase 7). Bundles <see cref="IAuditTrail"/> +
/// <see cref="IOperationSigner"/> + <see cref="TenantId"/> so each
/// service wires audit via a single ctor parameter.
/// </summary>
public sealed class PublicListingAuditEmitter
{
    private readonly IAuditTrail _auditTrail;
    private readonly IOperationSigner _signer;

    /// <summary>Tenant attribution applied to every emitted record.</summary>
    public TenantId Tenant { get; }

    /// <summary>Creates an emitter with the supplied audit-trail + signer + tenant attribution.</summary>
    public PublicListingAuditEmitter(IAuditTrail auditTrail, IOperationSigner signer, TenantId tenant)
    {
        ArgumentNullException.ThrowIfNull(auditTrail);
        ArgumentNullException.ThrowIfNull(signer);
        if (tenant == default)
        {
            throw new ArgumentException("Tenant is required for audit emission.", nameof(tenant));
        }
        _auditTrail = auditTrail;
        _signer = signer;
        Tenant = tenant;
    }

    /// <summary>Signs <paramref name="payload"/> + appends an <see cref="AuditRecord"/> tagged with <paramref name="eventType"/>.</summary>
    public async Task EmitAsync(AuditEventType eventType, AuditPayload payload, DateTimeOffset occurredAt, CancellationToken ct)
    {
        var signed = await _signer.SignAsync(payload, occurredAt, Guid.NewGuid(), ct).ConfigureAwait(false);
        var record = new AuditRecord(
            AuditId: Guid.NewGuid(),
            TenantId: Tenant,
            EventType: eventType,
            OccurredAt: occurredAt,
            Payload: signed,
            AttestingSignatures: ImmutableArray<AttestingSignature>.Empty);
        await _auditTrail.AppendAsync(record, ct).ConfigureAwait(false);
    }
}
