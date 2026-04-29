using System.Collections.Concurrent;
using System.Collections.Immutable;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Taxonomy.Audit;
using Sunfish.Foundation.Taxonomy.Models;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.Taxonomy.Services;

/// <summary>
/// In-memory reference implementation of <see cref="ITaxonomyRegistry"/>.
/// Thread-safe via <see cref="ConcurrentDictionary{TKey, TValue}"/>; not
/// durable. Production hosts replace this with a SQLCipher- or Postgres-
/// backed implementation when one ships.
/// </summary>
/// <remarks>
/// Audit emission is controlled by the presence of <see cref="IAuditTrail"/> +
/// <see cref="IOperationSigner"/> in the constructor; both must be supplied
/// together. When neither is supplied (the bare-bones constructor overload),
/// audit emission is silently skipped — useful for unit tests that do not
/// assert on audit shape, and for non-tenanted host bootstrap where audit
/// signing isn't yet wired.
/// </remarks>
public sealed class InMemoryTaxonomyRegistry : ITaxonomyRegistry
{
    private readonly IAuditTrail? _auditTrail;
    private readonly IOperationSigner? _signer;

    // Composite keys: (tenant, definition-id, version) and (tenant, node-id, version).
    // node-id already carries definition-id, so the node key is naturally tenant + node + version.
    private readonly ConcurrentDictionary<(TenantId Tenant, TaxonomyDefinitionId Id, TaxonomyVersion Version), TaxonomyDefinition> _definitions = new();
    private readonly ConcurrentDictionary<(TenantId Tenant, TaxonomyNodeId NodeId, TaxonomyVersion Version), TaxonomyNode> _nodes = new();

    /// <summary>Creates the registry with audit emission disabled.</summary>
    public InMemoryTaxonomyRegistry()
    {
    }

    /// <summary>Creates the registry with audit emission wired through <paramref name="auditTrail"/> + <paramref name="signer"/>.</summary>
    public InMemoryTaxonomyRegistry(IAuditTrail auditTrail, IOperationSigner signer)
    {
        ArgumentNullException.ThrowIfNull(auditTrail);
        ArgumentNullException.ThrowIfNull(signer);
        _auditTrail = auditTrail;
        _signer = signer;
    }

    // ─────────────────────────── Definition lifecycle ───────────────────────────

    /// <inheritdoc />
    public async Task<TaxonomyDefinition> CreateAsync(
        TenantId tenantId,
        TaxonomyDefinitionId id,
        TaxonomyVersion version,
        TaxonomyGovernanceRegime governance,
        string description,
        ActorId owner,
        TaxonomyLineage? derivedFrom,
        CancellationToken ct)
    {
        ValidateTenant(tenantId);
        id.Validate();
        EnsureAuthoritativeOwner(governance, owner);

        var def = new TaxonomyDefinition
        {
            Id = id,
            Version = version,
            Governance = governance,
            Description = description,
            Owner = owner,
            PublishedAt = DateTimeOffset.UtcNow,
            DerivedFrom = derivedFrom,
        };

        if (!_definitions.TryAdd((tenantId, id, version), def))
        {
            throw new TaxonomyConflictException($"Taxonomy definition '{id}' version '{version}' already exists for tenant '{tenantId}'.");
        }

        await EmitAsync(tenantId, AuditEventType.TaxonomyDefinitionCreated, TaxonomyAuditPayloadFactory.Created(def, owner), ct).ConfigureAwait(false);
        return def;
    }

    /// <inheritdoc />
    public async Task<TaxonomyDefinition> PublishVersionAsync(
        TenantId tenantId,
        TaxonomyDefinitionId id,
        TaxonomyVersion newVersion,
        ActorId publishedBy,
        CancellationToken ct)
    {
        ValidateTenant(tenantId);

        var existing = FindLatestDefinition(tenantId, id) ?? throw new InvalidOperationException($"Taxonomy definition '{id}' has no prior version for tenant '{tenantId}'.");
        EnsureAuthoritativeOwner(existing.Governance, publishedBy);

        var newDef = existing with
        {
            Version = newVersion,
            PublishedAt = DateTimeOffset.UtcNow,
            RetiredAt = null,
        };

        if (!_definitions.TryAdd((tenantId, id, newVersion), newDef))
        {
            throw new TaxonomyConflictException($"Taxonomy definition '{id}' version '{newVersion}' already exists for tenant '{tenantId}'.");
        }

        await EmitAsync(tenantId, AuditEventType.TaxonomyVersionPublished, TaxonomyAuditPayloadFactory.VersionPublished(id, newVersion, publishedBy), ct).ConfigureAwait(false);
        return newDef;
    }

    /// <inheritdoc />
    public async Task RetireDefinitionVersionAsync(
        TenantId tenantId,
        TaxonomyDefinitionId id,
        TaxonomyVersion version,
        ActorId retiredBy,
        CancellationToken ct)
    {
        ValidateTenant(tenantId);

        var key = (tenantId, id, version);
        if (!_definitions.TryGetValue(key, out var def))
        {
            throw new InvalidOperationException($"Taxonomy definition '{id}' version '{version}' not found for tenant '{tenantId}'.");
        }
        EnsureAuthoritativeOwner(def.Governance, retiredBy);

        if (def.RetiredAt is not null)
        {
            return; // already retired
        }

        var retired = def with { RetiredAt = DateTimeOffset.UtcNow };
        _definitions[key] = retired;

        await EmitAsync(tenantId, AuditEventType.TaxonomyVersionRetired, TaxonomyAuditPayloadFactory.VersionRetired(id, version, retiredBy), ct).ConfigureAwait(false);
    }

    // ────────────────────────────── Node lifecycle ──────────────────────────────

    /// <inheritdoc />
    public async Task<TaxonomyNode> AddNodeAsync(
        TenantId tenantId,
        TaxonomyDefinitionId definition,
        TaxonomyVersion version,
        string code,
        string display,
        string description,
        string? parentCode,
        ActorId addedBy,
        CancellationToken ct)
    {
        ValidateTenant(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(display);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        if (!_definitions.TryGetValue((tenantId, definition, version), out var def))
        {
            throw new InvalidOperationException($"Taxonomy definition '{definition}' version '{version}' not found for tenant '{tenantId}'.");
        }
        EnsureAuthoritativeOwner(def.Governance, addedBy);

        var nodeId = new TaxonomyNodeId(definition, code);
        var node = new TaxonomyNode
        {
            Id = nodeId,
            DefinitionVersion = version,
            Display = display,
            Description = description,
            ParentCode = parentCode,
            Status = TaxonomyNodeStatus.Active,
            PublishedAt = DateTimeOffset.UtcNow,
        };

        if (!_nodes.TryAdd((tenantId, nodeId, version), node))
        {
            throw new TaxonomyGovernanceException($"Node code '{code}' is already present in '{definition}' version '{version}' (codes are immutable post-publish; rename via a major-version bump).");
        }

        await EmitAsync(tenantId, AuditEventType.TaxonomyNodeAdded, TaxonomyAuditPayloadFactory.NodeAdded(node, addedBy), ct).ConfigureAwait(false);
        return node;
    }

    /// <inheritdoc />
    public async Task<TaxonomyNode> ReviseDisplayAsync(
        TenantId tenantId,
        TaxonomyNodeId nodeId,
        TaxonomyVersion version,
        string newDisplay,
        string newDescription,
        string? revisionReason,
        ActorId revisedBy,
        CancellationToken ct)
    {
        ValidateTenant(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(newDisplay);
        ArgumentException.ThrowIfNullOrWhiteSpace(newDescription);

        if (!_definitions.TryGetValue((tenantId, nodeId.Definition, version), out var def))
        {
            throw new InvalidOperationException($"Taxonomy definition '{nodeId.Definition}' version '{version}' not found for tenant '{tenantId}'.");
        }
        EnsureAuthoritativeOwner(def.Governance, revisedBy);

        var key = (tenantId, nodeId, version);
        if (!_nodes.TryGetValue(key, out var node))
        {
            throw new InvalidOperationException($"Node '{nodeId}' version '{version}' not found.");
        }

        var history = node.DisplayHistoryEntries.ToList();
        history.Add(new DisplayHistory
        {
            Display = node.Display,
            Description = node.Description,
            RevisedAt = DateTimeOffset.UtcNow,
            RevisionReason = revisionReason,
        });

        var revised = node with
        {
            Display = newDisplay,
            Description = newDescription,
            DisplayHistoryEntries = history.AsReadOnly(),
        };
        _nodes[key] = revised;

        await EmitAsync(tenantId, AuditEventType.TaxonomyNodeDisplayRevised, TaxonomyAuditPayloadFactory.NodeDisplayRevised(nodeId, version, newDisplay, newDescription, revisionReason, revisedBy), ct).ConfigureAwait(false);
        return revised;
    }

    /// <inheritdoc />
    public async Task TombstoneNodeAsync(
        TenantId tenantId,
        TaxonomyNodeId nodeId,
        TaxonomyVersion version,
        string deprecationReason,
        string? successorCode,
        ActorId tombstonedBy,
        CancellationToken ct)
    {
        ValidateTenant(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(deprecationReason);

        if (!_definitions.TryGetValue((tenantId, nodeId.Definition, version), out var def))
        {
            throw new InvalidOperationException($"Taxonomy definition '{nodeId.Definition}' version '{version}' not found for tenant '{tenantId}'.");
        }
        EnsureAuthoritativeOwner(def.Governance, tombstonedBy);

        var key = (tenantId, nodeId, version);
        if (!_nodes.TryGetValue(key, out var node))
        {
            throw new InvalidOperationException($"Node '{nodeId}' version '{version}' not found.");
        }

        if (node.Status == TaxonomyNodeStatus.Tombstoned)
        {
            return; // monotonic — tombstone of tombstoned is a no-op
        }

        var tombstoned = node with
        {
            Status = TaxonomyNodeStatus.Tombstoned,
            TombstonedAt = DateTimeOffset.UtcNow,
            DeprecationReason = deprecationReason,
            SuccessorCode = successorCode,
        };
        _nodes[key] = tombstoned;

        await EmitAsync(tenantId, AuditEventType.TaxonomyNodeTombstoned, TaxonomyAuditPayloadFactory.NodeTombstoned(nodeId, version, deprecationReason, successorCode, tombstonedBy), ct).ConfigureAwait(false);
    }

    // ───────────────────────────── Lineage operations ────────────────────────────

    /// <inheritdoc />
    public async Task<TaxonomyDefinition> CloneAsync(
        TenantId tenantId,
        TaxonomyDefinitionId source,
        TaxonomyVersion sourceVersion,
        TaxonomyDefinitionId newId,
        ActorId clonedBy,
        string reason,
        CancellationToken ct)
    {
        ValidateTenant(tenantId);
        newId.Validate();

        var derived = await DeriveAsync(tenantId, source, sourceVersion, newId, TaxonomyLineageOp.Clone, clonedBy, reason, ct).ConfigureAwait(false);

        await EmitAsync(tenantId, AuditEventType.TaxonomyDefinitionCloned, TaxonomyAuditPayloadFactory.Cloned(source, sourceVersion, newId, clonedBy, reason), ct).ConfigureAwait(false);
        return derived;
    }

    /// <inheritdoc />
    public async Task<TaxonomyDefinition> ExtendAsync(
        TenantId tenantId,
        TaxonomyDefinitionId source,
        TaxonomyVersion sourceVersion,
        TaxonomyDefinitionId newId,
        ActorId extendedBy,
        string reason,
        CancellationToken ct)
    {
        ValidateTenant(tenantId);
        newId.Validate();

        var derived = await DeriveAsync(tenantId, source, sourceVersion, newId, TaxonomyLineageOp.Extend, extendedBy, reason, ct).ConfigureAwait(false);

        await EmitAsync(tenantId, AuditEventType.TaxonomyDefinitionExtended, TaxonomyAuditPayloadFactory.Extended(source, sourceVersion, newId, extendedBy, reason), ct).ConfigureAwait(false);
        return derived;
    }

    /// <inheritdoc />
    public async Task<TaxonomyDefinition> AlterAsync(
        TenantId tenantId,
        TaxonomyDefinitionId source,
        TaxonomyVersion sourceVersion,
        TaxonomyDefinitionId newId,
        ActorId alteredBy,
        string reason,
        CancellationToken ct)
    {
        ValidateTenant(tenantId);
        newId.Validate();

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new TaxonomyGovernanceException("Alter requires an explicit reason.");
        }

        var derived = await DeriveAsync(tenantId, source, sourceVersion, newId, TaxonomyLineageOp.Alter, alteredBy, reason, ct).ConfigureAwait(false);

        await EmitAsync(tenantId, AuditEventType.TaxonomyDefinitionAltered, TaxonomyAuditPayloadFactory.Altered(source, sourceVersion, newId, alteredBy, reason), ct).ConfigureAwait(false);
        return derived;
    }

    private Task<TaxonomyDefinition> DeriveAsync(
        TenantId tenantId,
        TaxonomyDefinitionId source,
        TaxonomyVersion sourceVersion,
        TaxonomyDefinitionId newId,
        TaxonomyLineageOp op,
        ActorId actor,
        string reason,
        CancellationToken ct)
    {
        if (!_definitions.TryGetValue((tenantId, source, sourceVersion), out var sourceDef))
        {
            throw new InvalidOperationException($"Source taxonomy '{source}' version '{sourceVersion}' not found for tenant '{tenantId}'.");
        }

        var lineage = new TaxonomyLineage
        {
            Operation = op,
            AncestorDefinition = source,
            AncestorVersion = sourceVersion,
            DerivedBy = actor,
            DerivedAt = DateTimeOffset.UtcNow,
            Reason = reason,
        };

        // Derived taxonomies become Civilian-regime, owned by the deriving actor; nodes are copied as-is.
        var derived = new TaxonomyDefinition
        {
            Id = newId,
            Version = TaxonomyVersion.V1_0_0,
            Governance = TaxonomyGovernanceRegime.Civilian,
            Description = sourceDef.Description,
            Owner = actor,
            PublishedAt = DateTimeOffset.UtcNow,
            DerivedFrom = lineage,
        };

        if (!_definitions.TryAdd((tenantId, newId, TaxonomyVersion.V1_0_0), derived))
        {
            throw new TaxonomyConflictException($"Derived taxonomy '{newId}' already exists for tenant '{tenantId}'.");
        }

        // Copy nodes from source (preserves audit trail of inheritance per OQ-7).
        var sourceNodeKeys = _nodes.Keys.Where(k => k.Tenant == tenantId && k.NodeId.Definition == source && k.Version == sourceVersion).ToList();
        foreach (var key in sourceNodeKeys)
        {
            if (_nodes.TryGetValue(key, out var srcNode))
            {
                var copiedNode = srcNode with
                {
                    Id = new TaxonomyNodeId(newId, srcNode.Id.Code),
                    DefinitionVersion = TaxonomyVersion.V1_0_0,
                    PublishedAt = DateTimeOffset.UtcNow,
                };
                _nodes[(tenantId, copiedNode.Id, TaxonomyVersion.V1_0_0)] = copiedNode;
            }
        }

        return Task.FromResult(derived);
    }

    // ───────────────────────────────── Reads ────────────────────────────────────

    /// <inheritdoc />
    public Task<TaxonomyDefinition?> GetDefinitionAsync(TenantId tenantId, TaxonomyDefinitionId id, TaxonomyVersion version, CancellationToken ct)
    {
        ValidateTenant(tenantId);
        _definitions.TryGetValue((tenantId, id, version), out var def);
        return Task.FromResult<TaxonomyDefinition?>(def);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TaxonomyNode>> GetNodesAsync(TenantId tenantId, TaxonomyDefinitionId definition, TaxonomyVersion version, CancellationToken ct)
    {
        ValidateTenant(tenantId);
        var result = _nodes.Values
            .Where(n => n.Id.Definition == definition && n.DefinitionVersion == version)
            .Where(n => _nodes.ContainsKey((tenantId, n.Id, version)))
            .OrderBy(n => n.PublishedAt)
            .ToList();
        return Task.FromResult<IReadOnlyList<TaxonomyNode>>(result);
    }

    /// <inheritdoc />
    public Task<TaxonomyNode?> GetNodeAsync(TenantId tenantId, TaxonomyNodeId nodeId, TaxonomyVersion version, CancellationToken ct)
    {
        ValidateTenant(tenantId);
        _nodes.TryGetValue((tenantId, nodeId, version), out var node);
        return Task.FromResult<TaxonomyNode?>(node);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TaxonomyDefinition>> ListDefinitionsAsync(TenantId tenantId, string? filterByVendor, CancellationToken ct)
    {
        ValidateTenant(tenantId);
        var query = _definitions
            .Where(kv => kv.Key.Tenant == tenantId)
            .Select(kv => kv.Value);
        if (!string.IsNullOrWhiteSpace(filterByVendor))
        {
            query = query.Where(d => d.Id.Vendor == filterByVendor);
        }
        return Task.FromResult<IReadOnlyList<TaxonomyDefinition>>(query.ToList());
    }

    // ───────────────────────────── Bootstrap ─────────────────────────────────────

    /// <inheritdoc />
    public async Task RegisterCorePackageAsync(TenantId tenantId, TaxonomyCorePackage package, CancellationToken ct)
    {
        ValidateTenant(tenantId);
        ArgumentNullException.ThrowIfNull(package);

        var defKey = (tenantId, package.Definition.Id, package.Definition.Version);

        if (_definitions.TryGetValue(defKey, out var existing))
        {
            // Idempotent re-register: identical data is a no-op; different data conflicts.
            if (DefinitionsAreEquivalent(existing, package.Definition))
            {
                return;
            }
            throw new TaxonomyConflictException($"Core package '{package.Definition.Id}' version '{package.Definition.Version}' is already registered with different data for tenant '{tenantId}'.");
        }

        if (!_definitions.TryAdd(defKey, package.Definition))
        {
            throw new TaxonomyConflictException($"Core package '{package.Definition.Id}' could not be registered (concurrent insert).");
        }

        foreach (var node in package.Nodes)
        {
            _nodes[(tenantId, node.Id, package.Definition.Version)] = node;
        }

        await EmitAsync(tenantId, AuditEventType.TaxonomyDefinitionCreated, TaxonomyAuditPayloadFactory.Created(package.Definition, package.Definition.Owner), ct).ConfigureAwait(false);
    }

    // ──────────────────────────────── Helpers ───────────────────────────────────

    private static void ValidateTenant(TenantId tenantId)
    {
        if (tenantId == default)
        {
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        }
    }

    private static void EnsureAuthoritativeOwner(TaxonomyGovernanceRegime governance, ActorId actor)
    {
        if (governance == TaxonomyGovernanceRegime.Authoritative && actor != ActorId.Sunfish)
        {
            throw new TaxonomyGovernanceException($"Authoritative-regime taxonomies may only be mutated by the Sunfish actor (got '{actor.Value}').");
        }
    }

    private TaxonomyDefinition? FindLatestDefinition(TenantId tenantId, TaxonomyDefinitionId id)
    {
        return _definitions
            .Where(kv => kv.Key.Tenant == tenantId && kv.Key.Id == id)
            .OrderByDescending(kv => kv.Key.Version.Major)
            .ThenByDescending(kv => kv.Key.Version.Minor)
            .ThenByDescending(kv => kv.Key.Version.Patch)
            .Select(kv => kv.Value)
            .FirstOrDefault();
    }

    private static bool DefinitionsAreEquivalent(TaxonomyDefinition a, TaxonomyDefinition b) =>
        a.Id == b.Id &&
        a.Version == b.Version &&
        a.Governance == b.Governance &&
        a.Description == b.Description &&
        a.Owner == b.Owner;

    private async Task EmitAsync(TenantId tenantId, AuditEventType eventType, AuditPayload payload, CancellationToken ct)
    {
        if (_auditTrail is null || _signer is null)
        {
            return;
        }

        var occurredAt = DateTimeOffset.UtcNow;
        var signed = await _signer.SignAsync(payload, occurredAt, Guid.NewGuid(), ct).ConfigureAwait(false);
        var record = new AuditRecord(
            AuditId: Guid.NewGuid(),
            TenantId: tenantId,
            EventType: eventType,
            OccurredAt: occurredAt,
            Payload: signed,
            AttestingSignatures: ImmutableArray<AttestingSignature>.Empty);
        await _auditTrail.AppendAsync(record, ct).ConfigureAwait(false);
    }

    // ──────────────────────────── Internal helpers for resolver ──────────────────

    internal IReadOnlyDictionary<(TenantId, TaxonomyNodeId, TaxonomyVersion), TaxonomyNode> NodesSnapshot => _nodes;
}
