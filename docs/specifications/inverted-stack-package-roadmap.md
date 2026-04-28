# Inverted Stack Package Roadmap — Forward-Looking Namespaces from Book Volume 1

**Status:** Living document. Mirrors `C:/Projects/the-inverted-stack/docs/reference-implementation/sunfish-package-roadmap.md` (the authoritative source).
**Cadence:** Updated whenever a book extension introduces a new Sunfish namespace, or when implementation status advances on an existing forward-looking package.
**Last updated:** 2026-04-27.

## Why this document exists

The book *The Inverted Stack: Local-First Nodes in a SaaS World* (`C:/Projects/the-inverted-stack/`) makes architectural commitments to Sunfish packages that this implementation has not yet shipped. Each Volume 1 extension that introduces a new Sunfish namespace is a forward-looking commitment: this implementation will land the package, and the architecture the book specifies is the contract.

This document is the developer-facing entry point. For every forward-looking namespace, it lists:

- The book extension that committed to it
- The book chapter sections where the namespace appears
- The current implementation status in this repo
- The next implementation step
- Any open questions or scoping decisions

For the architectural commitment itself — what the package must own, the minimal surface it must expose — read the book-side authoritative document at `C:/Projects/the-inverted-stack/docs/reference-implementation/sunfish-package-roadmap.md`. The book is authoritative on architecture; this document is authoritative on implementation status.

## Status definitions

| Status | Meaning |
|---|---|
| `book-committed` | The book references the namespace. No corresponding package directory exists in this repo. |
| `adr-accepted` | An accepted ADR specifies the implementation choices for this package. Package may or may not be scaffolded. |
| `scaffolded` | A package directory exists at `packages/<name>/` with at least a `.csproj` and minimal interface declarations. |
| `in-development` | Active implementation. Some surface is functional; full book-referenced surface is not yet complete. |
| `shipped` | Full book-referenced surface available. Package added to the in-canon list. |

## Active forward-looking packages

### `Sunfish.Foundation.Recovery`

| Field | Value |
|---|---|
| Source extension | #48 key-loss recovery (book `design-decisions.md` §5 entry 48) |
| Status | `adr-accepted` — not yet scaffolded |
| Sunfish dir (planned) | `packages/foundation-recovery/` |
| Sunfish ADR | [`0046-key-loss-recovery-scheme-phase-1.md`](../adrs/0046-key-loss-recovery-scheme-phase-1.md) (accepted 2026-04-26) |
| Book chapters | Ch15 §Key-Loss Recovery; Ch20 §Key-Loss Recovery UX |

**Phase 1 scope (per ADR 0046):** sub-patterns 48a (multi-sig social) + 48c (paper-key) + 48e (timed grace period) + 48f (recovery-event audit trail). 48b (institutional custodian) and 48d (biometric-derived) are deferred to post-MVP. The Phase 1 architecture must not preclude adding them later.

**Next implementation step:** scaffold `packages/foundation-recovery/` with `Sunfish.Foundation.Recovery.csproj`, define minimal `IRecoveryArrangement` and `IShamirDealer` interfaces, wire to `Sunfish.Kernel.Security` for the existing Ed25519/X25519/SqlCipher substrate.

**Existing kernel substrate to lean on (per ADR 0046 references):**
- `packages/kernel-security/Crypto/Ed25519Signer.cs`
- `packages/kernel-security/Crypto/X25519KeyAgreement.cs`
- `packages/kernel-security/Keys/SqlCipherKeyDerivation.cs`
- `packages/kernel-security/Keys/KeystoreRootSeedProvider.cs`

**Open questions:**
- Audit-trail substrate: own `Sunfish.Kernel.Audit` package, or extend `Sunfish.Kernel.Ledger` / `Sunfish.Kernel.EventBus`. See next entry.
- Configuration manifest persistence: depend on `Sunfish.Foundation.LocalFirst`, or own.

### `Sunfish.Kernel.Audit`

| Field | Value |
|---|---|
| Source extension | #48 key-loss recovery |
| Status | `adr-accepted` — not yet scaffolded |
| Sunfish dir (planned) | `packages/kernel-audit/` |
| Sunfish ADR | [`0049-audit-trail-substrate.md`](../adrs/0049-audit-trail-substrate.md) (Accepted 2026-04-27) |
| Book chapters | Ch15 §Key-Loss Recovery (§Recovery-event audit trail); Ch15 §Implementation Surfaces |

**Resolution per ADR 0049:** distinct `Sunfish.Kernel.Audit` package, parallel to `Kernel.Ledger`, layered over the kernel `IEventLog` substrate from `Kernel.EventBus`. Same architectural pattern `Kernel.Ledger` uses (own contracts, own typed event stream, kernel `IEventLog` as durability hook). The retention semantics, Article 17 erasure logic, and third-party trustee metadata that distinguish audit records from application data are isolated in this package's contracts (`IAuditTrail`, `IComplianceQuery`).

**Next implementation step:** scaffold `packages/kernel-audit/` with `Sunfish.Kernel.Audit.csproj`, define `IAuditTrail` + `IAuditEventStream` + `AuditRecord` (marked `IMustHaveTenant`), implement `EventLogBackedAuditTrail`, mark persisted format as `v0` per ADR 0049 trust-impact (algorithm-agility dependency on ADR 0004). Wire into G6 host integration ("persist RecoveryEvents to per-tenant audit log") to unblock the Phase 1 carryover task.

### `Sunfish.Kernel.Performance`

| Field | Value |
|---|---|
| Source extension | #43 performance contracts with framework-level enforcement (book `design-decisions.md` §5 entry 43) |
| Status | `book-committed` — not yet scaffolded; no ADR yet |
| Sunfish dir (planned) | `packages/kernel-performance/` |
| Sunfish ADR | None yet. Recommend opening one as part of the next phase scoping. |
| Book chapters | Ch11 §Performance Contracts and Main-Thread Isolation; Ch20 §Performance Budgets and Progressive Degradation |

**Architectural commitment:** kernel-level enforcement of per-operation latency budgets. Routes CPU-bound operations (CRDT merges, projection rebuilds, large-query executions) to background threads or web workers. Provides progressive-degradation hooks. Ships a CI conformance test that fails the build on budget violations.

**Phase 1 scope (recommended):** all five sub-patterns 43a–43e ship together — partial implementation does not satisfy the book's FAILED-conditions block.

| Sub-pattern | Surface |
|---|---|
| 43a | Per-operation latency budget by operation class (local write <16ms, local read <50ms, projection rebuild <200ms, sync merge <200ms / progressive-degradation for large) |
| 43b | Main-thread isolation guarantee: no CPU-bound operation on the UI thread; enforced at kernel level, not plugin discipline |
| 43c | Progressive-degradation fallback via `IProgressiveDegradation` contract |
| 43d | `PerformanceBudgetValidator` CI test across three document sizes (1k, 10k, 100k operations) |
| 43e | Per-deployment-class calibration: `Interactive` (8ms write), `DocumentEditing` (16ms baseline), `BackgroundSync` (50ms relaxed) |

**Constraint propagated to existing packages:**

- `Sunfish.Kernel.Crdt` — `ICrdtEngine` merge operations are async by contract. No synchronous merge surface permitted. (This constraint is consistent with the existing async-first design per ADR 0028.)

**Kill trigger:** conformance below 95% (test operations completing within budget) for three consecutive CI sprints. This is an architectural-attention escalation, not a sprint task.

**Next implementation step:** open an ADR scoping Phase 1 of `Sunfish.Kernel.Performance` (likely "ADR 0049 — Performance Contracts package and main-thread isolation enforcement"). Reference the book's Ch11 §Performance Contracts as the architectural commitment, and the FAILED-conditions block as the testable specification.

**Open questions:**

- Kernel package vs. foundation package. The book treats it as kernel-tier because it constrains the kernel itself.
- `PerformanceBudgetValidator` location: in the package, or in `tests/` infrastructure consuming the package. The book leans toward "co-located with the contract."

## Anticipated future namespaces

These are not yet committed by book extensions but are anticipated as the loop-plan priority order advances. Listed for planning context, not implementation work.

| Extension | Likely namespace | Owns |
|---|---|---|
| #45 collaborator-revocation | extends `Sunfish.Foundation.Recovery` + `Sunfish.Kernel.Security` | revocation events; post-departure data partition |
| #11 fleet-management | NEW `Sunfish.Foundation.Fleet` (or `Sunfish.Kernel.Fleet`) | provisioning, key rotation, OTA, observability for headless-fleet |
| #47 endpoint-compromise | extends `Sunfish.Kernel.Security` | HSM/secure-enclave separation; attested boot; remote-wipe |
| #46 forward-secrecy | extends `Sunfish.Kernel.Security` + `Sunfish.Kernel.Sync` | per-message ephemeral keys; double-ratchet; sealed sender |
| #9 chain-of-custody | NEW `Sunfish.Kernel.Custody` (or extends `Sunfish.Kernel.Audit`) | multi-party signed transfer receipts |
| #12 privacy-aggregation | NEW relay-side privacy module | DP, k-anonymity, l-diversity at relay |
| #10 data-class-escalation | extends `Sunfish.Foundation.Catalog` + `Sunfish.Kernel.Runtime` | event-triggered re-classification |
| #44 per-data-class-device-distribution | extends `Sunfish.Kernel.Buckets` + `Sunfish.Kernel.Sync` | per-data-class device-eligibility declarations |

These become first-class entries here once the corresponding book extension reaches `code-check` stage and the namespace commitment solidifies.

## How to update this document

When a book extension reaches `code-check` and introduces a new Sunfish namespace:

1. The extension's HTML code-check annotation in the chapter prose names the namespace as forward-looking.
2. The book-side authoritative roadmap at `C:/Projects/the-inverted-stack/docs/reference-implementation/sunfish-package-roadmap.md` gets a new entry with status `book-committed`.
3. **This document gets a corresponding entry** with the same architectural commitment summarized, plus the implementation-status tracking that's specific to this repo.
4. When this repo opens an ADR for the namespace, the ADR ID is recorded here; status advances to `adr-accepted`.
5. When the package is scaffolded, status advances to `scaffolded`.
6. When the full surface ships, status advances to `shipped` and the package is added to the in-canon list.

## References

- **Authoritative roadmap (book side):** `C:/Projects/the-inverted-stack/docs/reference-implementation/sunfish-package-roadmap.md`
- **Per-extension architectural commitment:** `C:/Projects/the-inverted-stack/docs/reference-implementation/design-decisions.md` §5
- **Per-extension working artifacts:** `C:/Projects/the-inverted-stack/docs/book-update-plan/working/<extension-id>/`
- **In-canon Sunfish packages list:** `C:/Users/Chris/.claude/projects/C--Projects-Sunfish/memory/MEMORY.md` (and the user-memory `project_sunfish_packages.md` companion).
- **Existing ADRs:** `docs/adrs/` (see [README.md](../adrs/README.md) for the index).

If the book's commitment and this repo's implementation diverge, the book is authoritative for the architectural commitment (what the package must own). This document is authoritative for implementation status. Genuine architectural conflicts get an ADR.
