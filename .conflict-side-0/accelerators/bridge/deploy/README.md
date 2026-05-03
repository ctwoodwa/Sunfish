# Bridge dedicated-deployment packaging (Option B)

> Infrastructure-as-Code templates for spinning up a **dedicated** Bridge stack per
> enterprise contract, per [ADR 0031](../../../docs/adrs/0031-bridge-hybrid-multi-tenant-saas.md)'s
> Option B escape hatch.

---

## What is Option B?

Bridge's default deployment is **Option C (Zone-C Hybrid multi-tenant)**: a single shared
Bridge stack serves many tenants, each with an isolated per-tenant `local-node-host`
child process, sharing the control plane (Postgres/Redis/Rabbit/DAB), the ingress shell,
and the relay tier. Operator-visible data is ciphertext-only per paper §17.2, and the CP
quorum never includes the operator by default.

That posture covers the vast majority of commercial use. But some enterprise contracts
require that **even the ciphertext** not sit on shared infrastructure — typically driven
by regulators who treat "it's all ciphertext" as insufficient and insist on a physical
boundary. Option B serves that audience.

**Under Option B, the tenant gets their own full Bridge stack:**

- Their own Postgres Flexible Server / RDS / StatefulSet
- Their own Redis / ElastiCache / StatefulSet
- Their own RabbitMQ (or Azure Service Bus — see Bicep README for the protocol swap)
- Their own Bridge web app (`Sunfish.Bridge`)
- Their own Data API Builder instance (`mcr.microsoft.com/azure-databases/data-api-builder:1.7.90`)
- N pre-allocated tenant-node slots (one per team inside the contract's organization —
  typically 1, but enterprises may contract for multi-team isolation inside the
  dedicated stack)
- Their own observability plane (Log Analytics / CloudWatch / Prometheus)
- Their own DNS subdomain (e.g. `acme.bridge.example.com`)

**Same codebase. Same wire protocol.** The Anchor workstations gossiping with an Option-B
tenant see identical traffic to an Option-C tenant — the paper's wire format is
posture-agnostic.

---

## When to use Option B

Pick Option B when **any** of these apply:

1. The contract's data-residency requirements (HIPAA, FedRAMP, CJIS, SEC 17a-4, Schrems-II,
   regional sovereignty) mandate that the ciphertext bytes never touch shared infrastructure.
2. The auditor explicitly rejects "ciphertext on shared infra" as a control boundary.
3. The customer demands a named-region deployment where the operator's default region does
   not comply.
4. The customer needs version-pinning or feature-branch isolation (rare, but possible).
5. The customer contracts for a hard blast-radius boundary: "if another tenant has an
   incident, we must not be in the same failure domain."

**If none of the above apply**, keep the customer on Option C — it's cheaper, it's simpler
to operate, and the paper-level invariants still hold (ciphertext-only operator, per-tenant
CP/AP position, no operator in CP quorum by default).

---

## Choice framework — which IaC variant?

| Customer posture | Recommended variant | Why |
|---|---|---|
| Already on Azure (AAD, Azure Monitor, Azure AD B2C) | [`bicep/`](./bicep/) | Native Azure services; Service Bus replaces RabbitMQ; Key Vault replaces DPAPI for the install seed; lowest friction inside an Azure estate. |
| Already on AWS (IAM, CloudWatch, SSM) | [`terraform/`](./terraform/) | ECS/Fargate + RDS + ElastiCache + Secrets Manager; cloud-agnostic module but AWS-primary. |
| On-prem, customer-managed Kubernetes, or air-gapped | [`k8s/`](./k8s/) | Kustomize-ready manifests; bring-your-own Ingress controller; sealed-secrets for the install seed. |
| Multi-cloud / GCP / Oracle / other | [`terraform/`](./terraform/) + adapt provider | Terraform is the lowest-friction starting point; fork the AWS module and swap providers. See the TODO markers in `terraform/main.tf`. |

All three variants converge on the same runtime topology; the IaC flavor determines the
managed-services substitution (Service Bus vs. RabbitMQ, Key Vault vs. Secrets Manager vs.
sealed-secrets, etc.).

---

## Pre-allocated tenant-node slot pattern

Aspire 13.2 **cannot** add resources to a running `DistributedApplication` graph — the
graph is sealed at `builder.Build()`. See
[`_shared/research/aspire-13-runtime-resource-mutation.md`](../../../_shared/research/aspire-13-runtime-resource-mutation.md)
for the full finding.

The blessed workaround, which we've adopted across all three variants, is
**pre-allocated slots**: at IaC deployment time you declare N tenant-node slots
(Container App Jobs on Azure, Fargate tasks on AWS, StatefulSet replicas on k8s), and
each slot is assigned to a team at runtime via a `POD_ORDINAL → TeamId` mapping at
container entrypoint. Unused slots sit idle at near-zero cost (the container image is
small and idle CPU/RAM usage is minimal) but are ready to be "claimed" by a new team
registration without redeploying infra.

**Rule of thumb:** for a dedicated Option-B deployment, allocate **2× the expected
team count** as slots. Cost of idle slots is a rounding error; cost of an urgent infra
redeploy during a customer onboarding call is not.

---

## Sizing guidance

Per-tenant-slot idle footprint (measured on a reference Hetzner CX21 VM — 2 vCPU / 4 GB):

| Resource | Idle | Active (small team, <20 events/min) |
|---|---|---|
| RAM per tenant slot | ~256 MB | ~400 MB |
| vCPU per tenant slot | ~0.05 | ~0.25 |
| Disk (SQLCipher event log) | 10 MB seed | 1-5 GB after 1 yr typical team |
| Network egress | ~1 KB/s gossip | ~10 KB/s peak during catch-up |

**Budget 2× active footprint for health-margin**. A dedicated stack supporting 8 teams
should be provisioned for ~6.4 GB RAM and 4 vCPU worth of tenant-node capacity, plus
the Bridge web / DAB / Postgres / Redis / Service Bus overhead (~2 GB RAM / 1 vCPU
combined at low load).

**Typical starter footprint** for a dedicated stack with 1-4 teams:

- Bridge web: 512 MB / 0.5 vCPU
- DAB: 256 MB / 0.25 vCPU
- Postgres: 2 GB / 1 vCPU (burstable tier is fine for <4 teams)
- Redis: 512 MB / 0.25 vCPU
- Rabbit/ServiceBus: 512 MB / 0.25 vCPU (or managed Service Bus Basic tier)
- 8 tenant slots (2× of 4 teams): ~2 GB / 0.4 vCPU idle, ~3.2 GB / 2 vCPU active
- **Total: ~6 GB / 2.7 vCPU idle, ~7 GB / 4.3 vCPU active**

That fits inside a single Azure Container App Environment Consumption plan, a single
`t3.large` / `m6i.large` Fargate task group, or a single 3-node k8s cluster with
modest per-node specs (2 vCPU / 4 GB).

---

## Prerequisites

Common to all variants:

- Published container image for `Sunfish.Bridge` (the customer publishes to their own
  registry — the operator does not ship a binary; there is no `mcr.microsoft.com/sunfish/bridge` image).
- Published container image for `apps/local-node-host` (same — customer-published).
- DAB image: `mcr.microsoft.com/azure-databases/data-api-builder:1.7.90` (public; pulled
  directly by all three variants).
- The tenant admin's root-seed pre-provisioning strategy agreed (Key Vault /
  Secrets Manager / sealed-secret / external-secrets operator).
- DNS subdomain pointed at the ingress endpoint.

Variant-specific prerequisites are documented in each variant's README.

---

## See also

- [ADR 0031](../../../docs/adrs/0031-bridge-hybrid-multi-tenant-saas.md) — Bridge as Hybrid Multi-Tenant SaaS (Option B section lines 55-64, 118-125)
- [Aspire 13.2 runtime resource mutation research](../../../_shared/research/aspire-13-runtime-resource-mutation.md) — why pre-allocated slots
- [`accelerators/bridge/PLATFORM_ALIGNMENT.md`](../PLATFORM_ALIGNMENT.md) — reference alignment between shared-Bridge AppHost and Option B IaC
- [Wave 5.5 intake](../../../_shared/product/paper-alignment-plan.md) — paper-alignment-plan line 128
