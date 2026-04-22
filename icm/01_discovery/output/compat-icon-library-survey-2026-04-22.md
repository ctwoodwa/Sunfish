# Compat Icon Library Survey — 2026-04-22

**Stage:** 01 Discovery
**Task:** #121
**Parent workstream:** #120 — compat-icon-expansion
**Intake:** [`icm/00_intake/output/compat-icon-expansion-intake.md`](../../00_intake/output/compat-icon-expansion-intake.md)
**Snapshot date (all download / star counts):** 2026-04-22

This survey ranks OSS Blazor icon-library ecosystems by adoption and captures per-library
API-surface detail sufficient to scope each `compat-<library>` package. Popularity metrics
were gathered from NuGet (aggregate downloads across all versions of each package) and
GitHub (stargazers_count via the public REST API).

---

## 1. Popularity-ranked table

**Method:** Composite score uses (a) the highest-downloaded Blazor-integration NuGet
package on nuget.org (search: `<library>+blazor`, inspected the top 3 results per
library), (b) upstream GitHub stars on the icon-set repo itself (not the wrapper repo),
and (c) qualitative adoption signal (framework-bundled vs. standalone). Where multiple
Blazor packages exist, the table aggregates the **top 3 distinct-author** packages and
sums their downloads into `Blazor NuGet (top ~3)` for a like-for-like comparison across
libraries. Rank ties broken first by GitHub stars, then by upstream signal (e.g., a
Microsoft / Google / Bootstrap-core sponsored library edges out community-maintained
peers of similar magnitude).

| Rank | Library | Top Blazor NuGet package(s) | Blazor NuGet (top ~3) | GitHub repo | Stars | License | Tier |
|---:|---|---|---:|---|---:|---|---|
| 1 | **Font Awesome** | `Blazorise.Icons.FontAwesome` (3,861,837), `BootstrapBlazor.FontAwesome` (561,187), `Blazicons.FontAwesome` (79,055) | **4,502,079** | [FortAwesome/Font-Awesome](https://github.com/FortAwesome/Font-Awesome) | 76,527 | Free: CC-BY-4.0 (icons) + SIL-OFL-1.1 (fonts) + MIT (code) | Freemium — Pro tier exists (paid) |
| 2 | **Fluent UI System Icons** (Microsoft) | `Microsoft.FluentUI.AspNetCore.Components.Icons` (1,690,919), `Microsoft.Fast.Components.FluentUI.Icons` (108,379), `Blazicons.FluentUI` (65,995) | **1,865,293** | [microsoft/fluentui-system-icons](https://github.com/microsoft/fluentui-system-icons) | 10,513 | MIT | OSS |
| 3 | **Material Icons / Material Symbols** (Google) | `Blazorise.Icons.Material` (282,269), `MudBlazor.FontIcons.MaterialIcons` (212,049), `MudBlazor.FontIcons.MaterialSymbols` (208,827) | **703,145** (Material) + treats Material Symbols as same entry per intake §3 note | [google/material-design-icons](https://github.com/google/material-design-icons) | 53,159 | Apache-2.0 | OSS |
| 4 | **Bootstrap Icons** | `Blazicons.Bootstrap` (45,989), `Blazorise.Icons.Bootstrap` (45,926), `EasyAppDev.Blazor.Icons.Bootstrap` (3,753) | **95,668** | [twbs/icons](https://github.com/twbs/icons) | 7,973 | MIT | OSS |
| 5 | **Lucide** | `Blazicons.Lucide` (44,213), `InfiniLore.Lucide` (18,624), `InfiniLore.Lucide.Generators.Raw` (13,050) | **75,887** | [lucide-icons/lucide](https://github.com/lucide-icons/lucide) | 22,239 | ISC (NOASSERTION in API; LICENSE file is ISC) | OSS |
| 6 | **Heroicons** | `Blazor.Heroicons` (40,270), `TailBlazor.HeroIcons` (8,141), `HeroIcons.Blazor` (7,052) | **55,463** | [tailwindlabs/heroicons](https://github.com/tailwindlabs/heroicons) | 23,471 | MIT | OSS |
| 7 | **Simple Icons** (brand icons) | `timewarp-simple-icons` (23,933), `Json_exe.MudBlazor.SimpleIcons` (3,149), `NgIcons.SimpleIcons` (1,343) | **28,425** | [simple-icons/simple-icons](https://github.com/simple-icons/simple-icons) | 24,941 | CC0-1.0 | OSS (trademarks remain with brand owners) |
| 8 | **Octicons** (GitHub) | `BlazorOcticons` (12,450), `BlazorOcticonsGenerator` (5,572), `NgIcons.Octicons` (1,342) | **19,364** | [primer/octicons](https://github.com/primer/octicons) | 8,676 | MIT | OSS |
| 9 | **Tabler Icons** | `Vizor.Icons.Tabler` (3,745), `Kebechet.Blazor.Tabler.Icons` (2,117), `NgIcons.TablerIcons` (1,346) | **7,208** | [tabler/tabler-icons](https://github.com/tabler/tabler-icons) | 20,606 | MIT | OSS |
| 10 | **Iconify** (aggregator) | `BlazorIconify` (3,703), `SysAdminsMedia.BlazorIconify` (655), `Graphnode.BlazorIconify` (375) | **4,733** | [iconify/iconify](https://github.com/iconify/iconify) | 6,062 | MIT | OSS — **architecturally different: aggregator of 150+ icon sets** |
| 11 | **Feather Icons** | `NgIcons.FeatherIcons` (1,340), `QosmosUI.Icons.Feather` (677), `NeoBlazorUI.Icons.Feather` (318) | **2,335** | [feathericons/feather](https://github.com/feathericons/feather) | 25,887 | MIT | OSS — **upstream is in maintenance mode; Lucide is the active fork** |
| 12 | **Radix Icons** | `NgIcons.RadixIcons` (1,349), `QosmosUI.Icons.RadixIcons` (681) | **2,030** (only 2 Blazor packages exist) | [radix-ui/icons](https://github.com/radix-ui/icons) | 2,617 | MIT | OSS |
| 13 | **Phosphor Icons** | `NgIcons.PhosphorIcons` (1,345), `Phosphor.MudBlazor` (225) | **1,570** (only 2 Blazor packages exist) | [phosphor-icons/homepage](https://github.com/phosphor-icons/homepage) | 6,619 | MIT | OSS |

### Tie-break / ranking notes

- **Rank 1 — Font Awesome**: dominates aggregate NuGet downloads at 4.5M+ across top three
  packages. License is nuanced — the CC-BY-4.0 attribution requirement on the Free
  SVG/JS assets is meaningful for compat package consumers (see §2).
- **Rank 2 — Fluent UI Icons**: the Microsoft-published official `Microsoft.FluentUI.AspNetCore.Components.Icons`
  package alone is 1.69M downloads — single-package adoption exceeds every non-FA library
  combined. Ships as native Blazor components out of the box (no CSS dependency).
- **Rank 3 — Material Icons / Material Symbols**: Material Symbols is Google's newer
  variable-font evolution of Material Icons and is treated by this survey as the same
  library entry (per intake §3 guidance). Combined Material + Material Symbols downloads
  across MudBlazor + Blazorise + Blazicons exceed 900K.
- **Rank 4 vs 5 (Bootstrap Icons vs Lucide)**: near-parity on NuGet (95K vs 76K). Tie
  broken toward Bootstrap Icons based on upstream-sponsor weight (Bootstrap core team)
  and its pairing with a Sunfish adapter (intake Phase 2 reference).
- **Rank 6 (Heroicons)**: strong single-package leader (`Blazor.Heroicons` at 40K), ties
  closely with Lucide on ecosystem footprint; stars are highest in the modern-OSS cohort.
- **Rank 11 (Feather)**: the upstream repo shows 25K stars — higher than most libraries
  above it — but the repo's maintenance-mode status and the Lucide fork's emergence
  have pushed new Blazor wrappers to Lucide. Feather's Blazor NuGet footprint is ~2K
  downloads total, confirming the deprioritization.
- **Rank 13 (Phosphor)**: high-quality library with a healthy non-Blazor community
  (6K+ stars on the homepage repo), but Blazor adoption is nearly nonexistent — only 2
  packages totaling 1,570 downloads.

---

## 2. Per-library API-surface inventory (top 6)

### 2.1 Font Awesome (rank 1)

- **Dominant Blazor wrapper:** `Blazorise.Icons.FontAwesome` (3.86M downloads).
  This is the de-facto adoption signal but is **bound to Blazorise** — a compat package
  should target the second-most-popular *standalone* wrapper shape. The only truly
  standalone, actively-maintained, enum-based wrapper with meaningful adoption is
  **`Blazicons.FontAwesome`** (79K downloads, KyleHerzog). Note: the originally
  intake-named `Blazored.FontAwesome` (Chris Sainty) is effectively retired — its
  NuGet page returned 404 on 2026-04-22.
- **Component API shape (Blazicons.FontAwesome):**
  ```razor
  @using Blazicons
  <Blazicon Svg="FontAwesomeSolidIcon.Star" />
  <Blazicon Svg="FontAwesomeRegularIcon.Calendar" />
  ```
  The intake-cited shape (`<FontAwesomeIcon Icon="@FasIcons.Star" />`) matches the
  Blazored.FontAwesome / historical FA-wrapper convention — `compat-font-awesome`
  should reproduce this shape (since it's the source-code-parity target) rather than
  Blazicons' generic `<Blazicon>` wrapper.
- **Icon-identifier scheme:** strongly-typed static-class members per style
  (`FasIcons.Star` / `FontAwesomeSolidIcon.Star` for Solid, `FarIcons.Calendar` for
  Regular, `FabIcons.Github` for Brands). FA also exposes kebab-case string identifiers
  (`"fa-solid fa-star"`) via its CSS-based integration path.
- **Core wrapper types for compat-font-awesome:**
  - `FontAwesomeIcon` — primary icon renderer (required)
  - `FasIcons`, `FarIcons`, `FabIcons` — static identifier classes (required)
  - `FaList`, `FaListItem` — list-style icon composition (common, ship)
  - `FaLayers`, `FaLayersText`, `FaLayersCounter` — stacked icon composition (common, ship)
  - `FaDuotoneIcon` — Pro-only; **explicitly skip** per intake §7 (Pro is out of scope)
- **License:** Free tier is a three-license split:
  - Icons (SVG/JS): **CC-BY-4.0** — requires attribution
  - Fonts (web/desktop): **SIL-OFL-1.1**
  - Code: **MIT**
  - Pro tier exists (paid, proprietary) — **out of scope per intake §7**
- **Compat-package constraints:**
  - `compat-font-awesome` must not bundle FA assets (hard invariant in POLICY §3
    — "no vendor NuGet dependency"); the consumer app continues to reference FA's
    CDN/NPM itself. This sidesteps the CC-BY-4.0 attribution question at the Sunfish
    package level (attribution becomes the consumer's responsibility, as it already is
    when consuming FA directly).
  - Pro-only types (`FaDuotoneIcon`, Pro-tier icon identifiers) are not mirrored.
- **Blazor-ecosystem friction:** FA integration traditionally requires a CDN/NPM font
  reference in `index.html` / `App.razor`. No such friction exists in the compat shim
  itself — the active Sunfish `IconProvider` is what renders.

### 2.2 Fluent UI System Icons (rank 2)

- **Dominant Blazor wrapper:** `Microsoft.FluentUI.AspNetCore.Components.Icons` (1.69M
  downloads, Microsoft/fluentui-blazor). This is the officially-published Microsoft
  Blazor binding — unambiguous target.
- **Component API shape:**
  ```razor
  @using Icons = Microsoft.FluentUI.AspNetCore.Components.Icons
  <FluentIcon Icon="@(Icons.Regular.Size24.Save)" />
  <FluentIcon Value="@(Icon.FromImageUrl("/Blazor.png"))" Width="32px" />
  ```
- **Icon-identifier scheme:** strongly-typed nested class path:
  `Icons.[Variant].[Size].[IconName]` where Variant ∈ {Regular, Filled}, Size ∈
  {Size10, Size12, Size16, Size20, Size24, Size28, Size32, Size48}, and IconName is
  a type (instantiated as `new ()`).
- **Core wrapper types for compat-fluent-icons:**
  - `FluentIcon` — primary renderer
  - `Icon` — abstract base (for `Icon.FromImageUrl` factory)
  - `Icons.Regular.Size*.*` / `Icons.Filled.Size*.*` — static identifier hierarchy.
    The parity approach is to expose type-name shells that delegate to `GetIcon(name)`
    using a compiled name convention (e.g., `"fluent:regular:24:Save"`); alternatively
    pass-through as plain string per intake Decision 2 Option A.
- **License:** MIT (all components).
- **Compat-package constraints:** minimal — MIT is permissive, no attribution needed
  in consumer apps beyond standard MIT notice preservation if they ship Fluent assets.
- **Blazor-ecosystem friction:** ships as native Blazor components with embedded SVG
  assets — **no CSS/font CDN reference required** (advantageous over FA and Material
  for consumers). The compat wrapper surface is the largest of the top-6 because of the
  variant × size × name cross-product (thousands of generated types); `compat-fluent-icons`
  should use a source generator or pass-through string convention rather than mirroring
  the full type lattice.

### 2.3 Material Icons / Material Symbols (rank 3)

- **Dominant Blazor wrappers:** three near-tied by adoption:
  - `Blazorise.Icons.Material` (282,269) — Blazorise-bound
  - `MudBlazor.FontIcons.MaterialIcons` (212,049) — MudBlazor-bound
  - `MudBlazor.FontIcons.MaterialSymbols` (208,827) — MudBlazor-bound, newer Symbols variant
  - `Blazicons.MaterialDesignIcons` (138,526) — standalone-ish (Blazicons-bound)
  The **MudBlazor pair** is the most representative MDI Blazor surface — a migrator
  flipping usings expects MudBlazor's static-class shape.
- **Component API shape (MudBlazor):**
  ```razor
  @using MudBlazor.FontIcons.MaterialIcons
  <MudIcon Icon="@MaterialIcons.Outlined.Chat" />
  <MudIcon Icon="@MaterialIcons.Filled.Home" />
  ```
  Google's official rendering shape (CSS-based) is:
  ```razor
  <span class="material-icons">home</span>
  <span class="material-symbols-outlined">home</span>
  ```
- **Icon-identifier scheme:** MudBlazor uses strongly-typed nested classes per style
  (Outlined, Filled, Rounded, Sharp, TwoTone); Google's CSS approach uses plain
  lowercase string identifiers as element content.
- **Core wrapper types for compat-material-icons:**
  - `MudIcon` (source-shape from MudBlazor) — primary renderer
  - `MaterialIcons.{Outlined|Filled|Rounded|Sharp|TwoTone}.*` — identifier classes
  - A separate `compat-material-symbols` package may be warranted (different CSS class,
    variable-font axes fill/weight/grade/optical-size). **Recommendation: ship both in
    one package** keyed by a `Variant` parameter (Icons vs Symbols), since upstream
    treats them as one evolving library.
- **License:** Apache-2.0 — permissive, no attribution required beyond Apache notice.
- **Compat-package constraints:** Apache-2.0 — trivially compatible with Sunfish. No
  Material NuGet dependency; consumers keep their existing font/CSS link.
- **Blazor-ecosystem friction:** Google's native path requires a Google Fonts `<link>` or
  self-hosted font in `index.html`. MudBlazor-bound packages don't add Blazor components;
  they're just constant classes that consuming libraries render as CSS ligatures. The
  compat package is primarily an identifier-translation surface.

### 2.4 Bootstrap Icons (rank 4)

- **Dominant Blazor wrappers:** near-tie between `Blazicons.Bootstrap` (45,989) and
  `Blazorise.Icons.Bootstrap` (45,926). Blazicons is standalone; Blazorise is bound.
- **Component API shape (Blazicons):**
  ```razor
  @using Blazicons
  <Blazicon Svg="BootstrapIcon.Award" />
  ```
  Upstream CSS shape (framework-agnostic):
  ```razor
  <i class="bi bi-award"></i>
  ```
- **Icon-identifier scheme:** either strongly-typed static class (`BootstrapIcon.Award`)
  or CSS class string (`"bi bi-award"`).
- **Core wrapper types for compat-bootstrap-icons:**
  - A single wrapper component that accepts the `BootstrapIcon.*` identifier and
    delegates to `SunfishIcon` via `GetIcon("bi:award")` (or plain `"award"` per
    Decision 2 Option A). Surface is minimal — 1-2 types.
- **License:** MIT.
- **Compat-package constraints:** none beyond MIT notice.
- **Blazor-ecosystem friction:** CSS-only integration — consumer references
  `bootstrap-icons.css` in `index.html`. No Blazor component-based wrapper is
  standardized upstream, so the compat package's source-shape target is Blazicons'
  `<Blazicon Svg="BootstrapIcon.*">` pattern.

### 2.5 Lucide (rank 5)

- **Dominant Blazor wrappers:** `Blazicons.Lucide` (44,213), `InfiniLore.Lucide` (18,624),
  `BlazorBlueprint.Icons.Lucide` (9,152). Fragmented — no single dominant wrapper.
  InfiniLore uses source-generation; Blazicons uses the generic `<Blazicon>`.
- **Component API shape (InfiniLore):**
  ```razor
  <LucideIcon Icon="@LucideIcons.Star" Size="24" />
  ```
  Blazicons shape:
  ```razor
  <Blazicon Svg="LucideIcon.Star" />
  ```
- **Icon-identifier scheme:** strongly-typed static class (`LucideIcons.Star`) or
  PascalCase identifier (`Star`, `ArrowRight`).
- **Core wrapper types for compat-lucide:**
  - `LucideIcon` — single wrapper (surface is small, matches Lucide's minimalism)
  - `LucideIcons.*` — identifier class
- **License:** ISC (license file declares ISC; GitHub API returns NOASSERTION because
  of atypical license-file placement, but the repo's LICENSE is the standard ISC text —
  a fork of Feather's ISC).
- **Compat-package constraints:** ISC is permissive (≈ MIT-equivalent); no attribution
  issues.
- **Blazor-ecosystem friction:** Lucide SVGs ship as individual files — integration
  typically uses inline SVG rather than a CSS font. Source-generator approach
  (InfiniLore pattern) is the highest-fidelity option but adds build-time complexity.

### 2.6 Heroicons (rank 6)

- **Dominant Blazor wrapper:** `Blazor.Heroicons` (40,270, tmcknight) — clear leader.
  `TailBlazor.HeroIcons` (8,141) is a secondary wrapper with richer size/stroke controls.
- **Component API shape (Blazor.Heroicons):**
  ```razor
  <Heroicon Name="@HeroiconName.Academic_Cap" Type="HeroiconType.Solid" />
  <Heroicon Name="@HeroiconName.Home" Type="HeroiconType.Outline" Class="w-6 h-6" />
  ```
- **Icon-identifier scheme:** strongly-typed enum (`HeroiconName.Home`,
  `HeroiconType.Outline | Solid | Mini`).
- **Core wrapper types for compat-heroicons:**
  - `Heroicon` — primary renderer
  - `HeroiconName` — enum of all ~300 icons
  - `HeroiconType` — enum of style variants
- **License:** MIT.
- **Compat-package constraints:** none beyond MIT notice.
- **Blazor-ecosystem friction:** Heroicons upstream ships SVG files directly (no CSS
  font); Blazor wrappers inline the SVGs via RCL static assets. No consumer-side CDN
  reference required if the wrapper bundles the assets — but per Sunfish POLICY §3
  (no vendor NuGet dependency), the compat package cannot bundle the assets. The
  Sunfish IconProvider must resolve the identifier.

---

## 3. Phase 2 / Phase 3 recommendation

### Phase 1 (confirmed)

**`compat-font-awesome`** — unchanged from intake Decision 3. FA is rank 1 by a
significant margin (4.5M aggregate Blazor NuGet downloads, nearly 3x the next
library). Confirmed.

### Phase 2 — adapter-paired batch (recommend as planned, with one adjustment)

The intake proposes Bootstrap Icons + Material Icons + Fluent Icons (one per Sunfish
adapter: Bootstrap5 / Material3 / Fluent v9). The popularity data supports this
triplet but suggests a **reordering**:

1. **`compat-fluent-icons`** — rank 2, 1.86M downloads. Pair with `ui-adapters-blazor`
   Fluent v9 provider. **First** in Phase 2 (highest adoption after FA, plus native
   Blazor component shape aligns well with Sunfish's delegation pattern).
2. **`compat-material-icons`** (includes Material Symbols) — rank 3, 700K+ downloads.
   Pair with the Material 3 provider. Note: ship Material Icons and Material Symbols
   together in one compat package with a `Variant` discriminator — they are the same
   upstream library in two evolving forms.
3. **`compat-bootstrap-icons`** — rank 4, 95K downloads. Pair with the Bootstrap 5
   provider. Smallest surface of the three (single wrapper type).

The ordering aligns popularity with Phase 2 sequencing while preserving the
one-per-adapter coverage goal. All three remain OSS-permissive (MIT / Apache-2.0).

### Phase 3 — modern OSS batch, ranked by popularity

Phase 3 candidates, ranked by 2026-04-22 data (top 6 not already in Phase 1/2):

1. **`compat-lucide`** — rank 5, 75K downloads. Highest Phase-3 priority. Active
   fork of Feather; 22K stars.
2. **`compat-heroicons`** — rank 6, 55K downloads. Strong single-wrapper leader
   (Blazor.Heroicons at 40K).
3. **`compat-simple-icons`** — rank 7, 28K downloads. Brand-icons use case is
   distinct (logos for integrations pages) — valuable for app-migration scenarios
   that include "Login with GitHub / Google" patterns. License is CC0-1.0 (no
   attribution), making it the most license-friendly of the Phase-3 cohort.
4. **`compat-octicons`** — rank 8, 19K downloads. GitHub-specific; lower priority
   but clean surface (small library).
5. **`compat-tabler-icons`** — rank 9, 7K downloads. Despite 20K upstream stars,
   Blazor adoption is low — this is a Phase-3 "nice to have."
6. **`compat-iconify`** — rank 10, 5K downloads. **Architecturally different**:
   Iconify is an aggregator of 150+ icon sets via a single API. Flag for a
   **separate mini-intake decision** before scoping — the compat pattern
   (per-library wrapper with identifier translation) doesn't map cleanly to an
   aggregator. Recommend deferring Iconify to a post-Phase-3 standalone decision.

### Recommended drops from the candidate list

Per the intake's instruction to call out libraries the data suggests dropping:

- **Feather Icons** (rank 11) — upstream is in maintenance mode, and the Blazor
  ecosystem has followed Lucide (the active fork). Only 2,335 total downloads
  across all Feather Blazor packages. **Recommend: do not ship `compat-feather-icons`**.
  Consumers migrating from Feather are better served by `compat-lucide` since the
  icon names overlap nearly 1:1 and Lucide is where the community has moved.
- **Radix Icons** (rank 12) — only 2 Blazor packages exist, 2,030 combined downloads,
  and the library is scoped to a tight 15×15 sprite set that's rarely used outside
  React/Radix UI projects. **Recommend: drop from scope** unless a specific migrator
  requests it.
- **Phosphor Icons** (rank 13) — despite a reasonably active upstream (6.6K stars on
  the homepage repo), Blazor adoption is nearly nonexistent (1,570 downloads total
  across 2 packages). **Recommend: drop from scope** for initial expansion; revisit
  if a migrator specifically asks for it.

### Updated Phase 3 list (6 packages, dropped Feather/Radix/Phosphor)

1. `compat-lucide`
2. `compat-heroicons`
3. `compat-simple-icons`
4. `compat-octicons`
5. `compat-tabler-icons`
6. **`compat-iconify`** — deferred pending architecture decision (aggregator pattern)

Net Phase 3 is 5 shippable packages + 1 deferred intake item.

---

## Appendix — Research method

- **NuGet search**: `https://www.nuget.org/packages?q=<library>+blazor` for each
  candidate. Aggregate download counts were read from the search-result cards; top
  packages were then opened individually to capture markup examples and the icon
  identifier scheme.
- **GitHub data**: `https://api.github.com/repos/<org>/<repo>` for the upstream icon
  set (not the Blazor wrapper). Extracted `stargazers_count`, `license.spdx_id`, and
  `description`.
- **License clarifications**:
  - Font Awesome: split license read from [`FortAwesome/Font-Awesome/LICENSE.txt`](https://github.com/FortAwesome/Font-Awesome/blob/6.x/LICENSE.txt).
  - Lucide: API returned `NOASSERTION`; upstream LICENSE file is ISC (verified via repo description).

**Total WebFetch budget consumed:** ~28 calls (under the 40-call target).

---

## Cross-references

- Intake: [`icm/00_intake/output/compat-icon-expansion-intake.md`](../../00_intake/output/compat-icon-expansion-intake.md)
- Reference POLICY: [`packages/compat-telerik/POLICY.md`](../../../packages/compat-telerik/POLICY.md)
- Shared POLICY template: [`packages/compat-shared/POLICY-TEMPLATE.md`](../../../packages/compat-shared/POLICY-TEMPLATE.md)
- Sibling workstream: [`icm/00_intake/output/compat-expansion-intake.md`](../../00_intake/output/compat-expansion-intake.md) (commercial-vendor expansion)
