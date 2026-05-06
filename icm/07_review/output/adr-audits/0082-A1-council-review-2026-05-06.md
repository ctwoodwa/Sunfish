# ADR 0082 Amendment A1 — Canonical Pre-Merge Council Review

**PR:** [#700 — `docs(adrs): 0082 A1 — Mission Envelope integration + AtmosphereHealth.Unknown sentinel + NoopKeyRotation guidance`](https://github.com/ctwoodwa/Sunfish/pull/700)
**Branch:** `docs/adr-0082-a1-mission-envelope-integration` @ `ff08e3ac`
**Reviewed against:** `origin/main` @ `2047718f` (post-XO-ruling PR #699 merge)
**Council form:** standard adversarial 4-perspective (Outside Observer + Pessimistic Risk Assessor + Pedantic Lawyer + Skeptical Implementer); security-engineering subagent NOT required per author rationale (no decryption-path or audit-emission changes; H4 reflection test from PR #695 stands).
**Reviewer:** XO Opus 4.7 (xhigh)
**Date:** 2026-05-06
**Council posture:** pre-merge canonical per ADR 0069 D1; cohort batting average entering this review = 41-of-41 substrate amendments needed council fixes.

---

## TL;DR + verdict

**Verdict:** **NEEDS-AMENDMENT** — 1 Blocking, 2 Major, 3 Minor, 1 Mechanical. Substrate decisions (Unknown sentinel ordinal-0; opt-in Noop registration; ProbeStatus projection) are sound and correctly cited; the addendum's Phase 2b code samples carry two structural-citation defects in the `BuildLab` snippet that will block compilation as written. Once those are corrected, the ADR + addendum pair is council-clean.

**HA2 disposition:** **Path B (REJECT fallback) recommended** — see §1 below. Phase 2b ships Atmosphere + Unknown + Noop guidance without the Lab projection; a follow-up amendment widens `LabDiagnosticResult.Degradation` to `DegradationKind?` (nullable) before the lab pane lights up. Rationale: silently mapping per-dimension `ProbeStatus` to `DegradationKind.AdvisoryCaveat` violates §Trust ("a dashboard surface that synthesizes a degradation taxon from data that does not contain one") and is ironically the same class of misleading-success defect that A1.4 corrects for `NoopKeyRotationScheduler`.

**Finding count:** 7 (1 Blocking, 2 Major, 3 Minor, 1 Mechanical).

**Cohort batting average update:** **42-of-42** — every substrate amendment in this cohort, without exception, has surfaced a council-correctable issue at pre-merge. The Phase 2b implementation specification has two real compile-blockers and one design-judgment call that materially changes the §Trust contract; pre-merge catch saved a Phase 2b iteration cycle.

---

## §1 — HA2 disposition: REJECT the AdvisoryCaveat fallback (Path B)

### The question

ADR 0082 A1.10 halt-condition HA2 asks council to dispose: should Phase 2b populate `LabDiagnosticResult.Degradation` with a default of `DegradationKind.AdvisoryCaveat` when no `FeatureVerdict` references the dimension being projected (Path A), or should Phase 2b ship without the Lab projection and a follow-up amendment widen `LabDiagnosticResult.Degradation` to `DegradationKind?` (nullable) (Path B)?

### Council disposition: **Path B**

The §Trust contract this amendment exists to defend says: a Sick Bay surface MUST NOT misrepresent the underlying probe data. A1.3 establishes this for `AtmosphereHealth.Unknown` ("Phase 2 stub state ... MUST NOT default to Green; the semantics differ"). A1.4 establishes the same for `NoopKeyRotationScheduler` ("scheduling a Noop does NOT emit a new audit event ... user-visible 'rotation triggered' affordance MUST be disabled"). The Lab `Degradation` fallback proposed by Path A is the same class of defect both A1.3 and A1.4 reject:

- **The data does not contain a `DegradationKind`.** Per A1.7 §Structural-citation: `DegradationKind` lives on `FeatureVerdict` (a per-feature gate output), orthogonal to per-dimension `ProbeStatus`. There is no derivation rule from `(ProbeStatus, dimension)` to `DegradationKind` that holds without a feature-verdict cross-walk.
- **`AdvisoryCaveat` is the least-severe `DegradationKind`** per ADR 0062 A1.2 ordering. Choosing it as a default is "if we don't know, claim the most-benign value" — exactly the §Trust failure pattern A1.3 calls out for `AtmosphereHealth.Green`.
- **A read-model surface that synthesizes a taxon not present in the source data is a §Trust hazard.** The Lab tab is presented to operators as a tenant-friendly projection over Mission Envelope probe results. Operators reading "AdvisoryCaveat" on every healthy probe will form a mental model that the system has actually evaluated a degradation kind for that probe — when it has not.

The Path A rationale ("AdvisoryCaveat is the least severe; safe default for read-model display") would be persuasive if the field were optional; it is not. The Phase 1 record shape has `DegradationKind Degradation { get; init; }` as a non-nullable required positional parameter. Path A's "default to AdvisoryCaveat" is a hard claim that the system has evaluated degradation and concluded the lowest band, not a sentinel for "not yet evaluated."

Path B is a one-amendment cycle (low cost; ADR 0082 has demonstrated that amendments ship in 2-3h XO sessions with single PRs) and ships Phase 2b's other deliverables (Atmosphere projection + Unknown sentinel + Noop opt-in flag) without entanglement. The Lab tab is non-load-bearing for the Phase 3a UI render — Phase 3a's Atmosphere + Pharmacy panes ship without Lab, and Lab lights up after the follow-up amendment.

**Cohort precedent:** A1.3's solution to "stub Green is misleading-success" was to add a sentinel value (`Unknown`) at ordinal 0. The structurally-equivalent move for `LabDiagnosticResult.Degradation` is to make it nullable (`DegradationKind?`). The follow-up amendment therefore mirrors A1.3's decision pattern — "the API needs an additional value (or nullable shape) to express 'not-yet-known' honestly" — which is the cohort discipline ADR 0069 D1 is enforcing.

### Halt-condition disposition

**HA2 is RESOLVED to Path B.** Action items for Phase 2b PR description:

1. Drop the `BuildLab` implementation from Phase 2b (addendum §2.5).
2. Phase 2b's `BuildSnapshotAsync` returns an empty `Lab` list (`Array.Empty<LabDiagnosticResult>()`) with a code-comment pointing at the follow-up amendment.
3. File a follow-up amendment **A2** to ADR 0082 widening `LabDiagnosticResult.Degradation` to `DegradationKind?` (nullable). A2 may also widen the `LastRunAt` consideration (see §3 finding M2 below) if the cohort decides to align with NodaTime.
4. Phase 2c (post-A2) implements `BuildLab` per A2's nullable contract: when no FeatureVerdict references the dimension, `Degradation = null` (a sentinel for "not-yet-evaluated"), exactly mirroring A1.3's `AtmosphereHealth.Unknown` semantics.

This disposition aligns with halt-condition 1 in the prompt's halt-conditions check ("clear cohort-correct answer"); council is not split, and the rejection is structural, not preferential.

---

## §2 — Per-issue audit: Issue 1 (mechanical API drift correction)

### Finding [Mechanical, MEC1] — RESOLVED

The hand-off §2.1 cited `IMissionEnvelopeProvider.GetCurrentEnvelope(tenant)`. The actual contract on `origin/main 2047718f` at `packages/foundation-mission-space/Services/Contracts.cs:51` is `ValueTask<MissionEnvelope> GetCurrentAsync(CancellationToken ct = default)` — process-level, no `TenantId` parameter. Council verified the live contract.

The addendum at `icm/_state/handoffs/sick-bay-stage06-addendum.md` §1 corrects this:

> **Lab**: `await IMissionEnvelopeProvider.GetCurrentAsync(ct)` (returns `ValueTask<MissionEnvelope>`; no tenant parameter — `MissionEnvelope` is process-level per ADR 0062 A1.2)

**Disposition:** **PASS.** Mechanical correction landed correctly; halt-condition H2.A on the original hand-off is resolved. No amendment required.

---

## §3 — Per-issue audit: Issue 2 (`ProbeStatus` projection — substantive)

### Finding [Major, MAJ1] — `BuildLab` snippet in addendum §2.5 has TWO compile-blocking defects

This is the **Blocking** finding by §Trust risk magnitude (§5 below upgrades to Blocking after HA2 resolution; here scoped as Major in its own right).

The addendum §2.5 ships a sample implementation of `BuildLabResult` that will not compile against the actual `LabDiagnosticResult` shape on `origin/main`:

**Defect M1.1: Object-initializer syntax on a positional record.** The actual record at `packages/foundation-sick-bay/LabDiagnosticResult.cs` is positional:

```csharp
public sealed record LabDiagnosticResult(
    string ProbeName,
    string DimensionId,
    ProbeStatus Status,
    DegradationKind Degradation,
    DateTimeOffset LastRunAt,
    string? DiagnosticDetail);
```

The addendum §2.5 lines 230-239 attempts:

```csharp
return new LabDiagnosticResult
{
    ProbeName        = dimensionId,
    DimensionId      = dimensionId,
    ...
};
```

C# positional records do NOT synthesize a parameterless constructor; the synthesized init-only properties cannot be set without first calling the primary constructor. The code as written produces CS7036 ("There is no argument given that corresponds to the required parameter 'ProbeName'"). The correct invocation is constructor-style: `new LabDiagnosticResult(dimensionId, dimensionId, status, ..., null)`.

**Defect M1.2: `LastRunAt` type mismatch.** The addendum §2.5 line 236 reads:

```csharp
LastRunAt        = NodaTime.Instant.FromDateTimeOffset(capturedAt),
```

But `LabDiagnosticResult.LastRunAt` is `DateTimeOffset`, not `NodaTime.Instant`. The cited cohort precedent in ADR 0082 line 360 explicitly says "DateTimeOffset over NodaTime (cohort precedent W#46/W#49/W#50/W#54/W#55)" — the Sick Bay package contains zero NodaTime references (`git grep -n "using NodaTime\|Instant " packages/foundation-sick-bay/ packages/blocks-sick-bay/` returns empty). The addendum's NodaTime.Instant usage is structural drift from the cohort convention. The correct value is just `capturedAt` (the `DateTimeOffset` parameter).

**Disposition:** **NEEDS-AMENDMENT.** Addendum §2.5 must replace the `BuildLabResult` snippet with constructor-style invocation using `DateTimeOffset`. After HA2 is resolved Path B (above), this finding is moot for Phase 2b (Lab projection deferred), but the addendum's snippet should be corrected anyway since Phase 2c will reference it.

**Recommended edit (addendum §2.5):**

```csharp
private static LabDiagnosticResult BuildLabResult(
    string dimensionId, ProbeStatus status, DateTimeOffset capturedAt) =>
    new LabDiagnosticResult(
        ProbeName:        dimensionId,
        DimensionId:      dimensionId,
        Status:           status,
        Degradation:      DegradationKind.AdvisoryCaveat,  // Path A — REPLACE per HA2 disposition
        LastRunAt:        capturedAt,
        DiagnosticDetail: null);
```

(After HA2 Path B + A2: `Degradation` becomes nullable; the snippet passes `null` here.)

### Finding [Minor, MIN1] — A1.7 dimension-citation ordering inconsistent with `MissionEnvelope` declaration

A1.7 at line 1017 lists the 10 dimension records in this order: `Hardware ... Runtime, EditionCapabilities ... NetworkCapabilities ... TrustAnchorCapabilities ... SyncStateSnapshot ... FormFactorSnapshot ... VersionVectorSnapshot`. The actual `MissionEnvelope` record on `origin/main` declares them in order: `Hardware ... Runtime, FormFactor, Edition, Network, TrustAnchor, SyncState, VersionVector`. The line citations (line 30, 47, 62, 82, 99, 121, 135, 153, 164, 175) are individually correct — they identify the `ProbeStatus` field on each record. The list ordering in the audit block is just a clerical drift.

**Disposition:** **PASS with note.** Not material to correctness; A1.2.1 projection table is canonical (lists each dimension by name without ordering implication). Recommend a 1-line edit reordering the A1.7 list to match `MissionEnvelope` declaration order for clarity.

### Finding [Major, MAJ2] — A1.5 host-scoped Atmosphere is a real v1 limitation but the §Trust note is missing

A1.5 documents that Atmosphere is host-scoped (process-level), not tenant-scoped, and labels it as a known v1 limitation. This is correct per ADR 0062 A1.2 (`MissionEnvelope` is the host's runtime mission space). However, the §Trust impact section of ADR 0082 (lines 596-616) does not yet have a bullet capturing this: a tenant administrator viewing the Atmosphere tab MUST understand that the displayed health reflects the host process, not their tenant's configuration. A multi-tenant Bridge deployment (Zone C) where multiple tenants share a single hosted process would all see the *same* Atmosphere readout — not their own.

A1.5 includes UI guidance ("Atmosphere tab MUST label its scope as 'this device' / 'host' / 'Anchor process'"), but this is a presentation rule, not a §Trust contract. The §Trust impact section should add a bullet:

> **Atmosphere is host-scoped, not tenant-scoped.** `AtmosphereReadout` reflects the host process's Mission Envelope. In multi-tenant hosted deployments (Bridge / Zone C), ALL tenants sharing the host see the same Atmosphere readout. UI surfaces MUST label scope explicitly per §A1.5; back-end consumers MUST NOT cache Atmosphere by tenant key (any tenant cache flush would not invalidate cross-tenant readouts).

**Disposition:** **NEEDS-AMENDMENT.** Add this bullet to ADR 0082 §Trust impact (immediately following the existing A1.4 §Trust bullet at line 983).

---

## §4 — Per-issue audit: Issue 3 (`AtmosphereHealth.Unknown` + `RegisterNoopKeyRotationScheduler` flag)

### 4A — `AtmosphereHealth.Unknown` ordinal-0 placement

**Wire safety:** Verified ✓. `[JsonConverter(typeof(JsonStringEnumConverter))]` is applied at the type level on the existing enum at `AtmosphereHealth.cs:10`. JSON serialization is by name; existing consumers of `"Green"` are unaffected. Ordinal shift Green=0→1 is a wire no-op.

**Source safety (CS8509):** Verified ✓. Existing `switch` expressions on `AtmosphereHealth` will get CS8509 on the unhandled `Unknown` case. The precedent cited at A1.7 line 1034 ("matching the precedent of `ShipRole.IDC` exhaustive-switch caveat in §5") is real — `ShipRole.IDC` was added in W#46 P1 with documented CS8509 implications. The §5 caveat handler for `ShipRole.IDC` (ADR 0082 lines 461-465) IS the canonical pattern; A1's Unknown follows it.

**Default-init safety:** This is the council's spot-check focus. Pre-A1, `default(AtmosphereHealth) == AtmosphereHealth.Green` (Green at ordinal 0). Post-A1, `default(AtmosphereHealth) == AtmosphereHealth.Unknown`. The §Trust contract A1.3 articulates is precisely that this flip is desirable: zero-init structs / partial readouts / deserialization defaults flowing from "green" (positive assertion) to "unknown" (sentinel for not-yet-evaluated) is the §Trust posture being defended.

**Spot-check for behavior change:** Council audited consumers of `AtmosphereHealth` on `origin/main`:
- `AtmosphereReadout.cs` — `OverallHealth` is a constructor parameter (positional); no `default(AtmosphereHealth)` path. Existing `BuildAtmosphereStub` explicitly passes `AtmosphereHealth.Green`; Phase 2b migrates this to `Unknown` per addendum §2.2. ✓
- `SickBayDataProvider.cs` — only constructs via the explicit `AtmosphereHealth.Green` literal in `BuildAtmosphereStub`. No struct-default path. ✓
- No other usages found in `git grep -rn "AtmosphereHealth\\." packages/`.

**Disposition:** **PASS.** Wire-safe + source-safe + default-init flip is intentional and §Trust-positive. No amendment required for the Unknown sentinel itself.

### Finding [Minor, MIN2] — A1.3 WCAG rendering rule is non-testable as written

A1.3 specifies "MUST NOT default to Green" + "neutral/pending state UI" + "non-color marker" + "`aria-live="polite"` on Unknown → derived transition." The first three are presentation rules; the WCAG rule about non-color marker is enforceable by Storybook story / visual regression, but the addendum offers no enforcement seam. The aria-live transition rule could be enforced by a Blazor unit test asserting that the rendered DOM contains an `aria-live="polite"` region around the Unknown state badge.

**Disposition:** **PASS with hand-off note.** Phase 3a Blazor UI hand-off should pin a test:
- `AtmosphereHealth_Unknown_renders_aria_live_polite_region`
- `AtmosphereHealth_Unknown_renders_non_color_marker_OR_text_label`

Both can be Blazor `Bunit` component tests; Phase 3a hand-off update can capture this as a checkbox under the WCAG/a11y subagent's review surface (per the parent ADR 0082 §8 SC 1.4.1 + 4.1.3 contract). Not blocking for A1 (which is a substrate amendment, not a UI amendment), but worth flagging in the addendum's quick-reference §6 row.

### 4B — `SickBayOptions.RegisterNoopKeyRotationScheduler` opt-in flag

**Default `false`:** Verified in addendum §2.8. `AddSunfishSickBayDefaults` only registers `NoopKeyRotationScheduler` when `options.RegisterNoopKeyRotationScheduler == true`. Default-no-registration → `IKeyRotationScheduler` resolution failure surfaces to caller (DI exception, not silent-success).

**Test coverage:** The implementation checklist at A1.8 line 1068 explicitly names `AddSunfishSickBayDefaults_does_not_register_NoopKeyRotationScheduler_by_default` and the symmetric `AddSunfishSickBayDefaults_registers_NoopKeyRotationScheduler_when_option_flag_set`. ✓

### Finding [Minor, MIN3] — Opt-in flag does not address "user clicks toggle without understanding"

Council §Pessimistic Risk Assessor flag: the opt-in flag converts "silent default registration" to "explicit registration via flag." A host operator setting `options.RegisterNoopKeyRotationScheduler = true` without reading the §Trust bullet still produces a misleading-success outcome. The §Trust contract is documented (A1.4 + the new §Trust bullet at line 983), but it's possible to opt in without reading the docstring.

The mitigation that A1.4 already names is: hosts that opt in MUST disable the `TriggerKeyRotation` UI affordance (separate decision, separate code path). That's a procedural mitigation; not enforceable by DI.

A stronger mitigation would be a Roslyn analyzer that warns when `RegisterNoopKeyRotationScheduler = true` AND the host registers any UI surface that emits `SickBayKeyRotationTriggered` audit events (or a `TriggerKeyRotation` ShipAction handler). This is over-scope for A1 (substrate amendment); flag for a Phase 3a or future analyzer ADR.

**Disposition:** **PASS with hand-off note.** A1.4's §Trust bullet is sufficient documentation; the opt-in flag is a meaningful improvement over default-on. The "user clicks toggle without understanding" residual risk is a known v1 limitation; recommend adding a short note to A1.4 acknowledging the residual risk + naming the future analyzer as the long-term mitigation.

### Finding [Major, MAJ3] — A1.4's "audit-event analogy" reasoning is unclear

A1.4 line 987 reads:

> **`SickBayMedevacSelfApprovalRejected` audit-event analogy:** ADR 0082 §6 already names this audit event. Its precedent — explicit audit emission for a rejected operation — is mirrored here by recommendation only, not by a new audit event: scheduling a Noop does NOT emit a new audit event...

This passage tries to draw a parallel between `MedevacSelfApprovalRejected` (an audit event for a real rejected operation) and the Noop scheduler (which performs no operation). The analogy is broken: rejection in the medevac case is a **decision** the system makes (four-eyes guard); Noop scheduling is the **absence** of a decision. The reader is left unclear whether A1.4 is recommending a new audit event or specifically rejecting one.

The actual disposition (no new audit event; existing `SickBayKeyRotationTriggered` is sufficient because Phase 2 hosts MUST disable the affordance per the §Trust bullet) is correct, but the analogy obscures it.

**Disposition:** **NEEDS-AMENDMENT.** Replace the analogy paragraph with a direct statement:

> **No new audit event for Noop scheduling.** Scheduling a Noop produces no observable effect, so there is no rejected-operation event to emit (compare `SickBayMedevacSelfApprovalRejected`, which records a real rejected medevac decision). Phase 2 hosts that opt into Noop registration MUST disable the `TriggerKeyRotation` UI affordance per the §Trust bullet above; the existing `SickBayKeyRotationTriggered` audit event ships in Phase 3b when the real `IKeyRotationScheduler` lands and is wired ahead of `IKeyRotationScheduler.ScheduleAsync`.

This makes the disposition load-bearing and removes the misleading "analogy."

---

## §5 — Cited-symbol verification (3-direction discipline)

Council ran the full §A0 3-direction sweep on `origin/main 2047718f`:

### Negative-existence (claim: NOT on origin/main pre-A1)

| Citation | Verified |
|---|---|
| `AtmosphereHealth.Unknown` | ✓ `git show origin/main:packages/foundation-sick-bay/AtmosphereHealth.cs` shows 4 values (Green/Yellow/Orange/Red); no `Unknown`. |
| No parallel ADR 0082 PR open | ✓ `gh pr list --search "ADR 0082"` returns PR #700 only (this PR). |
| `SickBayOptions.RegisterNoopKeyRotationScheduler` | ✓ Re-checked `git show origin/main:packages/foundation-sick-bay/SickBayOptions.cs` — only `RegisteredFieldPurposes` + `FallbackPollingInterval` + `RegisterPurpose` exist. No flag yet. |

### Positive-existence (claim: ON origin/main; cited as substrate dependency)

| Citation | Verified |
|---|---|
| `IMissionEnvelopeProvider.GetCurrentAsync(CancellationToken)` returning `ValueTask<MissionEnvelope>` at `Contracts.cs:51` | ✓ |
| `IMissionEnvelopeObserver` at `Contracts.cs:35` | ✓ (line 36 actually; ±1 trivial) |
| `MissionEnvelope` sealed record with 10 dimension properties + `SnapshotAt` + `EnvelopeHash` | ✓ |
| 10 dimension records each with `required ProbeStatus ProbeStatus` | ✓ — Hardware (line 30), User (47), Regulatory (62), Runtime (82), Edition (99), Network (121), TrustAnchor (135), SyncState (153), FormFactor (164), VersionVector (175) — all 10 carry `[JsonConverter(typeof(JsonStringEnumConverter<ProbeStatus>))] public required ProbeStatus ProbeStatus { get; init; }` |
| `ProbeStatus` enum 5 values at `Enums.cs:48-55` | ✓ — `Healthy / Stale / Failed / PartiallyDegraded / Unreachable` (5 values, exactly as cited) |
| `IFeatureForceEnableSurface.ResolveAsync` at `Contracts.cs:45` | ✓ |
| `AtmosphereHealth` enum 4 values at `AtmosphereHealth.cs:11-24` | ✓ pre-A1 (Green/Yellow/Orange/Red); A1 brings to 5 |
| `AtmosphereReadout` positional record (5 params) | ✓ |
| `LabDiagnosticResult` positional record at `LabDiagnosticResult.cs` (6 params: `ProbeName, DimensionId, Status, Degradation, LastRunAt, DiagnosticDetail`); `LastRunAt` is `DateTimeOffset` | ✓ — **and this is the structural-citation drift caught at MAJ1 / M1.1 + M1.2** |
| `SickBayDataProvider.BuildAtmosphereStub` at line 143; returns `AtmosphereHealth.Green` | ✓ — verified line 143-149 |
| `NoopKeyRotationScheduler` at `packages/blocks-sick-bay/NoopKeyRotationScheduler.cs`; `ScheduleAsync` returns `Task.CompletedTask` | ✓ |
| `IKeyRotationScheduler` at `packages/foundation-sick-bay/IKeyRotationScheduler.cs` | ✓ |
| `DegradationKind` enum on `FeatureVerdict` (NOT per-dimension) at `Enums.cs:38-45` | ✓ — `ReadOnly / ReducedSurface / PerformanceLimited / PartiallyHidden / AdvisoryCaveat` |
| `EnvelopeChangeSeverity` enum at `Enums.cs:19-27` | ✓ — `Informational / Warning / Critical / ProbeUnreliable` |
| `ShipRole.IDC` exhaustive-switch precedent at ADR 0082 §5 | ✓ — §5 lines 461-465 document the CS8509 caveat |

### Structural-citation correctness (3rd direction)

| Claim | Verified |
|---|---|
| `IMissionEnvelopeProvider.GetCurrentAsync` returns `ValueTask<MissionEnvelope>` (NOT `Task<MissionEnvelope>`) | ✓ |
| `MissionEnvelope` exposes typed dimension records (NOT a flat probe-result list) | ✓ |
| `DegradationKind` is on `FeatureVerdict`, not per-dimension | ✓ — `Enums.cs:38-45` declares the taxonomy; dimension records carry `ProbeStatus` only |
| `[JsonConverter(typeof(JsonStringEnumConverter))]` is type-level on `AtmosphereHealth` | ✓ — `AtmosphereHealth.cs:10` |
| `LabDiagnosticResult.LastRunAt` is `DateTimeOffset` | ✓ — **addendum §2.5 cites `NodaTime.Instant` — DRIFT, MAJ1/M1.2** |
| `LabDiagnosticResult` is a positional record | ✓ — **addendum §2.5 uses object-initializer syntax — DRIFT, MAJ1/M1.1** |
| Cohort precedent: "DateTimeOffset over NodaTime" per W#46/W#49/W#50/W#54/W#55 | ✓ — ADR 0082 line 360 cites this; addendum §2.5 violates it |

**Net structural-citation finding:** the ADR body's §A1.7 self-audit is honest and correct. The **addendum's §2.5 code sample** is the location of the structural-citation drift. The split between honest-ADR / drifted-addendum is unusual; the council reading suggests the §A1.7 self-audit caught everything except the BuildLab snippet's compile-blockers.

---

## §6 — Per-workstream file + ledger + hand-off addendum consistency

### W#54 per-workstream file

`icm/_state/workstreams/W54-sick-bay-aggregation-surface.md` updated:
- `status_cell` reflects A1 + addendum + Phase 2b unblocked
- "Remaining phases" list adds Phase 2b (~2-3h, between Phase 2 merged and Phase 3a)
- Phase 2b section names the substrate-amendment scope (AtmosphereHealth.Unknown sentinel + opt-in flag) + spec source

**Disposition:** **PASS.** Internally consistent. After HA2 Path B disposition, the Phase 2b row should be edited to drop "Lab projection" from the scope and add a Phase 2c row pending Amendment A2.

### Ledger render

`python3 tools/icm/render-ledger.py --check` exits 0 ✓ (re-verified locally).

### Hand-off addendum internal consistency

`icm/_state/handoffs/sick-bay-stage06-addendum.md`:
- §1 — API drift correction ✓
- §2.1 — AtmosphereHealth.Unknown ✓
- §2.2 — Phase 2 stub migration ✓
- §2.3 — `IMissionEnvelopeProvider` injection ✓
- §2.4 — `BuildAtmosphereAsync` ✓ (Bucket() switch is correct against ProbeStatus enum)
- §2.5 — `BuildLab` ✗ (MAJ1; structural drift; deferred per HA2 Path B)
- §2.6 — `BuildSnapshot` async refactor ✓ (calls `BuildAtmosphereFromEnvelope` not the prior Async name; minor naming inconsistency noted in §3 but tolerable)
- §2.7 — `IMissionEnvelopeObserver` wire ✓
- §2.8 — `RegisterNoopKeyRotationScheduler` flag ✓

**Disposition:** **NEEDS-AMENDMENT.** §2.5 needs the MAJ1 fix; §2.6 reference to `BuildAtmosphereFromEnvelope` (vs §2.4's `BuildAtmosphereAsync`) is a minor naming thread that should be reconciled in the same edit.

---

## §7 — Phase 2 stub-flip timing

Question: PR #695 shipped `AtmosphereHealth.Green` stub. Does A1 specify when the stub flips to `Unknown`?

Answer: **YES, A1.8 is explicit.** Implementation checklist line 1049:

> Update `packages/blocks-sick-bay/SickBayDataProvider.cs` `BuildAtmosphereStub` to return `AtmosphereHealth.Unknown` until the real provider is wired (this is a one-line stub fix; the real projection lands in the same PR via `BuildAtmosphere` below).

The Phase 2b PR ships **both** the stub-flip and the real projection in one PR. There is no transitional state where the stub returns Green AND the real provider is wired (or vice versa). The cohort coordination concern is closed.

**Disposition:** **PASS.** No follow-up needed.

### Finding [Minor, MIN4] — Race window during first envelope projection

Council §Skeptical Implementer flag: between `IMissionEnvelopeProvider` injection and the first `GetCurrentAsync(ct)` resolution, a snapshot may be requested. Per addendum §2.4 + §2.6, `BuildAtmosphereAsync` returns `Unknown` if the provider throws or returns null, and the snapshot pipeline awaits the envelope before producing the readout. So there is no observable Green→Unknown flicker.

The addendum §2.7 wires `IMissionEnvelopeObserver` for push-driven invalidation. On the FIRST `OnChangedAsync` invocation after subscribe, the snapshot transitions from `Unknown` (initial subscribe state if envelope wasn't ready) → derived health. The aria-live transition rule (A1.3) applies here.

**Edge case:** if `IMissionEnvelopeProvider.GetCurrentAsync` resolves synchronously during DI (which `ValueTask` permits when the result is cached), the first snapshot may already have a derived health, never showing Unknown. This is fine — Unknown is not a state operators must observe; it is a sentinel for "not yet known to me."

**Disposition:** **PASS with hand-off note.** Phase 2b's `BuildAtmosphere_returns_Unknown_when_envelope_provider_is_null` test in §3 does not exhaustively cover the "provider returns synchronously" path. Recommend adding `BuildAtmosphere_returns_derived_when_envelope_returns_synchronously` to the test list to pin the no-flicker invariant.

---

## §8 — Verdict + cohort-batting update

### Verdict: NEEDS-AMENDMENT

Substrate decisions are sound:
- `AtmosphereHealth.Unknown` ordinal-0 is wire-safe + source-safe + §Trust-positive — PASS
- `RegisterNoopKeyRotationScheduler` opt-in flag default-false — PASS
- ProbeStatus → severity-bucket projection table at A1.2.1 is internally consistent + matches substrate — PASS
- A1.5 host-scoped Atmosphere is documented as v1 limitation — PASS (with §Trust bullet addition required)
- API drift correction in addendum §1 — PASS
- Per-workstream file + ledger render — PASS

Required amendments before merge:

| ID | Severity | Location | Action |
|---|---|---|---|
| HA2 (resolve Path B) | Blocking | ADR 0082 A1.10 + A1.8 + addendum §2.5 | Drop Lab projection from Phase 2b; file Amendment A2 widening `LabDiagnosticResult.Degradation` to `DegradationKind?`. |
| MAJ1 (M1.1 + M1.2) | Major | addendum §2.5 | Replace object-initializer syntax with constructor-style; replace `NodaTime.Instant.FromDateTimeOffset(capturedAt)` with `capturedAt`. (After HA2 Path B, this snippet is for Phase 2c; correctness still required.) |
| MAJ2 | Major | ADR 0082 §Trust impact (after line 983) | Add §Trust bullet for host-scoped Atmosphere in multi-tenant Bridge deployments. |
| MAJ3 | Major | ADR 0082 A1.4 line 987 | Replace the obscure `MedevacSelfApprovalRejected` "analogy" paragraph with a direct statement (no new audit event; opt-in hosts MUST disable affordance). |
| MIN1 | Minor | ADR 0082 A1.7 line 1017 | Reorder the dimension-record list to match `MissionEnvelope` declaration order. |
| MIN2 | Minor | addendum §6 quick-reference | Add hand-off note for Phase 3a Blazor: pin aria-live + non-color-marker tests. |
| MIN3 | Minor | ADR 0082 A1.4 (after the new direct-statement paragraph from MAJ3) | Acknowledge residual "user clicks toggle without understanding" risk + name future analyzer as long-term mitigation. |
| MIN4 | Minor | addendum §3 test list | Add `BuildAtmosphere_returns_derived_when_envelope_returns_synchronously` to pin no-flicker invariant. |
| MEC1 | Mechanical | (already resolved) | No action. |

**After amendments:** verdict flips to **PASS** and Phase 2b PR may auto-merge per ADR 0069 D1 cadence (with HA2 Path B implementation: Lab projection deferred to Phase 2c after Amendment A2).

### Cohort batting average update

Pre-review: 41-of-41 substrate amendments needed council fixes.

This review: **5 of 8 findings are surface-amendable** (MAJ2 + MAJ3 + MIN1 + MIN2 + MIN3 + MIN4 are documentation tightening; MAJ1 is a code-snippet structural fix; HA2 is a design-judgment call that materially changes Phase 2b scope). Combined into the cohort metric: this is one substrate amendment that needed council fixes, taking the count to **42-of-42**.

**Cohort discipline observation:** the §A1.7 self-audit in the ADR body is the most thorough we have seen in this cohort (verified 12+ symbol citations on origin/main; called out structural-citation correctness in 6 sub-points including the `DateTimeOffset` / `NodaTime` cohort precedent). The drift was confined to the addendum's code sample at §2.5 — author appears to have copy-edited from a different cohort's NodaTime convention without re-checking against `LabDiagnosticResult`'s actual shape. **Lesson for future amendments:** when a code sample crosses package boundaries (here: foundation-sick-bay's `LabDiagnosticResult` shape consumed by blocks-sick-bay's `BuildLab`), the §A0 self-audit should expand to the addendum's code samples explicitly, not just the ADR body's prose.

### Halt-conditions check

| Halt | Triggered? |
|---|---|
| HA2 disposition has no clear cohort-correct answer | NO — Path B is structurally aligned with A1.3's Unknown-sentinel pattern; rejection of Path A is a §Trust-driven decision, not a preference call. |
| ProbeStatus enum on origin/main has fewer than 5 values OR different shape | NO — verified 5 values (Healthy/Stale/Failed/PartiallyDegraded/Unreachable) matching A1's citation. |
| AtmosphereHealth.Unknown ordinal-0 introduces wire-incompat OR default-init regression | NO — wire-safe (JsonStringEnumConverter by name); default-init flip is intentional and §Trust-positive. |
| Per-workstream file or hand-off addendum has internal inconsistency that material-impacts implementer | YES — the MAJ1 finding in addendum §2.5 would block Phase 2b compilation. Council's NEEDS-AMENDMENT verdict captures this; not a hard halt because the fix is mechanical. |

**No hard halts triggered.** Council disposes NEEDS-AMENDMENT with the action list above.

---

## Appendix — Council perspective summary

### Outside Observer

The amendment correctly identifies and corrects three real issues (API drift; ProbeStatus-vs-DegradationKind taxonomy confusion; misleading-success Phase 2 stubs). The §A1.7 self-audit is exemplary. The author has internalized the cohort discipline: 3-direction structural-citation, AP-21 cited-symbol verification, §Trust-first reasoning. The single drift is in a code sample copied from the wrong cohort convention.

### Pessimistic Risk Assessor

The default-init flip from Green → Unknown is a behavior change. Verified no consumer relies on `default(AtmosphereHealth) == Green`. The Lab `Degradation` AdvisoryCaveat fallback is the highest-risk decision in A1; rejected per HA2 Path B. The opt-in NoopKeyRotation flag is meaningful but residual "click without understanding" risk persists; documented as known v1 limitation.

### Pedantic Lawyer

A1.5's host-scoped Atmosphere requires a §Trust bullet (added per MAJ2) to bind hosted-multi-tenant operators legally to the cross-tenant-readout contract. A1.4's Noop registration guidance is an obligation (`MUST NOT register` in user-visible-toast environments) — the opt-in flag converts this from documentation to enforced API. A1.4's audit-event "analogy" paragraph (MAJ3) muddies what the obligation IS; replace with direct statement.

### Skeptical Implementer

The addendum's §2.5 BuildLab code sample as written produces 2 compile errors (CS7036 + CS0029). The §2.6 refactor names `BuildAtmosphereFromEnvelope` while §2.4 names `BuildAtmosphereAsync`; reconcile. The aria-live transition (A1.3) is implementable but the test list (§3) does not exercise it; flag for Phase 3a Blazor hand-off. The race-window concern around first-envelope projection is bounded by Unknown sentinel semantics; no flicker.

---

**End of council review.**
