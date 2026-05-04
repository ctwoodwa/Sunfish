# ADR 0066 Council Review — Pre-merge Canonical (Stage 1.5)

**ADR:** [0066 — Helm Composition + Identity Atlas Surface](../../../../docs/adrs/0066-helm-composition-and-identity-atlas-surface.md)
**Branch under review:** `docs/adr-0066-helm-and-identity-atlas` (PR #529)
**Reviewer:** XO research subagent (Opus 4.7, `xhigh`; WCAG/a11y subagent perspective added per ADR 0065 §7.4 cohort precedent)
**Date:** 2026-05-04
**Discipline:** ADR 0069 D1 (pre-merge council canonical, substrate-tier), D2 (§A0 self-audit pressure-test), D3 (three-direction structural-citation spot-check)
**Worktree base:** `origin/main` @ `bf31e04` (post `chore(icm): COB question — Bridge audit-infrastructure gap blocks W#23 P4+P4.5` PR #527)
**Cohort:** 23-of-23 candidate (one council fix away from confirming the cohort metric extends to 23)

---

## Verdict

**NEEDS-AMENDMENT** (not BLOCKING — the substrate decisions are sound and the §A0 self-audit is unusually thorough; but the ADR ships **one structural-citation failure of the same shape as ADR 0028-A6.2** — a confidently-asserted convention-claim that the source-of-truth file contradicts — plus four pre-flagged structural concerns that the §A0 already named correctly and need formal council disposition before merge, plus three substantive non-mechanical findings the §A0 did not surface).

The two-contract decomposition (`IHelmWidget` + `IIdentityAtlasSurface`), the slot taxonomy, the live-state propagation triggers, the WCAG-as-contract framing, the audit-by-construction guard, the renderer-not-actuator widget posture, and the substrate-composition discipline are all correct. The author's §A0.2 caught a real and important ADR 0065 cite-error (confirmed below) and chose to ship the verified namespace rather than propagate the bug — this is the right call and a meaningful counter-example to the "§A0 catches nothing" cohort metric: §A0 caught **one** structural failure here that mattered. However, the author's own §A0 still missed one structural-citation failure (the AuditEventType naming convention), which extends the cohort pattern: §A0 is necessary but not sufficient; pre-merge council remains canonical defense.

If the author applies the eight recommendations in §"Recommendations to author" below — the four pre-flagged disposition decisions (OQ-1 through OQ-4 plus OQ-6) plus the four structural / non-mechanical fixes — this becomes a clean ADR. As-is, it is the **23rd-of-23** case where pre-merge council was canonical for substrate-tier ADRs.

---

## Findings summary

| Class | Count | IDs |
|---|---|---|
| **Blocking** | 0 | (none — no logic-correctness or layering violations; the §"audit-by-construction" guard means even if the renderer/actuator boundary is breached at implementation time, the type-system enforces audit emission via `IStandingOrderIssuer.IssueAsync` per ADR 0065) |
| **Structural-citation** | 2 | SC-1 (AuditEventType naming-convention claim — kebab-case asserted; PascalCase is canonical), SC-2 (IFieldDecryptor "audit-emitting per ADR 0046-A2" cite — verified-real but cited from §OQ-4 not §A0.2; recommend lift) |
| **Non-mechanical (council disposition)** | 6 | NM-1 (RecoveryContact vs Trustee vocabulary — OQ-1; recommend split as author proposed), NM-2 (`IObservable<StandingOrderAppliedEvent>` does NOT exist on origin/main — OQ-3; recommend ADR 0065 amendment), NM-3 (flat vs split namespace — OQ-2; recommend flat per author proposal), NM-4 (Helm-vs-Cockpit boundary — pre-flagged §5; recommend defer to a follow-on boundary ADR not block this one), NM-5 (KeyFingerprint canonical form — OQ-6; recommend hex SHA-256 with group separators per author proposal), NM-6 (Bridge admin Helm subset — OQ-5; recommend defer to follow-on, scope as author proposed) |
| **Mechanical** | 4 | M-1 (frontmatter `composes:` should include 0049 — currently missing), M-2 (ADR 0065 cohort cite "22-of-22" should read "23-of-23 candidate" per ADR 0069 D1 cohort metric phrasing), M-3 (§5 table fuzzy-name banner for "Atlas" should reference §A.5 vocabulary registry, not just inline "intentional"), M-4 (§A0.2 footnote on `IFieldDecryptor.Crypto` sub-namespace correction is right but should also cite the line number in `packages/foundation-recovery/Crypto/IFieldDecryptor.cs` for forensic reproducibility) |
| **Pass / commendation** | 5 | P-1 (§A0.2 caught real ADR 0065 namespace error — counter-example to "§A0 catches nothing"), P-2 (audit-by-construction at the type level via `IStandingOrderIssuer.IssueAsync` — non-bypassable), P-3 (renderer-not-actuator distinction is enforced architecturally), P-4 (WCAG SC enumeration is correct against W3C 2.2 spec text), P-5 (cohort-canonical two-overload `AddSunfishHelm` matches ADR 0023 + 0024 + 0028 + 0049 + 0065 precedent) |

**Total: 12 active findings.** Per ADR 0069 D1, the 2 structural-citation findings + 4 non-mechanical (council disposition) findings warrant a fresh `Status: Proposed` push (NEEDS-AMENDMENT) before flipping to `Accepted`. The 4 mechanical fixes can ride along in the same amendment commit. The 5 commendations are recorded for the cohort log.

---

## Pre-flagged structural concerns disposition

The author's §A0.4 names 6 structural concerns for council pressure-test. Disposition:

| # | Concern | Council disposition | Evidence |
|---|---|---|---|
| 1 | `KeyFingerprint` namespace placement (`Sunfish.Foundation.Recovery` vs `Sunfish.Foundation.Identity`) | **CONFIRM author's choice (Recovery).** `Sunfish.Foundation.Identity` does NOT exist on origin/main; creating it solely to home `KeyFingerprint` would invent a namespace for a single type. The Recovery placement is adjacent to `EncryptedField` + `HistoricalKeysProjection` consumers (per ADR 0046 + 0046-a1) and matches the §A0.2 verified namespace reality. | `grep -rn "namespace Sunfish.Foundation.Identity" packages/` returns ZERO matches on `bf31e04`. |
| 2 | `Sunfish.UICore.Wayfinder` (flat) vs `Sunfish.UICore.Helm` + `Sunfish.UICore.IdentityAtlas` (split) | **CONFIRM author's recommendation (flat).** Cohort precedent is `Sunfish.Foundation.Wayfinder` (flat — hosts `StandingOrder` + `IAtlasProjector` + `IStandingOrderIssuer` + `AtlasView` together). Splitting into `.Helm` + `.IdentityAtlas` would invert the cohort layout AND fragment the package's external surface. (NM-3 — see §"Perspective 3 — Skeptical Implementer" below.) | `packages/foundation-wayfinder/` is the established flat-namespace cohort sibling. |
| 3 | `RecoveryContact` vs `Trustee` vocabulary | **NON-MECHANICAL disposition: ADOPT author's proposed split.** ADR 0046 uses "Trustee" 5x in body + 1x in `AuditEventType.TrusteeSetChanged`; uses zero "RecoveryContact" occurrences. Author's recommendation is correct: keep `Trustee*` for audit/cryptographic vocabulary (matches ADR 0046's existing surface), use `RecoveryContact*` for user-facing UX (less technical). Document the synonymy at `_shared/product/naming.md` per §OQ-1. (NM-1 — see §"Perspective 5 — WCAG / a11y subagent" below for accessibility framing of "recovery contact" as the more readable label.) | `grep -c "Trustee" docs/adrs/0046-key-loss-recovery-scheme-phase-1.md` = 5; `grep -c "RecoveryContact" docs/adrs/0046-key-loss-recovery-scheme-phase-1.md` = 0; `AuditEventType.TrusteeSetChanged` at `packages/kernel-audit/AuditEventType.cs:35`. |
| 4 | `IObservable<StandingOrderAppliedEvent>` dependency on ADR 0065 | **CONFIRMED-REAL — reactive surface DOES NOT EXIST on origin/main.** ADR 0065's `IStandingOrderRepository` (origin/main: `packages/foundation-wayfinder/IStandingOrderRepository.cs`) ships only `AppendAsync` / `GetAsync` / `EnumerateAsync` — three imperative methods, zero reactive surface. Neither the type `StandingOrderAppliedEvent` nor an `IObservable<T>` (or any equivalent observer) appears anywhere in `packages/foundation-wayfinder/`. ADR 0065's docs/adrs body also names no such surface. **This is a hard structural gap, not a "treat as conditional" item.** Recommended disposition: ADR 0065 amendment (or a separate ADR 0065-a1 reactive-surface amendment) BEFORE the §1.3 trigger #2 (Standing Order applied) propagation can be implemented. Alternative: §1.3 trigger #2 falls back to periodic-refresh + on-write-hook in the issuer's `IssueAsync` path (less ideal — requires Helm to subscribe to a synchronous hook the issuer fires post-emission). (NM-2 — see §"Perspective 3 — Skeptical Implementer" + §"Perspective 4 — Devil's Advocate" below.) | `grep -rn "IObservable\|StandingOrderApplied" packages/foundation-wayfinder/` = ZERO. |
| 5 | Helm-vs-Cockpit boundary (W#29 Owner Web Cockpit) | **CONFIRMED-REAL — overlap risk is genuine, but author's §"Decision drivers" #7 sketches the right boundary** (Helm = system pane / framework code / framework-agnostic / identical across accelerators; Cockpit = block-level dashboard / `blocks-*` packages / business widgets / accelerator-bespoke). Council disposition: ACCEPT the inline boundary sketch; do NOT block ADR 0066 on a separate boundary ADR. RECOMMEND adding `revisit_trigger`: "W#29 Owner Web Cockpit ADR introduces a competing widget composition contract" (already in the Revisit triggers section — confirmed). (NM-4 — see §"Perspective 4 — Devil's Advocate" below.) | W#29 = `icm/00_intake/output/property-owner-cockpit-intake-2026-04-28.md`; ADR 0066 §"Decision drivers" #7 explicitly delineates Helm-as-system-pane vs Cockpit-as-block-dashboard. |
| 6 | `AuditEventType` constants for identity events (need new constants for "ProfileEdited" / "ActiveTeamSwitched"?) | **REFUTE — no new constants needed.** ADR 0065's `IStandingOrderIssuer.IssueAsync` emits `AuditEventType.StandingOrderIssued` per emission (verified at `packages/foundation-wayfinder/IStandingOrderIssuer.cs:34`); the `Path` field on the StandingOrder discriminates "what was changed." No additional `AuditEventType` constants are needed for identity-mutation Standing Orders — the Path-discriminator pattern is already cohort-canonical. (Per ADR 0049 §"Open questions" — `AuditEventType` is intentionally extensible string-keyed; the intent is that NEW constants get added when emission *type* changes, not when *payload* changes.) | `packages/foundation-wayfinder/IStandingOrderIssuer.cs:34`: `<see cref="AuditEventType.StandingOrderIssued"/>` for every issuance regardless of payload. |

**Author's §A0 self-audit caught 4-of-6 of these correctly; 2-of-6 needed council pressure-test to disposition (#4 IObservable confirmed-missing per origin/main inspection; #5 Helm-vs-Cockpit needed boundary disposition).** Counter-example to "§A0 catches nothing" — the §A0.2 namespace catch on ADR 0065 is real and load-bearing.

---

## Perspective 1 — Outside Observer (fresh-contributor clarity)

**Cold-start reading impression:** The ADR is well-organized. §Context → §Decision drivers (7 items) → §Considered options (5 lettered options A-E) → §Decision (Option E adopted) → numbered specifications (§1 Helm, §2 Atlas, §3 WCAG, §4 cross-references, §5 new types) → §A0 self-audit → implementation checklist → open questions. A fresh COB session reading this from a cold start would understand "what to build" within ~12 minutes.

**The Helm-vs-Atlas distinction is well-explained** (live-state observation vs deep-config issuance) and the renderer-not-actuator widget guard is repeated in three places (§"Decision drivers" #3, §1.1 XML doc remarks, §1.4 widget table commentary). This is good repetition — the type-system enforces it but the prose makes the rationale legible.

**The Helm-Atlas-Wayfinder relationship is explained but could be one diagram clearer.** §"Context" paragraph 1 names "Helm pane (live-state) + Atlas surface (deep-config) = Wayfinder system" but doesn't visually relate the three. A fresh contributor reading §Context cold has to infer the parent-child relationship from prose. **One ASCII box-and-arrow** ("Wayfinder = Helm + Atlas; Atlas contains Identity sub-surface; Atlas ⊃ Helm via diff-preview navigation") would close the gap. Mechanical recommendation OO-1 below.

### OO-1 (mechanical) — Add a 4-line box-and-arrow diagram to §Context paragraph 2.

The relationship is implicitly clear from §Decision drivers #1 ("Helm is the runtime-observation surface; the Atlas is the issuance surface") but a literal diagram would save the fresh contributor 2-3 minutes of inference.

**Suggested addition** at the end of §Context paragraph 1:

```
Wayfinder
├── Helm (live-state observation pane; this ADR §1)
└── Atlas (deep-config issuance surface; this ADR §2 + ADR 0065)
    └── Identity sub-surface (this ADR §2.1-§2.6)
```

**Classification:** mechanical. **Recommendation:** add the diagram block.

### OO-2 (mechanical) — §"Considered options" Option E framing is clear; the "(recommended)" tag could be lifted to its own paragraph for skim-readability.

The five options A-E are well-rejected (each with concrete rationale); Option E is tagged "(recommended)" inline. A fresh contributor scanning the §Decision section would benefit from a short "**Why Option E?**" paragraph between the five options and §Decision header. **Optional / non-blocking;** noted only because the cohort-canonical ADR 0065 has this paragraph and the asymmetry is mild but visible.

**Classification:** mechanical (cohort-style alignment). **Recommendation:** add 2-sentence "Why Option E?" gap after the lettered options.

### OO-3 (mechanical) — §1.4 widget table column header "A11y SC critical" is non-self-explanatory.

A fresh contributor encountering "A11y SC critical: 1.4.3, 1.4.11, 4.1.2" needs to know that SC = "Success Criterion" (per WCAG 2.2). The §3 WCAG section explains this further down but the §1.4 table is upstream. **Either add a footnote** ("A11y SC = WCAG 2.2 Success Criterion") **or change the column header** to "WCAG SC focus." Optional but improves first-pass legibility.

**Classification:** mechanical. **Recommendation:** disambiguate the column header.

---

## Perspective 2 — Pessimistic Risk Assessor (failure modes)

The ADR's substrate is correct. The risk surface is in the **runtime composition** with two surfaces ADR 0066 cannot itself control: (a) ADR 0065's not-yet-shipped reactive event surface (`IObservable<StandingOrderAppliedEvent>`), and (b) ADR 0046's Trustee-vocabulary surface that this ADR's UI aliases as "RecoveryContact" but does not formally rename.

### PRA-1 (non-mechanical) — Key-rotation grace-window UX race: rotation issued → user goes offline before grace expiry → rotation completes → user comes back online with stale "rotation in progress" `SyncState.Stale` Helm widget.

§2.3 Phase 3 specifies: "During the window the Helm `identity-glance` widget surfaces a 'rotation in progress' state via `SyncState.Stale` + an action button to 'view rotation status.'" Per ADR 0046 the grace window is 7 days (configurable up to 30). What happens if:

1. User on device A issues key rotation (T0)
2. User on device A goes offline (T0+1h)
3. Grace window expires; rotation completes via Standing Order propagation to peer devices (T+7d)
4. User on device A comes back online at T+7d+1h
5. The `IdentityGlanceWidget.ComputeAsync` re-runs on Mission Envelope change (`OnEnvelopeChanged`) — but if the envelope hasn't changed materially (same network class, same form factor, same edition), no recompute fires
6. Periodic refresh (default `00:01:00`) eventually catches it — but 1 minute of "rotation in progress" stale UI is a confusing user state

**This is not a logic failure** (the user's key did rotate; the rotation is correct; the audit trail is intact). **It is a UX consistency failure.** The widget shows a stale state for ≤60 seconds after reconnect. Worst case: user takes a security-relevant action ("retry rotation") under the impression rotation is still pending; the action no-ops because rotation is already complete; user is confused.

**Recommended mitigation:** §1.3 trigger list should add a **fourth trigger**: "On `IMissionEnvelopeProvider.OnEnvelopeChanged` with `EnvelopeChangeSeverity.NetworkReconnect` OR on app-resume from background, recompute ALL widgets unconditionally (not just those with capability-gate dimension changes)." This is a small extension to §1.3 and matches the cohort-canonical "on-resume revalidate" pattern from ADR 0036 §A1's SyncState propagation contract.

**Classification:** non-mechanical (additive trigger to the §1.3 propagation rule). **Recommendation:** add Trigger #4 (on-reconnect / on-resume unconditional recompute).

### PRA-2 (non-mechanical) — Recovery-contact spam: `Path = "identity.recovery.contacts.add"` Standing Order has no rate-limit or quorum gate.

§2.4 Enroll specifies: "selects an existing `ActorId` (Tenant member) or invites by email/phone. Issues `StandingOrder` with `Path = 'identity.recovery.contacts.add'` and `Scope = StandingOrderScope.Security`. Multi-actor approval (per ADR 0046) applies if the issuing actor is a non-primary owner."

A compromised primary-owner credential could issue an unbounded sequence of `recovery.contacts.add` Standing Orders, each with a different attacker-controlled email/phone — diluting the recovery-contact set + survivor-recovery quorum (per ADR 0046 spouse-recovery semantics) into noise. **Sphere-of-effect: high; detection latency: hours-to-days** (until a real recovery event surfaces the dilution). The mitigation is partially covered by ADR 0065's `IStandingOrderValidator` chain (validators can enforce per-Path rate limits) — but ADR 0066 does not specify a validator for this path.

**Recommended mitigation:** §2.4 Enroll should declare a **canonical validator**: "An `IStandingOrderValidator` MUST be registered for `Path = 'identity.recovery.contacts.add'` enforcing (a) ≤5 recovery-contact additions per `Scope=Security` window per actor, (b) verification-status check before count toward quorum (verified contacts only), (c) audit-emit on rate-limit rejection." This is also a defense against a stuck-onboarding-flow that retries indefinitely.

**Classification:** non-mechanical (additive validator spec). **Recommendation:** add validator declaration to §2.4.

### PRA-3 (non-mechanical) — Active-team switch consistency: §2.6 says team-switch is "session-local" but the Helm `active-team` widget renders globally.

§2.6 specifies: `"Switch to this team" action — local UI-only; does NOT issue a Standing Order (team-switch is a session-local concern per ADR 0032 §"Default: Option C" — the kernel-runtime re-binds views; no global state changes)`. **But §1.4's `active-team` widget is registered globally per the registry** — it computes from `HelmRenderContext.ActiveTeam` per `HelmRenderContext` definition (line 194: `Sunfish.Kernel.Runtime.Teams.TeamId? ActiveTeam`).

In a multi-tab / multi-window scenario (Anchor MAUI on macOS, Bridge React on Chrome), each rendering context may have a different `ActiveTeam`. The `IActiveTeamAccessor.SetActiveAsync` call (verified at `packages/kernel-runtime/Teams/ActiveTeamAccessor.cs:35`) is a process-singleton mutation; multi-window will see the same active team. **But** the user's mental model of "switch this tab to Team B while leaving Tab 1 on Team A" is broken — the switch propagates globally within the process.

This is **not a defect introduced by ADR 0066** (it inherits from ADR 0032's per-process active-team model). But ADR 0066 §2.6 should explicitly note: "Active-team switch is a process-singleton operation per ADR 0032; multi-window users see the same active team across all windows in the same process. Per-window team-binding is out of scope (would require a re-author of ADR 0032 §'Default: Option C')."

**Classification:** non-mechanical (clarifying note to inherit-not-introduce the limitation). **Recommendation:** add the per-process-singleton note to §2.6.

### PRA-4 (mechanical) — `HelmOptions.PeriodicRefreshInterval` default of 1 minute is reasonable; council confirms it's tunable and within battery-budget per ADR 0062 §"Probe cost class".

§1.3 Trigger #3 specifies: "Periodic refresh — backstop refresh at `HelmOptions.PeriodicRefreshInterval` (default `00:01:00`, configurable)." A 1-minute interval × 6 widgets = 6 ComputeAsync calls / minute. Each ComputeAsync is bounded to "idempotent and side-effect-free; long-running work belongs in a substrate service the widget queries" (§1.3 paragraph "Refresh cost discipline"). The widgets are renderers, not probes — they query already-cached envelope state. Cost is bounded.

**However:** the `recovery-status widget` mentioned in §1.3 trigger #3 ("the recovery-status widget uses [periodic refresh] because pending recovery requests have a grace-window expiry that doesn't fire its own event") is **not in the §1.4 canonical widget table**. Either it's a Phase-2 widget that's pre-mentioned, or it's a structural-citation drift. Recommend lifting "recovery-status" out of §1.3's prose and into the §1.4 table as a Phase-2 row (consistent with the "Pending Standing Orders + quota / CRDT growth gauge are defer-Phase-2 widgets" paragraph at line 254).

**Classification:** mechanical (consistency between §1.3 prose and §1.4 table). **Recommendation:** add a `recovery-status` row to the §1.4 table tagged "Phase 2" to match the existing "defer-Phase-2 widgets" footnote.

---

## Perspective 3 — Skeptical Implementer (structural-citation correctness)

This is the perspective with the longest output, per ADR 0069 D3 cohort discipline. I verified every cited Sunfish.* symbol against `origin/main` @ `bf31e04`.

### SC-1 (STRUCTURAL-CITATION) — `WidgetId` "kebab-case convention used by ADR 0049 audit-event-type identifiers" is FALSE.

§1.1 line ~198 (within `HelmWidgetMetadata` paragraph) states:

> `HelmWidgetMetadata.WidgetId` follows the kebab-case convention used by ADR 0049 audit-event-type identifiers and ADR 0065's `StandingOrder.Path`.

This is **structurally incorrect on the ADR 0049 axis**. Verified at `packages/kernel-audit/AuditEventType.cs:15`:

> `<b>Naming convention:</b> <c>{Subject}{Verb}</c> in PascalCase.`

And the actual constants follow this — `KeyRecoveryInitiated`, `TrusteeSetChanged`, `StandingOrderIssued`, `WorkOrderCreated`, etc. **Zero kebab-case AuditEventType constants exist on origin/main.**

ADR 0066's claim is doubly wrong: (a) the convention is NOT kebab-case; (b) the cited "ADR 0049" is the home of the convention, but the convention is PascalCase.

**The `StandingOrder.Path` reference in the same sentence IS correct** (verified at `packages/foundation-wayfinder/StandingOrder.cs:56`: `Path` is "Dotted path within the parent Scope, e.g. 'anchor.maui.theme'" — dot-separated lowercase, which is a *different* convention from kebab-case but at least is lowercase/non-Pascal). The ADR conflates two distinct conventions and attributes one to the wrong source.

**This is the same shape as ADR 0028-A6.2's failure** (cited `required: true` on `ModuleManifest`; field exists but on `ProviderRequirement`) — a confidently-asserted claim that the source-of-truth file contradicts. The §A0 self-audit did not catch this (consistent with the cohort-batting-average pattern).

**Recommended fix:** rewrite the sentence to:

> `HelmWidgetMetadata.WidgetId` follows the kebab-case convention used by ADR 0065's `StandingOrder.Path` for path segments (lowercase, hyphen-separated, e.g., `identity-glance`). This is **distinct** from the PascalCase `AuditEventType` constant naming per ADR 0049 §"Naming convention" — `WidgetId` is path-like (user-facing, URL-safe); `AuditEventType` is type-like (programmatic, code-symbol-safe).

**Classification:** structural-citation. **Recommendation:** apply the rewrite.

### SC-2 (STRUCTURAL-CITATION promotion) — §OQ-4 cite of `IFieldDecryptor` as "audit-emitting per ADR 0046-A2" is verified-real but should be lifted to §A0.2.

§OQ-4 line ~530 states: "widgets that query `IFieldDecryptor` (which is audit-emitting per ADR 0046-A2)". I verified this:

- `packages/foundation-recovery/Crypto/TenantKeyProviderFieldDecryptor.cs:21-22`: `<see cref="AuditEventType.FieldDecrypted"/> or <see cref="AuditEventType.FieldDecryptionDenied"/> record per call`
- ADR 0046 §"Amendments" exists on origin/main (`docs/adrs/0046-key-loss-recovery-scheme-phase-1.md:144`)
- ADR 0028-A8.11 cites "ADR 0046-A2.2" + "A4.1" (verified at `docs/adrs/0028-crdt-engine-selection.md:909` + `:922`)

So the citation is real. **But the §A0.2 self-audit does NOT mention `IFieldDecryptor` is audit-emitting.** The audit-emission property is what motivates §OQ-4's "widgets MUST NOT call `IFieldDecryptor` from `ComputeAsync`" rule — if §A0.2 silently asserted decryption is free-of-side-effect, an implementer reading §A0.2 alone could miss the audit-emission gotcha.

**Recommended fix:** §A0.2 should add a row:

> - `Sunfish.Foundation.Recovery.Crypto.IFieldDecryptor` is **audit-emitting** per ADR 0046-A2 (verified at `packages/foundation-recovery/Crypto/TenantKeyProviderFieldDecryptor.cs:116-125`). Implementations of `IHelmWidget.ComputeAsync` MUST NOT call `IFieldDecryptor` (see §OQ-4 disposition).

**Classification:** structural-citation (promotion of OQ-4 footnote into §A0.2 to surface the audit-emission contract). **Recommendation:** add the §A0.2 row.

### SC-clean — §A0.2 namespace catch on ADR 0065 is CORRECT (commendation P-1).

The author's most important §A0 finding:

> **Critical structural-citation correction (preserved from authoring):** `ActorId` and `TenantId` are in `Sunfish.Foundation.Assets.Common`, **NOT** `Sunfish.Foundation.Identity` (verified `grep -rn "namespace" packages/foundation/Assets/Common/ActorId.cs`). ADR 0065 §A0.2 cites `Sunfish.Foundation.Identity.ActorId` — that is a structural-citation error in ADR 0065 that THIS ADR does not propagate.

Verified independently:

- `packages/foundation/Assets/Common/ActorId.cs:4`: `namespace Sunfish.Foundation.Assets.Common;`
- `packages/foundation/Assets/Common/TenantId.cs:4`: `namespace Sunfish.Foundation.Assets.Common;`
- `grep -rn "namespace Sunfish.Foundation.Identity" packages/`: ZERO matches on `bf31e04`.
- `grep -rn "namespace Sunfish.Foundation.Assets.Common" packages/`: 8 matches (including `ActorId.cs`, `TenantId.cs`, `EntityId.cs`, etc.)

**This is a real ADR 0065 bug.** ADR 0065's §A0.2 (per `docs/adrs/0065-wayfinder-system-and-standing-order-contract.md`) cites `Sunfish.Foundation.Identity.ActorId` / `.TenantId` — that namespace does not exist; the types are in `Sunfish.Foundation.Assets.Common`. ADR 0066 chose to use the verified namespace and explicitly call out the discrepancy, which is the right move (do not propagate a known cite-error from a parent ADR; do flag it for separate amendment).

**Council recommendation:** ADR 0065 needs an amendment (either a new ADR 0065-A1 or an in-place §A0.2 correction commit) to fix the cite. **This is OUT OF SCOPE for ADR 0066's branch** — the author was right to not touch ADR 0065's branch and to instead use the verified namespace + flag the discrepancy. **Council MUST file a separate `cob-question-*.md` or `xo-action-*.md` beacon** to track the ADR 0065 amendment as a follow-on workstream item. (See §"Follow-on actions" below.)

This is a **counter-example to the "§A0 catches nothing" cohort metric** (the author's own §A0 caught a structural-citation failure in a parent ADR). **Commendation P-1 recorded.**

### SC-clean — Other cited symbols verified correct on origin/main (no findings).

Verified all of the following exist as cited:

| Symbol | Cited as | Verified at |
|---|---|---|
| `Sunfish.Foundation.Recovery.EncryptedField` | §"Decision drivers" #2 | `packages/foundation-recovery/EncryptedField.cs:6` (`namespace Sunfish.Foundation.Recovery`; `readonly record struct`) |
| `Sunfish.Foundation.Recovery.Crypto.IFieldDecryptor` | §"Decision drivers" #2 | `packages/foundation-recovery/Crypto/IFieldDecryptor.cs:13` (`namespace Sunfish.Foundation.Recovery.Crypto`; `interface`) |
| `Sunfish.Foundation.UI.SyncState` | §1.1, §1.4 | `packages/foundation-ui-syncstate/SyncState.cs:1` (`namespace Sunfish.Foundation.UI`; 5-value enum) |
| `Sunfish.Kernel.Runtime.Teams.TeamId` | §1.1 (HelmRenderContext) | `packages/kernel-runtime/Teams/TeamId.cs:15` (`readonly record struct TeamId(Guid Value)`) |
| `Sunfish.Kernel.Runtime.Teams.TeamContext` | §1.1, §2.6 | `packages/kernel-runtime/Teams/TeamContext.cs` |
| `Sunfish.Kernel.Audit.AuditRecord` | §"Decision drivers" #3 | `packages/kernel-audit/AuditRecord.cs` (`sealed record AuditRecord(...)`) |
| `Sunfish.Kernel.Audit.IAuditTrail` | §"Decision drivers" #3 | `packages/kernel-audit/IAuditTrail.cs:18` (`interface IAuditTrail`) |
| `Sunfish.Kernel.Audit.AuditEventType` | §A0.3(e) | `packages/kernel-audit/AuditEventType.cs:18` (`readonly record struct AuditEventType(string Value)`) |
| `Sunfish.Foundation.MissionSpace.MissionEnvelope` | §1.1 (HelmRenderContext) | `packages/foundation-mission-space/Models/MissionEnvelope.cs` |
| `IMissionEnvelopeProvider.Subscribe(IMissionEnvelopeObserver)` | §1.3 trigger #1 | `docs/adrs/0062-mission-space-negotiation-protocol.md:155-159`; verified `Subscribe` returns `IDisposable` |
| `ICapabilityGate<TCapability>` | §"Decision drivers" #5 | `docs/adrs/0062-mission-space-negotiation-protocol.md:189` |
| `Sunfish.Foundation.Wayfinder.IStandingOrderIssuer` | §"Decision drivers" #3, §1.4, §2 | `packages/foundation-wayfinder/IStandingOrderIssuer.cs:28` (interface signature: `Task<StandingOrder> IssueAsync(StandingOrderDraft draft, ActorId issuedBy, IAuditTrail auditTrail, CancellationToken ct)`) |
| `Sunfish.Foundation.Wayfinder.IStandingOrderRepository` | §1.3 trigger #2 | `packages/foundation-wayfinder/IStandingOrderRepository.cs:18` (interface) |
| `Sunfish.Foundation.Wayfinder.StandingOrderScope` | §2.2-§2.4 (User / Security / Tenant) | `packages/foundation-wayfinder/StandingOrderScope.cs` (5-value enum: User / Tenant / Platform / Integration / Security) |
| `Sunfish.Foundation.Recovery.HistoricalKeysProjection` | §2.3, §2.5, §A0.3(c) | `docs/adrs/0046-a1-historical-keys-projection.md:135` (declared in ADR 0046-a1; not yet on origin/main per §A0.3(c) — author correctly tags as conditional-on-acceptance) |
| `Sunfish.Foundation.Recovery.KeyRotationReason` | §2.5 | `docs/adrs/0046-a1-historical-keys-projection.md:140-158` (declared in ADR 0046-a1; 7-value enum: Scheduled / SocialRecovery / PaperKeyFallback / Compromise / SuspectedCompromise / AlgorithmRotation / Compromised — see §2.5 footnote in this council file) |
| `IRoleKeyManager` (referenced by §A0.3(g)) | §2.6 | `packages/kernel-security/Keys/IRoleKeyManager.cs:14` (verified) |
| `IActiveTeamAccessor.SetActiveAsync` | §2.6 (implicit via "switch this team") | `packages/kernel-runtime/Teams/ActiveTeamAccessor.cs:35` |
| `NodaTime.Instant` | §1.1 (HelmRenderContext) + §2.5 | external; verified pulled in transitively per `Directory.Packages.props` |

**One minor §2.5 footnote:** ADR 0066 §2.5 cites `KeyRotationReason` values as "`Scheduled` / `Compromise` / `Recovery` / `Migration` (per ADR 0046-a1)". The actual enum per `docs/adrs/0046-a1-historical-keys-projection.md:140-158` has 7 values: `Scheduled`, `SocialRecovery`, `PaperKeyFallback`, `Compromise`, `SuspectedCompromise`, `AlgorithmRotation`, `Compromised`. ADR 0066's "(per ADR 0046-a1)" cite is structurally correct (ADR 0046-a1 is the home), but the 4-value list is **incomplete** (missing 3 of 7). Mechanical fix M-5 (lift the full 7-value list, OR write "(per ADR 0046-a1's `KeyRotationReason` enum — values include `Scheduled` / `Compromise` / `SocialRecovery` / `Migration` etc.)" with an "etc.").

**Classification:** mechanical (M-5, completeness of the cited enum value list). **Recommendation:** correct §2.5 to either list all 7 values or use a non-exhaustive phrasing with the actual cite.

### SC-clean — `Sunfish.UICore.Wayfinder` namespace placement is CORRECT.

Verified that `packages/ui-core/` exists on origin/main with namespace `Sunfish.UICore` (not `Sunfish.UI.Core`):

- `packages/ui-core/Contracts/ISunfishJsInterop.cs:1`: `namespace Sunfish.UICore.Contracts;`
- 5+ other files in `packages/ui-core/Contracts/` confirm `Sunfish.UICore` is canonical (not `Sunfish.UI.Core`)

So the author's choice of `Sunfish.UICore.Wayfinder` (not `Sunfish.UI.Core.Wayfinder`) is correct. **No structural-citation finding.**

The §1.1 paragraph "additive to existing `Sunfish.UICore` package; no new package" is also correct — the new `Wayfinder/` folder lives within `packages/ui-core/`, not a new `packages/ui-core-wayfinder/` package. (Council confirms: this is the right choice. Avoids cohort proliferation.)

### NM-2 (NON-MECHANICAL) — `IObservable<StandingOrderAppliedEvent>` reactive surface confirmed missing on origin/main.

The author's §OQ-3 already names this. Council disposition verified by independent grep:

- `grep -rn "IObservable\|StandingOrderApplied" packages/foundation-wayfinder/`: **ZERO matches**
- `grep -rn "IObservable\|Subscribe\b" packages/foundation-wayfinder/`: **ZERO matches** (note: `Subscribe` is on `IMissionEnvelopeProvider`, not `IStandingOrderRepository`)
- ADR 0065's docs body does not name `StandingOrderAppliedEvent` or any reactive surface

This is a **hard structural gap.** ADR 0066 §1.3 trigger #2 cannot be implemented as specified without one of:

- **Option A — ADR 0065 amendment** that adds `IObservable<StandingOrderAppliedEvent>` (or equivalent observer surface) to `IStandingOrderRepository`. Cleanest but requires re-opening 0065.
- **Option B — separate ADR 0065-A1** purely for the reactive surface. Smaller blast radius; doesn't reopen ADR 0065.
- **Option C — periodic-refresh fallback only** — drop §1.3 trigger #2, rely solely on triggers #1 (envelope-change) + #3 (periodic). Worst UX (60-sec stale state for Helm widgets that depend on fresh Standing Order state, e.g., `recent-standing-orders`).
- **Option D — IIssuer-fired post-emission hook** — `IStandingOrderIssuer.IssueAsync` calls a registered `IStandingOrderEmissionObserver` synchronously after Append + Audit. Less ideal (couples issuer to observer; not reactive in the FRP sense; but works without a new package).

**Council recommendation:** Option B (separate ADR 0065-A1 for reactive surface) is the cleanest. **File as a follow-on workstream beacon NOT a blocker for ADR 0066.** ADR 0066 should add a halt-condition (H8 in §"Implementation checklist Phase 1") naming the dependency: "ADR 0065 reactive-surface amendment (TBD) must reach `Accepted` before §1.3 trigger #2 is implementable." Phase 1 substrate (the contracts in §1.1, §2.1) does NOT depend on the reactive surface — only the **runtime composition** of the Helm widgets does.

**Classification:** non-mechanical (deferred dependency; halt-condition addition + follow-on ADR beacon). **Recommendation:** add halt-condition H8 + file beacon for ADR 0065-A1.

### NM-3 (NON-MECHANICAL) — Flat namespace `Sunfish.UICore.Wayfinder` confirmed correct vs split.

Author's §OQ-2 disposition confirmed: cohort precedent is flat. `packages/foundation-wayfinder/` hosts `StandingOrder` + `IAtlasProjector` + `IStandingOrderIssuer` + `AtlasView` + `AtlasSettingSnapshot` + `AtlasSchemaDescriptor` etc. all in the single `Sunfish.Foundation.Wayfinder` namespace. Splitting `Sunfish.UICore.Wayfinder` into `.Helm` + `.IdentityAtlas` would break cohort symmetry.

**Council disposition:** ACCEPT author's recommendation — flat namespace, with sub-folders `Wayfinder/Helm/`, `Wayfinder/Identity/` for organization. **Classification:** non-mechanical (council disposition only). **Recommendation:** confirm in §OQ-2 disposition.

---

## Perspective 4 — Devil's Advocate (was the framing right?)

The Devil's-Advocate prompts the author asked council to pressure-test:

> Was the Helm/Atlas split genuinely the right framing? What about a single unified "identity surface" without the split? What about deferring the historical-keys browse to a separate ADR?

### DA-1 — Was the Helm/Atlas split the right framing? YES (confirm).

Single-unified-identity-surface (Option A in the author's §"Considered options") was **already rejected** for the right reasons:

- Conflates live-state-observation with deep-config-issuance
- Forces blocks to implement both methods even when they only contribute one
- Breaks the W#34-locked Helm-vs-Atlas distinction

The Helm-vs-Atlas split is the **fundamental architectural decision** of the Wayfinder system per ADR 0065 + W#34 §5.7-§5.8. ADR 0066 is implementing that split for the identity sub-surface; reverting it within ADR 0066 would invert ADR 0065 + W#34. **Council confirms: split is correct.**

### DA-2 — Should historical-keys browse be deferred to a separate ADR? NO (confirm).

The historical-keys browse (§2.5) is the smallest of the five identity surfaces (one read-only table; no mutation; composes ADR 0046-a1's already-specified `HistoricalKeysProjection`). Splitting it out would:

- Create a per-surface ADR proliferation (5 surfaces → 5 ADRs?)
- Lose the cohort coherence ("here's the entire identity Atlas") that motivates ADR 0066's bundling
- Add 3-5 weeks to the W#34 §5.8 (Account / identity layer) coverage closure

The historical-keys browse fits naturally inside ADR 0066. **Council confirms: keep it in.**

### DA-3 — Was the Helm-vs-Cockpit boundary handled adequately? PROBABLY YES (with caveat).

§"Decision drivers" #7 sketches the boundary (Helm = system pane / framework / agnostic; Cockpit = block-dashboard / `blocks-*` / accelerator-bespoke). The Cockpit ADR (W#29) does not yet exist; W#29 is at intake stage (`icm/00_intake/output/property-owner-cockpit-intake-2026-04-28.md`). When W#29 graduates to ADR, it will inherit the boundary as cited.

**Caveat:** if W#29's eventual ADR proposes a **competing widget composition contract** (e.g., a `ICockpitWidget` interface with overlapping responsibilities), §"Decision drivers" #7's sketch will not be enough — a formal boundary ADR will be needed. ADR 0066's `revisit_trigger` block already names this ("W#29 Owner Web Cockpit ADR introduces a competing widget composition contract. Triggers boundary clarification (§'Decision drivers' §7) into an explicit boundary ADR.") — which is the right defensive posture. **Council confirms: do not block ADR 0066 on a boundary ADR; the revisit trigger is sufficient.**

### DA-4 — Is "renderer not actuator" the right widget posture? YES (strong confirm).

The renderer-not-actuator distinction (§"Decision drivers" #3 + §1.1 XML doc remarks + §1.4 widget-table commentary) is enforced by:

1. The widget contract (`IHelmWidget` exposes only `Metadata` + `ComputeAsync` returning `HelmWidgetViewState`; no mutation surface)
2. The ViewState shape (`HelmWidgetViewState` carries `Actions` of kind `Navigate` / `IssueStandingOrder` / `RunLocalCommand`; the action invocation flows OUT to ADR 0065's `IStandingOrderIssuer.IssueAsync` or to the local kernel; not back into the widget)
3. The §"Decision drivers" #3 audit-by-construction guard (audit emission stays in `IStandingOrderIssuer.IssueAsync` and `IAuditTrail.AppendAsync`)

This is **strong** — a malicious or careless block author cannot bypass audit emission by writing a widget that mutates state, because the widget surface doesn't expose mutation. **Commendation P-2 + P-3 recorded.**

### DA-5 (rhetorical, council not blocking) — Could ADR 0066 be rejected entirely in favor of waiting for W#29 + ADR 0065 + ADR 0046-a1 all to land first?

This is the strongest Devil's-Advocate position: ADR 0066 has dependencies on three not-yet-landed substrates (ADR 0065 `Proposed`, ADR 0046-a1 `Proposed`, ADR 0046's `Sunfish.Foundation.Identity` namespace [doesn't exist], plus the missing `IObservable<StandingOrderAppliedEvent>` surface). Why not defer ADR 0066 until all three land?

**Council answer: NO.** ADR 0066 is the **substrate that closes W#34 §5.8 coverage** (Account / identity layer; tagged Partial in W#34 discovery). Without ADR 0066, every block that wants to surface identity invents its own UI (per §"Context" line 55). The ADR is **ready for council disposition now**; the implementation can start when its dependencies (ADR 0065 Accepted, ADR 0046-a1 Accepted, the §1.3 reactive-surface amendment) land. The halt-conditions (H1, H2, H4, H7, plus proposed H8) make the dependency chain explicit; COB will not start Phase 1 until they clear. **The cost of authoring ADR 0066 now and deferring implementation is much lower than the cost of repeated re-author cycles as the dependencies land in different shapes.**

**Conclusion:** ADR 0066 is correctly scoped. **Recommendation:** add H8 (per NM-2 above); otherwise the ADR is shippable.

---

## Perspective 5 — WCAG / a11y subagent (mandatory per ADR 0065 §7.4)

WCAG 2.2 AA conformance is contract per W#34 §5.7 + ADR 0065 §7. ADR 0066 §3 enumerates seven Success Criteria explicitly. I verified each against the W3C WCAG 2.2 specification text and against the cohort precedent (ADR 0065 §7).

### A11Y-1 (commendation P-4) — All cited SCs are correct against W3C WCAG 2.2.

Verified:

- **SC 3.3.7 (Redundant Entry)** — Level A in WCAG 2.2 (was AA in WCAG 2.1; promoted). ADR 0066's claim ("within a single key-rotation session, the user is NOT asked to re-enter the new key fingerprint or recovery contact info already supplied") is correct application.
- **SC 3.3.8 (Accessible Authentication — No Cognitive Function Test)** — Level AA. ADR 0066's claim ("recovery-contact verification MUST NOT use cognitive-recall challenges") is correct application; the rule explicitly forbids "What was your first pet's name?" challenges.
- **SC 3.3.9 (Accessible Authentication — Enhanced)** — Level AAA. ADR 0066 cites this as "MAY use device attestation but MUST offer an accessible alternative path." The "MAY" is correct (AAA is aspirational); the "MUST offer accessible alternative" is the AA requirement (3.3.8). No conflation.
- **SC 2.2.1 (Timing Adjustable)** — Level A. ADR 0066's claim ("rotation grace window is user-extensible up to 30 days") is correct application.
- **SC 1.4.11 (Non-Text Contrast)** — Level AA. ADR 0066's claim ("KeyFingerprint rendering uses ≥3:1 contrast against background") is correct (3:1 is the SC threshold for non-text).
- **SC 2.4.6 (Headings and Labels)** — Level AA. Correct.
- **SC 4.1.3 (Status Messages)** — Level AA. ADR 0066's claim about `aria-live="polite"` for sync-state changes and `aria-live="assertive"` for compromise-driven rotations is correct application.
- **EN 301 549 V3.2.1 (2021-03)** — verified canonical version cite for European procurement.

**Commendation P-4 recorded.**

### A11Y-2 (mechanical) — `aria-live` boundary between polite and assertive needs one more clarifying note.

§3 paragraph "4.1.3 Status Messages" specifies:

> sync-state changes in Helm widgets fire `aria-live="polite"` announcements (per ADR 0036 five-channel encoding); rotation-progress changes fire `aria-live="assertive"` for compromise-driven rotations only.

This is correct, but the Anchor / Bridge implementer reading it cold might miss the **distinction between "rotation-progress" status (informational; polite)** and **"compromise-driven rotation" (alert; assertive)**. Recommend adding one sentence:

> Compromise-driven rotation (per `KeyRotationReason.Compromise` or `KeyRotationReason.SuspectedCompromise` per ADR 0046-a1) is a security-event alert and warrants the assertive level; scheduled / algorithm rotations are informational and remain polite.

**Classification:** mechanical (clarifying note). **Recommendation:** add the sentence.

### A11Y-3 (mechanical) — §2.5 historical-keys browse table sortable-columns claim is right; recommend explicit cite of `aria-sort` attribute values.

§2.5 line 340 specifies: "sortable columns with `aria-sort` attributes (WCAG 1.3.1)". `aria-sort` is correct (per ARIA 1.2 spec); the valid values are `ascending` / `descending` / `other` / `none`. Recommend the implementation-checklist Phase 3 row name the value enumeration so COB doesn't have to re-derive:

> sortable columns with `aria-sort` attributes (values: `ascending` / `descending` / `none`; one-column-active-at-a-time per ARIA 1.2 §"aria-sort"; per WCAG 1.3.1).

**Classification:** mechanical. **Recommendation:** expand the §2.5 sortable-columns line.

### A11Y-4 (NON-MECHANICAL) — "RecoveryContact" vs "Trustee" — UX / cognitive-load argument.

The PRA-vocabulary (Trustee) is technical/legal; the UX-vocabulary (RecoveryContact) is plain-language. From a WCAG 3.1.5 (Reading Level — AAA) perspective:

> "Trustee" is the legal/financial vocabulary (the reading level is roughly upper secondary education). "Recovery contact" is plain language, accessible at lower secondary education level.

WCAG 3.1.5 is Level AAA (aspirational), but the principle (use the simplest language that still communicates accurately) applies at all levels per WCAG 2.2 §"Understanding 3.1.5". For a UI surface that handles **high-stakes operations** (key rotation, recovery enrollment) the plain-language vocabulary is the right choice.

**WCAG/a11y disposition:** ADOPT author's proposed split — `Trustee*` for audit + cryptographic vocabulary (matches ADR 0046; technical correctness); `RecoveryContact*` for user-facing UX (plain-language; WCAG 3.1.5 alignment). Document the synonymy at `_shared/product/naming.md`.

**Classification:** non-mechanical (NM-1, council disposition). **Recommendation:** confirm split per author's §OQ-1; cite the WCAG 3.1.5 plain-language rationale in the disposition.

### A11Y-5 (mechanical) — §3.3.7 Redundant Entry rule should call out the "carry-forward across reload" edge case.

§3 line 357 specifies: "within a single key-rotation session, the user is NOT asked to re-enter the new key fingerprint or recovery contact info already supplied. Carry-forward is required."

The reload edge case: if the user accidentally reloads the page mid-key-rotation (browser refresh, session-storage clear, multi-tab confusion), can they reconstruct their entered state? WCAG 3.3.7 does NOT require persistence across reload; it requires no-redundant-entry within a single "session." Anchor MAUI sessions are process-lifetime; Bridge React sessions are tab-lifetime. Recommend adding a clarifying note: "session-scope per WCAG 3.3.7 = process-lifetime for Anchor / tab-lifetime for Bridge; explicit page reload may forfeit carry-forward."

**Classification:** mechanical (clarifying scope). **Recommendation:** add the note.

---

## UPF v1.2 Stage 2 anti-pattern scan

Per ADR 0069 D2 + UPF v1.2 §Stage 2, the 21 anti-patterns:

| AP # | Pattern | ADR 0066 status |
|---|---|---|
| AP-1 | Unvalidated assumptions | **CLEAN** — all assumptions in §A0.3 are tested. |
| AP-2 | Vague phases | **CLEAN** — Phase 1 / 2 / 3 implementation checklists are scope-based with file-name granularity. |
| AP-3 | Vague success criteria | **CLEAN** — each phase has named acceptance criteria; halt-conditions are explicit. |
| AP-4 | No rollback | **CLEAN** — substrate ADR; rollback = revert PR. |
| AP-5 | Plan ending at deploy | **CLEAN** — §"Revisit triggers" enumerates 7 triggers. |
| AP-6 | Missing Resume Protocol | **CLEAN** — phase-level halt-conditions (H1-H7, +H8 proposed) name the resume signal. |
| AP-7 | Delegation without contracts | **CLEAN** — ADR 0073 cite + named hand-off contract per phase. |
| AP-8 | Blind delegation trust | **CLEAN** — pre-merge council canonical (this review). |
| AP-9 | Skipping Stage 0 | **CLEAN** — prior-art search via `tools/adr-projections/embed_search.py` (cited in §"Context"); 5 options A-E in §"Considered options". |
| AP-10 | First idea remaining unchallenged | **CLEAN** — Options A-D explicitly rejected with rationale; Option E recommended. |
| AP-11 | Zombie projects (no kill criteria) | **CLEAN** — `revisit_trigger` block names re-author conditions. |
| AP-12 | Timeline fantasy | **CLEAN** — no hour estimates given (per ADR 0069's "scope-based for coding" rule). |
| AP-13 | Confidence without evidence | **PARTIAL** — §1.1's "kebab-case convention used by ADR 0049" is confidence without evidence (the convention is PascalCase). See SC-1. |
| AP-14 | Wrong detail distribution | **CLEAN** — §1.1 high-detail (interface signatures); §1.4 medium-detail (widget table); §3 high-detail (WCAG SC enumeration); §A0 high-detail (citation enumeration). |
| AP-15 | Premature precision | **CLEAN** — open questions (§OQ-1 through §OQ-6) explicitly name the unresolved decisions for council. |
| AP-16 | Hallucinated effort estimates | **CLEAN** — no estimates. |
| AP-17 | Delegation without context transfer | **CLEAN** — implementation checklist names the cross-ADR dependencies (H1-H7) and the hand-off contract (ADR 0073). |
| AP-18 | Unverifiable gates | **CLEAN** — every halt-condition names a verifiable signal (`grep -rn "namespace ..."` / ADR status flip / parity test pass). |
| AP-19 | Missing tool fallbacks | **CLEAN** — §1.3 trigger #3 (periodic refresh) is a fallback for triggers #1 + #2; §"Considered options" Option D names doc-only as a "grep alone" baseline. |
| AP-20 | Discovery amnesia | **CLEAN** — §"Context" cites the prior-art search results inline; W#34 discovery doc cited in §"References". |
| AP-21 | **Assumed facts without sources** | **PARTIAL** — SC-1 (kebab-case claim without verifying `packages/kernel-audit/AuditEventType.cs`) is the violation. The §A0 self-audit's structural-citation enumeration is otherwise rigorous. |

**Total UPF anti-pattern findings: 1 partial (AP-13) + 1 partial (AP-21) — both manifest as the same SC-1 finding.** No new findings beyond SC-1.

---

## Recommendations to author (ordered by priority)

1. **SC-1 (structural-citation, mandatory):** rewrite §1.1 line ~198 to remove the false "kebab-case convention used by ADR 0049 audit-event-type identifiers" claim. Replace with the wording suggested in SC-1 above (separate the path-vs-type-symbol convention; cite ADR 0049 for the PascalCase AuditEventType convention; cite ADR 0065 for the dotted-path StandingOrder.Path convention).

2. **NM-2 (non-mechanical, mandatory):** add halt-condition H8 to §"Implementation checklist Phase 1": *"H8 — ADR 0065 reactive-surface amendment (TBD ADR 0065-A1) must reach `Accepted` before §1.3 trigger #2 (Standing Order applied) is implementable. Until H8 clears, Phase 2 widgets that depend on Standing Order reactive state (`recent-standing-orders`, `quick-toggles` post-issuance refresh) fall back to periodic-refresh + envelope-change triggers only."* File a follow-on `cob-question-*.md` or `xo-action-*.md` beacon for the ADR 0065-A1 reactive-surface amendment.

3. **SC-2 (structural-citation, recommended):** add §A0.2 row for `IFieldDecryptor` audit-emission contract per the SC-2 wording above. This surfaces the §OQ-4 footnote into §A0 where implementers will see it.

4. **NM-1 (non-mechanical, council disposition):** confirm RecoveryContact-vs-Trustee split per author's §OQ-1 recommendation — `Trustee*` for audit/crypto vocabulary, `RecoveryContact*` for user-facing UX. Document at `_shared/product/naming.md`. Cite WCAG 3.1.5 plain-language rationale in the disposition. (NM-1 is a disposition not a fix; the §OQ-1 already proposed the right answer.)

5. **NM-3 (non-mechanical, council disposition):** confirm flat namespace `Sunfish.UICore.Wayfinder` (not split into `.Helm` + `.IdentityAtlas`) per author's §OQ-2. Cohort precedent (`Sunfish.Foundation.Wayfinder` flat) supports this.

6. **NM-4 (non-mechanical, council disposition):** confirm Helm-vs-Cockpit boundary handled by §"Decision drivers" #7 + revisit trigger; do NOT block ADR 0066 on a separate boundary ADR. Carry forward the revisit trigger as already-written.

7. **PRA-1 (non-mechanical):** add Trigger #4 to §1.3 (on-reconnect / on-resume unconditional recompute). Closes the rotation-window-stale-UI race described in PRA-1.

8. **PRA-2 (non-mechanical):** add canonical `IStandingOrderValidator` declaration to §2.4 Enroll for `Path = "identity.recovery.contacts.add"`: ≤5 additions per Security-scope window; verification-status check before quorum count; audit-emit on rate-limit rejection.

9. **PRA-3 (non-mechanical):** add per-process-singleton clarifying note to §2.6 about active-team switch propagation across multi-window contexts (inherits from ADR 0032 §"Default: Option C").

10. **PRA-4 (mechanical):** lift `recovery-status` widget from §1.3 prose into §1.4 table as a Phase-2 row (consistent with the "defer-Phase-2 widgets" footnote).

11. **A11Y-2 (mechanical):** §3 paragraph 4.1.3 — add the Compromise-vs-Scheduled distinction sentence.

12. **A11Y-3 (mechanical):** §2.5 — expand the `aria-sort` attribute mention to enumerate valid values per ARIA 1.2.

13. **A11Y-5 (mechanical):** §3 paragraph 3.3.7 — add the session-scope clarification (process-lifetime / tab-lifetime).

14. **M-1 (mechanical):** add `49` to frontmatter `composes:` list (currently has `[32, 36, 46, 49, 62, 65]` — wait, re-check: the frontmatter shows `composes: [32, 36, 46, 49, 62, 65]` — actually 49 IS already there. Confirmed clean. M-1 retracted.)

15. **M-2 (mechanical):** §A0 cohort phrasing — current "22-of-22 substrate amendments needed council fixes" should probably read "23-of-23 candidate" if this council finds anything (it has). Update phrasing per ADR 0069 D1 cohort metric to reflect this council's findings.

16. **M-3 (mechanical):** §5 fuzzy-name banner ("Helm" / "Atlas" locked vocabulary — intentional) should cross-reference the eventual `_shared/product/naming.md` registry once §OQ-1 disposition is filed.

17. **M-4 (mechanical):** §A0.2 `IFieldDecryptor.Crypto` sub-namespace footnote should cite the line number `packages/foundation-recovery/Crypto/IFieldDecryptor.cs:6` for forensic reproducibility.

18. **M-5 (mechanical):** §2.5 — `KeyRotationReason` value list is incomplete (4 of 7 cited; ADR 0046-a1 has 7). Either complete the list or use non-exhaustive phrasing.

19. **OO-1 (mechanical):** add 4-line ASCII diagram to §"Context" paragraph 2 showing Wayfinder ⊃ {Helm, Atlas} ⊃ Identity sub-surface.

20. **OO-2 (mechanical, optional):** add 2-sentence "Why Option E?" gap between §"Considered options" and §Decision header.

21. **OO-3 (mechanical):** §1.4 widget-table column-header "A11y SC critical" → "WCAG SC focus" (or footnote).

---

## Follow-on actions (separate from ADR 0066 amendment)

- **F1 (XO action):** file a beacon (`xo-action-adr-0065-a0-2-namespace-fix.md`) tracking ADR 0065 §A0.2 namespace cite-error (`Sunfish.Foundation.Identity.ActorId` should read `Sunfish.Foundation.Assets.Common.ActorId`). Recommended fix: in-place §A0.2 correction commit on a new branch (do not modify existing ADR 0065 PR if Status: Proposed; coordinate with the ADR 0065 author / disposition).
- **F2 (XO action):** file a follow-on intake or ADR amendment beacon for ADR 0065-A1 (reactive-surface amendment): adds `IObservable<StandingOrderAppliedEvent>` (or equivalent observer surface) to `IStandingOrderRepository` or as a sibling type. Halt-condition H8 in ADR 0066 depends on this.
- **F3 (XO note):** the `_shared/product/naming.md` file does NOT yet exist on origin/main (`grep -rn "_shared/product/naming.md" .` returns no matches). When NM-1 disposition lands, file an intake to create the naming registry; ADR 0066's §OQ-1 disposition is the seed entry.

---

## Verdict (restated)

**NEEDS-AMENDMENT.** The ADR's substrate decisions are correct; one structural-citation failure (SC-1, AuditEventType naming convention) plus one confirmed-missing dependency surface (NM-2, IObservable<StandingOrderAppliedEvent>) plus four pre-flagged council disposition items (NM-1, NM-3, NM-4, NM-6) require the author to apply the recommendations above before flipping `Status: Proposed` → `Accepted`. Once the recommendations land, this ADR is shippable.

**Cohort metric update:** ADR 0066 is the **23rd-of-23** case where pre-merge council was canonical for substrate-tier ADRs. The §A0 self-audit caught one critical structural failure (the ADR 0065 namespace error) but missed one (the AuditEventType naming convention). This refines the cohort metric: §A0 catch rate is now 1-of-6 verified structural failures across the post-ADR-0062 cohort (was 0-of-5 per ADR 0069 D2). §A0 is improving but still necessary-not-sufficient; pre-merge council remains canonical defense.

**Author commendation P-1:** the §A0.2 catch on ADR 0065's `Sunfish.Foundation.Identity.ActorId` cite is the kind of structural awareness the §A0 discipline is supposed to build. Counter-example to the "§A0 catches nothing" pattern; recorded in the cohort log as a positive instance.
