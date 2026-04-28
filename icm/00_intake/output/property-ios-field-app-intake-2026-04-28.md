# Intake Note — iOS Field-Capture App

**Status:** `design-in-flight` — Stage 00 intake. **sunfish-PM: do not build against this intake until status flips to `ready-to-build` and a hand-off file appears in `icm/_state/handoffs/`.**
**Status owner:** research session
**Date:** 2026-04-28
**Requestor:** Christopher Wood (BDFL)
**Spec source:** Multi-turn architectural conversation 2026-04-28 (turns 1–7 — iOS access for property field operations).
**Pipeline variant:** `sunfish-feature-change` (with new accelerator app; new mobile platform target)
**Parent:** [`property-ops-INDEX-intake-2026-04-28.md`](./property-ops-INDEX-intake-2026-04-28.md)
**Position in cluster:** Cross-cutting #3 — the user-facing surface that consumes most cluster modules.

---

## Problem Statement

The BDFL needs a field application that runs on iPad and iPhone to operate his property business on-site:

- Capture receipts when paying vendors or buying supplies
- Inventory assets per property (water heaters, HVAC, appliances) including serial-number capture
- Conduct annual inspections + move-in/move-out walkthroughs with structured condition assessments
- Capture signatures on leases and maintenance sign-offs while on location with leaseholders or vendors
- Log mileage manually for trips between properties (with optional GPS auto-tracking later)
- Open work orders from on-site discoveries

The defining characteristic is **field-grade reliability**: storage closets, basements, and properties without cell coverage are common workplaces. The app must be **offline-first** and feel instant. A thin client doesn't suffice; capture has to happen locally and sync opportunistically.

This intake captures the iOS app architecture, framework choices, offline-first capture pipeline, transport choices, and the per-domain integration surface.

## Scope Statement

### In scope (this intake)

1. **iOS app shell.** SwiftUI native (not MAUI). Targets iOS 16+ for `DataScannerViewController` baseline. iPad + iPhone universal binary; iPad-optimized layouts for inspection/signing flows; iPhone-optimized for receipt/mileage quick capture.
2. **Local persistence layer.**
   - SQLite via GRDB.swift
   - Optional SQLCipher for at-rest encryption (matches paper §15 + Foundation.Recovery)
   - Content-addressed blob store in app sandbox for photos/PDFs/signatures
3. **Outbound sync engine.**
   - Background `URLSession` with `URLSessionConfiguration.background` so uploads survive app suspension
   - Append-only event queue (per OQ2 mobile-CRDT decision: capture-only LWW, no rich CRDT merge in Phase 2.1)
   - Retry semantics with exponential backoff
   - Resumable blob uploads (matches Bridge blob-ingest API per OQ3)
4. **Capture flows** (one per cluster domain that has field-capture needs):
   - Receipts: photo capture + OCR + categorization + asset-link picker
   - Assets: nameplate OCR via DataScannerViewController + serial/barcode/photo capture + asset-class picker
   - Inspections: structured form + photos + per-asset condition assessments + sign-off (move-in/out only)
   - Signatures: PencilKit canvas + CryptoKit signing + PDF generation (per Signatures intake)
   - Mileage: manual entry form (vehicle + start/end odometer + purpose + linked-property-visit picker)
   - Work orders: open-from-finding flow during inspection; basic detail view
5. **Auth + identity.** Per-device pairing token issued by Anchor; stored in iOS Keychain (with optional cloud Keychain sync). Token-based auth to Bridge; token-based auth to Anchor over Tailscale (per OQ4 transport).
6. **Transport (per discussion turns 1–3):** Bridge as primary transport (always reachable from any internet); Tailscale as optimization when on home Wi-Fi or tailnet (Phase 2.3 enhancement, not Phase 2.1).
7. **Multi-actor support.** Owner + spouse + bookkeeper + contractor each can install the app; capability-driven UX trims features per role (matches ADR 0032 multi-team Anchor pattern).
8. **App distribution.** TestFlight for BDFL + spouse + close team in Phase 2.1; App Store distribution Phase 2.3 (or self-distribution via Apple Business Manager if compliance-sensitive).
9. **`accelerators/anchor-mobile-ios/`** OR **`apps/field/`** path under repo (decision in Stage 02 per OQ-I1 below).
10. **ADR 0028 amendment** (mobile reality check): YDotNet has no Swift port; Phase 2.1 ships append-only events on iOS; full CRDT-on-mobile is Phase 3.

### Out of scope (this intake — handled elsewhere)

- Domain entity definitions (Property, Asset, Inspection, etc.) — sibling intakes
- Bridge blob-ingest API server-side — handled within Public Listings intake's Bridge surface scope, OR as an additional Bridge intake; cross-cutting OQ3 in INDEX
- Outbound messaging from iOS (the iOS app *consumes* the messaging substrate; doesn't reimplement)
- Apple Pencil-pro features (timing, tilt) beyond standard capture — captured in Signatures intake
- Live Activities / widgets / Apple Watch companion — Phase 2.3+
- Push notifications — Phase 2.2 (deferred until Bridge messaging substrate has push channel)

### Explicitly NOT in scope

- MAUI iOS — explicitly rejected. SwiftUI native chosen for camera, PencilKit, background URLSession, Vision/DataScannerViewController, and PDFKit. Reusing the Blazor adapter for *field* UI is a false economy.
- React Native — same rejection rationale; bridges add latency for camera-heavy flows
- Android version — Phase 4+; not in Phase 2 scope (BDFL is iOS-only)
- Web-based PWA fallback — not a field-grade solution; rejected
- Full Sunfish kernel port to Swift — Phase 3 if and when offline-on-mobile-with-full-merge becomes a real requirement

---

## Affected Sunfish Areas

| Layer | Item | Change |
|---|---|---|
| Accelerators | `accelerators/anchor-mobile-ios/` (proposed path) | New accelerator |
| Foundation | None directly — iOS app consumes Bridge HTTP API |
| Bridge | Blob-ingest API + structured-event ingest API | Already required by other cluster intakes; iOS is a primary consumer |
| Bridge | Auth: device-pairing-token issuance flow | New endpoint pair (Anchor-side issue + iOS-side redeem) |
| iOS | `accelerators/anchor-mobile-ios/Package.swift` (Swift Package Manager) | New project tree |
| iOS | Capture pipeline modules: GRDB schema, blob store, sync queue, capture views | All-new |
| ADRs | ADR 0028 amendment (mobile reality check) | Append-only events on iOS in Phase 2.1; rich CRDT in Phase 3 |
| ADRs | ADR 0048 (Anchor multi-backend MAUI) | Cross-reference: iOS is *not* a MAUI target; new ADR or 0048 amendment to document the explicit non-MAUI iOS path |

---

## Acceptance Criteria

- [ ] ADR 0028 amendment accepted (mobile-CRDT posture)
- [ ] ADR 0048 amendment OR new ADR documenting iOS-as-SwiftUI (not MAUI) accepted
- [ ] `accelerators/anchor-mobile-ios/` skeleton with SwiftUI + GRDB + sandboxed blob store
- [ ] Device-pairing flow: Anchor issues a token → iOS redeems → Keychain stores → calls authenticated to Bridge succeed
- [ ] Background URLSession upload demo: capture a photo → app suspended → photo uploads in background → reappears in Bridge
- [ ] One end-to-end capture flow shipping minimum viable in Phase 2.1a (recommend: Receipts as the canonical exemplar)
- [ ] Per-domain capture flows added incrementally (Assets, Inspections, Mileage, Signatures, Work Orders) per cluster phase mapping
- [ ] Multi-actor capability trimming demonstrated (owner sees all flows; bookkeeper sees only receipts; contractor sees only assigned work orders)
- [ ] TestFlight distribution for BDFL + spouse in Phase 2.1a
- [ ] apps/docs entry covering iOS app architecture + flows
- [ ] Per-flow test plan (manual + automated where feasible — XCTest for view-model logic, XCUITest for critical capture flows)

---

## Open Questions

| ID | Question | Resolution path |
|---|---|---|
| OQ-I1 | Repo path: `accelerators/anchor-mobile-ios/` (treats as third Anchor variant per ADR 0048) vs `apps/field/` (treats as separate user-facing app, not an accelerator). | Stage 02. Recommend `accelerators/anchor-mobile-ios/` since it's the BDFL's daily field tool and aligns with ADR 0048's accelerator framing. |
| OQ-I2 | Code-sharing strategy: zero shared code with Anchor MAUI (clean SwiftUI), shared API client via OpenAPI codegen, shared business logic via .NET → Swift via Mono/Xamarin (rejected per scope), or shared via published .NET API client + thin Swift wrapper. | Stage 02. Recommend OpenAPI codegen for API client; no business-logic sharing. |
| OQ-I3 | iOS minimum version: iOS 16 (DataScannerViewController) vs iOS 17 (improvements) vs iOS 18 (newest). | Stage 02. Recommend iOS 16 baseline; iPad/iPhone going back ~3 generations covered. |
| OQ-I4 | App ID / bundle ID: `dev.sunfish.anchor.field` per Sunfish naming, or BDFL-business-specific `com.bdfl-llc.field`? | Stage 02. Recommend Sunfish-namespaced; BDFL business uses Sunfish app the way every other tenant will. |
| OQ-I5 | Apple developer account: BDFL personal vs Sunfish org account vs BDFL property-business LLC account? | Stage 02. Recommend Sunfish org account; BDFL is the test customer, not the distributor. |
| OQ-I6 | Offline data retention policy: how long does iOS keep blob copies after server confirms? Storage cap on device? | Stage 02. Recommend 30 days post-confirm + 5GB cap; user-configurable. |
| OQ-I7 | App Store privacy nutrition labels — what data is collected, what's tracked, what's shared? | Stage 02. Comprehensive review in Stage 03. |
| OQ-I8 | Crash reporting: Sentry, Firebase Crashlytics, or self-hosted? Provider-neutrality concerns. | Stage 02. Recommend Sentry self-hosted (org-controlled) or skip in Phase 2.1; revisit. |
| OQ-I9 | Push notifications channel: APNs direct from Bridge, or via a PaaS (OneSignal, etc.)? | Phase 2.2 — defer. |
| OQ-I10 | Maps integration for showing-trip planning + property locator: MapKit (Apple, native, free) vs MapBox (cross-platform consistency) vs Google Maps. | Stage 02. Recommend MapKit for MVP; MapBox if showing trip-planning UX requires it. |

---

## Dependencies

**Blocked by:**
- Bridge blob-ingest API spec (cluster OQ3) — must be specified before iOS sync pipeline is implementable
- ADR 0028 amendment (mobile reality check)
- ADR 0048 amendment or new ADR (SwiftUI vs MAUI iOS decision)
- Per-domain entity definitions in sibling intakes (the iOS app is the *consumer*; entities must be specified)

**Blocks:**
- All Phase 2.1 field-capture milestones (a, b, c, f) — iOS is the surface where they ship

**Cross-cutting open questions consumed:** OQ2 (CRDT-on-mobile), OQ3 (blob ingest API), OQ4 (MAUI vs SwiftUI lock-in) from INDEX.

---

## Pipeline Variant Choice

`sunfish-feature-change` — large feature; first non-MAUI accelerator. Stage 02, 03, 04 (scaffolding for the SwiftUI+GRDB skeleton) all mandatory.

---

## Cross-references

- Parent: [`property-ops-INDEX-intake-2026-04-28.md`](./property-ops-INDEX-intake-2026-04-28.md)
- All sibling intakes (the iOS app consumes them all)
- ADR 0028 (CRDT engine) — amendment driven by this intake
- ADR 0048 (Anchor multi-backend MAUI) — amendment or related ADR
- Workstream #15 (foundation-recovery split) — for at-rest encryption + device-key issuance

---

## Sign-off

Research session — 2026-04-28
