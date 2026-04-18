using Sunfish.Foundation.Crypto;

namespace Sunfish.Foundation.PolicyEvaluator;

/// <summary>
/// A subject is the actor whose permissions are being evaluated. It wraps a
/// <see cref="PrincipalId"/> (the cryptographic identity) plus an optional list of role strings
/// which act as a placeholder for future RBAC integration; an empty list is a valid subject.
/// </summary>
/// <remarks>
/// <para>
/// <b>Equality:</b> The synthesized record equality of <c>Subject</c> compares the
/// <see cref="Roles"/> list by reference (since <see cref="IReadOnlyList{T}"/> does not override
/// <c>Equals</c>). Because the tuple store hashes subjects as part of its composite key,
/// <see cref="Equals(Subject?)"/> and <see cref="GetHashCode"/> are overridden to hash on
/// <see cref="PrincipalId"/> alone — two subjects with the same principal (regardless of roles)
/// are treated as the same subject. Roles are a projection of the principal's membership and are
/// advisory only at this layer; the ReBAC evaluator reads relation tuples, not roles.
/// </para>
/// <para>
/// See the Sunfish Platform specification §3.5 for the role of <c>Subject</c> in ReBAC evaluation.
/// </para>
/// </remarks>
public sealed record Subject(PrincipalId PrincipalId, IReadOnlyList<string> Roles)
{
    /// <summary>Convenience constructor for a subject with no roles.</summary>
    public Subject(PrincipalId principalId) : this(principalId, Array.Empty<string>()) { }

    /// <inheritdoc />
    public bool Equals(Subject? other)
        => other is not null && PrincipalId.Equals(other.PrincipalId);

    /// <inheritdoc />
    public override int GetHashCode() => PrincipalId.GetHashCode();
}
