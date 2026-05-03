# Hand-off addendum — W#23 iOS Field-Capture App: post-A9 envelope augmentation

**From:** XO (research session)
**To:** sunfish-PM session (COB)
**Created:** 2026-05-01
**Augments:** [`property-ios-field-app-stage06-handoff.md`](./property-ios-field-app-stage06-handoff.md) + [`property-ios-field-app-stage06-addendum.md`](./property-ios-field-app-stage06-addendum.md)
**Spec source:** [ADR 0028 amendment A9 + A9.8 council fixes](../../docs/adrs/0028-crdt-engine-selection.md) (PR #429 merged 2026-05-01)
**Status:** Augments W#23's existing `ready-to-build` hand-off; W#23 ledger row remains `ready-to-build` (not flipped — addendum updates the spec the existing hand-off implements).

---

## Why this addendum

W#23's existing Stage 06 hand-off was authored 2026-04-30 against ADR 0028-A1 (post-A2/A3/A4) — the iOS Phase 2.1 envelope shape known at the time. **A9 (PR #429 merged 2026-05-01) augments that envelope shape with two new fields per A6.11 + A7.5:**

- `capturedUnderKernel: SemVer` — kernel version running on iPad at capture time
- `capturedUnderSchemaEpoch: uint32` — schema epoch the iPad was on at capture time

Plus 1 new `AuditEventType` constant: `PreA9EnvelopeConsumed` (per A9.3 merge-boundary fallback for pre-A9 envelopes).

Plus a merge-boundary fallback contract for pre-A9-shape envelopes (per A9.3.1 + A9.3.2 + A9.3.3).

**This addendum updates W#23's iOS-side capture path + Anchor-side merge boundary** to consume the post-A9 envelope shape. The original W#23 hand-off's other 8 phases (SwiftUI scaffold, GRDB+SQLCipher persistence, sync engine, pairing flow, queue UX, TestFlight, ledger flip) remain unchanged.

**Per A9.7 cohort discipline:** A9's substrate-tier amendment underwent pre-merge council; A9.8 absorbed the 2 Required council fixes (schemaEpoch citation correction; build-time-vs-DI tradeoff documentation; pre-A9 fallback known-limitation). This addendum consumes the post-A9.8 surface.

---

## Files updated (in W#23 hand-off scope; consume the post-A9 surface)

### Phase 3 (Event envelope + canonicalization) — augmented

W#23 Phase 3's envelope construction + canonicalization MUST add the 2 new fields:

```text
A1Envelope (post-A9) ::= {
    device_local_seq:           uint64,           // existing per A1
    captured_at:                ISO 8601 UTC,     // existing per A1
    device_id:                  string,           // existing per A1
    event_type:                 enum,             // existing per A1
    payload:                    JSON-canonical bytes,  // existing per A1

    // NEW (A9):
    captured_under_kernel:      SemVer,           // kernel version at capture time
    captured_under_schema_epoch: uint32           // schema epoch at capture time
}
```

**JSON canonical shape** (per `Sunfish.Foundation.Crypto.CanonicalJson.Serialize`; camelCase per ADR 0028-A7.8):

```json
{
  "deviceLocalSeq": 12345,
  "capturedAt": "2026-05-01T03:30:00Z",
  "deviceId": "ipad-...",
  "eventType": "InspectionPhotoCaptured",
  "payload": {...},
  "capturedUnderKernel": "1.3.0",
  "capturedUnderSchemaEpoch": 7
}
```

### Phase 3 capture-time population path (post-A9)

Per A9.2 + A9.8.2 council fix: **build-time constant vs runtime DI** is a Stage 06 decision deferred to W#23. **Default expectation: runtime DI** for testability (unit tests can mock the kernel-version provider; build-time-constant forces tests to use the actual installed kernel version, limiting testable scenarios).

**Recommended W#23 implementation:**

- **`IKernelVersionProvider`** Swift protocol (matching the .NET `IKernelVersionProvider` interface for consistency with future cross-platform work):
  ```swift
  protocol IKernelVersionProvider {
      func currentKernelVersion() -> String
  }
  ```
- **`DefaultKernelVersionProvider`** reads from a `Bundle.main.infoDictionary` entry (`SunfishKernelVersion`) baked in at build time via the iOS app's plist. Default; production.
- **`MockKernelVersionProvider`** for unit tests; allows synthetic kernel versions across multiple test scenarios.
- DI registration via the iOS app's existing service-locator (per W#23 Phase 1 SwiftUI scaffold).

**`capturedUnderSchemaEpoch` population:**

- **`ISchemaRegistryProvider`** Swift protocol — reads from the iPad's local schema-registry view (per ADR 0001 schema-registry-governance — note that ADR 0001 governs registry tier-naming, not schemaEpoch itself; the canonical schemaEpoch reference is **paper §7.1 + §7.4** per ADR 0028-A10 retraction). The iPad's local cache of the workspace's current schemaEpoch is read at envelope-construction time.
- **`DefaultSchemaRegistryProvider`** reads from a SQLite-cached schema-registry snapshot (synchronized on each Anchor handshake per W#23 Phase 4 sync engine).
- **`MockSchemaRegistryProvider`** for unit tests.

**Population timing:** at `IFieldEventQueue.EnqueueAsync`-equivalent time (Swift: `enqueue(_ event:)` on the event-queue actor). Local + sub-millisecond per A9.2.

### Phase 4 (Sync engine + Bridge `POST /api/v1/field/event`) — augmented

W#23 Phase 4's outbound sync engine MUST handle the **pre-A9 envelope fallback** at the merge boundary (server-side; runs in Anchor's request handler, not in iOS):

Per A9.3 + A9.8.3 council clarification: **pre-A9 envelope handling at the merge boundary** when an iPad sends a pre-A9-shape envelope (no `capturedUnderKernel` AND no `capturedUnderSchemaEpoch`):

1. **Fallback to upload-time version-vector.** The merge service uses the iPad's *current* (upload-time) `MissionEnvelope.VersionVector.kernel` + `schemaEpoch` as the implicit capture-context. This is the legacy behavior — pre-A9 envelopes lose the capture-time-vs-upload-time distinction.
2. **Sequester per A6.11.3 if cross-epoch surfaces.** If the iPad's *current* `schemaEpoch` does not match Anchor's current epoch AND the envelope lacks `capturedUnderSchemaEpoch`, sequester the event to a `LegacyEpochEvent` audit-record per A7.5.3. Hard-dropping is forbidden.
3. **Emit `PreA9EnvelopeConsumed` audit** (new constant; W#23 ships) with `(remote_node_id, device_local_seq, fallback_kernel, fallback_schema_epoch)`. Dedup at the emission boundary per A6.5.1 pattern: at most one emission per `(remote_node_id, fallback_kernel, fallback_schema_epoch)` tuple per **24-hour rolling window** (matches `LegacyDeviceReconnected` cadence; pre-A9 envelopes are a long-tail concern).

**Known limitation (per A9.8.3):** the upload-time vector applies to ALL queued events, regardless of when individual events were captured. An iPad that captured events at kernel 1.2.0 and then upgraded to 1.3.0 will have ALL its pre-A9 queue events tagged with the 1.3.0 fallback vector. **W#23's Stage 06 migration UX MUST surface this** (e.g., *"Events captured before <date> use approximate version-tagging; precise version-tagging available for events captured after the iPad's last upgrade"*).

### New audit constant (W#23 Phase 4 ships)

`AuditEventType` MUST gain 1 new constant in `packages/kernel-audit/AuditEventType.cs`:

```csharp
public static readonly AuditEventType PreA9EnvelopeConsumed = new("PreA9EnvelopeConsumed");
```

Per A1.7 audit-emission convention, the dedup wiring is at the emission boundary (consume the existing `VersionVectorAuditPayloads` factory pattern from W#34's `Sunfish.Foundation.Versioning` package — W#34 P4 shipped a similar dedup approach via `ConcurrentDictionary` keyed on the dedup-tuple).

---

## Acceptance criteria (post-A9 augmentation; W#23 Phase 3 + Phase 4 specific)

- [ ] iOS envelope shape augmented per A9.1 with `capturedUnderKernel` + `capturedUnderSchemaEpoch`
- [ ] `IKernelVersionProvider` + `DefaultKernelVersionProvider` + `MockKernelVersionProvider` Swift protocols + impls
- [ ] `ISchemaRegistryProvider` + `DefaultSchemaRegistryProvider` + `MockSchemaRegistryProvider` Swift protocols + impls
- [ ] CanonicalJson round-trip test: augmented envelope serializes + deserializes; new fields preserved
- [ ] Forward-compat test (per A9.4): post-A9 envelope received by hypothetical pre-A9 receiver; CanonicalJson unknown-key tolerance preserves the new fields silently
- [ ] Backward-compat test (per A9.4 + A9.8.3): pre-A9-shape envelope (synthetic; no capture-context fields) consumed by post-A9 merge-boundary; fallback applied per A9.3; `PreA9EnvelopeConsumed` audit emitted; cross-epoch case sequesters to `LegacyEpochEvent`
- [ ] `PreA9EnvelopeConsumed` AuditEventType constant added; dedup tested (50 pre-A9 envelopes from same `(remote_node_id, fallback_kernel, fallback_schema_epoch)` within 1 hour → 1 emission only)
- [ ] Migration UX surface in W#23's iOS app names the perma-fallback known limitation per A9.8.3

---

## Halt-conditions (cob-question if any of these surface; ADD to W#23's existing 7 halt-conditions per the original hand-off)

8. **Build-time vs runtime DI confusion.** Per A9.8.2 the default is **runtime DI** for testability. If COB starts with a build-time constant for `capturedUnderKernel` (using `Bundle.main.infoDictionary` directly without an `IKernelVersionProvider` abstraction), file `cob-question-*` beacon — testability gap is the A9 council finding; default-to-DI is the resolution.

9. **`capturedUnderSchemaEpoch` source confusion.** Per A9.8.1 council retraction: ADR 0001 does NOT define schemaEpoch; the canonical reference is **paper §7.1 + §7.4**. The iPad's local SQLite-cached schema-registry view is the read source. If COB hits ADR 0001 for the schemaEpoch semantic and finds it absent (which it is), file `cob-question-*` beacon — the canonical reference is the paper, not ADR 0001.

10. **Pre-A9 envelope fallback at merge boundary — perma-fallback race condition.** Per A9.8.3 known limitation: an iPad with events captured at multiple kernel versions queued (e.g., kernel 1.2.0 → 1.3.0 mid-queue) will have ALL its pre-A9 events tagged with the 1.3.0 fallback vector. W#23's migration UX MUST surface this. If the UX surfacing feels awkward / unclear / unnecessary, file `cob-question-*` beacon — the limitation is real but the UX shape is COB's call.

11. **`LegacyEpochEvent` audit constant cross-package coordination.** Per A9.6 + A8.3 rule 5, the `LegacyEpochEvent` audit constant is declared in W#35 Foundation.Migration substrate (already built per PR #446). W#23 Phase 4 consumes it but does NOT redeclare it. If COB sees a duplicate-declaration build error, file `cob-question-*` beacon — the constant lives in `Sunfish.Foundation.Migration.Audit` / `kernel-audit/AuditEventType.cs`, not in W#23-specific code.

---

## Cited-symbol verification

**Existing on origin/main (verified 2026-05-01):**

- ADR 0028-A9 + A9.8 (PR #429 merged) — substrate spec source ✓
- ADR 0028-A6.11 + A7.5 (post-A7 fixes; PR #395 merged) — merge-boundary contract ✓
- ADR 0028-A6.5.1 (a.k.a. A7.4 audit dedup pattern; PR #395) ✓
- ADR 0028-A10 retraction (PR #436 merged) — schemaEpoch citation now points to paper §7.1 + §7.4 ✓
- W#34 Foundation.Versioning (PR #423; built) — provides `VersionVector` type for upload-time vector fallback ✓
- W#35 Foundation.Migration (PRs #439-#446; built) — provides `LegacyEpochEvent` audit constant per A8.3 rule 5 ✓
- `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` — encoding contract ✓
- Paper §7.1 + §7.4 — canonical schemaEpoch reference (not ADR 0001 per A10 retraction) ✓

**Introduced by W#23 Phase 3 + Phase 4** (per this addendum):

- iOS envelope schema augmentation: `capturedUnderKernel: SemVer` + `capturedUnderSchemaEpoch: uint32`
- Swift protocols: `IKernelVersionProvider`, `ISchemaRegistryProvider` + Default + Mock impls
- 1 new `AuditEventType` constant: `PreA9EnvelopeConsumed`
- Migration-UX surfacing for perma-fallback limitation

---

## Cross-references

- Spec source: ADR 0028-A9 + A9.8 (PR #429 merged 2026-05-01)
- Council that drove A9.8 fixes: PR #433 (merged); council file at `icm/07_review/output/adr-audits/0028-A9-council-review-2026-05-01.md`
- Parent retraction: ADR 0028-A10 (PR #436 merged) — schemaEpoch citation correction
- Existing W#23 hand-off: `icm/_state/handoffs/property-ios-field-app-stage06-handoff.md` (8 phases; ~28.5h)
- Existing W#23 addendum: `icm/_state/handoffs/property-ios-field-app-stage06-addendum.md` (post-A2/A3/A4 envelope work)
- Sibling cohort substrate: W#34 Foundation.Versioning (built); W#35 Foundation.Migration (built); W#36 Bridge subscription emitter (queued); W#37 Foundation.UI.SyncState (queued)
