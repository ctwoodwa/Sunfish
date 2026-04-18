using Marilo.PmDemo.Data;
using Microsoft.EntityFrameworkCore;

namespace Marilo.PmDemo.MigrationService;

/// <summary>
/// One-shot worker that applies EF Core migrations to the PmDemo database, then
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
            var db = scope.ServiceProvider.GetRequiredService<PmDemoDbContext>();

            // In development, drop the existing schema before migrating. The data
            // volume persists across F5 cycles, and any change to entity → table
            // mapping (or column names) would otherwise leave stale tables around
            // that the current Initial migration has no knowledge of.
            if (_env.IsDevelopment())
            {
                _logger.LogInformation("Development environment: dropping existing PmDemo schema before migrate.");
                await db.Database.EnsureDeletedAsync(stoppingToken);
            }

            _logger.LogInformation("Applying PmDemo database migrations...");
            await db.Database.MigrateAsync(stoppingToken);
            _logger.LogInformation("PmDemo database migrations complete.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "PmDemo migration failed.");
            Environment.ExitCode = 1;
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }
}
