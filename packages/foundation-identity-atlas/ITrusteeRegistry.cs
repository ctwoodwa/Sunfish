using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Foundation.IdentityAtlas;

/// <summary>
/// Read-side access to recovery trustee policy and enrolled trustees per ADR 0066 §Phase 3.
/// Real implementation ships in a future wallet/keystore workstream; until then
/// <see cref="NullTrusteeRegistry"/> is the default.
/// </summary>
public interface ITrusteeRegistry
{
    /// <summary>
    /// Returns the trustee policy for <paramref name="tenant"/>.
    /// Never returns null — returns a zero-max policy when recovery is disabled.
    /// </summary>
    ValueTask<TrusteePolicy> GetPolicyAsync(
        TenantId tenant,
        CancellationToken ct = default);

    /// <summary>
    /// Returns all trustees enrolled by <paramref name="actor"/> in <paramref name="tenant"/>.
    /// Returns an empty list when no trustees are enrolled.
    /// </summary>
    ValueTask<IReadOnlyList<Trustee>> GetTrusteesAsync(
        TenantId tenant,
        ActorId actor,
        CancellationToken ct = default);
}
