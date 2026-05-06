using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Foundation.Ship.Common;

/// <summary>
/// In-process default implementation of
/// <see cref="IActorPrincipalResolver"/>.
/// </summary>
/// <remarks>
/// <para>
/// Falls back to the canonical Sunfish ActorId invariant:
/// <c>ActorId.Value = PrincipalId.ToBase64Url()</c> — the 43-character
/// base64url encoding of the 32-byte Ed25519 public key. Returns
/// <c>null</c> if the value is not a valid base64url-encoded 32-byte
/// key (fail-closed per <see cref="IActorPrincipalResolver"/> contract).
/// </para>
/// <para>
/// Use <see cref="Register"/> to add explicit
/// <c>ActorId → Principal</c> mappings for test fixtures that use
/// non-canonical <see cref="ActorId"/> values (e.g.,
/// <c>ActorId("alice")</c>). Registered mappings take precedence over
/// the canonical derivation.
/// </para>
/// <para>
/// <b>Thread safety:</b> <see cref="Register"/> is synchronized via a
/// lock. <see cref="ResolveAsync"/> holds the lock only for the
/// registered-mapping lookup; the canonical derivation runs lock-free.
/// </para>
/// </remarks>
public sealed class InMemoryActorPrincipalResolver : IActorPrincipalResolver
{
    private readonly Dictionary<ActorId, Principal> _overrides = new();
    private readonly object _gate = new();

    /// <summary>
    /// Registers an explicit <paramref name="actorId"/> →
    /// <paramref name="principal"/> mapping. Takes precedence over
    /// the canonical base64url derivation; re-registering the same
    /// <paramref name="actorId"/> overwrites the prior mapping.
    /// </summary>
    public void Register(ActorId actorId, Principal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        lock (_gate)
        {
            _overrides[actorId] = principal;
        }
    }

    /// <inheritdoc />
    public ValueTask<Principal?> ResolveAsync(
        TenantId tenantId,
        ActorId actorId,
        CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (_overrides.TryGetValue(actorId, out var registered))
            {
                return ValueTask.FromResult<Principal?>(registered);
            }
        }

        // Canonical invariant: ActorId.Value = PrincipalId.ToBase64Url().
        try
        {
            var id = PrincipalId.FromBase64Url(actorId.Value);
            return ValueTask.FromResult<Principal?>(new Individual(id));
        }
        catch (FormatException)
        {
            return ValueTask.FromResult<Principal?>(null);
        }
    }
}
