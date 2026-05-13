# Intake — Anchor's fate: MAUI vs Tauri after W#60

**Date:** 2026-05-12
**Author:** XO
**Type:** ADR-precursor intake (will produce ADR 0086 or sequel)
**Trigger:** W#60 Phase 3 (Tauri shell + Loro CRDT) introduces a parallel Zone A desktop surface; the existing `accelerators/anchor/` (MAUI + Blazor) is the original Zone A choice. Both cannot remain the canonical local-first desktop without doubling maintenance.

---

## Context

`accelerators/anchor/` (~6,700 LOC, .NET 11 MAUI + Blazor WebView) was Sunfish's first stab at Zone A — the local-first desktop wrapper paper §20.7 calls for. It depends on:

- `foundation-localfirst`, `foundation-recovery`
- `kernel-crdt`, `kernel-runtime`, `kernel-security`, `kernel-sync`
- `blocks-crew-comms` (W#45 work)
- `ui-adapters-blazor` (the ADR 0014 parity adapter for Blazor)

It targets Windows (`10.0.19041+`) and Mac Catalyst (`17+`); iOS/Android are commented out but on the roadmap. The W#23 iOS field-capture workstream is a separate native SwiftUI app, not a MAUI build.

ADRs that depend on Anchor's existence: 0028 (CRDT engine selection), 0031 (Bridge hybrid), 0032 (multi-team workspace switching), 0044 (Anchor Windows-only Phase 1), 0048 (Anchor multi-backend MAUI), 0053 (work order domain model), 0054 (electronic signature), 0055 (dynamic forms), 0061 (three-tier peer transport).

W#60 chose React 19 + Vite + Tailwind + shadcn/ui + Tauri v2 as the "correct tech stack" for the property management product. That's the technical bet of the W#60 pivot.

Anchor and the Tauri-React stack now occupy the same architectural slot — Zone A, local-first desktop, single executable, embedded webview.

---

## Why this can't be deferred

1. **W#60 Phase 3 prerequisite.** Phase 3 builds a Tauri shell. Whether it replaces, parallels, or coexists with Anchor determines Phase 3's PR shape, file layout (`apps/anchor-tauri/` vs `accelerators/anchor-tauri/`), and the maintenance burden going forward.
2. **Anchor MVP work in flight.** W#59 (Crew Comms Anchor MVP) just shipped 5 PRs into Anchor. Phase 2/3 of W#60 will divert UI development from Anchor's Blazor surface to React. Without a clear ADR, both efforts will accumulate divergent UI choices.
3. **Story to investors / book readers.** *The Inverted Stack* book references Anchor specifically. If Anchor is being retired, the book needs to know before chapters lock.
4. **iOS / Android.** Anchor's commented-out mobile targets vs the standalone W#23 iOS SwiftUI app — same problem at a different abstraction. Resolving Anchor's fate constrains the mobile story too.

---

## The three options (from W#60 Phase 3 intake D-flag)

### α — Retire MAUI Anchor

`accelerators/anchor/` is removed. Tauri-React Anchor is the only Zone A. W#59 Crew Comms work + the W#23 iOS app remain (different stacks for different surfaces).

**Pros:**
- Single Zone A maintenance surface
- Frees ~6,700 LOC of .NET 11 MAUI from the maintenance budget
- Eliminates the Blazor WebView learning-curve tax for new contributors
- Cleaner book narrative: "local-first desktop = Tauri + React"
- ADR 0048 (Anchor multi-backend MAUI) becomes superseded — closes a hard-to-implement design space

**Cons:**
- ~6 ADRs become "historical": 0044, 0048, 0053, 0054, 0055 (some have generic content that survives; some are MAUI-specific)
- W#59's Crew Comms Anchor MVP becomes a temporary milestone — work is preserved (`blocks-crew-comms` is framework-agnostic) but its Anchor demo surface gets replaced
- Loses the ".NET + MAUI is a viable Zone A path" proof-of-concept demonstration
- `ui-adapters-blazor` becomes used only by `apps/kitchen-sink` (the demo); risks bit-rot
- Investors / book readers who anchored on Anchor get a confusing pivot signal (this is the second time — Sunfish previously pivoted from web-only to Anchor)

### β — Keep both

Two parallel Zone A products. MAUI Anchor for .NET-native shops + Crew Comms demos; Tauri-React for property-management and the W#60 product flow.

**Pros:**
- No throwaway work
- Demonstrates Sunfish's framework-agnostic claim (ADR 0017 web components Lit basis + ADR 0014 adapter parity) by *showing* parity across two complete desktop products
- Each adapter stays exercised (Blazor + React adapters both have a live consumer)
- Lower switching cost for current users / contributors
- Book can pitch both narratives

**Cons:**
- Doubled maintenance: every Zone A feature ships twice
- Every UI ADR debates "should this work in both?" (paper §22 — parity tests)
- Bus factor: small team, two stacks
- Two install/distribute pipelines (MSIX/PKG vs Tauri .msi/.dmg)
- Two crash-reporting + telemetry pipelines
- Diluted focus when triaging UX bugs

### γ — Split by domain

MAUI Anchor for kernel-* / Crew Comms / `_shared/*` demos (the platform showcase). Tauri-React for property management (the product). Each owns a different value proposition.

**Pros:**
- Domain-aligned: showcase vs product
- Anchor's Crew Comms work isn't wasted
- React stack isn't burdened with also being the Sunfish-platform demo
- Clean ADR 0048 outcome: MAUI multi-backend is for showcase demos, not products
- Tauri's smaller ecosystem (vs MAUI's .NET tooling) doesn't have to also model Sunfish's full platform demo surface

**Cons:**
- "Which Anchor do I install?" branding split
- Two desktop products published from one repo (paper §20.7 was singular)
- Forks the Crew Comms story: Tauri-React doesn't initially have Crew Comms (it's in MAUI Anchor); duplicating it in Tauri = MORE work than option β
- Slightly muddier book narrative

---

## Decision criteria (XO scoring)

| Criterion | Weight | α retire | β keep both | γ split by domain |
|---|---|---|---|---|
| **Maintenance cost** (one team, small) | high | ✅ best | ❌ worst | 🟡 middle |
| **Time to W#60 product MVP** | high | ✅ no diversion | ❌ both stacks tax W#60 | 🟡 some |
| **Book / narrative clarity** | medium | ✅ clean pivot | 🟡 dual story works but complex | ❌ split confusing |
| **Demonstrates framework-agnosticism** | medium | ❌ loses Blazor proof | ✅ proves it via doubling | 🟡 partial |
| **W#59 Crew Comms work preserved (UI surface)** | medium | ❌ replaced eventually | ✅ kept | ✅ kept |
| **`ui-adapters-blazor` package stays alive** | low | ❌ orphaned | ✅ used | ✅ used (in MAUI) |
| **Investor / contributor signal stability** | medium | ❌ second pivot in 18 months | ✅ no pivot | 🟡 ambiguous |
| **Reduces ADR debt** | low | ✅ closes 0048; supersedes 5 | ❌ adds debate per-ADR | 🟡 redefines 0048 |
| **Surface Pro ARM Windows risk hedge** | medium | ❌ all eggs in Tauri ARM basket | ✅ MAUI ARM is proven | 🟡 partial hedge |

---

## XO recommendation

**Recommend a phased decision** rather than a single all-at-once choice:

1. **Now (pre-Phase-3):** declare **γ split by domain** as the operating posture — MAUI Anchor remains the platform-showcase / Crew Comms surface; Tauri-React Anchor is the W#60 product surface. This preserves W#59's work, hedges against Surface Pro ARM Tauri risk, and gives the React stack room to prove itself.
2. **At Phase 3 PASS:** re-evaluate. If Tauri-React on Surface Pro ARM works flawlessly AND the React product UI hits CO's daily-use bar, **flip to α retire** in a follow-on workstream. If Tauri-React has rough edges or the demo value of Crew Comms in MAUI remains high, **stay at γ** indefinitely.
3. **Never recommend β** (keep both fully) — it's the worst long-term outcome despite being the least disruptive short-term.

This phased posture also matches the FAILED-trigger pattern used elsewhere: ship the new stack with a clear retreat path, evaluate at a known gate.

---

## What this intake produces (next-stage routing)

If CO accepts the phased approach:

- **Now:** an addendum to **ADR 0048** ("Anchor multi-backend MAUI") explicitly scoping it to *platform-showcase / Crew Comms demo surface*, not the W#60 property product. ~30-line addendum, low effort.
- **Now:** an ADR draft **0086 — "Anchor Tauri-React product surface"** (parallel slot to ADR 0048) defining the Tauri-React stack as the W#60 product surface, naming naming conventions (`apps/anchor-tauri/` or similar), and explicitly listing the criteria for Phase 3 PASS that would trigger an α flip later.
- **At Phase 3 PASS:** stand up a re-evaluation workstream W#61 (or whatever's next) to apply the criteria and recommend final disposition.

If CO instead picks α directly (retire MAUI Anchor now), the chain is:
- Supersede ADR 0048
- Supersede or amend ADRs 0044, 0053, 0054, 0055 to be stack-agnostic or Tauri-named
- Schedule retirement of `accelerators/anchor/` after W#59 Crew Comms MVP ships
- Move `blocks-crew-comms` UI consumer to Tauri-React in Phase 4

If CO picks β (keep both forever), this intake becomes the ADR record that we knowingly took the doubled-maintenance path.

---

## Open questions for CO

1. **Which option (α / β / γ / phased-γ-then-α)?**
2. **Is the W#23 iOS SwiftUI app in scope** for this decision, or is iOS handled separately? (Recommend separate — iOS is constrained by Apple's tooling, not by our desktop stack choice.)
3. **W#59 Crew Comms — Phase 2 of W#60 (React UI) — does it include a Crew Comms screen?** Yes (per Phase 2 hand-off Phase 4 deliverable). That means the React stack will have Crew Comms before Phase 3 PASS. Reduces the γ-vs-α tradeoff: MAUI Anchor's Crew Comms surface becomes redundant sooner than expected. Worth noting.
4. **Book chapter alignment** — what's the latest chapter status, and how quickly can a pivot signal be reflected? Coordinate with PAO.

---

## Estimate

- This intake: authored (you're reading it)
- ADR 0048 addendum (γ scope clarification): ~1h XO
- ADR 0086 (Tauri-React product surface): ~3h XO
- Phase 3 PASS re-evaluation: a workstream of its own, scoped at Phase 3 PASS time
- Phase 5 W#60 (`docker-compose` self-hosting guide) likely depends on Anchor's fate being settled — keep this in mind when sequencing
