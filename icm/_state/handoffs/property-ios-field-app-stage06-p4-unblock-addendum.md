# W#23 Phase 4 Unblock Addendum — Bridge field-event + field-blob endpoints

**Date**: 2026-05-04
**Resolves**: `cob-question-2026-05-04T17-50Z-w23-p4-bridge-endpoints.md`
**Augments**: `icm/_state/handoffs/property-ios-field-app-stage06-handoff.md` (W#23 Stage 06 hand-off)
**Decision**: **Option (b)** — inline into **W#23 Phase 4.5 scope expansion**. New workstream row NOT created.

---

## Decision

COB beacon flagged that W#23 P4 (URLSession sync engine on iOS) is implementation-ready but cannot smoke-test end-to-end because the target Bridge endpoints don't exist:

- `POST /api/v1/field/event` — accept a field-event envelope from iOS
- `POST /api/v1/field/blob/<sha256>` — accept a content-addressed blob (photo / signature / form attachment)

COB asked: (a) author a W#28 follow-up hand-off, OR (b) inline into a W#23 Phase 4.5 scope expansion.

**XO chooses (b).** Reasoning:

- **Conceptually iOS-coupled**: field-event upload is mobile-domain, not public-listing domain. Even though W#28 owns the Bridge route *family*, the field-event sub-tree is W#23's iOS workstream.
- **Avoids parallel-PR drift**: a new W#28 follow-up means two workstreams contending for the same Bridge route file; harder to merge cleanly.
- **Ships faster**: COB has the iOS side ready; coupled implementation lands in one PR cycle.
- **Audit-ownership stays clean**: W#23 already owns the iOS-side audit emission; extending Bridge-side audit emission for the same flow keeps the audit story coherent.

W#28 Public Listings + W#23 iOS field app are both legitimate consumers of the `/api/v1/...` Bridge route family; the key is that route ownership is per-resource-tree (`/api/v1/field/...` is W#23's tree), not per-route-family.

Phase 4.5 lands as a **single PR** that includes both the iOS-side (URLSession + retry queue) AND the Bridge-side (2 endpoints + defense pipeline composition).

---

## Phase 4.5 scope (additive to Phase 4)

### Bridge-side endpoints

#### 1. `POST /api/v1/field/event`

**Request**:

- Auth: pairing-token via `Authorization: Bearer <pairing-token-jwt>` per ADR 0028-A2.6 (the iOS-paired-device auth pattern; same shape as W#23 P0 pairing surface)
- Body: JSON envelope per ADR 0028-A1 (post-A9 wire shape — already shipped via PR #516 W#23 P3 substrate types):
  - `eventId` (UUID v7)
  - `tenantId`
  - `actorId`
  - `eventType` (string discriminator)
  - `payload` (JSON object; per-event-type schema)
  - `capturedAt` (RFC 3339 UTC)
  - `capturedUnderKernel` (SemVer per A6.11)
  - `capturedUnderSchemaEpoch` (uint32 per A6.11)
  - `signature` (Ed25519 over canonical-JSON; per ADR 0028 §A2.6)
- Defense pipeline: reuse W#28 P5b pattern — Layer 4 hard-reject for malformed envelopes; Layer 5 manual-triage for ambiguous failures
- Idempotency: server keys on `eventId`; duplicate POST returns the original 200 response (no double-write)

**Response**:

- 200 + `{"eventId": "...", "accepted_at": "..."}` — accepted; queued for replication
- 400 — schema validation failed (returns array of validation errors)
- 401 — invalid pairing-token
- 409 — eventId conflict with different content (signature drift)
- 422 — schema epoch unsupported (Bridge older than client)

**Audit emission**:

- New `AuditEventType` constants on `Sunfish.Kernel.Audit.AuditEventType`:
  - `FieldEventAccepted` (one per successful accept)
  - `FieldEventRejected` (one per non-200; payload includes status + reason)
- Audit record references the `eventId` and the originating `pairing-token`'s `deviceId` (from the JWT claim)
- Emit synchronously from the route handler; failed audit emission rejects the event with 500 (per W#32 both-or-neither pattern)

#### 2. `POST /api/v1/field/blob/<sha256>`

**Request**:

- Auth: same pairing-token as `/field/event`
- Path param: `sha256` (the content-addressed hash; lowercase hex; 64 chars)
- Body: raw bytes (typical: image/jpeg or image/png; possibly other content types) — `Content-Type` header carries the MIME
- Max size: 10 MiB per blob (configurable via Bridge config; default reflects mobile-photo realistic ceiling)
- Server validates: SHA-256 of body matches path param; reject with 400 if mismatch

**Response**:

- 200 + `{"sha256": "...", "blob_url": "/api/v1/field/blob/<sha256>"}` — stored
- 400 — SHA-256 mismatch
- 401 — invalid pairing-token
- 413 — payload too large (config-bounded)

**Audit emission**:

- New `AuditEventType` constants:
  - `FieldBlobAccepted`
  - `FieldBlobRejected`
- Audit record carries `sha256` + `byteCount` + `mimeType` + `deviceId`

### iOS-side (already in scope from W#23 P4 hand-off)

URLSession sync engine + retry queue + pairing-token attachment. No additional work required beyond what the original P4 hand-off describes.

---

## Acceptance criteria for Phase 4.5

- [ ] Both endpoints implemented in `accelerators/bridge/` matching W#28 P5b defense pipeline pattern
- [ ] 4 new `AuditEventType` constants on `Sunfish.Kernel.Audit.AuditEventType`
- [ ] Audit emission synchronous from route handler (W#32 both-or-neither)
- [ ] Idempotency on `eventId` (duplicate POST → original response)
- [ ] SHA-256 path-param verification on blob endpoint
- [ ] Pairing-token validation (Bearer JWT; same shape as W#23 P0 surface)
- [ ] Tests: ≥6 endpoint tests per route covering accept/reject paths + audit assertion
- [ ] iOS-side smoke test from a paired device → Bridge → audit record visible in `/api/v1/audit/...` query

---

## Halt-conditions (escalate to XO if hit)

1. **Pairing-token JWT shape mismatch**: if W#23 P0's pairing surface emits a token shape Bridge can't parse with existing JWT lib, halt + write `cob-question`. Likely fix: small JWT-issuer alignment in W#23 P0 retroactively.
2. **Schema-epoch policy unclear**: if a paired iOS device sends `capturedUnderSchemaEpoch` that Bridge doesn't recognize (older or newer than current), the policy is "reject 422 with current epoch in response body so client can self-update." If COB finds this insufficient, halt + escalate.
3. **Defense pipeline composition with W#28 P5b**: if the existing W#28 defense pipeline pattern doesn't compose cleanly with `/field/event`'s requirements (different envelope shape; different auth tier), halt + escalate.
4. **Blob-storage backend choice**: P4.5 doesn't specify the backend (S3 / local disk / content-addressed Loro store). Default for Phase 1 is local disk under `accelerators/bridge/var/blobs/<sha256[0:2]>/<sha256>`. If COB needs durability beyond a single Bridge node, halt + escalate (cluster-level blob storage is a separate ADR; not P4.5 scope).

---

## Cross-references

- Parent hand-off: `icm/_state/handoffs/property-ios-field-app-stage06-handoff.md`
- Beacon being resolved: `icm/_state/research-inbox/cob-question-2026-05-04T17-50Z-w23-p4-bridge-endpoints.md` (move to `_archive/` in this PR)
- ADR 0028 §A2.6 (mobile pairing-token auth pattern)
- ADR 0028 §A6.11 (schemaEpoch + kernel-version envelope fields)
- ADR 0028 §A9 (post-A9 envelope wire shape)
- W#23 P3 substrate types (PR #516)
- W#23 P3.5 JsonCanonical Swift mirror (PR #517)
- W#28 P5b defense pipeline (PR #376) — pattern reference for inbound rejection handling
- W#32 both-or-neither audit-emission convention

---

## Notes for COB

- **Phase ordering inside the PR**: implement Bridge endpoints first (with mock iOS client tests); then wire the iOS-side URLSession to the real endpoints. This way the Bridge-side defense pipeline is testable in isolation before iOS smoke test.
- **Audit-record format**: use `MigrationAuditPayloads`-style alphabetized keys per cohort convention.
- **Run the naming-collision tool** before adding any new types: `tools/naming/check.py auto FieldEventAccepted` — verify the AuditEventType constants don't collide.

W#23 cohort progress matrix (current state):

| Phase | Status | PR |
|---|---|---|
| P0 — pairing surface | merged | #478 |
| P1 — SwiftUI scaffold | merged | #498 |
| P2 — GRDB persistence | merged | #511 |
| P3 — event envelope substrate | merged | #516 |
| P3.5 — JsonCanonical Swift mirror | merged | #517 |
| **P4 + P4.5 (this addendum)** | ready-to-build | — |
| P5 — pairing flow | queued | — |
| P6 — queue-status home | queued | — |
| P7 — TestFlight build | queued | — |
| P8 — ledger flip | queued | — |

Phase 4 + 4.5 should ship as a single PR (single-PR atomic per W#34 P1 precedent for tightly-coupled cross-tier work).
