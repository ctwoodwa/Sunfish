# Intake — ADR 0028 Amendment A1.x (companion to A6+A7): iOS event envelope capture-context tagging

**Date:** 2026-04-30
**Requestor:** XO (declared by ADR 0028-A7.5 / A7.13 as the coordinated companion amendment to A6+A7)
**Request:** Amend ADR 0028's A1 (iOS Phase 2.1 append-only event queue) so the per-event envelope carries `captured_under_kernel: SemVer` + `captured_under_schema_epoch: uint32`. This is the A1-side ratification of A6.11 (per A7.5).
**Pipeline variant:** `sunfish-api-change` (envelope schema change; foundation-tier surface)
**Stage:** 00 — pending CO promotion to active

---

## Problem Statement

ADR 0028-A6 (post-A7) specifies that mixed-version Sunfish clusters use a federation-time version-vector handshake (per A6.1–A6.10) plus a per-event capture-context tagging path for the iOS append-only queue (per A6.11, added by A7.5). The capture-context fields (`captured_under_kernel` + `captured_under_schema_epoch`) need to live in the iOS A1 envelope itself — but A1 currently specifies the envelope as `{ device_local_seq, captured_at, device_id, event_type, payload }` with no version-vector data.

A6.11 declares the augmentation needed; the actual A1-side ratification is this companion amendment. Without it, A6.11's merge-boundary contract has no on-the-wire data to evaluate against.

## Predecessor

ADR 0028-A1 (iOS Phase 2.1 mobile reality check). Already on `origin/main` post-A2/A3/A4 fixes. This amendment adds two optional-by-default fields to the envelope; per A7.5's verification of `CanonicalJson.Serialize` unknown-field-tolerance, the change is forward-compat (older receivers ignore the fields silently; newer receivers consume them).

**Why amendment, not new ADR:** A1 owns the iOS envelope schema. Adding two fields to that schema is mechanically inside A1's contract surface.

## Scope

- **A1 envelope schema augmentation:** add `captured_under_kernel: SemVer` (kernel version running on iPad at capture time) + `captured_under_schema_epoch: uint32` (schema epoch the iPad was on at capture time) to the existing envelope shape.
- **Capture-time population path:** specify how iOS captures these values at envelope-construction time (likely a kernel-version constant + a schema-epoch lookup on the device at the moment the event is enqueued).
- **Backward-compat for existing queued events:** if an iPad already has events queued under the pre-A1.x envelope shape (no capture-context fields), the merge boundary's behavior — currently A6.11.2 says evaluate against `event.captured_under_kernel`; if the field is absent, fall back to the iPad's *current* version-vector at upload time? Or sequester? Spec.
- **Acceptance criteria:** envelope round-trip via `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` with the new fields; merge-boundary unit test that sequesters cross-epoch events into `LegacyEpochEvent` audit records (per A7.5.3); pre-A1.x-shape envelopes are handled deterministically per the spec'd fallback.

## Dependencies and Constraints

- **Hard predecessor:** ADR 0028-A6 (declared the augmentation) + A7 (council-fixed A6) — both must land before A1.x to avoid forward-references against still-in-flight contracts.
- **Soft predecessor:** ADR 0001 schema-registry-governance (provides the `schemaEpoch` semantic A1.x consumes).
- **Effort estimate:** small (~2–4h authoring + council review). Mechanical envelope-shape change with a single new code path (capture-time field population) and a single merge-boundary test.
- **Council review posture:** pre-merge canonical per cohort discipline — substrate-tier amendment, even though small. Cost ~30 min council; benefit unchanged from cohort precedent.

## Affected Areas

- foundation: `Sunfish.Foundation.Versioning` consumers (A6.11 merge-boundary uses A1's envelope)
- accelerators/anchor: merge boundary consumes the new fields
- accelerators/anchor-mobile-ios (if/when scaffolded per W#23 hand-off): captures the new fields at envelope-construction time
- W#22 / W#23: any iOS-event consumer downstream

## Downstream Consumers

- **W#23 iOS Field-Capture App** — the consumer that produces these envelopes; A1.x specifies what the iOS app must populate at capture time.
- **Anchor merge service** — consumes the new fields per A6.11.2 / A6.11.3.
- **Audit substrate** — `LegacyEpochEvent` audit-record producer (per A7.5.3).

## Next Steps

Promote to active workstream when CO confirms; XO authors the A1.x amendment; pre-merge council; merge under the same auto-merge-disabled-then-re-enabled pattern as A6 (PR #395) + A7. Recommend authoring **after** A6+A7 land (currently auto-merge enabled on PR #395 post-A7); A1.x does not block W#33 Stage 02 design but must land before W#33 Stage 06 build emits its first iOS envelope with the augmented shape.

## Cross-references

- Parent: ADR 0028-A7.5 / A7.13 (declaration)
- Parent ADR: ADR 0028 (CRDT Engine Selection) + amendments A1 + A6 + A7
- Related discovery: `icm/01_discovery/output/2026-04-30_mission-space-matrix.md` §5.8 (the original gap)
- Related intake: `icm/00_intake/output/2026-04-30_version-vector-compatibility-intake.md` (parent A6 intake)
- Council review: `icm/07_review/output/adr-audits/0028-A6-council-review-2026-04-30.md` (F5 / council A5; the finding that drove A7.5)
