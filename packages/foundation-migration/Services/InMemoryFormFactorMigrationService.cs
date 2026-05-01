using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Migration.Audit;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.Migration;

/// <summary>
/// Reference <see cref="IFormFactorMigrationService"/> per ADR 0028-A5.
/// Phase 2 shipped <see cref="ComputeDerivedSurfaceAsync"/>; Phase 3
/// wires <see cref="ApplyMigrationAsync"/> + the sequestration-store
/// transitions per A5.4 (rules 1–3) + A8.3 (rules 5–7) + A8.5 (rule 6
/// field-level write authorization).
/// </summary>
public sealed class InMemoryFormFactorMigrationService : IFormFactorMigrationService
{
    /// <summary>Default A8.7 dedup window for <see cref="AuditEventType.AdapterRollbackDetected"/>.</summary>
    public static readonly TimeSpan DefaultAdapterRollbackDedupWindow = TimeSpan.FromHours(6);

    private readonly ISequestrationStore? _store;
    private readonly IAuditTrail? _auditTrail;
    private readonly IOperationSigner? _signer;
    private readonly TenantId _tenantId;
    private readonly TimeProvider _time;
    private readonly TimeSpan _rollbackDedupWindow;

    private readonly ConcurrentDictionary<RollbackDedupKey, DateTimeOffset> _rollbackLastSeen = new();

    /// <summary>Phase-2 overload — sequestration store not wired; <see cref="ApplyMigrationAsync"/> throws.</summary>
    public InMemoryFormFactorMigrationService()
    {
        _store = null;
        _auditTrail = null;
        _signer = null;
        _tenantId = default;
        _time = TimeProvider.System;
        _rollbackDedupWindow = DefaultAdapterRollbackDedupWindow;
    }

    /// <summary>Phase-3 overload — wires the sequestration store; audit emission is disabled.</summary>
    public InMemoryFormFactorMigrationService(ISequestrationStore sequestrationStore, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(sequestrationStore);
        _store = sequestrationStore;
        _auditTrail = null;
        _signer = null;
        _tenantId = default;
        _time = time ?? TimeProvider.System;
        _rollbackDedupWindow = DefaultAdapterRollbackDedupWindow;
    }

    /// <summary>
    /// Phase-4 overload — wires the sequestration store + audit emission.
    /// W#32 both-or-neither contract: audit trail + signer + tenant id
    /// required together. Emits the 5 sequestration-related events
    /// (PlaintextSequestered / CiphertextSequestered / DataReleased /
    /// FormFactorQuorumIneligible) on every <see cref="ApplyMigrationAsync"/>
    /// transition + <see cref="AuditEventType.HardwareTierChanged"/> at
    /// the start of every call. <see cref="AuditEventType.AdapterRollbackDetected"/>
    /// is dedup'd 1-per-(node_id, adapter_id, version_pair) per a 6-hour
    /// rolling window per A8.7.
    /// </summary>
    public InMemoryFormFactorMigrationService(
        ISequestrationStore sequestrationStore,
        IAuditTrail auditTrail,
        IOperationSigner signer,
        TenantId tenantId,
        TimeProvider? time = null,
        TimeSpan? adapterRollbackDedupWindow = null)
    {
        ArgumentNullException.ThrowIfNull(sequestrationStore);
        ArgumentNullException.ThrowIfNull(auditTrail);
        ArgumentNullException.ThrowIfNull(signer);
        if (tenantId == default)
        {
            throw new ArgumentException("tenantId is required when audit emission is wired.", nameof(tenantId));
        }
        _store = sequestrationStore;
        _auditTrail = auditTrail;
        _signer = signer;
        _tenantId = tenantId;
        _time = time ?? TimeProvider.System;
        _rollbackDedupWindow = adapterRollbackDedupWindow ?? DefaultAdapterRollbackDedupWindow;
    }

    /// <inheritdoc />
    public ValueTask<DerivedSurface> ComputeDerivedSurfaceAsync(
        FormFactorProfile profile,
        IReadOnlySet<string> workspaceDeclaredCapabilities,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(workspaceDeclaredCapabilities);
        ct.ThrowIfCancellationRequested();

        var hostCapabilities = DeriveHostCapabilities(profile);
        var declared = new HashSet<string>(workspaceDeclaredCapabilities, StringComparer.Ordinal);
        var included = new HashSet<string>(declared, StringComparer.Ordinal);
        included.IntersectWith(hostCapabilities);
        var excluded = new HashSet<string>(declared, StringComparer.Ordinal);
        excluded.ExceptWith(hostCapabilities);

        return ValueTask.FromResult(new DerivedSurface
        {
            FormFactor = profile.FormFactor,
            WorkspaceDeclaredCapabilities = declared,
            IncludedCapabilities = included,
            ExcludedCapabilities = excluded,
        });
    }

    /// <inheritdoc />
    public async ValueTask ApplyMigrationAsync(HardwareTierChangeEvent change, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (_store is null)
        {
            throw new InvalidOperationException(
                "ApplyMigrationAsync requires a sequestration store. Use the constructor that takes ISequestrationStore.");
        }

        await EmitAsync(
            AuditEventType.HardwareTierChanged,
            MigrationAuditPayloads.HardwareTierChanged(
                change.NodeId,
                change.PreviousProfile.FormFactor,
                change.CurrentProfile.FormFactor,
                change.TriggeringEvent),
            ct).ConfigureAwait(false);

        var hostCapabilities = DeriveHostCapabilities(change.CurrentProfile);
        var entries = await _store.GetByNodeAsync(change.NodeId, ct).ConfigureAwait(false);
        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            var supported = hostCapabilities.Contains(entry.RequiredCapability);
            if (supported)
            {
                // A5.4 rule 2 — re-emergence on surface expansion.
                if (entry.Flag is not null)
                {
                    await _store.ReleaseAsync(entry.NodeId, entry.RecordId, ct).ConfigureAwait(false);
                    await EmitAsync(
                        AuditEventType.DataReleased,
                        MigrationAuditPayloads.DataReleased(entry.NodeId, entry.RecordId, entry.RequiredCapability),
                        ct).ConfigureAwait(false);
                }
                continue;
            }

            // A5.4 rule 1 — sequestration over deletion.
            // A8.3 rule 5 — plaintext-vs-ciphertext distinction.
            // A8.3 rule 7 — field-level redaction default; record-level
            // sequestration only when the primary-key / display fields
            // are themselves encrypted.
            var flag = ClassifySequestrationFlag(entry, change.TriggeringEvent);
            await _store.SequesterAsync(entry.NodeId, entry.RecordId, flag, ct).ConfigureAwait(false);

            var auditEvent = flag switch
            {
                SequestrationFlagKind.PlaintextSequestered => AuditEventType.PlaintextSequestered,
                SequestrationFlagKind.CiphertextSequestered => AuditEventType.CiphertextSequestered,
                SequestrationFlagKind.FormFactorQuorumIneligible => AuditEventType.FormFactorQuorumIneligible,
                _ => AuditEventType.PlaintextSequestered, // StorageBudgetExceeded + FormFactorFilteredOut both use PlaintextSequestered as the audit event type per A8.3 rule 5
            };
            await EmitAsync(
                auditEvent,
                MigrationAuditPayloads.Sequestered(entry.NodeId, entry.RecordId, entry.RequiredCapability, flag),
                ct).ConfigureAwait(false);
        }

        if (change.TriggeringEvent == TriggeringEventKind.AdapterDowngrade)
        {
            await TryEmitAdapterRollbackAsync(change, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// A8.3 rule 6 — CP-record quorum eligibility check. A host is
    /// quorum-ineligible for a CP-class record if the record is
    /// sequestered (the host can't read it, so its vote is ignored).
    /// </summary>
    public async ValueTask<bool> IsQuorumEligibleAsync(string nodeId, string recordId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeId);
        ArgumentException.ThrowIfNullOrEmpty(recordId);
        if (_store is null) return true; // no store wired → vacuously eligible
        var entries = await _store.GetByNodeAsync(nodeId, ct).ConfigureAwait(false);
        foreach (var entry in entries)
        {
            if (entry.RecordId == recordId)
            {
                if (!entry.IsCpClass) return true;
                return entry.Flag is null;
            }
        }
        return true; // unknown record → vacuously eligible (caller hasn't registered it)
    }

    /// <summary>
    /// A8.5 rule 6 — field-level write authorization. A field is
    /// write-sequestered iff it is read-sequestered (because the host
    /// lacks the per-tenant key for that field's encryption surface).
    /// Write attempts to a write-sequestered field should be rejected
    /// at the consumer's CRDT-write boundary; this method exposes the
    /// gate. When audit emission is wired, every write-sequestered call
    /// emits <see cref="AuditEventType.FieldWriteSequestered"/>.
    /// </summary>
    public async ValueTask<bool> CanWriteFieldAsync(string nodeId, string fieldEntryId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(nodeId);
        ArgumentException.ThrowIfNullOrEmpty(fieldEntryId);
        if (_store is null) return true;
        var entries = await _store.GetByNodeAsync(nodeId, ct).ConfigureAwait(false);
        foreach (var entry in entries)
        {
            if (entry.RecordId == fieldEntryId)
            {
                if (entry.Flag is not null)
                {
                    await EmitAsync(
                        AuditEventType.FieldWriteSequestered,
                        MigrationAuditPayloads.FieldWriteSequestered(nodeId, fieldEntryId, entry.RequiredCapability),
                        ct).ConfigureAwait(false);
                    return false;
                }
                return true;
            }
        }
        return true;
    }

    private async ValueTask TryEmitAdapterRollbackAsync(HardwareTierChangeEvent change, CancellationToken ct)
    {
        // Per A8.7: dedup 1-per-(node_id, adapter_id, version_pair) per
        // 6-hour rolling window. The substrate doesn't yet model
        // adapter id / version pair as event fields — treat the
        // (node_id, current_form_factor, previous_form_factor) tuple as
        // the dedup key for v0; future amendments augment.
        var key = new RollbackDedupKey(
            change.NodeId,
            change.PreviousProfile.FormFactor.ToString(),
            change.CurrentProfile.FormFactor.ToString());
        var now = _time.GetUtcNow();
        if (_rollbackLastSeen.TryGetValue(key, out var lastSeen) && now - lastSeen < _rollbackDedupWindow)
        {
            return;
        }
        _rollbackLastSeen[key] = now;

        await EmitAsync(
            AuditEventType.AdapterRollbackDetected,
            MigrationAuditPayloads.AdapterRollbackDetected(
                change.NodeId,
                adapterId: $"formFactor.{change.CurrentProfile.FormFactor}",
                previousVersion: change.PreviousProfile.FormFactor.ToString(),
                currentVersion: change.CurrentProfile.FormFactor.ToString()),
            ct).ConfigureAwait(false);
    }

    private async ValueTask EmitAsync(AuditEventType eventType, AuditPayload payload, CancellationToken ct)
    {
        if (_auditTrail is null || _signer is null)
        {
            return;
        }
        var occurredAt = _time.GetUtcNow();
        var signed = await _signer.SignAsync(payload, occurredAt, Guid.NewGuid(), ct).ConfigureAwait(false);
        var record = new AuditRecord(
            AuditId: Guid.NewGuid(),
            TenantId: _tenantId,
            EventType: eventType,
            OccurredAt: occurredAt,
            Payload: signed,
            AttestingSignatures: ImmutableArray<AttestingSignature>.Empty);
        await _auditTrail.AppendAsync(record, ct).ConfigureAwait(false);
    }

    private readonly record struct RollbackDedupKey(string NodeId, string PreviousFormFactor, string CurrentFormFactor);

    private static SequestrationFlagKind ClassifySequestrationFlag(SequesteredRecord entry, TriggeringEventKind triggeringEvent)
    {
        // Storage-budget triggers always emit StorageBudgetExceeded —
        // the data hasn't lost capability, the host's storage shrunk.
        if (triggeringEvent == TriggeringEventKind.StorageBudgetChanged)
        {
            return SequestrationFlagKind.StorageBudgetExceeded;
        }

        // A8.3 rule 6 — CP-class records get the quorum flag in
        // addition to (logically) the underlying sequestration reason.
        if (entry.IsCpClass)
        {
            return SequestrationFlagKind.FormFactorQuorumIneligible;
        }

        // A8.3 rule 5 — encrypted records that the host can't decrypt
        // are CiphertextSequestered; un-encrypted records that the
        // host's UI / capability surface excludes are PlaintextSequestered.
        if (entry.IsEncrypted)
        {
            return SequestrationFlagKind.CiphertextSequestered;
        }
        return SequestrationFlagKind.PlaintextSequestered;
    }

    /// <summary>
    /// Derives the capability tag set from a <see cref="FormFactorProfile"/>.
    /// Tags are namespaced (<c>input.*</c>, <c>display.*</c>, <c>network.*</c>,
    /// <c>power.*</c>, <c>sensor.*</c>, <c>formFactor.*</c>) so workspaces can
    /// scope their declared requirements to the relevant namespace. The set
    /// is order-stable across calls (`StringComparer.Ordinal`) so canonical-
    /// JSON encoding produces signature-stable output.
    /// </summary>
    public static HashSet<string> DeriveHostCapabilities(FormFactorProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var caps = new HashSet<string>(StringComparer.Ordinal)
        {
            $"formFactor.{profile.FormFactor}",
            $"display.{profile.DisplayClass}",
            $"network.{profile.NetworkPosture}",
            $"power.{profile.PowerProfile}",
            $"instanceClass.{profile.InstanceClass}",
            $"storage.budgetMb={profile.StorageBudgetMb}",
        };
        foreach (var modality in profile.InputModalities)
        {
            caps.Add($"input.{modality}");
        }
        foreach (var sensor in profile.SensorSurface)
        {
            caps.Add($"sensor.{sensor}");
        }
        return caps;
    }
}
