# ADR 0081 Council Review — 2026-05-05

**ADR:** `docs/adrs/0081-tactical-anomaly-detection.md`
**Branch:** `docs/adr-0081-tactical-anomaly-detection`
**Date:** 2026-05-05
**Verdict:** NEEDS-AMENDMENT → all blocking amendments applied same session → **re-review queued**

---

## Final verdict (post-amendment)

All 10 Critical (SI) + 8 Critical (Security) + 6 Critical (A11y) + 8 Critical (PL) findings
applied in the same authoring session, plus the most-impactful Major findings. Combined 42-
finding pass resolved in a single full-rewrite. Council dispatched across 4 perspectives;
re-review to confirm all Criticals resolved.

---

## Findings

| ID | Perspective | Severity | Finding | Resolution |
|---|---|---|---|---|
| SI-01 | Skeptical Implementer | Critical | `TryIssueAsync` returns `object?` forward-ref placeholder | Applied: return type changed to `string?` (StandingOrderId string); §4 documents orderId as Guid.NewGuid().ToString("N") |
| SI-02 | Skeptical Implementer | Critical | Permission-denied path semantics inconsistent (null vs throw vs audit) | Applied: §4 `TryIssueAsync` order-of-operations specifies emit TacticalAuthorizationDenied + return null (not throw) on denial |
| SI-03 | Skeptical Implementer | Critical | Dedup race condition for concurrent calls | Applied: §4.3 specifies thread-safe ConcurrentDictionary pattern |
| SI-04 | Skeptical Implementer | Critical | Audit emission responsibility unclear in RouteAsync | Applied: §2 RouteAsync contract specifies normative 5-step order; IAlertRouter is sole emitter of AnomalyDetected+AlertRouted |
| SI-05 | Skeptical Implementer | Critical | AlertId regex missing `.` from RuleName format collision | Applied: regex updated to `^[A-Za-z0-9_\-\.:]{1,128}$`; §1 TacticalAlert doc note updated |
| SI-06 | Skeptical Implementer | Critical | Sync-only rule contract prevents state-aware rules | Applied: §2 ITacticalRule doc specifies rules MUST NOT do I/O; state pre-populated externally via constructor injection |
| SI-07 | Skeptical Implementer | Critical | Signal ordering policy (OccurredAt vs arrival order) undefined | Applied: §2.2 specifies arrival order; OccurredAt is informational only; rules MUST NOT assume monotonicity |
| SI-08 | Skeptical Implementer | Critical | Snapshot degradation not flagged | Applied: `IsPartialSnapshot` + `DegradedSubsystems` added to TacticalSnapshot; §3.1 failure modes updated |
| SI-09 | Skeptical Implementer | Critical | IPermissionResolver.IsRegistered() may not exist | Applied: §8.1 adds forward-ref note to W#46 to confirm startup-check API signature |
| SI-10 | Skeptical Implementer | Major | ILookout.GetActiveLookoutAlerts definition mismatch (Active vs Active+Acknowledged) | Applied: ILookout returns Active-only; TacticalSnapshot.LookoutAlerts doc updated |
| SI-11 | Skeptical Implementer | Major | RunbookStepIds have no format constraint | Applied: `^[a-z][a-z0-9\-]{0,63}$` regex added to §1 TacticalAlert doc and §3 OpenIncidentAsync |
| SI-12 | Skeptical Implementer | Major | OpenIncidentAsync contradiction (throw vs idempotent return) | Applied: §3 clarified — not-found throws; already-open returns existing (idempotent) |
| SI-13 | Skeptical Implementer | Major | CloseIncidentAsync exception type unspecified | Applied: §3 specifies ArgumentException for null/whitespace/overflow resolutionNote |
| SI-14 | Skeptical Implementer | Major | TacticalOptions has no bounds | Applied: bounds documented as normative in §1; Validate() contract added |
| SI-23 | Skeptical Implementer | Major | ILookout capacity eviction has no audit event | Applied: TacticalAlertExpired + LookoutAlertEvicted added to §5 |
| SI-24 | Skeptical Implementer | Major | AlertStatus.Superseded never transitioned | Applied: §1 AlertStatus enum doc defines Superseded lifecycle (newer same-RuleName alert) |
| SI-29 | Skeptical Implementer | Major | TacticalUnauthorizedException audit emission unspecified | Applied: §3 command service docs specify emitting TacticalAuthorizationDenied before throw |
| SI-30 | Skeptical Implementer | Major | LinkedAlertIds can never grow (no LinkAlertToIncidentAsync API) | Applied: §3 IncidentRecord doc notes "populated via future LinkAlertToIncidentAsync; initially [RootAlertId]" |
| SI-36 | Skeptical Implementer | Major | LookoutAlerts cap 200 vs Quarterdeck cap 50 mismatch | Applied: §7.2 documents the caps are independent; Quarterdeck returns most-recent 50 |
| PL-01 | Pedantic Lawyer | Critical | AlertId regex / RuleName dot collision (same as SI-05) | Applied: see SI-05 |
| PL-02 | Pedantic Lawyer | Critical | `TryIssueAsync` `object?` return (same as SI-01) | Applied: see SI-01 |
| PL-03 | Pedantic Lawyer | Critical | Permission denial audit in TryIssueAsync (same as SI-02) | Applied: see SI-02 |
| PL-04 | Pedantic Lawyer | Critical | `orderId` impossible before `AppendAsync` | Applied: §4 step 5 generates orderId client-side before AppendAsync; §5 EmergencyStandingOrderIssued carries pre-generated orderId; §A0 Open Q1 documents AppendAsync caller-supplied ID confirmation needed |
| PL-05 | Pedantic Lawyer | Critical | Dedup trigger timing undefined | Applied: §4.3 rewritten with "60 seconds from the issuance timestamp of the last successful Standing Order for that key" |
| PL-06 | Pedantic Lawyer | Critical | RouteAsync partial-failure behavior undefined | Applied: §2 RouteAsync contract specifies 5-step order with explicit partial-failure semantics |
| PL-07 | Pedantic Lawyer | Critical | RunbookStepIds no schema (same as SI-11) | Applied: see SI-11 |
| PL-08 | Pedantic Lawyer | Critical | OpenIncidentAsync self-contradiction (same as SI-12) | Applied: see SI-12 |
| PL-09 | Pedantic Lawyer | Major | ILookout capacity eviction: which audit event? | Applied: LookoutAlertEvicted added to §5 |
| PL-10 | Pedantic Lawyer | Major | AlertStatus.Superseded semantics undefined (same as SI-24) | Applied: see SI-24 |
| PL-11 | Pedantic Lawyer | Major | IncidentStatus.Investigating transition API missing | Applied: §1 IncidentStatus doc notes "reserved for future amendment"; transition from Open→Investigating deferred |
| PL-16 | Pedantic Lawyer | Major | MaxActiveIncidents hardcoded 50 | Applied: added MaxActiveIncidents to TacticalOptions |
| PL-23 | Pedantic Lawyer | Major | InformationalSonar alerts vanish (no queryable store) | Applied: ISonarStore interface added to §2 |
| PL-25 | Pedantic Lawyer | Major | ManageThreatTriggers declared but unused | Applied: §6 documents it as reserved for future runtime template management |
| PL-30 | Pedantic Lawyer | Major | Title/ResolutionNote/Summary length limits missing | Applied: TacticalAlert.Title≤80, Summary≤200, IncidentRecord.Title≤256, ResolutionNote≤4096 |
| PL-39 | Pedantic Lawyer | Minor | ThreatTriggerTemplate.ExpiresAfter used NodaTime.Duration; TacticalOptions used TimeSpan | Applied: ThreatTriggerTemplate.ExpiresAfter changed to TimeSpan? for consistency with TacticalOptions |
| S-01 | Security Engineer | Critical | TryIssueAsync issuer parameter forgeable | Applied: issuer removed from parameter; §4.1 ISystemPrincipalProvider pattern defined |
| S-02 | Security Engineer | Critical | alert.TenantId not verified against ambient tenant | Applied: §4 step 1 cross-check; §8.2 tenant anti-spoofing section updated |
| S-04 | Security Engineer | Critical | Dedup bypass via multiple rule names; unbounded Standing Orders per signal | Applied: §4.4 per-signal order budget + §8.5 amplification budget |
| S-05 | Security Engineer | Critical | Dedup resets on restart | Applied: §4.3 documents restart behavior explicitly; idempotency at AppendAsync is canonical defense; noted multi-instance behavior |
| S-06 | Security Engineer | Critical | Third-party rules can post to Quarterdeck ticker | Applied: §8.3 AllowedHighPriorityRulePrefixes; unauthorized prefixes downgraded with audit |
| S-07 | Security Engineer | Critical | Rule impersonation not enforced | Applied: §8.3 RegisterRule enforces sunfish.* prefix requires first-party assembly identity |
| S-09 | Security Engineer | Critical | Cross-tenant alert leak in LookoutQuarterdeckAlertSource | Applied: §7.2 tenant cross-check + filter by alert.TenantId == tenantId |
| S-10 | Security Engineer | Critical | GetAlertsAsync has no actor param (authorization bypass) | Applied: Principal actor added to GetAlertsAsync + GetActiveIncidentsAsync; §6 Note scoping enforced at provider layer |
| S-11 | Security Engineer | Major | AnomalyDetected payload missing triggeringActorId | Applied: triggeringActorId added to §5 AnomalyDetected payload |
| S-13 | Security Engineer | Major | IncidentOpen/CloseAsync lack pre-op audit events | Applied: IncidentOpenRequested + IncidentCloseRequested added to §5; §3 docs updated |
| S-14 | Security Engineer | Major | ManageThreatTriggers declared but not used (same as PL-25) | Applied: see PL-25 |
| S-16 | Security Engineer | Major | No audit on Lookout capacity eviction | Applied: LookoutAlertEvicted added to §5 |
| S-18 | Security Engineer | Major | SubscribeSnapshotAsync doesn't re-resolve permissions | Applied: §3 SubscribeSnapshotAsync doc mandates permission re-resolution on every emission; stream terminates on revocation |
| S-22 | Security Engineer | Major | Audit-before-action broken on denial (no audit on deny path) | Applied: §4 order-of-operations specifies TacticalAuthorizationDenied emitted on ALL denial paths including permission denial |
| A1 | WCAG/a11y | Critical | Assertive region fires on status changes (not just additions) | Applied: §7.3 Lookout specifies assertions-only; Acknowledged status-changes MUST use separate polite channel |
| A2 | WCAG/a11y | Critical | Native `disabled` vs `aria-disabled` not specified | Applied: §7.5 mandates `aria-disabled="true"` (NEVER native disabled); tabindex remains; SunfishA11yAssertions.AriaDisabledButtonRemainsInTabOrder REQUIRED |
| A3 | WCAG/a11y | Critical | Missing `role="alertdialog"` + aria-modal on confirmation dialog | Applied: §7.6 specifies `role="alertdialog" aria-modal="true" aria-labelledby aria-describedby`; SunfishA11yAssertions.AlertDialogHasRoleModalLabelDescribedBy REQUIRED |
| A4 | WCAG/a11y | Critical | No SR announcement when confirm button becomes enabled | Applied: §7.6 specifies polite live region injecting "Confirm available" at t=2000ms; SunfishA11yAssertions.DeliberationPauseAnnouncesEnablement REQUIRED |
| A5 | WCAG/a11y | Critical | aria-label on runbook steps double-announces content | Applied: §7.3 Fire Control specifies aria-labelledby referencing step-number span + step-title span (not aria-label) |
| A8 | WCAG/a11y | Major | §7.3 and §7.5 inconsistent on skip-link targets | Applied: §7.3 enumerates per-sub-room skip-link targets with IDs; HTML example added |
| A9 | WCAG/a11y | Major | Pause control label/aria-pressed pattern ambiguous | Applied: §7.3 specifies static label "Pause Lookout ticker" + aria-pressed toggle (matching ADR 0080 §4 pattern) |
| A10 | WCAG/a11y | Major | SC 2.3.1 ignores red-flash; flashing default not addressed | Applied: §7.4 specifies default-no-flash; flashing is opt-in; both general + red flash thresholds cited |
| A11 | WCAG/a11y | Major | SC 2.2.2 pause requirement not universal | Applied: §7.5 requires pause universally; auto-pause on hover + keyboard focus |
| A12 | WCAG/a11y | Major | Severity icon shapes not specified; aria-hidden missing | Applied: §7.4 adds distinct shapes per severity; all icons aria-hidden="true" |
| A13 | WCAG/a11y | Major | Dialog outcome not announced on close | Applied: §7.6 requires polite live region outcome announcement; SunfishA11yAssertions.DialogOutcomeAnnouncedOnClose REQUIRED |

---

## Structural-citation spot-check (post-amendment)

| Symbol | Status |
|---|---|
| `Sunfish.Foundation.Assets.Common.TenantId` | PASS ✓ |
| `Sunfish.Foundation.Assets.Common.ActorId` | PASS ✓ |
| `Sunfish.Foundation.Capabilities.Principal` | PASS ✓ |
| `Sunfish.Foundation.Ship.Common.ShipRole` | FORWARD-REF (W#46) |
| `Sunfish.Foundation.Ship.Common.ShipAction` | FORWARD-REF (W#46) |
| `Sunfish.Foundation.Ship.Common.IPermissionResolver` | FORWARD-REF (W#46; startup-check API TBD) |
| `Sunfish.Foundation.Ship.Common.ISystemPrincipalProvider` | FORWARD-REF (new type; ADR 0077 amendment or standalone) |
| `Sunfish.Kernel.Audit.AuditEventType` | PASS ✓ (ADR 0049) |
| `Sunfish.Foundation.Wayfinder.IStandingOrderRepository.AppendAsync` | PASS ✓ (ADR 0065 §2) |
| `Sunfish.Foundation.Quarterdeck.IQuarterdeckAlertSource` | FORWARD-REF (ADR 0080 §2) |
| `NodaTime.Instant` | PASS ✓ |
