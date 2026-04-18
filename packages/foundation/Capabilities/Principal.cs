using Sunfish.Foundation.Crypto;

namespace Sunfish.Foundation.Capabilities;

/// <summary>
/// Base type for all principals (nodes in the capability graph).
/// A principal is either an <see cref="Individual"/> (leaf) or a <see cref="Group"/> (composite).
/// See <c>docs/specifications/sunfish-platform-specification.md</c> §10.2.1.
/// </summary>
public abstract record Principal(PrincipalId Id);

/// <summary>
/// Leaf principal — an Ed25519 keypair owner. Typically a device, service account,
/// or logical identity considered atomic by the consumer.
/// </summary>
public sealed record Individual(PrincipalId Id) : Principal(Id);

/// <summary>
/// Composite principal — a named collection of member principals. Members may be
/// individuals or other groups (nesting allowed). Membership is mutated via
/// <c>AddMember</c> / <c>RemoveMember</c> signed operations.
/// </summary>
public sealed record Group(PrincipalId Id, IReadOnlyList<PrincipalId> Members) : Principal(Id);
