namespace Sunfish.Foundation.PolicyEvaluator;

/// <summary>
/// Fluent builder for a single <see cref="RelationRewrite"/>. Each factory method sets the
/// builder's root and returns <c>this</c>; the last call wins if multiple are made.
/// </summary>
public sealed class RelationBuilder
{
    private RelationRewrite? _root;

    /// <summary>
    /// Direct-user relation: a tuple of the form <c>(subject, thisRelation, resource)</c> grants it.
    /// The <paramref name="allowedTypes"/> list documents which user types may appear (currently advisory).
    /// </summary>
    public RelationBuilder DirectUsers(params string[] allowedTypes)
    {
        _root = new RelationRewrite.DirectUsers(allowedTypes ?? Array.Empty<string>());
        return this;
    }

    /// <summary>
    /// Computes this relation from another relation on the same resource (e.g. <c>can_write ← editor</c>).
    /// </summary>
    public RelationBuilder ComputedFrom(string relation)
    {
        _root = new RelationRewrite.ComputedUserset(relation);
        return this;
    }

    /// <summary>
    /// Follows a tuple indirection: collect all R' where <c>(resource, tupleset, R')</c> exists, then
    /// evaluate <c>(subject, computedRelation, R')</c>. Models the OpenFGA <c>X#Y</c> pattern.
    /// </summary>
    public RelationBuilder TupleToUserset(string tupleset, string computedRelation)
    {
        _root = new RelationRewrite.TupleToUserset(tupleset, computedRelation);
        return this;
    }

    /// <summary>Sets the root to a union of the supplied child rewrites.</summary>
    public RelationBuilder Union(params RelationRewrite[] children)
    {
        _root = new RelationRewrite.Union(children ?? Array.Empty<RelationRewrite>());
        return this;
    }

    /// <summary>Sets the root to an intersection of the supplied child rewrites.</summary>
    public RelationBuilder Intersection(params RelationRewrite[] children)
    {
        _root = new RelationRewrite.Intersection(children ?? Array.Empty<RelationRewrite>());
        return this;
    }

    /// <summary>Sets the root to an exclusion (<paramref name="include"/> AND NOT <paramref name="exclude"/>).</summary>
    public RelationBuilder Exclusion(RelationRewrite include, RelationRewrite exclude)
    {
        _root = new RelationRewrite.Exclusion(include, exclude);
        return this;
    }

    /// <summary>Finalises the rewrite. Defaults to <see cref="RelationRewrite.Self"/> if none was set.</summary>
    public RelationRewrite Build() => _root ?? new RelationRewrite.Self();
}
