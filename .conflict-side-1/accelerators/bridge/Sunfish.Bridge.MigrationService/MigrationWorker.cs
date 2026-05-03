using Sunfish.Bridge.Data;
using Microsoft.EntityFrameworkCore;

namespace Sunfish.Bridge.MigrationService;

/// <summary>
/// One-shot worker that applies EF Core migrations to the Bridge database, then
/// stops the host. Aspire AppHost wires DAB and the web project to
/// WaitForCompletion on this resource so the schema exists before they read it.
/// </summary>
public sealed class MigrationWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IHostEnvironment _env;
    private readonly ILogger<MigrationWorker> _logger;

    public MigrationWorker(
        IServiceProvider services,
        IHostApplicationLifetime lifetime,
        IHostEnvironment env,
        ILogger<MigrationWorker> logger)
    {
        _services = services;
        _lifetime = lifetime;
        _env = env;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SunfishBridgeDbContext>();

            // In development, drop the existing schema before migrating. The data
            // volume persists across F5 cycles, and any change to entity → table
            // mapping (or column names) would otherwise leave stale tables around
            // that the current Initial migration has no knowledge of.
            if (_env.IsDevelopment())
            {
                _logger.LogInformation("Development environment: dropping existing Bridge schema before migrate.");
                await db.Database.EnsureDeletedAsync(stoppingToken);
            }

            _logger.LogInformation("Applying Bridge database migrations...");
            await db.Database.MigrateAsync(stoppingToken);
            _logger.LogInformation("Bridge database migrations complete.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Bridge migration failed.");
            Environment.ExitCode = 1;
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }
}
