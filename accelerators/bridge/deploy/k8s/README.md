# Bridge Option B — Kubernetes manifests

Kustomize-ready manifests for spinning up a dedicated Bridge stack per
ADR 0031 Option B on a customer-managed Kubernetes cluster (on-prem, EKS, GKE,
AKS, k3s, OpenShift — any CNCF-conformant flavor).

## What it provisions

| File | Resource | Purpose |
|---|---|---|
| `00-namespace.yaml` | Namespace | `sunfish-bridge-{tenantSlug}` — isolation boundary |
| `01-secrets.yaml.template` | Secrets (template) | Install seed, Postgres, Redis, Rabbit creds — seal or externalize before apply |
| `10-postgres.yaml` | StatefulSet + Service | Postgres 16 (single replica) |
| `11-redis.yaml` | StatefulSet + Service | Redis 7 (single replica) |
| `12-rabbit.yaml` | StatefulSet + Service | RabbitMQ 3.13 with management plugin |
| `20-bridge-web.yaml` | Deployment + Service + Ingress | Bridge web app |
| `21-bridge-dab.yaml` | Deployment + Service | DAB 1.7.90 |
| `30-tenant-nodes.yaml` | StatefulSet + Service | Pre-allocated tenant-node slots (default 8 replicas) |
| `40-network-policies.yaml` | NetworkPolicies | Default-deny ingress + egress + allow-intra-ns |
| `kustomization.yaml` | Kustomize base | Ties everything together + image overrides |

## Prerequisites

1. **Kubernetes 1.28+** (earlier versions are missing the `matchLabelKeys` and `sleep`
   lifecycle niceties StatefulSets benefit from).
2. **An Ingress controller** — Ingress-NGINX is assumed (`ingressClassName: nginx`);
   swap annotations for Traefik / Contour / HAProxy as needed.
3. **A NetworkPolicy-capable CNI** — Calico, Cilium, Antrea, or Weave Net with policy.
   Plain Flannel does not enforce NetworkPolicies and leaves `40-network-policies.yaml`
   as advisory-only.
4. **One of:**
   - **Sealed Secrets controller** (https://sealed-secrets.netlify.app/) for committing
     the seed + connection strings; OR
   - **External Secrets Operator** (https://external-secrets.io/) pointed at the
     operator's vault (HashiCorp Vault, Azure Key Vault, AWS Secrets Manager, GCP SM).
   - (Do **not** commit raw `01-secrets.yaml` to VCS with live values.)
5. **cert-manager** (recommended) for automating the ingress TLS cert via Let's Encrypt,
   ACME DNS, or your enterprise CA.
6. **Customer-published container images** for:
   - `sunfish-bridge`
   - `sunfish-local-node-host`
   - (DAB is pulled from `mcr.microsoft.com/azure-databases/data-api-builder:1.7.90`.)

## Deploy

```bash
# 1. Seal the secrets or emit ExternalSecrets from your vault.
#    Example with kubeseal (Sealed Secrets):
cp 01-secrets.yaml.template 01-secrets.yaml
# ... fill 01-secrets.yaml with real values ...
kubeseal -f 01-secrets.yaml -w 01-secrets.sealed.yaml
rm 01-secrets.yaml  # never commit the unsealed form
# Add 01-secrets.sealed.yaml to your overlay's kustomization.yaml.

# 2. Create a per-tenant overlay (see kustomization.yaml header comment for
#    the skeleton) and kustomize-build.
kubectl apply -k overlays/acmedemo/

# 3. Verify.
kubectl get pods -n sunfish-bridge-acmedemo
kubectl get ingress -n sunfish-bridge-acmedemo

# 4. Validate manifests without applying.
kubectl apply -k overlays/acmedemo/ --dry-run=client -o yaml | kubectl apply --dry-run=client -f -
# or
kubeval <(kustomize build overlays/acmedemo/)
```

## Validate the base manifests

```bash
# Syntax-check without a cluster
kubectl kustomize . | kubectl apply --dry-run=client -f -

# Or with kubeval (https://github.com/yannh/kubeconform)
kubectl kustomize . | kubeconform -strict -summary
```

## Post-deployment

1. Wait for all StatefulSets to reach `Ready` (`kubectl rollout status sts -A`).
2. Confirm DNS resolves for your ingress host (e.g. `acme.bridge.example.com`).
3. Verify the install-seed secret is mounted into the Bridge + tenant-node pods:
   ```bash
   kubectl -n sunfish-bridge-acmedemo exec deploy/bridge-web -- ls /etc/sunfish/seed
   ```
4. Hit the health endpoint:
   ```bash
   curl -I https://acme.bridge.example.com/health/live
   ```

## Scaling the tenant-node pool

The StatefulSet `bridge-tenant-node` defaults to `replicas: 8`. To bump:

```bash
kubectl -n sunfish-bridge-acmedemo scale statefulset bridge-tenant-node --replicas=16
```

Or via Kustomize overlay (preferred for reproducibility):

```yaml
# overlays/acmedemo/kustomization.yaml
replicas:
  - name: bridge-tenant-node
    count: 16
```

## Tear down

```bash
kubectl delete -k overlays/acmedemo/
# PVCs are NOT auto-deleted (StatefulSet default). To wipe data:
kubectl delete pvc -n sunfish-bridge-acmedemo --all
kubectl delete namespace sunfish-bridge-acmedemo
```

## Known TODOs

- **`TODO(w5.5.1)`** (cross-variant): DAB config is assumed baked into the image.
  Mount `dab-config.json` from a ConfigMap once we standardize the DAB runtime path.
- **`TODO(w5.5.6)`**: `40-network-policies.yaml` allows all HTTPS egress. Tighten to
  the operator's relay CIDR(s) or use Cilium FQDN policies for regulated contracts.
- **HA variants**: swap single-replica StatefulSets for operators (CloudNativePG,
  RabbitMQ Cluster Operator, Redis Operator) for production-grade HA.
- **Tenant-node slot assignment**: the `SUNFISH_NODE_SLOT_HOSTNAME` env var exposes the
  StatefulSet pod name (`bridge-tenant-node-0`, `-1`, ...). The local-node-host
  entrypoint parses the trailing ordinal and dials Bridge's registration endpoint.
  If Bridge isn't yet emitting tenant assignments when a slot boots, it sits idle and
  polls. This is intentional — slot lifecycle is Bridge-controlled.
