# Intake Note — Foundation-Audit vs Kernel-Audit Relationship

**Date:** 2026-04-28
**Requestor:** Christopher Wood (BDFL)
**Spec source:** Discovered while drafting the multi-tenancy type surface intake (`tenant-id-sentinel-pattern-intake-2026-04-28.md`) and reading `packages/foundation/Assets/Audit/` to inventory `TenantId?` use sites.
**Pipeline variant:** `sunfish-quality-control` (audit + decision: are we duplicating, or are these legitimately different concepts?)
**Status:** Stub. Filed to prevent drift while the more urgent multi-tenancy convention intake works through Stage 01–02. Promote to full intake when one of the revisit triggers fires.

## Problem Statement

Sunfish has two packages owning concepts called "audit," each with overlapping naming but different semantics. The collision was discovered 2026-04-28 while reading the foundation pieces during the multi-tenancy type surface intake's discovery work. Filing this stub so the question doesn't drift while the bigger convention work proceeds.

### Two parallel "audit" surfaces

**`Sunfish.Foundation.Assets.Audit` (existing, in production):**

- Files: `IAuditLog.cs`, `AuditRecord.cs`, `AuditQuery.cs`, `AuditAppend.cs`, `AuditId.cs`, `Op.cs`, `HashChain.cs`, `IAuditContextProvider.cs`, `InMemoryAuditLog.cs`
- Conceptual model: per-entity CRUD audit trail with SHA-256 hash chain; "who edited what entity, when, with what justification, hash-chained for tamper evidence"
- `AuditRecord` shape: `(AuditId, EntityId, VersionId?, Op, ActorId, TenantId, At, Justification, Payload, Signature?, Prev, Hash)` — **entity-scoped, hash-chained, single-issuer signature**
- `AuditQuery` shape: `(EntityId?, ActorId?, TenantId?, FromInclusive, ToExclusive, Op?, Limit?)` — **entity-CRUD-shaped**
- Verifies the hash chain via `IAuditLog.VerifyChainAsync(EntityId)`

**`Sunfish.Kernel.Audit` (proposed in ADR 0049, scaffolded in PR #190):**

- Files: `IAuditTrail.cs`, `AuditRecord.cs`, `AuditQuery.cs`, `AuditEventType.cs`, `AuditPayload.cs`, `EventLogBackedAuditTrail.cs`
- Conceptual model: cross-cutting security/compliance event log; "who initiated which security event, with what attestations, durably persisted via kernel `IEventLog`"
- `AuditRecord` shape: `(AuditId, TenantId, AuditEventType, OccurredAt, SignedOperation<AuditPayload>, IReadOnlyList<Signature>, FormatVersion)` — **event-stream-scoped, signed envelope, multi-party attestations**
- `AuditQuery` shape: `(TenantId, AuditEventType?, OccurredAfter, OccurredBefore, IssuedBy?)` — **event-stream-shaped**
- Layered over kernel `IEventLog`; no hash chain (the event log's append-only sequence is the integrity primitive)

These are legitimately different concepts:

| Dimension | Foundation-audit | Kernel-audit |
|---|---|---|
| Granularity | per-entity CRUD trail | cross-cutting event stream |
| Tamper evidence | SHA-256 hash chain per entity | append-only `IEventLog` sequence + signed envelopes |
| Storage | own `IAuditLog` impl (e.g., `InMemoryAuditLog`, `PostgresAuditLog`) | kernel `IEventLog` |
| Signature model | single optional issuer signature | required `SignedOperation<T>` envelope + multi-party attestations |
| Primary consumer | entity-history UIs, regulatory exports | recovery coordinator, capability subsystem, payment substrate, IRS export |
| Tenant scope | `TenantId?` (currently nullable) | `TenantId` required (`IMustHaveTenant`) |

## Question

How should the two coexist? Three options:

1. **Keep both names, document the boundary clearly.** Lowest churn; relies on documentation to prevent confusion. Risk: future contributors hit the collision and waste time disambiguating.
2. **Rename one to disambiguate.** Most principled. Candidates:
   - Rename foundation: `Foundation.Assets.History` (the model is "entity-version-history-with-tamper-evidence")
   - Rename kernel: `Sunfish.Kernel.SecurityEventLog` (the model is "cross-cutting security event stream")
   - The `Kernel.Audit` name is more recently committed (ADR 0049) and less embedded in production code, so renaming it is less disruptive — but the foundation name is older and may be more accurately "history" anyway.
3. **Merge into one substrate.** Probably wrong. Different requirements (entity-scoped CRUD trail vs. event-stream security log) suggest different designs; merging would force one of them into a less natural shape.

## Scope Statement

### In scope

1. **Side-by-side concept comparison.** Document the differences (granularity, tamper-evidence model, storage, signature model, consumer) so contributors don't accidentally pick the wrong substrate.
2. **Naming decision.** Pick option 1, 2, or 3 above. If 2, decide which to rename and to what.
3. **Cross-package guidance.** When should a new feature pick foundation-audit vs kernel-audit? A short decision tree in the convention doc that lands.
4. **Migration path if option 2.** If we rename, what's the deprecation plan for the old name?

### Out of scope

- Refactoring foundation-audit's hash-chain model — works as designed.
- Refactoring kernel-audit's `EventLogBackedAuditTrail` — covered by the multi-tenancy convention intake's retrofit plan.
- Generalizing both into a single abstraction — option 3 explicitly rejected as scope.

## Affected Sunfish Areas (preliminary)

| Area | Impact |
|---|---|
| `packages/foundation/Assets/Audit/*.cs` | possibly affected (rename) |
| `packages/foundation-assets-postgres/` | affected if foundation-audit renames (PostgresAuditLog tests reference current names) |
| `packages/kernel-audit/*.cs` | possibly affected (rename) |
| `_shared/engineering/CONVENTIONS-*.md` | new — documents the boundary + decision tree |
| Consumers: `blocks-tenant-admin`, `blocks-businesscases`, `blocks-subscriptions` (foundation-audit users); `kernel-security` recovery coordinator, future payments / IRS export (kernel-audit users) | possibly affected (rename callers) |

## Open Questions

1. Which package's name is more semantically accurate? Foundation-audit is closer to "entity history with tamper evidence" than "audit"; kernel-audit is closer to "audit" in the security/compliance sense. Suggests renaming foundation to `Assets.History` is more honest than renaming kernel.
2. Is there a third use case in flight that would change the decision? E.g., a "general-purpose application action log" use case that's neither entity-CRUD nor security-event would suggest a third concept — but no such use case is currently named in any active intake.
3. Should the decision wait for ADR 0004 algorithm-agility to land? Both audit substrates carry signatures whose forward stability depends on ADR 0004. If renames happen during the same wave as the format-v1 migration, callers only break once.

## Revisit triggers (when to promote this stub to a full intake)

- A new feature being designed has to pick one substrate and the choice is non-obvious to the implementer.
- A new ADR proposes a third "audit" substrate.
- ADR 0004 algorithm-agility refactor begins (good moment to bundle a rename).
- A contributor asks the question "why are there two AuditRecords?" in a PR review.

## Pipeline variant routing

**Filed as:** `sunfish-quality-control` (audit/consistency-check pipeline). Promotes to `sunfish-api-change` if the resolution is to rename one of the packages (api-change semantics for downstream consumers).

## Next stage

Hold at Stage 00 until a revisit trigger fires. Until then, contributors picking between the two substrates should default to the side-by-side comparison table above.
