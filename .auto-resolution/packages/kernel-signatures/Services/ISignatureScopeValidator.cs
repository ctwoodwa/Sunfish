using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Taxonomy.Models;

namespace Sunfish.Kernel.Signatures.Services;

/// <summary>
/// Validates that every <see cref="TaxonomyClassification"/> in a
/// signature's scope resolves to an active node in
/// <c>Sunfish.Signature.Scopes@1.0.0</c> (per ADR 0054 amendment A7;
/// taxonomy seeded by W#31 PR #263). Tombstoned nodes are rejected;
/// classifications referencing a different taxonomy
/// (<see cref="TaxonomyDefinitionId"/> ≠ <c>Sunfish.Signature.Scopes</c>)
/// are also rejected.
/// </summary>
public interface ISignatureScopeValidator
{
    /// <summary>The taxonomy this validator binds to (consumers can assert + log it).</summary>
    TaxonomyDefinitionId BoundTaxonomy { get; }

    /// <summary>Returns a verdict for the supplied scope list. <see cref="ScopeValidationResult.Pass"/> only when every classification resolves to an active node in the bound taxonomy.</summary>
    Task<ScopeValidationResult> ValidateAsync(TenantId tenant, IReadOnlyList<TaxonomyClassification> scope, CancellationToken ct);
}

/// <summary>The verdict for an <see cref="ISignatureScopeValidator.ValidateAsync"/> call.</summary>
public sealed record ScopeValidationResult
{
    /// <summary>Whether every classification resolves to an active node in the bound taxonomy.</summary>
    public required bool Passed { get; init; }

    /// <summary>The first rejecting classification (when <see cref="Passed"/> is false) — for callers that surface a single-line error message.</summary>
    public TaxonomyClassification? RejectedAt { get; init; }

    /// <summary>The category of failure when <see cref="Passed"/> is false.</summary>
    public ScopeValidationFailure? FailedBecause { get; init; }

    /// <summary>Human-readable reason for audit body.</summary>
    public string? Reason { get; init; }

    /// <summary>The accept verdict.</summary>
    public static ScopeValidationResult Pass { get; } = new() { Passed = true };

    /// <summary>Builds a fail verdict.</summary>
    public static ScopeValidationResult Fail(TaxonomyClassification rejectedAt, ScopeValidationFailure failure, string reason) =>
        new() { Passed = false, RejectedAt = rejectedAt, FailedBecause = failure, Reason = reason };
}

/// <summary>Failure categories for scope validation.</summary>
public enum ScopeValidationFailure
{
    /// <summary>The supplied scope list was empty (a signature MUST have at least one scope per ADR 0054 A7).</summary>
    EmptyScope,

    /// <summary>One classification references a different taxonomy than the validator's bound taxonomy.</summary>
    OutOfTaxonomy,

    /// <summary>One classification's <see cref="TaxonomyClassification.Code"/> + version triple does not resolve to a known node.</summary>
    UnknownNode,

    /// <summary>One classification resolves but the node is tombstoned per ADR 0056 governance.</summary>
    TombstonedNode,
}
