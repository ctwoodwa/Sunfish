using Marilo.PmDemo.Data.Authorization;

namespace Marilo.PmDemo.MigrationService;

/// <summary>
/// Placeholder ITenantContext for the migration runner. Migrations don't query
/// through tenant filters, so the values here are never observed in practice.
/// </summary>
internal sealed class MigrationTenantContext : ITenantContext
{
    public string TenantId => "migration";
    public string UserId => "migration";
    public IReadOnlyList<string> Roles { get; } = [];
    public bool HasPermission(string permission) => false;
}
