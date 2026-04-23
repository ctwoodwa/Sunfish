using Microsoft.Extensions.Logging;
using Sunfish.Bridge.Data.Entities;
using Sunfish.Foundation.Authorization;

namespace Sunfish.Bridge.Authorization;

/// <summary>
/// DEMO ONLY. Provides a hardcoded tenant/user identity for local development.
/// </summary>
/// <remarks>
/// <para>
/// Per ADR 0031 Wave 5.1, this context is scoped to Bridge's control plane only:
/// it resolves a <see cref="TenantRegistration"/> row for signup/billing/support
/// use-cases. It holds no authority over team data. The demo tenant is just the
/// <c>slug="demo"</c> row seeded by <c>BridgeSeeder</c>; in production this class
/// is replaced by a claims-backed <see cref="ITenantContext"/> that reads the
/// authenticated tenant from OIDC/Entra/Okta.
/// </para>
/// <para>
/// The <see cref="ITenantContext"/> shape (<see cref="TenantId"/>, <see cref="UserId"/>,
/// <see cref="Roles"/>, <see cref="HasPermission"/>) is preserved because many other
/// packages (blocks-subscriptions, blocks-tenant-admin, blocks-businesscases, etc.)
/// consume it; the narrowing ADR 0031 calls for is in how Bridge *uses* the context —
/// control-plane only — not in the interface itself. See
/// accelerators/bridge/ROADMAP.md §Auth for replacement guidance.
/// </para>
/// </remarks>
public sealed class DemoTenantContext : ITenantContext
{
    /// <summary>Slug of the demo tenant row seeded into <c>tenant_registrations</c>.</summary>
    public const string DemoSlug = "demo";

    private static int _warningLogged; // emit once per process

    public DemoTenantContext(ILogger<DemoTenantContext> logger)
    {
        if (System.Threading.Interlocked.CompareExchange(ref _warningLogged, 1, 0) == 0)
        {
            logger.LogWarning(
                "DEMO AUTH SEAM ACTIVE: DemoTenantContext is registered (control-plane only per ADR 0031). " +
                "TenantId='{TenantId}' (slug '{Slug}'), UserId='{UserId}'. " +
                "This is for local development only. Replace with a real ITenantContext implementation " +
                "before production deployment. See accelerators/bridge/ROADMAP.md §Auth.",
                TenantId, DemoSlug, UserId);
        }
    }

    /// <summary>Matches the <c>BridgeSeeder</c> demo tenant for dev ergonomics;
    /// a claims-backed implementation would pull this from the authenticated session.</summary>
    public string TenantId => "demo-tenant";
    public string UserId => "demo-user";
    public IReadOnlyList<string> Roles { get; } = [Authorization.Roles.Manager];
    public bool HasPermission(string permission) => true;
}

internal static class Roles
{
    public const string Manager = Sunfish.Bridge.Data.Authorization.Roles.Manager;
}
