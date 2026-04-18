var builder = DistributedApplication.CreateBuilder(args);

// Infrastructure resources
var postgresServer = builder.AddPostgres("pmdemodb-server")
    .WithDataVolume();
var postgres = postgresServer.AddDatabase("pmdemodb");

var redis = builder.AddRedis("pmdemo-redis");

var rabbit = builder.AddRabbitMQ("pmdemo-rabbit")
    .WithManagementPlugin();

// DEMO ONLY. MockOktaService is a minimal OIDC mock for local development.
// Replace with real Okta / Entra ID / Auth0 configuration before production.
// See accelerators/bridge/ROADMAP.md §Auth.
var okta = builder.AddProject<Projects.MockOktaService>("mock-okta");

// One-shot migration runner. Applies EF Core migrations and exits. DAB and the
// web project WaitForCompletion on this so the schema exists before either reads it.
var migrations = builder.AddProject<Projects.Sunfish_Bridge_MigrationService>("pmdemo-migrations")
    .WithReference(postgres)
    .WaitFor(postgres);

// Data API Builder — exposes the Postgres schema as GraphQL.
// dab-config.json lives next to the .slnx and is bind-mounted into the container.
// WithReference(postgres) injects ConnectionStrings__pmdemodb with the correct
// container-to-container hostname (pmdemodb-server, NOT localhost). dab-config.json
// reads it via @env('ConnectionStrings__pmdemodb').
var dabConfigPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "dab-config.json");
var dab = builder.AddContainer("pmdemo-dab", "mcr.microsoft.com/azure-databases/data-api-builder", "latest")
    .WithBindMount(dabConfigPath, "/App/dab-config.json", isReadOnly: true)
    .WithReference(postgres)
    .WithHttpEndpoint(targetPort: 5000, name: "graphql")
    .WaitForCompletion(migrations);

// Server project
builder.AddProject<Projects.Sunfish_Bridge>("pmdemo-web")
    .WithReference(postgres)
    .WithReference(redis)
    .WithReference(rabbit)
    .WithReference(okta)
    .WithEnvironment("DAB_GRAPHQL_URL", dab.GetEndpoint("graphql"))
    .WaitForCompletion(migrations)
    .WaitFor(redis)
    .WaitFor(rabbit)
    .WaitFor(dab);

builder.Build().Run();
