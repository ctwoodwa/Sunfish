using System.Collections.Immutable;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;

namespace Sunfish.Kernel.Signatures.Services;

/// <summary>
/// Shared audit-emission helper used by the InMemory implementations of
/// <see cref="IConsentRegistry"/>, <see cref="ISignatureCapture"/>, and
/// <see cref="ISignatureRevocationLog"/>. Bundles
/// <see cref="IAuditTrail"/> + <see cref="IOperationSigner"/> +
/// <see cref="TenantId"/> so each service can wire audit emission via
/// a single constructor parameter instead of three.
/// </summary>
public sealed class SignatureAuditEmitter
{
    private readonly IAuditTrail _auditTrail;
    private readonly IOperationSigner _signer;

    /// <summary>Tenant attribution applied to every emitted record.</summary>
    public TenantId Tenant { get; }

    /// <summary>Creates an emitter with the supplied audit-trail + signer + tenant attribution.</summary>
    public SignatureAuditEmitter(IAuditTrail auditTrail, IOperationSigner signer, TenantId tenant)
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
