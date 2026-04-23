// ============================================================================
// Bridge Option B — inner stack module (resource-group scoped)
// ============================================================================
//
// All tenant-scoped resources for a dedicated Bridge deployment.
// Invoked from main.bicep; not meant to be deployed directly.

targetScope = 'resourceGroup'

param tenantSlug string
param location string
param tags object
param tenantNodeSlotCount int
param bridgeImage string
param localNodeImage string
param dabImage string
param postgresAdminUser string
@secure()
param postgresAdminPassword string
param postgresSkuName string
param postgresTier string
param redisSku string
param redisCapacity int
param serviceBusSku string

// ----------------------------------------------------------------------------
// Observability: Log Analytics + Application Insights
// ----------------------------------------------------------------------------

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-${tenantSlug}'
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-${tenantSlug}'
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logs.id
  }
}

// ----------------------------------------------------------------------------
// Secrets: Key Vault (holds install-level root seed + connection strings)
// ----------------------------------------------------------------------------

resource kv 'Microsoft.KeyVault/vaults@2024-04-01-preview' = {
  name: 'kv-${tenantSlug}-${uniqueString(resourceGroup().id)}'
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true
    publicNetworkAccess: 'Enabled'
  }
}

// ----------------------------------------------------------------------------
// Data plane: Postgres Flexible Server
// ----------------------------------------------------------------------------

resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2023-12-01-preview' = {
  name: 'psql-${tenantSlug}-${uniqueString(resourceGroup().id)}'
  location: location
  tags: tags
  sku: {
    name: postgresSkuName
    tier: postgresTier
  }
  properties: {
    version: '16'
    administratorLogin: postgresAdminUser
    administratorLoginPassword: postgresAdminPassword
    storage: {
      storageSizeGB: 32
    }
    backup: {
      backupRetentionDays: 14
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
  }
}

resource postgresFirewallAzure 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2023-12-01-preview' = {
  parent: postgres
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource postgresDb 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-12-01-preview' = {
  parent: postgres
  name: 'bridge'
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

// ----------------------------------------------------------------------------
// Cache: Azure Cache for Redis
// ----------------------------------------------------------------------------

resource redis 'Microsoft.Cache/redis@2024-03-01' = {
  name: 'redis-${tenantSlug}-${uniqueString(resourceGroup().id)}'
  location: location
  tags: tags
  properties: {
    sku: {
      name: redisSku
      family: redisSku == 'Premium' ? 'P' : 'C'
      capacity: redisCapacity
    }
    enableNonSslPort: false
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// ----------------------------------------------------------------------------
// Message broker: Azure Service Bus (RabbitMQ substitute)
// ----------------------------------------------------------------------------
// The Wolverine transport in Sunfish.Bridge can consume Azure Service Bus
// (via WolverineFx.AzureServiceBus); the wire-level protocol is AMQP 1.0 in
// both cases. See main.bicep header for the substitution rationale.

resource serviceBus 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: 'sb-${tenantSlug}-${uniqueString(resourceGroup().id)}'
  location: location
  tags: tags
  sku: {
    name: serviceBusSku
    tier: serviceBusSku
  }
  properties: {
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    zoneRedundant: false
  }
}

// Pre-create the topics the Bridge data plane uses for gossip-fan-out.
// Wolverine creates subscriptions dynamically from the message handlers.
var gossipTopics = [
  'bridge-relay-ingress'
  'bridge-relay-egress'
  'bridge-gossip-fanout'
  'bridge-tenant-lifecycle'
]

resource sbTopics 'Microsoft.ServiceBus/namespaces/topics@2024-01-01' = [for t in gossipTopics: {
  parent: serviceBus
  name: t
  properties: {
    enablePartitioning: false
    maxSizeInMegabytes: 1024
    defaultMessageTimeToLive: 'P7D'
  }
}]

// ----------------------------------------------------------------------------
// Container Apps Environment
// ----------------------------------------------------------------------------

resource caEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: 'cae-${tenantSlug}'
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
  }
}

// ----------------------------------------------------------------------------
// Bridge web — Container App
// ----------------------------------------------------------------------------

var dabConnStrSecretName = 'dab-postgres-connection'
var bridgeConnStrSecretName = 'bridge-postgres-connection'
var redisConnStrSecretName = 'redis-connection'
var sbConnStrSecretName = 'servicebus-connection'

var postgresFqdn = postgres.properties.fullyQualifiedDomainName
var bridgePostgresConnection = 'Host=${postgresFqdn};Database=bridge;Username=${postgresAdminUser};Password=${postgresAdminPassword};SslMode=Require'
var redisHost = redis.properties.hostName
#disable-next-line use-resource-symbol-reference
var redisAuthKey = listKeys(redis.id, '2024-03-01').primaryKey
var redisConnection = '${redisHost}:6380,password=${redisAuthKey},ssl=True,abortConnect=False'
var sbConnection = listKeys('${serviceBus.id}/AuthorizationRules/RootManageSharedAccessKey', '2024-01-01').primaryConnectionString

resource bridgeWeb 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'ca-bridge-${tenantSlug}'
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: caEnv.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
        traffic: [
          {
            weight: 100
            latestRevision: true
          }
        ]
      }
      secrets: [
        {
          name: bridgeConnStrSecretName
          value: bridgePostgresConnection
        }
        {
          name: redisConnStrSecretName
          value: redisConnection
        }
        {
          name: sbConnStrSecretName
          value: sbConnection
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'bridge'
          image: bridgeImage
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            {
              name: 'ConnectionStrings__bridge-db'
              secretRef: bridgeConnStrSecretName
            }
            {
              name: 'ConnectionStrings__cache'
              secretRef: redisConnStrSecretName
            }
            {
              name: 'ConnectionStrings__messaging'
              secretRef: sbConnStrSecretName
            }
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              value: appInsights.properties.ConnectionString
            }
            {
              name: 'SUNFISH_BRIDGE_TENANT_SLUG'
              value: tenantSlug
            }
            {
              name: 'SUNFISH_BRIDGE_KEY_VAULT_URI'
              value: kv.properties.vaultUri
            }
            {
              name: 'SUNFISH_BRIDGE_DEPLOYMENT_MODE'
              value: 'OptionB-Dedicated'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 3
      }
    }
  }
}

// Grant Bridge's system-assigned identity Key Vault Secrets User (RBAC).
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

resource kvRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(kv.id, bridgeWeb.id, keyVaultSecretsUserRoleId)
  scope: kv
  properties: {
    principalId: bridgeWeb.identity.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalType: 'ServicePrincipal'
  }
}

// ----------------------------------------------------------------------------
// DAB — Container App
// ----------------------------------------------------------------------------

resource dab 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'ca-dab-${tenantSlug}'
  location: location
  tags: tags
  properties: {
    managedEnvironmentId: caEnv.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: false
        targetPort: 5000
        transport: 'http'
      }
      secrets: [
        {
          name: dabConnStrSecretName
          value: bridgePostgresConnection
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'dab'
          image: dabImage
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            {
              name: 'dab-connection-string'
              secretRef: dabConnStrSecretName
            }
          ]
          // TODO(w5.5.1): mount dab-config.json from a Key Vault / Files
          // share. For now we assume the image has it baked in.
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
}

// ----------------------------------------------------------------------------
// Tenant-node slots — pre-allocated Container Apps
// ----------------------------------------------------------------------------
// Aspire 13.2 cannot add resources post-boot. See
// _shared/research/aspire-13-runtime-resource-mutation.md. Instead we
// pre-allocate N Container Apps; each reads a SLOT_ORDINAL env var at
// startup and pulls its TenantId assignment from Bridge's registration
// endpoint. Slots without an assignment idle at minReplicas=0 (scale-to-zero).

resource tenantNodes 'Microsoft.App/containerApps@2024-03-01' = [for i in range(0, tenantNodeSlotCount): {
  name: 'ca-node-${tenantSlug}-${padLeft(string(i), 3, '0')}'
  location: location
  tags: union(tags, {
    slotOrdinal: string(i)
  })
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: caEnv.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: false
        targetPort: 8080
        transport: 'http'
      }
    }
    template: {
      containers: [
        {
          name: 'local-node-host'
          image: localNodeImage
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            {
              name: 'SUNFISH_NODE_SLOT_ORDINAL'
              value: string(i)
            }
            {
              name: 'SUNFISH_BRIDGE_ENDPOINT'
              value: 'https://${bridgeWeb.properties.configuration.ingress.fqdn}'
            }
            {
              name: 'SUNFISH_BRIDGE_KEY_VAULT_URI'
              value: kv.properties.vaultUri
            }
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              value: appInsights.properties.ConnectionString
            }
          ]
        }
      ]
      scale: {
        // Slots scale to zero when unassigned. Bridge pokes them awake via
        // HTTP after writing the assignment to Key Vault.
        minReplicas: 0
        maxReplicas: 1
      }
    }
  }
}]

// Grant each tenant-node slot Key Vault Secrets User on the Key Vault.
resource nodeKvRoleAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for i in range(0, tenantNodeSlotCount): {
  name: guid(kv.id, tenantNodes[i].id, keyVaultSecretsUserRoleId)
  scope: kv
  properties: {
    principalId: tenantNodes[i].identity.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalType: 'ServicePrincipal'
  }
}]

// ----------------------------------------------------------------------------
// Outputs
// ----------------------------------------------------------------------------

output bridgeFqdn string = bridgeWeb.properties.configuration.ingress.fqdn
output keyVaultUri string = kv.properties.vaultUri
output serviceBusNamespace string = serviceBus.name
output postgresServerName string = postgres.name
output logAnalyticsWorkspaceId string = logs.id
