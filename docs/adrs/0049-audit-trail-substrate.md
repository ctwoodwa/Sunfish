---
id: 49
title: 'Audit-Trail Substrate: Distinct Package over Kernel IEventLog'
status: Accepted
date: 2026-04-27
tier: kernel
pipeline_variant: sunfish-feature-change
concern:
  - audit
  - security
  - persistence
enables:
  - audit-event-emission
  - audit-record-attestation
  - compliance-query
composes:
  - 3
extends: []
supersedes: []
superseded_by: null
amendments: []
---

# ADR 0049 — Audit-Trail Substrate: Distinct Package over Kernel `IEventLog`

**Status:** Accepted (2026-04-27)
**Date:** 2026-04-27
**Resolves:** Open question flagged in [`docs/specifications/inverted-stack-package-roadmap.md`](../specifications/inverted-stack-package-roadmap.md) `Sunfish.Kernel.Audit` entry (currently `book-committed`). The roadmap defers the question — *"is this a distinct package, or a subsystem of `Sunfish.Kernel.Ledger` / `Sunfish.Kernel.EventBus`?"* — and notes: *"Decide before scaffolding `foundation-recovery`."* This ADR makes that decision.

---

## Context

ADR 0046 commits Phase 1 of `Sunfish.Foundation.Recovery` to ship four sub-patterns of primitive #48, including **48f cryptographically-signed audit trail**: every recovery event written to a per-tenant audit log — *"encrypted, signed by attesting trustees, timestamped; replicated via the same sync protocol as business data; visible in tenant audit-log UI."*

`foundation-recovery` cannot scaffold cleanly until the audit-trail substrate has a defined home. The roadmap names two paths it considered architecturally sound:

- **Distinct `Sunfish.Kernel.Audit` package** at `packages/kernel-audit/`
- **Subsystem of `Sunfish.Kernel.Ledger` / `Sunfish.Kernel.EventBus`**

After auditing the existing kernel structure (per `packages/kernel-ledger/README.md` and `packages/kernel-ledger/ILedgerEventStream.cs`):

- **`Kernel.Ledger` is a double-entry accounting subsystem.** It owns `Posting`, `Transaction` (with sum-to-zero invariant), `IPostingEngine`, `IBalanceProjection`, `IStatementProjection`, `IPeriodCloser`. It implements paper §12 — financial / CP-class value records. Audit records are not postings; they don't balance to zero; they don't have closing periods or compensation semantics. **Layering audit on top of `Kernel.Ledger` is the wrong abstraction.**
- **`Kernel.EventBus` provides the kernel `IEventLog`** — the untyped durability substrate carrying `KernelEvent`s. `Kernel.Ledger` uses `IEventLog` as its durability hook (per `ILedgerEventStream`'s docstring: *"sits alongside the kernel `IEventLog` which carries untyped `KernelEvent`s — this stream carries the domain-typed ledger events"*).
- **The right pattern is parallel to `Kernel.Ledger`:** a distinct subsystem package (`Kernel.Audit`) that owns its domain-typed records and its own typed event stream, with the kernel `IEventLog` as the shared durability substrate. Same substrate-impl insulation pattern proven by ADR 0028 (`ICrdtDocument` over Loro/YDotNet) and visible in `Kernel.Ledger`'s relationship to `IEventLog`.

A third option emerges from this audit: **define the audit contract as its own package, implement it on top of the kernel `IEventLog`** — parallel to `Kernel.Ledger`, not on top of it. This is the architecturally clean choice and the one this ADR adopts.

---

## Decision drivers

- **`foundation-recovery` blocks on this decision.** ADR 0046 Phase 1 implementation cannot begin until the audit-trail home is defined. G6 host integration (per `project_business_mvp_phase_1_progress` memory: *"persist RecoveryEvents to per-tenant audit log"*) is currently **not started** and depends on this substrate.
- **Article 17 (GDPR right to erasure) semantics differ between audit and application data.** Audit records may have legally-mandated retention windows; application records may be erasable on request. Mixing the two complicates compliance.
- **Audit records are append-only by definition.** Concurrent edits do not occur; the CRDT merge semantics `Kernel.Crdt` provides for AP-class application data are unnecessary for audit-tier data.
- **Substrate-impl insulation is the project's house style.** ADR 0028's compatibility plan (and the subsequent Loro→YDotNet substitution per `packages/kernel-crdt/SPIKE-OUTCOME.md`) prove that contracts beat substrate bindings. `Kernel.Ledger`'s relationship to `IEventLog` is the kernel-tier instance of the same pattern.
- **Future compliance features will land here.** Chain-of-custody (#9), regulatory exports, IRS audit support (per Phase 2 commercial scope), payment-event audit, and per-data-class escalation (#10) all need an audit substrate. Whatever shape this ADR chooses must accommodate them.
- **Threat-model boundary clarity.** ADR 0043 already practices cross-ADR threat delegation (crypto to 0004, CRDT to 0028). A distinct audit boundary makes the next layer of cross-ADR delegation cleaner; mixing audit into ledger or event-bus blurs it.
- **Sync substrate stays unified regardless.** ADR 0046 explicitly requires audit events replicated *"via the same sync protocol as business data."* The shared `IEventLog` substrate satisfies this; sync transport remains shared regardless of package layout.

---

## Considered options

### Option A — Distinct `Sunfish.Kernel.Audit` package with its own event-log substrate

Spin up `packages/kernel-audit/` with full ownership: own contracts, own storage, own sync, own durability — no shared kernel substrate.

- **Pro:** maximum semantic separation; Article 17 logic isolated; audit fully independent.
- **Con:** duplicates the kernel's `IEventLog` mechanism; two durability paths to coordinate.
- **Con:** contradicts ADR 0046's *"same sync protocol"* framing — would need a separate sync transport.
- **Con:** breaks parallelism with `Kernel.Ledger`, which uses kernel `IEventLog`. Asymmetry without justification.
- **Verdict:** over-isolated. Coordination cost is real and unjustified by the semantic gain.

### Option B — Subsystem of `Kernel.EventBus` (audit record type added to the existing event-bus package)

Add an `AuditRecord` type to `Kernel.EventBus`. Retention rules become per-record-type filters within event-bus.

- **Pro:** lowest scaffolding cost; reuses existing event-bus machinery directly.
- **Con:** Article 17 semantics get tangled with general-purpose event distribution; future compliance carve-outs accumulate as per-type filters in a generic substrate.
- **Con:** mixes domain-typed audit semantics with generic event-bus mechanics, violating the *"different semantics, different package"* principle the project applies elsewhere. The fact that `Kernel.Ledger` is its own package despite using `IEventLog` is direct precedent for not doing this.
- **Con:** future packages (`Foundation.Recovery`, `Kernel.Custody`) acquire transitive dependency on `Kernel.EventBus`'s full surface when they only need audit primitives.
- **Verdict:** false economy. Scaffolding savings paid back many times over in coupling costs.

### Option C — Distinct `Sunfish.Kernel.Audit` package, parallel to `Kernel.Ledger`, layered over kernel `IEventLog` **[RECOMMENDED]**

`packages/kernel-audit/` defines its own contracts (`IAuditTrail`, `IAuditEventStream`, `IComplianceQuery`) and its own domain-typed records (`AuditRecord`, `AuditEventType`). Durability delegates to the kernel `IEventLog` (from `Kernel.EventBus`) — same pattern as `Kernel.Ledger`. Retention, Article 17, and compliance logic live in `Kernel.Audit`. Schema epoch is shared via the kernel.

- **Pro:** structurally parallel to `Kernel.Ledger` — same package shape, same `IEventLog` integration. Uniform mental model for anyone who already understands the ledger subsystem.
- **Pro:** contract-vs-substrate split mirrors the project's house style — `ICrdtDocument` over Loro/YDotNet (ADR 0028); `Kernel.Ledger` over `IEventLog`; Capabilities/Macaroons two-layer model in `Foundation`.
- **Pro:** Article 17 / retention / compliance logic concentrated in one package; future regulatory features extend `Kernel.Audit` without touching ledger or event-bus.
- **Pro:** `Foundation.Recovery` depends on `Kernel.Audit`'s narrow surface, not on `Kernel.Ledger`'s or `Kernel.EventBus`'s full surfaces.
- **Pro:** no sync-protocol duplication; ADR 0046's framing preserved.
- **Pro:** future migration of audit-record storage to a different substrate (e.g., a compliance-specific WORM store) is an `IAuditTrail` impl swap, not a kernel rewrite — same insulation discipline as ADR 0028.
- **Con:** contributors must understand the layering: audit semantics in `Kernel.Audit`, audit durability via `IEventLog`. Same conceptual cost as understanding `Kernel.Ledger`'s relationship to `IEventLog`.
- **Verdict:** the right shape. Parallel to `Kernel.Ledger`; honors substrate-impl insulation; preserves single-substrate sync; isolates compliance concerns.

---

## Decision

**Adopt Option C.** Scaffold `packages/kernel-audit/` as a distinct kernel-tier package, parallel to `Kernel.Ledger`, layered over the kernel `IEventLog` substrate from `Kernel.EventBus`.

### Initial contract surface (illustrative)

The exact API may evolve during scaffolding review; the shape below illustrates the layering and the parallel structure to `ILedgerEventStream`:

```csharp
namespace Sunfish.Kernel.Audit;

using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.MultiTenancy;

/// <summary>
/// Append-only domain-typed audit trail. Durability delegated to kernel IEventLog,
/// parallel to how Kernel.Ledger uses IEventLog. Audit records are tenant-scoped
/// (IMustHaveTenant) and signed; AppendAsync verifies signatures before persistence.
/// </summary>
public interface IAuditTrail
{
    ValueTask AppendAsync(AuditRecord record, CancellationToken ct = default);
    IAsyncEnumerable<AuditRecord> QueryAsync(AuditQuery query, CancellationToken ct = default);
}

/// <summary>
/// Internal typed event stream for audit subsystem consumers (compliance projections,
/// retention reporters). Sits alongside the kernel IEventLog. Mirrors ILedgerEventStream.
/// </summary>
public interface IAuditEventStream
{
    IReadOnlyList<AuditRecord> ReplayAll();
    IDisposable Subscribe(Action<AuditRecord> handler);
}

/// <summary>
/// Tenant-scoped audit record. Signed by one or more attesting principals.
/// Note: AttestingSignatures uses Sunfish.Foundation.Crypto.Signature, which today
/// is algorithm-locked to Ed25519 per ADR 0004. The audit-record format is marked
/// v0 and SHOULD NOT be considered forward-stable until ADR 0004's algorithm-agility
/// refactor lands; see "Trust impact" below.
/// </summary>
public readonly record struct AuditRecord(
    Guid AuditId,
    TenantId TenantId,
    AuditEventType EventType,
    DateTimeOffset OccurredAt,
    SignedOperation<AuditPayload> Payload,
    IReadOnlyList<Signature> AttestingSignatures) : IMustHaveTenant;

public interface IComplianceQuery
{
    ValueTask<RetentionDecision> EvaluateRetentionAsync(AuditRecord record, CancellationToken ct);
    ValueTask<bool> CanEraseAsync(AuditRecord record, ErasureRequest request, CancellationToken ct);
}
```

The contract is intentionally narrow — append, query, retention. Future compliance features (chain-of-custody, regulatory export, per-data-class escalation) extend through additional interfaces in the same package, not through new packages.

### Layering over kernel `IEventLog`

`Kernel.Audit` follows the same pattern as `Kernel.Ledger`:

1. `IAuditTrail.AppendAsync` writes a typed `AuditRecord` to its in-process `IAuditEventStream` AND appends a corresponding entry to the kernel `IEventLog` for durability.
2. `IAuditEventStream` carries the domain-typed records for in-process consumers (UIs, projections, retention reporters).
3. The kernel `IEventLog` carries the durability events; on construction `IAuditTrail` rebuilds its in-process state from the event log.
4. Other consumers MAY read audit records via `IAuditTrail`; they MUST NOT read directly from `IEventLog` for audit semantics. The contract is the access path; the substrate is implementation detail.

This is the same access discipline `ICrdtDocument` enforces over Loro/YDotNet and that `Kernel.Ledger` enforces over `IEventLog`.

---

## Consequences

### Positive

- `Foundation.Recovery` Phase 1 can scaffold immediately against `IAuditTrail` without waiting for full compliance scope to be designed. **Unblocks G6 host integration** — the not-started "persist RecoveryEvents to per-tenant audit log" task in the active Phase 1 plan.
- Article 17 semantics are isolated; future compliance features (`Kernel.Custody`, regulatory exports) extend `Kernel.Audit` without touching `Kernel.Ledger` or `Kernel.EventBus`.
- ADR 0046's *"same sync protocol"* requirement is preserved — audit and application data both flow via the kernel event-log + sync substrate.
- Substrate-impl insulation discipline carries forward: audit storage can be swapped to a compliance-specific substrate without rippling into application code.
- ADR 0043's threat-model delegation pattern extends cleanly: future security ADRs can defer audit-tier concerns to ADR 0049 the same way they defer crypto to ADR 0004 and CRDT to ADR 0028.
- Phase 2 commercial scope (per `project_phase_2_commercial_scope` memory) — payments, IRS export, bookkeeper audit — all consume `IAuditTrail` from this package.

### Negative

- A new package adds maintenance surface. Mitigation: starts small (3 interfaces, a record type, retention primitive); growth gated on real compliance demands.
- Contributors must understand the layering. Mitigation: package README documents the substrate-impl pattern with `Kernel.Ledger`'s relationship to `IEventLog` as the direct precedent reference.

### Trust impact

`Kernel.Audit` surfaces multi-party signed attestation as a first-class kernel primitive. Threat model: the package becomes a target for forging compliance records; signature verification at the `IAuditTrail.AppendAsync` boundary is the mitigation.

**Important: `AuditRecord.AttestingSignatures` inherits ADR 0004's pre-agility constraint.** `Sunfish.Foundation.Crypto.Signature` is currently algorithm-locked to Ed25519 (64-byte fixed). ADR 0004 commits to refactoring this into an algorithm-agile envelope before v1. **Audit records are exactly the long-retention data class that needs algorithm-agility before format commitment** — a 7-year-retained IRS audit record, payment dispute trail, or recovery attestation written today against fixed Ed25519 will need migration when PQC signatures ship per ADR 0004's dual-sign window.

Two valid responses:
1. **Sequence:** Hold scaffolding `Kernel.Audit` until ADR 0004's `Signature` refactor lands. Adds a dependency on ADR 0004 implementation to the critical path.
2. **Forward-compatibility shim:** Define `AuditRecord.AttestingSignatures` as `IReadOnlyList<Signature>` today but **mark the persisted format as `v0`**; a `v1` envelope adds an algorithm tag before any audit data is considered forward-stable. This is the lower-risk path if ADR 0004's refactor is on a longer timeline.

This ADR recommends path 2 (shim with explicit `v0` marking) and adds a revisit trigger when ADR 0004 implementation begins. Future amendments to ADR 0043 should add an audit-tier threat row and delegate to this ADR.

---

## Compatibility plan

- `Sunfish.Kernel.Audit` joins the in-canon kernel-tier package list alongside `Kernel.Ledger`, `Kernel.EventBus`, `Kernel.Crdt`, `Kernel.Security`, `Kernel.Sync`, `Kernel.Buckets`, `Kernel.Runtime`, `Kernel.Lease`, `Kernel.SchemaRegistry`.
- `Foundation.Recovery` (per ADR 0046) depends on `Kernel.Audit`, not on `Kernel.Ledger` or `Kernel.EventBus` directly.
- A future migration of audit-record storage to a non-`IEventLog` substrate is an `IAuditTrail` impl swap; consumers of the contract are unaffected.
- Roadmap entry for `Sunfish.Kernel.Audit` in `inverted-stack-package-roadmap.md` advances from `book-committed` to `adr-accepted` upon acceptance of this ADR.

---

## Implementation checklist

- [ ] Scaffold `packages/kernel-audit/` with `Sunfish.Kernel.Audit.csproj`.
- [ ] Reference `Kernel.EventBus` (for `IEventLog`), `Foundation.Crypto` (for `Signature` / `SignedOperation`), `Foundation.MultiTenancy` (for `IMustHaveTenant`), `Foundation.Assets.Common` (for `TenantId`).
- [ ] Define `IAuditTrail`, `IAuditEventStream`, `IComplianceQuery`, `AuditRecord` (marked `IMustHaveTenant`), `AuditEventType`, `RetentionDecision`, `ErasureRequest` (final shape per scaffolding review).
- [ ] Implement `EventLogBackedAuditTrail` as the default `IAuditTrail` impl, appending to `IEventLog` and an in-process `IAuditEventStream` — direct parallel to `Kernel.Ledger`'s implementation.
- [ ] **Mark the audit-record persisted format as `v0`** and document the ADR 0004 algorithm-agility dependency in the package README.
- [ ] Update `inverted-stack-package-roadmap.md` `Sunfish.Kernel.Audit` entry to `adr-accepted`.
- [ ] Update `Sunfish.Foundation.Recovery` (orchestration; depends on `Kernel.Security` crypto primitives — see ADR 0046 § "Package placement") to depend on `Kernel.Audit`.
- [ ] Wire `IAuditTrail` into G6 host integration (the not-started Phase 1 task: "persist RecoveryEvents to per-tenant audit log").
- [ ] Property tests: append-then-query roundtrip, retention-decision determinism, multi-party signature verification on attestation, replay-from-IEventLog reconstructs in-process state.
- [ ] README documenting the substrate-impl layering with reference to ADR 0028 and `Kernel.Ledger`'s README as precedents.

---

## Open questions

- **What is the canonical retention policy schema?** `RetentionDecision` shape is illustrative; first real compliance use case (likely IRS export per Phase 2 scope) will inform the contract.
- **`AuditEventType` initial enum?** Likely starts with: `KeyRecoveryInitiated`, `KeyRecoveryAttested`, `KeyRecoveryCompleted`, `CapabilityDelegated`, `CapabilityRevoked`, `PaymentAuthorized`, `PaymentCaptured`, `PaymentRefunded`, `BookkeeperAccess`, `TaxAdvisorAccess`, `IrsExportGenerated`. Extensible.
- **Should cross-tenant audit records be permitted (`IMayHaveTenant`)?** Probably not for v0; cross-tenant compliance audits (e.g., consolidated reporting across the BDFL's 6-tenant property holding structure per Phase 2 scope) may surface this need later.
- **Does `AuditQuery` support time-range + principal-filter as the v0 surface?** Likely yes; tax-advisor IRS export needs time-range; security review needs principal-filter.
- **Is `IComplianceQuery` Phase 1 or Phase 2?** Could split: ship `IAuditTrail` + `IAuditEventStream` in 0049's first scaffolding (sufficient for G6 + recovery), defer `IComplianceQuery` to a follow-up when a real compliance use case (IRS export per Phase 2) materializes.

---

## Revisit triggers

- **ADR 0004 algorithm-agility refactor begins** — re-evaluate `AuditRecord` format; flip from `v0` to `v1` envelope with algorithm tag.
- A real compliance requirement (e.g., regulated SMB segment per ADR 0046's revisit triggers) demands a non-`IEventLog` storage substrate — implementation switches `IAuditTrail` impl; ADR text unchanged unless the contract itself is found insufficient.
- Chain-of-custody (#9) primitive ships and exposes a need for inter-tenant audit signatures — extend `IAuditTrail` or open a sister package.
- Article 17 erasure semantics evolve (regulator publishes new guidance) — re-evaluate `IComplianceQuery.CanEraseAsync` contract.
- A real audit-trail incident produces a post-mortem that surfaces a missing primitive.
- ADR 0043 amended with an audit-tier threat row that requires a control this contract does not currently support.

---

## References

### Predecessor and sister ADRs

- [ADR 0046](./0046-key-loss-recovery-scheme-phase-1.md) — Key-loss recovery Phase 1; the first consumer of `Kernel.Audit`. Sub-pattern 48f names the audit-trail requirement this ADR resolves.
- [ADR 0028](./0028-crdt-engine-selection.md) — CRDT engine selection; the substrate-impl insulation precedent this ADR follows. Direct architectural parallel.
- [ADR 0003](./0003-event-bus-distribution-semantics.md) — event-bus distribution semantics; defines the `IEventLog` substrate this ADR layers over.
- [ADR 0004](./0004-post-quantum-signature-migration.md) — post-quantum signature migration; defines the algorithm-agility refactor this ADR's audit format depends on for forward stability.
- [ADR 0008](./0008-foundation-multitenancy.md) — `Foundation.MultiTenancy` contracts; defines `TenantId` and `IMustHaveTenant` markers used by `AuditRecord`.
- [ADR 0043](./0043-unified-threat-model-public-oss-chain-of-permissiveness.md) — unified threat model; this ADR's package becomes a referenceable boundary for future audit-tier threat delegation.
- [ADR 0012](./0012-foundation-localfirst.md) — `Foundation.LocalFirst` contracts; Anchor's local-first runtime that consumes audit records.
- [ADR 0029](./0029-federation-reconciliation.md) — federation and reconciliation; describes how audit records propagate between peers via the shared sync transport.

### Architectural precedent (the parallel this ADR mirrors)

- [`packages/kernel-ledger/README.md`](../../packages/kernel-ledger/README.md) — direct precedent. `Kernel.Ledger` is its own kernel-tier package with its own contracts (`IPostingEngine`, `IBalanceProjection`) and its own typed event stream (`ILedgerEventStream`), implemented over the kernel `IEventLog`. `Kernel.Audit` follows the same shape.
- [`packages/foundation/DECENTRALIZATION.md`](../../packages/foundation/DECENTRALIZATION.md) — the four-namespace authorization model in `Foundation` that `IAuditTrail` integrates with for capability-based audit-read authorization.

### Roadmap and specifications

- [`docs/specifications/inverted-stack-package-roadmap.md`](../specifications/inverted-stack-package-roadmap.md) — `Sunfish.Kernel.Audit` entry currently `book-committed`; this ADR advances it to `adr-accepted`.
- Book chapters: Ch15 §Key-Loss Recovery (§Recovery-event audit trail); Ch15 §Implementation Surfaces.

### Existing substrate this ADR layers over

- `packages/kernel-event-bus/` — provides the kernel `IEventLog` durability substrate.
- `packages/foundation/Crypto/SignedOperation.cs` — multi-party signature envelope used in `AuditRecord.AttestingSignatures`.
- `packages/foundation/Capabilities/` — the capability graph used for audit-tier read authorization.
- `packages/foundation-multitenancy/` — `TenantId` and `IMustHaveTenant` markers.

### External

- GDPR Article 17 (right to erasure): `https://gdpr-info.eu/art-17-gdpr/`.
- NIST SP 800-92 — Guide to Computer Security Log Management (informs retention defaults): `https://csrc.nist.gov/pubs/sp/800/92/final`.
