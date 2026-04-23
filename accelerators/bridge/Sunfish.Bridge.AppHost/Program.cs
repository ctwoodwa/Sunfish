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

// Wave 5.2.E — per-tenant orchestration configuration passed to bridge-web.
//
// TenantDataRoot: platform-appropriate default unless overridden by
//     Bridge:Orchestration:TenantDataRoot in AppHost appsettings.json or via
//     environment variable. The test-host (DistributedApplicationTestingBuilder
//     in Sunfish.Bridge.Tests.Integration) injects a per-test temp directory
//     to avoid cross-test contamination.
//
// LocalNodeExecutablePath: resolved from the Aspire-generated
//     Projects.Sunfish_LocalNodeHost.ProjectPath metadata. We locate the
//     built dll under the Sunfish.LocalNodeHost project's standard output
//     folder (bin/{Configuration}/{TargetFramework}/{AssemblyName}.dll) —
//     this is deterministic because the ProjectReference added in
//     Sunfish.Bridge.AppHost.csproj builds the host as a dependency, so the
//     dll exists by the time AppHost starts bridge-web.
var configuredTenantDataRoot = builder.Configuration["Bridge:Orchestration:TenantDataRoot"];
var tenantDataRoot = !string.IsNullOrWhiteSpace(configuredTenantDataRoot)
    ? configuredTenantDataRoot
    : Path.Combine(Path.GetTempPath(), "sunfish-bridge-tenants");

var configuredLocalNodeExePath = builder.Configuration["Bridge:Orchestration:LocalNodeExecutablePath"];
var localNodeExePath = !string.IsNullOrWhiteSpace(configuredLocalNodeExePath)
    ? configuredLocalNodeExePath
    : ResolveLocalNodeHostDllPath();

var bridgeWeb = builder.AddProject<Projects.Sunfish_Bridge>("bridge-web")
    .WithReference(postgres)
    .WithReference(redis)
    .WithReference(rabbit)
    .WithReference(okta)
    .WithEnvironment("DAB_GRAPHQL_URL", dab.GetEndpoint("graphql"))
    .WithEnvironment("DAB_MCP_URL", $"{dab.GetEndpoint("graphql")}/mcp")
    .WithEnvironment("Bridge__Orchestration__TenantDataRoot", tenantDataRoot)
    .WithEnvironment("Bridge__Orchestration__LocalNodeExecutablePath", localNodeExePath)
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
Console.WriteLine("  before production deployment. See accelerators/bridge/ROADMAP.md §Auth.");
Console.WriteLine("==============================================================================");
Console.WriteLine();

builder.Build().Run();


// ---------------------------------------------------------------------------
// Wave 5.2.E helper — resolve the built Sunfish.LocalNodeHost dll path from
// Aspire's generated project metadata.
// ---------------------------------------------------------------------------

// Resolves the built Sunfish.LocalNodeHost dll path via Aspire's generated
// Projects.Sunfish_LocalNodeHost.ProjectPath metadata. The .csproj sits at
// {repo}/apps/local-node-host/Sunfish.LocalNodeHost.csproj; its standard
// framework-dependent build output is bin/{Configuration}/{TargetFramework}/
// Sunfish.LocalNodeHost.dll. We probe common configurations/frameworks and
// return the first match. If none exist (unlikely — ProjectReference forces
// a build), return the expected Debug/net11.0 path and let Process.Start
// surface the missing-file error at first spawn.
static string ResolveLocalNodeHostDllPath()
{
    var projectPath = new Projects.Sunfish_LocalNodeHost().ProjectPath;
    var projectDir = Path.GetDirectoryName(projectPath)
        ?? throw new InvalidOperationException(
            $"Could not determine directory for project path '{projectPath}'.");

    // Probe order matches typical build pipelines: current .NET version first,
    // then fall back to older preview flavors seen in this repo.
    string[] configurations = ["Debug", "Release"];
    string[] frameworks = ["net11.0", "net10.0", "net9.0"];

    foreach (var config in configurations)
    {
        foreach (var tfm in frameworks)
        {
            var candidate = Path.Combine(projectDir, "bin", config, tfm, "Sunfish.LocalNodeHost.dll");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    // Last-ditch: return the Debug/net11.0 path even if it doesn't exist yet.
    // The supervisor surfaces the error at spawn time, which is visible in the
    // Aspire dashboard rather than silently swallowed at AppHost boot.
    return Path.Combine(projectDir, "bin", "Debug", "net11.0", "Sunfish.LocalNodeHost.dll");
}
