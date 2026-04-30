# Workstream #23 — iOS Field-Capture App (substrate v1) — Stage 06 hand-off

**Workstream:** #23 (`accelerators/anchor-mobile-ios/` — SwiftUI native iOS field-capture app substrate)
**Spec sources:**
- Intake: [`property-ios-field-app-intake-2026-04-28.md`](../../00_intake/output/property-ios-field-app-intake-2026-04-28.md)
- ADR 0028 + A1 + A2 + A3 (CRDT engine selection — mobile reality check + per-event-type LWW table + URLSession config + capability scope + cited-symbol fixes; PRs #342 + #351)
- ADR 0048 + A1 + A2 (multi-backend MAUI — mobile-scope clarification carving out Field-Capture as SwiftUI sibling; PR #347)
- ADR 0061 (Three-Tier Peer Transport — mDNS / Mesh VPN / Managed Relay; A1–A4 amendments landed PR #299)
- ADR 0046-A1 (historical-keys projection) for operator-key read-back
- ADR 0054 (electronic signatures + canonicalization) — referenced for Phase X capture-flow follow-up

**Pipeline variant:** `sunfish-feature-change` (net-new accelerator family; zero existing callers)
**Estimated effort:** 25–35h focused sunfish-PM time (substrate only; capture flows are follow-up hand-offs)
**Decomposition:** 8 phases shipping as ~5 separate PRs
**Prerequisites:**
- ✓ ADR 0028 A1+A2+A3 (mobile substrate decisions; per-event-type LWW table)
- ✓ ADR 0048 A1+A2 (MAUI vs SwiftUI carve-out)
- ✓ ADR 0061 (transport substrate; iOS Phase 2.1 uses Tier 3 only per ADR 0028-A1.4.1)
- ✓ ADR 0046-A1 (historical-keys projection for operator-key signature verification)
- 🟡 W#28 Public Listings Bridge route family (`apps/bridge/Pages/...`) — Phase 4 outbound sync transport target. PR #303/#306/#308/#320/#321/#324/#334 shipped substrate; W#23 Phase 4 needs a `POST /api/v1/field/event` route that may not exist yet. Halt-condition if missing.

---

## Scope summary

This is the **substrate v1 hand-off**. It ships the SwiftUI app shell, local persistence, event-queue contract, outbound sync engine, pairing flow, queue-status UX, and TestFlight smoke test — but **NOT** the 6 capture flows themselves (receipts / assets / inspections / signatures / mileage / work-order-response). Each capture flow is a follow-up Stage 06 hand-off composed on top of this substrate.

Why substrate-first: per the W#19 / W#22 / W#27 / W#28 first-slice precedent, shipping a usable-but-narrow substrate that subsequent hand-offs build against is cleaner than a single 30-phase mega-hand-off. Each capture-flow hand-off will be ~3–6 phases targeting one domain.

1. **`accelerators/anchor-mobile-ios/` package scaffold** — Xcode project; SwiftUI shell; iOS 16+ baseline; Bundle ID `dev.sunfish.field` (sibling to Anchor's `dev.sunfish.anchor` per ADR 0048-A1.7.OQ-A1.2).
2. **Local persistence** — GRDB.swift over SQLite + optional SQLCipher (whole-database encryption per ADR 0028-A2.3); content-addressed blob store at app sandbox; queue table for outbound events.
3. **Event envelope contract** — per ADR 0028-A2.1 per-event-type LWW + forward-only-status guards. `device_local_seq` (uint64) + `captured_at` (ISO 8601 UTC) + `device_id` (per Phase 0 stub) + `event_type` (capture domain enum) + canonical-encoded `payload` (RFC 8785 / `Sunfish.Foundation.Canonicalization.JsonCanonical` per ADR 0054-A1).
4. **Outbound sync engine** — `URLSessionConfiguration.background` per ADR 0028-A2.2 settings (`discretionary=false`, `sessionSendsLaunchEvents=true`, file-based `uploadTask(with:fromFile:)` for blobs); retry semantics; queue compaction per ADR 0028-A2.7 (5000 events / 500 MB cap; 30/90-day TTL; SQLite VACUUM after ACK'd batches ≥100).
5. **Pairing flow with Anchor** — Phase 0 stub addendum surface (per W#19 Phase 3 stub precedent in lieu of a pre-ADR per W#23-queued-plan iteration N+3-DROPPED decision). Per-device install identity (Ed25519 root keypair generated locally on first launch + stored in Keychain with `kSecAttrAccessibleAfterFirstUnlock` per ADR 0028-A2.8); Anchor-issued pairing token consumed via Bridge HTTPS + bound to `device_id` derived from the install Ed25519 public key.
6. **Queue-status home screen + sync UX** — minimal SwiftUI home with queue-status row (`<events queued>` + `<MB blob storage>` + `<last successful sync>`); tap-to-force-sync; user-visible warning at 80% queue cap; user-visible block at 100% per ADR 0028-A2.7.
7. **TestFlight build + end-to-end smoke test** — submit to App Store Connect TestFlight; first user (BDFL + spouse + close team per W#23 intake item 8) installs; pair to Anchor; submit a smoke-test "Hello" event through to Anchor merge boundary.
8. **Ledger flip** — update `icm/_state/active-workstreams.md` row #23 → `built` (substrate-only; capture-flow follow-up hand-offs queued separately).

**NOT in scope** (deferred to follow-up hand-offs):
- All 6 capture flows (receipts / assets / inspections / signatures / mileage / work-order-response) — each is a separate Stage 06 hand-off composing on top of this substrate
- Tailscale / mesh-VPN transport upgrade (Phase 2.2; ADR 0061 Tier 2 work)
- Push notifications (Phase 2.2 per W#23 intake out-of-scope)
- App Store distribution (Phase 2.3; this hand-off ships TestFlight only)
- Live Activities / widgets / Apple Watch companion (Phase 2.3+)
- Full Sunfish kernel port to Swift (Phase 3+; per ADR 0028-A1.3 reconsider triggers)
- Android version (Phase 4+)
- iOS-direct-to-Anchor read API (Phase 2.2; W#23 Phase 2.1 ships read-only via Bridge HTTPS only per ADR 0028-A1.2)

---

## Phases

### Phase 0 — Per-device install identity stub (~1.5h)

Per W#19 Phase 3 stub precedent. Multi-device pairing surface is small enough to ship inline as a Phase 0 stub rather than a pre-ADR (per W#23-queued-plan iteration N+3 DROPPED decision).

**Files to create on the iOS side:**
- `accelerators/anchor-mobile-ios/Sources/Identity/InstallIdentity.swift` — `struct InstallIdentity` wrapping the local Ed25519 root keypair + Keychain persistence
- `accelerators/anchor-mobile-ios/Sources/Identity/InstallIdentity+Keychain.swift` — Keychain access (`kSecAttrAccessibleAfterFirstUnlock`; `kSecAttrSynchronizable=false`)
- `accelerators/anchor-mobile-ios/Sources/Identity/DeviceId.swift` — `struct DeviceId(value: String)`; derived from install Ed25519 public key (first 16 hex chars of SHA-256 of the public key)

**Files to modify on the Anchor side (.NET):**
- `accelerators/anchor/.../PairingToken.cs` (new) — minimal record `(PairingTokenId, DeviceId, IssuedAt, ExpiresAt, ConsumedAt?, Hmac)` matching the W#18 `VendorMagicLink` pattern; reuses `Sunfish.Foundation.Recovery.TenantKey.ITenantKeyProvider` (existing W#20 Phase 0 stub) for HMAC derivation with purpose label `field-pairing-token-hmac` (matching the W#18 `vendor-magic-link-hmac` precedent)
- `accelerators/anchor/.../IPairingService.cs` (new) — `IssuePairingTokenAsync(...)`, `ConsumePairingTokenAsync(...)`, `RevokePairingAsync(...)`
- `accelerators/anchor/.../HmacPairingService.cs` (new) — reference impl

**Halt-conditions for Phase 0:**
- If `ITenantKeyProvider` doesn't accept `field-pairing-token-hmac` purpose (e.g., the existing impl restricts purpose labels to a known set): HALT + `cob-question-*-w23-p0-tenant-key-provider-purpose.md`
- If `accelerators/anchor/Sunfish.Anchor.csproj` cannot reference `Sunfish.Foundation.Recovery` (csproj reference graph): HALT + verify with XO

**Gate:** PASS iff
- `dotnet build accelerators/anchor/Sunfish.Anchor.csproj` clean with new pairing-service types
- iOS side: `swift build` (or Xcode build) clean with InstallIdentity + DeviceId
- Round-trip test: install identity persists to Keychain + deterministic `device_id` derivation from public key
- Anchor-side test: HMAC-SHA256 over `(PairingTokenId, device_id, issued_at)` round-trips through issue → consume

**PR title:** `feat(anchor,anchor-mobile-ios): per-device pairing surface (W#23 Phase 0)`

### Phase 1 — `accelerators/anchor-mobile-ios/` package scaffold (~4h)

Xcode project + SwiftUI app shell + iOS 16+ baseline + GRDB.swift dependency.

**Files to create:**
- `accelerators/anchor-mobile-ios/Project.xcodeproj/` (Xcode project file; standard SwiftUI app template)
- `accelerators/anchor-mobile-ios/SunfishField/Info.plist` — `CFBundleIdentifier = dev.sunfish.field`; `MinimumOSVersion = 16.0`; `LSRequiresIPhoneOS = true`; `UIRequiredDeviceCapabilities = [armv7]`; `NSCameraUsageDescription` + `NSPhotoLibraryUsageDescription` (capture-flow phases populate these); `BGTaskSchedulerPermittedIdentifiers = [dev.sunfish.field.upload]`
- `accelerators/anchor-mobile-ios/SunfishField/SunfishFieldApp.swift` — `@main` `App` struct; minimal SwiftUI scene with empty `ContentView`
- `accelerators/anchor-mobile-ios/SunfishField/ContentView.swift` — placeholder `Text("Sunfish Field — Phase 1 scaffold")`
- `accelerators/anchor-mobile-ios/Package.swift` (Swift Package Manager wrapper; alternative to Xcode project for CI builds)
- `accelerators/anchor-mobile-ios/Package.resolved` (locked GRDB.swift version per pre-release-latest-first policy: latest stable 6.x as of 2026-04-30)
- `accelerators/anchor-mobile-ios/.gitignore` (Xcode + Swift defaults)
- `accelerators/anchor-mobile-ios/README.md` — package overview + dependency graph + iOS 16 baseline + build instructions

**SPM dependencies pinned:**
- `GRDB.swift` (latest 6.x stable) — SQLite ORM
- `swift-crypto` (Apple-maintained; for Ed25519 + HMAC parity with .NET side per ADR 0004)

NOT yet added (deferred to capture-flow phases): no Vision, no PencilKit, no PDFKit imports — those land per their consuming phase.

**Build verification on macOS:**
```bash
cd accelerators/anchor-mobile-ios
xcodebuild -scheme SunfishField -destination 'generic/platform=iOS Simulator' build
# OR if SPM-based:
swift build --triple arm64-apple-ios16.0-simulator
```

**Gate:** PASS iff `xcodebuild` (or `swift build`) clean on macOS host; `Info.plist` validates; SwiftUI shell launches in iOS Simulator showing the placeholder text.

**PR title:** `feat(anchor-mobile-ios): SwiftUI scaffold + Bundle ID + iOS 16 baseline (W#23 Phase 1)`

### Phase 2 — Local persistence: GRDB.swift + SQLCipher + blob store (~5h)

**Files to create:**
- `accelerators/anchor-mobile-ios/SunfishField/Persistence/AppDatabase.swift` — `DatabaseQueue` factory; `NSFileProtectionComplete` data protection; SQLCipher wrapping (per W#23 intake §"In scope" item 2)
- `accelerators/anchor-mobile-ios/SunfishField/Persistence/Schema/V1Migration.swift` — initial schema:
  - `event_queue` table — `(rowid INTEGER PRIMARY KEY AUTOINCREMENT, device_local_seq INTEGER UNIQUE NOT NULL, captured_at TEXT NOT NULL, event_type TEXT NOT NULL, payload BLOB NOT NULL, blob_ref TEXT NULL, queue_status TEXT NOT NULL DEFAULT 'pending', last_attempt_at TEXT NULL, attempt_count INTEGER NOT NULL DEFAULT 0)` — `queue_status` ∈ {`pending`, `uploading`, `acked`, `failed-permanent`}
  - `audit_local` table — `(rowid INTEGER PRIMARY KEY, occurred_at TEXT NOT NULL, event_type TEXT NOT NULL, payload BLOB NOT NULL)` — local-only audit log; mirrored to Anchor on next sync
  - Indexes: `event_queue(queue_status)`; `event_queue(captured_at)`
- `accelerators/anchor-mobile-ios/SunfishField/Persistence/EventQueueRecord.swift` — `struct EventQueueRecord: Codable, FetchableRecord, PersistableRecord`
- `accelerators/anchor-mobile-ios/SunfishField/Persistence/BlobStore.swift` — content-addressed blob store at `Library/Application Support/SunfishField/blobs/<sha256>.bin`; reference-counted; cleanup on next launch after sync ACK
- `accelerators/anchor-mobile-ios/SunfishField/Persistence/CompactionPolicy.swift` — per ADR 0028-A2.7: 5000-event hard cap; 500 MB blob cap; 30-day warning TTL; 90-day forced-foregrounded-sync TTL; `VACUUM` after batches ≥100

**SQLCipher key derivation:** per `Sunfish.Foundation.Recovery.PaperKeyDerivation` pattern but using `swift-crypto`'s `HKDF<SHA256>` from the install's Ed25519 root private key + tenant ID (TBD per pairing flow Phase 0; for Phase 2 ship a Phase-1-only-key derived from install root + sentinel `"sunfish-field-sqlcipher-v1"`; multi-tenant SQLCipher keys land in a follow-up hand-off when iOS supports multi-team).

**Halt-conditions:**
- GRDB.swift 6.x ABI changes from the Package.resolved version: HALT; verify with XO before bumping
- `NSFileProtectionComplete` interferes with background URLSession reads (uploads can't access encrypted DB while device is locked): HALT; document as known iOS gotcha + decide between `NSFileProtectionCompleteUntilFirstUserAuthentication` (allows background access post-first-unlock) or accept the limitation

**Gate:** PASS iff
- GRDB schema migration runs clean on first launch
- Round-trip: insert event → retrieve → equal
- SQLCipher: open with key, close, reopen with wrong key → fails; reopen with correct key → succeeds
- BlobStore: write blob → retrieve by SHA-256 hash → bytes match
- Compaction smoke test: insert 5001 events → 5001th insert blocked + user-visible error

**PR title:** `feat(anchor-mobile-ios): GRDB + SQLCipher persistence layer + blob store (W#23 Phase 2)`

### Phase 3 — Event envelope contract (~2.5h)

Per ADR 0028-A2.1 (per-event-type LWW table) + ADR 0028-A1.2 (envelope shape).

**Files to create:**
- `accelerators/anchor-mobile-ios/SunfishField/Events/EventType.swift` — `enum EventType: String, Codable { case Inspection, Receipt, Asset, Signature, Mileage, WorkOrderResponse }`
- `accelerators/anchor-mobile-ios/SunfishField/Events/EventEnvelope.swift` — `struct EventEnvelope: Codable { let deviceLocalSeq: UInt64; let capturedAt: Date; let deviceId: DeviceId; let eventType: EventType; let payload: Data; let blobRefs: [String]? }` — `payload` is canonical-encoded JSON per RFC 8785; `blobRefs` are SHA-256 content addresses for any binary attachments
- `accelerators/anchor-mobile-ios/SunfishField/Events/JsonCanonical.swift` — RFC 8785 / JCS canonicalizer in Swift; matches `Sunfish.Foundation.Canonicalization.JsonCanonical` byte-for-byte (cross-language test verifies)
- `accelerators/anchor-mobile-ios/SunfishField/Events/EventQueueService.swift` — `appendAsync(envelope:) -> async throws`; `nextPendingBatch(limit:) -> async [EventQueueRecord]`; `markAcked(deviceLocalSeq:)`; `markFailed(deviceLocalSeq:reason:)`

**Cross-language canonicalization test:** Phase 3 ships a smoke test (in `accelerators/anchor-mobile-ios/Tests/JsonCanonicalCrossLangTests.swift`) that loads a fixture file (~10 sample JSON inputs + their RFC 8785 outputs from the .NET-side `JsonCanonical.Canonicalize`) and verifies the Swift impl produces identical bytes. Fixtures live in `_shared/fixtures/json-canonical-rfc8785/` (new directory; cross-language test fixture).

**Halt-conditions:**
- RFC 8785 Swift impl diverges from .NET `JsonCanonical.Canonicalize` on any fixture: HALT; XO investigates which side has the bug
- `device_local_seq` overflow risk on long-lived install (uint64; would take ~5×10^11 events/sec for 1000 years to overflow — practically impossible). Not a halt-condition; just confirm the test exercises the high-value range.

**Gate:** PASS iff
- 10/10 cross-language canonicalization fixtures match byte-for-byte
- Event envelope round-trips through GRDB persistence (Phase 2 schema)
- `device_local_seq` is monotonically increasing across appends; uniqueness-violation test (insert duplicate seq → fails)
- Event-type enum exhaustively covers the 6 capture domains per ADR 0028-A2.1 table

**PR title:** `feat(anchor-mobile-ios): event envelope contract + RFC 8785 canonicalization (W#23 Phase 3, ADR 0028-A2.1)`

### Phase 4 — Outbound sync engine (~6h)

Per ADR 0028-A2.2 explicit `URLSessionConfiguration.background` settings. This is the largest phase.

**Files to create:**
- `accelerators/anchor-mobile-ios/SunfishField/Sync/SyncEngine.swift` — top-level coordinator; consumes `EventQueueService` (Phase 3) + manages background URLSession lifecycle
- `accelerators/anchor-mobile-ios/SunfishField/Sync/BackgroundUrlSession.swift` — singleton background session per the Apple-recommended pattern (one per app); identifier `dev.sunfish.field.upload`; settings per ADR 0028-A2.2
- `accelerators/anchor-mobile-ios/SunfishField/Sync/EventUploadTask.swift` — wraps `URLSession.uploadTask(with:fromFile:)`; serializes envelope to a temp file; tracks `device_local_seq` ↔ `URLSessionTask.taskIdentifier` mapping
- `accelerators/anchor-mobile-ios/SunfishField/Sync/BlobUploadTask.swift` — file-based blob upload (per ADR 0028-A2.2: file-based for OS-resumability)
- `accelerators/anchor-mobile-ios/SunfishField/Sync/SyncDelegate.swift` — `URLSessionDelegate` conformance; handles `urlSession(_:dataTask:didReceive:)`, `urlSession(_:task:didCompleteWithError:)`, and the special `NSURLErrorBackgroundSessionWasDisconnected` (-997) restart path per ADR 0028-A2.2
- `accelerators/anchor-mobile-ios/SunfishField/Sync/RetryPolicy.swift` — exponential backoff per `URLSessionConfiguration` defaults; `attempt_count` from Phase 2 schema; `failed-permanent` after 10 attempts (configurable)

**Bridge target endpoint** (per W#23 intake §"In scope" item 6 + ADR 0061 Tier 3):
- `POST /api/v1/field/event` — JSON envelope upload; returns `{ ack: true, server_ack_id: "..." }` on success
- `POST /api/v1/field/blob/<sha256>` — multipart file upload; returns `{ ack: true }` on success; idempotent on `<sha256>`

**Halt-condition:** if `apps/bridge/` does not yet have these endpoints, HALT + write `cob-question-*-w23-p4-bridge-endpoints.md` with the proposed shape. **W#28 Public Listings owns the Bridge route family** (per ADR 0028-A2.6 + W#28 intake); the field-event endpoints likely need a small W#28 follow-up hand-off OR can be added inline as a W#23 Phase 4.5.

**Background-task scheduling:** use `BGTaskScheduler` with task identifier `dev.sunfish.field.upload` (declared in Phase 1's Info.plist `BGTaskSchedulerPermittedIdentifiers`). Schedule on (a) app foreground, (b) capture flow submit, (c) network-connectivity-restored notification, (d) opportunistic on `discretionary=false` (which we set per ADR 0028-A2.2).

**Gate:** PASS iff
- URLSession config matches A2.2 settings exactly (smoke test asserts `discretionary == false`, `sessionSendsLaunchEvents == true`, `allowsCellularAccess == true`, `waitsForConnectivity == true`, `timeoutIntervalForResource >= 7*24*3600`)
- File-based upload survives `NSURLErrorBackgroundSessionWasDisconnected` simulation (manual test: airplane-mode mid-upload + airplane-off → upload resumes from byte 0 of a fresh attempt against the same source file)
- Empty-body POST integration test on iOS 16 baseline (per ADR 0028-A2.2 known gotcha — explicit test required)
- 10-event batch upload + dedup test: re-upload same events → server returns acks for already-acked events (idempotent)
- Retry path: failure → `attempt_count` increments → 10th failure → `failed-permanent`; user-visible error in queue-status row

**PR title:** `feat(anchor-mobile-ios,bridge): outbound sync engine + Bridge field-event endpoints (W#23 Phase 4)`

### Phase 5 — Pairing flow with Anchor (~4h)

Composes Phase 0's pairing-token surface with the iOS app's first-launch flow.

**iOS files:**
- `accelerators/anchor-mobile-ios/SunfishField/Onboarding/PairingFlow.swift` — SwiftUI screens: (1) "Enter pairing code from Anchor" (8-character alphanumeric); (2) "Pairing in progress…" (HTTPS POST to Bridge with the code); (3) "Paired! Welcome." or "Pairing failed: <reason>"
- `accelerators/anchor-mobile-ios/SunfishField/Onboarding/PairingService.swift` — `pairAsync(code: String) async throws -> PairingResult`; consumes pairing-token via `POST /api/v1/field/pair` Bridge endpoint
- `accelerators/anchor-mobile-ios/SunfishField/Onboarding/PairingResult.swift` — record `(tenantId: String, anchorBaseUrl: String, expiresAt: Date)`

**Anchor / Bridge files:**
- `apps/bridge/Pages/Field/Pair/{code}.cshtml` — Razor page rendering the pairing UI for the desktop user (Anchor opens this URL after issuing the code; user reads the code aloud or types it on the iPad)
- `apps/bridge/Controllers/FieldPairingController.cs` — `POST /api/v1/field/pair` endpoint; validates pairing-token via `IPairingService.ConsumePairingTokenAsync`; binds `device_id` to the consumed token; emits `FieldDevicePaired` audit event
- `accelerators/anchor/.../FieldPairingViewModel.cs` — Anchor desktop UI for issuing the pairing code (consumes `IPairingService.IssuePairingTokenAsync`)

**New `AuditEventType` constants** (per ADR 0049 + ADR 0058 audit pattern):
- `FieldDevicePairingTokenIssued` — emitted when Anchor user requests a pairing code
- `FieldDevicePaired` — emitted when iPad consumes the code successfully
- `FieldDevicePairingTokenExpired` — emitted on expiry without consumption
- `FieldDeviceRevoked` — emitted when Anchor user revokes a previously-paired device

**Halt-conditions:**
- Phase 4 Bridge endpoints not yet shipped (Phase 4 prereq): HALT; ship Phase 4 first
- `IPairingService` from Phase 0 doesn't expose `ConsumePairingTokenAsync` correctly: HALT + verify Phase 0 acceptance criteria

**Gate:** PASS iff
- End-to-end pairing test: Anchor issues code → iPad consumes → both sides have paired state
- Wrong-code rejection: iPad enters wrong code → 401 + audit event emitted
- Expired-code rejection: code expired before consumption → 410 Gone + audit event emitted
- 4 new audit events round-trip through `IAuditTrail.AppendAsync`

**PR title:** `feat(anchor-mobile-ios,bridge,anchor): pairing flow + 4 AuditEventType (W#23 Phase 5)`

### Phase 6 — Queue-status home screen + sync UX (~3h)

Per ADR 0028-A2.7 user-visible warning at 80% / block at 100%.

**Files to create:**
- `accelerators/anchor-mobile-ios/SunfishField/Home/HomeView.swift` — main SwiftUI scene post-pairing; lists capture-flow entry points (placeholder for Phase 7+ flows); queue-status row at the bottom
- `accelerators/anchor-mobile-ios/SunfishField/Home/QueueStatusRow.swift` — `<events queued>` + `<MB blob storage>` + `<last successful sync>`; tap-to-force-sync; color-coded (green / yellow at 80% / red at 100%)
- `accelerators/anchor-mobile-ios/SunfishField/Home/SettingsView.swift` — minimal settings: device ID display (read-only); paired tenant; "Unpair this device" button (calls `POST /api/v1/field/unpair`); sync-history view (last N attempts)

**Bridge endpoint:**
- `POST /api/v1/field/unpair` — emits `FieldDeviceRevoked`; bridge writes the revocation to Anchor's audit; iPad clears local Keychain entries on receipt

**Gate:** PASS iff
- Empty queue → "No events queued" state
- Queue with 100 events → row shows "100 events / X MB / Last sync: <date>"
- 80% queue threshold (4000 events) → yellow warning
- 100% queue threshold (5000 events) → red blocker; new captures blocked until queue drains
- Unpair flow: iPad clears Keychain + returns to PairingFlow

**PR title:** `feat(anchor-mobile-ios): home screen + queue-status UX + settings (W#23 Phase 6, ADR 0028-A2.7)`

### Phase 7 — TestFlight build + first end-to-end smoke test (~2h)

**Tasks:**
- Configure App Store Connect: TestFlight tester group (BDFL + spouse + close team per W#23 intake item 8); App ID `dev.sunfish.field`; signing certificate + provisioning profile
- Submit first build to TestFlight (manual upload OR Xcode Cloud OR Fastlane)
- First-pass smoke test:
  - Install on iPad
  - Pair with Anchor
  - Submit a synthetic "Hello" event (event_type = `Receipt`, payload = `{"hello": "world"}`)
  - Anchor merge boundary receives + processes
  - Anchor desktop UI shows the synthetic event in its received-from-field view (TBD; may need a tiny Anchor-side smoke UI as part of this phase)

**Halt-conditions:**
- Apple Developer Program membership not active for `dev.sunfish.field` Bundle ID: HALT; CO-class issue (signing cert + DUNS number + business membership)
- TestFlight rejection on first build (privacy manifest missing, etc.): HALT + iterate on Info.plist / privacy declarations

**Gate:** PASS iff TestFlight build is downloadable + smoke test completes end-to-end + audit trail on Anchor shows the event.

**PR title:** `chore(anchor-mobile-ios): TestFlight Phase 1 build + end-to-end smoke (W#23 Phase 7)`

### Phase 8 — Ledger flip (~0.5h)

Update `icm/_state/active-workstreams.md` row #23 → `built` (substrate v1).

Note that capture-flow follow-up hand-offs are queued separately (W#23.1 Receipts; W#23.2 Assets; W#23.3 Inspections; W#23.4 Signatures; W#23.5 Mileage; W#23.6 Work-Order-Response).

**PR title:** `chore(icm): flip W#23 substrate v1 → built; queue capture-flow follow-ups`

---

## Total decomposition

| Phase | Subject | Hours |
|---|---|---|
| 0 | Per-device install identity stub (iOS + Anchor sides) | 1.5 |
| 1 | `accelerators/anchor-mobile-ios/` SwiftUI scaffold | 4.0 |
| 2 | GRDB.swift + SQLCipher persistence + blob store | 5.0 |
| 3 | Event envelope contract + RFC 8785 canonicalization | 2.5 |
| 4 | Outbound sync engine + Bridge field-event endpoints | 6.0 |
| 5 | Pairing flow + 4 AuditEventType | 4.0 |
| 6 | Home screen + queue-status UX | 3.0 |
| 7 | TestFlight build + smoke test | 2.0 |
| 8 | Ledger flip + capture-flow queue | 0.5 |
| **Total** | | **~28.5 h** |

---

## Halt conditions (substrate-level)

1. **`ITenantKeyProvider` purpose-label restriction** (Phase 0): `field-pairing-token-hmac` purpose unsupported → HALT
2. **W#28 Bridge route family doesn't expose `POST /api/v1/field/*`** (Phase 4): HALT; route ownership negotiation between W#23 + W#28
3. **GRDB.swift 6.x ABI break** (Phase 2): HALT; verify with XO before bumping
4. **`NSFileProtectionComplete` interferes with background URLSession reads** (Phase 2): HALT; decide between weaker protection or accept locked-device limitation
5. **RFC 8785 Swift ↔ .NET canonicalizer divergence** (Phase 3): HALT; XO investigates which side has the bug
6. **Apple Developer Program membership inactive for `dev.sunfish.field`** (Phase 7): HALT; CO-class spending decision (Apple Developer Program: $99/year; DUNS number for business account; LLC formation may gate)
7. **Phase 5 pairing UX requires capabilities not in current Bridge auth model** (e.g., need OIDC + macaroons that don't ship in Phase 2.1): HALT; XO sequences with W#28 / capability-promotion work

---

## Acceptance criteria (cumulative)

- [ ] All Phase 0–8 acceptance criteria pass
- [ ] `accelerators/anchor-mobile-ios/` package exists; SwiftUI shell launches in iOS Simulator
- [ ] GRDB.swift + SQLCipher schema round-trips event captures
- [ ] Event envelope contract matches ADR 0028-A2.1 + RFC 8785 canonicalization byte-for-byte cross-language
- [ ] `URLSessionConfiguration.background` settings match ADR 0028-A2.2
- [ ] Pairing flow: Anchor issues + iPad consumes; 4 AuditEventType emitted
- [ ] Queue-status UX: 80% warning + 100% block per ADR 0028-A2.7
- [ ] TestFlight build downloadable + end-to-end smoke test passes
- [ ] Ledger row #23 → `built` (substrate v1)
- [ ] Capture-flow follow-up hand-offs queued (W#23.1 Receipts at minimum; remaining 5 to be authored as priority surfaces)

---

## Capture-flow follow-up hand-offs (NOT in scope of this hand-off)

After substrate ships, each capture flow becomes its own Stage 06 hand-off (~3-6 phases each):

| Workstream | Capture domain | Native APIs | Effort estimate |
|---|---|---|---|
| W#23.1 | Receipts (photo + OCR + categorize) | DataScannerViewController, Vision, PHPicker | ~6-8h |
| W#23.2 | Assets / Equipment (nameplate OCR + barcode + condition) | DataScannerViewController, Vision | ~6-8h |
| W#23.3 | Inspections (structured form + photos + condition assessments) | Vision; multi-screen form UX | ~10-15h |
| W#23.4 | Signatures (PencilKit canvas + CryptoKit signing + PDF) | PencilKit, CryptoKit, PDFKit | ~8-12h |
| W#23.5 | Mileage (manual entry + odometer + property-link) | None (form-only) | ~3-5h |
| W#23.6 | Work-Order Response (open-from-finding + status updates) | None (form-only); composes W#19 work-order substrate | ~4-6h |

Total capture-flow effort: ~37-54h additional sunfish-PM time after substrate v1 ships.

---

## Decision-class

Session-class per `feedback_decision_discipline` Rule 1 (NOT CO-class — pure phase decomposition; ADRs 0028 + 0048 + 0061 + 0046 + 0054 drive the spec). Phase 7 has one CO-class halt (Apple Developer Program membership / spending). Authority: XO; mechanical authoring against the Stage 06 template + the cited ADRs.

---

## References

- **Spec:** [W#23 intake](../../00_intake/output/property-ios-field-app-intake-2026-04-28.md)
- **CRDT mobile substrate:** [ADR 0028](../../docs/adrs/0028-crdt-engine-selection.md) + A1+A2+A3 amendments
- **MAUI scope carve-out:** [ADR 0048](../../docs/adrs/0048-anchor-multi-backend-maui.md) + A1+A2 amendments
- **Transport substrate:** [ADR 0061](../../docs/adrs/0061-three-tier-peer-transport.md) (Tier 3 only in Phase 2.1)
- **Operator-key projection:** [ADR 0046-A1](../../docs/adrs/0046-a1-historical-keys-projection.md)
- **Signature canonicalization:** [ADR 0054](../../docs/adrs/0054-electronic-signature-capture-and-document-binding.md) Amendment A1
- **Pattern references:** W#18 `VendorMagicLink` precedent (PR #346 + later phases); W#19 Phase 3 stub addendum precedent (PR #274); W#28 Bridge route family (PRs #303 / #306 / #320 / #321)
- **W#23 plan memory:** `~/.claude/projects/-Users-christopherwood-Projects-Sunfish/memory/project_workstream_23_ios_app_queued.md`
