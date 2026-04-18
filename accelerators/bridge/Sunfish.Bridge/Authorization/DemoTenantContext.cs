using Sunfish.Bridge.Data.Authorization;

namespace Sunfish.Bridge.Authorization;

/// <summary>
/// DEMO ONLY. Provides a hardcoded tenant/user identity for local development.
/// Replace with a real <see cref="ITenantContext"/> implementation that reads
/// from authenticated claims (OIDC, Entra, Okta) before production deployment.
/// See accelerators/bridge/ROADMAP.md §Auth for replacement guidance.
/// </summary>
public sealed class DemoTenantContext : ITenantContext
{
    public string TenantId => "demo-tenant";
    public string UserId => "demo-user";
    public IReadOnlyList<string> Roles { get; } = [Authorization.Roles.ProjectManager];
    public bool HasPermission(string permission) => true;
}

internal static class Roles
{
    public const string ProjectManager = Sunfish.Bridge.Data.Authorization.Roles.ProjectManager;
}
