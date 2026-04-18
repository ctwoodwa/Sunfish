using Sunfish.Bridge.Data;
using Sunfish.Foundation.Authorization;
using Sunfish.Bridge.MigrationService;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// Migration runner doesn't need a real tenant — it never queries through filters.
builder.Services.AddScoped<ITenantContext, MigrationTenantContext>();

builder.Services.AddDbContext<SunfishBridgeDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("sunfishbridgedb")));
builder.EnrichNpgsqlDbContext<SunfishBridgeDbContext>();

builder.Services.AddHostedService<MigrationWorker>();

var host = builder.Build();
host.Run();
