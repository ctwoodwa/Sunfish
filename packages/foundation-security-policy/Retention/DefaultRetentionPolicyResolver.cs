using System;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.SecurityPolicy.Models;

namespace Sunfish.Foundation.SecurityPolicy.Retention;

/// <summary>
/// Reference implementation of
/// <see cref="IRetentionPolicyResolver"/>. Loads the active per-tenant
/// <see cref="TenantSecurityPolicy"/> via the injected loader Func
/// (Phase 1 shim — replaced with
/// <c>ITenantSecurityPolicyLoader</c> in PR 3b.4), reads
/// <see cref="TenantSecurityPolicy.AuditRetention"/>, and applies
/// per-class resolution + §5.2 jurisdiction floor via
/// <see cref="JurisdictionFloorHelper.GetFloor"/>.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// <para>
/// <b>Authorization contract.</b> This service does NOT consult
/// <c>IUserContext</c>. The caller is the authority on whether the
/// supplied tenant identifier is valid; this resolver only answers
/// "given the tenant's active policy, what's the retention verdict?"
/// </para>
/// </remarks>
public sealed class DefaultRetentionPolicyResolver : IRetentionPolicyResolver
{
    private readonly Func<TenantId, CancellationToken, ValueTask<TenantSecurityPolicy>> _policyLoader;

    /// <summary>Construct bound to a per-tenant policy loader.</summary>
    public DefaultRetentionPolicyResolver(
        Func<TenantId, CancellationToken, ValueTask<TenantSecurityPolicy>> policyLoader)
    {
        _policyLoader = policyLoader ?? throw new ArgumentNullException(nameof(policyLoader));
    }

    /// <inheritdoc />
    public async ValueTask<AuditRetentionPolicy> GetActiveAsync(
        TenantId tenant,
        CancellationToken cancellationToken = default)
    {
        var policy = await _policyLoader(tenant, cancellationToken).ConfigureAwait(false);
        return policy.AuditRetention;
    }

    /// <inheritdoc />
    public async ValueTask<RetentionVerdict> ResolveAsync(
        TenantId tenant,
        AuditEventClass eventClass,
        DateTimeOffset recordCreatedAt,
        CancellationToken cancellationToken = default)
    {
        var policy = await GetActiveAsync(tenant, cancellationToken).ConfigureAwait(false);

        // 1. Per-class override > default per §5.2 resolution order.
        var (min, max) = policy.PerClassOverrides.TryGetValue(eventClass, out var perClass)
            ? perClass
            : (policy.DefaultMinimumRetentionWindow, policy.DefaultMaximumRetentionWindow);

        // 2. Jurisdiction-floor application — floor wins only when STRICTLY greater
        //    than the per-class minimum.
        var floor = JurisdictionFloorHelper.GetFloor(policy.JurisdictionPreset, eventClass);
        var isJurisdictionFloor = false;
        if (floor is { } f && f > min)
        {
            min = f;
            isJurisdictionFloor = true;
        }

        // 3. Compose verdict timestamps.
        return new RetentionVerdict(
            EventClass: eventClass,
            MinimumHoldUntil: recordCreatedAt + min,
            MaximumHoldUntil: max == TimeSpan.MaxValue ? DateTimeOffset.MaxValue : recordCreatedAt + max,
            IsJurisdictionFloor: isJurisdictionFloor);
    }
}
