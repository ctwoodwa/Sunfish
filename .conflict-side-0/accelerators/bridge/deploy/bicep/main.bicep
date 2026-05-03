// ============================================================================
// Bridge — Option B dedicated-deployment — Azure Bicep
// ============================================================================
//
// Provisions a fully isolated Bridge stack for a single enterprise contract
// per ADR 0031 Option B. Every resource is scoped to one tenant (this tenant
// is the only workload in this resource group); the stack is designed to be
// deployed at subscription scope into a fresh resource group.
//
// Service substitutions versus the shared-Bridge AppHost:
//   - RabbitMQ        -> Azure Service Bus Namespace (Standard tier).
//     Justification: Azure Service Bus is the native Azure message broker;
//     it supports topics/subscriptions (which the Wolverine transport in
//     Sunfish.Bridge can consume via the Azure Service Bus transport). The
//     shared-Bridge AppHost uses RabbitMQ because Aspire's local dev loop
//     does not yet emit a first-class Service Bus emulator. On production
//     Azure, Service Bus is the preferred broker.
//   - Windows DPAPI   -> Azure Key Vault with managed-identity access.
//     The install-level IRootSeedProvider reads its seed material from Key
//     Vault at boot; the Container Apps system-assigned managed identity
//     is granted Get+List on the Key Vault secrets.
//   - Log file sinks  -> Log Analytics workspace + Application Insights.
//
// Pre-allocated tenant-node slots are Container App Jobs configured with
// replicaTimeoutSeconds=0 (long-running) — one per expected team. Unused
// slots sit idle; assignment to a TeamId happens via the POD_ORDINAL-like
// replica index provided by the entrypoint script.
// See: _shared/research/aspire-13-runtime-resource-mutation.md

targetScope = 'subscription'

// ----------------------------------------------------------------------------
// Parameters
// ----------------------------------------------------------------------------

@description('Tenant slug (lowercase, 3-16 chars). Used as the resource-group suffix and DNS prefix.')
@minLength(3)
@maxLength(16)
param tenantSlug string

@description('Azure region for the entire stack. All resources live in one region for Option B isolation.')
param location string = 'eastus2'

@description('Number of pre-allocated tenant-node slots. Rule of thumb: 2x expected team count.')
@minValue(1)
@maxValue(64)
param tenantNodeSlotCount int = 8

@description('Bridge web container image (full registry/repo:tag). Customer-published.')
param bridgeImage string

@description('local-node-host container image (full registry/repo:tag). Customer-published.')
param localNodeImage string

@description('DAB image. Default matches the shared-Bridge AppHost pin.')
param dabImage string = 'mcr.microsoft.com/azure-databases/data-api-builder:1.7.90'

@description('Postgres administrator username.')
param postgresAdminUser string = 'bridgeadmin'

@description('Postgres administrator password. Prefer passing via Key Vault reference.')
@secure()
param postgresAdminPassword string

@description('Postgres SKU name. "Standard_B2s" covers 1-4 teams; scale up for larger dedicated deployments.')
param postgresSkuName string = 'Standard_B2s'

@description('Postgres tier. "Burstable" for <=4 teams; "GeneralPurpose" for larger.')
param postgresTier string = 'Burstable'

@description('Redis SKU. "Basic" is fine for Option B (single-AZ); "Standard" for HA.')
param redisSku string = 'Basic'

@description('Redis capacity (0 = C0/250MB, 1 = C1/1GB, 2 = C2/2.5GB).')
@minValue(0)
@maxValue(6)
param redisCapacity int = 1

@description('Service Bus SKU. "Standard" required for topics; "Basic" only supports queues.')
param serviceBusSku string = 'Standard'

@description('Tag map applied to every resource. Include the contract id, owner, and cost center.')
param tags object = {
  deployment: 'bridge-option-b'
  tenantSlug: tenantSlug
}

// ----------------------------------------------------------------------------
// Resource group
// ----------------------------------------------------------------------------

var rgName = 'rg-sunfish-bridge-${tenantSlug}'

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: rgName
  location: location
  tags: tags
}

// ----------------------------------------------------------------------------
// Inner module — all tenant resources in the RG
// ----------------------------------------------------------------------------

module stack 'modules/stack.bicep' = {
  name: 'bridge-stack-${tenantSlug}'
  scope: rg
  params: {
    tenantSlug: tenantSlug
    location: location
    tags: tags
    tenantNodeSlotCount: tenantNodeSlotCount
    bridgeImage: bridgeImage
    localNodeImage: localNodeImage
    dabImage: dabImage
    postgresAdminUser: postgresAdminUser
    postgresAdminPassword: postgresAdminPassword
    postgresSkuName: postgresSkuName
    postgresTier: postgresTier
    redisSku: redisSku
    redisCapacity: redisCapacity
    serviceBusSku: serviceBusSku
  }
}

// ----------------------------------------------------------------------------
// Outputs (bubbled up from the inner module)
// ----------------------------------------------------------------------------

output bridgeFqdn string = stack.outputs.bridgeFqdn
output tenantSlug string = tenantSlug
output resourceGroupName string = rgName
output keyVaultUri string = stack.outputs.keyVaultUri
output serviceBusNamespace string = stack.outputs.serviceBusNamespace
output postgresServerName string = stack.outputs.postgresServerName
output logAnalyticsWorkspaceId string = stack.outputs.logAnalyticsWorkspaceId
