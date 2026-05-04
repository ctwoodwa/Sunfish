# Sunfish Field-Capture iOS App (W#23)

SwiftUI native iOS field-capture app substrate per [ADR 0028 + A1+A2+A3](../../docs/adrs/0028-crdt-engine-selection.md) (mobile reality check + per-event-type LWW table) and [ADR 0048 + A1+A2](../../docs/adrs/0048-multi-backend-maui.md) (MAUI mobile-scope clarification carving out Field-Capture as SwiftUI sibling).

**Bundle ID:** `dev.sunfish.field` (sibling to Anchor's `dev.sunfish.anchor` per ADR 0048-A1.7.OQ-A1.2)
**iOS baseline:** 16.0
**Phase 0:** per-device install identity + pairing-token surface (built — PR #478)
**Phase 1:** SwiftUI scaffold + Xcode project + iOS 16 baseline (this PR)

## Layout

```
accelerators/anchor-mobile-ios/
├── Package.swift                       # SPM manifest (Swift 5.9 toolchain)
├── Package.resolved                    # Pins: swift-crypto 3.x, GRDB 6.x
├── Project.xcodeproj/                  # Xcode project (open with `xed .`)
├── SunfishField/                       # P1 — SwiftUI app shell
│   ├── SunfishFieldApp.swift
│   ├── ContentView.swift
│   └── Info.plist                      # Bundle ID: dev.sunfish.field
├── Sources/
│   └── Identity/                       # P0 — install identity + DeviceId
│       ├── InstallIdentity.swift
│       ├── InstallIdentity+Keychain.swift
│       └── DeviceId.swift
└── Tests/
    └── SunfishFieldIdentityTests/
        ├── DeviceIdTests.swift
        └── InstallIdentityTests.swift
```

## Phase 0 — install identity + pairing token

The iOS app generates a per-install Ed25519 root keypair on first launch, persists the private key in the iOS Keychain (`kSecAttrAccessibleAfterFirstUnlock` per ADR 0028-A2.8), and derives a stable `DeviceId` from the public key (first 16 hex chars of `SHA-256(publicKeyBytes)`).

The Anchor app issues a short-lived pairing token (HMAC-bound to `(PairingTokenId, DeviceId, IssuedAt)` per W#18 vendor-magic-link precedent); the iOS app consumes the token over Bridge HTTPS during pairing. Token TTL defaults to 10 minutes (operator pairs the device live).

## Phase 1 — SwiftUI scaffold + Xcode project (this PR)

Two coexisting build systems target the same source tree:

1. **`Project.xcodeproj`** — primary developer experience (Xcode + iOS Simulator). Open with `xed accelerators/anchor-mobile-ios` from the repo root. iOS 16 deployment target; Swift 6.0 language version; bundle ID `dev.sunfish.field`.
2. **`Package.swift`** — CI-friendly SPM wrapper. The `SunfishField` library target compiles the SwiftUI sources on a macOS host without requiring `xcodebuild`. Pins `swift-crypto@3.x` (P0) and `GRDB.swift@6.x` (P2) in `Package.resolved`.

`SunfishField/Info.plist` declares `BGTaskSchedulerPermittedIdentifiers = [dev.sunfish.field.upload]` for the P4 background upload task per ADR 0028-A2.2, and ships placeholder `NSCameraUsageDescription` / `NSPhotoLibraryUsageDescription` strings that capture-flow phases will refine.

## Building

```bash
# macOS host with Swift 5.9+ toolchain (Phase 0 + Phase 1 source compile only):
swift build --package-path accelerators/anchor-mobile-ios
swift test --package-path accelerators/anchor-mobile-ios

# Full iOS app build (requires Xcode + iOS 16+ simulator):
xcodebuild -project accelerators/anchor-mobile-ios/Project.xcodeproj \
           -scheme SunfishField \
           -destination 'generic/platform=iOS Simulator' \
           build
```

The SPM library targets compile + test on macOS hosts (Phase 0 Keychain calls are guarded by `#if canImport(Security)` so the cryptographic path runs cross-platform). Building the actual `.app` bundle for iOS Simulator or device requires the Xcode project.

## Phases ahead

- **P1** — *this PR* — SwiftUI scaffold + Bundle ID + iOS 16 baseline + GRDB pinned.
- **P2** — Local persistence: GRDB + SQLCipher + content-addressed blob store + queue table.
- **P3** — Event envelope contract per ADR 0028-A2.1 + RFC 8785 canonicalization (cross-language test against the .NET side).
- **P4** — Outbound sync engine + Bridge `POST /api/v1/field/event` route (W#28 boundary; gated halt-condition).
- **P5** — Pairing flow + 4 new `AuditEventType` constants.
- **P6** — Queue-status home + 80% warning / 100% block per ADR 0028-A2.7.
- **P7** — TestFlight build + smoke test (CO-class halt: Apple Developer Program $99/yr).
- **P8** — Ledger flip → built (substrate-only; capture-flow follow-up hand-offs queued separately).

## Cross-language parity

The iOS Field-Capture app maintains canonical-JSON wire-format parity with the .NET side via `swift-crypto` for Ed25519 / HMAC-SHA256 and a hand-written RFC 8785 canonicalizer (Phase 3) matching the .NET `Sunfish.Foundation.Canonicalization.JsonCanonical`. Cross-language round-trip tests live under both `Tests/` (Swift) and `accelerators/anchor/tests/` (.NET) — Phase 3 lands the first such test.
