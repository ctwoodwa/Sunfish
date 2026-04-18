using Marilo.PmDemo.Data;
using Marilo.PmDemo.Data.Authorization;
using Marilo.PmDemo.MigrationService;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// Migration runner doesn't need a real tenant — it never queries through filters.
builder.Services.AddScoped<ITenantContext, MigrationTenantContext>();

builder.Services.AddDbContext<PmDemoDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("pmdemodb")));
builder.EnrichNpgsqlDbContext<PmDemoDbContext>();

builder.Services.AddHostedService<MigrationWorker>();

var host = builder.Build();
host.Run();
