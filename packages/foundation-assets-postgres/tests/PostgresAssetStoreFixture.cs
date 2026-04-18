using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Sunfish.Foundation.Assets.Postgres.Tests;

/// <summary>
/// xUnit class-level fixture that spins up a PostgreSQL 16 container, applies EF Core
/// migrations, and exposes a per-test <see cref="IDbContextFactory{TContext}"/>.
/// </summary>
/// <remarks>
/// <para>
/// The container is shared across all tests in a single test class (via
/// <see cref="Xunit.IClassFixture{TFixture}"/>). Each test writes to its own isolated
/// <c>scheme</c> namespace, so tests do not collide in practice. The <c>Reset</c> helper
/// is available for tests that need a truly clean slate.
/// </para>
/// <para>
/// Podman / Windows compatibility: Testcontainers auto-detects the Docker socket. On the
/// dev box this is a Podman machine exposing the docker-compat API on the npipe; Testcontainers
/// picks that up via the default <c>DOCKER_HOST</c> resolution, matching the shim used for
/// the existing kind cluster.
/// </para>
/// </remarks>
public sealed class PostgresAssetStoreFixture : IAsyncLifetime
{
    // Podman Ryuk-cleanup + Windows path handling is brittle; disable Ryuk to match the
    // prior-art pattern used elsewhere in the repo.
    static PostgresAssetStoreFixture()
    {
        Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "true");
    }

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("sunfish_assets_test")
        .WithUsername("sunfish")
        .WithPassword("sunfish")
        .Build();

    /// <summary>Resolved Npgsql connection string for the running container.</summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <summary>Returns a fresh EF Core context factory bound to the test database.</summary>
    public IDbContextFactory<AssetStoreDbContext> CreateFactory()
        => new TestDbContextFactory(ConnectionString);

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await _container.StartAsync().ConfigureAwait(false);
        var options = new DbContextOptionsBuilder<AssetStoreDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        await using var ctx = new AssetStoreDbContext(options);
        await ctx.Database.MigrateAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>
    /// Drops and re-applies the schema for tests that need a truly clean slate.
    /// </summary>
    public async Task ResetAsync(CancellationToken ct = default)
    {
        var options = new DbContextOptionsBuilder<AssetStoreDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        await using var ctx = new AssetStoreDbContext(options);
        await ctx.Database.EnsureDeletedAsync(ct).ConfigureAwait(false);
        await ctx.Database.MigrateAsync(ct).ConfigureAwait(false);
    }

    private sealed class TestDbContextFactory : IDbContextFactory<AssetStoreDbContext>
    {
        private readonly string _connectionString;

        public TestDbContextFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        public AssetStoreDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<AssetStoreDbContext>()
                .UseNpgsql(_connectionString)
                .Options;
            return new AssetStoreDbContext(options);
        }
    }
}
