using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Taxonomy.Models;
using Sunfish.Foundation.Taxonomy.Services;

namespace Sunfish.Kernel.Signatures.Services;

/// <summary>
/// Default <see cref="ISignatureScopeValidator"/> backed by an
/// <see cref="ITaxonomyResolver"/>. Binds to
/// <c>Sunfish.Signature.Scopes</c> by default — the canonical taxonomy
/// per ADR 0054 amendment A7. Hosts can rebind to a civilian-derived
/// taxonomy if their deployment publishes its own scope set.
/// </summary>
public sealed class InMemorySignatureScopeValidator : ISignatureScopeValidator
{
    /// <summary>The Sunfish-shipped taxonomy this validator binds to by default.</summary>
    public static readonly TaxonomyDefinitionId DefaultTaxonomy =
        new("Sunfish", "Signature", "Scopes");

    private readonly ITaxonomyResolver _resolver;

    /// <inheritdoc />
    public TaxonomyDefinitionId BoundTaxonomy { get; }

    /// <summary>Creates the validator using <see cref="DefaultTaxonomy"/> + the supplied resolver.</summary>
    public InMemorySignatureScopeValidator(ITaxonomyResolver resolver)
        : this(resolver, DefaultTaxonomy) { }

    /// <summary>Creates the validator with a custom bound taxonomy (e.g., civilian-derived).</summary>
    public InMemorySignatureScopeValidator(ITaxonomyResolver resolver, TaxonomyDefinitionId boundTaxonomy)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        _resolver = resolver;
        BoundTaxonomy = boundTaxonomy;
    }

    /// <inheritdoc />
    public async Task<ScopeValidationResult> ValidateAsync(TenantId tenant, IReadOnlyList<TaxonomyClassification> scope, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ct.ThrowIfCancellationRequested();

        if (scope.Count == 0)
        {
            return ScopeValidationResult.Fail(
                rejectedAt: null!,
                ScopeValidationFailure.EmptyScope,
                "SignatureEvent.Scope must contain at least one taxonomy classification.");
        }

        foreach (var classification in scope)
        {
            ArgumentNullException.ThrowIfNull(classification);

            if (classification.Definition != BoundTaxonomy)
            {
                return ScopeValidationResult.Fail(
                    classification,
                    ScopeValidationFailure.OutOfTaxonomy,
                    $"Classification '{classification.Code}' references taxonomy '{classification.Definition}' but the validator is bound to '{BoundTaxonomy}'.");
            }

            var node = await _resolver.ResolveAsync(tenant, classification, ct).ConfigureAwait(false);
            if (node is null)
            {
                return ScopeValidationResult.Fail(
                    classification,
                    ScopeValidationFailure.UnknownNode,
                    $"Classification '{classification.Code}@{classification.Version}' does not resolve to a known node in '{BoundTaxonomy}'.");
            }

            if (node.Status == TaxonomyNodeStatus.Tombstoned)
            {
                return ScopeValidationResult.Fail(
                    classification,
                    ScopeValidationFailure.TombstonedNode,
                    $"Classification '{classification.Code}@{classification.Version}' resolves to a tombstoned node; tombstoned scopes cannot be used for new signature captures.");
            }
        }

        return ScopeValidationResult.Pass;
    }
}
