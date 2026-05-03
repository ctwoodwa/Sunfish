using Sunfish.Foundation.Crypto;

namespace Sunfish.Foundation.Capabilities;

/// <summary>
/// Base type for all capability-graph mutations. Operations are wrapped in a
/// <see cref="SignedOperation{T}"/> envelope for cryptographic authorship, then
/// applied to an <see cref="ICapabilityGraph"/>.
/// </summary>
public abstract record CapabilityOp;

/// <summary>
/// Discriminator for <see cref="MintPrincipal"/> — identifies whether the new
/// principal is a leaf <see cref="Individual"/> or a composite <see cref="Group"/>.
/// </summary>
public enum PrincipalKind
{
    /// <summary>A leaf principal (typically tied to a single keypair).</summary>
    Individual,

    /// <summary>A composite principal with member references.</summary>
    Group,
}

/// <summary>
/// Introduces a brand-new principal into the graph. For groups, optional initial
/// members may be provided; otherwise the group begins empty.
/// </summary>
/// <param name="NewId">The identifier (Ed25519 public key) of the new principal.</param>
/// <param name="Kind">Whether the new principal is an individual or a group.</param>
/// <param name="InitialMembers">
/// Optional seed members when minting a group; ignored for individuals. <c>null</c> or empty
/// yields a group with no members.
/// </param>
public sealed record MintPrincipal(
    PrincipalId NewId,
    PrincipalKind Kind,
    IReadOnlyList<PrincipalId>? InitialMembers = null) : CapabilityOp;

/// <summary>
/// Delegates a <see cref="CapabilityAction"/> on a <see cref="Resource"/> to the given
/// <paramref name="Subject"/>. The issuing principal must itself hold the action with
/// the <see cref="CapabilityAction.Delegate"/> privilege (enforced by the graph).
/// </summary>
/// <param name="Subject">The principal receiving the delegated capability.</param>
/// <param name="Resource">The resource the capability applies to.</param>
/// <param name="Action">The action being granted (read, write, etc.).</param>
/// <param name="Expires">
/// Optional expiration time. When <c>null</c>, the delegation is unbounded; consumers still
/// revoke explicitly via <see cref="Revoke"/>.
/// </param>
public sealed record Delegate(
    PrincipalId Subject,
    Resource Resource,
    CapabilityAction Action,
    DateTimeOffset? Expires = null) : CapabilityOp;

/// <summary>
/// Revokes a previously-delegated capability. Revocation is a new op in the log —
/// prior <see cref="Delegate"/> ops remain for audit purposes.
/// </summary>
/// <param name="Subject">The principal whose capability is being revoked.</param>
/// <param name="Resource">The resource the capability applied to.</param>
/// <param name="Action">The action being revoked.</param>
public sealed record Revoke(
    PrincipalId Subject,
    Resource Resource,
    CapabilityAction Action) : CapabilityOp;

/// <summary>
/// Adds a principal to a group. Nested groups are allowed; the graph computes
/// transitive membership at query time.
/// </summary>
/// <param name="Group">The target group principal.</param>
/// <param name="Member">The principal being added as a member.</param>
public sealed record AddMember(
    PrincipalId Group,
    PrincipalId Member) : CapabilityOp;

/// <summary>
/// Removes a principal from a group. If the member is not currently in the group,
/// this is a no-op but still appended to the log for audit purposes.
/// </summary>
/// <param name="Group">The target group principal.</param>
/// <param name="Member">The principal being removed from the group.</param>
public sealed record RemoveMember(
    PrincipalId Group,
    PrincipalId Member) : CapabilityOp;
