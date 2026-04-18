using Marilo.PmDemo.Data.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Marilo.PmDemo.Data;

/// <summary>
/// Used by `dotnet ef migrations` to construct PmDemoDbContext at design time.
/// At runtime the host registers a real ITenantContext from DI; here we only need
/// a placeholder so EF can build the model.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PmDemoDbContext>
{
    private sealed class DesignTimeTenant : ITenantContext
    {
        public string TenantId => "design-time";
        public string UserId => "design-time";
        public IReadOnlyList<string> Roles { get; } = [];
        public bool HasPermission(string permission) => false;
    }

    public PmDemoDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PmDemoDbContext>()
            .UseNpgsql("Host=localhost;Database=pmdemodb;Username=postgres;Password=postgres")
            .Options;
        return new PmDemoDbContext(options, new DesignTimeTenant());
    }
}
