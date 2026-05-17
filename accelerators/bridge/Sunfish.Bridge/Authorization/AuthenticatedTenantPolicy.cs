using Microsoft.AspNetCore.Authorization;

namespace Sunfish.Bridge.Authorization;

/// <summary>
/// Shared authorization policy for Cohort 1 non-cockpit Bridge route
/// families (<c>/api/v1/properties</c>, <c>/api/v1/leases</c>, future
/// <c>/api/v1/maintenance/*</c>). Looser than
/// <see cref="Sunfish.Bridge.Cockpit.CockpitEndpoints.CockpitPolicyName"/>:
/// requires only that the caller is authenticated; tenant scoping
/// happens server-side via <c>ITenantContext</c> resolution inside the
/// handler (NOT via query param or header).
/// </summary>
/// <remarks>
/// Per the W#74 hand-off §2.2 + §3.1: Cohort 1 pages must be reachable
/// by any authenticated tenant user (not only the cockpit role set),
/// so <see cref="Sunfish.Bridge.Cockpit.CockpitEndpoints.CockpitPolicyName"/>
/// is too narrow. This policy is the cluster-wide replacement for
/// non-cockpit cluster endpoint families.
/// </remarks>
public static class AuthenticatedTenantPolicy
{
    /// <summary>Policy name; reference from <c>RequireAuthorization()</c>.</summary>
    public const string PolicyName = "AuthenticatedTenantPolicy";

    /// <summary>
    /// Register <see cref="PolicyName"/>. Call from
    /// <c>AddAuthorization</c>'s configuration callback alongside
    /// <c>AddCockpitPolicy()</c>.
    /// </summary>
    public static AuthorizationOptions AddAuthenticatedTenantPolicy(this AuthorizationOptions options)
    {
        System.ArgumentNullException.ThrowIfNull(options);
        options.AddPolicy(PolicyName, policy =>
        {
            policy.RequireAuthenticatedUser();
            // Tenant assertion happens server-side via ITenantContext.TenantId
            // resolution inside the handler. No claim-level tenant binding is
            // required at the policy layer — handlers MUST resolve the tenant
            // from the context (NOT from query parameters or request headers)
            // before any tenant-scoped read.
        });
        return options;
    }
}
