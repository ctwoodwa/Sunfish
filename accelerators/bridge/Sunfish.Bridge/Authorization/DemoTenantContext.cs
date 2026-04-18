using Microsoft.Extensions.Logging;
using Sunfish.Foundation.Authorization;

namespace Sunfish.Bridge.Authorization;

/// <summary>
/// DEMO ONLY. Provides a hardcoded tenant/user identity for local development.
/// Replace with a real <see cref="ITenantContext"/> implementation that reads
/// from authenticated claims (OIDC, Entra, Okta) before production deployment.
/// See accelerators/bridge/ROADMAP.md §Auth for replacement guidance.
/// </summary>
public sealed class DemoTenantContext : ITenantContext
{
    private static int _warningLogged; // emit once per process

    public DemoTenantContext(ILogger<DemoTenantContext> logger)
    {
        if (System.Threading.Interlocked.CompareExchange(ref _warningLogged, 1, 0) == 0)
        {
            logger.LogWarning(
                "DEMO AUTH SEAM ACTIVE: DemoTenantContext is registered. " +
                "TenantId='{TenantId}', UserId='{UserId}'. " +
                "This is for local development only. Replace with a real ITenantContext implementation " +
                "before production deployment. See accelerators/bridge/ROADMAP.md \u00A7Auth.",
                TenantId, UserId);
        }
    }

    public string TenantId => "demo-tenant";
    public string UserId => "demo-user";
    public IReadOnlyList<string> Roles { get; } = [Authorization.Roles.ProjectManager];
    public bool HasPermission(string permission) => true;
}

internal static class Roles
{
    public const string ProjectManager = Sunfish.Bridge.Data.Authorization.Roles.ProjectManager;
}
