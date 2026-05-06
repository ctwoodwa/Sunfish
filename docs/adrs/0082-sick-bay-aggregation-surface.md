---
id: 82
title: Sick Bay Aggregation Surface + IDC Role
status: Accepted
date: 2026-05-05
tier: ui-core
pipeline_variant: sunfish-feature-change

concern:
  - security
  - audit
  - accessibility
  - identity
  - observability

enables:
  - sick-bay-ui
  - pharmacy-inventory
  - lab-diagnostics
  - atmosphere-monitor
  - medevac-flow
  - first-aid-contextual-help

composes:
  - 46   # key management + recovery (A1 + A2 + W#32 EncryptedField + W#53 KeyFingerprint)
  - 62   # MissionSpace substrate (IDimensionProbe, MissionEnvelope, DegradationKind)
  - 63   # MissionSpace.Requirements (LabDiagnosticResult composes MinimumSpec evaluation)
  - 66   # Helm + Identity Atlas (disambiguate scope; Sick Bay aggregation vs Atlas quick-glance)
  - 68   # Tenant Security Policy (KeyRotationTrigger, MfaEnrollmentPolicy)
  - 77   # Shared Design System (ShipRole, IPermissionResolver, foundation-ship-common)
  - 78   # OOD Watch Rotation (Medevac requires watch-qualified authority; stretcher-bearer scope)
extends: []
supersedes: []
superseded_by: null
deprecated_in_favor_of: null
amendments: [A1]
---

# ADR 0082 — Sick Bay Aggregation Surface + IDC Role

**Status:** Accepted
**Date:** 2026-05-05
**Authors:** XO research session
**Pipeline variant:** `sunfish-feature-change`
**Council posture:** standard adversarial + WCAG/a11y subagent (mandatory — key-fingerprint
display, recovery-contact verification, medevac consent are high-sensitivity accessibility
surfaces) + security-engineering subagent (mandatory — Pharmacy tab touches encrypted-field
metadata; Medevac escalation carries elevated authority; first-aid surface exposes platform
health to any authenticated user)

---

## Context

The W#35 Ship Architecture discovery (§5.5) tagged Sick Bay as **Partial** coverage:
the *substrate* is solid (ADR 0046 key-management + `0046-A1` historical-keys projection +
ADR 0046-A2 `EncryptedField` + W#32 `Foundation.Recovery` built 2026-04-30 +
ADR 0046's `KeyFingerprint` via ADR 0066 / W#53) but the *aggregation UI* that puts
Pharmacy, Lab, and Atmosphere together in a single department view is missing.
The IDC role ("Doc" in Naval parlance) is referenced in W#35 §6.4 but never formally
registered as a `ShipRole` with permission tuples.

**Scope disambiguation with ADR 0066 (Helm + Identity Atlas):**
ADR 0066 defines *quick-glance widgets* for the main Helm dashboard:
`IdentityGlanceWidget`, `ActiveTeamWidget`, `KeyRotationWidget` (phase 2 canonical widgets).
ADR 0082 defines the *full department view* the IDC works in daily:
Pharmacy aggregation (cross-record encrypted-field inventory with rotation status),
Lab diagnostics (dimension-probe history from ADR 0062/0063), Atmosphere monitor
(mission envelope health gauge), Medevac escalation flow, and the first-aid contextual
help baseline that every surface inherits from Sick Bay. These compose rather than overlap.
A user glances at the Helm identity widget for a quick status; the IDC spends their watch
in the full Sick Bay view.

W#35 §7.3 formalizes *stretcher-bearer cross-training*: DCA, MPA, Comms Officer, and
Sonar Officer can be paged for first response. W#35 §7.4 formalizes *first-aid contextual
help*: every user surface inherits an IDC-level baseline contextual help layer. Both need
contracts; this ADR provides them.

---

## Decision drivers

1. **Incident response time.** An IDC responding to an encrypted-field compromise alert
   must navigate to Sick Bay and see rotation status + affected-record count within 30 seconds
   without pivoting to a separate tool. This is a latency requirement on the contract, not
   just the implementation.
2. **IDC role registration.** `ShipRole.IDC` must be registered in `foundation-ship-common`
   (ADR 0077) with authoritative permission tuples covering: view Sick Bay, view Pharmacy,
   manage recovery contacts, trigger key rotation. Without a formal role, RBAC is ad-hoc.
3. **Pharmacy: encrypted-field inventory, not decryption.** Pharmacy shows *metadata only* —
   purpose label, tenant count, rotation status, last-rotated timestamp. It MUST NOT expose
   decrypted field values. `IFieldDecryptor` is FORBIDDEN inside any `ISickBayDataProvider`
   implementation per ADR 0046-A2 §4 — decryption is an audit-emitting operation; read-model
   aggregation is not a legitimate audit-trigger point.
4. **Lab: diagnostic probe history from ADR 0062.** `IDimensionProbe<TDimension>`
   implementations produce `ProbeStatus` + `DegradationKind`; Lab surfaces a per-probe
   history view without re-running probes (read-model only). The probe scheduler lives in
   ADR 0062's `IMissionEnvelopeProvider`; Lab is a consumer.
5. **Atmosphere: mission envelope health gauge.** A single-glance health indicator mapping
   the `MissionEnvelope`'s current degradation profile to a traffic-light / severity roll-up.
   Adapts ADR 0062's `DegradationKind` taxonomy for the Sick Bay UI surface.
6. **Medevac: escalation posture, not wire protocol.** Phase 1 specifies the contract
   (`IMedevacService`) and the state machine (`MedevacState`). The Bridge-side encrypted
   support channel wire protocol is deferred to a follow-on workstream — Phase 1 medevac
   opens a structured ticket via `IChannelProvider` intra-tenant; cross-tenant Bridge
   escalation is Phase 2 scope (see Open Questions).
7. **First-aid baseline.** `IFirstAidSurface` is defined here and registered in DI by
   `AddSunfishSickBay`. Other surface blocks may inject it for contextual hints. This is
   an additive registration; no existing surface is broken.
8. **WCAG 2.2 AA conformance — high-sensitivity surfaces.**
   - Key-fingerprint display: monospace font + grouped chunks + `aria-label` pronunciation
     hint per chunk (`aria-label="fingerprint group 1 of 8: A1 B2"`) — never image-only.
   - Recovery-contact verification: trust decisions MUST NOT rely on color or icon alone
     (SC 1.4.1); verified-status is text-equivalent; verification cadence countdown MUST be
     adjustable (SC 2.2.1) or provide a disable option.
   - Medevac consent dialog: `role="alertdialog"` + `aria-labelledby` + `aria-describedby`
     pointing at the encrypted-channel scope and recipient identity; keyboard-operable.
   - Live regions: Atmosphere status changes + Medevac state transitions MUST announce via
     `aria-live="polite"` (status) or `aria-live="assertive"` (MedevacState.Authorized /
     MedevacState.Complete transitions).
9. **Shared Design System composition.** Sick Bay builds on ADR 0077 `foundation-ship-common`
   for `ShipRole`, `IPermissionResolver`, `PermissionDecision`. No standalone role definitions;
   no standalone permission resolution.
10. **Stretcher-bearer cross-training.** `IStretcherBearerPolicy` registers which `ShipRole`
    values can act as first-responders when the IDC is unavailable. The policy is
    tenant-configurable (max first-responder count floor from ADR 0068 §5.5 `RecoveryContactPolicy`
    provides a precedent for the minimum-floor pattern).

---

## Considered options

### Option A — Sick Bay entirely in `blocks-sick-bay`, no foundation contract

Single package; `blocks-sick-bay` reads directly from `IFieldDecryptor`,
`IFieldEncryptor`, `IMissionEnvelopeProvider`, and ADR 0046's key-management surface.

**Pro:** minimal packages; fast to scaffold.
**Con:** the aggregation data model (Pharmacy inventory, Lab results, Atmosphere readout) is
  embedded in the block, making it untestable without a UI renderer. Bridge's tenant-admin
  surface and the iOS field app cannot reuse the contracts without taking a Blazor-tier
  dependency. The `IFirstAidSurface` registration becomes a block-tier artifact that other
  foundation contracts cannot reference.
**Verdict:** rejected.

### Option B — `foundation-sick-bay` contracts + `blocks-sick-bay` UI **[RECOMMENDED]**

`foundation-sick-bay` owns: observable data model (`PharmacyInventoryEntry`,
`LabDiagnosticResult`, `AtmosphereReadout`, `SickBaySnapshot`); provider/command interfaces
(`ISickBayDataProvider`, `ISickBayCommandService`); first-aid surface contract (`IFirstAidSurface`);
stretcher-bearer policy contract (`IStretcherBearerPolicy`); medevac state machine
(`IMedevacService`, `MedevacState`); new `AuditEventType` constants; new `ShipAction` additions.
`blocks-sick-bay` is the UI block that binds the data to the Shared Design System primitives.

**Pro:** contracts are independently testable; Bridge and iOS can use the same data model;
  `IFirstAidSurface` is at the right tier for cross-surface injection; `ShipAction` additions
  land in the correct foundation package.
**Con:** two packages to scaffold.
**Verdict:** recommended.

### Option C — Extend `foundation-ship-common` (ADR 0077) with Sick Bay contracts

Add `ISickBayDataProvider` and friends to `foundation-ship-common`.

**Pro:** fewer packages.
**Con:** `foundation-ship-common` is about role/permission topology (ShipRole, IPermissionResolver,
  design tokens). Mixing in encrypted-field inventory, probe history, and medevac state
  violates single-responsibility. Packages that only need the permission model would
  transitively depend on the recovery + mission-space substrate.
**Verdict:** rejected.

---

## Decision

**Adopt Option B.** Introduce `Sunfish.Foundation.SickBay` (new package:
`packages/foundation-sick-bay/`) for contracts and `Sunfish.Blocks.SickBay` (new package:
`packages/blocks-sick-bay/`) for the UI block.

### §1 Observable data model

All types in namespace `Sunfish.Foundation.SickBay`:

```csharp
// ── Pharmacy ──────────────────────────────────────────────────────────────────
// Metadata view of an encrypted-field purpose. No field values or raw ciphertext exposed.
// TenantRecordCount uses a k-anonymity floor: values 1–2 are reported as "<3" to prevent
// tenant data-composition inference via record-count correlation.
public sealed record PharmacyInventoryEntry
{
    public required string          FieldPurpose         { get; init; }  // ADR 0046-A2 purpose label
    public required string          FriendlyName         { get; init; }  // display-safe; no PII
    // Record count uses k=3 floor: values in [1,2] are suppressed (see §Trust impact).
    // Implementations MUST NOT expose raw counts below this threshold.
    public required PharmacyRecordCount RecordCount      { get; init; }
    public required NodaTime.Instant LastRotatedAt       { get; init; }
    public required RotationHealth  RotationStatus       { get; init; }
    // Phase 2 addition (requires ADR 0068 Status: Accepted; halt-condition H3):
    // public required string? PendingTriggerLabel { get; init; }
    public required bool            HasCompromiseFlag    { get; init; }
}

/// <summary>
/// Record count with k-anonymity floor. Prevents tenant-composition inference
/// from encrypted-field record counts.
/// </summary>
public sealed record PharmacyRecordCount
{
    public static readonly PharmacyRecordCount Suppressed = new(null, true);
    public static PharmacyRecordCount Exact(int count) =>
        count < 3 ? Suppressed : new(count, false);

    private PharmacyRecordCount(int? value, bool suppressed)
    { Value = value; IsSuppressed = suppressed; }

    public int?  Value       { get; }   // null when IsSuppressed
    public bool  IsSuppressed { get; }  // "< 3" when displayed
}

public enum RotationHealth { Current, RotationDue, RotationOverdue, Compromised }

// ── Lab ───────────────────────────────────────────────────────────────────────
// Read-model view of a single probe run (one per IDimensionProbe<T> implementation).
// Probe execution lives in IMissionEnvelopeProvider (ADR 0062); Lab is a consumer only.
public sealed record LabDiagnosticResult
{
    public required string           ProbeName   { get; init; }
    public required string           DimensionId { get; init; }  // kebab-case; e.g., "hardware"
    public required ProbeStatus      Status      { get; init; }  // from ADR 0062
    public required DegradationKind  Degradation { get; init; }  // from ADR 0062
    public required NodaTime.Instant LastRunAt   { get; init; }
    public required string?          DiagnosticDetail { get; init; }  // plain text; no HTML
}

// ── Atmosphere ────────────────────────────────────────────────────────────────
// Roll-up of the current MissionEnvelope health state (ADR 0062).
public sealed record AtmosphereReadout
{
    public required AtmosphereHealth        OverallHealth     { get; init; }
    public required int                     WarningProbeCount  { get; init; }
    public required int                     CriticalProbeCount { get; init; }
    public required bool                    ForceEnableActive  { get; init; }
    public required NodaTime.Instant        CapturedAt         { get; init; }
}

// ADR 0082-A1: Unknown added as zero-value sentinel — see §A1
public enum AtmosphereHealth { Unknown, Green, Yellow, Orange, Red }

// ── Snapshot ──────────────────────────────────────────────────────────────────
// Full Sick Bay view — aggregates all three departments + medevac state.
// Uses lists (not fixed tuples) to avoid binary-compat breaks if departments expand.
public sealed record SickBaySnapshot
{
    public required IReadOnlyList<PharmacyInventoryEntry> Pharmacy    { get; init; }
    public required IReadOnlyList<LabDiagnosticResult>   Lab          { get; init; }
    public required AtmosphereReadout                    Atmosphere   { get; init; }
    public required MedevacState                         MedevacState { get; init; }
    public required NodaTime.Instant                     CapturedAt   { get; init; }
}

// ── Medevac ───────────────────────────────────────────────────────────────────
public enum MedevacState { Idle, Requested, PendingAuthorization, Authorized, InProgress, Complete }
```

### §2 Provider and command interfaces

```csharp
namespace Sunfish.Foundation.SickBay;

public interface ISickBayDataProvider
{
    /// <summary>
    /// Returns a single Sick Bay snapshot. Must be side-effect-free; projection only.
    /// Implementations MUST NOT invoke IFieldDecryptor (ADR 0046-A2 §4 — decryption is
    /// audit-emitting; read-model aggregation is not a legitimate audit-trigger point).
    /// Implementations MUST honor <paramref name="ct"/>. Recommended completion: ≤2s.
    /// If a department (Pharmacy / Lab / Atmosphere) exceeds its budget, the implementation
    /// MUST return a partial snapshot with an empty list for that department rather than
    /// throwing — degraded availability is preferred over unavailability.
    /// </summary>
    Task<SickBaySnapshot> GetSnapshotAsync(TenantId tenant, CancellationToken ct = default);

    /// <summary>
    /// Streams Sick Bay snapshots as underlying data changes.
    /// Push-based; implementations SHOULD use IStandingOrderEventStream (ADR 0065-A1)
    /// and IMissionEnvelopeObserver (ADR 0062) where available; fall back to polling
    /// (interval configured via <c>SickBayOptions.FallbackPollingInterval</c>, default 60s)
    /// until both event streams ship.
    /// </summary>
    IAsyncEnumerable<SickBaySnapshot> SubscribeSnapshotAsync(TenantId tenant,
                                                              CancellationToken ct = default);
}

/// <summary>
/// Key-rotation scheduling command surface. Separated from medevac to isolate
/// crypto-adjacent commands (higher authority) from escalation commands.
/// </summary>
public interface ISickBayCommandService
{
    /// <summary>
    /// Schedules a key rotation for <paramref name="fieldPurpose"/>.
    /// Emits <c>AuditEventType.SickBayKeyRotationTriggered</c> before the operation proceeds.
    /// Requires ShipAction.TriggerKeyRotation permission (Captain; System for EmergencyOverride).
    /// Phase 2 addition: triggerReason will become typed <c>KeyRotationTrigger</c> after
    /// ADR 0068 reaches Status: Accepted (halt-condition H3).
    /// </summary>
    Task TriggerKeyRotationAsync(TenantId tenant, string fieldPurpose,
                                 string triggerReason, CancellationToken ct = default);
}

/// <summary>
/// Medevac state machine. Manages escalation lifecycle separately from crypto commands.
///
/// <para><strong>State-transition table (valid transitions only):</strong></para>
/// <para>
/// Idle → Requested (RequestAsync; IDC or Captain)<br/>
/// Requested → PendingAuthorization (system; auto-transition on receipt by Captain's client)<br/>
/// PendingAuthorization → Authorized (AuthorizeAsync; Captain; four-eyes: authorizer ≠ requester)<br/>
/// PendingAuthorization → Cancelled (CancelAsync; IDC or Captain)<br/>
/// Authorized → InProgress (system; auto-transition on escalation channel open)<br/>
/// InProgress → Complete (CompleteAsync; IDC or Captain)<br/>
/// InProgress → Cancelled (CancelAsync; Captain only after InProgress)<br/>
/// Any → Idle (system reset after Complete or Cancelled, post-cooldown)
/// </para>
/// <para>Invalid transitions MUST throw <c>InvalidOperationException</c> with the
/// attempted transition in the message. No silent no-ops.</para>
/// </summary>
public interface IMedevacService
{
    Task<MedevacState> GetStateAsync(TenantId tenant, CancellationToken ct = default);

    /// <summary>
    /// Transitions Idle → Requested. Emits SickBayMedevacInitiated before the transition.
    /// Stores <paramref name="requestedBy"/> for four-eyes enforcement.
    /// Requires ShipAction.InitiateMedevac (IDC or Captain).
    /// </summary>
    Task RequestAsync(TenantId tenant, PrincipalId requestedBy, string reason,
                      CancellationToken ct = default);

    /// <summary>
    /// Transitions PendingAuthorization → Authorized.
    /// Emits SickBayMedevacAuthorized before the transition.
    /// Requires ShipAction.AuthorizeMedevac (Captain only).
    /// MUST reject if <paramref name="authorizingPrincipal"/> == the RequestedBy principal
    /// recorded on the pending request. On self-approval attempt, emits
    /// SickBayMedevacSelfApprovalRejected and throws <c>InvalidOperationException</c>.
    /// </summary>
    Task AuthorizeAsync(TenantId tenant, PrincipalId authorizingPrincipal,
                        CancellationToken ct = default);

    /// <summary>
    /// Cancels from Requested, PendingAuthorization, or InProgress (Captain only for InProgress).
    /// Emits SickBayMedevacCancelled before the transition.
    /// </summary>
    Task CancelAsync(TenantId tenant, PrincipalId cancellingPrincipal,
                     CancellationToken ct = default);

    /// <summary>
    /// Transitions InProgress → Complete.
    /// Emits SickBayMedevacCompleted before the transition.
    /// Requires ShipAction.InitiateMedevac (IDC or Captain).
    /// </summary>
    Task CompleteAsync(TenantId tenant, CancellationToken ct = default);
}
```

### §3 First-aid contextual help surface

```csharp
namespace Sunfish.Foundation.SickBay;

/// <summary>
/// Provides contextual help hints for a given UI surface key.
/// Registered by AddSunfishSickBay(); other blocks inject this interface to surface
/// IDC-level help annotations alongside their own UI (formalizes W#35 §7.4).
/// </summary>
public interface IFirstAidSurface
{
    /// <param name="surfaceKey">
    /// Kebab-case surface identifier (e.g., "pharmacy", "engine-room", "quarterdeck").
    /// Unknown keys return an empty list; they do not throw.
    /// </param>
    Task<IReadOnlyList<FirstAidHint>> GetContextualHintsAsync(string surfaceKey,
                                                               CancellationToken ct = default);
}

public sealed record FirstAidHint
{
    public required string        Key      { get; init; }  // stable identifier for tests
    public required string        Title    { get; init; }  // displayed in disclosure header
    /// <summary>
    /// Plain text only. MUST NOT contain HTML tags, markdown, or script content.
    /// Constructor rejects strings containing <c>&lt;</c>, <c>&gt;</c>, <c>&amp;</c>,
    /// or ASCII control characters (< 0x20 except LF). Legitimate comparators like
    /// "temp > 100" should be rephrased as "temp exceeds 100".
    /// </summary>
    /// <remarks>
    /// <para><strong>Rendering contract:</strong> Blazor renderers MUST bind this value
    /// using text-interpolation (<c>@hint.Body</c>), NEVER as
    /// <c>@((MarkupString)hint.Body)</c>. Violating this contract is an XSS vector
    /// regardless of the constructor validation.</para>
    /// </remarks>
    public required string        Body     { get; init; }
    public required FirstAidLevel Level    { get; init; }
}

public enum FirstAidLevel { Info, Caution, Warning }
```

### §4 Stretcher-bearer policy

```csharp
namespace Sunfish.Foundation.SickBay;

/// <summary>
/// Constrained subset of ShipRole values eligible as stretcher-bearers.
/// Excludes command roles (Captain, XO, IDC itself) to prevent role-escalation
/// via IStretcherBearerPolicy implementations.
/// </summary>
public enum StretcherBearerRole { DCA, MPA, CommsOfficer, SonarOfficer }

/// <summary>
/// Determines which non-command roles may act as first-responders when the IDC is
/// unavailable (formalizes W#35 §7.3). Returns <c>StretcherBearerRole</c> — a
/// constrained subset of ShipRole — so implementations CANNOT escalate arbitrary
/// command roles (Captain, XO) to first-responder status via this interface.
/// Captain and XO are ALWAYS first-responder eligible and are NOT in this list;
/// they are handled by the permission layer, not this policy.
/// This list MUST NOT be consumed for permission/authority decisions — only for
/// notification routing and display.
/// </summary>
public interface IStretcherBearerPolicy
{
    Task<IReadOnlyList<StretcherBearerRole>> GetEligibleRespondersAsync(
        TenantId tenant, CancellationToken ct = default);
}
```

Default eligible roles (registered in `DefaultStretcherBearerPolicy`):
all four `StretcherBearerRole` values (`DCA`, `MPA`, `CommsOfficer`, `SonarOfficer`).
`ShipRole.Captain` + `ShipRole.XO` are implicitly eligible (no separate configuration).

### §5 ShipRole registration

`ShipRole.IDC` MUST be added to `foundation-ship-common` `ShipRole` enum during Phase 1
(additive; binary-compat safe). Permission tuples registered via `IPermissionResolver`:

| ShipAction | Minimum role | Notes |
|---|---|---|
| `ViewSickBay` | `IDC`, `Captain`, `XO` | Read-only snapshot; Pharmacy metadata visible |
| `ViewPharmacy` | `IDC` only | Pharmacy tab isolates encrypted-field metadata |
| `ManageRecoveryContacts` | `IDC`, `Captain` | Add/remove/verify recovery contacts (ADR 0046) |
| `TriggerKeyRotation` | `Captain` | Non-emergency; `System` for emergency override |
| `InitiateMedevac` | `IDC`, `Captain` | Request medevac escalation; stores `RequestedBy` |
| `AuthorizeMedevac` | `Captain` only | **Four-eyes mandatory:** authorizer MUST NOT be the same principal as `RequestedBy`. Implementations MUST check this before transitioning state. |
| `ViewFirstAid` | all authenticated roles | Contextual help is always visible |

**`ShipRole.IDC` + `ShipRole` exhaustive-switch caveat:** Adding `IDC` is binary-compat safe
at the binary level, but consumers using exhaustive `switch` expressions (C# 8+) will get
compiler warning CS8509 / error (if `TreatWarningsAsErrors`) on unhandled cases. The
`foundation-ship-common` changelog MUST document this and provide a default case pattern
for existing `switch` sites in ADR 0078 (OOD) and ADR 0079 (Engine Room) blocks.

### §6 AuditEventType constants

Added as `static readonly` fields on `Sunfish.Kernel.Audit.AuditEventType`
(per ADR 0049 §3 protocol; no enum; additive; binary-compat safe):

```csharp
// Pharmacy
public static readonly AuditEventType SickBayPharmacyViewed              = new("sick-bay.pharmacy.viewed");
public static readonly AuditEventType SickBayKeyRotationTriggered         = new("sick-bay.key-rotation.triggered");
// Lab
public static readonly AuditEventType SickBayLabDiagnosticViewed          = new("sick-bay.lab.viewed");
// Atmosphere
public static readonly AuditEventType SickBayAtmosphereViewed             = new("sick-bay.atmosphere.viewed");
// Medevac — one event per valid state transition
// Emitters: IMedevacService.RequestAsync / AuthorizeAsync / CancelAsync / CompleteAsync
public static readonly AuditEventType SickBayMedevacInitiated             = new("sick-bay.medevac.initiated");
public static readonly AuditEventType SickBayMedevacAuthorized            = new("sick-bay.medevac.authorized");
public static readonly AuditEventType SickBayMedevacCancelled             = new("sick-bay.medevac.cancelled");
public static readonly AuditEventType SickBayMedevacCompleted             = new("sick-bay.medevac.completed");
// Emitted by IMedevacService.AuthorizeAsync on self-approval attempt (four-eyes violation)
public static readonly AuditEventType SickBayMedevacSelfApprovalRejected  = new("sick-bay.medevac.self-approval-rejected");
// Recovery contacts (additive to ADR 0046 existing constants)
public static readonly AuditEventType SickBayRecoveryContactManaged       = new("sick-bay.recovery-contact.managed");
```

11 total `AuditEventType` constants.
`IFirstAidSurface.GetContextualHintsAsync` is intentionally unaudited (read-only,
no PII exposure, no state change). Explicitly called out as acceptable in §Trust impact.

### §7 DI registration

```csharp
// Extension method in Sunfish.Foundation.SickBay
public static class SickBayServiceCollectionExtensions
{
    /// <summary>
    /// Registers ISickBayDataProvider, ISickBayCommandService, IFirstAidSurface,
    /// IStretcherBearerPolicy, and IMedevacService in the DI container.
    /// </summary>
    public static IServiceCollection AddSunfishSickBay(
        this IServiceCollection services,
        Action<SickBayOptions>? configure = null)
    { ... }
}

public sealed class SickBayOptions
{
    /// <summary>
    /// Named encrypted-field purposes surfaced in the Pharmacy tab, keyed by
    /// ADR 0046-A2 purpose label → friendly display name. Only registered purposes appear.
    /// Register at startup via <c>options.RegisterPurpose("ssn", "Social Security Number")</c>.
    /// This avoids a runtime scan of EF DbContext column metadata (which would
    /// require a persistence-tier dependency in the foundation package).
    /// </summary>
    public IDictionary<string, string> RegisteredFieldPurposes { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Registers a purpose label with its UI-friendly display name.</summary>
    public SickBayOptions RegisterPurpose(string purposeLabel, string friendlyName)
    {
        RegisteredFieldPurposes[purposeLabel] = friendlyName;
        return this;
    }

    /// <summary>
    /// Override the default 60s polling interval for SubscribeSnapshotAsync when
    /// IStandingOrderEventStream is unavailable (ADR 0065-A1 prerequisite).
    /// </summary>
    public TimeSpan FallbackPollingInterval { get; set; } = TimeSpan.FromSeconds(60);
}
```

### §8 WCAG 2.2 AA conformance contract

The following WCAG 2.2 / EN 301 549 requirements are load-bearing; implementations MUST
NOT ship until each is verified by the WCAG/a11y council subagent:

| § | Criterion | Surface | Requirement |
|---|---|---|---|
| SC 1.3.1 | Info and Relationships | RotationHealth badge → row; fingerprint chunk grouping; AtmosphereHealth gauge → probe counts | Programmatically determinable structure for all badge-row associations and chunk groupings |
| SC 1.4.1 | Use of Color | All Sick Bay tabs | Trust, risk, and health status MUST convey meaning via text (or pattern + text) in addition to color |
| SC 1.4.3 | Contrast (Min) | RotationHealth + AtmosphereHealth badges | ≥4.5:1 for text; ≥3:1 for large text against ADR 0077 design tokens |
| SC 2.1.1 | Keyboard | Key-fingerprint display; Medevac dialog | Full keyboard operability; no mouse-only interactions |
| SC 2.2.1 | Timing Adjustable | Recovery-contact verification cadence countdown | Countdown MUST be extendable to ≥10× the default duration, OR disableable entirely |
| SC 2.4.3 | Focus Order | Pharmacy/Lab/Atmosphere tab navigation | Deterministic focus order on tab activation; keyboard focus lands in new tab content on selection |
| SC 2.4.7 | Focus Visible | Key-fingerprint copy-to-clipboard button; tab controls | Visible focus indicator on all interactive elements |
| SC 3.3.1 | Error Identification | Key-rotation failure | Error state identified in text; not color-only |
| SC 3.3.4 | Error Prevention | Medevac consent dialog | Confirmation required; reversible within state machine (cancel before Authorized) |
| SC 3.3.8 | Accessible Authentication (by analogy) | Recovery-contact verification UX | SC 3.3.8 applies by analogy: recovery-contact enrollment is a step-up authentication mechanism. Copy-paste assistance MUST be enabled; verification MUST NOT require real-time input under time pressure. |
| SC 4.1.3 | Status Messages | Atmosphere + Medevac live regions | Status updates use `aria-live="polite"` (all status + Medevac state transitions including Authorized and Complete). Reserve `aria-live="assertive"` ONLY for unsolicited critical escalations (Atmosphere → Red; Medevac auto-cancelled by system). |

### §9 Phase delivery plan

| Phase | Scope | ~Duration | Key deliverables |
|---|---|---|---|
| 1 | `foundation-sick-bay` scaffold + contracts | ~4-5h / 1 PR | Package scaffold + §1-§4 types + `IMedevacService` + `IKeyRotationScheduler` + §5 `ShipRole.IDC` + §6 `AuditEventType` + §7 DI extension stub |
| 2 | Reference implementations + `DefaultStretcherBearerPolicy` | ~4-5h / 1 PR | `SickBayDataProvider` from `SickBayOptions.RegisteredFieldPurposes` + `IMissionEnvelopeProvider`; `DefaultStretcherBearerPolicy`; `IFirstAidSurface` hint library; pre-merge security council mandatory |
| 3a | `blocks-sick-bay` Blazor block (Pharmacy + Lab + Atmosphere) | ~4-5h / 1 PR | Blazor Razor components + WCAG §8 contracts verified; WCAG/a11y council subagent mandatory |
| 3b | `ISickBayCommandService` + `IMedevacService` implementation | ~3-4h / 1 PR | `SickBayCommandService` + `MedevacOrchestrator` (intra-tenant Phase 1); four-eyes enforcement; security-engineering council mandatory |
| 4 | Anchor + Bridge rendering wiring + apps/docs | ~3-4h / 1 PR | `AddSunfishSickBay()` in accelerators; `apps/docs/blocks/sick-bay/overview.md` |
| 5 | Ledger flip + memory | ~30min | W#54 row `ready-to-build` → `built` |

---

## Consequences

### Positive

- IDC role is formally registered; RBAC for Sick Bay surfaces is consistent with the rest
  of the ship-architecture cohort.
- Pharmacy surface makes encrypted-field rotation status actionable without requiring
  IDC to navigate to individual records.
- First-aid contextual help baseline is available to all blocks via DI — each surface
  only needs to declare its surface key; Sick Bay maintains the hint library.
- Medevac escalation path is formally specified; Phase 1 wires intra-tenant;
  Phase 2 (Bridge cross-tenant escalation) is a clean extension.
- Stretcher-bearer cross-training is machine-checkable: `IStretcherBearerPolicy` produces
  a deterministic list of eligible first-responders.

### Negative

- Two new packages to scaffold, CI-wire, and document.
- `SickBayDataProvider` joins a potentially long list of implementations that each tenant
  must configure. Mitigation: `AddSunfishSickBay()` registers sensible defaults with
  convention-based scanning.
- `IFirstAidSurface.GetContextualHintsAsync("unknown-key")` returns empty list silently;
  surface authors must test their key strings match the hint library. Convention: key string
  testing is part of Phase 3a acceptance criteria.

### Trust impact / Security & privacy

- **Atmosphere is host-scoped, not tenant-scoped.** `AtmosphereReadout` reflects the host
  process's Mission Envelope. In multi-tenant hosted deployments (Bridge / Zone C), ALL tenants
  sharing the host see the same Atmosphere readout. UI surfaces MUST label scope explicitly per
  §A1.5; back-end consumers MUST NOT cache Atmosphere by tenant key (any tenant cache flush would
  not invalidate cross-tenant readouts).
- **Pharmacy read-model posture.** `ISickBayDataProvider` MUST NOT call `IFieldDecryptor`.
  The Pharmacy inventory shows metadata (purpose, count, rotation status) only.
  Any implementation that exposes decrypted field values or raw ciphertext is a
  `Critical` council finding. This constraint is verified in Phase 2's security-engineering
  council.
- **Medevac authorization chain.** `AuthorizeMedevacAsync` requires `ShipAction.AuthorizeMedevac`
  permission which is `Captain` only. The service MUST verify permission via
  `IPermissionResolver` before emitting the `SickBayMedevacAuthorized` audit event.
  An authorization that succeeds without a pre-op audit record is a compliance gap.
- **First-aid hint body is plain text.** `FirstAidHint.Body` MUST be plain text; no HTML,
  no markdown. Implementations that render this as HTML without sanitization are an XSS
  vector. The type constraint is string; validation enforced in `FirstAidHint` constructor
  (reject strings that contain `<` / `>`).
- **`ShipAction.ViewPharmacy` isolation.** Pharmacy metadata is only visible to `IDC` role.
  The `IPermissionResolver` check MUST occur before `ISickBayDataProvider.GetSnapshotAsync`
  returns — the data provider itself is not permission-aware (per ADR 0077 §3 posture).

---

## Compatibility plan

No existing packages are modified beyond additive changes:

| Package | Action |
|---|---|
| `packages/foundation-sick-bay/` | NEW — contracts only; no behavioral change to existing packages |
| `packages/blocks-sick-bay/` | NEW — UI block; registered in Anchor + Bridge by default |
| `packages/foundation-ship-common/` | ADDITIVE — `ShipRole.IDC` enum value + 6 `ShipAction` additions + 9 `AuditEventType` constants (all binary-compat safe) |
| `accelerators/anchor/` | MauiProgram.cs addition: `services.AddSunfishSickBay(...)` |
| `accelerators/bridge/` | Program.cs addition: `services.AddSunfishSickBay(...)` (Phase 4) |

---

## Implementation checklist

**Phase 1 — `foundation-sick-bay` scaffold + contracts**

- [ ] Scaffold `packages/foundation-sick-bay/Sunfish.Foundation.SickBay.csproj` — IsPackable,
  deps on `foundation`, `foundation-ship-common`, `foundation-recovery`, `foundation-wayfinder`
- [ ] Implement §1 observable data model:
  `PharmacyInventoryEntry`, `RotationHealth`, `LabDiagnosticResult`, `AtmosphereReadout`,
  `AtmosphereHealth`, `SickBaySnapshot`, `MedevacState`, `FirstAidHint`, `FirstAidLevel`
- [ ] Implement §2 provider interfaces: `ISickBayDataProvider`, `ISickBayCommandService`
- [ ] Implement §3 first-aid surface: `IFirstAidSurface` (interface + `FirstAidHint` validation)
- [ ] Implement §4 stretcher-bearer policy: `IStretcherBearerPolicy` + `StretcherBearerRole` enum
- [ ] `IMedevacService` is declared in §2 — no duplicate contract block needed here;
  implement the full interface from §2 (state-transition table + four-eyes invariants)
- [ ] Introduce `IKeyRotationScheduler` new contract in `foundation-sick-bay`:

  ```csharp
  /// <summary>
  /// Schedules key rotation for a field purpose. Abstraction layer between
  /// ISickBayCommandService and the actual key-rotation substrate (W#32 / ADR 0046-A2).
  /// Phase 2 wires to the real rotation infrastructure; Phase 1 stubs return Task.CompletedTask.
  /// </summary>
  public interface IKeyRotationScheduler
  {
      Task ScheduleAsync(TenantId tenant, string fieldPurpose,
                         string triggerReason, CancellationToken ct = default);
  }
  ```

- [ ] Add `ShipRole.IDC` to `foundation-ship-common/ShipRole.cs`
  (note: exhaustive-switch warning caveat — see §5 note)
- [ ] Add 6 `ShipAction` constants to `foundation-ship-common`
- [ ] Add 11 `AuditEventType` constants to `kernel-audit`
- [ ] Implement `AddSunfishSickBay()` DI extension (stubs only for Phase 1; full impl Phase 2)
- [ ] Unit tests: `PharmacyRecordCount` k-anonymity floor (1,2 → suppressed; 3+ → exact);
  `FirstAidHint` constructor rejects HTML chars (`<`, `>`, `&`, control chars);
  `SickBaySnapshot` factory builder;
  `IMedevacService` valid transitions + invalid-transition throws;
  `IMedevacService.AuthorizeAsync` self-approval rejection
- [ ] Pre-merge council: standard adversarial (4 perspectives)

**Phase 2 — Reference implementations + DefaultStretcherBearerPolicy**

- [ ] Implement `SickBayDataProvider : ISickBayDataProvider` in `blocks-sick-bay/`
  - Pharmacy: read `SickBayOptions.RegisteredFieldPurposes` for purpose labels/names;
    derive `RotationHealth` from `LastRotatedAt` + configurable rotation-due threshold;
    derive `RecordCount` via `PharmacyRecordCount.Exact(count)` (k-anonymity floor applied)
  - Lab: query `IMissionEnvelopeProvider.GetCurrentEnvelope(tenant)`;
    derive `LabDiagnosticResult` per probe from envelope's dimension status
  - Atmosphere: map `DegradationKind` counts from `MissionEnvelope` to `AtmosphereHealth`
  - `SubscribeSnapshotAsync`: use `IMissionEnvelopeObserver` (ADR 0062) + polling fallback
  - FORBIDDEN: no `IFieldDecryptor` call anywhere in this class — verified in test
    (`[Fact] SickBayDataProvider_DoesNotReference_IFieldDecryptor()` using reflection)
- [ ] Implement `DefaultStretcherBearerPolicy` (all four `StretcherBearerRole` values)
- [ ] Implement `IFirstAidSurface` with initial hint library (≥5 hints across
  "pharmacy", "lab", "atmosphere" surface keys)
- [ ] Stub `IKeyRotationScheduler` implementation (returns `Task.CompletedTask` in Phase 1;
  full implementation wired to W#32 rotation substrate in Phase 3b)
- [ ] Pre-merge council: **security-engineering subagent mandatory** (verify no decryption path;
  audit emission pre-op; k-anonymity floor verified)

**Phase 3a — `blocks-sick-bay` Blazor UI**

- [ ] Scaffold `packages/blocks-sick-bay/Sunfish.Blocks.SickBay.csproj` — deps on
  `foundation-sick-bay`, `foundation-ship-common`, `ui-core`
- [ ] Implement `SickBayBlock.razor` (root; tabbed navigation: Pharmacy / Lab / Atmosphere)
- [ ] Implement `PharmacyTabContent.razor` — inventory list; `RotationHealth` badge (color +
  text + icon triple-encoding per SC 1.4.1); fingerprint display component
  (`<span aria-label="fingerprint group N of 8: XX YY">`) per §8
- [ ] Implement `LabTabContent.razor` — probe-history table; SC 1.4.3 contrast verified;
  no chart without `<table>` data-alternative
- [ ] Implement `AtmosphereTabContent.razor` — health gauge; `aria-live="polite"` on status
  updates; `aria-live="assertive"` on severity escalation to Red
- [ ] Implement `MedevacDialog.razor` — `role="alertdialog"`; `aria-labelledby` + `aria-describedby`;
  confirm + cancel controls; keyboard-operable per SC 2.1.1
- [ ] Implement `KeyFingerprintDisplay.razor` — monospace; grouped chunks; `aria-label` per chunk;
  copy-to-clipboard with visible focus indicator
- [ ] Unit tests: tab navigation; permission gating (Pharmacy tab hidden when role ≠ IDC);
  `aria-live` region assertions; Medevac dialog state machine
- [ ] Pre-merge council: **WCAG/a11y subagent mandatory** (SC 1.4.1, SC 1.4.3, SC 2.1.1,
  SC 2.2.1, SC 3.3.1, SC 3.3.4, SC 3.3.8, SC 4.1.3)

**Phase 3b — `ISickBayCommandService` + Medevac orchestration**

- [ ] Implement `SickBayCommandService : ISickBayCommandService`
  - `TriggerKeyRotationAsync`: emit `SickBayKeyRotationTriggered` BEFORE calling
    `IKeyRotationScheduler.ScheduleAsync` (audit-before-operation invariant)
- [ ] Implement `MedevacServiceImpl : IMedevacService`
  - `RequestAsync` / `AuthorizeAsync` / `CancelAsync` / `CompleteAsync`:
    each must emit their respective `AuditEventType` pre-op;
    `AuthorizeAsync` MUST reject if `authorizingPrincipal == requestedBy`
    (four-eyes invariant; emit `SickBayMedevacSelfApprovalRejected` + throw)
- [ ] Implement `MedevacOrchestrator` — manages `MedevacState` transitions;
  Phase 1: intra-tenant notification only (no Bridge wire protocol);
  Phase 2 hook: `IMedevacEscalationStrategy` for future Bridge extension (interface deferred)
- [ ] Unit tests: command audit pre-emission order; Medevac state machine transitions;
  invalid-state rejection (cancel when Complete must throw)
- [ ] Pre-merge council: **security-engineering subagent mandatory**

**Phase 4 — Anchor + Bridge wiring + apps/docs**

- [ ] Wire `services.AddSunfishSickBay(...)` in `accelerators/anchor/MauiProgram.cs`
- [ ] Wire `services.AddSunfishSickBay(...)` in `accelerators/bridge/Program.cs`
- [ ] Kitchen-sink demo: Sick Bay tab in Anchor demo shell
- [ ] `apps/docs/blocks/sick-bay/overview.md`
- [ ] `apps/docs/foundation/sick-bay/overview.md`

**Phase 5 — Ledger flip + close**

- [ ] Update `icm/_state/active-workstreams.md`: W#54 row → `built`
- [ ] Write XO project memory update

---

## Open questions

1. **Medevac Phase 2 wire protocol (Bridge encrypted support channel).** Phase 1 medevac is
   an intra-tenant notification + state machine only. Phase 2 needs a cross-tenant Bridge
   side: a support-request queue + encrypted channel between Anchor IDC and Bridge admin.
   The `foundation-channels` (ADR 0076) `IChannelProvider` is intra-tenant only.
   **Decision deferred.** A follow-on workstream (W#56?) will specify the Bridge medevac
   wire protocol. Phase 1 MedevacOrchestrator includes an `IMedevacEscalationStrategy`
   hook point for Phase 2 to plug into.

2. **`IStretcherBearerPolicy` tenant override.** Phase 1 ships `DefaultStretcherBearerPolicy`
   with hard-coded role defaults. Should tenants be able to override which roles are
   stretcher-bearer eligible via a Standing Order (ADR 0065)? **Decision deferred.**
   Phase 1 default is appropriate for the demo; Standing Order integration is a natural
   Phase 2 extension that doesn't block Phase 1.

3. **`IFirstAidSurface` hint authoring UX.** The hint library in Phase 1 is hardcoded
   (static C# collection). Long-term, IDCs should be able to author custom hints via a
   Ship's Office document (ADR 0083 scope). **Decision deferred.**
   Phase 1 hardcoded hints are sufficient for demo; ADR 0083 Ship's Office authoring surface
   will add a Standing Order–driven hint override.

---

## Revisit triggers

- ADR 0065-A1 `IStandingOrderEventStream` ships → remove polling fallback in
  `SickBayDataProvider.SubscribeSnapshotAsync`; wire to event-stream push.
- ADR 0076 `foundation-channels` extends to cross-tenant scope → wire Medevac Phase 2
  Bridge escalation via `IChannelProvider`.
- ADR 0068 `KeyRotationPolicy` thresholds change → review `RotationHealth` derivation
  logic in `SickBayDataProvider`.
- ADR 0083 Ship's Office ships → wire `IFirstAidSurface` hint library to Ship's Office
  document authoring surface.
- `ShipRole` enum exceeds 32 values → evaluate `[Flags]` pattern change for
  `IStretcherBearerPolicy.GetEligibleRespondersAsync` permission set.

---

## References

### Predecessor and sister ADRs

- [ADR 0046](./0046-key-management-and-recovery.md) — key management; `PeerId` Ed25519 lifecycle
- [ADR 0046-A1](./0046-a1-historical-keys-projection.md) — historical-keys projection
- [ADR 0062](./0062-mission-space-negotiation-protocol.md) — `IMissionEnvelopeProvider` +
  `IDimensionProbe<T>` + `DegradationKind` + `ProbeStatus` — Sick Bay Lab + Atmosphere compose these
- [ADR 0063](./0063-mission-space-requirements.md) — `IMinimumSpecResolver` — Lab surfaces
  MinimumSpec evaluation results
- [ADR 0065](./0065-wayfinder-system-and-standing-order-contract.md) — `IStandingOrderIssuer` —
  key-rotation standing orders reference this substrate
- [ADR 0066](./0066-helm-composition-and-identity-atlas-surface.md) — `IIdentityAtlasSurface` +
  `KeyFingerprint` + recovery-contact view-models; disambiguated with ADR 0082 in §Context
- [ADR 0068](./0068-tenant-security-policy.md) — `KeyRotationTrigger` + `KeyRotationPolicy`
  thresholds + `MfaEnrollmentPolicy` + `RecoveryContactPolicy`
- [ADR 0077](./0077-shared-design-system.md) — `ShipRole` + `IPermissionResolver` + design tokens
- [ADR 0078](./0078-ood-watch-rotation.md) — `IOodWatchService`; referenced in Medevac
  watch-authority check

### Intake + discovery

- Sick Bay intake: `icm/00_intake/output/2026-05-01_sick-bay-aggregation-intake.md`
- W#35 Ship Architecture discovery: `icm/01_discovery/output/2026-05-01_ship-architecture.md`
  §5.5 (Sick Bay coverage), §6.4 (IDC role), §7.3 (stretcher-bearer), §7.4 (first-aid baseline),
  §8.6 (WCAG concerns for credential + health surfaces)

---

## Pre-acceptance audit

- [x] **AHA pass.** Options A (block-only) and C (extend foundation-ship-common) considered
  and rejected above. Option B (two-package split) is the correct tier-discipline choice.
- [x] **FAILED conditions.** Kill trigger: if `IFieldDecryptor` is invoked inside any
  `ISickBayDataProvider` implementation, halt and redesign the Pharmacy read-model.
  Kill trigger: if Medevac consent dialog fails keyboard operability (SC 2.1.1) in Phase 3a
  council review, halt Phase 3b until 3a passes.
- [x] **Rollback strategy.** `foundation-sick-bay` and `blocks-sick-bay` are new packages;
  rollback = remove packages + revert `MauiProgram.cs` / `Program.cs` additions.
  `ShipRole.IDC` + `ShipAction` + `AuditEventType` additions are additive to
  `foundation-ship-common` and `kernel-audit`; rollback = remove those constants.
  No existing packages modified beyond these additive changes.
- [x] **Confidence level.** HIGH for Phases 1–2 (substrate types well-understood from ADR
  0046 + ADR 0062 cohort). MEDIUM for Phase 3 (Blazor accessibility debt identified in
  W#35 §8.6 requires WCAG/a11y council at every UI phase; Medevac dialog state machine is
  novel). Phase 2 Bridge medevac escalation is explicitly deferred.
- [x] **Cited-symbol verification.**
  - `KeyRotationTrigger` — introduced by ADR 0068 (PR #584 pending); Phase 2 addition
    only (`PharmacyInventoryEntry` Phase 1 record does NOT reference it — see
    `// Phase 2 addition` comment in §1); halt-condition H3 governs Phase 2 start.
  - `ProbeStatus`, `DegradationKind` — in `Sunfish.Foundation.MissionSpace` (ADR 0062;
    built 2026-05-01; verified present on origin/main).
  - `MissionEnvelope`, `IMissionEnvelopeProvider` — in `Sunfish.Foundation.MissionSpace`
    (ADR 0062; verified present).
  - `ShipRole`, `IPermissionResolver`, `PermissionDecision` — in `foundation-ship-common`
    (ADR 0077; Accepted 2026-05-05 via PR #543; ready-to-build but NOT yet built —
    halt-condition H1: build begins only after W#46 Phase 1 lands `ShipRole` on origin/main).
  - `KeyFingerprint` — introduced by W#53 Phase 1a (ADR 0066; ready-to-build; NOT yet built —
    halt-condition H2: `KeyFingerprintDisplay.razor` Phase 3a deferred until W#53 Phase 1
    lands).
  - `IFieldDecryptor`, `IFieldEncryptor`, `EncryptedField` — in `foundation-recovery`
    (ADR 0046-A2; built 2026-04-30; verified present on origin/main). Note: there is NO
    `IEncryptedFieldStore` type in this package; Pharmacy data sourcing uses
    `SickBayOptions.RegisteredFieldPurposes` registration (see §7 + Phase 2 checklist).
  - `IKeyRotationScheduler` — introduced by THIS ADR (ADR 0082) in `foundation-sick-bay`;
    no prior existence; COB defines on Phase 1 build.
  - `IOodWatchService` — introduced by W#49 (ADR 0078; Accepted; ready-to-build;
    not yet built — Medevac watch-authority check in Phase 3b uses a stub if W#49
    not yet built; no hard gate on Phase 3b).
- [x] **Anti-pattern scan.**
  AP-1 (unvalidated assumptions): Open Questions §1–§3 explicit;
    `KeyRotationTrigger` pending ADR 0068 acceptance noted in halt-conditions.
  AP-3 (vague phases): Phase 1 has 15 discrete checklist items.
  AP-11 (zombie project): Revisit triggers named.
  AP-21 (cited-symbol drift): all symbols verified above.
  AP-15 (premature precision): `FirstAidHint.Body` plain-text constraint is load-bearing
    (XSS prevention) — intentional.
- [x] **Council review posture.** Standard adversarial + WCAG/a11y subagent (Phases 3a/4;
  mandatory per §8) + security-engineering subagent (Phases 2/3b; mandatory per §Trust
  impact). Pre-merge canonical.
- [x] **Halt conditions (H1-H4).**
  - H1: `ShipRole` on origin/main — W#46 Phase 1 must land before Phase 1 build begins.
  - H2: `KeyFingerprint` on origin/main — W#53 Phase 1 must land before `KeyFingerprintDisplay`
    Phase 3a component.
  - H3: ADR 0068 Status: Accepted — `KeyRotationTrigger` type gate.
  - H4: `IFieldDecryptor` absence in `SickBayDataProvider` — verified by security council
    before Phase 2 merges.
- [x] **Cold Start Test.** Implementation checklist has 5 phases / ~17 discrete steps.
  Each is independently verifiable. Phase dependencies are explicit (H1-H4 halt conditions).

---

## Amendment A1 — Mission Envelope integration + AtmosphereHealth.Unknown sentinel + NoopKeyRotation guidance

**Status:** Accepted
**Date:** 2026-05-06
**Authors:** XO research session
**Council posture:** pre-merge canonical (per ADR 0069 D1; cohort batting average 30-of-35 substrate amendments needed council-sourced fixes — auto-merge NOT enabled until verdict received)
**Scope:** additive amendment to `Sunfish.Foundation.SickBay` types in `packages/foundation-sick-bay/` + projection-rule specification consumed by `Sunfish.Blocks.SickBay.SickBayDataProvider` in Phase 2b. No breaking changes to Phase 1 substrate. Existing `AtmosphereHealth` callers (Phase 2 stub returning `Green`) are migrated by this amendment to return `Unknown`.

### A1.1 — Context

Two surfaces in ADR 0082 (parent) acquired drift between draft and Phase 2 build:

1. **§9 Phase 2 checklist + W#54 hand-off §2.1** specify "query `IMissionEnvelopeProvider.GetCurrentEnvelope(tenant)`" and "map `MissionEnvelope` `DegradationKind` counts → `AtmosphereHealth`". Both are inaccurate against the substrate that landed via ADR 0062 / W#33:
   - The contract on `origin/main` is `IMissionEnvelopeProvider.GetCurrentAsync(CancellationToken)` — process-level (no `TenantId` parameter; `MissionEnvelope` is the host's runtime mission space, not a per-tenant view).
   - `MissionEnvelope` exposes ten typed dimension records (`HardwareCapabilities`, `UserCapabilities`, `RegulatoryCapabilities`, `RuntimeCapabilities`, `FormFactorSnapshot`, `EditionCapabilities`, `NetworkCapabilities`, `TrustAnchorCapabilities`, `SyncStateSnapshot`, `VersionVectorSnapshot`); each carries a `ProbeStatus` field. There is no per-dimension `DegradationKind`. `DegradationKind` lives on `FeatureVerdict` (the `IFeatureGate` taxonomy: `ReadOnly` / `ReducedSurface` / `PerformanceLimited` / `PartiallyHidden` / `AdvisoryCaveat`) — orthogonal to dimension-probe health.

2. **PR #695 council §Trust review (W#54 Phase 2)** flagged two stub-behavior risks that need ADR-level disposition rather than a code-only patch: (a) `AtmosphereHealth.Green` returned by the Phase 2 stub is indistinguishable from a real Green to downstream consumers, and (b) `NoopKeyRotationScheduler.ScheduleAsync` returning `Task.CompletedTask` silently is misleading-success when surfaced behind a user-visible "rotation triggered" toast.

This amendment closes the API drift, specifies a probe-status–based projection rule using the actual substrate, and adds the `AtmosphereHealth.Unknown` sentinel and a `NoopKeyRotationScheduler` registration-guidance note.

### A1.2 — Decision: dimension-`ProbeStatus` projection (Option A reconciled to substrate)

The XO directive recommended "Option A — each dimension-record carries `DegradationKind`; Sick Bay sums non-`None` across dimensions." The substrate verification (§A1.7 below) shows the per-dimension health field on `MissionEnvelope` is `ProbeStatus`, not `DegradationKind`. The directive's **intent** — Sick Bay consumes typed dimensions directly with no new substrate — is preserved; the **field name** is corrected to the value that actually exists.

`Sunfish.Blocks.SickBay.SickBayDataProvider.BuildAtmosphere` materializes `AtmosphereReadout` from a single `IMissionEnvelopeProvider.GetCurrentAsync(ct)` call, then projects the ten dimension `ProbeStatus` values via the table below.

#### A1.2.1 — `ProbeStatus` → severity bucket projection table

Each dimension contributes one count to either `WarningProbeCount`, `CriticalProbeCount`, or neither:

| `ProbeStatus` (ADR 0062) | Severity bucket | Rationale |
|---|---|---|
| `Healthy` | none (counted as healthy) | Probe ran, returned a normal reading. |
| `Stale` | `WarningProbeCount` | Probe last reading is past its TTL; data may be incorrect but not actively failing. |
| `PartiallyDegraded` | `WarningProbeCount` | Probe completed with caveats per ADR 0062 A1.10. |
| `Failed` | `CriticalProbeCount` | Probe execution failed; observable state is unknown. |
| `Unreachable` | `CriticalProbeCount` | Probe could not reach its target (e.g., network dimension offline); observable state is unknown. |

Total dimension count is always 10 (the `MissionEnvelope` shape is fixed by ADR 0062 A1.2). `WarningProbeCount + CriticalProbeCount + healthy` therefore sums to 10 in every materialized readout.

#### A1.2.2 — `OverallHealth` discriminator derivation

The `AtmosphereHealth` value is derived from `(WarningProbeCount, CriticalProbeCount, ForceEnableActive)`:

| Condition | `OverallHealth` |
|---|---|
| Provider has not yet projected a real `MissionEnvelope` (Phase 2 stub state) | `Unknown` (new — see A1.3) |
| `CriticalProbeCount == 0 && WarningProbeCount == 0 && !ForceEnableActive` | `Green` |
| `CriticalProbeCount == 0 && WarningProbeCount >= 1 && !ForceEnableActive` | `Yellow` |
| `CriticalProbeCount >= 1 && CriticalProbeCount <= 2` (or `ForceEnableActive`) | `Orange` |
| `CriticalProbeCount >= 3` | `Red` |

These thresholds align to the existing `AtmosphereHealth` XML doc semantics in `packages/foundation-sick-bay/AtmosphereHealth.cs` (Green = all healthy; Yellow = ≥1 warning; Orange = multiple warnings or one critical; Red = multiple criticals). The `ForceEnableActive` escalation to at least Orange preserves the operator-override visibility requirement: any active force-enable should never present as Green or Yellow regardless of probe counts.

#### A1.2.3 — `ForceEnableActive` derivation

`ForceEnableActive` is `true` when any `IFeatureForceEnableSurface.ResolveAsync` for any registered feature × dimension pair returns a non-null `ForceEnableRecord`. Phase 2b will inject `IFeatureForceEnableSurface` and the registered feature-keys list (sourced from `SickBayOptions` — added in Phase 2b per the hand-off addendum). Phase 2 stub keeps `ForceEnableActive = false`. Aligning with ADR 0062 A1.9.

### A1.3 — Decision: add `AtmosphereHealth.Unknown` sentinel

Add a new enum value to `AtmosphereHealth` in `packages/foundation-sick-bay/AtmosphereHealth.cs`:

```csharp
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AtmosphereHealth
{
    /// <summary>
    /// The provider has not yet projected real Mission Envelope probe data —
    /// e.g., Phase 2 stub state, or Phase 2b before the first
    /// IMissionEnvelopeProvider.GetCurrentAsync(ct) call has resolved.
    /// UI consumers MUST render a neutral / pending state (e.g., "—",
    /// a spinner, or a "data not yet available" banner). UI consumers
    /// MUST NOT render Unknown as Green — they convey different semantics.
    /// </summary>
    Unknown,

    /// <summary>All probes reporting healthy.</summary>
    Green,

    /// <summary>One or more probes warning; no critical states.</summary>
    Yellow,

    /// <summary>Multiple warnings or one critical probe.</summary>
    Orange,

    /// <summary>Multiple critical probes; immediate intervention required.</summary>
    Red,
}
```

**Position of `Unknown` in the enum (ordinal 0):** `Unknown` is added as the **first** member so that `default(AtmosphereHealth) == AtmosphereHealth.Unknown`. This preserves the ADR 0082 §Trust contract — code paths that fail to set `OverallHealth` (zero-init structs, partial readouts, deserialization defaults) MUST NOT default to `Green`. Existing callers that check `OverallHealth == AtmosphereHealth.Green` are unaffected (Green still exists; the value just shifts to ordinal 1). JSON serialization is by name (`JsonStringEnumConverter`), so wire-format compatibility is preserved.

**Migration of the Phase 2 stub:** `SickBayDataProvider.BuildAtmosphereStub` (currently returns `Green` per `packages/blocks-sick-bay/SickBayDataProvider.cs`) MUST return `Unknown`. Phase 2b's real projection then returns the derived `AtmosphereHealth` from §A1.2.2 once the provider is wired.

**WCAG 2.2 implications (additive to ADR 0082 §8):**

- SC 1.4.1 (Use of Color): Unknown MUST be rendered with a text label ("data not yet available" or equivalent) plus a non-color visual marker (e.g., a dashed-border icon, a spinner, or a "—" placeholder). It MUST NOT be conveyed by absence-of-color alone (a missing badge is indistinguishable from a not-yet-loaded badge for screen-reader users).
- SC 4.1.3 (Status Messages): the transition from `Unknown` → derived health on first envelope projection MUST be announced via `aria-live="polite"` (consistent with ADR 0082 §8 SC 4.1.3 row).

**`Unknown` is not a Critical-band state — it is a not-yet-known state.** UI consumers SHOULD differentiate visually: Unknown is "we don't know yet; wait" whereas Red is "we know, and intervention is required." Both render in non-green states; the affordance differs.

### A1.4 — Decision: `NoopKeyRotationScheduler` registration guidance + ADR §Trust note

`NoopKeyRotationScheduler.ScheduleAsync` returns `Task.CompletedTask` per the W#54 hand-off Phase 2 contract — this is the documented stub for environments where the real key-rotation substrate has not yet been wired (Phase 3b). Council §Trust correctly flagged that a UI surfacing "rotation triggered" alongside this stub creates a false-success: the toast renders, no rotation occurs, audit trail records intent without effect.

**ADR 0082 §Trust impact gains a new bullet:**

> **Phase 2 `NoopKeyRotationScheduler` is misleading-success when surfaced behind UI.** `NoopKeyRotationScheduler.ScheduleAsync` returns `Task.CompletedTask` without scheduling work. Hosts MUST NOT register `NoopKeyRotationScheduler` in any environment that surfaces a user-visible "rotation triggered" toast or any other affordance that implies effect. Acceptable Phase 2 host postures: (a) register `NoopKeyRotationScheduler` with the `TriggerKeyRotation` UI affordance hidden / disabled until Phase 3b lands the real `IKeyRotationScheduler`, or (b) defer registering `IKeyRotationScheduler` entirely (callers see DI resolution failure rather than silent success). Phase 3b ships a real implementation wired to the W#32 / ADR 0046-A2 rotation substrate; the Noop is removed from Anchor + Bridge default registration at that point.

**Anchor + Bridge wiring (Phase 4 of ADR 0082):** `AddSunfishSickBayDefaults` (per cohort `AddSunfishXDefaults` convention) MUST register `NoopKeyRotationScheduler` only when the consumer explicitly opts in (e.g., a `SickBayOptions.RegisterNoopKeyRotationScheduler` flag, default `false`). This makes the Phase 2 stub posture a deliberate host decision, not a default-on hazard. The hand-off addendum (Phase 2b) specifies the option-flag wiring.

**No new audit event for Noop scheduling.** Scheduling a Noop produces no observable effect, so there is no rejected-operation event to emit (compare `SickBayMedevacSelfApprovalRejected`, which records a real rejected medevac decision — a four-eyes guard that the system has actively evaluated). Phase 2 hosts that opt into Noop registration MUST disable the `TriggerKeyRotation` UI affordance per the §Trust bullet above; the existing `SickBayKeyRotationTriggered` audit event ships in Phase 3b when the real `IKeyRotationScheduler` lands and is wired ahead of `IKeyRotationScheduler.ScheduleAsync`.

**Residual risk (known v1 limitation):** the opt-in flag converts "silent default registration" to "explicit registration via flag," but a host operator setting `options.RegisterNoopKeyRotationScheduler = true` without reading the §Trust bullet above still produces a misleading-success outcome. This is a known v1 limitation. The long-term mitigation is a Roslyn analyzer that warns when `RegisterNoopKeyRotationScheduler = true` and the host registers any UI surface that emits `SickBayKeyRotationTriggered` audit events (or a `TriggerKeyRotation` ShipAction handler); that analyzer is out of scope for Phase 2b and is flagged as a future work item for a Phase 3a or analyzer-ADR follow-on.

### A1.5 — Scoping note: Atmosphere is host-scoped (process-level), not tenant-scoped

A consequence of consuming `IMissionEnvelopeProvider.GetCurrentAsync(ct)` directly: `AtmosphereReadout` reflects the **host process's** mission space (network connectivity, hardware, edition, regulatory jurisdiction, sync state of the local device), not any single tenant's posture. The `TenantId` parameter to `ISickBayDataProvider.GetSnapshotAsync(TenantId, CancellationToken)` is consumed only by the Pharmacy projection (which IS per-tenant — registered field purposes can vary per tenant); the Atmosphere projection ignores it.

This is an intentional v1 limitation: Mission Envelope is shaped at the kernel-runtime tier as a host concept (ADR 0062 A1.2 — "host's runtime mission space"). Per-tenant atmospheric views (e.g., per-tenant force-enables, per-tenant sync-state slices) are out of scope for v1 and would require a tenant-scoped Mission Envelope substrate — out of scope for this amendment.

**UI guidance (Phase 3a + apps/docs):** the Atmosphere tab MUST label its scope as "this device" / "host" / "Anchor process" rather than implying tenant-scope. The Pharmacy and Lab tabs remain tenant-scoped; the Atmosphere tab is the only departmental view that is host-scoped, and the UI must surface that distinction visually.

### A1.6 — New + modified types introduced by A1

| Type | Namespace | Tier | Action | Naming-check |
|---|---|---|---|---|
| `AtmosphereHealth.Unknown` (new enum value, ordinal 0) | `Sunfish.Foundation.SickBay` | foundation | additive | CLEAN — no collision with existing values |

No other new types. No removed types. Phase 2b's `BuildAtmosphere` projection logic is implementation in `Sunfish.Blocks.SickBay.SickBayDataProvider`, not a new public surface.

### A1.7 — §A0 self-audit (additive)

**Negative-existence (A1 symbol not yet on `origin/main`):**

- `AtmosphereHealth.Unknown` — verified `grep -n "Unknown" packages/foundation-sick-bay/AtmosphereHealth.cs` returns zero matches on `origin/main` `72ab276b`. Phase 1 enum has four values (`Green`, `Yellow`, `Orange`, `Red`); A1 adds a fifth (`Unknown`) at ordinal 0.
- No parallel session is authoring an A1 — verified `gh pr list --search "ADR 0082"` returns zero open PRs at amendment authoring time.

**Positive-existence (cited symbols exist on `origin/main`):**

- `IMissionEnvelopeProvider.GetCurrentAsync(CancellationToken)` — `packages/foundation-mission-space/Services/Contracts.cs:51` ✓ (returns `ValueTask<MissionEnvelope>`).
- `IMissionEnvelopeObserver` — `packages/foundation-mission-space/Services/Contracts.cs:35` ✓.
- `MissionEnvelope` — `packages/foundation-mission-space/Models/MissionEnvelope.cs:13` ✓ (sealed record; ten dimension properties + `SnapshotAt` + `EnvelopeHash`).
- The ten dimension records — `packages/foundation-mission-space/Models/Dimensions/Dimensions.cs` — verified each carries a `ProbeStatus` field: `HardwareCapabilities` (line 30), `UserCapabilities` (line 47), `RegulatoryCapabilities` (line 62), `RuntimeCapabilities` (line 82), `FormFactorSnapshot` (line 164), `EditionCapabilities` (line 99), `NetworkCapabilities` (line 121), `TrustAnchorCapabilities` (line 135), `SyncStateSnapshot` (line 153), `VersionVectorSnapshot` (line 175). All ten ✓. (Ordered to match `MissionEnvelope` property declaration order: Hardware, User, Regulatory, Runtime, FormFactor, Edition, Network, TrustAnchor, SyncState, VersionVector.)
- `ProbeStatus` enum — `packages/foundation-mission-space/Models/Enums.cs:48–55` ✓ (five values: `Healthy`, `Stale`, `Failed`, `PartiallyDegraded`, `Unreachable`).
- `IFeatureForceEnableSurface.ResolveAsync` — `packages/foundation-mission-space/Services/Contracts.cs:45` ✓.
- `AtmosphereHealth` enum (Phase 1 four values) — `packages/foundation-sick-bay/AtmosphereHealth.cs:11–24` ✓.
- `AtmosphereReadout` record — `packages/foundation-sick-bay/AtmosphereReadout.cs:15–20` ✓ (positional record: `OverallHealth`, `WarningProbeCount`, `CriticalProbeCount`, `ForceEnableActive`, `CapturedAt`).
- `Sunfish.Blocks.SickBay.SickBayDataProvider` Phase 2 stub — `packages/blocks-sick-bay/SickBayDataProvider.cs:143–149` ✓ (`BuildAtmosphereStub` returns `AtmosphereHealth.Green`; A1 migrates this to `Unknown`).
- `NoopKeyRotationScheduler` — `packages/blocks-sick-bay/NoopKeyRotationScheduler.cs` ✓ (Phase 2 stub returning `Task.CompletedTask`).
- `IKeyRotationScheduler` — declared in `packages/foundation-sick-bay/IKeyRotationScheduler.cs` per Phase 1 ✓.
- `SickBayKeyRotationTriggered` audit event — declared per ADR 0082 §6 (Phase 1 added 10 audit constants) — referenced by name only in this amendment.

**Structural-citation correctness (do APIs match the cited shape?):**

- `IMissionEnvelopeProvider.GetCurrentAsync(CancellationToken)` returns `ValueTask<MissionEnvelope>` (NOT `Task<MissionEnvelope>` and NOT `MissionEnvelope` — `Contracts.cs:51`). Phase 2b implementation MUST `await` the `ValueTask` correctly; cannot use `IMissionEnvelopeProvider.GetCurrentAsync(ct).Result` (ValueTask `.Result` is a synchronous-completion access pattern that's unsafe in general). The hand-off addendum specifies `await provider.GetCurrentAsync(ct)`.
- `IMissionEnvelopeProvider.GetCurrentEnvelope(tenant)` does NOT exist on `origin/main` (verified `grep -n "GetCurrentEnvelope" packages/foundation-mission-space/Services/Contracts.cs` returns zero matches). The W#54 hand-off §2.1 cites this name; the addendum at `icm/_state/handoffs/sick-bay-stage06-addendum.md` corrects it.
- `MissionEnvelope` exposes typed dimension records — NOT a flat probe-result list. Code that wants per-dimension health must read each typed record's `ProbeStatus` field independently (no enumerable dimension iteration on `MissionEnvelope` itself; the dimension fields are accessed by name).
- `DegradationKind` enum (`Models/Enums.cs:38–45`) is a `FeatureVerdict.DegradationKind` taxonomy (`ReadOnly` / `ReducedSurface` / `PerformanceLimited` / `PartiallyHidden` / `AdvisoryCaveat`) — orthogonal to dimension `ProbeStatus`. Sick Bay's Atmosphere readout consumes `ProbeStatus` (per-dimension), not `DegradationKind` (per-feature-verdict). The hand-off §2.1 conflates these; the addendum disambiguates.
- `EnvelopeChangeSeverity` enum (`Models/Enums.cs:19–27`) carries `Informational` / `Warning` / `Critical` / `ProbeUnreliable` — this is the per-`EnvelopeChange` severity (diff between two snapshots), not a per-snapshot per-dimension health. Sick Bay's `AtmosphereReadout` is a per-snapshot view; `EnvelopeChangeSeverity` is consumed by `IMissionEnvelopeObserver` subscribers for change events, not for static readouts.
- `AtmosphereHealth.Unknown` ordinal 0 placement — verified by reading the existing enum: Phase 1 values are `Green=0`, `Yellow=1`, `Orange=2`, `Red=3`. After A1: `Unknown=0`, `Green=1`, `Yellow=2`, `Orange=3`, `Red=4`. **Wire-format compatibility:** `[JsonConverter(typeof(JsonStringEnumConverter))]` is applied at the type level (`AtmosphereHealth.cs:10`) — JSON serialization is by name, so the ordinal shift is wire-safe. **Source-compat:** any `switch` expression on `AtmosphereHealth` will get a CS8509 warning for the unhandled `Unknown` case (matching the precedent of `ShipRole.IDC` exhaustive-switch caveat in §5). Implementations MUST add an `Unknown` arm.

**Council disposition expected at pre-merge (this amendment):**

- §Trust review: confirm `Unknown` semantics are documented for both code consumers (must not equate to Green) and UI consumers (must render neutral, not absent) — addressed in §A1.3.
- §Trust review: confirm `NoopKeyRotationScheduler` registration-guidance is enforced at the DI level (host-opt-in flag, not default-on) — addressed in §A1.4.
- §Structural-Citation: confirm `ProbeStatus` is the correct dimension health field (not `DegradationKind`) — addressed in §A1.7 above + §A1.2.1 projection table.
- §Pedantic-Lawyer: confirm tenant-scope vs host-scope is documented as a known v1 limitation (not a bug) — addressed in §A1.5.
- §A11y / WCAG: confirm `Unknown` rendering guidance does not weaken SC 1.4.1 or SC 4.1.3 — addressed in §A1.3 (rendering MUST include text + non-color marker; transitions MUST announce via `aria-live="polite"`).

### A1.8 — Implementation checklist (Phase 2b — new sub-phase)

Phase 2b is a new sub-phase in ADR 0082's §9 phase table — slotted between Phase 2 (merged via PR #695) and Phase 3a (Blazor UI). Estimated ~2–3h, single PR. Pre-merge security-engineering subagent NOT required for this sub-phase (no decryption-path or audit-emission changes); standard adversarial council canonical per ADR 0069 D1.

- [ ] Add `AtmosphereHealth.Unknown` enum value at ordinal 0 in `packages/foundation-sick-bay/AtmosphereHealth.cs`. Update XML doc per §A1.3.
- [ ] Update `packages/blocks-sick-bay/SickBayDataProvider.cs` `BuildAtmosphereStub` to return `AtmosphereHealth.Unknown` until the real provider is wired (this is a one-line stub fix; the real projection lands in the same PR via `BuildAtmosphere` below).
- [ ] Inject `IMissionEnvelopeProvider` into `SickBayDataProvider` (constructor parameter; nullable for backward-compat with Phase 2 tests until the test fixture is updated in the same PR).
- [ ] Implement `BuildAtmosphereAsync(MissionEnvelope envelope, DateTimeOffset capturedAt)` per §A1.2.1 + §A1.2.2 projection rules. Materialize once per `GetSnapshotAsync` invocation; do NOT call `GetCurrentAsync` more than once per snapshot.
- [ ] **Lab projection deferred to Amendment A2.** Phase 2b ships `BuildSnapshotAsync` returning
  `Array.Empty<LabDiagnosticResult>()` for the Lab list, with a code comment pointing at Amendment A2.
  **Rationale (council HA2 Path B disposition):** synthesizing `DegradationKind.AdvisoryCaveat` from
  per-dimension `ProbeStatus` data that contains no `DegradationKind` is the same §Trust failure class
  that A1.3 + A1.4 reject — a dashboard surface MUST NOT synthesize a taxon not present in source data.
  Amendment A2 will widen `LabDiagnosticResult.Degradation` to `DegradationKind?` (nullable) and spec
  the full projection in Phase 2c (post-A2). Phase 2b ships Atmosphere + Unknown sentinel + Noop opt-in
  only.

- [ ] Wire `IMissionEnvelopeObserver.Subscribe` in `SubscribeSnapshotAsync` so envelope changes drive snapshot re-emission (replacing the current "emit-once-then-poll" stub). Coalesce concurrent change-events (debounce ~250ms) to avoid flapping during multi-dimension probe runs.
- [ ] Add a `SickBayOptions.RegisterNoopKeyRotationScheduler` boolean flag (default `false`). `AddSunfishSickBayDefaults` registers `NoopKeyRotationScheduler` only when this flag is `true`; otherwise `IKeyRotationScheduler` is left unregistered (DI resolution failure surfaces to the caller).
- [ ] Tests:
  - `AtmosphereHealth_Unknown_is_ordinal_zero` — `Assert.Equal(0, (int)AtmosphereHealth.Unknown)`
  - `BuildAtmosphere_returns_Green_when_all_probes_Healthy` — fixture envelope with all 10 dimensions `ProbeStatus.Healthy`
  - `BuildAtmosphere_returns_Yellow_on_one_Stale_probe` — fixture with Hardware Stale, others Healthy
  - `BuildAtmosphere_returns_Orange_on_one_Failed_probe` — fixture with Network Failed, others Healthy
  - `BuildAtmosphere_returns_Red_on_three_Failed_probes` — fixture with Hardware/Network/Runtime Failed
  - `BuildAtmosphere_returns_Orange_when_ForceEnableActive` — fixture all Healthy + `ForceEnableActive = true` → expect `Orange` (escalation)
  - `BuildAtmosphere_returns_Unknown_when_provider_not_wired` — null provider injection (or provider throwing) → expect `Unknown`
  - `SubscribeSnapshotAsync_re_emits_on_IMissionEnvelopeObserver_change` — observer-driven invalidation
  - `AddSunfishSickBayDefaults_does_not_register_NoopKeyRotationScheduler_by_default` — verify DI resolution fails for `IKeyRotationScheduler` unless flag is set
- [ ] Pre-merge council: standard adversarial (per ADR 0069 D1). Auto-merge NOT enabled until verdict received.

### A1.9 — Cross-references

- **ADR 0062** (Mission Space Negotiation Protocol) — substrate origin of `IMissionEnvelopeProvider`, `MissionEnvelope`, the ten dimension records, `ProbeStatus`, `DegradationKind`, `EnvelopeChangeSeverity`. A1.2.1 projection table consumes `ProbeStatus`; A1.7 verifies all citations.
- **ADR 0069 D1** (ADR Authoring Discipline) — pre-merge council canonical for substrate amendments; this amendment runs council before merge.
- **W#54 hand-off addendum** at `icm/_state/handoffs/sick-bay-stage06-addendum.md` — companion document specifying §2.1 API correction + Phase 2b implementation per §A1.8.
- **PR #695** (W#54 Phase 2 — `feat(blocks-sick-bay): W#54 Phase 2 — reference impls + H4 reflection test`) — merged; this amendment supersedes the §Trust posture of the Phase 2 stub (`AtmosphereHealth.Green` → `AtmosphereHealth.Unknown`).
- **COB beacon** at `icm/_state/research-inbox/cob-question-2026-05-06T18-00Z-w54-mission-envelope-integration.md` — resolved by this amendment; archived in the same PR.

### A1.10 — Halt-conditions specific to A1

- **(HA1)** This amendment must reach `Status: Accepted` on `origin/main` before Phase 2b PR may auto-merge. (Auto-merge is gated by ADR 0069 D1 anyway; this is operational reinforcement.)
- **(HA2 — RESOLVED Path B)** Pre-merge council (PR #701) rejected the `DegradationKind.AdvisoryCaveat`
  fallback for `LabDiagnosticResult.Degradation` as a §Trust violation (same class as A1.3 + A1.4):
  synthesizing a taxon not present in source data is a misleading-success defect. **Path B disposition:**
  Lab projection is deferred to Amendment A2, which will widen `LabDiagnosticResult.Degradation` to
  `DegradationKind?` (nullable). Phase 2b ships Atmosphere + Unknown sentinel + Noop opt-in only;
  Phase 2c (post-A2) ships the Lab projection per the nullable contract.
