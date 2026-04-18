using Marilo.PmDemo.Data.Authorization;

namespace Marilo.PmDemo.Authorization;

public sealed class DemoTenantContext : ITenantContext
{
    public string TenantId => "demo-tenant";
    public string UserId => "demo-user";
    public IReadOnlyList<string> Roles { get; } = [Authorization.Roles.ProjectManager];
    public bool HasPermission(string permission) => true;
}

internal static class Roles
{
    public const string ProjectManager = Marilo.PmDemo.Data.Authorization.Roles.ProjectManager;
}
