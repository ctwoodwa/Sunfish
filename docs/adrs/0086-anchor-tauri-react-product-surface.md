---
id: 86
title: Anchor Tauri-React Product Surface
status: Accepted
date: 2026-05-12
tier: accelerator
pipeline_variant: sunfish-feature-change

concern:
  - ui
  - persistence
  - distribution
  - accessibility

enables:
  - local-first-property-management-product
  - offline-read-write-via-sqlite-cache
  - ap-class-collaborative-data-via-loro
  - native-desktop-shell-via-tauri

composes:
  - 14   # ui-adapters-react parity adapter
  - 17   # Web Components Lit technical basis (informs the cross-stack vision)
  - 28   # CRDT engine selection (Loro)
  - 31   # Bridge hybrid multi-tenant SaaS
  - 32   # Anchor multi-team workspace switching
  - 48   # Anchor multi-backend MAUI (parallel surface; see decision below)
  - 67   # Headscale substitution for Tailscale BSL

extends: []
supersedes: []
superseded_by: null
deprecated_in_favor_of: null

amendments:
  - A1   # 2026-05-13 Council review: CP/AP boundary, ERPNext license note, max-defer clock, rename-on-α-flip
---

# ADR 0086 — Anchor Tauri-React Product Surface

**Status:** Proposed
**Date:** 2026-05-12

---

## Context

W#60 (ERPNext Composition Pivot, CO UPF plan approved 2026-05-11) reframes Sunfish's product story: ERPNext (GPLv3, self-hosted Docker) becomes the property/accounting engine, and Sunfish becomes the local-first sync + offline + React UI + tenant communications layer over it.

That layer needs a **native desktop shell**. The Surface Pro deployment scenario (CO mobile, visiting properties, sitting with the accountant) makes a browser-tab-pointed-at-localhost a developer fallback, not a product. Phase 1 of W#60 PASS (2026-05-12 — ERPNext self-hosted, full Wave history migrated, in daily use) confirms the product trajectory. Phases 2–5 build out the React UI, local-first sync engine, multi-actor model, and self-hosting story.

The pre-W#60 Zone A choice was `accelerators/anchor/` — .NET 11 MAUI + Blazor WebView. That decision (ADR 0048 — "Anchor multi-backend MAUI") predates the W#60 pivot. ADR 0048 remains valuable for what it covers (kernel-\* and `blocks-crew-comms` showcase + .NET-native demo surface), but W#60's product story is best served by a different stack: **Tauri v2 (Rust shell) + React 19 + Vite + Tailwind v4 + shadcn/ui**, with **Loro CRDT** for AP-class collaborative data and a **SQLite-backed sync engine** for offline read/write.

The Anchor's fate intake (`icm/00_intake/output/2026-05-12_anchor-maui-vs-tauri-fate.md`) evaluated three options for how the MAUI Anchor and the new Tauri Anchor should coexist:

- **α** retire MAUI Anchor entirely;
- **β** keep both as full product surfaces (worst long-term outcome);
- **γ** split by domain: MAUI Anchor for kernel-\*/Crew Comms platform showcase + .NET-native demos; Tauri Anchor for the W#60 property management product.

XO recommends **phased γ → α**: declare γ now, re-evaluate after Phase 3 PASS (Tauri + Loro + sync engine working on Surface Pro ARM Windows). This ADR formalizes the **γ posture** for the Tauri-React surface.

The matching change on the MAUI side is captured in an addendum to ADR 0048 (separately authored) scoping its applicability to the platform-showcase domain.

---

## Decision

**Sunfish ships two Zone A desktop surfaces, scoped by domain:**

1. **`accelerators/anchor/` (.NET 11 MAUI + Blazor WebView)** — platform-showcase / Crew Comms / `_shared/*` demo surface. ADR 0048 governs.
2. **`apps/anchor-tauri/` (Tauri v2 + React 19 + Vite + Tailwind v4 + shadcn/ui + Loro)** — W#60 property management product surface. This ADR governs.

The two are isolated by code, dependencies, and consumer story. Neither stack imports the other. Shared assets — design tokens (`foundation-design-tokens`), domain primitives (`foundation-multitenancy`, `foundation-channels`), and reusable React components (`@sunfish/ui-react`) — are framework-agnostic packages consumed by either Anchor.

**Tauri Anchor's stack:**

| Layer | Choice | Rationale |
|---|---|---|
| Native shell | Tauri v2 | Smaller bundles than Electron; Rust backend gives durable sync engine + keychain; ARM Windows support stabilized 2025-Q4 |
| UI framework | React 19 | Aligned with Sunfish's framework-agnostic claim (ADR 0014 React adapter parity already shipping) |
| Build tooling | Vite 6 | Fastest TS dev loop; first-party Tailwind v4 plugin |
| Styling | Tailwind v4 + shadcn/ui | CSS-first config; component library on-demand |
| Local state | Zustand 5 | Minimal API; works cleanly with Tauri IPC boundaries |
| Server state | TanStack Query v5 | Cache + retry + invalidation primitives the sync engine consumes |
| Forms | React Hook Form + Zod | Zero re-renders on input; runtime + compile-time validation |
| Local persistence | SQLite (`tauri-plugin-sql`) | Standard local-first store; cross-table queries; persists across renderer crashes |
| Conflict-free types | Loro CRDT (MIT) | ADR 0028 already accepted; AP-class collaborative data (notes, photos, maintenance comments) |
| Native auth storage | OS keychain (`tauri-plugin-stronghold`) | Device tokens + Bridge credentials never touch the SPA bundle |
| Update mechanism | Tauri v2 built-in updater | Dev-signed for v0.1; production signing at Phase 5 |
| Telemetry | None third-party | Local-first principle; errors POST to Bridge's `/api/v1/telemetry/error` only |

**Product scope of Tauri Anchor** (per W#60 Phases 2–4):

- Property management screens: Properties, Leases, Rent Collection, Accounting, Crew Comms, Maintenance
- Offline read from local SQLite cache mirroring a subset of ERPNext doctypes
- Offline write queue for AP-class records (notes, photos, comments, free-text fields per paper §2.2). Refused offline (returns user-visible `OfflineRefused` with a `requiresQuorum` reason code) for CP-class records, defined as: GL-affecting financial transactions; resource reservations (lease renewals, scheduled slots); signed attestations (inspections, signatures); audit log writes. The authoritative CP/AP classification per record-type is owned by the sync engine's classification table, deliverable in W#60 Phase 3 Stage 02 architecture.
- Role-aware UI (owner / accountant / cpa / tenant) via `<RoleGate>` from `@sunfish/ui-react`
- Multi-company switcher via `<CompanySwitcher>` from `@sunfish/ui-react`

**Out of scope of Tauri Anchor** (handled by other surfaces):

- Crew Comms demos for kernel-\* / blocks-\* showcase — MAUI Anchor's domain (ADR 0048)
- Tenant-facing portal — separate `apps/tenant-portal/` (W#60 Phase 4)
- iOS field capture — separate `accelerators/anchor-mobile-ios/` (W#23 native SwiftUI; constrained by Apple tooling)

---

## Consequences

### Positive

- **Product velocity.** React + TanStack Query + shadcn/ui is the fastest path for the property-management CRUD UX. Building on the existing `ui-adapters-react` (ADR 0014) parity-adapter substrate.
- **Local-first promise made concrete.** Loro + SQLite + Tauri's Rust backend gives durable offline behavior that MAUI Anchor never had (its Blazor WebView talks to a back-end host; offline-by-default is harder).
- **Smaller bundle, faster startup.** Tauri v2 typically ships ~10 MB shells vs Electron's ~100 MB; the React production bundle for the 6 property-management screens is well under the 50 MB installer budget set in the Phase 3 intake.
- **Cross-platform without Microsoft's MAUI tax.** MAUI ARM Windows support has been historically rough; Tauri v2 ARM Windows stabilized in 2025-Q4 and is now production-grade.
- **F/OSS license clean** across the stack (Tauri Apache-2.0/MIT, Loro MIT, React MIT, Tailwind MIT, shadcn MIT). Sunfish-authored code stays MIT (per Phase 5 intake D4 recommendation). ERPNext (GPLv3, self-hosted) is consumed via its REST API; per FSF guidance, GPLv3-server REST consumption from an MIT client does not trigger copyleft on the client — no ERPNext code is statically or dynamically linked into the Tauri Anchor binary. If ERPNext's license ever migrates to AGPLv3 or a similar network-copyleft variant, this ADR is revisit-triggered.
- **The Inverted Stack book** gets a coherent local-first product narrative: "Tauri + React + Loro over ERPNext."

### Negative

- **Two desktop products to maintain.** Each Zone A feature that crosses both domains ships twice. The Phase 2 addendum + Anchor's fate intake explicitly acknowledge this; the phased γ → α plan mitigates by limiting MAUI Anchor's scope to platform-showcase + Crew Comms.
- **Anchor brand confusion.** "Anchor" now denotes two products. Naming convention: `accelerators/anchor/` = "Anchor MAUI" or "Anchor showcase"; `apps/anchor-tauri/` = "Anchor Tauri" or just "Anchor" (the product, in marketing copy). Final naming TBD with PAO during book alignment.
- **Tauri ecosystem is smaller** than .NET MAUI's. Fewer enterprise integrations available out of the box (e.g., Windows-native printer dialogs, OS-level auth providers). For Sunfish's local-first product, this is rarely on the critical path; document workarounds in the self-hosting guide.

### Risks

- **Tauri v2 ARM Windows regressions** — unverified at production scale on Surface Pro. Phase 3 intake's FAILED trigger #1: "Tauri ARM Windows bundle won't run on Surface Pro after one engineering-week of effort → fall back to PWA (browser install) for Phase 3; revisit native shell at Phase 5."
- **Loro production stability** — ADR 0028's Loro selection was confidence-bounded; if Loro proves unstable at our data volumes, Phase 3 intake's FAILED trigger #2: "fall back to Automerge (smaller community but production-proven at Ink&Switch)."
- **Sync engine complexity** — Phase 3's CP/AP boundary, write queue, and pull strategy are architecturally novel for Sunfish. Risk of design churn during Phase 3 build. Mitigated by the Phase 3 intake's 7 design decisions being resolved in Stage 02 architecture before Stage 06 build.
- **γ posture indefinite extension.** If Phase 3 PASS happens but the α flip (retire MAUI Anchor) keeps getting deferred, Sunfish accumulates the doubled-maintenance cost of β. Mitigation: explicit Phase 3 PASS re-evaluation gate (workstream W#61 or similar).

---

## Alternatives considered

1. **Extend MAUI Anchor** for the W#60 product surface — rejected. MAUI's Blazor WebView doesn't give the local-first / offline / CRDT semantics W#60 needs without substantial new code, and the React adapter (ADR 0014) was already shipping at the time W#60 was pivoted. Continuing on MAUI doubles down on a stack that doesn't match the product's required behavior.

2. **Electron + React** for Tauri Anchor — rejected. Electron's ~100 MB shell budget conflicts with Phase 3's 50 MB installer cap, and the Tauri v2 maturity in 2025-Q4 closed the gap on Electron's developer ergonomics.

3. **Browser-only PWA** — rejected as primary surface. PWAs can't (today, on Surface Pro ARM) hold a durable SQLite + Rust sync engine outside the renderer process. PWA remains the FAILED-trigger fallback if Tauri ARM Windows proves unworkable.

4. **Single Zone A product (retire MAUI Anchor now, option α direct)** — deferred to post-Phase-3. Retiring MAUI Anchor mid-W#59 (Crew Comms MVP shipped 2026-05-06) would invalidate fresh work and create a confusing pivot signal. Phased γ → α defers the retirement decision to when the Tauri stack has proven itself.

5. **Single Zone A product (retire Tauri before starting, keep MAUI)** — would require building Tauri-quality offline + Loro + SQLite semantics inside Blazor WebView. Substantially more effort; conflicts with the React-stack bet of W#60 UPF approval. Not viable.

---

## Implementation notes

### File and project layout

- `apps/anchor-tauri/` — the Tauri app root (γ-period path; see rename note below)
  - `src-tauri/` — Rust crate (Tauri commands, SQLite via `tauri-plugin-sql`, sync engine)
  - `src/` — React app (mirrors `apps/anchor-react/` from W#60 Phase 2)
  - `Cargo.toml`, `tauri.conf.json`, `package.json`
  - **Rename-on-α-flip:** if/when the Phase 3 PASS re-evaluation triggers the α decision (retire MAUI Anchor), `apps/anchor-tauri/` SHOULD be renamed to `apps/anchor/` in the same workstream. The `anchor-tauri/` path is intentionally temporary — it avoids collision with `accelerators/anchor/` (MAUI) during the γ period. The marketing name "Anchor" always refers to this product surface; `anchor-tauri` in the path is an implementation detail, not the name.
- `apps/anchor-react/` — pre-Tauri standalone React app (W#60 Phase 2). Phase 3 absorbs its `src/` tree under `apps/anchor-tauri/src/`; `apps/anchor-react/` is retired (replaced by the Tauri-wrapped version) once Phase 3 ships.
- `packages/ui-react/` (`@sunfish/ui-react`) — extracted reusable components (SyncStateBadge, OfflineIndicator, FreshnessBadge, PropertyCard, RoleGate, CompanySwitcher). Consumed by both `apps/anchor-react/` (Phase 2) and `apps/anchor-tauri/` (Phase 3). MAUI Anchor does NOT consume this package.
- `accelerators/anchor/` (existing MAUI) — unchanged. ADR 0048's scope (per its addendum) is the platform-showcase / Crew Comms surface. Does not migrate to React.

### Boundary with MAUI Anchor

The two Anchors share **packages** (foundation-\*, kernel-\*, blocks-\*, design tokens) but no UI code. Specifically:

- `blocks-crew-comms` (W#45) — framework-agnostic; consumed by both. The MAUI Anchor SunfishChat Blazor component (W#59 Phase 4) and the Tauri Anchor CrewComms React page (W#60 Phase 2 Phase 4) both subscribe to the same `BridgeHub` SignalR endpoint.
- `foundation-channels` — protocol definitions; both consume.
- Design tokens — both consume; ensures visual consistency where it matters.
- Build pipelines remain separate: MAUI uses dotnet/MSBuild; Tauri uses Cargo + Vite.

### Phase 3 PASS re-evaluation criteria (binding)

After W#60 Phase 3 PASS, Sunfish opens a re-evaluation workstream (anticipated W#61 or similar) to score Tauri Anchor against:

1. **Surface Pro ARM Windows** — installs, runs, doesn't crash, doesn't drain battery anomalously.
2. **Offline workflow** — CO can complete one full day's bookkeeping with no network (read all data, queue all writes, replay on reconnect).
3. **Conflict handling** — Loro CRDT resolves at least 3 simulated concurrent-edit scenarios without data loss.
4. **Bundle size + cold-start** — installer ≤50 MB; cold start ≤3s on Surface Pro.
5. **Developer ergonomics** — sunfish-PM reports build/test/deploy ergonomics are at-or-better than MAUI Anchor's.

If all five pass: recommend flipping to **α** (retire MAUI Anchor; consolidate to Tauri Anchor) in a follow-on workstream. If any fail: stay at γ; address the failure; re-evaluate after fix.

6. **Max-defer clock** — the re-evaluation workstream MUST be opened within 30 days of W#60 Phase 3 PASS being declared, OR within 180 days of this ADR's acceptance — whichever comes first. If neither trigger has fired by the 180-day mark, XO MUST raise a `cob-question-*` to CO escalating the indefinite-γ risk.

### Coexistence guardrails

While both Anchors exist (γ posture):

- New Sunfish kernel/foundation/blocks-\* features land **framework-agnostic** (no Blazor-only or React-only contracts).
- ADRs that name a UI surface must specify which Anchor; the default is **Tauri Anchor** for product features, **MAUI Anchor** for platform-showcase features.
- Crew Comms Anchor MVP (W#59) stays on MAUI; the Tauri Anchor's Crew Comms screen (W#60 Phase 2) is a parallel consumer of the same `BridgeHub`.
- No new MAUI-specific features beyond the W#59 Crew Comms scope until α flip is decided.

### Relationship to ADR 0048

ADR 0048 ("Anchor multi-backend MAUI") is **not** superseded by this ADR. Both Anchors are valid; they cover different domains. ADR 0048 will receive an amendment scoping its applicability to the platform-showcase domain (separately authored). The two ADRs together describe the γ posture: ADR 0048 governs the MAUI surface; this ADR governs the Tauri surface.

If the post-Phase-3 re-evaluation flips to α (retire MAUI Anchor), ADR 0048 will become Superseded by a follow-on ADR (not by this one — this ADR is about the Tauri surface, not the unification).

### Naming conventions for marketing / book / docs

- "Anchor" alone — refers to the product (Tauri Anchor) in marketing and the book.
- "Anchor MAUI" or "Anchor showcase" — refers to the platform-showcase MAUI Anchor when distinction is needed.
- "Anchor Tauri" — when the distinction matters in docs but "Anchor" alone would be ambiguous.

PAO sign-off on the naming convention as part of the W#60 Phase 5 book-alignment deliverable.

---

## Amendment A1 — Council review corrections (2026-05-13)

Council review conducted 2026-05-13 (Opus 4.7 / xhigh). Verdict: ACCEPT WITH AMENDMENTS. Four blocking gaps closed inline:

1. **CP/AP boundary tightened** (§Product scope bullet) — explicit record-type list: GL-affecting financial transactions; resource reservations; signed attestations; audit log writes. Classification table deliverable in Phase 3 Stage 02 architecture.

2. **ERPNext GPLv3 API license note added** (§Consequences/Positive) — API-only consumption does not trigger copyleft; revisit-triggered if ERPNext migrates to AGPLv3 or network-copyleft.

3. **Max-defer clock added** (§Phase 3 PASS re-evaluation criteria, item 6) — re-evaluation workstream MUST open within 30 days of Phase 3 PASS OR 180 days of ADR acceptance, whichever first; else XO escalates to CO.

4. **Rename-on-α-flip note added** (§File and project layout) — `apps/anchor-tauri/` is the γ-period path; SHOULD rename to `apps/anchor/` when α flip occurs. Framework name in path is intentionally temporary.

Full council review memo: `icm/07_review/output/council-review-adr-0086-tauri-react-surface-2026-05-13.md`
