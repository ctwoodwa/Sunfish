using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Foundation.IdentityAtlas;

/// <summary>
/// Null-object <see cref="ITrusteeRegistry"/> that returns a zero-max policy and
/// empty trustee list for all queries.
/// Default registration until a real wallet/keystore workstream ships the production
/// implementation. Per ADR 0066 §Phase 3 stub-first pattern (W#55 P2d precedent).
/// </summary>
/// <remarks>
/// TODO: W#XX — replace with real wallet/keystore implementation reading from
/// kernel-security / kernel-runtime backing stores.
/// </remarks>
public sealed class NullTrusteeRegistry : ITrusteeRegistry
{
    private static readonly TrusteePolicy DisabledPolicy = new(MaxTrustees: 0);

    /// <inheritdoc />
    public ValueTask<TrusteePolicy> GetPolicyAsync(
        TenantId tenant,
        CancellationToken ct = default) =>
        ValueTask.FromResult(DisabledPolicy);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<Trustee>> GetTrusteesAsync(
        TenantId tenant,
        ActorId actor,
        CancellationToken ct = default) =>
        ValueTask.FromResult<IReadOnlyList<Trustee>>(System.Array.Empty<Trustee>());
}
