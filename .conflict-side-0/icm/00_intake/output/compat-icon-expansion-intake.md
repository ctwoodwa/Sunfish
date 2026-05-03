# Intake Note — Compat Icon Library Expansion (Font Awesome + OSS peers)

**Date:** 2026-04-22
**Requestor:** Christopher Wood (BDFL)
**Request:** Extend the `compat-<vendor>` migration-off-ramp pattern to popular OSS icon libraries, starting with Font Awesome. Deliver per-library compat packages that expose each icon library's markup shape (e.g., `<FontAwesomeIcon Icon="@FasIcons.Star" />`) and delegate to `SunfishIcon` under the hood. Runs parallel to the commercial-vendor compat expansion (Syncfusion / DevExpress / Infragistics); reuses the `compat-shared` primitives shipped under `0eff43b`.

---

## 1. Request Summary

Ship per-library compat packages that each provide a source-shape-compatible migration surface for one OSS (or OSS-tier of freemium) icon library. Wrapper components expose the vendor's component API and delegate rendering to canonical Sunfish (`SunfishIcon` + `ISunfishIconProvider`).

The pattern is identical to compat-vendor: consumers flip `using Blazored.FontAwesome` → `using Sunfish.Compat.FontAwesome` and keep existing markup working. Icon identifiers pass through to the active Sunfish `IconProvider` (FluentUI/Bootstrap/Material) for rendering.

---

## 2. Motivation

Teams migrating to Sunfish from an existing Blazor app inherit whichever icon library that app chose — most commonly **Font Awesome**, followed by **Bootstrap Icons**, **Material Icons**, and the modern OSS set (**Lucide**, **Heroicons**, **Tabler**, **Phosphor**, **Feather**). Without a compat surface, migration means a repo-wide find-and-replace across every icon reference — which is painful even for medium-sized apps.

Three signals align:

1. **compat-shared is ready.** `0eff43b` landed the vendor-agnostic primitives (`CompatChildComponent`, `UnsupportedParam`, `CompatIconAdapter`) — the icon-compat packages consume them directly.
2. **Icon libraries are semantically simpler than full component vendors.** No state, no layout, no events. Each package is typically 1–3 wrapper types plus an identifier-translation surface. Scoping risk is much lower than compat-syncfusion's 12 components.
3. **Covers the same migration story at a fraction of the cost.** Without icon-compat, compat-vendor packages are incomplete — a Telerik migrator often uses Font Awesome icons inside `<TelerikGrid>` cells. Covering both axes together is the first truly flip-your-usings story.

---

## 3. Affected Sunfish Areas

Impact markers are approximate — Stage 01 Discovery will refine via the popularity/license survey.

| Area | Impact | Note |
|---|---|---|
| `packages/compat-font-awesome/` | **new** | First compat-icon package (Phase 1, highest-priority). Shape: `FontAwesomeIcon` wrapper + icon-identifier pass-through. |
| `packages/compat-bootstrap-icons/` | **new** | Phase 2 candidate (pairs with Bootstrap Sunfish provider). |
| `packages/compat-material-icons/` | **new** | Phase 2 candidate (pairs with Material Sunfish provider). |
| `packages/compat-fluent-icons/` | **new** | Phase 2 candidate (pairs with FluentUI Sunfish provider). |
| `packages/compat-lucide/`, `compat-heroicons/`, `compat-tabler-icons/`, `compat-phosphor-icons/`, `compat-feather-icons/`, `compat-octicons/` | **possible** | Phase 3 candidates — ranked by Stage 01 Discovery popularity output. |
| `packages/compat-shared/` | **unaffected** | Reuse existing `CompatIconAdapter` + `UnsupportedParam`. If a second pattern emerges (e.g., an identifier-translation dictionary base), lift it to a new `CompatIconMap<TVendorEnum>` type here. |
| `packages/ui-adapters-blazor/` | **not affected** | No changes — wrappers delegate to existing `SunfishIcon`. |
| `packages/ui-core/` | **not affected** | `ISunfishIconProvider.GetIcon(string name)` signature already accepts any identifier. |
| `apps/kitchen-sink/` | **affected** | Migration demo page per icon library. |
| `apps/docs/` | **affected** | Per-package mapping doc: `docs/compat-font-awesome-mapping.md`, etc. |
| `Sunfish.slnx` | **affected** | 1 new project + 1 new test project per icon package. |

---

## 4. Selected Pipeline Variant

- [x] **`sunfish-feature-change`** — each new compat-icon package is a feature in the "extending adapter support" category.

Not `sunfish-gap-analysis` even though Stage 01 includes a popularity survey — the survey's output is a ranked priority list, not a gap discovery. The features are already scoped at intake.

---

## 5. Open Architecture Decisions

### Decision 1 — Package layout: per-icon-library vs umbrella

**Question:** Ship separate packages (`compat-font-awesome`, `compat-bootstrap-icons`, …), or one umbrella (`compat-icon-libraries/` with subfolders)?

**Recommendation:** **Per-icon-library packages** (consistent with compat-vendor Decision 1).

**Rationale:** IntelliSense cleanliness — an FA migrator shouldn't see Lucide types. License/policy isolation — OSS licenses differ (MIT, Apache 2.0, CC BY 4.0 for some icon sets) and per-package makes each license explicit. Consistency with the compat-vendor shape already shipped.

### Decision 2 — Icon-identifier translation strategy

**Question:** When a consumer writes `<FontAwesomeIcon Icon="@FasIcons.Star">`, how does the compat wrapper render it?

- **A.** Pass the vendor's identifier string (e.g., `"fas-star"`) through to `ISunfishIconProvider.GetIcon(name)`; the active Sunfish provider resolves it against its own icon set.
- **B.** Maintain a per-compat-package translation table from the vendor's identifier → Sunfish's canonical icon name; then call `GetIcon`.
- **C.** Direct-render the vendor's CSS class on the element; Sunfish doesn't translate.

**Recommendation:** **Option A** — pass-through to `GetIcon`.

**Rationale:** Matches the compat-vendor delegation pattern (thin shim, semantics live in the canonical layer). Keeps translation responsibility in one place (the Sunfish `IconProvider` implementations), which are already the authority on icon-name resolution. Option B triplicates maintenance; Option C leaves Sunfish rendering inconsistent across compat and native.

**Implication if reversed later:** If icon-name collision becomes a problem (e.g., FA's `"chevron-down"` maps to a different visual than Bootstrap Icons' `"chevron-down"`), we introduce per-compat-package prefixed identifiers (`"fa:chevron-down"`). That's additive; doesn't break Option A consumers.

### Decision 3 — Priority / phasing

**Question:** What order do the icon-compat packages ship?

**Recommendation:** **Font Awesome first (Phase 1). Phase 2/3 ordering set by Stage 01 Discovery's popularity ranking.**

**Rationale:** FA is the explicit requestor-named target and has the broadest user base (≈7M monthly NuGet downloads across Blazored.FontAwesome + FontAwesome.Sharp + ancillary packages). Other libraries' priorities benefit from an evidence-based ranking (NuGet download stats + GitHub stars + StackOverflow tag counts), which is what Stage 01 produces.

### Decision 4 — Shared primitives: reuse `compat-shared` vs. introduce `compat-icons-shared`

**Question:** Do compat-icon packages need their own shared-primitives package?

**Recommendation:** **Reuse `compat-shared` initially.** Introduce `compat-icons-shared` only when a second icon-specific pattern emerges.

**Rationale:** `CompatIconAdapter` and `UnsupportedParam` already cover 90% of what an icon-compat package needs. The remaining 10% (e.g., a `CompatIconMap<TVendorEnum>` for vendors that expose strongly-typed enum identifiers) is premature to lift without two consumers.

---

## 6. Prerequisites

None — `compat-shared` already shipped (`0eff43b`).

---

## 7. Out of Scope

- **Commercial icon libraries** (Streamline, Nucleo, Icons8). Phase 1+2+3 focus on OSS. Commercial icon libraries may be added later under separate policy review — they typically prohibit the "no NuGet dependency" pattern that compat-shared relies on.
- **Non-Blazor icon libraries**. The compat surface is Blazor-only (consistent with compat-vendor).
- **Pixel-perfect visual parity**. Per shared POLICY: source-shape parity only. If a Sunfish provider's icon renderer produces a visually different glyph than the original, that's documented but not a blocker.
- **Icon authoring or remixing.** Compat packages do not ship the actual SVG/font files — they rely on the active Sunfish `IconProvider` (which may itself ship assets from FluentUI/Bootstrap/Material). If a consumer wants FA glyphs rendered exactly, they continue to reference FontAwesome's CDN/NPM in their `App.razor` as before.
- **Sunfish's own icon set decisions.** That's owned by the `ISunfishIconProvider` implementations in each UI adapter; compat is a pass-through.

---

## 8. Risk and Assumption Log

| # | Assumption | Validate by | Impact if wrong |
|---|---|---|---|
| 1 | Each icon library's public API surface can be captured in ≤5 wrapper types (component + optional icon-set enum + maybe a defaults provider). | Stage 01 per-library audit. | Some libraries may have larger surfaces (FA has `FontAwesomeIcon`, `FaList`, `FaListItem`, `FaLayers`, layers-counter, transform helpers). Impact: surface count per package grows by 2–3; still bounded. |
| 2 | Pass-through identifier strategy (Decision 2 Option A) works for the common case. | Stage 01 empirical check: a Sunfish `IconProvider` resolves FA's `"fas-star"` to a visual equivalent. | If the active Sunfish provider doesn't have a rendering for every vendor identifier, we get broken icons. Mitigation: per-provider Font-Awesome-name translation table lives in the provider, not the compat package. |
| 3 | OSS licenses of in-scope libraries all permit the "no NuGet reference" pattern (type names parity without re-distributing assets). | Stage 01 per-library license read. | Most MIT / Apache-2.0 licenses permit this. CC BY 4.0 (some icon sets) may require attribution in consumer app; document per-package. |
| 4 | Popularity survey methodology (NuGet downloads + GitHub stars + SO tag counts) gives a defensible ranking. | Stage 01 publishes the ranking table. | Different metrics disagree; prioritize the union (if a library is top-5 on any metric, Phase 2 candidate). |

---

## 9. Acceptance Gate (Intake → Discovery)

**BDFL sign-off — 2026-04-22** — approved en bloc, as recommended:

- ✅ **Decision 1** — Per-icon-library packages.
- ✅ **Decision 2** — Option A, pass-through identifier to `GetIcon`.
- ✅ **Decision 3** — Font Awesome first; Stage 01 Discovery ranks Phase 2/3.
- ✅ **Decision 4** — Reuse `compat-shared` until a second pattern emerges.

**Pre-release note:** Sunfish is pre-v1, breaking changes approved, third-party compat compatibility relaxed. Shared POLICY invariants from `compat-shared/POLICY-TEMPLATE.md` apply unchanged — no-NuGet, root-namespace parity, unsupported-throws, documented divergences.

**Dispatch order:**
1. **Now** — Stage 01 Discovery agent: popularity survey across OSS Blazor icon libraries.
2. **After Stage 01** — Phase 1: `compat-font-awesome` scaffolding.
3. **After Phase 1 lands** — Phase 2 batch (BS icons + Material icons + Fluent icons — one per adapter, can fan out 3 ways).
4. **After Phase 2** — Phase 3 batch (modern OSS — Lucide / Heroicons / Tabler / Phosphor / Feather / Octicons, ranked by Stage 01 output).

### Stage 01 Discovery — post-survey update (2026-04-22)

Discovery output at [`icm/01_discovery/output/compat-icon-library-survey-2026-04-22.md`](../../01_discovery/output/compat-icon-library-survey-2026-04-22.md) (13 libraries surveyed, top-6 API inventory). Key deltas:

- **Phase 2 reorder by popularity:** `compat-fluent-icons` → `compat-material-icons` → `compat-bootstrap-icons` (was BS → Material → Fluent). Fluent UI System Icons outrank Material by ~2.5× on Blazor NuGet downloads.
- **Phase 3 revised list:** `compat-lucide`, `compat-heroicons`, `compat-simple-icons` (CC0-1.0 — most license-friendly), `compat-octicons`, `compat-tabler-icons`. Five packages instead of six.
- **Dropped from scope:** `compat-feather-icons` (Lucide replaced it — 2,335 Blazor DL vs Lucide's 75,887), `compat-radix-icons` (2,030 Blazor DL), `compat-phosphor-icons` (1,570 Blazor DL). Adoption data doesn't justify maintenance cost.
- **Iconify deferred:** architecturally different (aggregator of 150+ icon sets). Needs a separate mini-intake to decide shim shape.
- **FA package-landscape correction:** the intake-cited `Blazored.FontAwesome` is effectively retired (404 on NuGet). Realistic standalone-wrapper target is `Blazicons.FontAwesome` (79K downloads), though the source-shape `<FontAwesomeIcon Icon="@FasIcons.Star" />` remains the correct compat target. Blazorise.Icons.FontAwesome (3.86M DL) dominates adoption but ships FA as a Blazorise-internal dependency — not a direct compat shape.
- **Material Icons + Material Symbols merged:** ship as one `compat-material-icons` package with a `Variant` discriminator (same upstream library, two forms).

---

## 10. Discovery Sources (Stage 01)

**Popularity metrics** (rank each library by the union of these):
- NuGet download counts: `https://www.nuget.org/packages?q=<library>+blazor`
- GitHub stars: `https://github.com/<org>/<repo>`
- StackOverflow tag counts for library-specific tags
- Google Trends for Blazor-intersected queries (optional)

**Per-library** (capture minimum surface + license for the candidates named in §3):
- Component API shape (the `<VendorIcon>` markup)
- Icon-identifier scheme (enum, string, typed-icon class)
- License terms
- Major Blazor wrapper packages on NuGet (official or popular community)

**Candidate list for Discovery** (non-exhaustive, ranked by prior art guess — re-rank by output):
Font Awesome, Bootstrap Icons, Material Icons (Google), Fluent UI System Icons, Lucide, Heroicons, Tabler Icons, Phosphor Icons, Feather Icons, Octicons (GitHub), Radix Icons, Simple Icons, Iconify (aggregator — may warrant its own decision).

---

## 11. Task IDs

- **#120 (parent)** — compat-icon-expansion workstream
- **#121** — Stage 01 Discovery: popularity survey + per-library surface inventory
- **#122** — Phase 1: `compat-font-awesome`
- **#123** — Phase 2: BS icons + Material icons + Fluent icons (3 parallel agents)
- **#124** — Phase 3: modern OSS batch (Lucide / Heroicons / Tabler / Phosphor / Feather / Octicons), re-ordered by Stage 01 ranking
- **#125** — Shared policy template + mapping-doc template verification against first shipped icon package

---

## Cross-References

- [`compat-shared/POLICY-TEMPLATE.md`](../../../packages/compat-shared/POLICY-TEMPLATE.md) — shared invariants applied here.
- [`compat-shared/README.md`](../../../packages/compat-shared/README.md) — shared primitives available to compat-icon packages.
- [`icm/00_intake/output/compat-expansion-intake.md`](compat-expansion-intake.md) — sibling workstream (commercial vendor libraries); same pattern, different axis.
- [`packages/compat-telerik/POLICY.md`](../../../packages/compat-telerik/POLICY.md) — reference POLICY implementation; icon packages use the same invariants.

---

## Next Steps

1. **Stage 01 Discovery** — dispatch 1 agent to produce the popularity-ranked icon-library survey + per-library surface inventory. Output: `icm/01_discovery/output/compat-icon-library-survey-2026-04-22.md`.
2. **Phase 1 — compat-font-awesome scaffolding.** Starts after Stage 01 lands (needs the FA surface inventory).
3. **Phase 2 / Phase 3** — fan out by adapter-pairing (phase 2) or popularity (phase 3).
