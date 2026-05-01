# Intake — ADR 0036 Amendment A1: Public `Sunfish.Foundation.UI.SyncState` enum

**Date:** 2026-04-30
**Requestor:** XO (declared by ADR 0063-A1.2 halt-condition + A1.17 sibling-amendment dependencies)
**Request:** Amend ADR 0036 (sync-state surface) with a new amendment A1 exposing a public foundation-tier `Sunfish.Foundation.UI.SyncState` enum (PascalCase form of the existing encoding-contract identifiers `healthy / stale / offline / conflict / quarantine`). ADR 0036 currently declares the encoding contract but does NOT expose a public C# enum that downstream substrate ADRs can consume in type signatures.
**Pipeline variant:** `sunfish-api-change` (introduces new public type; non-breaking due to additive surface)
**Stage:** 00 — pending CO promotion to active

---

## Problem Statement

ADR 0063 (Mission Space Requirements; in flight via PR #411) introduces `SyncStateSpec.AcceptableStates: IReadOnlySet<SyncState>?` as part of the install-time spec schema. The `SyncState` type ADR 0063 cites does NOT exist as a public foundation-tier enum on `origin/main` — ADR 0036 declares the *encoding contract* (the canonical identifiers `healthy / stale / offline / conflict / quarantine`) but stops short of exposing a public enum.

Consumers today receive sync state via ADR 0036's encoding contract (typically as opaque strings or inline-typed records); ADR 0063's spec layer needs a typed enum value-set so spec authors can declare `AcceptableStates: { Healthy, Stale }` rather than `AcceptableStates: { "healthy", "stale" }` (string-set with no compile-time check).

The ADR 0063 council (PR #413) caught the gap and added a halt-condition: ADR 0063 Stage 06 build cannot ship `SyncStateSpec` as `IReadOnlySet<SyncState>?` until either (a) ADR 0036-A1 exposes the enum, OR (b) `SyncStateSpec.AcceptableStates` is changed to `IReadOnlySet<string>?`. Option (a) is preferred — typed surface is the canonical authoring model; this intake files (a).

## Predecessor

ADR 0036 (sync-state surface) — Accepted on `origin/main`. Currently specifies:

- 5 sync states with ARIA roles + encoding-contract identifiers `healthy / stale / offline / conflict / quarantine`
- UI surface conventions per ADR 0036 (banner / toast / aria-live treatments)
- No public C# `SyncState` enum exposed at the foundation tier

**Why amendment, not new ADR:** the enum exposure is intrinsic to ADR 0036's contract — it's a surface-completion of the existing encoding contract. New ADR would over-fragment the sync-state surface.

## Industry prior-art

- **ADR 0028-A6.1's `VersionVector.channel: enum { Stable, Beta, Nightly }`** — same shape (PascalCase enum derived from canonical identifiers); Sunfish-internal precedent
- **ADR 0028-A8.4's `formFactor` enum** — same shape; in-cohort precedent
- **`Sunfish.Kernel.Audit.AuditEventType`** — `readonly record struct` with string `Value` form (alternate pattern; rejected for SyncState because the discrete-cardinality 5-value set is well-suited to a flat enum)

## Scope

- **Public enum exposure:**

  ```csharp
  namespace Sunfish.Foundation.UI;

  public enum SyncState
  {
      Healthy,    // matches encoding-contract identifier "healthy"
      Stale,      // matches "stale"
      Offline,    // matches "offline"
      Conflict,   // matches "conflict"
      Quarantine  // matches "quarantine"
  }
  ```

- **Encoding round-trip:** PascalCase ↔ canonical-identifier string; `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` round-trippable both directions per existing camelCase/snake_case-to-enum convention (per ADR 0028-A7.8 + ADR 0028-A8.4 precedents)
- **Backward compatibility:** existing consumers using string-form sync-state continue to work; the enum is additive
- **Documentation:** apps/docs entry for ADR 0036 gains an §"Enum exposure" subsection naming the public type
- **Audit / telemetry:** none; this is a pure type-exposure amendment

## Dependencies and Constraints

- **Hard predecessor:** ADR 0036 itself (Accepted on origin/main)
- **Hard consumer:** ADR 0063 Stage 06 build (`SyncStateSpec` consumes the enum). ADR 0063 PR #411's auto-merge enabled post-A1 fixes; this intake's authoring lands AFTER ADR 0063 settles to avoid forward-references.
- **Effort estimate:** small (~2–4h authoring + council review). Mechanical type-exposure amendment.
- **Council review posture:** pre-merge canonical per cohort discipline (14-of-14 substrate amendments needed council fixes; structural-citation failure rate 10-of-14 (~71%) — the cohort lesson is "even small amendments benefit from council").

## Affected Areas

- packages/foundation-localfirst (or wherever ADR 0036's sync-state surface lives in code) — add public `Sunfish.Foundation.UI.SyncState` enum + the encoding-contract round-trip helpers
- packages/foundation-mission-space (post-ADR-0063 build) — `SyncStateSpec.AcceptableStates` consumes the enum
- apps/docs/foundation-localfirst/sync-state/ — schema documentation update

## Downstream Consumers

- **ADR 0063 Stage 06 build** — unblocks `SyncStateSpec` as `IReadOnlySet<SyncState>?` (typed authoring surface)
- **Future spec ADRs** consuming sync-state (any future amendment to ADR 0062 or downstream that needs to gate on sync state)

## Next Steps

Promote to active workstream when CO confirms; XO authors the A1 amendment; pre-merge council; merge under the same auto-merge-disabled-then-re-enabled pattern as prior substrate amendments. Recommend authoring **after** ADR 0063 settles (currently PR #411 auto-merge enabled post-A1).

## Cross-references

- Parent: ADR 0063-A1.2 (halt-condition) + ADR 0063-A1.17 (sibling-amendment declaration)
- Parent ADR: ADR 0036 (sync-state surface)
- Council review: `icm/07_review/output/adr-audits/0063-council-review-2026-04-30.md` (F2 drove A1.2 which in turn declared this intake)
- Sibling intakes: PR #409 (ADR 0031-A1 Bridge subscription-event-emitter), PR #412 (ADR 0007-A1 BusinessCaseBundleManifest.Requirements field)
