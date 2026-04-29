using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Taxonomy.Models;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.Taxonomy.Audit;

/// <summary>
/// Builds <see cref="AuditPayload"/> bodies for the 9 taxonomy lifecycle
/// events (ADR 0056 + ADR 0049). The caller signs the payload via
/// <see cref="Sunfish.Foundation.Crypto.IOperationSigner"/> and constructs
/// the <see cref="AuditRecord"/>.
/// </summary>
internal static class TaxonomyAuditPayloadFactory
{
    /// <summary>Body for <see cref="AuditEventType.TaxonomyDefinitionCreated"/>.</summary>
    public static AuditPayload Created(TaxonomyDefinition def, ActorId owner) =>
        new(new Dictionary<string, object?>
        {
            ["definition_id"] = def.Id.Value,
            ["version"] = def.Version.ToString(),
            ["regime"] = def.Governance.ToString(),
            ["owner"] = owner.Value,
            ["lineage_op"] = def.DerivedFrom?.Operation.ToString() ?? TaxonomyLineageOp.InitialPublication.ToString(),
        });

    /// <summary>Body for <see cref="AuditEventType.TaxonomyVersionPublished"/>.</summary>
    public static AuditPayload VersionPublished(TaxonomyDefinitionId id, TaxonomyVersion version, ActorId publishedBy) =>
        new(new Dictionary<string, object?>
        {
            ["definition_id"] = id.Value,
            ["version"] = version.ToString(),
            ["published_by"] = publishedBy.Value,
        });

    /// <summary>Body for <see cref="AuditEventType.TaxonomyVersionRetired"/>.</summary>
    public static AuditPayload VersionRetired(TaxonomyDefinitionId id, TaxonomyVersion version, ActorId retiredBy) =>
        new(new Dictionary<string, object?>
        {
            ["definition_id"] = id.Value,
            ["version"] = version.ToString(),
            ["retired_by"] = retiredBy.Value,
        });

    /// <summary>Body for <see cref="AuditEventType.TaxonomyNodeAdded"/>.</summary>
    public static AuditPayload NodeAdded(TaxonomyNode node, ActorId addedBy) =>
        new(new Dictionary<string, object?>
        {
            ["definition_id"] = node.Id.Definition.Value,
            ["version"] = node.DefinitionVersion.ToString(),
            ["code"] = node.Id.Code,
            ["parent_code"] = node.ParentCode,
            ["added_by"] = addedBy.Value,
        });

    /// <summary>Body for <see cref="AuditEventType.TaxonomyNodeDisplayRevised"/>.</summary>
    public static AuditPayload NodeDisplayRevised(TaxonomyNodeId nodeId, TaxonomyVersion version, string newDisplay, string newDescription, string? revisionReason, ActorId revisedBy) =>
        new(new Dictionary<string, object?>
        {
            ["definition_id"] = nodeId.Definition.Value,
            ["version"] = version.ToString(),
            ["code"] = nodeId.Code,
            ["new_display"] = newDisplay,
            ["new_description"] = newDescription,
            ["revision_reason"] = revisionReason,
            ["revised_by"] = revisedBy.Value,
        });

    /// <summary>Body for <see cref="AuditEventType.TaxonomyNodeTombstoned"/>.</summary>
    public static AuditPayload NodeTombstoned(TaxonomyNodeId nodeId, TaxonomyVersion version, string deprecationReason, string? successorCode, ActorId tombstonedBy) =>
        new(new Dictionary<string, object?>
        {
            ["definition_id"] = nodeId.Definition.Value,
            ["version"] = version.ToString(),
            ["code"] = nodeId.Code,
            ["deprecation_reason"] = deprecationReason,
            ["successor_code"] = successorCode,
            ["tombstoned_by"] = tombstonedBy.Value,
        });

    /// <summary>Body for <see cref="AuditEventType.TaxonomyDefinitionCloned"/>.</summary>
    public static AuditPayload Cloned(TaxonomyDefinitionId source, TaxonomyVersion sourceVersion, TaxonomyDefinitionId newId, ActorId clonedBy, string reason) =>
        new(new Dictionary<string, object?>
        {
            ["source_definition_id"] = source.Value,
            ["source_version"] = sourceVersion.ToString(),
            ["new_definition_id"] = newId.Value,
            ["cloned_by"] = clonedBy.Value,
            ["reason"] = reason,
        });

    /// <summary>Body for <see cref="AuditEventType.TaxonomyDefinitionExtended"/>.</summary>
    public static AuditPayload Extended(TaxonomyDefinitionId source, TaxonomyVersion sourceVersion, TaxonomyDefinitionId newId, ActorId extendedBy, string reason) =>
        new(new Dictionary<string, object?>
        {
            ["source_definition_id"] = source.Value,
            ["source_version"] = sourceVersion.ToString(),
            ["new_definition_id"] = newId.Value,
            ["extended_by"] = extendedBy.Value,
            ["reason"] = reason,
        });

    /// <summary>Body for <see cref="AuditEventType.TaxonomyDefinitionAltered"/>.</summary>
    public static AuditPayload Altered(TaxonomyDefinitionId source, TaxonomyVersion sourceVersion, TaxonomyDefinitionId newId, ActorId alteredBy, string reason) =>
        new(new Dictionary<string, object?>
        {
            ["source_definition_id"] = source.Value,
            ["source_version"] = sourceVersion.ToString(),
            ["new_definition_id"] = newId.Value,
            ["altered_by"] = alteredBy.Value,
            ["reason"] = reason,
        });
}
