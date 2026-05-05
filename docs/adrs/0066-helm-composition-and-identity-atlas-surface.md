---
id: 66
title: Helm Composition + Identity Atlas Surface
status: Accepted
date: 2026-05-01
tier: ui-core
pipeline_variant: sunfish-feature-change
concern:
  - ui
  - accessibility
  - identity
  - security
  - configuration
  - audit
enables:
  - helm-widget-composition
  - identity-glance-surface
  - key-rotation-ux
  - recovery-contact-enrollment
  - historical-keys-browse
  - active-team-switcher-widget
composes:
  - 32
  - 36
  - 46
  - 49
  - 62
  - 65
extends: []
supersedes: []
superseded_by: null
amendments:
  - id: A1
    title: IAtlasProvider<T> covariant Atlas-view base
    date: 2026-05-05
    status: Proposed
---
# ADR 0066 — Helm Composition + Identity Atlas Surface

**Status:** Accepted
**Date:** 2026-05-01
**Authors:** XO research session
**Pipeline variant:** `sunfish-feature-change`
**Council posture:** standard adversarial + WCAG/a11y subagent (mandatory; per W#34 §7.4 and ADR 0065 cohort precedent)
**Consumer scope:** Wayfinder UI (Helm pane + Atlas surface — identity sub-surface), Anchor + Bridge accelerators, all `blocks-*` packages that contribute Helm widgets

---

## Status

Proposed. Substrate-tier UI contract — pre-merge council canonical per ADR 0069 (cohort batting average: 23-of-23 substrate amendments needed council fixes; pre-merge council eliminates the post-acceptance amendment cycle). Not auto-mergeable.

---

## Context

The Wayfinder discovery doc (W#34, `icm/01_discovery/output/2026-05-01_wayfinder-configuration-ux.md`) classifies eight Sunfish configuration layers and two cross-cutting UX surfaces. The two cross-cutting surfaces are the **Helm** (live-state pane: "what your device can do right now") and the **Atlas** (deep-config UI: "where you issue Standing Orders"). ADR 0065 (Wayfinder System + Standing Order Contract) specifies the Atlas-as-projection-of-Standing-Orders pattern and the system-wide search/diff-preview surface. ADR 0065 does **not** specify (a) what widgets compose the Helm, (b) the per-widget contract, (c) the layout slots, (d) live-state propagation mechanics, or (e) the identity sub-surface within the Atlas (profile edit, key rotation, recovery contacts, historical keys, active-team switcher).

```
Wayfinder System (ADR 0065)
├── Helm Pane          — live-state observation layer
└── Atlas Surface      — deep-configuration issuance layer
    └── Identity Atlas — sub-surface: recovery contacts + active team
```

W#34 §5.8 (Account / identity layer) tags coverage **Partial**. Cryptographic infrastructure is fully built or fully specified: ADR 0046 ships `Sunfish.Foundation.Recovery.EncryptedField` + `Sunfish.Foundation.Recovery.Crypto.IFieldDecryptor` + role-key wrapping + spouse-recovery semantics; ADR 0046-a1 specifies `HistoricalKeysProjection` for signature survival across operator-key rotation; ADR 0032 ships `Sunfish.Kernel.Runtime.Teams.TeamContext` + `TeamId` for per-team subkey isolation; ADR 0036 ships `Sunfish.Foundation.UI.SyncState` (5-value enum) for live-state encoding; ADR 0049 ships the audit substrate (`AuditRecord`, `IAuditTrail`, `AuditEventType`). What is missing is the **UX**: how the user sees their identity at a glance, enrolls a recovery contact, initiates key rotation, browses historical keys, switches active team. Without this ADR, every block that wants to surface identity invents its own UI; W#23 (iOS Field-Capture) and Phase 2 commercial scope (multi-actor delegation per ADR 0046 spouse-recovery) cannot proceed without a stable identity surface to hang their flows on.

**Prior-art search** (`tools/adr-projections/embed_search.py`, 2026-05-01):
- Top hits for "Helm pane identity Atlas account profile": ADR 0058 A4 (Vendor Onboarding — score 0.529), ADR 0036 (SyncState — 0.514), ADR 0033 (Browser Shell trust posture — 0.498), ADR 0023 (Dialog Provider — 0.471), ADR 0024 (ButtonVariant — 0.469).
- Top hits for "key rotation recovery contacts": ADR 0046 A3 (Phase 1 recovery — 0.574), ADR 0061 A8 (peer transport — 0.511), ADR 0058 A1 (Vendor onboarding — 0.503), ADR 0054 A3 (electronic signature — 0.500), ADR 0028 A8 (CRDT engine — 0.493).

The relevant prior art establishes the substrate; none of it specifies the **composition surface**. ADR 0023 and ADR 0024 are the closest UI-contract precedents for a slot-based widget composition, and this ADR borrows their two-overload `AddSunfishX` DI pattern and `JsonStringEnumConverter` discipline.

---

## Decision drivers

1. **Operator-runtime control.** Per W#34 §3, identity state is *operator-issued* (the user issues a Standing Order to rotate a key, enroll a recovery contact, switch active team) and *runtime-observable* (the user must see the current state at a glance). The Helm is the runtime-observation surface; the Atlas is the issuance surface. Splitting the two preserves the live-state-vs-deep-config distinction that the discovery established.

2. **Composition with W#32 substrate.** `Sunfish.Foundation.Recovery.EncryptedField` and `Sunfish.Foundation.Recovery.Crypto.IFieldDecryptor` already ship on `origin/main` (PR #325/#326 merged 2026-04-30; W#32 substrate). The Helm's identity-glance widget MUST consume the existing substrate without invasive change; this ADR specifies the rendering contract, not new crypto primitives.

3. **Audit-by-construction.** Every identity mutation (profile edit, key rotation, recovery-contact change, active-team switch) is a `StandingOrder` issuance per ADR 0065 and an `AuditRecord` per ADR 0049. The Helm and Atlas surfaces emit no audit records of their own — they are renderers. Audit emission stays in `IStandingOrderIssuer.IssueAsync` and `IAuditTrail.AppendAsync`. (Composition guard: any widget that bypasses the issuer chain is a contract violation.)

4. **WCAG 2.2 AA conformance.** Identity surfaces handle high-stakes operations (key rotation, recovery-contact enrollment, spouse-recovery flow) where SC 3.3.7 (Redundant Entry), 3.3.8 (Accessible Authentication — No Cognitive Function Test), 3.3.9 (Accessible Authentication — Enhanced) apply directly. Per W#34 §5.7 and ADR 0065 §7, WCAG/a11y is a contract, not a goal. Council a11y subagent is mandatory.

5. **Capability gating via ADR 0062.** Per-widget visibility is decided by `ICapabilityGate<TCapability>` against the current `MissionEnvelope` (per ADR 0062). A widget that requires a hardware key reader is hidden when the envelope reports no hardware-key-reader capability. The Helm itself does not gate; it delegates to ADR 0062.

6. **Live-state propagation via SyncState.** The 5-value `SyncState` enum (per ADR 0036) is the canonical encoding for identity glance state (`Healthy` / `Stale` / `Offline` / `Conflict` / `Quarantine`). Identity-glance widget renders SyncState with the cohort-canonical color + icon + label + role agreement (per ADR 0036 five-channel encoding contract).

7. **Cross-Wayfinder boundary clarity.** W#29 (Owner Web Cockpit) is an adjacent UI surface with overlap risk. Per W#34 §7 follow-up note, this ADR explicitly delineates: the Helm is a **system pane** owned by Sunfish framework code, framework-agnostic, identical across Anchor / Bridge / future accelerators; the Cockpit is a **block-level dashboard** owned by `blocks-*` packages, composed of business widgets, accelerator-bespoke. The Helm widget contract is the surface a Cockpit dashboard tile *consumes*; not the other way around.

---

## Considered options

### Option A — Single `IIdentitySurface` interface that renders both Helm and Atlas

A single contract `IIdentitySurface` with two methods (`RenderGlance()` for Helm + `RenderEdit()` for Atlas). Each block implements both. Rejected: conflates two distinct rendering paradigms (live-state-observation vs deep-config-issuance); blocks are forced to implement both even when they only contribute one (e.g., `blocks-properties` contributes a property-glance widget but has no profile-edit surface). Also breaks the W#34-locked Helm-vs-Atlas distinction.

### Option B — One Razor/React component per widget, no contract

Each widget is just a Razor or React component with no shared contract; Helm composition is hand-wired in `accelerators/anchor/MainLayout.razor`. Rejected: no parity discipline (Blazor adapter could ship widgets the React adapter doesn't); no capability-gate consumption (each widget would re-implement gating); no audit-emission discipline; no testability (no contract to mock); fails ADR 0023 / 0024 cohort pattern.

### Option C — Reuse `IBlock` as the Helm widget contract

`IBlock` (the existing module-registration interface per ADR 0015) carries the widget contract directly. Rejected: `IBlock` is a server-side persistence + DI registration surface; conflating it with a UI-rendering contract is a layering violation. ADR 0015 owns the entity-module pattern; ADR 0066 owns the UI-widget pattern. Two distinct concerns, two distinct surfaces.

### Option D — "Grep alone" baseline: skip the contract; document conventions in `apps/docs/wayfinder/helm.md`

No code-level contract; just a markdown doc telling block authors how to write Helm widgets. Rejected per cohort discipline: doc-only conventions drift; the `tools/naming/check.py` cohort lesson (PR #522) demonstrates that documented-only rules without enforcement mechanism produce 100% compliance failure within 3 cohort iterations. Adapter parity is mandatory; without a contract there is no parity test.

### Option E (recommended) — Two contracts: `IHelmWidget` + `IIdentityAtlasSurface`, composed via DI registry

Two distinct contracts in `Sunfish.UICore.Wayfinder` (a new namespace within the existing `packages/ui-core/` package — additive, no new package required):
- **`IHelmWidget`** — every Helm tile is an `IHelmWidget`. Widgets self-declare metadata (slot, capability gate, refresh policy). Registered via `IServiceCollection.AddHelmWidget<TWidget>()`. Discovered at runtime by `IHelmWidgetRegistry`; rendered by adapter-specific renderers (`Sunfish.UI.Adapters.Blazor.Helm.HelmRenderer` + `Sunfish.UI.Adapters.React.Helm.HelmRenderer` — two adapters, one contract).
- **`IIdentityAtlasSurface`** — the identity sub-surface within the Atlas. One implementation per accelerator; Anchor + Bridge each ship their own. Composes the Atlas's `IStandingOrderIssuer` (per ADR 0065) for every identity mutation; never bypasses it.

Two contracts because the rendering paradigms are distinct (one-off live-state tile vs full-surface deep-config form); composition because both compose existing substrate (ADRs 0036, 0046, 0046-a1, 0032, 0062, 0065) without inventing new primitives.

---

## Decision

Option E is selected because it splits Helm and Atlas into named contracts without creating separate packages. The single `ui-core-wayfinder` package boundary keeps the dependency graph flat while enabling separate feature-phasing (Helm Phase 1, Atlas Phase 1, Identity Atlas Phase 2+).

Adopt **Option E**. Specifications follow.

### 1. Helm composition contract

#### 1.1 — `IHelmWidget` interface

New types in `packages/ui-core/Wayfinder/` (additive to existing `Sunfish.UICore` package; no new package):

```csharp
namespace Sunfish.UICore.Wayfinder;

/// <summary>
/// A single tile rendered in the Wayfinder Helm pane. Widgets are registered
/// at startup via <c>IServiceCollection.AddHelmWidget&lt;TWidget&gt;()</c>;
/// the <see cref="IHelmWidgetRegistry"/> discovers all registered widgets and
/// hands them to the adapter-specific renderer.
/// </summary>
/// <remarks>
/// <para>
/// Widgets are <i>renderers</i>, not <i>actuators</i>. A widget MUST NOT issue
/// Standing Orders, mutate state, or emit audit records directly. User actions
/// invoked from a widget (e.g., "switch team", "go offline") flow to an
/// <see cref="IIdentityAtlasSurface"/> implementation or an
/// <c>IStandingOrderIssuer</c> (per ADR 0065).
/// </para>
/// </remarks>
public interface IHelmWidget
{
    /// <summary>The widget's identity, layout slot, and gating metadata.</summary>
    HelmWidgetMetadata Metadata { get; }

    /// <summary>
    /// Computes the widget's current view-state from the live
    /// <see cref="MissionEnvelope"/> (per ADR 0062) and any
    /// widget-bespoke substrate.
    /// </summary>
    ValueTask<HelmWidgetViewState> ComputeAsync(
        HelmRenderContext context,
        CancellationToken ct = default);
}

public sealed record HelmWidgetMetadata(
    string WidgetId,             // stable kebab-case id; e.g., "identity-glance"
    HelmSlot Slot,               // primary layout slot
    int OrderHint,               // stable sort within slot (lower = leftmost / topmost)
    string AccessibleName,       // WCAG 4.1.2 — non-empty
    Type? CapabilityGateType);   // optional ICapabilityGate<T>; null = always shown

public enum HelmSlot
{
    /// <summary>Top-row glance band — identity, sync state, active team, mission summary.</summary>
    GlanceBand,
    /// <summary>Right-side action stack — quick toggles (offline / DND / pause sync).</summary>
    ActionStack,
    /// <summary>Bottom-row activity feed — recent + pending Standing Orders, quota gauges.</summary>
    ActivityFeed,
}

public sealed record HelmWidgetViewState(
    SyncState State,                     // per ADR 0036; canonical 5-value
    string PrimaryLabel,                 // e.g., "Healthy" / "Offline since 14:32"
    string? SecondaryLabel,              // e.g., "Team: Wood Family"
    IReadOnlyList<HelmWidgetAction> Actions);

public sealed record HelmWidgetAction(
    string ActionId,
    string AccessibleLabel,
    HelmActionInvocationKind Kind,       // navigates / issues-standing-order / runs-local-command
    string Target);                      // route / standing-order-path / local-command-id

public enum HelmActionInvocationKind
{
    Navigate,                  // navigate to an Atlas surface
    IssueStandingOrder,        // delegate to IStandingOrderIssuer (ADR 0065)
    RunLocalCommand,           // local-only action (e.g., "refresh sync state")
}

public sealed record HelmRenderContext(
    Sunfish.Foundation.MissionSpace.MissionEnvelope Envelope,
    Sunfish.Foundation.Assets.Common.TenantId Tenant,
    Sunfish.Foundation.Assets.Common.ActorId Actor,
    Sunfish.Kernel.Runtime.Teams.TeamId? ActiveTeam,
    NodaTime.Instant Now);
```

`HelmWidgetMetadata.WidgetId` follows the kebab-case convention used by ADR 0065's `StandingOrder.Path` for path segments (lowercase, hyphen-separated, e.g., `identity-glance`). This is **distinct** from the PascalCase `AuditEventType` constant naming per ADR 0049 §"Naming convention" — `WidgetId` is path-like (user-facing, URL-safe); `AuditEventType` is type-like (programmatic, code-symbol-safe). `OrderHint` defaults to `1000` for unattributed widgets to allow Sunfish-canonical widgets (identity-glance = 100, sync-state = 200, active-team = 300) to render leftmost without per-block coordination.

#### 1.2 — `IHelmWidgetRegistry` + DI registration

```csharp
namespace Sunfish.UICore.Wayfinder;

public interface IHelmWidgetRegistry
{
    IReadOnlyList<IHelmWidget> Widgets { get; }
    IReadOnlyList<IHelmWidget> GetSlot(HelmSlot slot);
}

// Registration — two-overload pattern per cohort discipline
public static class HelmServiceCollectionExtensions
{
    public static IServiceCollection AddSunfishHelm(this IServiceCollection services)
        => services.AddSunfishHelm(_ => { });

    public static IServiceCollection AddSunfishHelm(
        this IServiceCollection services,
        Action<HelmOptions> configure)
    {
        // ...registers IHelmWidgetRegistry singleton, options, and adapter glue
    }

    public static IServiceCollection AddHelmWidget<TWidget>(this IServiceCollection services)
        where TWidget : class, IHelmWidget
        => services.AddSingleton<IHelmWidget, TWidget>();
}
```

Two-overload `AddSunfishHelm` matches the cohort pattern (`AddSunfishKernelAudit`, `AddSunfishKernelCrdt`, `AddSunfishWayfinder` per ADR 0065 §6). `AddHelmWidget<T>` registers each widget as an additional `IHelmWidget` keyed singleton (Microsoft.Extensions.DependencyInjection's multi-registration semantics).

#### 1.3 — Live-state propagation

Helm widgets re-compute on three triggers, in priority order:
1. **Mission Envelope change** — the widget subscribes to `IMissionEnvelopeProvider.Subscribe(IMissionEnvelopeObserver)` (per ADR 0062). On `OnEnvelopeChanged(MissionEnvelopeChange)`, the widget recomputes if its declared `CapabilityGateType` has any dimension that changed.
2. **Standing Order applied** — the widget subscribes to `IStandingOrderApplied` events (per ADR 0065 §6.4 — *if* §6.4 specifies an event-bus surface; otherwise via a local `IObservable<StandingOrderAppliedEvent>` exposed by `IStandingOrderRepository`). Widgets that render Wayfinder-derived state (e.g., active-team) recompute on every applied StandingOrder whose `Scope` matches.
3. **Periodic refresh** — backstop refresh at `HelmOptions.PeriodicRefreshInterval` (default `00:01:00`, configurable). Most widgets ignore this (the two reactive triggers cover their state); the recovery-status widget uses it because pending recovery requests have a grace-window expiry that doesn't fire its own event.
4. **On-reconnect / on-resume** — unconditional full recompute of all widget states; prevents stale-render window after network interruption or process resume.

**Refresh cost discipline.** Widgets MUST be idempotent and side-effect-free. `ComputeAsync` is invoked on the UI thread (Blazor) or via the React render loop; long-running work belongs in a substrate service the widget queries.

#### 1.4 — Canonical Helm widgets specified in this ADR

This ADR specifies six **canonical** widgets that ship in Phase 1 of the build (per ADR 0073 stage-06 hand-off contract). Each is implemented in `packages/ui-core/Wayfinder/Widgets/` with adapter-specific renderers in `ui-adapters-*`:

| WidgetId | Slot | OrderHint | Substrate composed | WCAG SC focus |
|---|---|---|---|---|
| `identity-glance` | GlanceBand | 100 | ADR 0046 (role keys), ADR 0046-a1 (`HistoricalKeysProjection`), `KeyFingerprint` (new) | 1.4.3, 1.4.11, 4.1.2 |
| `sync-state` | GlanceBand | 200 | ADR 0036 (`SyncState`) | 4.1.3, 1.3.3 |
| `active-team` | GlanceBand | 300 | ADR 0032 (`TeamContext`, `TeamId`) | 2.4.6, 4.1.2 |
| `mission-envelope-summary` | GlanceBand | 400 | ADR 0062 (`MissionEnvelope`) | 1.4.10, 4.1.3 |
| `quick-toggles` | ActionStack | 100 | ADR 0065 (`IStandingOrderIssuer`) | 2.5.5, 4.1.2 |
| `recent-standing-orders` | ActivityFeed | 100 | ADR 0065 (`IStandingOrderRepository`) | 1.3.2, 4.1.2 |
| `recovery-status` | GlanceBand | 350 | `ICrewRoster` recovery-contact quorum state; amber ≥1 unverified; red = zero verified | 4.1.3, 1.4.3 | *(Phase 2)* |

(Pending Standing Orders + quota / CRDT growth gauge are defer-Phase-2 widgets; out of scope for this ADR's checklist but reserved in the slot table.)

### 2. Identity Atlas surface

#### 2.1 — `IIdentityAtlasSurface` interface

```csharp
namespace Sunfish.UICore.Wayfinder;

/// <summary>
/// The identity sub-surface within the Wayfinder Atlas. One implementation per
/// accelerator (Anchor, Bridge). Composes the Atlas's
/// <c>IStandingOrderIssuer</c> (per ADR 0065) for every identity mutation;
/// never bypasses it.
/// </summary>
public interface IIdentityAtlasSurface
{
    /// <summary>Profile editor view-model.</summary>
    ValueTask<IdentityProfileEditViewModel> GetProfileEditAsync(
        Sunfish.Foundation.Assets.Common.TenantId tenant,
        Sunfish.Foundation.Assets.Common.ActorId actor,
        CancellationToken ct);

    /// <summary>Key-rotation flow view-model.</summary>
    ValueTask<KeyRotationViewModel> GetKeyRotationAsync(
        Sunfish.Foundation.Assets.Common.TenantId tenant,
        Sunfish.Foundation.Assets.Common.ActorId actor,
        CancellationToken ct);

    /// <summary>Recovery-contact management view-model.</summary>
    ValueTask<RecoveryContactsViewModel> GetRecoveryContactsAsync(
        Sunfish.Foundation.Assets.Common.TenantId tenant,
        Sunfish.Foundation.Assets.Common.ActorId actor,
        CancellationToken ct);

    /// <summary>Historical-keys browse view-model (composes ADR 0046-a1).</summary>
    ValueTask<HistoricalKeysBrowseViewModel> GetHistoricalKeysAsync(
        Sunfish.Foundation.Assets.Common.TenantId tenant,
        Sunfish.Foundation.Assets.Common.ActorId actor,
        CancellationToken ct);

    /// <summary>Active-team overview view-model (composes ADR 0032).</summary>
    ValueTask<ActiveTeamOverviewViewModel> GetActiveTeamOverviewAsync(
        Sunfish.Foundation.Assets.Common.TenantId tenant,
        Sunfish.Foundation.Assets.Common.ActorId actor,
        CancellationToken ct);
}
```

View-model record shapes are specified in `packages/ui-core/Wayfinder/Identity/` and exhaustively listed in the implementation checklist below. Each carries the data needed for the corresponding form view; all mutations go through `IStandingOrderIssuer.IssueAsync` per ADR 0065.

#### 2.2 — Profile-edit Standing Order

Profile-edit emits a `StandingOrder` with:
- `Scope = StandingOrderScope.User`
- `Triples = [("identity.profile.displayName", oldName, newName), ("identity.profile.contactEmail", oldEmail, newEmail), ...]`
- `IssuedBy = actor`
- Diff-preview confirm step is REQUIRED (per ADR 0065 §7's WCAG 3.3.7 contract for User-scope changes that touch contact info — contact-email change is a recovery-vector change and inherits Security-scope confirm semantics).

#### 2.3 — Key-rotation flow

Three-phase UX:
1. **Pre-rotation review** — surface current `KeyFingerprint` (new value type; see §5 below) + `HistoricalKeysProjection.Count` (per ADR 0046-a1) so the user sees what historical signatures will continue to verify.
2. **Confirmation** — diff-preview lists "current key fingerprint" → "new key fingerprint after rotation"; explicit "I understand historical signatures remain valid; new mutations will be signed with the new key" confirm.
3. **Issuance + grace window** — emits `StandingOrder` with `Scope = StandingOrderScope.Security`, `Path = "identity.keys.activeRotation"`. Per ADR 0046, rotations have a 7-day grace window (configurable per tenant). During the window the Helm `identity-glance` widget surfaces a "rotation in progress" state via `SyncState.Stale` + an action button to "view rotation status."

Rotation-window UX: WCAG 2.2.1 (Timing Adjustable) applies — the user can extend the grace window up to 30 days via a Standing Order amendment, no cognitive-recall test required (3.3.8).

#### 2.4 — Recovery-contact management

Composes ADR 0046's spouse-recovery semantics. Three operations:
- **Enroll** — selects an existing `ActorId` (Tenant member) or invites by email/phone. Issues `StandingOrder` with `Path = "identity.recovery.contacts.add"` and `Scope = StandingOrderScope.Security`. Multi-actor approval (per ADR 0046) applies if the issuing actor is a non-primary owner. `IStandingOrderValidator` for Path = `"identity.recovery.contacts.add"` enforces ≤5 active recovery contacts per Security-scope window; verifies at least one contact has a verified status before accepting a quorum increase; audit-emits on rate-limit rejection (path mirrors ADR 0049 audit-by-construction pattern).
- **Verify** — the recovery contact confirms via Standing Order acknowledgment (`Path = "identity.recovery.contacts.verify"`). Until verified, the contact appears in the Atlas with a "pending verification" badge (`SyncState.Stale`).
- **Remove** — issues `Path = "identity.recovery.contacts.remove"`. Per ADR 0046 spouse-recovery semantics, removing the *last* recovery contact for a tenant requires multi-actor approval; the diff-preview surfaces this requirement before issuance.

Spouse-recovery flow: when the issuing actor's relationship to the tenant is `Spouse` (per Phase 2 commercial scope's relationship taxonomy), the enrollment UI surfaces the survivor-recovery semantics ("If the primary owner becomes unavailable, you and N other recovery contacts can co-sign a key recovery") — this is informational text, not a Standing Order field.

#### 2.5 — Historical-keys browse

Composes ADR 0046-a1's `HistoricalKeysProjection`. Renders a table:
- Key fingerprint (`KeyFingerprint`)
- Activated date (`NodaTime.Instant`)
- Retired date (nullable)
- `KeyRotationReason` (per ADR 0046-a1 — non-exhaustive values listed here: `Scheduled` / `Compromise` / `Recovery` / `Migration`; see ADR 0046-a1 for the complete `KeyRotationReason` enum)
- Signature-survival count (number of historical events still verifiable with this key)

Browse is read-only — historical keys are immutable once retired. The view supports filtering by reason and date range; sortable columns with `aria-sort` attributes (WCAG 1.3.1). Per ARIA 1.2, `aria-sort` accepts `none` | `ascending` | `descending` | `other`; the Atlas table MUST declare `aria-sort` on the current sort column with one of these values and `aria-sort="none"` on unsorted columns.

#### 2.6 — Active-team overview

Composes ADR 0032's `TeamContext` per-team subkey derivation. Renders a list of teams the current `ActorId` is a member of, with for each:
- Team display name + `TeamId`
- Current role (per ADR 0046 role-key wrapping)
- Subkey fingerprint
- "Switch to this team" action — local UI-only; does NOT issue a Standing Order (team-switch is a session-local concern per ADR 0032 §"Default: Option C" — the kernel-runtime re-binds views; no global state changes). Per ADR 0032 §"Default: Option C", active-team state is a per-process singleton shared across all windows in that Anchor process. A team switch via the Helm pane propagates immediately to all open windows in the same Anchor instance; separate Anchor processes maintain independent active-team state.

A "leave team" action issues a `StandingOrder` with `Path = "identity.teams.{teamId}.membership"`, `Scope = StandingOrderScope.User`. (Multi-actor approval applies if the leaving actor is the team's last admin.)

### 3. WCAG 2.2 AA conformance

Per W#34 §5.7 and ADR 0065 §7, WCAG/a11y is contract. This ADR's identity surfaces add specific SC requirements beyond the Atlas baseline:

- **3.3.7 Redundant Entry** — within a single key-rotation session, the user is NOT asked to re-enter the new key fingerprint or recovery contact info already supplied. Carry-forward is required. Session-scope means process-lifetime for Anchor (desktop) and tab-lifetime for Bridge (web); the validator MUST persist user preference for the duration of the relevant scope and MUST NOT reset on navigation.
- **3.3.8 Accessible Authentication (No Cognitive Function Test)** — recovery-contact verification MUST NOT use cognitive-recall challenges (no "What was your first pet's name"). Verification uses Standing Order acknowledgment via the contact's own Anchor/Bridge installation.
- **3.3.9 Accessible Authentication (Enhanced)** — key-rotation issuance MAY use device attestation but MUST offer an accessible alternative path (the alternative path is the recovery-contact verification flow per §2.4 — no cognitive recall required).
- **2.2.1 Timing Adjustable** — rotation grace window is user-extensible up to 30 days.
- **1.4.11 Non-Text Contrast** — `KeyFingerprint` rendering uses a distinguishable font (monospace) with ≥3:1 contrast against background; never as the *only* identity signal (always paired with role + team labels).
- **2.4.6 Headings and Labels** — all five identity-Atlas pages have descriptive H1 headings; sub-sections have H2/H3 with `aria-labelledby` linking to form regions.
- **4.1.3 Status Messages** — sync-state changes in Helm widgets fire `aria-live="polite"` announcements (per ADR 0036 five-channel encoding); rotation-progress changes fire `aria-live="assertive"` for compromise-driven rotations only. Compromise events (cryptographic-key compromise, security alert) use `role=alert` (assertive); Scheduled-maintenance events use `aria-live=polite` to avoid interrupting user-initiated actions.
- **EN 301 549 procurement compliance** — for Bridge tenants in EU jurisdictions; identity-Atlas conformance reports produced per release per `apps/docs/wcag/identity-atlas.md` (new file added in Phase 2 of build).

### 4. Composition + cross-references

| Section | Composes | Substrate symbol(s) |
|---|---|---|
| §1.1 IHelmWidget | ADR 0062, ADR 0036 | `MissionEnvelope`, `SyncState`, `ICapabilityGate<T>` |
| §1.2 DI registration | cohort | `AddSunfishHelm` two-overload pattern |
| §1.3 Live-state propagation | ADR 0062, ADR 0065 | `IMissionEnvelopeProvider`, `IStandingOrderRepository` |
| §1.4 Canonical widgets | 0036, 0046, 0046-a1, 0032, 0062, 0065 | (multiple — see table) |
| §2.1 IIdentityAtlasSurface | ADR 0065 | `IStandingOrderIssuer` |
| §2.2 Profile edit | ADR 0065 | `StandingOrder`, `StandingOrderScope` |
| §2.3 Key rotation | ADR 0046, ADR 0046-a1, ADR 0065 | `EncryptedField`, `IFieldDecryptor`, `HistoricalKeysProjection`, `StandingOrder` |
| §2.4 Recovery contacts | ADR 0046, ADR 0065 | `RecoveryContact` (new), `StandingOrder` |
| §2.5 Historical-keys browse | ADR 0046-a1 | `HistoricalKeysProjection`, `KeyRotationReason` |
| §2.6 Active-team overview | ADR 0032 | `TeamContext`, `TeamId` |
| §3 WCAG | W#34, ADR 0065 §7 | (SC numbers) |

### 5. New types proposed by this ADR

| Type | Namespace | Tier | Naming-check verdict |
|---|---|---|---|
| `IHelmWidget` | `Sunfish.UICore.Wayfinder` | ui-core | CLEAN (fuzzy: "Helm" locked vocabulary — intentional; see `_shared/product/naming.md` §Wayfinder vocabulary registry once §OQ-1 disposition lands) |
| `IHelmWidgetRegistry` | `Sunfish.UICore.Wayfinder` | ui-core | CLEAN |
| `HelmWidgetMetadata` | `Sunfish.UICore.Wayfinder` | ui-core | CLEAN |
| `HelmSlot` (enum) | `Sunfish.UICore.Wayfinder` | ui-core | CLEAN |
| `HelmWidgetViewState` | `Sunfish.UICore.Wayfinder` | ui-core | CLEAN |
| `HelmWidgetAction` | `Sunfish.UICore.Wayfinder` | ui-core | CLEAN |
| `HelmActionInvocationKind` (enum) | `Sunfish.UICore.Wayfinder` | ui-core | CLEAN |
| `HelmRenderContext` | `Sunfish.UICore.Wayfinder` | ui-core | CLEAN |
| `HelmOptions` | `Sunfish.UICore.Wayfinder` | ui-core | CLEAN |
| `IIdentityAtlasSurface` | `Sunfish.UICore.Wayfinder` | ui-core | CLEAN (fuzzy: "Atlas" locked vocabulary — intentional; see `_shared/product/naming.md` §Wayfinder vocabulary registry once §OQ-1 disposition lands) |
| `IdentityProfileEditViewModel` | `Sunfish.UICore.Wayfinder.Identity` | ui-core | CLEAN |
| `KeyRotationViewModel` | `Sunfish.UICore.Wayfinder.Identity` | ui-core | CLEAN |
| `RecoveryContactsViewModel` | `Sunfish.UICore.Wayfinder.Identity` | ui-core | CLEAN |
| `RecoveryContact` | `Sunfish.UICore.Wayfinder.Identity` | ui-core | CLEAN |
| `HistoricalKeysBrowseViewModel` | `Sunfish.UICore.Wayfinder.Identity` | ui-core | CLEAN |
| `ActiveTeamOverviewViewModel` | `Sunfish.UICore.Wayfinder.Identity` | ui-core | CLEAN |
| `KeyFingerprint` | `Sunfish.Foundation.Recovery` (additive) | foundation | CLEAN |
| `AddSunfishHelm` (extension method) | `Sunfish.UICore.Wayfinder` | ui-core | CLEAN |
| `AddHelmWidget<T>` (extension method) | `Sunfish.UICore.Wayfinder` | ui-core | CLEAN |

`KeyFingerprint` is the one symbol added to a non-ui-core package. Rationale: rendering a fingerprint is a UI concern but *what counts as a fingerprint* (canonical string form, equality semantics, JSON converter) is a recovery-substrate concern. Placing it in `Sunfish.Foundation.Recovery` keeps it adjacent to `EncryptedField` + `HistoricalKeysProjection` consumers.

---

## §A0 — Self-audit limitation block

The author of this ADR ran the standard 3-direction self-audit (per ADR 0062-A1.14 cohort discipline; per `feedback_decision_discipline.md`). Cohort batting average: 23-of-23 substrate amendments needed council fixes (this ADR is the 23rd case per council review 2026-05-04). Council pre-merge canonical per ADR 0069.

**Council dispositions (2026-05-04 council review, auto-accepted per ADR 0069 Decision Discipline Rule 3):**

- **OQ-1 RecoveryContact vs Trustee (council CONFIRMS author choice):** Council confirms the author's proposed split — `Trustee*` for audit/cryptographic vocabulary (matches ADR 0046's existing `AuditEventType.TrusteeSetChanged` surface), `RecoveryContact*` for user-facing UX (plain-language per WCAG 3.1.5 alignment). Synonymy to be documented at `_shared/product/naming.md` once that file is created (see Follow-on F3). (Council NM-1 disposition.)
- **OQ-2 Flat namespace (council CONFIRMS author choice):** Council confirms `Sunfish.UICore.Wayfinder` flat namespace (not split into `.Helm` + `.IdentityAtlas`). Cohort precedent is `Sunfish.Foundation.Wayfinder` (flat). (Council NM-3 disposition.)
- **OQ-3 RecoveryContact/Trustee split (council CONFIRMS):** See OQ-1 above. (Council NM-1 disposition.)
- **OQ-5 / §A0.4 #5 Helm-vs-Cockpit boundary (council CONFIRMS author's §Decision-drivers #7 sketch):** Council confirms the boundary sketch in §"Decision drivers" #7 is sufficient; no separate boundary ADR is needed before ADR 0066 merges. The `revisit_trigger` block already names "W#29 Owner Web Cockpit ADR introduces a competing widget composition contract" as the re-author condition. (Council NM-4 disposition.)
- **OQ-6 / §A0.4 #6 AuditEventType new constants (council REFUTES — NO new constants needed):** Council confirmed per `packages/foundation-wayfinder/IStandingOrderIssuer.cs:34` that `AuditEventType.StandingOrderIssued` is emitted per issuance regardless of payload; the `Path` field discriminates what changed. No additional `AuditEventType` constants for "ProfileEdited" / "ActiveTeamSwitched" are required or introduced by this ADR. (Council §A0.4 #6 REFUTE disposition.)

### §A0.1 — Negative-existence (do these symbols NOT exist?)

Verified the following are **introduced by this ADR** (do not exist on `origin/main` as of `bf31e04` 2026-05-01):
- `Sunfish.UICore.Wayfinder.*` — entire namespace is new (verified `find packages/ui-core -path "*Wayfinder*"` returns nothing on origin/main except `packages/foundation-wayfinder/` which is the foundation-tier from ADR 0065, distinct from this ui-core-tier surface).
- `IHelmWidget`, `IHelmWidgetRegistry`, `IIdentityAtlasSurface`, `HelmWidgetMetadata`, `HelmSlot`, `HelmWidgetViewState`, `HelmWidgetAction`, `HelmActionInvocationKind`, `HelmRenderContext`, `HelmOptions` — all new.
- `IdentityProfileEditViewModel`, `KeyRotationViewModel`, `RecoveryContactsViewModel`, `RecoveryContact`, `HistoricalKeysBrowseViewModel`, `ActiveTeamOverviewViewModel` — all new.
- `KeyFingerprint` — new in `Sunfish.Foundation.Recovery` (verified `grep -rn "KeyFingerprint" packages/foundation-recovery/` returns nothing).
- `AddSunfishHelm`, `AddHelmWidget<T>` — new extension methods.

**Council MUST spot-check** that `Sunfish.UICore.Wayfinder` is not pre-empted by a parallel-session change between this ADR's authoring (2026-05-01) and council review.

### §A0.2 — Positive-existence (do cited symbols exist?)

Verified the following exist on `origin/main` (`bf31e04`) as cited:

- `Sunfish.Foundation.Recovery.EncryptedField` — `packages/foundation-recovery/EncryptedField.cs` line 6 (`namespace Sunfish.Foundation.Recovery`). Verified `readonly record struct`.
- `Sunfish.Foundation.Recovery.Crypto.IFieldDecryptor` — `packages/foundation-recovery/Crypto/IFieldDecryptor.cs:6` (`namespace Sunfish.Foundation.Recovery.Crypto` — note: `.Crypto` sub-namespace, NOT plain `Sunfish.Foundation.Recovery`; the structural-citation correction is preserved in §2.3 / §5).
- `Sunfish.Foundation.UI.SyncState` — `packages/foundation-ui-syncstate/SyncState.cs` line 1 (`namespace Sunfish.Foundation.UI`). Verified 5-value enum per ADR 0036-A1.
- `Sunfish.Kernel.Runtime.Teams.TeamId` — `packages/kernel-runtime/Teams/TeamId.cs` line 1 (`namespace Sunfish.Kernel.Runtime.Teams`). Verified `readonly record struct TeamId(Guid Value)`.
- `Sunfish.Kernel.Runtime.Teams.TeamContext` — `packages/kernel-runtime/Teams/TeamContext.cs` line 3.
- `Sunfish.Kernel.Audit.AuditRecord`, `Sunfish.Kernel.Audit.IAuditTrail`, `Sunfish.Kernel.Audit.AuditEventType` — `packages/kernel-audit/`. Verified `AuditEventType` is `readonly record struct(string Value)` with static-field constants (NOT an enum — preserves the structural correction first surfaced in ADR 0065 §A0.3).
- `Sunfish.Foundation.MissionSpace.MissionEnvelope`, `IMissionEnvelopeProvider`, `ICapabilityGate<TCapability>` — per ADR 0062 (verified §A0.2 in ADR 0062's own self-audit + post-A1 amendment cohort).
- `NodaTime.Instant` — external package; well-established.
- `IFieldDecryptor` audit-emission contract — `packages/foundation-recovery/Crypto/IFieldDecryptor.cs:6` states "audit-emitting per ADR 0046-A2" via XML doc remark. Widgets MUST NOT call `IFieldDecryptor` from `ComputeAsync` (per OQ-4 recommendation); the fingerprint rendered in the `identity-glance` widget is a non-encrypted projection to avoid spurious audit emission per render.

**Critical structural-citation correction (preserved from authoring):** `ActorId` and `TenantId` are in `Sunfish.Foundation.Assets.Common`, **NOT** `Sunfish.Foundation.Identity` (verified `grep -rn "namespace" packages/foundation/Assets/Common/ActorId.cs`). ADR 0065 §A0.2 cites `Sunfish.Foundation.Identity.ActorId` — that is a structural-citation error in ADR 0065 that THIS ADR does not propagate. The W#33/W#34 cohort will need to address ADR 0065's cite separately; this ADR uses the verified namespace.

**Council MUST spot-check** the namespace discrepancy: confirm whether ADR 0065's `Sunfish.Foundation.Identity.ActorId` cite is (a) a typo to be amended in ADR 0065, (b) a planned move from `Assets.Common` to `Identity` in the Wayfinder build, or (c) my misreading. Recommend (a) — this ADR uses `Sunfish.Foundation.Assets.Common.ActorId` per the source-of-truth file.

### §A0.3 — Structural-citation correctness (do APIs match the cited shape?)

Per cohort discipline (5-of-5 prior structural-citation failures per ADR 0028-A8.11; ADR 0062-A1.1 dropped a hallucinated ADR 0041 cite; ADR 0028-A6.2 cited `required: true` on the wrong type per `feedback_council_can_miss_spot_check_negative_existence`):

- (a) `IMissionEnvelopeProvider.Subscribe(IMissionEnvelopeObserver)` — verified at `docs/adrs/0062-mission-space-negotiation-protocol.md:156`. The §1.3 propagation paragraph cites this signature correctly.
- (b) `IStandingOrderIssuer.IssueAsync` — verified per ADR 0065 §6.x. ADR 0065 itself is `Proposed` (not `Accepted`) and the `IStandingOrderIssuer` symbol is INTRODUCED by ADR 0065 (not yet shipped on `origin/main`). This ADR's references are thus to the *proposed* surface; council MUST treat those references as conditional on ADR 0065's acceptance + build.
- (c) `HistoricalKeysProjection` — verified introduced by ADR 0046-a1 (line 122). ADR 0046-a1 is `Proposed`; the symbol is not on `origin/main` yet. Same conditional-on-acceptance footing as (b).
- (d) `SyncState` enum values — verified per ADR 0036-A1 lines 215-238: `Healthy`, `Stale`, `Offline`, `Conflict`, `Quarantine` (5 values). §1.4 widget table uses `SyncState` correctly.
- (e) `AuditEventType` is a `readonly record struct(string Value)`, NOT an enum — verified `packages/kernel-audit/AuditEventType.cs` line 16. This ADR does NOT introduce new `AuditEventType` constants; identity-related audit events are emitted by `IStandingOrderIssuer` per ADR 0065's per-StandingOrder grain. (No structural risk on this axis for this ADR.)
- (f) WCAG SC numbers verified against W3C WCAG 2.2 specification: 3.3.7 (Redundant Entry — Level A in 2.2), 3.3.8 (Accessible Authentication Minimum — Level AA), 3.3.9 (Accessible Authentication Enhanced — Level AAA; cited only as "MAY use" not contract). 2.2.1 (Timing Adjustable — Level A). 1.4.11 (Non-Text Contrast — Level AA). 2.4.6 (Headings and Labels — Level AA). 4.1.3 (Status Messages — Level AA).
- (g) `TeamContext` carries the per-team subkey through `IRoleKeyManager` per ADR 0032 line 118. The §2.6 reference to "subkey fingerprint" is rendered by *querying* the `IRoleKeyManager` via the `TeamContext`, not by exposing private key material.
- (h) `RecoveryContact` is a NEW type introduced by this ADR; ADR 0046's spouse-recovery semantics speak of "trustees" (per `AuditEventType.TrusteeSetChanged`). Council MUST decide: (i) is "RecoveryContact" the right user-facing name OR (ii) should this ADR adopt "Trustee" as the canonical name to match ADR 0046's vocabulary? Pre-flagged as council pressure-test point §OQ-3 below.

### §A0.4 — Pre-flagged structural concerns for council pressure-testing

1. **Namespace placement of `KeyFingerprint`** — proposed in `Sunfish.Foundation.Recovery`. Alternative: `Sunfish.Foundation.Identity` (does not exist as a namespace today; would be created). Council should decide.
2. **`Sunfish.UICore.Wayfinder` vs `Sunfish.UICore.Helm`** — the Wayfinder umbrella name is locked vocabulary; the Helm is a sub-pane. Namespace `Sunfish.UICore.Wayfinder` covers both Helm and identity-Atlas widgets. Alternative: split into `.Helm` and `.Atlas` sub-namespaces. Council should decide.
3. **"RecoveryContact" vs "Trustee" vocabulary** — ADR 0046 uses "Trustee" in its audit vocabulary; this ADR proposes `RecoveryContact` as the user-facing UX name. Council should rationalize.
4. **Live-state event-bus dependency** — §1.3 trigger #2 (Standing Order applied) presumes ADR 0065 exposes an `IObservable<StandingOrderAppliedEvent>` or similar surface. ADR 0065 §6.x does not specify this explicitly. Council should flag whether this is (i) a Phase-1 requirement on ADR 0065's build, (ii) a polled-poll fallback, or (iii) needs an ADR 0065 amendment.
5. **Composition with W#29 Owner Web Cockpit** — per W#34 §7 follow-up note, the Helm-vs-Cockpit boundary needs explicit relationship clarification. This ADR §"Decision drivers" §7 sketches the boundary; council should pressure-test whether this is sufficient or whether a separate boundary ADR is needed.
6. **`AuditEventType` constants for identity events** — ADR 0046 already defines `KeyRecoveryInitiated` / `KeyRecoveryAttested` / `KeyRecoveryDisputed` / `KeyRecoveryCompleted` / `TrusteeSetChanged`. ADR 0049's per-StandingOrder grain emits one audit event per issuance regardless of payload. Council should confirm: does this ADR need to introduce additional `AuditEventType` constants for "ProfileEdited" / "ActiveTeamSwitched", OR are those covered by the generic `StandingOrderIssued` event type that ADR 0065 introduces?

The §A0 self-audit is *necessary but not sufficient* — council remains canonical defense per the cohort batting average (~65% of XO-authored ADRs had ≥1 structural-citation failure NOT caught by §A0).

---

## Implementation checklist

### Phase 1 — substrate (sunfish-PM)

Hand-off contract per ADR 0073 (stage-06 hand-off template). Halt-conditions named for COB.

- [ ] Add `Sunfish.UICore.Wayfinder` namespace to `packages/ui-core/`. Folder: `packages/ui-core/Wayfinder/`.
- [ ] `IHelmWidget` interface + `HelmWidgetMetadata` + `HelmSlot` enum + `HelmWidgetViewState` + `HelmWidgetAction` + `HelmActionInvocationKind` + `HelmRenderContext` + `HelmOptions` (record types per §1.1).
- [ ] `IHelmWidgetRegistry` interface + `DefaultHelmWidgetRegistry` implementation.
- [ ] `HelmServiceCollectionExtensions` with two-overload `AddSunfishHelm` + `AddHelmWidget<T>` per cohort pattern.
- [ ] `Sunfish.Foundation.Recovery.KeyFingerprint` value type + JSON converter + `IEquatable<KeyFingerprint>` + tests for canonical-string round-trip (additive to `packages/foundation-recovery/`).
- [ ] `IIdentityAtlasSurface` interface + view-model record types in `packages/ui-core/Wayfinder/Identity/`.
- [ ] `RecoveryContact` value type (or — pending §OQ-3 council decision — alias to `Trustee` if ADR 0046's vocabulary wins).
- [ ] xunit tests for `HelmWidgetRegistry` slot-ordering + `OrderHint` stable-sort + capability-gate hide/show; HelmWidget round-trip JSON tests (none — widgets are runtime-only).
- [ ] Two-package XML doc coverage for all public types (per cohort doc discipline).

**Halt-conditions (named for COB):**
- (H1) ADR 0065 must reach `Accepted` status before Phase 1 build starts. ADR 0065 introduces `IStandingOrderIssuer` + `StandingOrder` + `StandingOrderScope` — ALL referenced by this ADR §2. If ADR 0065 is still `Proposed` at hand-off time, COB halts and posts to `cob-question-*.md`.
- (H2) `Sunfish.Foundation.MissionSpace.MissionEnvelope` must be on `origin/main` (per ADR 0062 Phase 1 build). Verify at hand-off via `grep -rn "namespace Sunfish.Foundation.MissionSpace" packages/foundation-mission-space/`.
- (H3) Council pre-merge canonical (per ADR 0069) — Phase 1 PR MUST NOT enable auto-merge. COB stages PR with `gh pr ready --draft` or with `--draft` initially; XO triggers council review; mechanical amendments auto-accept; PR is marked ready-for-review only after council verdict.
- (H8) `IObservable<StandingOrderAppliedEvent>` is not yet defined on `Sunfish.Foundation.Wayfinder` (confirmed by council NM-2 via `grep -rn "IObservable\|StandingOrderApplied" packages/foundation-wayfinder/` = ZERO). Phase 1 of ADR 0066 build halts on §1.3 trigger #2 (Standing Order applied reactive propagation) until ADR 0065-A1 amendment ships the event-stream contract. Until H8 clears, Phase 2 widgets that depend on Standing Order reactive state (`recent-standing-orders`, `quick-toggles` post-issuance refresh) fall back to periodic-refresh + envelope-change triggers only. Resume signal: `grep -rn "StandingOrderAppliedEvent" packages/foundation-wayfinder/` returns ≥1 match after ADR 0065-A1 build lands.

### Phase 2 — canonical Helm widgets (sunfish-PM)

Six canonical widgets per §1.4; each in `packages/ui-core/Wayfinder/Widgets/`:

- [ ] `IdentityGlanceWidget : IHelmWidget` — composes `KeyFingerprint`, `HistoricalKeysProjection`, role-key state.
- [ ] `SyncStateWidget : IHelmWidget` — composes `Sunfish.Foundation.UI.SyncState` per ADR 0036 five-channel encoding.
- [ ] `ActiveTeamWidget : IHelmWidget` — composes `TeamContext` + `TeamId`.
- [ ] `MissionEnvelopeSummaryWidget : IHelmWidget` — composes `MissionEnvelope` (top 3 capability-gate verdicts).
- [ ] `QuickTogglesWidget : IHelmWidget` — composes `IStandingOrderIssuer` for offline/DND/pause-sync toggles.
- [ ] `RecentStandingOrdersWidget : IHelmWidget` — composes `IStandingOrderRepository` query for last 5.
- [ ] Adapter renderers: `Sunfish.UI.Adapters.Blazor.Wayfinder.HelmRenderer` (Razor component) + `Sunfish.UI.Adapters.React.Wayfinder.HelmRenderer` (React component); parity tests.
- [ ] WCAG 2.2 AA conformance test suite — automated assertion of accessible-name, aria-live region presence, keyboard-only navigation through all 6 widgets, contrast ratios.

**Halt-conditions:**
- (H4) ADR 0046-a1 (`HistoricalKeysProjection`) must be `Accepted` + Phase 1 substrate built before `IdentityGlanceWidget` ships (depends on the projection type existing). If still Proposed, COB ships the widget with a placeholder "historical keys: not yet available" SyncState.Quarantine view-state and a follow-up PR queued.
- (H5) Adapter parity gate: parity test suite (per `_shared/engineering/coding-standards.md`) must pass for both adapters before Phase 2 PR closes.

### Phase 3 — identity Atlas surface (sunfish-PM, separate workstream)

Implements `IIdentityAtlasSurface` for Anchor + Bridge accelerators:

- [ ] `accelerators/anchor/Wayfinder/Identity/AnchorIdentityAtlasSurface.cs` — Anchor implementation.
- [ ] `accelerators/bridge/Wayfinder/Identity/BridgeIdentityAtlasSurface.cs` — Bridge implementation (subset; Bridge admin operates on hosted-tenant identity surface, not own).
- [ ] Five view pages per §2.2-§2.6 with WCAG-conformant Razor + React parity.
- [ ] Diff-preview UI wiring for User-scope + Security-scope Standing Orders per ADR 0065 §7.
- [ ] Recovery-contact verification flow integration (composes ADR 0046's spouse-recovery + Trustee semantics).
- [ ] `apps/docs/wcag/identity-atlas.md` conformance report stub + per-release update workflow.

**Halt-conditions:**
- (H6) Phase 3 cannot start until Phase 2 lands (the canonical widgets are how the user navigates *to* the Atlas surface).
- (H7) Phase 2 commercial scope's relationship taxonomy (per W#5) must be on `origin/main` before §2.4's spouse-recovery surfacing ships. If not, COB ships §2.4 with an "info text not yet available" placeholder + queue follow-up.

---

## Open questions

1. **OQ-1: `RecoveryContact` vs `Trustee` vocabulary.** ADR 0046's audit events use "Trustee" (`TrusteeSetChanged`). User-facing UX may benefit from "recovery contact" (less technical). Decision: rationalize in council. Recommended: use `RecoveryContact` user-facing, keep `Trustee` audit-vocabulary, document the synonymy in `_shared/product/naming.md`.

2. **OQ-2: `Sunfish.UICore.Wayfinder` vs split into `.Helm` and `.Atlas`.** Single namespace (this draft) or split (allows independent versioning)? Recommended: single namespace with sub-folders (`Wayfinder/Helm/`, `Wayfinder/Identity/`); namespace stays flat for type-resolution simplicity.

3. **OQ-3: `IObservable<StandingOrderAppliedEvent>` dependency on ADR 0065.** §1.3 trigger #2 presumes ADR 0065 exposes a reactive surface. Council should determine: (a) does ADR 0065 already cover this implicitly via `IStandingOrderRepository`, (b) needs an amendment, or (c) acceptable to poll at `HelmOptions.PeriodicRefreshInterval`.

4. **OQ-4: Helm widget caching policy.** `IHelmWidget.ComputeAsync` is invoked on every render; widgets that query `IFieldDecryptor` (which is audit-emitting per ADR 0046-A2) would emit an audit record per render. Recommended (and adopted): widgets MUST NOT call `IFieldDecryptor` from `ComputeAsync`; rendered fingerprint is a non-encrypted projection. Council confirm.

5. **OQ-5: Bridge admin Helm subset.** Bridge serves N hosted tenants; the Bridge admin operator's Helm should render Bridge ops state, not per-tenant identity glance. §2 of the implementation checklist's Bridge surface needs scoping. Recommended: separate Bridge ops Helm widget set deferred to a future ADR; Phase 3 ships only the per-tenant identity Atlas for Bridge-hosted self-service users.

6. **OQ-6: KeyFingerprint canonical form.** Hex-encoded SHA-256 of public key, or first-N-bytes-base32 (per OpenSSH convention)? Recommended: hex SHA-256 with `:` group separators every 2 bytes for readability (`AB:CD:...`); 4 lines of 16 groups (32 bytes). Council confirm or override.

---

## Revisit triggers

- ADR 0065 (Wayfinder System + Standing Order Contract) is rejected, withdrawn, or substantively re-shaped during council. Triggers re-author.
- ADR 0062 (Mission Space Negotiation Protocol) introduces a 3rd capability-gate concept beyond `ICapabilityGate<T>` + `IFeatureBespokeProbe`. Triggers §1.3 update.
- ADR 0046-a1 (Historical-Keys Projection) is rejected or re-scoped. Triggers §2.5 + §1.4 (`identity-glance` widget) update.
- W#29 Owner Web Cockpit ADR introduces a competing widget composition contract. Triggers boundary clarification (§"Decision drivers" §7) into an explicit boundary ADR.
- WCAG 2.3 (post-2026 hypothetical) introduces new identity-relevant SC. Triggers §3 amendment.
- A 7th canonical Helm widget is needed (e.g., quota gauge from paper §9, pending Standing Orders surface). Triggers §1.4 amendment.
- A new accelerator beyond Anchor + Bridge needs `IIdentityAtlasSurface` implementation that doesn't fit the per-accelerator pattern. Triggers §2.1 amendment.

---

## References

### ADRs composed

- [ADR 0032](./0032-multi-team-anchor-workspace-switching.md) — Multi-team Anchor (`TeamContext`, `TeamId`)
- [ADR 0036](./0036-syncstate-multimodal-encoding-contract.md) — SyncState multimodal encoding (`Sunfish.Foundation.UI.SyncState`)
- [ADR 0046](./0046-key-loss-recovery-scheme-phase-1.md) — Key-loss recovery + `EncryptedField` + `IFieldDecryptor`
- [ADR 0046-a1](./0046-a1-historical-keys-projection.md) — Historical-keys projection
- [ADR 0049](./0049-audit-trail-substrate.md) — Audit trail substrate (`IAuditTrail`, `AuditRecord`, `AuditEventType`)
- [ADR 0062](./0062-mission-space-negotiation-protocol.md) — Mission Space Negotiation (`MissionEnvelope`, `IMissionEnvelopeProvider`, `ICapabilityGate<T>`)
- [ADR 0065](./0065-wayfinder-system-and-standing-order-contract.md) — Wayfinder System + Standing Order contract

### ADRs referenced (composition guard / boundary discipline)

- [ADR 0009](./0009-foundation-featuremanagement.md) — FeatureManagement (Wayfinder integration ADR amendment forthcoming per W#34 §6.5)
- [ADR 0015](./0015-module-entity-registration.md) — Module-entity registration (clarifies why `IBlock` is not the Helm widget contract)
- [ADR 0023](./0023-dialog-provider-slot-methods.md) — Dialog provider slot pattern (precedent for slot-based UI composition)
- [ADR 0024](./0024-button-variant-enum-expansion.md) — ButtonVariant enum expansion (precedent for cohort-canonical UI surface vocabulary)
- [ADR 0028](./0028-crdt-engine-selection.md) — CRDT engine selection (Standing Order distribution substrate per ADR 0065)
- [ADR 0033](./0033-browser-shell-render-model-and-trust-posture.md) — Browser Shell render model (adjacent UI-trust prior art)
- [ADR 0058](./0058-vendor-onboarding-posture.md) — Vendor Onboarding Posture (top embed_search hit; identity-edit precedent)
- [ADR 0069](./0069-adr-authoring-discipline.md) — ADR authoring + council discipline (this ADR conforms)
- [ADR 0073](./0073-stage06-handoff-template-contract.md) — Stage 06 hand-off contract (this ADR's checklist conforms)

### Discovery + intake artifacts

- `icm/01_discovery/output/2026-05-01_wayfinder-configuration-ux.md` — W#34 Wayfinder discovery (parent)
- `icm/00_intake/output/2026-05-01_helm-and-identity-atlas-intake.md` — this ADR's source intake

### Cohort discipline references

- `feedback_decision_discipline.md` — verify cited symbols; UPF-when-no-recommendation; auto-accept mechanical council amendments
- `feedback_council_can_miss_spot_check_negative_existence.md` — council misses in 3 directions (negative + positive + structural)
- ADR 0062 §"Cohort discipline" — origin of §A0 self-audit limitation block format
- `_shared/engineering/naming-canon.md` — naming-collision rules (this ADR conforms; all proposed symbols passed `tools/naming/check.py auto`)

### Prior-art search results (reproducible)

```bash
python3 tools/adr-projections/embed_search.py search "Helm pane identity Atlas account profile" --top 8 --collapse
python3 tools/adr-projections/embed_search.py search "key rotation recovery contacts" --top 5 --collapse
```

Top hits cited inline in §"Context".

### WCAG references

- WCAG 2.2 (W3C Recommendation 2023-10-05) — SC 3.3.7, 3.3.8, 3.3.9, 2.2.1, 1.4.11, 2.4.6, 4.1.3
- EN 301 549 V3.2.1 (2021-03) — European procurement accessibility standard, superset target

### External package versions verified

- `NodaTime` (transitively pulled in by `Sunfish.Foundation` per `Directory.Packages.props`) — used for `Instant`
- `Microsoft.Extensions.DependencyInjection` — `IServiceCollection` extension methods

---

## Amendment A1 — `IAtlasProvider<T>` covariant Atlas-view base

**Status:** Proposed
**Date:** 2026-05-05
**Authors:** XO research session
**Council posture:** pre-merge canonical (per ADR 0069; cohort batting average ≥95% substrate amendments needed council fixes — auto-merge NOT enabled)
**Scope:** additive amendment to `Sunfish.UICore.Wayfinder` in `packages/ui-core/`; no changes to existing types

### A1.1 — Context

ADR 0066 specifies `IHelmWidget` (Helm pane live-state surface) and `IIdentityAtlasSurface` (identity sub-surface within the Atlas). It does **not** define a generic Atlas-provider base type, because at the time of authoring the only planned Atlas sub-surface was the identity sub-surface and the general pattern had not crystallized.

Two downstream requirements have since surfaced this gap:

1. **W#48 Atlas Integration-Config UI Surface (ADR 0067)** — introduces `IIntegrationAtlasProvider : IAtlasProvider<IntegrationAtlasView>`. ADR 0067 §§2–3 define this as the first concrete specialization of a covariant generic base, but the base type `IAtlasProvider<T>` is not in the ADR 0066 body.

2. **ADR 0068 Tenant Security Policy + Atlas Surface (W#37)** — Phase 2 of ADR 0068 introduces `ISecurityPolicyAtlasProvider : IAtlasProvider<SecurityPolicyAtlasView>`. ADR 0068 §7 explicitly gates Phase 2 on this amendment reaching `Accepted`.

The W#53 (Helm + Identity Atlas) Stage 06 hand-off at `icm/_state/handoffs/helm-identity-atlas-stage06-handoff.md` already specifies that `IAtlasProvider<T>` is built in Phase 1a. This amendment formally ratifies that contract in the ADR record.

### A1.2 — Decision: `IAtlasProvider<out TView>` interface

A new covariant generic interface in `Sunfish.UICore.Wayfinder`, aligned to the W#53
Stage 06 hand-off spec:

```csharp
namespace Sunfish.UICore.Wayfinder;

/// <summary>
/// Base contract for Atlas sub-surface data providers. A provider
/// materializes the current Atlas view for a given sub-surface (integration
/// configuration, security policy, identity, etc.) without prescribing the
/// rendering layer.
/// </summary>
/// <typeparam name="TView">
/// Covariant Atlas-view type (must be a reference type). The <c>out</c>
/// modifier permits assignment of <c>IAtlasProvider&lt;ConcreteView&gt;</c>
/// to a variable typed as <c>IAtlasProvider&lt;object&gt;</c> without casting,
/// enabling heterogeneous registration lists in the Atlas shell.
/// </typeparam>
/// <remarks>
/// <para>
/// Implementations are registered by the host accelerator (Anchor or Bridge)
/// and resolved by the Atlas shell renderer. The provider is a read-model
/// only — mutations are always issued via
/// <c>Sunfish.Foundation.Wayfinder.IStandingOrderIssuer</c> (ADR 0065);
/// the provider reacts to applied Standing Orders via
/// <c>Sunfish.Foundation.Wayfinder.IStandingOrderEventStream</c> (ADR 0065-A1)
/// to regenerate its view.
/// </para>
/// <para><strong>§GC.1 note:</strong> Implementations that surface regulated-data
/// attributes (security policy presets, MFA posture) carry the ADR 0068 §GC.1
/// general-counsel engagement obligation. This interface itself is neutral;
/// the obligation travels with the concrete view-model type.</para>
/// </remarks>
public interface IAtlasProvider<out TView>
    where TView : class
{
    /// <summary>
    /// Materializes the current Atlas view for this sub-surface.
    /// Must be side-effect-free; projection only.
    /// </summary>
    Task<TView> GetAtlasViewAsync(CancellationToken ct = default);
}
```

**Return type (`Task<TView>`, non-nullable):** Aligned to the W#53 hand-off spec (line 96). The provider always produces a view; the Atlas shell decides whether to display or hide a sub-surface based on capability gates — that decision is above the provider layer. The non-nullable contract keeps downstream implementations (ADR 0067 `IIntegrationAtlasProvider`, ADR 0068 `ISecurityPolicyAtlasSurface`) free of null-propagation defensive code.

**Covariance (`out TView where TView : class`):** The `where TView : class` reference-type constraint is required for covariance to work safely. C# covariance is limited to reference types; without the constraint, a value-type `TView` would violate the variance rules at compile time. The `out` modifier permits `IAtlasProvider<ConcreteView>` to be assigned to `IAtlasProvider<object>` (the covariance test in the W#53 hand-off uses `object` as the supertype for the registration list).

### A1.3 — Namespace and package placement

`IAtlasProvider<T>` lives in `Sunfish.UICore.Wayfinder` (namespace flat; `packages/ui-core/Wayfinder/`), consistent with ADR 0066 §5's namespace decision (OQ-2 — council confirmed flat namespace over split). The namespace does NOT exist on `origin/main` as of `ca4bbd9`; it is created by W#53 Phase 1a build (this amendment authorizes the type before the build, per the W#53 hand-off contract).

Naming-tool check: `tools/naming/check.py auto IAtlasProvider` → CLEAN (no collisions with `foundation-wayfinder`, `Sunfish.Foundation.Wayfinder.*`, or `Sunfish.UICore.*` existing types on `origin/main`).

### A1.4 — New type introduced by A1

| Type | Namespace | Tier | Naming-check |
|---|---|---|---|
| `IAtlasProvider<out TView>` | `Sunfish.UICore.Wayfinder` | ui-core | CLEAN |

Note: No `IAtlasView` marker interface is introduced by this amendment (deferred per YAGNI; no consumer of a heterogeneous `IAtlasProvider<IAtlasView>` list exists in any current ADR or hand-off; the W#53 covariance test uses `IAtlasProvider<object>`). A future amendment may introduce `IAtlasView` when a real consumer emerges.

### A1.5 — Implementation checklist (W#53 Phase 1a)

- [ ] `IAtlasProvider<out TView> where TView : class` interface in
  `packages/ui-core/Wayfinder/IAtlasProvider.cs`
- [ ] XML doc coverage including §GC.1 note (use plain `<c>` text for
  cross-namespace crefs to avoid CS1574 — verify `packages/ui-core/ui-core.csproj`
  includes a ProjectReference to `packages/foundation-wayfinder/` before using
  `<see cref="IStandingOrderIssuer"/>`)
- [ ] Negative-existence pre-flight: `grep -rn "IAtlasProvider" packages/ui-core/`
  returns zero matches before Phase 1a PR; one match after
- [ ] Unit test: covariance — `IAtlasProvider<object> _ = new StubAtlasProvider()`
  where `StubAtlasProvider : IAtlasProvider<object>` (compile-time assignment,
  matching W#53 hand-off lines 449–454)
- [ ] Pre-merge council canonical; auto-merge disabled until verdict received

**Halt-conditions (W#53 Phase 1a):**

- **(HA1)** This amendment must reach `Status: Accepted` on `origin/main` before
  W#53 Phase 1a PR may auto-merge. The W#48 Phase 1 operational gate is
  `grep -rn "IAtlasProvider" packages/ui-core/` ≥ 1 match; this amendment is
  the ADR-level ratification ahead of that check.

### A1.6 — Cross-references

- **W#48 ADR 0067** — first concrete specialization:
  `IIntegrationAtlasProvider : IAtlasProvider<IntegrationAtlasView>`.
  Phase 1 of W#48 gates on W#53 Phase 1 (this type landing on `origin/main`).
- **ADR 0068 §7.1** — Phase 2 of ADR 0068 (security-policy Atlas surface:
  `ISecurityPolicyAtlasSurface : IAtlasProvider<SecurityPolicyAtlasView>`) gates
  on this amendment reaching `Accepted`. See ADR 0068 §§A0 + §7.1.
- **W#53 Stage 06 hand-off** (`icm/_state/handoffs/helm-identity-atlas-stage06-handoff.md`)
  — Phase 1a builds `IAtlasProvider<T>`; halt-condition HA1 above must be clear
  before Phase 1a PR auto-merges.

### A1.7 — §A0 self-audit (additive)

**Negative-existence (A1 symbol not yet on `origin/main`):**
- `IAtlasProvider<T>` — verified `grep -rn "IAtlasProvider" packages/ui-core/`
  returns zero results on `origin/main` `ca4bbd9`.

**Positive-existence (cited symbols exist):**
- `Sunfish.UICore.Wayfinder` namespace does NOT yet exist on `origin/main`; it is
  created by W#53 Phase 1a build (this amendment authorizes the type before the
  build, per the W#53 hand-off contract at
  `icm/_state/handoffs/helm-identity-atlas-stage06-handoff.md`).
- `IStandingOrderIssuer` — `packages/foundation-wayfinder/IStandingOrderIssuer.cs` ✓
  (ADR 0065, built via W#42 PRs #503–#514).
- `IStandingOrderEventStream` — `packages/foundation-wayfinder/IStandingOrderEventStream.cs` ✓
  (ADR 0065-A1, built via W#42).

**Structural-citation correctness:**
- `Task<TView>` return type + `where TView : class` constraint — aligned to W#53
  hand-off line 89 + 96 (both match).
- Covariance (`out TView`): `TView` is used only in output position (`GetAtlasViewAsync`
  return type) — valid covariant usage. The `where TView : class` constraint makes
  C# accept the variance annotation without compile-time error.
- `ISecurityPolicyAtlasSurface` (ADR 0068) — cited correctly; NOT
  `ISecurityPolicyAtlasProvider` (council SC-1 correction applied).

**Council disposition (this amendment):**
- B-1 resolved: `Task<TView>` non-nullable aligned to W#53 hand-off.
- B-2 resolved: `where TView : class` added.
- B-3 resolved: `IAtlasView` marker dropped (YAGNI — defer to future amendment).
- B-4 resolved: `Sunfish.UICore.Wayfinder` correctly described as not-yet-on-main.
- NM-1 resolved: ProjectReference verification added to A1.5 checklist.
- NM-2 resolved: HA2 dropped (moot — IAtlasView not introduced).
- NM-3 resolved: covariance test aligned to `object` supertype per hand-off.
- SC-1 resolved: `ISecurityPolicyAtlasSurface` corrected in A1.6.
- SC-2 resolved: A1.4 now shows a single-row table (no ambiguity).
