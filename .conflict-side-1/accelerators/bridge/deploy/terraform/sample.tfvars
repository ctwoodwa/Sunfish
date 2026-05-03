#############################################################################
# Bridge Option B — sample Terraform variables
#
# Copy to my.auto.tfvars and edit for your contract. DO NOT commit filled-in
# values to VCS.
#############################################################################

tenant_slug = "acmedemo"

region = "us-east-1"

tenant_count = 4

# Customer-published images. These examples assume a GHCR org; swap for ECR
# / Quay / Docker Hub as appropriate.
bridge_image     = "ghcr.io/YOUR-ORG/sunfish-bridge:latest"
local_node_image = "ghcr.io/YOUR-ORG/sunfish-local-node-host:latest"

# DAB pinned to the shared-Bridge AppHost version
dab_image = "mcr.microsoft.com/azure-databases/data-api-builder:1.7.90"

# Sizing: db.t4g.small + cache.t4g.small is a reasonable starting point for
# 1-4 teams. Scale up for heavier workloads.
rds_instance_class = "db.t4g.small"
rds_size_gb        = 32
redis_node_type    = "cache.t4g.small"

extra_tags = {
  contract    = "CONTRACT-ID-HERE"
  owner       = "ops@example.com"
  cost_center = "CC-1234"
}
