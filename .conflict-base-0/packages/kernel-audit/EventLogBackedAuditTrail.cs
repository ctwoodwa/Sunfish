using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Events;

namespace Sunfish.Kernel.Audit;

/// <summary>
/// Default <see cref="IAuditTrail"/> implementation. Verifies the payload
/// signature, persists to the kernel <see cref="IEventLog"/>, and publishes to
/// the in-process <see cref="IAuditEventStream"/>. Direct parallel to how
/// <c>Sunfish.Kernel.Ledger</c>'s <c>PostingEngine</c> writes
/// <c>PostingsAppliedEvent</c>s through the same substrate.
/// </summary>
internal sealed class EventLogBackedAuditTrail : IAuditTrail
{
    private const string EventKindPrefix = "audit.";

    private readonly IEventLog _eventLog;
    private readonly IOperationVerifier _verifier;
    private readonly InMemoryAuditEventStream _stream;
    private readonly ILogger<EventLogBackedAuditTrail> _logger;

    public EventLogBackedAuditTrail(
        IEventLog eventLog,
        IOperationVerifier verifier,
        IAuditEventStream stream,
        ILogger<EventLogBackedAuditTrail>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(eventLog);
        ArgumentNullException.ThrowIfNull(verifier);
        ArgumentNullException.ThrowIfNull(stream);

        if (stream is not InMemoryAuditEventStream concreteStream)
        {
            throw new ArgumentException(
                $"EventLogBackedAuditTrail requires the {nameof(InMemoryAuditEventStream)} " +
                $"implementation of {nameof(IAuditEventStream)}; the trail publishes to that " +
                "concrete instance directly to keep replay deterministic.",
                nameof(stream));
        }

        _eventLog = eventLog;
        _verifier = verifier;
        _stream = concreteStream;
        _logger = logger ?? NullLogger<EventLogBackedAuditTrail>.Instance;
    }

    public async ValueTask AppendAsync(AuditRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (record.TenantId == default)
        {
            throw new ArgumentException(
                "AuditRecord.TenantId must be non-default; audit records are tenant-scoped.",
                nameof(record));
        }

        if (record.FormatVersion != AuditRecord.CurrentFormatVersion)
        {
            throw new ArgumentException(
                $"AuditRecord.FormatVersion {record.FormatVersion} is not supported by this " +
                $"build (current: {AuditRecord.CurrentFormatVersion}). Per ADR 0049, the audit " +
                "format is marked v0 until ADR 0004's algorithm-agility refactor lands.",
                nameof(record));
        }

        // Verify the payload's own SignedOperation envelope. Multi-party
        // attesting signatures (record.AttestingSignatures) are not
        // algorithmically verified at the kernel boundary in v0 — the contract
        // does not bind them to specific principals or to a canonical bytes
        // form. Verification of multi-party attestation is the caller's
        // responsibility (e.g., RecoveryCoordinator already verifies trustee
        // attestations via TrusteeAttestation.Verify before constructing the
        // AuditRecord). ADR 0049 §"Open questions" tracks promotion of this
        // to a kernel-tier check.
        if (!_verifier.Verify(record.Payload))
        {
            throw new AuditSignatureException(
                $"AuditRecord {record.AuditId} payload signature failed verification.");
        }

        // Persist via the kernel IEventLog. The KernelEvent payload carries the
        // AuditRecord by reference (KernelEvent's payload dictionary holds
        // typed objects); subscribers reading from IEventLog can extract it.
        // Audit records are tenant-scoped — encode the tenant in EntityId so
        // per-tenant log filters work without re-parsing the payload.
        var kernelEvent = new KernelEvent(
            Id: Sunfish.Kernel.Events.EventId.NewId(),
            EntityId: TenantEntityId(record.TenantId, record.AuditId),
            Kind: EventKindPrefix + record.EventType.Value,
            OccurredAt: record.OccurredAt,
            Payload: new Dictionary<string, object?>
            {
                ["auditRecord"] = record,
                ["formatVersion"] = record.FormatVersion,
            });

        await _eventLog.AppendAsync(kernelEvent, ct).ConfigureAwait(false);

        _stream.Publish(record);

        _logger.LogDebug(
            "Audit record {AuditId} ({EventType}) appended for tenant {TenantId}.",
            record.AuditId, record.EventType, record.TenantId);
    }

    public async IAsyncEnumerable<AuditRecord> QueryAsync(
        AuditQuery query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.TenantId == default)
        {
            throw new ArgumentException(
                "AuditQuery.TenantId must be non-default; audit queries are tenant-scoped.",
                nameof(query));
        }

        // v0: source from the in-process stream replay rather than reading the
        // event log directly. The stream is rebuilt from the event log on
        // construction (see ADR 0049 §"Layering over kernel IEventLog"); this
        // keeps query behavior consistent across in-process subscribers and
        // the QueryAsync surface. A future revision may add a paged read
        // directly from IEventLog for tenants with very large audit histories.
        foreach (var record in _stream.ReplayAll())
        {
            ct.ThrowIfCancellationRequested();
            if (Matches(record, query))
            {
                yield return record;
            }
        }
        await ValueTask.CompletedTask;
    }

    private static bool Matches(AuditRecord record, AuditQuery query)
    {
        if (record.TenantId != query.TenantId) return false;
        if (query.EventType is { } et && record.EventType != et) return false;
        if (query.OccurredAfter is { } after && record.OccurredAt < after) return false;
        if (query.OccurredBefore is { } before && record.OccurredAt > before) return false;
        if (query.IssuedBy is { } issuer && record.Payload.IssuerId != issuer) return false;
        return true;
    }

    private static EntityId TenantEntityId(TenantId tenantId, Guid auditId)
        => new(
            Scheme: "audit",
            Authority: tenantId.ToString(),
            LocalPart: auditId.ToString("D"));
}
