var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure resources
var postgresServer = builder.AddPostgres("sunfishbridgedb-server")
    .WithDataVolume();
var postgres = postgresServer.AddDatabase("sunfishbridgedb");

var redis = builder.AddRedis("bridge-redis");

var rabbit = builder.AddRabbitMQ("bridge-rabbit")
    .WithManagementPlugin();

// DEMO ONLY. MockOktaService is a minimal OIDC mock for local development.
// Replace with real Okta / Entra ID / Auth0 configuration before production.
// See accelerators/bridge/ROADMAP.md §Auth.
var okta = builder.AddProject<Projects.MockOktaService>("mock-okta");

// One-shot migration runner. Applies EF Core migrations and exits. DAB and the
// web project WaitForCompletion on this so the schema exists before either reads it.
var migrations = builder.AddProject<Projects.Sunfish_Bridge_MigrationService>("bridge-migrations")
    .WithReference(postgres)
    .WaitFor(postgres);

// Data API Builder — exposes the Postgres schema as GraphQL AND as an MCP SQL
// server (DML tools) at /mcp for AI-agent integration. Pinned to 1.7.90 to match
// the local `dab` CLI; avoids `:latest` drift.
// dab-config.json lives next to the .slnx and is bind-mounted into the container.
// WithReference(postgres) injects ConnectionStrings__sunfishbridgedb with the correct
// container-to-container hostname (sunfishbridgedb-server, NOT localhost). dab-config.json
// reads it via @env('ConnectionStrings__sunfishbridgedb').
var dabConfigPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "dab-config.json");
var dab = builder.AddContainer("bridge-dab", "mcr.microsoft.com/azure-databases/data-api-builder", "1.7.90")
    .WithBindMount(dabConfigPath, "/App/dab-config.json", isReadOnly: true)
    .WithReference(postgres)
    .WithHttpEndpoint(targetPort: 5000, name: "graphql")
    .WaitForCompletion(migrations);

// Server project
builder.AddProject<Projects.Sunfish_Bridge>("bridge-web")
    .WithReference(postgres)
    .WithReference(redis)
    .WithReference(rabbit)
    .WithReference(okta)
    .WithEnvironment("DAB_GRAPHQL_URL", dab.GetEndpoint("graphql"))
    .WithEnvironment("DAB_MCP_URL", $"{dab.GetEndpoint("graphql")}/mcp")
    .WaitForCompletion(migrations)
    .WaitFor(redis)
    .WaitFor(rabbit)
    .WaitFor(dab);

// DEMO AUTH SEAM WARNING — surfaces in Aspire dashboard console on boot.
Console.WriteLine();
Console.WriteLine("==============================================================================");
Console.WriteLine("  DEMO AUTH SEAM ACTIVE");
Console.WriteLine();
Console.WriteLine("  MockOktaService is registered as the OIDC provider. This is for local");
Console.WriteLine("  development only. Replace with real Okta / Entra ID / Auth0 configuration");
Console.WriteLine("  before production deployment. See accelerators/bridge/ROADMAP.md \u00A7Auth.");
Console.WriteLine("==============================================================================");
Console.WriteLine();

builder.Build().Run();
