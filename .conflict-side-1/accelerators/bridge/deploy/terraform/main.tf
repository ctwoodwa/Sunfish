#############################################################################
# Bridge — Option B dedicated-deployment — Terraform (AWS primary)
#############################################################################
#
# Cloud-agnostic in intent; AWS-primary in implementation because that's the
# most-requested enterprise cloud for Bridge contracts. Forks for GCP / Oracle
# / Alibaba should swap the provider block + the ecs/rds/elasticache/secrets
# resources but leave main.tf's orchestration intact.
#
# See ADR 0031 Option B for the "why". See
# _shared/research/aspire-13-runtime-resource-mutation.md for the
# pre-allocated-slot pattern.

terraform {
  required_version = ">= 1.7.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = ">= 5.0"
    }
    random = {
      source  = "hashicorp/random"
      version = ">= 3.5"
    }
  }
}

provider "aws" {
  region = var.region

  default_tags {
    tags = merge(
      {
        deployment  = "bridge-option-b"
        tenant_slug = var.tenant_slug
        managed_by  = "terraform"
      },
      var.extra_tags,
    )
  }
}

locals {
  name_prefix = "bridge-${var.tenant_slug}"
}

#############################################################################
# VPC + networking — sized for up to 32 tenant slots
#############################################################################

data "aws_availability_zones" "available" {
  state = "available"
}

resource "aws_vpc" "main" {
  cidr_block           = "10.42.0.0/16"
  enable_dns_support   = true
  enable_dns_hostnames = true

  tags = {
    Name = "${local.name_prefix}-vpc"
  }
}

resource "aws_subnet" "public" {
  count                   = 2
  vpc_id                  = aws_vpc.main.id
  cidr_block              = cidrsubnet(aws_vpc.main.cidr_block, 8, count.index)
  availability_zone       = data.aws_availability_zones.available.names[count.index]
  map_public_ip_on_launch = true

  tags = {
    Name = "${local.name_prefix}-public-${count.index}"
    Tier = "public"
  }
}

resource "aws_subnet" "private" {
  count             = 2
  vpc_id            = aws_vpc.main.id
  cidr_block        = cidrsubnet(aws_vpc.main.cidr_block, 8, count.index + 10)
  availability_zone = data.aws_availability_zones.available.names[count.index]

  tags = {
    Name = "${local.name_prefix}-private-${count.index}"
    Tier = "private"
  }
}

resource "aws_internet_gateway" "main" {
  vpc_id = aws_vpc.main.id

  tags = {
    Name = "${local.name_prefix}-igw"
  }
}

resource "aws_route_table" "public" {
  vpc_id = aws_vpc.main.id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.main.id
  }

  tags = {
    Name = "${local.name_prefix}-public-rt"
  }
}

resource "aws_route_table_association" "public" {
  count          = length(aws_subnet.public)
  subnet_id      = aws_subnet.public[count.index].id
  route_table_id = aws_route_table.public.id
}

# TODO(w5.5.2): NAT gateway for private-subnet egress. Parked because tenant-
# node slots currently dial out only to Bridge's internal DNS (same VPC) plus
# the relay on a public endpoint; a NAT is required once slots need to reach
# external APIs (e.g. WebAuthn attestation vendors).

#############################################################################
# Security groups
#############################################################################

resource "aws_security_group" "alb" {
  name        = "${local.name_prefix}-alb-sg"
  description = "Bridge ALB ingress"
  vpc_id      = aws_vpc.main.id

  ingress {
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
    description = "HTTPS from the internet"
  }

  ingress {
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
    description = "HTTP (redirect to HTTPS)"
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

resource "aws_security_group" "tasks" {
  name        = "${local.name_prefix}-tasks-sg"
  description = "Bridge ECS tasks"
  vpc_id      = aws_vpc.main.id

  ingress {
    from_port       = 8080
    to_port         = 8080
    protocol        = "tcp"
    security_groups = [aws_security_group.alb.id]
    description     = "From ALB"
  }

  # Tasks talk to each other intra-SG (Bridge -> tenant-node, etc.)
  ingress {
    from_port = 0
    to_port   = 0
    protocol  = "-1"
    self      = true
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

resource "aws_security_group" "rds" {
  name        = "${local.name_prefix}-rds-sg"
  description = "Postgres"
  vpc_id      = aws_vpc.main.id

  ingress {
    from_port       = 5432
    to_port         = 5432
    protocol        = "tcp"
    security_groups = [aws_security_group.tasks.id]
  }
}

resource "aws_security_group" "elasticache" {
  name        = "${local.name_prefix}-cache-sg"
  description = "Redis"
  vpc_id      = aws_vpc.main.id

  ingress {
    from_port       = 6379
    to_port         = 6379
    protocol        = "tcp"
    security_groups = [aws_security_group.tasks.id]
  }
}

#############################################################################
# Postgres (RDS)
#############################################################################

resource "random_password" "postgres" {
  length  = 32
  special = true
  # RDS doesn't accept these in passwords:
  override_special = "!#$%&*+-.=?@^_"
}

resource "aws_db_subnet_group" "main" {
  name       = "${local.name_prefix}-rds-subnets"
  subnet_ids = aws_subnet.private[*].id
}

resource "aws_db_instance" "postgres" {
  identifier        = "${local.name_prefix}-pg"
  engine            = "postgres"
  engine_version    = "16.4"
  instance_class    = var.rds_instance_class
  allocated_storage = var.rds_size_gb

  db_name  = "bridge"
  username = "bridgeadmin"
  password = random_password.postgres.result

  db_subnet_group_name   = aws_db_subnet_group.main.name
  vpc_security_group_ids = [aws_security_group.rds.id]

  backup_retention_period = 14
  backup_window           = "03:00-04:00"
  maintenance_window      = "sun:04:00-sun:05:00"
  deletion_protection     = false
  skip_final_snapshot     = true

  performance_insights_enabled = true
  storage_encrypted            = true

  tags = {
    Name = "${local.name_prefix}-pg"
  }
}

#############################################################################
# Redis (ElastiCache)
#############################################################################

resource "aws_elasticache_subnet_group" "main" {
  name       = "${local.name_prefix}-cache-subnets"
  subnet_ids = aws_subnet.private[*].id
}

resource "random_password" "redis_auth" {
  length  = 32
  special = false
}

resource "aws_elasticache_replication_group" "main" {
  replication_group_id = "${local.name_prefix}-redis"
  description          = "Bridge Redis for ${var.tenant_slug}"

  engine         = "redis"
  engine_version = "7.1"
  node_type      = var.redis_node_type
  num_cache_clusters = 1
  port                       = 6379

  subnet_group_name  = aws_elasticache_subnet_group.main.name
  security_group_ids = [aws_security_group.elasticache.id]

  at_rest_encryption_enabled = true
  transit_encryption_enabled = true
  auth_token                 = random_password.redis_auth.result

  apply_immediately = true
}

#############################################################################
# Secrets Manager — install root-seed slot + generated connection strings
#############################################################################

resource "aws_secretsmanager_secret" "root_seed" {
  name        = "${local.name_prefix}-root-seed"
  description = "Install-level IRootSeedProvider wrapped seed. Write the wrapped seed bytes here before first Bridge boot."
  # 90-day recovery window for production; 7 days for demo.
  recovery_window_in_days = 7
}

resource "aws_secretsmanager_secret" "postgres_conn" {
  name                    = "${local.name_prefix}-postgres-conn"
  description             = "Bridge Postgres connection string"
  recovery_window_in_days = 7
}

resource "aws_secretsmanager_secret_version" "postgres_conn" {
  secret_id     = aws_secretsmanager_secret.postgres_conn.id
  secret_string = "Host=${aws_db_instance.postgres.address};Port=5432;Database=bridge;Username=bridgeadmin;Password=${random_password.postgres.result};SslMode=Require"
}

resource "aws_secretsmanager_secret" "redis_conn" {
  name                    = "${local.name_prefix}-redis-conn"
  description             = "Bridge Redis connection string"
  recovery_window_in_days = 7
}

resource "aws_secretsmanager_secret_version" "redis_conn" {
  secret_id     = aws_secretsmanager_secret.redis_conn.id
  secret_string = "${aws_elasticache_replication_group.main.primary_endpoint_address}:6379,password=${random_password.redis_auth.result},ssl=True,abortConnect=False"
}

# TODO(w5.5.3): SQS / Amazon MQ (ActiveMQ or RabbitMQ) is the AWS substitute
# for Service Bus. The Wolverine transport works with all three; pick based
# on the customer's existing pattern. For now we ship Amazon MQ RabbitMQ
# because it preserves message-envelope parity with the shared-Bridge
# AppHost. Fleshing out the MQ cluster + its secret is a follow-up.

#############################################################################
# ECS cluster + Fargate services
#############################################################################

resource "aws_ecs_cluster" "main" {
  name = "${local.name_prefix}-cluster"

  setting {
    name  = "containerInsights"
    value = "enabled"
  }
}

resource "aws_cloudwatch_log_group" "bridge" {
  name              = "/ecs/${local.name_prefix}"
  retention_in_days = 30
}

data "aws_caller_identity" "current" {}
data "aws_region" "current" {}

resource "aws_iam_role" "task_exec" {
  name = "${local.name_prefix}-task-exec"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action = "sts:AssumeRole"
      Effect = "Allow"
      Principal = {
        Service = "ecs-tasks.amazonaws.com"
      }
    }]
  })
}

resource "aws_iam_role_policy_attachment" "task_exec_basic" {
  role       = aws_iam_role.task_exec.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}

resource "aws_iam_policy" "task_secrets_read" {
  name = "${local.name_prefix}-task-secrets-read"
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect = "Allow"
      Action = [
        "secretsmanager:GetSecretValue",
        "secretsmanager:DescribeSecret",
      ]
      Resource = [
        aws_secretsmanager_secret.root_seed.arn,
        aws_secretsmanager_secret.postgres_conn.arn,
        aws_secretsmanager_secret.redis_conn.arn,
      ]
    }]
  })
}

resource "aws_iam_role_policy_attachment" "task_exec_secrets" {
  role       = aws_iam_role.task_exec.name
  policy_arn = aws_iam_policy.task_secrets_read.arn
}

resource "aws_iam_role" "task" {
  name = "${local.name_prefix}-task"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action = "sts:AssumeRole"
      Effect = "Allow"
      Principal = {
        Service = "ecs-tasks.amazonaws.com"
      }
    }]
  })
}

resource "aws_iam_role_policy_attachment" "task_secrets" {
  role       = aws_iam_role.task.name
  policy_arn = aws_iam_policy.task_secrets_read.arn
}

#############################################################################
# Bridge web service
#############################################################################

resource "aws_ecs_task_definition" "bridge" {
  family                   = "${local.name_prefix}-bridge"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = "512"
  memory                   = "1024"
  execution_role_arn       = aws_iam_role.task_exec.arn
  task_role_arn            = aws_iam_role.task.arn

  container_definitions = jsonencode([{
    name  = "bridge"
    image = var.bridge_image
    portMappings = [{
      containerPort = 8080
      protocol      = "tcp"
    }]
    environment = [
      { name = "SUNFISH_BRIDGE_TENANT_SLUG", value = var.tenant_slug },
      { name = "SUNFISH_BRIDGE_DEPLOYMENT_MODE", value = "OptionB-Dedicated" },
      { name = "SUNFISH_BRIDGE_ROOT_SEED_SECRET_ARN", value = aws_secretsmanager_secret.root_seed.arn },
      { name = "AWS_REGION", value = var.region },
    ]
    secrets = [
      { name = "ConnectionStrings__bridge-db", valueFrom = aws_secretsmanager_secret.postgres_conn.arn },
      { name = "ConnectionStrings__cache", valueFrom = aws_secretsmanager_secret.redis_conn.arn },
    ]
    logConfiguration = {
      logDriver = "awslogs"
      options = {
        "awslogs-group"         = aws_cloudwatch_log_group.bridge.name
        "awslogs-region"        = var.region
        "awslogs-stream-prefix" = "bridge"
      }
    }
    essential = true
  }])
}

resource "aws_lb" "main" {
  name               = "${local.name_prefix}-alb"
  internal           = false
  load_balancer_type = "application"
  security_groups    = [aws_security_group.alb.id]
  subnets            = aws_subnet.public[*].id
}

resource "aws_lb_target_group" "bridge" {
  name        = "${local.name_prefix}-bridge-tg"
  port        = 8080
  protocol    = "HTTP"
  target_type = "ip"
  vpc_id      = aws_vpc.main.id

  health_check {
    path                = "/health/live"
    interval            = 30
    timeout             = 5
    healthy_threshold   = 2
    unhealthy_threshold = 3
  }
}

# TODO(w5.5.4): HTTPS listener requires an ACM cert. For demo we expose HTTP
# only — production deployments must terminate TLS at the ALB. Pre-flight
# your ACM cert ARN and swap in a `aws_lb_listener` with protocol=HTTPS.
resource "aws_lb_listener" "http" {
  load_balancer_arn = aws_lb.main.arn
  port              = 80
  protocol          = "HTTP"

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.bridge.arn
  }
}

resource "aws_ecs_service" "bridge" {
  name            = "${local.name_prefix}-bridge"
  cluster         = aws_ecs_cluster.main.id
  task_definition = aws_ecs_task_definition.bridge.arn
  desired_count   = 1
  launch_type     = "FARGATE"

  network_configuration {
    subnets          = aws_subnet.private[*].id
    security_groups  = [aws_security_group.tasks.id]
    assign_public_ip = false
  }

  load_balancer {
    target_group_arn = aws_lb_target_group.bridge.arn
    container_name   = "bridge"
    container_port   = 8080
  }

  depends_on = [aws_lb_listener.http]
}

#############################################################################
# DAB service (mcr.microsoft.com/azure-databases/data-api-builder:1.7.90)
#############################################################################

resource "aws_ecs_task_definition" "dab" {
  family                   = "${local.name_prefix}-dab"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = "256"
  memory                   = "512"
  execution_role_arn       = aws_iam_role.task_exec.arn
  task_role_arn            = aws_iam_role.task.arn

  container_definitions = jsonencode([{
    name  = "dab"
    image = var.dab_image
    portMappings = [{
      containerPort = 5000
      protocol      = "tcp"
    }]
    secrets = [
      { name = "dab-connection-string", valueFrom = aws_secretsmanager_secret.postgres_conn.arn },
    ]
    logConfiguration = {
      logDriver = "awslogs"
      options = {
        "awslogs-group"         = aws_cloudwatch_log_group.bridge.name
        "awslogs-region"        = var.region
        "awslogs-stream-prefix" = "dab"
      }
    }
    essential = true
  }])
}

resource "aws_ecs_service" "dab" {
  name            = "${local.name_prefix}-dab"
  cluster         = aws_ecs_cluster.main.id
  task_definition = aws_ecs_task_definition.dab.arn
  desired_count   = 1
  launch_type     = "FARGATE"

  network_configuration {
    subnets          = aws_subnet.private[*].id
    security_groups  = [aws_security_group.tasks.id]
    assign_public_ip = false
  }
}

#############################################################################
# Pre-allocated tenant-node slots (Fargate tasks — one per slot)
#############################################################################
# See _shared/research/aspire-13-runtime-resource-mutation.md.
# Each slot gets SUNFISH_NODE_SLOT_ORDINAL=i at startup; the local-node-host
# entrypoint reads that ordinal and calls Bridge's assignment endpoint to
# resolve "which TeamId am I serving right now?"

resource "aws_ecs_task_definition" "tenant_node" {
  count                    = var.tenant_count
  family                   = "${local.name_prefix}-node-${format("%03d", count.index)}"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = "256"
  memory                   = "512"
  execution_role_arn       = aws_iam_role.task_exec.arn
  task_role_arn            = aws_iam_role.task.arn

  container_definitions = jsonencode([{
    name  = "local-node-host"
    image = var.local_node_image
    portMappings = [{
      containerPort = 8080
      protocol      = "tcp"
    }]
    environment = [
      { name = "SUNFISH_NODE_SLOT_ORDINAL", value = tostring(count.index) },
      { name = "SUNFISH_BRIDGE_ENDPOINT", value = "http://${aws_lb.main.dns_name}" },
      { name = "SUNFISH_BRIDGE_ROOT_SEED_SECRET_ARN", value = aws_secretsmanager_secret.root_seed.arn },
      { name = "AWS_REGION", value = var.region },
    ]
    logConfiguration = {
      logDriver = "awslogs"
      options = {
        "awslogs-group"         = aws_cloudwatch_log_group.bridge.name
        "awslogs-region"        = var.region
        "awslogs-stream-prefix" = "node-${format("%03d", count.index)}"
      }
    }
    essential = true
  }])
}

resource "aws_ecs_service" "tenant_node" {
  count           = var.tenant_count
  name            = "${local.name_prefix}-node-${format("%03d", count.index)}"
  cluster         = aws_ecs_cluster.main.id
  task_definition = aws_ecs_task_definition.tenant_node[count.index].arn
  # Start slots idle (desired=0) — Bridge scales them up on tenant assignment
  # via the AWS API. For a first cut we start 1-of-N as a smoke test; production
  # should drive this via a lightweight scaler Lambda triggered by Bridge.
  desired_count = count.index == 0 ? 1 : 0
  launch_type   = "FARGATE"

  network_configuration {
    subnets          = aws_subnet.private[*].id
    security_groups  = [aws_security_group.tasks.id]
    assign_public_ip = false
  }

  # TODO(w5.5.5): wire a ServiceDiscovery namespace so Bridge can dial slots
  # by DNS instead of fetching task IPs from the ECS API. Parked for a follow-up.
}
