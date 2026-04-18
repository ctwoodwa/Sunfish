using Sunfish.Bridge.Data.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sunfish.Bridge.Data;

/// <summary>
/// Used by `dotnet ef migrations` to construct SunfishBridgeDbContext at design time.
/// At runtime the host registers a real ITenantContext from DI; here we only need
/// a placeholder so EF can build the model.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SunfishBridgeDbContext>
{
    private sealed class DesignTimeTenant : ITenantContext
    {
        public string TenantId => "design-time";
        public string UserId => "design-time";
        public IReadOnlyList<string> Roles { get; } = [];
        public bool HasPermission(string permission) => false;
    }

    public SunfishBridgeDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SunfishBridgeDbContext>()
            .UseNpgsql("Host=localhost;Database=pmdemodb;Username=postgres;Password=postgres")
            .Options;
        return new SunfishBridgeDbContext(options, new DesignTimeTenant());
    }
}
