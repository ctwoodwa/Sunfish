# Bridge Option B — Azure Bicep

Azure-native IaC for spinning up a dedicated Bridge stack per ADR 0031 Option B.

## What it provisions

Per `main.bicep` + `modules/stack.bicep`, every resource below is created in a
brand-new resource group named `rg-sunfish-bridge-{tenantSlug}`:

| Resource | Azure service | Purpose |
|---|---|---|
| Postgres | Flexible Server (PG 16) | Bridge control-plane tables + migrations |
| Redis | Azure Cache for Redis | SignalR backplane, tenant-state cache |
| Broker | **Azure Service Bus** (namespace + topics) | Wolverine transport — replaces RabbitMQ (see "Protocol swap" below) |
| Bridge web | Container App | Runs the `Sunfish.Bridge` image |
| DAB | Container App | Runs `mcr.microsoft.com/azure-databases/data-api-builder:1.7.90` |
| Tenant-node slots | N Container Apps | Pre-allocated `local-node-host` slots (default N=8) |
| Secrets | Azure Key Vault | Install-level root seed, connection strings, replaces Windows DPAPI |
| Observability | Log Analytics + Application Insights | Logs, metrics, traces |
| Container Apps environment | Managed Environment | Single env scoped to this tenant |

## Protocol swap — RabbitMQ → Service Bus

The shared-Bridge AppHost (`Sunfish.Bridge.AppHost/Program.cs`) wires up RabbitMQ via
`Aspire.Hosting.RabbitMQ`. In Azure-native Option B we substitute **Azure Service Bus**:

- Same wire protocol at AMQP 1.0 level — Wolverine has a first-class Azure Service Bus
  transport that parses the same message envelopes.
- Service Bus Standard tier (required for topics/subscriptions) maps cleanly to the
  Wolverine "broker with fan-out" pattern Bridge uses for gossip relay.
- The Bridge web container reads a `ConnectionStrings__messaging` environment variable;
  both transports publish to the same name.

To keep your on-prem dev loop on RabbitMQ and production on Service Bus:

```csharp
// Sunfish.Bridge/Program.cs (existing)
builder.UseWolverine(opts => {
    var messagingConn = builder.Configuration.GetConnectionString("messaging");
    if (messagingConn!.StartsWith("Endpoint=sb://")) {
        opts.UseAzureServiceBus(messagingConn);
    } else {
        opts.UseRabbitMq(messagingConn);
    }
});
```

The IaC emits a Service-Bus-format connection string; your local `dotnet run`
continues to emit a RabbitMQ-format one.

## Prerequisites

1. Azure subscription + a resource-group-creator role at subscription scope (Contributor,
   or a scoped custom role covering `Microsoft.Resources/subscriptions/resourceGroups/write`).
2. Quotas available in your chosen region for:
   - Azure Container Apps environments (1 per tenant)
   - Postgres Flexible Server (1 per tenant)
   - Azure Cache for Redis (1 per tenant)
   - Azure Key Vault (1 per tenant — purge-protection is enabled, so reuse of soft-deleted
     names is gated by the 90-day retention window)
   - Service Bus namespace (1 per tenant)
3. Your Bridge + local-node-host container images published to a registry that the
   Container Apps environment can reach (ACR + managed identity is the cleanest path;
   GHCR/Docker-Hub public images work too).
4. Bicep CLI 0.25+ installed (`az bicep install`).

## Deploy

```bash
# Copy and edit the sample parameters file first
cp sample-parameters.json my-params.json
# Edit my-params.json — set tenantSlug, image tags, region, etc.

# Deploy at subscription scope (creates the RG + all resources)
az deployment sub create \
  --name bridge-option-b-acmedemo \
  --location eastus2 \
  --template-file main.bicep \
  --parameters @my-params.json
```

## Validate the template without deploying

```bash
# Compile only (checks syntax + type correctness)
az bicep build --file main.bicep --outfile main.json

# What-if (shows planned changes against a subscription)
az deployment sub what-if \
  --location eastus2 \
  --template-file main.bicep \
  --parameters @my-params.json
```

## Post-deployment

1. Copy the `bridgeFqdn` output and map your DNS record (e.g. `acme.bridge.example.com`)
   to it via a CNAME or an Azure Front Door upstream.
2. Seed the Key Vault with the install-level root seed:
   ```bash
   az keyvault secret set \
     --vault-name $(az deployment sub show -n bridge-option-b-acmedemo --query properties.outputs.keyVaultUri.value -o tsv | sed 's|https://||;s|/.*||') \
     --name install-root-seed \
     --file /path/to/wrapped-root-seed.bin \
     --encoding base64
   ```
3. Restart the Bridge Container App so it picks up the seed:
   ```bash
   az containerapp revision restart \
     --name ca-bridge-acmedemo \
     --resource-group rg-sunfish-bridge-acmedemo \
     --revision latest
   ```

## Tear down

Delete the resource group — everything except Key Vault is destroyed instantly; Key Vault
enters soft-delete for 90 days (set `enablePurgeProtection: false` in stack.bicep if that
retention is unacceptable for a demo deployment — but do NOT relax it for production).

```bash
az group delete --name rg-sunfish-bridge-acmedemo --yes --no-wait
```

## Known TODOs

- `TODO(w5.5.1)`: DAB config is assumed baked into the image. A follow-up pass should mount
  `dab-config.json` from a Files share or Key Vault.
- Zone-redundant Postgres / Premium Redis are wired in as parameters but default to the
  cheapest SKUs. Review per contract SLA.
- Private endpoints for Postgres / Redis / Service Bus are **not** configured — the
  Container Apps environment hits them over public endpoints with firewall rules.
  For regulated contracts, extend with a VNet and private endpoints.
