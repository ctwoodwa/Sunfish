namespace Sunfish.Foundation.PolicyEvaluator;

/// <summary>
/// A reference to the <i>user side</i> of a relation tuple <c>(user, relation, object)</c>.
/// In OpenFGA the user side may be a principal, a "self" reference to another object (so that
/// tuple-to-userset rewrites can follow the chain), or another userset expression (<c>object#relation</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists:</b> The plan for Task 6 hit a gap when modeling
/// <see cref="RelationRewrite.TupleToUserset"/>. If the user-side were just a
/// <see cref="Subject"/> (a principal), there would be no way to express
/// "<c>acmeFirm</c> is the <c>pm_firm</c> of <c>property:42</c>" — because <c>acmeFirm</c> is a
/// resource (inspection_firm), not a principal — and no way to find all principals who are
/// employees of <c>acmeFirm</c>. <c>UsersetRef</c> solves this by allowing the user side to be
/// either a concrete subject, a self-reference to another resource (used for tuple-to-userset
/// chaining), or an explicit userset expression.
/// </para>
/// <para>
/// See §3.5 of the Sunfish Platform specification for the worked OpenFGA example that motivates
/// this layering.
/// </para>
/// </remarks>
public abstract record UsersetRef
{
    /// <summary>A concrete principal in the tuple's user slot.</summary>
    public sealed record User(Subject Subject) : UsersetRef;

    /// <summary>
    /// A self-reference to another resource. Used when a tuple such as
    /// <c>(acmeFirm-as-resource, pm_firm, property:42)</c> expresses "acmeFirm is the pm_firm of
    /// property 42". Evaluated specially by tuple-to-userset rewrites.
    /// </summary>
    public sealed record SelfRef(PolicyResource Resource) : UsersetRef;

    /// <summary>
    /// An explicit userset: "all users with <see cref="Relation"/> on <see cref="Resource"/>".
    /// Equivalent to OpenFGA's <c>object#relation</c> user-side syntax.
    /// </summary>
    public sealed record Set(PolicyResource Resource, string Relation) : UsersetRef;
}

/// <summary>
/// The relation-tuple persistence contract used by <see cref="ReBACPolicyEvaluator"/>.
/// Stores tuples of the form <c>(user, relation, object)</c> where the user side is a
/// <see cref="UsersetRef"/> (see the type's remarks for why this is not a plain <see cref="Subject"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Layering:</b> This interface is deliberately separate from
/// <see cref="Sunfish.Foundation.Capabilities.ICapabilityGraph"/>. The capability graph is the
/// signed-delegation authority record; the tuple store is a policy-model fact base. Keeping them
/// distinct lets the policy evaluator be self-contained and testable without requiring callers
/// to construct signed capability ops for every ReBAC fact. Integration wiring between the two
/// layers is out of scope for Task 6 and belongs to Task 8.
/// </para>
/// <para>
/// <b>Idempotency:</b> <see cref="AddAsync"/> is idempotent — adding the same tuple twice has no
/// effect. <see cref="RemoveAsync"/> returns silently if the tuple is not present.
/// </para>
/// <para>
/// <b>Thread-safety:</b> The in-memory implementation serialises mutations under a single lock.
/// Reads (including <see cref="ListUsersAsync"/>) take a snapshot, so enumeration is not affected
/// by concurrent writes.
/// </para>
/// </remarks>
public interface IRelationTupleStore
{
    /// <summary>Adds <c>(user, relation, @object)</c> to the store (idempotent).</summary>
    ValueTask AddAsync(UsersetRef user, string relation, PolicyResource @object, CancellationToken ct = default);

    /// <summary>Removes <c>(user, relation, @object)</c> if present (silent if absent).</summary>
    ValueTask RemoveAsync(UsersetRef user, string relation, PolicyResource @object, CancellationToken ct = default);

    /// <summary>Returns <c>true</c> iff <c>(user, relation, @object)</c> is present.</summary>
    ValueTask<bool> ExistsAsync(UsersetRef user, string relation, PolicyResource @object, CancellationToken ct = default);

    /// <summary>Enumerates all user-side entries for tuples matching <c>(anything, relation, @object)</c>.</summary>
    IAsyncEnumerable<UsersetRef> ListUsersAsync(string relation, PolicyResource @object, CancellationToken ct = default);
}

/// <summary>Convenience extensions over <see cref="IRelationTupleStore"/>.</summary>
public static class RelationTupleStoreExtensions
{
    /// <summary>
    /// Checks whether a concrete subject is in the user slot of <c>(subject, relation, @object)</c>.
    /// </summary>
    public static ValueTask<bool> SubjectHasRelationAsync(
        this IRelationTupleStore store,
        Subject subject,
        string relation,
        PolicyResource @object,
        CancellationToken ct = default)
        => store.ExistsAsync(new UsersetRef.User(subject), relation, @object, ct);

    /// <summary>Convenience for the common case of adding a subject-user tuple.</summary>
    public static ValueTask AddSubjectAsync(
        this IRelationTupleStore store,
        Subject subject,
        string relation,
        PolicyResource @object,
        CancellationToken ct = default)
        => store.AddAsync(new UsersetRef.User(subject), relation, @object, ct);
}
