using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Catalog.ExtensionFields.Audit;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Extensibility;
using Sunfish.Foundation.FeatureManagement;
using Sunfish.Foundation.Migration;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.Catalog.ExtensionFields;

/// <summary>
/// Default in-memory implementation of <see cref="IExtensionFieldCatalog"/>.
/// Safe for concurrent reads after startup registration. The async overload
/// adds feature-gate evaluation, audit emission, sequestration, and
/// capability-graph-gated redaction per ADR 0075.
/// </summary>
public sealed class ExtensionFieldCatalog : IExtensionFieldCatalog
{
    /// <summary>
    /// Default <c>nodeId</c> used when no explicit value is supplied. Halt-C
    /// fallback per hand-off — <c>INodeIdProvider</c> is not on origin/main;
    /// hosts SHOULD pass their own node identifier (Anchor: hardware install
    /// ID; Bridge: tenant-scoped node-id) to preserve cross-node provenance
    /// in audit and sequestration records.
    /// </summary>
    public const string DefaultNodeId = "local";

    private static readonly CapabilityAction RedactExtensionFieldAction = new("redact-extension-field");

    private readonly ConcurrentDictionary<Type, List<ExtensionFieldSpec>> _byEntity = new();
    private readonly IFeatureEvaluator? _featureEvaluator;
    private readonly IAuditTrail? _auditTrail;
    private readonly ISequestrationStore? _sequestrationStore;
    private readonly ICapabilityGraph? _capabilityGraph;
    private readonly IOperationSigner? _signer;
    private readonly TimeProvider _clock;
    private readonly string _nodeId;

    /// <summary>
    /// Backward-compatible constructor — registers a catalog with no
    /// feature-gate wiring. <see cref="GetFieldsAsync"/> in this mode behaves
    /// as if all specs were Ungated.
    /// </summary>
    public ExtensionFieldCatalog() : this(null, null, null, null, null, null) { }

    /// <summary>
    /// Creates a catalog with optional feature-gate, audit, sequestration,
    /// and capability dependencies. Null dependencies mean the corresponding
    /// path is skipped (Hide policy without audit; Sequester / Redact paths
    /// throw when their store / graph is null and a gated-OFF spec is hit).
    /// Per ADR 0075 §Lazy-DI optionality.
    /// </summary>
    /// <param name="featureEvaluator">Optional feature evaluator. Null disables gating; all specs return as <see cref="GateState.Ungated"/>.</param>
    /// <param name="auditTrail">Optional audit trail. Null skips audit emission silently.</param>
    /// <param name="sequestrationStore">Required for <see cref="FeatureGateOffPolicy.Sequester"/>; null throws at evaluation time.</param>
    /// <param name="capabilityGraph">Required for <see cref="FeatureGateOffPolicy.Redact"/>; null throws at evaluation time.</param>
    /// <param name="signer">Required for audit signing AND for <see cref="FeatureGateOffPolicy.Redact"/> capability lookup; null skips audit silently and throws on Redact policy.</param>
    /// <param name="clock">Time source for audit timestamps + capability-graph as-of queries. Defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="nodeId">Identifier of the host emitting sequester / audit records. Hosts SHOULD pass an Anchor install ID, Bridge tenant-scoped node-id, etc. Defaults to <see cref="DefaultNodeId"/> (<c>"local"</c>) when omitted.</param>
    public ExtensionFieldCatalog(
        IFeatureEvaluator? featureEvaluator,
        IAuditTrail? auditTrail,
        ISequestrationStore? sequestrationStore,
        ICapabilityGraph? capabilityGraph,
        IOperationSigner? signer,
        TimeProvider? clock,
        string nodeId = DefaultNodeId)
    {
        _featureEvaluator = featureEvaluator;
        _auditTrail = auditTrail;
        _sequestrationStore = sequestrationStore;
        _capabilityGraph = capabilityGraph;
        _signer = signer;
        _clock = clock ?? TimeProvider.System;
        _nodeId = nodeId ?? DefaultNodeId;
    }

    /// <inheritdoc />
    public void Register(Type entityType, ExtensionFieldSpec spec)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(spec);

        var list = _byEntity.GetOrAdd(entityType, _ => new List<ExtensionFieldSpec>());
        lock (list)
        {
            if (list.Any(existing => existing.Key.Equals(spec.Key)))
            {
                throw new InvalidOperationException(
                    $"Extension field '{spec.Key}' is already registered on '{entityType.Name}'.");
            }

            list.Add(spec);
        }
    }

    /// <inheritdoc />
#pragma warning disable CS0618 // Type or member is obsolete — internal sync access remains valid for non-gated callers.
    public IReadOnlyList<ExtensionFieldSpec> GetFields(Type entityType)
#pragma warning restore CS0618
    {
        ArgumentNullException.ThrowIfNull(entityType);

        if (!_byEntity.TryGetValue(entityType, out var list))
        {
            return Array.Empty<ExtensionFieldSpec>();
        }

        lock (list)
        {
            return list.ToArray();
        }
    }

    /// <inheritdoc />
    public bool TryGetField(Type entityType, ExtensionFieldKey key, [NotNullWhen(true)] out ExtensionFieldSpec? spec)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        if (_byEntity.TryGetValue(entityType, out var list))
        {
            lock (list)
            {
                spec = list.FirstOrDefault(s => s.Key.Equals(key));
                return spec is not null;
            }
        }

        spec = null;
        return false;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<MaterializedExtensionField>> GetFieldsAsync(
        Type entityType,
        FeatureEvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(context);

        var specs = GetFieldsSnapshot(entityType);
        if (specs.Count == 0) return Array.Empty<MaterializedExtensionField>();

        var output = new List<MaterializedExtensionField>(specs.Count);
        foreach (var spec in specs)
        {
            // Ungated path: no FeatureKey OR no IFeatureEvaluator wired ⇒ pass-through.
            if (spec.FeatureKey is not { } featureKey || _featureEvaluator is null)
            {
                output.Add(new MaterializedExtensionField(spec, GateState.Ungated));
                continue;
            }

            bool gatedOn;
            try
            {
                gatedOn = await _featureEvaluator.IsEnabledAsync(featureKey, context, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await EmitAuditAsync(
                    AuditEventType.ExtensionFieldGateEvaluationFailed,
                    context,
                    ExtensionFieldGateAuditPayloads.GateEvaluationFailed(
                        entityType.FullName ?? entityType.Name, spec.Key.Value, featureKey.Value, ex.Message),
                    cancellationToken).ConfigureAwait(false);
                gatedOn = false; // fail-closed
            }

            if (gatedOn)
            {
                await EmitAuditAsync(
                    AuditEventType.ExtensionFieldGated,
                    context,
                    ExtensionFieldGateAuditPayloads.Gated(
                        entityType.FullName ?? entityType.Name, spec.Key.Value, featureKey.Value),
                    cancellationToken).ConfigureAwait(false);
                output.Add(new MaterializedExtensionField(spec, GateState.GatedOn));
                continue;
            }

            // Gated OFF — apply the configured policy.
            switch (spec.FeatureGateOffPolicy)
            {
                case FeatureGateOffPolicy.Hide:
                    await EmitAuditAsync(
                        AuditEventType.ExtensionFieldFiltered,
                        context,
                        ExtensionFieldGateAuditPayloads.Filtered(
                            entityType.FullName ?? entityType.Name, spec.Key.Value, featureKey.Value),
                        cancellationToken).ConfigureAwait(false);
                    break;

                case FeatureGateOffPolicy.Sequester:
                    if (_sequestrationStore is null)
                        throw new InvalidOperationException(
                            $"FeatureGateOffPolicy.Sequester requires ISequestrationStore on the catalog (entity={entityType.FullName}, field={spec.Key.Value}).");
                    var recordId = $"extension-field#{entityType.FullName}#{spec.Key.Value}";
                    await _sequestrationStore.SequesterAsync(
                        _nodeId, recordId, SequestrationFlagKind.FeatureGateOff, cancellationToken)
                        .ConfigureAwait(false);
                    await EmitAuditAsync(
                        AuditEventType.ExtensionFieldSequestered,
                        context,
                        ExtensionFieldGateAuditPayloads.Sequestered(
                            entityType.FullName ?? entityType.Name, spec.Key.Value, featureKey.Value, _nodeId),
                        cancellationToken).ConfigureAwait(false);
                    break;

                case FeatureGateOffPolicy.Redact:
                    var granted = await AssertRedactAuthorisedAsync(entityType, spec, cancellationToken)
                        .ConfigureAwait(false);
                    await EmitAuditAsync(
                        AuditEventType.ExtensionFieldRedacted,
                        context,
                        ExtensionFieldGateAuditPayloads.Redacted(
                            entityType.FullName ?? entityType.Name, spec.Key.Value, featureKey.Value, granted),
                        cancellationToken).ConfigureAwait(false);
                    if (!granted)
                    {
                        throw new ExtensionFieldRedactionDeniedException(
                            action: RedactExtensionFieldAction.Name,
                            entityTypeFullName: entityType.FullName ?? entityType.Name,
                            fieldKey: spec.Key.Value,
                            reason: "ICapabilityGraph denied the redact-extension-field action for the configured signer.");
                    }
                    // Tombstone is data-plane concern; the catalog does not own the field's storage —
                    // P3 unit tests verify the audit + capability check; actual tombstoning is the
                    // persistence adapter's responsibility (a follow-up surface in P4.5).
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unrecognized FeatureGateOffPolicy '{spec.FeatureGateOffPolicy}' on extension field '{spec.Key.Value}' of '{entityType.FullName}'.");
            }
        }

        return output;
    }

    private List<ExtensionFieldSpec> GetFieldsSnapshot(Type entityType)
    {
        if (!_byEntity.TryGetValue(entityType, out var list))
            return new List<ExtensionFieldSpec>();
        lock (list)
        {
            return list.ToList();
        }
    }

    private async ValueTask<bool> AssertRedactAuthorisedAsync(
        Type entityType, ExtensionFieldSpec spec, CancellationToken ct)
    {
        if (_capabilityGraph is null || _signer is null)
            throw new InvalidOperationException(
                $"FeatureGateOffPolicy.Redact requires both ICapabilityGraph and IOperationSigner on the catalog (entity={entityType.FullName}, field={spec.Key.Value}).");

        var resource = new Resource($"extension-field#{entityType.FullName}#{spec.Key.Value}");
        return await _capabilityGraph.QueryAsync(
            _signer.IssuerId,
            resource,
            RedactExtensionFieldAction,
            _clock.GetUtcNow(),
            ct).ConfigureAwait(false);
    }

    private async ValueTask EmitAuditAsync(
        AuditEventType eventType,
        FeatureEvaluationContext context,
        AuditPayload payload,
        CancellationToken ct)
    {
        if (_auditTrail is null || _signer is null) return;
        if (context.TenantId is not { } tenantId) return;

        var occurredAt = _clock.GetUtcNow();
        var nonce = Guid.NewGuid();
        var signed = await _signer.SignAsync(payload, occurredAt, nonce, ct).ConfigureAwait(false);
        var record = new AuditRecord(
            AuditId: Guid.NewGuid(),
            TenantId: tenantId,
            EventType: eventType,
            OccurredAt: occurredAt,
            Payload: signed,
            AttestingSignatures: Array.Empty<AttestingSignature>());
        try
        {
            await _auditTrail.AppendAsync(record, ct).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort audit emission — propagating an audit failure into the
            // gate-evaluation hot path would deny field access on every audit
            // backend hiccup. Swallowing matches the cohort precedent
            // (see TenantKeyProviderFieldDecryptor).
        }
    }
}
