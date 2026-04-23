#############################################################################
# Bridge Option B — Terraform outputs
#############################################################################

output "bridge_endpoint_url" {
  description = "Public URL of the Bridge web service. Map a CNAME here from your tenant subdomain."
  value       = "http://${aws_lb.main.dns_name}"
  # TODO(w5.5.4): switch to https:// once the ACM cert listener is wired.
}

output "bridge_alb_dns_name" {
  description = "Raw ALB DNS name."
  value       = aws_lb.main.dns_name
}

output "postgres_connection_string_secret_arn" {
  description = "ARN of the Secrets Manager entry holding the Postgres connection string."
  value       = aws_secretsmanager_secret.postgres_conn.arn
}

output "postgres_connection_string" {
  description = "Postgres connection string (sensitive). Prefer reading via the Secrets Manager ARN."
  value       = "Host=${aws_db_instance.postgres.address};Port=5432;Database=bridge;Username=bridgeadmin;Password=${random_password.postgres.result};SslMode=Require"
  sensitive   = true
}

output "redis_endpoint" {
  description = "Redis primary endpoint host."
  value       = aws_elasticache_replication_group.main.primary_endpoint_address
}

output "root_seed_secret_arn" {
  description = "ARN of the empty install-level root-seed slot. Write the wrapped seed bytes here before first Bridge boot."
  value       = aws_secretsmanager_secret.root_seed.arn
}

output "first_two_tenant_node_families" {
  description = "Task-definition families for the first two pre-allocated tenant-node slots. Useful for smoke testing."
  value       = var.tenant_count >= 2 ? [aws_ecs_task_definition.tenant_node[0].family, aws_ecs_task_definition.tenant_node[1].family] : [aws_ecs_task_definition.tenant_node[0].family]
}

output "ecs_cluster_arn" {
  description = "ECS cluster ARN — useful for operator tooling that scales tenant-node slots."
  value       = aws_ecs_cluster.main.arn
}

output "vpc_id" {
  description = "VPC ID hosting the Bridge stack."
  value       = aws_vpc.main.id
}
