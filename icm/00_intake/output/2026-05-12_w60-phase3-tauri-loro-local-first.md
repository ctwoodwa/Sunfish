# Intake — W#60 Phase 3: Tauri v2 + Loro CRDT + Local-First Sync

**Date:** 2026-05-12
**Author:** XO
**Workstream:** W#60 Phase 3 of 5
**Pipeline variant:** `sunfish-feature-change`
**Predecessor:** W#60 Phase 2 (React UI + ASP.NET proxy — `ready-to-build`)
**Estimate:** ~3 dev-weeks per UPF plan

---

## Problem statement

After Phase 2, CO can use the Sunfish React UI from any browser pointed at the local ASP.NET proxy. But that's still **online-only** — if CO's home internet is down, ERPNext is down, or CO is on the road with the Surface Pro and no LAN connection, the system is unusable. Phase 3 delivers the **local-first** promise of the W#60 pivot:

- **Offline read** from a local SQLite cache mirroring ERPNext data
- **Offline write** via a durable queue that replays to ERPNext on reconnect
- **AP-class collaborative data** (notes, photos, maintenance comments) via Loro CRDT
- **Native desktop wrapper** via Tauri v2 — single executable, no Docker, no browser tab

---

## Why now

1. **Phase 1 PASS unlocked Phase 2; Phase 2 unlocks Phase 3.** Phase 3 needs the React app (`apps/anchor-react/`) to wrap, and the ERPNext API contract (Bridge proxy endpoints) to mirror locally.
2. **Surface Pro deployment scenario is the primary CO use case.** CO is mobile (visiting properties, sitting with the accountant). Browser-tab-pointed-at-localhost is a dev fallback, not a product.
3. **Tauri v2 stabilized in 2025-Q4.** ARM Windows support is now production-grade (was a UPF FAILED trigger; needs re-verification on actual Surface Pro hardware).
4. **The Inverted Stack paper §13–14** (event-sourced ledger + AP-class projections) requires a local cache to make sense; Phase 3 is the first concrete instance.

---

## Scope

### In scope

1. **Tauri v2 shell** wrapping `apps/anchor-react/` static bundle. Single executable for Windows ARM (Surface Pro), Windows x64, and macOS (CO dev machine).
2. **Local SQLite cache** in Tauri's app-data directory. Schema mirrors the subset of ERPNext doctypes used by the React UI (Properties, Leases, Payments, Maintenance, Customers, plus the Sunfish-custom doctypes if W#60 Phase 2 G1 picks option c).
3. **Sync engine** running in Tauri's Rust process:
   - **Pull:** poll ERPNext (or webhook on LAN) for changes, write to local SQLite
   - **Push (write queue):** when React posts via Tauri's IPC, write to queue (durable), attempt immediate POST to ERPNext via Bridge proxy; on failure, retain in queue and retry on connectivity
   - **Conflict handling:** for CP-class records (financial transactions), refuse local edits when offline — show "ERPNext unreachable; queue is empty; cannot record payment offline" with user explanation
4. **Loro CRDT** for AP-class data:
   - Maintenance ticket comments / activity log
   - Property notes
   - Photo metadata (geotag, caption — actual blobs handled separately via existing `federation-blob-replication`)
5. **Sync state UI:** the `SyncStateBadge` component from `@sunfish/ui-react` (Phase 2) gets wired to actual sync state: green (in sync), amber (queued writes), offline (no connectivity), conflict (rare but possible).
6. **First-run setup:** Tauri app prompts for Bridge URL + credentials; persists in OS keychain (not in SQLite).

### Out of scope (later phases)

- Peer-to-peer sync between two Anchor desktops (Phase 4 — accountant peer node)
- Tenant magic-link portal (Phase 4 — different deployment surface)
- Background sync when app is closed (Phase 5 or post-MVP)
- Mobile (iOS/Android) — separate workstream (W#23 iOS already in design-in-flight)

---

## Key design decisions to resolve in Stage 02 (architecture)

| # | Decision | Options |
|---|---|---|
| D1 | **What runs in Rust vs webview?** | (a) Rust = sync engine + SQLite + Loro; webview = React UI only (recommended). (b) Rust = thin shell; sync logic in TS via Tauri JS bindings to Loro. (a) is faster, durable across renderer crashes; (b) is easier to debug. |
| D2 | **Local SQLite schema strategy** | (a) Mirror ERPNext doctypes 1:1 (JSON blob columns + indexed key fields). (b) Denormalized read-model tables per screen. (a) is simpler, slower for joins; (b) is faster, requires migration discipline. |
| D3 | **CP/AP class boundary** | Where does Loro CRDT take over from synchronous ERPNext writes? Hard rule: anything that touches the GL = CP (must reach ERPNext before user sees "saved"). Anything else = AP (Loro). |
| D4 | **Pull strategy** | (a) Poll every N seconds. (b) ERPNext webhook → Bridge → push to Anchor over WebSocket. (c) Hybrid: poll on app foreground, webhook when LAN. (c) is correct but most work. |
| D5 | **Auth on the Tauri side** | Tauri app needs Bridge credentials. Reuse Bridge's session cookie? OAuth client_credentials? Device-bound API token? Recommend device token issued on first-run setup, stored in OS keychain. |
| D6 | **Bundle size budget** | Tauri v2 bundles ~10 MB Rust + webview runtime + React bundle. Hard cap: 50 MB for the installer. Affects Loro version choice (loro-internal is small; loro-crdt npm wrapper adds size). |
| D7 | **Update mechanism** | Tauri v2 has built-in updater. Sign with developer cert? Use GitHub Releases? Defer to Phase 5? Recommend defer to Phase 5 — Phase 3 ships dev-signed installers. |

---

## Open research questions

1. **ARM Windows Tauri v2 stability** — last UPF check (2025-Q3) flagged as risky. Re-test on actual Surface Pro before committing.
2. **Loro vs Automerge vs Y.js** — ADR 0028 (CRDT engine selection) already evaluated. Reconfirm Loro is still the right call given size/performance/license. License recheck: Loro is MIT, no drift.
3. **SQLite vs IndexedDB vs WatermelonDB** — Tauri can expose any local store. SQLite via `tauri-plugin-sql` is the natural choice for a desktop app that may need cross-table queries. Recheck for ARM Windows.
4. **Frappe REST vs Frappe API generator** — does ERPNext have a stable changefeed (webhooks per doctype) we can subscribe to? Or do we poll modified timestamps?
5. **Time skew handling** — Loro and the write queue both timestamp events. Surface Pro clock vs ERPNext server clock skew up to ±5s is normal. Pick a single source of truth (ERPNext server time) for ordering.

---

## Acceptance criteria (Phase 3 PASS)

CO can:

1. Install the Tauri app on Surface Pro (ARM Windows 11). Single .msi. No Docker.
2. Open the app while the home internet is up — sees all properties, leases, recent payments. UI is responsive (<100ms screen transitions, sourced from local cache).
3. Unplug the network. Continue to browse all data. Type a property note + take a photo — saves locally with a queued-writes badge.
4. Record a rent payment offline → app refuses with explanation (CP-class).
5. Reconnect. Within 10 seconds, queued AP-class writes (note + photo) replay to ERPNext via Bridge. SyncStateBadge transitions amber → green.
6. Pull a new ERPNext-side change (e.g., accountant enters a payment in the web UI) — appears in Anchor within 30 seconds.

**FAILED triggers:**
- Tauri ARM Windows bundle won't run on Surface Pro after one engineering-week of effort → fall back to PWA (browser install) for Phase 3; revisit native shell at Phase 5
- Loro proves unstable at our data volumes → fall back to Automerge (smaller community but production-proven at Ink&Switch)
- Sync engine causes UI jank (frame drops during pull) → move all sync to dedicated Tauri command; UI never blocks on sync

---

## Stage routing

| Stage | Action |
|---|---|
| 00 Intake (this doc) | ✅ authored |
| 01 Discovery | needed — verify Tauri v2 ARM Windows on Surface Pro, Loro npm package size, ERPNext webhook API, Frappe API rate limits |
| 02 Architecture | needed — resolve D1–D7 above, produce ADR (likely `0086-anchor-tauri-local-first-shell.md`) |
| 03 Package design | needed — `apps/anchor-tauri/` (Rust crate + Tauri config) + `packages/sync-engine/` (TS/Rust shared logic?) |
| 04 Scaffolding | likely re-use Tauri v2 template + the existing `apps/anchor-react/` |
| 05 Implementation plan | needed — sequence of 4–6 PRs probably |
| 06 Build | sunfish-PM |
| 07 Review | council required (security-sensitive: keychain, sync engine, conflict handling) |
| 08 Release | dev-signed installer for CO; production sign later |

---

## Predecessors and successors

**Predecessors:**
- W#60 Phase 2 (React UI built + `@sunfish/ui-react` shipping ESM) — **must be at "built" before Phase 3 starts**
- ADR 0028 (CRDT engine selection — Loro) — already Accepted
- ADR 0032 (Anchor workspace switching) — may need addendum for Tauri vs MAUI Anchor coexistence (see XO recommendation #5 in the W#60 status update: Anchor's fate ADR)

**Successors:**
- W#60 Phase 4 (peer node + tenant portal) — needs Phase 3's sync engine
- Mobile workstreams (W#23 iOS app) — may share sync-engine semantics; opportunity for shared design

---

## Open item flagged for CO

**Anchor's fate** — the existing `accelerators/anchor/` is .NET MAUI + Blazor. Phase 3 introduces a parallel Tauri + React shell as the new local-first surface. Two ADRs become possible:
- **(α)** Retire MAUI Anchor; Tauri Anchor is the only Zone A. Saves maintenance; loses the .NET-native demo + Blazor parity proof.
- **(β)** Keep both. MAUI Anchor as Zone A.1 (.NET shop default); Tauri Anchor as Zone A.2 (React/TypeScript shop default). Doubles maintenance.
- **(γ)** MAUI Anchor stays for Crew Comms / kernel-* demos; Tauri Anchor for property management. Diverges by domain.

XO recommends **(α)** if Phase 3 PASS demonstrates clear superiority; **(γ)** as interim during Phase 3 evaluation. CO decision needed by end of Phase 3.
