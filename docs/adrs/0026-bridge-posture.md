---
id: 26
title: Bridge Posture (SaaS Shell vs. Managed Relay)
status: Superseded
date: 2026-04-22
tier: accelerator
concern: []
composes:
  - 6
  - 7
  - 12
  - 18
extends: []
supersedes: []
superseded_by: 31
amendments: []
---
# ADR 0026 — Bridge Posture (SaaS Shell vs. Managed Relay)

**Status:** Superseded by [ADR 0031](./0031-bridge-hybrid-multi-tenant-saas.md) (2026-04-23)
**Date:** 2026-04-22
**Resolves:** Bridge's current multi-tenant-SaaS-shell framing ([ADR 0006](0006-bridge-is-saas-shell.md)) is structurally the inverse of the [local-node architecture paper](../../_shared/product/local-node-architecture-paper.md) §3 inversion thesis. The paper mandates *"the local node is primary; the cloud is a sync peer"*; Bridge today ships as *"hosted Postgres is primary; the local device is a client."* The paper-alignment audit ([`paper-alignment-audit-2026-04-22.md`](../../icm/07_review/output/paper-alignment-audit-2026-04-22.md) §3.2, §5.2, conflict α) explicitly flagged this as the first-order structural tension in the repo and called for an ADR. This ADR decides how to reconcile. Wave 0.2 of the paper-alignment plan.

---

## Context

Bridge today is a .NET Aspire 13-orchestrated multi-tenant SaaS shell: Blazor Server host, hosted Postgres via EF Core 10, Redis cache, RabbitMQ transport via Wolverine with Postgres outbox, Data API Builder for GraphQL, and a `BridgeHub` SignalR endpoint for real-time updates. `DemoTenantContext` returns a hardcoded tenant ID and `MockOktaService` provides OIDC. Bridge boots with `dotnet run --project Sunfish.Bridge.AppHost` and is a working end-to-end demo of every Sunfish tier composing into one application. ADR 0006 formalized this framing in April 2026 by declaring Bridge "the Sunfish reference SaaS shell accelerator — a generic multi-tenant platform host."

The paper, published shortly after ADR 0006, takes the opposite position on where authority lives. Paper §3 ("Reframing the SaaS Contract") states: *"instead of the cloud being primary and the local device being a read-through cache, the local node is primary and the cloud is a sync peer. The local node holds the authoritative copy. Cloud infrastructure, where it exists, is one peer among many — not the authority."* Paper §6.1 names three peer-discovery tiers — mDNS, mesh VPN, and a **managed relay** for teams where direct peer connectivity is not viable. Paper §17.2 then names that managed relay as Sunfish's sustainable-revenue SKU: *"the sustainable revenue model is a managed relay service: operationally hardened, SLA-backed relay infrastructure that teams subscribe to for guaranteed peer coordination, NAT traversal, and first-line support."*

Bridge's own [`PLATFORM_ALIGNMENT.md`](../../accelerators/bridge/PLATFORM_ALIGNMENT.md) already acknowledges the tension — version store 🔴, cryptographic ownership proofs 🔴, federation 🔴, temporal as-of queries 🔴 — because a paper-aligned Bridge cannot be structurally authoritative. The audit's §3.2 conclusion labeled this "Structural Conflict, Reconcilable": the paper offers its own reconciliation path. Bridge can become the **managed relay** (Posture B) — a peer among peers that adds operational reliability rather than an authority that owns the data. Bridge can also keep its current multi-tenant-hosted shape (Posture A) for the paper's §14 "non-technical trust gap" audience — organizations that explicitly do not want to run local nodes and whose dominant requirement is "who do I call."

Two natural postures; the decision is which (or whether both).

---

## Decision drivers

- **Paper is source of truth.** Where paper and ADR disagree, paper wins; ADR 0006's framing is the one that needs to move.
- **Pre-release status.** Sunfish is pre-v1; breaking changes are approved. No external consumer is pinned to Bridge-as-SaaS-shell.
- **Preserve demo utility.** Bridge is the only accelerator that actually runs end-to-end today ([audit §3.2](../../icm/07_review/output/paper-alignment-audit-2026-04-22.md); Anchor is scaffolded-only). Discarding Bridge's working demo to chase paper alignment trades a real artifact for an aspirational one.
- **SMB "who do I call" trust gap (paper §14).** The paper itself identifies an audience for whom hosted-authority is the right answer. Discarding that capability is narrower than the paper asks for.
- **§17.2 revenue mandate.** The paper explicitly names managed relay as the revenue SKU. Whatever Bridge becomes, the managed-relay capability must exist somewhere.
- **LLC formation pending public release.** Governance and licensing decisions (per [ADR 0018](0018-governance-and-license-posture.md)) gate the private→public flip; posture decisions made now ride the same governance window.
- **Two-accelerator surface-area cost.** Every new top-level accelerator doubles doc burden, sample surface, and contributor-onboarding load.

---

## Considered options

### Option A — Deprecate Bridge's SaaS-shell mode; reposition purely as managed relay

Bridge becomes exclusively the paper's managed relay. ADR 0006's multi-tenant-shell framing is retired. Current Aspire + Postgres + DAB + SignalR stack is repurposed as relay infrastructure (peer coordination, NAT traversal, SLA-backed uptime).

- Pro: Cleanest paper alignment; one posture to document and maintain.
- Pro: Preserves most of the existing infrastructure investment.
- Con: Loses Bridge's current demo utility during the transition — the running end-to-end showpiece disappears until the relay is stood up.
- Con: Abandons the paper §14 SMB audience that genuinely wants hosted-authority software.

### Option B — Dual-posture: Bridge supports both SaaS-shell mode AND managed-relay mode via opt-in configuration

Bridge retains its current SaaS-shell deployment as Posture A and adds a managed-relay deployment as Posture B, selectable at install time (e.g., `--mode=saas` vs. `--mode=relay`). The two postures share Aspire orchestration, Postgres, observability, and tenant identity machinery; they diverge on what the tenant-facing contract is (authoritative host vs. peer coordination service).

- Pro: Preserves Bridge's current demo + opens paper-aligned deployment without a second accelerator.
- Pro: Reuses substantial infrastructure (Aspire, Postgres, DAB, observability) across both postures.
- Pro: Mirrors the paper's install-time surfacing convention (paper §2.3) — the posture choice is visible where the installer is run.
- Con: Maintenance burden — two operational shapes in one repo; two test matrices; two documentation paths.
- Con: Marketing-clarity risk — "is Bridge a SaaS shell or a relay?" requires a one-sentence answer that the docs have to enforce consistently.

### Option C — Retain Bridge's SaaS-shell mode as-is; add a separate `accelerators/relay` as the paper-aligned managed relay

Bridge keeps its ADR 0006 framing. A new `accelerators/relay` is built alongside it, purpose-built as the paper's managed relay. The two accelerators are peers in the repo.

- Pro: No churn on Bridge; relay gets a clean slate free of Bridge's current assumptions.
- Con: Two overlapping accelerators; consumer confusion about which to pick ("do I install Bridge or Relay? Both? Why?").
- Con: Doubles the accelerator surface area (README, alignment doc, tests, sample bundles).
- Con: The infrastructure overlap (Aspire, Postgres, observability) is substantial; duplicating it is waste.

### Option D — Retain Bridge as-is; mark it "pre-paper era" and treat paper-aligned deployment as requiring Anchor + a future relay built from scratch

Bridge is frozen as a pre-paper artifact. Paper alignment is blocked on Anchor maturing (currently scaffolded-only) plus a greenfield relay accelerator later.

- Pro: Zero short-term churn on Bridge; cheapest today.
- Con: Paper alignment is indefinitely deferred. Managed-relay revenue path (§17.2) does not exist until the relay is built.
- Con: Two-accelerator confusion (Bridge + future relay) still appears, just later.
- Con: Concedes the structural conflict without resolving it.

---

## Decision (recommended)

**Adopt Option B — dual-posture Bridge.**

Rationale:

- The paper itself in §17.2 names the managed relay as the sustainable-revenue SKU. Bridge is already the product with hosted infrastructure; extending it to offer relay services is smaller-scope than building a second accelerator and then maintaining it in parallel.
- SMB customers per paper §14 explicitly need "who do I call"; a multi-tenant-hosted Bridge-as-SaaS posture remains valuable post-paper for organizations that don't want to run local nodes at all.
- Pre-release status allows a breaking change to ADR 0006's framing; no external consumer is pinned.
- Dual-posture is visible as a configuration switch at install-time (`--mode=saas` vs. `--mode=relay`), mirroring the paper's installation-time surfacing convention in §2.3.
- Re-uses substantial existing investment: Aspire orchestration, Postgres, DAB, observability, tenant identity plumbing. The relay posture inherits rather than rebuilds.

---

## Consequences

### Positive

- Bridge has a well-defined paper-aligned future without discarding current work.
- Managed-relay posture inherits Bridge's Aspire orchestration, Postgres, DAB, SignalR, and observability stack — substantial reuse, lower delivery cost than Option C.
- Clear migration narrative for current Bridge users: same installer, different configuration flag.
- The paper §17.2 revenue SKU has a concrete home without spawning a second accelerator.
- Structural conflict α from the paper-alignment audit is resolved.

### Negative

- **Code complexity — two deployment modes to test.** Every feature either scopes to one posture or has to be validated in both.
- **Confusion risk for early adopters.** "Is Bridge a SaaS shell or a relay?" needs a crisp docs-level answer. Mitigation: dedicated section near the top of Bridge's README.
- **`PLATFORM_ALIGNMENT.md` becomes two tables** (one per posture). Maintenance cost acknowledged.
- **ADR 0006 gets superseded.** Framing change is consumer-visible even with no external pin, because contributors reading ADR 0006 today will need to re-read ADR 0026.
- **Feature-parity ambiguity.** Some primitives (e.g., cryptographic ownership proofs, version store) only make sense in Posture B. Posture A gets a documented exemption from those rows in `PLATFORM_ALIGNMENT.md`.

---

## Compatibility plan

- [ADR 0006](0006-bridge-is-saas-shell.md) gets a status update to *"Superseded by 0026; SaaS-shell is Posture A of dual-posture Bridge."* The status update ships in the follow-up PR listed in the implementation checklist below — this ADR does **not** modify ADR 0006 directly.
- Bridge's existing code stays. A new configuration path adds relay mode in a follow-up PR (scoped separately as Wave 4.2 of the paper-alignment plan).
- `accelerators/bridge/README.md` gains a dual-posture narrative near the top, naming Posture A (SaaS shell) and Posture B (managed relay) and pointing at this ADR.
- `accelerators/bridge/PLATFORM_ALIGNMENT.md` gets posture-split tables — the Posture A column tracks shell-concerns coverage; the Posture B column tracks managed-relay coverage; rows that are N/A in one posture are marked ⚪ in that column.
- No immediate migration for current Bridge users: `--mode=saas` is the default and matches today's behavior.

---

## Implementation checklist

- [ ] Update [`docs/adrs/0006-bridge-is-saas-shell.md`](0006-bridge-is-saas-shell.md) Status to *"Superseded by 0026"* (follow-up PR, not in this ADR's changeset).
- [ ] Update [`accelerators/bridge/README.md`](../../accelerators/bridge/README.md) with dual-posture narrative; name Posture A and Posture B; link to this ADR.
- [ ] Update [`accelerators/bridge/PLATFORM_ALIGNMENT.md`](../../accelerators/bridge/PLATFORM_ALIGNMENT.md) with separate Posture-A and Posture-B status tables; mark posture-irrelevant rows ⚪.
- [ ] Scope relay-mode implementation (`--mode=relay` configuration path + managed-relay services) as Wave 4.2 of the paper-alignment plan.
- [ ] Update the paper-alignment audit ([`paper-alignment-audit-2026-04-22.md`](../../icm/07_review/output/paper-alignment-audit-2026-04-22.md)) §3.2 and conflict α row — "resolved by ADR 0026."
- [ ] Communicate the posture split in the next release notes under a "Governance" or "Architecture direction" heading.

---

## References

- [`_shared/product/local-node-architecture-paper.md`](../../_shared/product/local-node-architecture-paper.md) §3 (Reframing the SaaS Contract), §6.1 (Peer discovery — managed relay), §14 (Non-technical trust gap), §17.2 (Managed Relay as Sustainable Revenue).
- [ADR 0006](0006-bridge-is-saas-shell.md) — Bridge Is a Generic SaaS Shell, Not a Vertical App. Superseded by this ADR (framing shifts from single-posture SaaS shell to dual-posture Bridge).
- [ADR 0007](0007-bundle-manifest-schema.md) — Bundle Manifest Schema. Bundle activation is a Posture-A concern; Posture B does not host bundles.
- [ADR 0012](0012-foundation-localfirst.md) — Foundation local-first primitives. Posture B is where these land operationally.
- [ADR 0018](0018-governance-and-license-posture.md) — Governance / license. LLC formation gates the private→public flip that puts posture choices in front of external consumers.
- [`accelerators/bridge/PLATFORM_ALIGNMENT.md`](../../accelerators/bridge/PLATFORM_ALIGNMENT.md) — gets posture-split tables per the compatibility plan above.
- [`icm/07_review/output/paper-alignment-audit-2026-04-22.md`](../../icm/07_review/output/paper-alignment-audit-2026-04-22.md) §3.2 (Bridge structural conflict, reconcilable), §5.2 / conflict α (ADR 0026 called for).
- [`_shared/product/paper-alignment-plan.md`](../../_shared/product/paper-alignment-plan.md) — Wave 0.2 delivers this ADR; Wave 4.2 executes its decision.
