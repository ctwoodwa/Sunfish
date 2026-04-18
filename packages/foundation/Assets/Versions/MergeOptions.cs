using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Assets.Versions;

/// <summary>
/// Options for <see cref="IVersionStore.MergeAsync"/>. Phase A stubs the operation; see
/// plan D-CRDT-ROUTE.
/// </summary>
public sealed record MergeOptions(ActorId Actor, string? Resolver = null);
