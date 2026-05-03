#############################################################################
# Bridge Option B — Terraform variables
#############################################################################

variable "tenant_slug" {
  description = "Short tenant identifier (3-16 chars, lowercase). Used as a resource name prefix."
  type        = string

  validation {
    condition     = can(regex("^[a-z0-9]{3,16}$", var.tenant_slug))
    error_message = "tenant_slug must be 3-16 lowercase alphanumeric characters."
  }
}

variable "region" {
  description = "AWS region for the entire stack. Everything is single-region for Option B."
  type        = string
  default     = "us-east-1"
}

variable "tenant_count" {
  description = "Number of pre-allocated tenant-node slots. Rule of thumb: 2x expected team count. Max 32 with the default /16 VPC + /24 subnets."
  type        = number
  default     = 8

  validation {
    condition     = var.tenant_count > 0 && var.tenant_count <= 32
    error_message = "tenant_count must be between 1 and 32."
  }
}

variable "bridge_image" {
  description = "Bridge web container image (full registry/repo:tag). Customer-published."
  type        = string
}

variable "local_node_image" {
  description = "local-node-host container image (full registry/repo:tag). Customer-published."
  type        = string
}

variable "dab_image" {
  description = "DAB container image. Default matches the shared-Bridge AppHost pin."
  type        = string
  default     = "mcr.microsoft.com/azure-databases/data-api-builder:1.7.90"
}

variable "rds_instance_class" {
  description = "RDS instance class. db.t4g.small covers 1-4 teams; scale up for larger dedicated deployments."
  type        = string
  default     = "db.t4g.small"
}

variable "rds_size_gb" {
  description = "RDS allocated storage (GB)."
  type        = number
  default     = 32
}

variable "redis_node_type" {
  description = "ElastiCache node type."
  type        = string
  default     = "cache.t4g.small"
}

variable "extra_tags" {
  description = "Additional tags merged into the provider's default_tags. Include contract id, owner, cost center."
  type        = map(string)
  default     = {}
}
