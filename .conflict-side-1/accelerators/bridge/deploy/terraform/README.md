# Bridge Option B — Terraform (AWS-primary)

Cloud-agnostic-in-intent Terraform module for spinning up a dedicated Bridge stack
per ADR 0031 Option B. AWS-primary in the current shape; see "Adapting for other
clouds" at the bottom.

## What it provisions

| Resource | AWS service | Purpose |
|---|---|---|
| VPC + 2 public / 2 private subnets | VPC | 2-AZ, /16, sized for up to 32 tenant slots |
| Postgres | RDS (PG 16) | Bridge control-plane tables + migrations |
| Redis | ElastiCache | SignalR backplane, tenant-state cache |
| Broker | **Amazon MQ RabbitMQ** (TODO — see below) | Wolverine transport — same envelopes as the shared-Bridge AppHost |
| ECS cluster | ECS on Fargate | Runs Bridge web + DAB + tenant-node slots |
| Bridge web service | ECS service (desired=1) | Runs the `Sunfish.Bridge` image |
| DAB service | ECS service (desired=1) | Runs `mcr.microsoft.com/azure-databases/data-api-builder:1.7.90` |
| Tenant-node slots | ECS task definitions × N | Pre-allocated `local-node-host` slots (default N=8); first starts at desired=1 for smoke, rest at desired=0 |
| Secrets | Secrets Manager | Install root-seed, Postgres conn, Redis conn |
| ALB | Application Load Balancer | Public ingress to Bridge web |
| Logs | CloudWatch Logs | 30-day retention |

## Prerequisites

1. AWS account + credentials with Admin (or a scoped role covering VPC, RDS, ElastiCache,
   ECS, IAM, ALB, Secrets Manager, CloudWatch Logs).
2. Terraform 1.7+ (`terraform version`).
3. AWS provider 5+ (pulled via `terraform init`).
4. Bridge + local-node-host container images published to a registry that Fargate can pull
   from (ECR is cleanest; GHCR/Docker-Hub public works too).

## Deploy

```bash
# Initialize
terraform init

# Copy and edit the sample tfvars
cp sample.tfvars my.auto.tfvars
# Edit my.auto.tfvars — set tenant_slug, image URIs, etc.

# Plan + apply
terraform plan
terraform apply
```

## Post-deployment

1. Terraform prints `bridge_endpoint_url` — map your DNS record (e.g.
   `acme.bridge.example.com`) to the ALB DNS name via CNAME.
2. Seed the root-seed secret:
   ```bash
   ROOT_SEED_ARN=$(terraform output -raw root_seed_secret_arn)
   aws secretsmanager put-secret-value \
     --secret-id "$ROOT_SEED_ARN" \
     --secret-binary fileb:///path/to/wrapped-root-seed.bin
   ```
3. Force the Bridge service to restart so it picks up the seed:
   ```bash
   aws ecs update-service \
     --cluster bridge-acmedemo-cluster \
     --service bridge-acmedemo-bridge \
     --force-new-deployment
   ```

## Tear down

```bash
terraform destroy
```

Secrets Manager enters a 7-day recovery window by default (configured in `main.tf`);
production deployments should bump that to 30-90 days.

## Known TODOs

- **`TODO(w5.5.2)`**: NAT gateway for private-subnet egress. Parked until slots need to
  reach external APIs (WebAuthn attestation vendors, etc.).
- **`TODO(w5.5.3)`**: Message broker. The current shape references Amazon MQ RabbitMQ for
  envelope parity with the shared-Bridge AppHost, but the MQ cluster + its secret are not
  yet provisioned — TODO follow-up pass. For a true AWS-native posture, consider swapping
  to SQS + SNS (no rebuild required for Wolverine, but wire-level semantics differ).
- **`TODO(w5.5.4)`**: HTTPS listener + ACM cert. Demo ships HTTP-only via the ALB; production
  must terminate TLS at the ALB. Pre-flight your ACM cert ARN and add an `aws_lb_listener`
  resource with `protocol = "HTTPS"` + `ssl_policy`.
- **`TODO(w5.5.5)`**: ServiceDiscovery namespace so Bridge dials tenant-node slots by DNS
  instead of the ECS API.
- **Tenant-node scaling**: the current module starts slot 0 at `desired_count=1` and the
  rest at 0. A production deployment needs a small Lambda that reacts to Bridge's tenant-
  assignment events and bumps `desired_count` for the claimed slot. Parked as a follow-up.

## Adapting for other clouds

The module is structured so you can fork and retarget:

- **GCP**: swap `aws` → `google`; `aws_ecs_*` → `google_cloud_run_v2_service`; `aws_db_instance` → `google_sql_database_instance`; `aws_elasticache_*` → `google_redis_instance`; `aws_secretsmanager_*` → `google_secret_manager_secret`.
- **Oracle Cloud**: swap to the `oci` provider; Fargate equivalent is OKE.
- **Alibaba / Tencent**: equivalent resources exist in their Terraform providers.

In every case, `main.tf`'s orchestration (create broker, create DB, create cache, create N
slot definitions, create Bridge service) stays the same — only the resource types change.
