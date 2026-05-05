using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Extensibility;
using Sunfish.Foundation.FeatureManagement;

namespace Sunfish.Foundation.Catalog.ExtensionFields;

/// <summary>
/// Registry of extension fields per entity type. Persistence adapters,
/// UI renderers, and validators all consume this catalog rather than
/// discovering fields ad hoc.
/// </summary>
public interface IExtensionFieldCatalog
{
    /// <summary>Registers an extension field on the given entity type. Duplicate keys throw.</summary>
    void Register(Type entityType, ExtensionFieldSpec spec);

    /// <summary>Returns every registered spec for the entity type, in registration order.</summary>
    /// <remarks>
    /// Does NOT apply <see cref="ExtensionFieldSpec.FeatureGateOffPolicy"/>; gated specs
    /// are returned as-is regardless of evaluator state. Use
    /// <see cref="GetFieldsAsync"/> for feature-gate-aware materialization. Per ADR 0075.
    /// </remarks>
    [Obsolete("Use GetFieldsAsync which supports feature-gate evaluation. GetFields does not apply FeatureGateOffPolicy.", error: false)]
    IReadOnlyList<ExtensionFieldSpec> GetFields(Type entityType);

    /// <summary>Tries to resolve one registered spec by key.</summary>
    bool TryGetField(Type entityType, ExtensionFieldKey key, [NotNullWhen(true)] out ExtensionFieldSpec? spec);

    /// <summary>
    /// Returns the materialized field set in the given context. Each spec with a
    /// non-null <see cref="ExtensionFieldSpec.FeatureKey"/> is evaluated against
    /// <see cref="IFeatureEvaluator"/>; gated-OFF specs are filtered / sequestered /
    /// redacted per <see cref="ExtensionFieldSpec.FeatureGateOffPolicy"/>; every
    /// decision emits an audit record. Null-evaluator host ⇒ gating is skipped (all
    /// specs returned as <see cref="GateState.Ungated"/>). Per ADR 0075.
    /// </summary>
    ValueTask<IReadOnlyList<MaterializedExtensionField>> GetFieldsAsync(
        Type entityType,
        FeatureEvaluationContext context,
        CancellationToken cancellationToken = default);
}
