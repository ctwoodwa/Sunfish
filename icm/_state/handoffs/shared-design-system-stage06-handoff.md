# Shared Design System — Stage 06 hand-off (W#46)

**From:** XO research session
**To:** sunfish-PM (COB) session
**Workstream:** W#46 — Shared Design System (ADR 0077)
**Spec:** [ADR 0077](../../docs/adrs/0077-shared-design-system.md) (Accepted 2026-05-05 via PR #543)
**Pipeline variant:** `sunfish-feature-change`
**Estimated effort:** 28–38h sunfish-PM time / 6 phases / ~8–10 PRs
**Council posture:** pre-merge council canonical (ADR 0069 D1) **for every phase** — WCAG/a11y
subagent + security-engineering subagent mandatory for Phases 1, 3, 4; design-engineering subagent
for Phase 2.

---

## §A0 — Cited-symbol audit

Per ADR 0069 D2 (XO pre-hand-off verification, three directions):

### Existing — verified on origin/main

| Symbol / Path | Location | Verified |
|---|---|---|
| `Sunfish.Foundation.Capabilities.ICapabilityGraph` | `packages/foundation/Capabilities/ICapabilityGraph.cs` | yes |
| `Sunfish.Foundation.Capabilities.CapabilityAction` | `packages/foundation/Capabilities/` | yes |
| `Sunfish.Foundation.Capabilities.PrincipalId` | `packages/foundation/Capabilities/ICapabilityGraph.cs` parameter | yes |
| `Sunfish.Foundation.Capabilities.Resource` | `packages/foundation/Capabilities/ICapabilityGraph.cs` parameter | yes |
| `Sunfish.Foundation.MissionSpace.IFeatureGate<TFeature>` | `packages/foundation-mission-space/Services/Contracts.cs` | yes |
| `Sunfish.Foundation.MissionSpace.MissionEnvelope` | `packages/foundation-mission-space/` | yes |
| `Sunfish.Foundation.Wayfinder.IStandingOrderIssuer` | `packages/foundation-wayfinder/IStandingOrderIssuer.cs` | yes |
| `Sunfish.Foundation.Wayfinder.IStandingOrderRepository` | `packages/foundation-wayfinder/IStandingOrderRepository.cs` | yes |
| `Sunfish.Foundation.Wayfinder.StandingOrder` | `packages/foundation-wayfinder/StandingOrder.cs` | yes |
| `Sunfish.Foundation.Wayfinder.StandingOrderState` | `packages/foundation-wayfinder/StandingOrderState.cs` | yes |
| `Sunfish.Foundation.Wayfinder.StandingOrderId` | `packages/foundation-wayfinder/StandingOrderId.cs` | yes |
| `Sunfish.Foundation.Wayfinder.StandingOrderScope` | `packages/foundation-wayfinder/StandingOrderScope.cs` | yes |
| `Sunfish.Foundation.Assets.Common.TenantId` | `packages/foundation/` (Assets.Common namespace) | yes |
| `Sunfish.Foundation.Identity.ActorId` | `packages/foundation/` | yes |
| `Sunfish.Kernel.Audit.IAuditTrail` | `packages/kernel-audit/` | yes |
| `Sunfish.Kernel.Audit.AuditEventType` | `packages/kernel-audit/AuditEventType.cs` | yes |
| `Sunfish.Kernel.Audit.AuditRecord` | `packages/kernel-audit/` | yes |
| `packages/ui-adapters-blazor-a11y` | `packages/ui-adapters-blazor-a11y/` | yes |
| `packages/foundation-wayfinder` | `packages/foundation-wayfinder/` (W#42 built 2026-05-04) | yes |
| `NodaTime.Instant` | NuGet (existing dep) | yes |

### Introduced by this hand-off

| Symbol / Path | Phase | Notes |
|---|---|---|
| `packages/foundation-ship-common/` | Phase 1 | new package |
| `Sunfish.Foundation.Ship.Common.ShipRole` | Phase 1 | sealed enum |
| `Sunfish.Foundation.Ship.Common.DivisionAssignment` | Phase 1 | enum |
| `Sunfish.Foundation.Ship.Common.ShipRoleAssignment` | Phase 1 | sealed record |
| `Sunfish.Foundation.Ship.Common.IShipRoleRegistry` | Phase 1 | interface |
| `Sunfish.Foundation.Ship.Common.ShipLocation` | Phase 1 | enum |
| `Sunfish.Foundation.Ship.Common.DeckDepth` | Phase 1 | enum |
| `Sunfish.Foundation.Ship.Common.ShipAction` | Phase 1 | readonly record struct |
| `Sunfish.Foundation.Ship.Common.IPermissionResolver` | Phase 1 | interface |
| `Sunfish.Foundation.Ship.Common.PermissionDecision` | Phase 1 | abstract record (Granted / Denied) |
| `Sunfish.Foundation.Ship.Common.DenialReason` | Phase 1 | enum |
| `Sunfish.Foundation.Ship.Common.Remediation` | Phase 1 | sealed record |
| `Sunfish.Foundation.Ship.Common.RemediationKind` | Phase 1 | enum |
| `Sunfish.Foundation.Ship.Common.IDeckRegistry` | Phase 1 | interface |
| `Sunfish.Foundation.Ship.Common.DeckRegistration` | Phase 1 | sealed record |
| `Sunfish.Foundation.Ship.Common.DefaultPermissionResolver` | Phase 1 | concrete impl |
| `Sunfish.Kernel.Audit.AuditEventType.PermissionDenied` | Phase 1 | new static-readonly constant in existing file |
| `Sunfish.Kernel.Audit.AuditEventType.PermissionDenialRateExceeded` | Phase 1 | new static-readonly constant in existing file |
| `packages/foundation-design-tokens/` | Phase 2 | new package + tokens.json |
| `Sunfish.Foundation.DesignTokens` (namespace) | Phase 2 | code-gen output |
| `packages/ui-core/src/tokens.css` | Phase 2 | code-gen output |
| `Sunfish.UICore.Primitives` (namespace) | Phase 3 | extension to ui-core package |
| `Sunfish.UICore.Primitives.ILiveAnnouncer` | Phase 3 | interface |
| `Sunfish.UICore.Primitives.IFocusTrap` | Phase 3 | interface |
| `Sunfish.UICore.Primitives.IFormControlContract` | Phase 3 | interface |
| `Sunfish.UICore.Primitives.IDiffPreview` | Phase 3 | interface |
| `Sunfish.UICore.Primitives.ISearchAsYouType` | Phase 3 | interface |
| `Sunfish.UICore.FirstAid` (namespace) | Phase 3 | extension to ui-core package |
| `Sunfish.UICore.FirstAid.IFirstAidContract` | Phase 3 | interface |
| `Sunfish.UICore.Conformance` (namespace) | Phase 3 | extension to ui-core package |
| `Sunfish.UICore.Conformance.IConformanceRegistry` | Phase 3 | interface |
| `Sunfish.UICore.Conformance.ConformanceDeclaration` | Phase 3 | record |

### Not-yet-built (ADR 0065-A1 spec; no code on origin/main yet)

| Symbol | Where specified | Impact on this hand-off |
|---|---|---|
| `Sunfish.Foundation.Wayfinder.IStandingOrderEventStream` | ADR 0065-A1 §A1.2 (PR #537 merged docs-only) | Phase 1 `DefaultPermissionResolver` role-assignment cache cannot use subscribe-before-load invalidation. Use **TTL-based cache (60s default)** for Phase 1. Subscribe-before-load is a follow-up once `IStandingOrderEventStream` ships. Add as a halt-condition. |
| `Sunfish.Foundation.Wayfinder.StandingOrderAppliedEvent` | ADR 0065-A1 §A1.1 | Same — not needed until subscribe-before-load cache. |

*§A0 is necessary but not sufficient — pre-merge council is mandatory per ADR 0069 D1.*

---

## Scope summary

Build the **Shared Design System** substrate across three new packages plus extensions to existing
packages. The system provides:

1. `foundation-ship-common` — role taxonomy (`ShipRole`) + permission resolver
   (`IPermissionResolver`) + deck registry (`IDeckRegistry`)
2. `foundation-design-tokens` — W3C Design Tokens format token catalog + build-time contrast
   verification + code-gen (C# + CSS + Markdown)
3. `ui-core` extensions — framework-agnostic component primitives (`Sunfish.UICore.Primitives`),
   First-Aid contextual-help baseline (`Sunfish.UICore.FirstAid`), conformance declaration
   registry (`Sunfish.UICore.Conformance`)
4. Adapter implementations (Phase 4) — Blazor + React + MAUI Win/Mac concrete primitives
5. DI meta-extension `AddSunfishSharedDesignSystem()` + apps/docs (Phase 5)
6. Ledger flip (Phase 6)

**Load-bearing for all W#35 downstream ADRs.** OOD-Watch rotation / Quarterdeck / Engine Room /
Tactical / Sick Bay / Ship's Office UI ADRs all consume `ShipRole`, `IPermissionResolver`, and
the First-Aid baseline established here. Ship these phases before authoring those follow-on ADRs.

### NOT in scope
- OOD-Watch rotation substrate (follow-on ADR after Phase 6 closes)
- Bridge React renderer for system requirements (W#42 follow-on — separate workstream)
- `SupplyOffice` / `Wardroom` / `Brig` permission rules (Phase 2 deferred per ADR 0077 §1.6 / §2.3)
- Per-locale satellite `.resx` files (en-US baseline only; satellite locales via locale-completeness-check cadence)
- `IStandingOrderEventStream` subscribe-before-load cache invalidation (depends on ADR 0065-A1
  code implementation — see §A0 critical-dependency note above)

---

## Phase 1 — `foundation-ship-common` package + permission resolver (~7h)

**What to build:**

Create `packages/foundation-ship-common/Sunfish.Foundation.Ship.Common.csproj` with references to:
- `Sunfish.Foundation.Capabilities` (ICapabilityGraph, CapabilityAction, PrincipalId, Resource)
- `Sunfish.Foundation.MultiTenancy` (ITenantScoped, TenantId)
- `Sunfish.Foundation.Wayfinder` (IStandingOrderIssuer, IStandingOrderRepository,
  StandingOrder, StandingOrderState, StandingOrderScope)
- `Sunfish.Foundation.Assets.Common` (TenantId)
- `Sunfish.Foundation.MissionSpace` (IFeatureGate<TFeature>, MissionEnvelope)
- `Sunfish.Kernel.Audit` (IAuditTrail, AuditEventType, AuditRecord)
- `NodaTime`

**Types to define (all per ADR 0077 §1 / §2 / §3 verbatim):**

```
ShipRole                  — sealed enum (Captain, XO, EngineerOfficer, Navigator,
                            TacticalOfficer, DivisionOfficer, IDC, Scribe, SUPPO,
                            OOD, EOOW)
DivisionAssignment        — enum (MPA, DCA, Comms, Sonar, Electrical, QA)
ShipRoleAssignment        — sealed record (TenantId, ActorId Holder, ShipRole, 
                            DivisionAssignment?, Instant AssignedAt, Instant? RotatesAt,
                            StandingOrderId IssuedBy)
IShipRoleRegistry         — void AssignLabel(ShipRole, string tenantLabel, ScopeRestriction?)
                            + read API
ShipLocation              — enum (Quarterdeck, Wayfinder, EngineRoom, Tactical, SickBay,
                            ShipsOffice, SupplyOffice, Wardroom, Brig)
DeckDepth                 — enum (TopDeck, MainDeck, EngineeringDeck, BelowTheWaterline)
ShipAction                — readonly record struct(string Name) with 9 static-readonly fields
IPermissionResolver       — ValueTask<PermissionDecision> ResolveAsync(...)
PermissionDecision        — abstract record with Granted / Denied subtypes
DenialReason              — enum (7 values per ADR 0077 §2)
Remediation               — sealed record
RemediationKind           — enum (6 values per ADR 0077 §2)
IDeckRegistry             — Register / ForLocation / DefaultLandingDeck
DeckRegistration          — sealed record
```

**DefaultPermissionResolver implementation notes:**

Per ADR 0077 §2.1 resolution algorithm (steps 0–8):

- **Step 0(a) — deck canonicalization:** `ActionMinimumDeck` static-readonly
  `IReadOnlyDictionary<ShipAction, DeckDepth>` mapping; `effectiveDeck =
  max(callerDeck, ActionMinimumDeck[action])`. Use `effectiveDeck` for all subsequent steps.
- **Step 0(b) — promotion guard:** if `action == ShipAction.PromoteRole`, enforce hierarchy
  invariant + self-promotion prohibition.
- **Step 0(c) — resource-scope guard.**
- **Steps 1–8:** implement per ADR 0077 §2.1 verbatim.

**Cache (Phase 1 implementation — TTL-based fallback):**

`IStandingOrderEventStream` is not yet built (ADR 0065-A1 specifies it; code hasn't shipped).
Phase 1 `DefaultPermissionResolver` uses a **simple per-tenant 60-second TTL cache** for
`ShipRoleAssignment` lookups via `IStandingOrderRepository.EnumerateAsync`. On TTL expiry,
cold-reads from the repo. Subscribe-before-load invalidation is a follow-up item (halt-condition
C — see §Halt conditions below).

**Denial-rate-limiting (per §2.4):**

Per-`(ActorId, ShipLocation)` denial counter with 1-minute sliding window. When N=10 denials
exceeded within the window: emit `AuditEventType.PermissionDenialRateExceeded` record + return
`Denied(SecurityPolicyBlocked, ...)` for all subsequent calls until window expiry. Reset on expiry.

**New AuditEventType constants** (add to `packages/kernel-audit/AuditEventType.cs`):

```csharp
// ===== ADR 0077 §2 — W#46 Shared Design System =====

/// <summary>A permission request was denied. Per ADR 0077 §2.4.</summary>
public static readonly AuditEventType PermissionDenied = new("PermissionDenied");

/// <summary>A per-(ActorId, ShipLocation) denial-rate-limit was exceeded within the
/// 1-minute sliding window. Per ADR 0077 §2.4 rate-limiting spec.</summary>
public static readonly AuditEventType PermissionDenialRateExceeded = new("PermissionDenialRateExceeded");
```

**Tests** (`packages/foundation-ship-common/tests/`):

20 unit tests:
- Steps 0–8 each covered: deck-canonicalization promotes `MainDeck` to `BelowTheWaterline` for
  `Quarantine` action; self-promotion returns `Denied(SecurityPolicyBlocked)`; hierarchy-inversion
  returns `Denied(SecurityPolicyBlocked)`; `Watch` precondition fires for `TransferWatch` when
  subject lacks OOD/EOOW; `Phase2Deferred` fires for `SupplyOffice`; `NoMatchingRole` for
  unassigned subject; `DeckRestriction` for `MainDeck` role at `BelowTheWaterline` request;
  happy-path `Granted`.
- Denial accessibility shape: `Denied.ReasonDisplay` non-null + non-empty; `Denied.Remediation`
  non-null with `GuidanceDisplay` populated.
- `BelowTheWaterline` `Granted` emits `PermissionDenied` NOT emitted; wait — `Granted` at
  BelowTheWaterline emits `AuditRecord` (audit-loud set); verify via `IAuditTrail` mock.
- Denial rate-limit: N=10 denials → 11th call returns `Denied(SecurityPolicyBlocked)` + emits
  `PermissionDenialRateExceeded` exactly once.
- Cache TTL: after 60s, EnumerateAsync is called again on the next resolve (mock clock).
- `DefaultLandingDeck`: Captain/XO returns `TopDeck`; EngineerOfficer at EngineRoom returns
  `MainDeck`.

**PR title:** `feat(foundation-ship-common): W#46 Phase 1 — ShipRole taxonomy + IPermissionResolver + DefaultPermissionResolver`

**Gate:** `dotnet build -c Release` clean; 20 new unit tests pass; `ShipRole` sealed enum has
exactly 11 values; `IPermissionResolver.ResolveAsync` returns `ValueTask<PermissionDecision>`;
`AuditEventType.PermissionDenied` + `PermissionDenialRateExceeded` exist in
`packages/kernel-audit/AuditEventType.cs`.

---

## Phase 2 — `foundation-design-tokens` package + W3C token build pipeline (~6h)

**What to build:**

Create `packages/foundation-design-tokens/Sunfish.Foundation.DesignTokens.csproj`.

**`tokens.json` — W3C Design Tokens format source-of-truth:**

Author the full token namespace per ADR 0077 §5.2:
- `sf.color.surface.*` (primary/secondary/tertiary with light/dark variants)
- `sf.color.text.*` (on-surface variants)
- `sf.color.state.*` (success/warning/error/info/focus-ring — extends ADR 0036 SyncState palette)
- `sf.color.role-band.*` (captain/xo/department-head/division-officer/idc/scribe/watch hue bands)
- `sf.typography.*` (family-sans/serif/mono; size xs→4xl; weight 4 values; line-height 3 values)
- `sf.space.*` (4px-grid: 0, 1, 2, 3, 4, 6, 8, 12, 16, 24, 32, 48, 64)
- `sf.radius.*` (none/sm/md/lg/full)
- `sf.elevation.*` (0–3/modal/drawer)
- `sf.motion.*` (duration 4 values; easing 5 values; reduced-motion-fallback zero-motion variant)
- `sf.target-size.*` (min-web 24px; min-ios 44pt; min-android 48dp)

Each color token carries `light` and `dark` mode variants per §5.3 OS-preference tokens.
Also emit `@media` blocks for `prefers-reduced-motion`, `prefers-reduced-transparency`,
`prefers-contrast`, `forced-colors` (Windows High Contrast — mandatory).

**Build-time tooling (add as MSBuild BeforeBuild target in csproj):**

1. **C# codegen** — `tokens.json` → `Sunfish.Foundation.DesignTokens` static readonly const records
   (one class per namespace group: `SurfaceColors`, `TextColors`, `StateColors`, `RoleBandColors`,
   `Typography`, `Space`, `Radius`, `Elevation`, `Motion`, `TargetSize`)
2. **CSS codegen** — `tokens.json` → `packages/ui-core/src/tokens.css` custom properties
   (`:root { --sf-color-surface-primary-light: ...; }`) plus `@media` blocks
3. **Markdown codegen** — `tokens.json` → `apps/docs/design-system/tokens.md` reference table
4. **WCAG 1.4.3 + 1.4.11 contrast verification** — for every `color.text.* × color.surface.*`
   pair: assert contrast ratio ≥4.5:1 (normal text) or ≥3:1 (large text / non-text). **CI BUILD
   FAILS on regression.** Both light and dark themes verified.
5. **CVD ΔE2000 audit** — for every `color.role-band.*` hue: assert pairwise ΔE2000 ≥ threshold
   (per ADR 0036 CVD precedent). Generate `apps/docs/design-system/role-band-cvd.md` audit report.

**Tooling implementation note:** use an MSBuild `Exec` task calling a small .NET tool or script.
Pattern precedent: `packages/kernel-signatures/` uses a similar BeforeBuild codegen step.
If a standalone .NET tool is simpler, place under `tooling/design-tokens-codegen/`.

**Tests** (`packages/foundation-design-tokens/tests/`):

8 unit tests:
- Codegen round-trip: a sample token name maps to the expected C# field name.
- Contrast verification: a deliberately low-contrast pair triggers the build failure signal.
- CVD audit: two role-band hues with ΔE2000 < threshold trigger the audit failure signal.
- tokens.css file exists and contains `--sf-color-surface-primary-light` after codegen.
- `forced-colors` `@media` block present in tokens.css.
- `prefers-reduced-motion` variant present.
- `sf.target-size.min-web` equals `24` (basic sanity check).
- Role-band has exactly 7 hue bands (captain/xo/department-head/division-officer/idc/scribe/watch).

**PR title:** `feat(foundation-design-tokens): W#46 Phase 2 — W3C token catalog + contrast CI gate + CVD audit`

**Gate:** `dotnet build -c Release` clean; contrast verification runs as part of CI; 8 new tests pass;
`packages/ui-core/src/tokens.css` generated with `--sf-color-surface-primary-light` present;
`apps/docs/design-system/tokens.md` and `apps/docs/design-system/role-band-cvd.md` generated.

---

## Phase 3 — `ui-core` extensions: Primitives + FirstAid + Conformance (~5h)

**What to build:**

Extend `packages/ui-core/Sunfish.UICore.csproj` with three new namespace groups. Each group is
a new folder inside `packages/ui-core/`:

**`packages/ui-core/Primitives/` → `Sunfish.UICore.Primitives`:**

```csharp
ILiveAnnouncer           — void Announce(string message, LiveRegionPoliteness politeness)
LiveRegionPoliteness     — enum (Polite, Assertive, Critical)
IFocusTrap               — ValueTask EnterAsync(ct); ValueTask ExitAsync(ct)
IFormControlContract     — string? ErrorMessage; bool IsRequired; etc.
FormControlKind          — enum (Text, Number, Select, Checkbox, etc.)
IDiffPreview             — IReadOnlyList<DiffEntry> Entries; string Summary
DiffEntry                — record (string Field, object? OldValue, object? NewValue)
DiffPreviewView          — enum (Compact, Expanded)
ISearchAsYouType         — ValueTask<IReadOnlyList<T>> SearchAsync<T>(string query, ct)
```

**`packages/ui-core/FirstAid/` → `Sunfish.UICore.FirstAid`:**

Per ADR 0077 §4 verbatim:
```csharp
IFirstAidContract        — HelpKey, FormControl?, NextActionHintKey, HelpLocation,
                           TargetSize, ExemptFromRedundantEntry, LiveAnnouncementPolicy
HelpLocation             — enum (TopOfSurface, Sidebar, HelpButton, Inline)
TargetSizeCompliance     — enum (Conforming, ExemptByException, NonConforming)
```

Reference `Sunfish.UICore.Primitives.LiveRegionPoliteness` for `LiveAnnouncementPolicy`.

**`packages/ui-core/Conformance/` → `Sunfish.UICore.Conformance`:**

```csharp
IConformanceRegistry     — Register(ConformanceDeclaration); 
                           IReadOnlyList<ConformanceDeclaration> ForLocation(ShipLocation)
ConformanceDeclaration   — record (ShipLocation Location, string SurfaceId,
                           Wcag22Level Level, IReadOnlyList<WcagSuccessCriterion> Covered,
                           IReadOnlyList<En301549Chapter> Chapters,
                           IReadOnlyList<ConformanceException> Exceptions,
                           DateTimeOffset DeclaredAt)
Wcag22Level              — enum (A, AA, AAA)
WcagSuccessCriterion     — record (string Id, string Title) — e.g. ("1.4.3", "Contrast (Minimum)")
En301549Chapter          — record (string Id, string Title) — e.g. ("9.1.4.3", "Contrast (Minimum)")
ConformanceException     — record (string CriterionId, string Justification, DateTimeOffset ExpiresAt?)
```

Add `ProjectReference` to `Sunfish.Foundation.Ship.Common` (for `ShipLocation` enum).

**Tests** (`packages/ui-core/tests/`):

12 unit tests:
- `IFirstAidContract` shape: `LiveAnnouncementPolicy` field accepts all `LiveRegionPoliteness` values.
- `IConformanceRegistry.Register` then `ForLocation` returns correct declaration.
- `ConformanceDeclaration` round-trip: DeclaredAt serializes to ISO 8601.
- `ILiveAnnouncer` mock: `Announce` with `Assertive` sets correct politeness enum value.
- `IFocusTrap` mock: enter then exit does not throw.
- `IDiffPreview` mock: two entries with `Summary = "2 changes"`.
- `Wcag22Level.AA` is the mid value.
- `HelpLocation.Inline` is one of the 4 values.
- `TargetSizeCompliance.Conforming` is the expected-pass value.
- `DenialReason` from `foundation-ship-common`: `NoMatchingRole` enum value exists (cross-package
  compile-time check — add `ProjectReference` to `foundation-ship-common` in the test project).
- `FormControlKind` has at least Text / Number / Select / Checkbox.
- `ConformanceException.ExpiresAt` is nullable.

**PR title:** `feat(ui-core): W#46 Phase 3 — UICore.Primitives + UICore.FirstAid + UICore.Conformance`

**Gate:** `dotnet build -c Release` clean; 12 new tests pass; `IFirstAidContract` exists in
`Sunfish.UICore.FirstAid` namespace; `IConformanceRegistry` exists; ui-core test suite remains green.

---

## Phase 4 — Adapter implementations + a11y harness extension + CI gates (~8h)

**What to build:**

Implement the concrete adapter classes for all three adapter packages. This is the largest phase.

**`packages/ui-adapters-blazor/` → Blazor adapters:**

```
BlazorLiveAnnouncer          implements ILiveAnnouncer
                             — Blazor JS interop call to aria-live polite/assertive region
BlazorFocusTrap              implements IFocusTrap
                             — JS interop to trap/release focus in a `<div>` boundary
BlazorFirstAidRenderer       — Razor component consuming IFirstAidContract; renders
                             HelpKey localized text + aria-describedby + NextActionHint
BlazorConformanceRegistration — IConformanceRegistry implementation backed by DI singleton
```

**`packages/ui-adapters-react/` → React adapters:**

```
ReactLiveAnnouncer           — aria-live region via React ref
ReactFocusTrap               — focus-trap-react or equivalent; must be keyboard-reachable
ReactFirstAidRenderer        — JSX component consuming IFirstAidContract
ReactConformanceRegistration — IConformanceRegistry implementation
```

**`packages/ui-adapters-blazor/Maui/` (or `ui-adapters-maui/`) → MAUI adapters:**

```
MauiLiveAnnouncer            implements ILiveAnnouncer for Win + MacCatalyst:
                             Windows: UIAutomation.AutomationElement.RaiseNotificationEvent
                             MacCatalyst: NSAccessibilityPostNotification (NSAccessibilityAnnouncement)
                             iOS/Android: deferred to W#23 follow-up
MauiFocusTrap                implements IFocusTrap
                             Windows: .Focus(FocusState.Keyboard)
                             Mac: NSApp.keyWindow?.makeFirstResponder(view)
MauiFirstAidRenderer         — MAUI BlazorWebView wrapper (Anchor is MAUI Blazor Hybrid)
                             — delegates to BlazorFirstAidRenderer
```

**a11y harness extension (`packages/ui-adapters-blazor-a11y/`):**

Extend `SunfishA11yContract` to require `IFirstAidContract` declaration for every registered
component — a component registered to a `ShipLocation` that does NOT declare `IFirstAidContract`
fails the axe/bUnit a11y gate. This is the CI enforcement of the First-Aid-by-inheritance rule.

**CI gates (add to `.github/workflows/`):**

1. `tokens-contrast.yml` — triggered on any change to `packages/foundation-design-tokens/`.
   Runs the contrast-verification step; fails build if any pair regresses.
2. `conformance-coverage.yml` — for every adapter surface registered to a `ShipLocation`,
   assert `IConformanceRegistry.ForLocation(location)` returns at least one declaration
   with `Level >= Wcag22Level.AA`. Surfaces without a declaration = build failure.
3. `a11y-bindings.yml` — per-platform a11y binding tables complete for every primitive in
   `Sunfish.UICore.Primitives`. A new `ILiveAnnouncer` impl without all three platform stubs
   (Windows/Mac/iOS at minimum) = build failure.

**Tests:**

6 cross-adapter integration tests:
- `BlazorLiveAnnouncer.Announce(Polite)` → JS interop called with `polite`
- `BlazorLiveAnnouncer.Announce(Assertive)` → JS interop called with `assertive`
- `BlazorFocusTrap.EnterAsync` then `ExitAsync` → no exception; focus state observable
- `ReactLiveAnnouncer.Announce` → aria-live region updated
- `MauiLiveAnnouncer.Announce` on simulated Windows env → UIA notification raised
- `MauiLiveAnnouncer.Announce` on simulated MacCatalyst env → NSAccessibility notification raised

**PR title:** `feat(ui-adapters): W#46 Phase 4 — Blazor + React + MAUI a11y primitive impls + CI gates`

**Gate:** `dotnet build -c Release` clean on all adapter packages; 6 integration tests pass;
`BlazorLiveAnnouncer` implements `ILiveAnnouncer`; all three CI gate workflows exist.

---

## Phase 5 — DI meta-extension + apps/docs (~3h)

**What to build:**

**`AddSunfishSharedDesignSystem(IServiceCollection)` meta-extension:**

Add to `packages/foundation-ship-common/ShipCommonServiceExtensions.cs` (or a new
`ShipDesignSystemServiceExtensions.cs` file in `foundation-ship-common` — whichever mirrors the
`WayfinderServiceExtensions.cs` pattern):

```csharp
public static IServiceCollection AddSunfishSharedDesignSystem(this IServiceCollection services)
{
    // Phase 1 — ship-common
    services.AddSingleton<IDeckRegistry, DefaultDeckRegistry>();
    services.AddSingleton<IPermissionResolver, DefaultPermissionResolver>();
    services.AddSingleton<IShipRoleRegistry, DefaultShipRoleRegistry>();
    // Phase 3 — ui-core
    services.AddSingleton<IConformanceRegistry, DefaultConformanceRegistry>();
    // Phase 4 adapters wired separately per adapter package's own extension
    return services;
}
```

Wire `AddSunfishSharedDesignSystem()` into `apps/kitchen-sink/` startup to demonstrate:
- Role-tagged UI with `ShipRole` enum driving a role-band color token
- A denial surface using `PermissionDecision.Denied.ReasonDisplay` + `Remediation.GuidanceDisplay`
  rendered through `BlazorFirstAidRenderer`
- A conformance declaration registered for one kitchen-sink surface

**apps/docs pages:**

- `apps/docs/design-system/README.md` — overview: 3-package structure, how to register,
  link to each sub-page
- `apps/docs/design-system/tokens.md` — auto-generated by Phase 2 codegen (verify it exists)
- `apps/docs/design-system/role-band-cvd.md` — auto-generated by Phase 2 codegen (verify it exists)
- `apps/docs/design-system/conformance-baseline.md` — Stage 07 review checklist:
  which WCAG 2.2 AA SCs are covered by the token system, which require per-component verification,
  which are adapter-specific
- `apps/docs/design-system/platform-a11y-bindings.md` — per-primitive platform binding table:
  | Primitive | Blazor | React | MAUI Win | MAUI Mac | iOS | Android |
  showing what API each adapter uses + "deferred" for iOS/Android where applicable

**PR title:** `feat(design-system): W#46 Phase 5 — AddSunfishSharedDesignSystem + kitchen-sink demo + apps/docs`

**Gate:** `apps/kitchen-sink` builds clean; `AddSunfishSharedDesignSystem()` registers without DI
conflict; 4 apps/docs pages generated and committed; role-tagged UI renders in kitchen-sink.

---

## Phase 6 — Ledger flip + close W#35 follow-on row (~30min)

**What to build:**

- Flip the W#46 row in `icm/_state/active-workstreams.md` from `design-in-flight` →
  `built` with the PR list + new package list + AuditEventType constants added.
- Update memory `project_workstream_46_shared_design_system.md`.
- File a new hand-off for the **OOD-Watch rotation primitive** — the second W#35 follow-on per
  ADR 0077 §Phase 6 completion mandate. The OOD-Watch ADR was filed as an intake stub by W#35;
  it consumes `ShipRole.OOD` + `ShipRole.EOOW` + `IPermissionResolver` from this phase. Do NOT
  start OOD-Watch hand-off until Phase 6 confirms all Phase 1–5 gates are green.

**PR title:** `chore(icm): W#46 Shared Design System ledger flip + OOD-Watch follow-on hand-off note`

**Gate:** active-workstreams W#46 row reads `built`; PR list includes all Phase 1–5 PRs.

---

## Halt conditions

- **A — Council return:** Do NOT enable auto-merge on any phase PR before pre-merge council
  (Opus + xhigh) returns a verdict. Dispatch WCAG/a11y subagent alongside the council subagent for
  Phases 1, 3, 4. Dispatch security-engineering subagent for Phase 1 (permission resolver is a
  security primitive). Dispatch design-engineering subagent for Phase 2 (token palette). Per ADR
  0069 D1; ADR 0077 council posture.

- **B — Build failure on contrast regression:** Phase 2's CI gate deliberately fails the build on
  contrast ratio regression. Do NOT work around the gate — the token palette MUST satisfy WCAG
  1.4.3 + 1.4.11 for both light and dark themes before proceeding to Phase 3.

- **C — `IStandingOrderEventStream` gap:** Phase 1 ships with a 60s TTL cache instead of
  subscribe-before-load invalidation because `IStandingOrderEventStream` is not yet built (ADR
  0065-A1 is spec-only; no code on origin/main). When `IStandingOrderEventStream` ships in
  `packages/foundation-wayfinder/`, the `DefaultPermissionResolver` cache must be upgraded to
  subscribe-before-load per ADR 0077 §2.5. Flag this as a follow-up in the Phase 1 PR description.

- **D — `ui-core` test regressions:** Phase 3 modifies `packages/ui-core/`. Run the full
  `ui-core/tests/` suite (not just the 12 new tests) before committing. Any regression in
  existing `Sunfish.UICore.*` tests = halt + investigate before proceeding.

- **E — Adapter parity:** All three adapters (Blazor + React + MAUI) must implement ALL
  primitives before Phase 4 closes. A partial implementation (Blazor-only) is NOT acceptable per
  the adapter-parity principle in CLAUDE.md. iOS/Android MAUI adapters are explicitly deferred
  (noted in §NOT in scope); Windows + MacCatalyst MAUI are required.

- **F — Kitchen-sink smoke test (Phase 5):** After wiring `AddSunfishSharedDesignSystem()` into
  `apps/kitchen-sink`, manually verify:
  1. A `ShipRole.Captain`-tagged UI element renders with the `captain` role-band token applied.
  2. A `ShipRole.DivisionOfficer` navigating to `EngineRoom` receives a denial surface with
     non-empty `ReasonDisplay` and visible `GuidanceDisplay`.
  3. A conformance declaration is registered and returned by `IConformanceRegistry.ForLocation`.

---

## Prerequisite checks before starting Phase 1

Run these before the first commit:

```bash
# Verify foundation-ship-common doesn't already exist
ls packages/ | grep ship-common  # must return empty

# Verify design-tokens doesn't already exist
ls packages/ | grep design-token  # must return empty

# Verify ICapabilityGraph exists
grep -rn "interface ICapabilityGraph" packages/foundation/Capabilities/

# Verify AuditEventType.PermissionDenied doesn't already exist (no collision)
grep -n "PermissionDenied" packages/kernel-audit/AuditEventType.cs  # must return empty

# Verify IFeatureGate exists
grep -rn "interface IFeatureGate" packages/foundation-mission-space/

# Verify no outstanding PRs touching foundation-ship-common or ui-core (check before P3)
gh pr list --state open | grep -E "foundation-ship|ui-core"
```

**Status:** `ready-to-build`
