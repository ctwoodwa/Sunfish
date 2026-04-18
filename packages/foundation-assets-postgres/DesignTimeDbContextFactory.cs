using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sunfish.Foundation.Assets.Postgres;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> to scaffold migrations without a running
/// provider. Points at a default localhost Postgres connection string; the migration
/// generator only needs the schema model, not a live database.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AssetStoreDbContext>
{
    /// <inheritdoc />
    public AssetStoreDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AssetStoreDbContext>()
            .UseNpgsql("Host=localhost;Database=sunfish_assets;Username=postgres;Password=postgres")
            .Options;
        return new AssetStoreDbContext(options);
    }
}
