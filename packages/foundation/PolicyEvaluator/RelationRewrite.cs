namespace Sunfish.Foundation.PolicyEvaluator;

/// <summary>
/// The userset-rewrite algebra: an expression tree describing how the set of users who have a
/// relation on an object is computed. This is the Sunfish-internal analogue of the OpenFGA
/// rewrite grammar (direct, computed_userset, tuple_to_userset, union, intersection, difference).
/// </summary>
/// <remarks>
/// The <see cref="ReBACPolicyEvaluator"/> walks this tree recursively; each variant describes a
/// distinct evaluation strategy. See the spec §3.5 worked example for the canonical lease/inspection model.
/// </remarks>
public abstract record RelationRewrite
{
    /// <summary>Matches the relation as-is (direct tuple lookup with the <i>current</i> relation name).</summary>
    /// <remarks>Equivalent to <see cref="DirectUsers"/> without a type filter.</remarks>
    public sealed record Self : RelationRewrite;

    /// <summary>
    /// Direct tuple lookup. The <see cref="AllowedTypes"/> list documents authorial intent for
    /// the allowed user types; Phase B does not enforce this at evaluation time.
    /// </summary>
    public sealed record DirectUsers(IReadOnlyList<string> AllowedTypes) : RelationRewrite;

    /// <summary>
    /// Evaluates another relation on the <i>same</i> resource (e.g. <c>can_read ← owner</c>).
    /// </summary>
    public sealed record ComputedUserset(string Relation) : RelationRewrite;

    /// <summary>
    /// Follows a tuple indirection: find all R' such that <c>(currentResource, Tupleset, R')</c>
    /// exists in the tuple store, then evaluate <c>(subject, ComputedRelation, R')</c> for each.
    /// Models relationships like "<c>can_read ← parent#viewer</c>" in OpenFGA.
    /// </summary>
    public sealed record TupleToUserset(string Tupleset, string ComputedRelation) : RelationRewrite;

    /// <summary>Set union — permit if any child permits.</summary>
    public sealed record Union(IReadOnlyList<RelationRewrite> Children) : RelationRewrite;

    /// <summary>Set intersection — permit only if every child permits. An empty child list denies.</summary>
    public sealed record Intersection(IReadOnlyList<RelationRewrite> Children) : RelationRewrite;

    /// <summary>Set difference — permit iff <see cref="Include"/> permits and <see cref="Exclude"/> does not.</summary>
    public sealed record Exclusion(RelationRewrite Include, RelationRewrite Exclude) : RelationRewrite;
}
