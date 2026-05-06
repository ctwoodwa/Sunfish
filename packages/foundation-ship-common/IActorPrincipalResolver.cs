using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Foundation.Ship.Common;

/// <summary>
/// Resolves an <see cref="ActorId"/> to its canonical <see cref="Principal"/>
/// (Ed25519 public-key identity). Used by Phase 2 data providers that
/// receive <see cref="ActorId"/> from Standing-Order context and need to
/// call <see cref="IPermissionResolver"/> (which takes
/// <see cref="Principal"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Canonical invariant.</b> In Sunfish's self-sovereign identity
/// model, <c>ActorId.Value</c> is the 43-character base64url (RFC 4648
/// §5, unpadded) encoding of the 32-byte Ed25519 public key.
/// <see cref="InMemoryActorPrincipalResolver"/> enforces this
/// invariant as the fallback path; non-canonical fixture
/// <see cref="ActorId"/>s (e.g., <c>ActorId("alice")</c>) require an
/// explicit override registered via
/// <see cref="InMemoryActorPrincipalResolver.Register"/>.
/// </para>
/// <para>
/// <b>Null means fail-closed.</b> When <see cref="ResolveAsync"/>
/// returns <c>null</c>, the actor cannot be resolved to a principal.
/// Callers MUST treat <c>null</c> as deny / skip — never assume an
/// unresolvable actor is permitted.
/// </para>
/// <para>
/// <b>Why not pass <see cref="Principal"/> directly to data
/// providers?</b> Standing-Order context carries
/// <see cref="ActorId"/>; the v1 substrate does not propagate
/// <see cref="Principal"/> through the call boundary. The resolver
/// seam isolates the actor → principal mapping to one well-defined
/// host-registered service rather than threading
/// <see cref="Principal"/> through every Phase 2 contract.
/// </para>
/// </remarks>
public interface IActorPrincipalResolver
{
    /// <summary>
    /// Resolves <paramref name="actorId"/> to its
    /// <see cref="Principal"/> within <paramref name="tenantId"/>.
    /// Returns <c>null</c> if the actor cannot be resolved — callers
    /// MUST treat <c>null</c> as fail-closed.
    /// </summary>
    /// <param name="tenantId">
    /// Tenant scope; reserved for future per-tenant override paths
    /// (the in-memory default ignores it because overrides are
    /// actor-scoped, not tenant-scoped).
    /// </param>
    /// <param name="actorId">Actor identifier to resolve.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<Principal?> ResolveAsync(
        TenantId tenantId,
        ActorId actorId,
        CancellationToken ct = default);
}
