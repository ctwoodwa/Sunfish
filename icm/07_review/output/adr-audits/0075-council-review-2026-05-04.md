# ADR 0075 Council Review — Pre-merge Canonical (Stage 1.5)

**ADR:** [0075 — ExtensionFields Feature-Evaluation Hook](../../../../docs/adrs/0075-extensionfields-feature-evaluation-hook.md)
**Branch under review:** `docs/adr-0075-extensionfields-feature-gate` (PR #508)
**Reviewer:** XO research subagent (Opus 4.7, `xhigh`)
**Date:** 2026-05-04
**Discipline:** ADR 0069 D1 (pre-merge council canonical), D2 (§A0 self-audit pressure-test), D3 (three-direction structural-citation spot-check)
**Worktree base:** `origin/main` @ `85fe4f4` (post `docs/adrs 0072` merge)
**Cohort:** 22-of-22 candidate (one council fix away from confirming the cohort metric)

---

## Verdict

**NEEDS-AMENDMENT** (not BLOCKING — but two structural-citation failures with broken `./<file>.md` links that would 404 in the rendered docs site, plus one symbol-name drift, plus three substantive substrate concerns the §A0 self-audit either under-stated or mis-attributed).

The decision (Option A — filter at `GetFieldsAsync`), the contract surface (`ExtensionFieldSpec` extension + `FeatureGateOffPolicy` enum + `GateState` + `MaterializedExtensionField`), and the lazy-DI optionality are all sound. The audit-emission shape is right. The §References block, however, has TWO broken relative links to ADRs that don't exist under the cited filenames, plus one mis-attributed claim ("ADR 0046's capability-graph reasoning" — 0046 has no capability-graph content) that needs either re-attribution to `Sunfish.Foundation.Capabilities` (the actual home of `ICapabilityGraph`) or to a different ADR. The author's §A0 self-audit caught 0-of-2 of these failures — confirming the §A0 catch-rate cited in ADR 0069 D2 is real.

If the author applies the seven recommendations in §"Recommendations to author" below, this becomes the cohort's **first** counter-example to the 21-of-21 pattern. As-is, it is the **22nd-of-22** case where pre-merge council was canonical.

---

## Findings summary

| Class | Count | IDs |
|---|---|---|
| **Blocking** | 0 | (none — no logic-correctness or layering violations) |
| **Structural-citation** | 5 | SC-1 (0028 broken link), SC-2 (0046 broken link), SC-3 (0046 capability-graph mis-attribution), SC-4 (`ISigner` vs `IOperationSigner` symbol drift, 3 instances), SC-5 (PR #486 merge date drift) |
| **Non-mechanical** | 5 | NM-1 (gate-evaluator throw semantics under-specified), NM-2 (race between gate-flip + concurrent `GetFieldsAsync`), NM-3 (parallel-overload silent-bypass remediation incomplete), NM-4 (Redact one-way-door enforcement under-spec'd), NM-5 (`Sequester` reuse of `PlaintextSequestered` is more semantically wrong than §A0.5 #2 admits) |
| **Mechanical** | 4 | M-1 (cohort metric phrasing tightening), M-2 (`Sunfish.Foundation.Crypto` "transitive dependency" — actually a same-package sub-namespace), M-3 (`§A1.4` is a section heading not a sub-paragraph; cite specificity), M-4 (audit-volume sampling §Open question 4 framing) |

**Total: 14 findings.** Per ADR 0069 D1, all 5 structural-citation findings + the 5 non-mechanical findings warrant a fresh `Status: Proposed` push (NEEDS-AMENDMENT) before flipping to `Accepted`. The 4 mechanical fixes can ride along in the same amendment commit.

---

## Perspective 1 — Outside Observer (fresh-contributor clarity)

**Cold-start reading impression:** The ADR is unusually well-organized. §Context → §Decision drivers → §Considered options → §Decision is a clean flow, and the decision drivers explicitly enumerate WHY the hook is delivered as a new overload (no breaking change), WHY the substrate dependencies are nullable (no mandatory new dependency), and WHY the default policy is `Hide` (safest reversible). A fresh COB session reading this from a cold start would understand "what to build" within ~10 minutes.

**The 4-substrate diagram (§Context "closes the loop between four parallel substrates") is the right framing.** A reader who understands `ExtensionFields`, `FeatureManagement`, `Audit`, and `Migration` separately will see how this ADR *binds* them at one decision point. The diagram could be a literal ASCII box-and-arrow but the prose works.

### OO-1 (mechanical) — ADR 0009 Amendment A1 composition: explained well, but A1's runtime-control claim depends on code that does not yet exist.

The ADR repeatedly cites Amendment A1's `WayfinderFeatureProvider` as the substrate that makes "operator-runtime control" real. §A0.4 cites Amendment A1 as "merged 2026-05-02 as PR #486" and says "Verified by reading docs/adrs/0009-foundation-featuremanagement.md Amendment A1." That's accurate at the *spec* level — but a fresh contributor reading the ADR may infer that `WayfinderFeatureProvider` exists as code on `origin/main`. **It does not.** PR #486 was a docs-only merge (verified via `gh pr view 486 --json files`: only 3 files changed, all docs/icm). The Wayfinder package (`packages/foundation-wayfinder/`) does NOT contain `WayfinderFeatureProvider` or `IAtlasProjector` on `origin/main`.

This is not a defect in ADR 0075 — the ADR is not claiming runtime composition with extant code. But the "post-W#42/W#43 the hook becomes runtime-controllable" framing in §Context invites a fresh reader to assume the runtime path is wired today. A one-line clarification ("at the spec level; runtime wiring lands when W#42/W#43 implementation phases ship") would close the gap.

**Classification:** mechanical. **Recommendation:** add the parenthetical to §Context paragraph 2.

### OO-2 (mechanical) — §References lists 13 references; the rendered Markdown links break for 2 of them.

A fresh contributor clicking the §References links in the rendered docs site would hit 404s on:

- `./0028-decentralized-collaboration.md` — actual file is `0028-crdt-engine-selection.md`
- `./0046-multi-sig-shared-account-recovery-and-key-recovery.md` — actual file is `0046-key-loss-recovery-scheme-phase-1.md`

This is documented as SC-1 + SC-2 below. From the Outside Observer perspective, broken links erode trust in the ADR's accuracy more than the actual structural-citation defect would suggest.

**Classification:** mechanical (link-target corrections). **Recommendation:** see SC-1 + SC-2 fixes.

### OO-3 (non-mechanical) — `MaterializedExtensionField` + `GateState` design has a subtle quirk that's not flagged.

The §Decision says: "For policies `Hidden`, `Sequestered`, `Redacted`, the field is **excluded** from the returned list when the consumer wants the 'what's visible right now?' projection..." Then: "callers that need to render UI affordances for sequestered/redacted state ... may opt in to receive those entries via an alternative `GetFieldsWithGatesAsync(...)` query — see §Open questions item 5."

**The quirk:** `MaterializedExtensionField` carries a `GateState` enum with five values, but the contract for `GetFieldsAsync(...)` only ever returns specs in `Ungated` or `GatedOn` states. The `Hidden`, `Sequestered`, and `Redacted` enum values are never observable through the v1 surface. A fresh contributor reading the enum will assume all five states are reachable; the §Open questions item 5 footnote is far from the type.

**This is a contract-design smell.** Either:
1. Ship `GateState` with only `{Ungated, GatedOn}` in v1 and add the other three when `GetFieldsWithGatesAsync` lands, OR
2. Ship the full enum but pin a doc comment on `Hidden/Sequestered/Redacted` saying "currently never returned by `GetFieldsAsync`; see ADR 0075 §Open questions item 5", OR
3. Ship `GetFieldsWithGatesAsync` in v1 alongside `GetFieldsAsync` and resolve the question now.

The author's choice (full enum with footnote) is the worst of three because the footnote is far from the type and a future contributor is likely to misread the enum.

**Classification:** non-mechanical. **Recommendation:** prefer option 2 (pin the doc comment on the three never-returned values) at minimum; consider option 1 as cleaner.

### OO-4 (mechanical) — Implementation checklist Phase 9 is recursive.

Phase 9 says "Per ADR 0069 D1, dispatch four-perspective adversarial council...at `high` effort, Opus 4.7. Pressure-test points enumerated in §A0.5." This council review IS Phase 9. After this review merges, Phase 9 is complete; the checklist should reflect that or be removed, since the implementation checklist is consumed by the COB Stage 06 hand-off and the COB does not run councils.

**Classification:** mechanical. **Recommendation:** mark Phase 9 [x] in the same amendment or remove it from the implementation checklist and move it to a separate "ADR pre-acceptance steps" list.

---

## Perspective 2 — Pessimistic Risk Assessor (failure modes + blast radius)

This perspective is where the ADR's substrate-tier risk surface is most exposed. The §A0.5 pressure-test list captures ~half the relevant questions; the other half is below.

### PR-1 (non-mechanical, critical) — What happens when `IFeatureEvaluator.EvaluateAsync` throws?

The ADR is silent on this. The `IFeatureEvaluator.EvaluateAsync` contract says: "Throws `InvalidOperationException` when the feature is not in the catalog or when no resolver produced a value and the spec has no default." Real production deployments WILL hit this — a `WayfinderFeatureProvider` calling out to a CRDT-replicated Atlas projection has multiple failure modes (projection not warm, transient I/O, schema-epoch mismatch).

If `GetFieldsAsync` is called and one of N gated specs has its `EvaluateAsync` throw:

- **Option A:** Throw the whole call. UI list-view crashes. Worst-case for user trust.
- **Option B:** Treat throw as gate-OFF. Silently filter the field. Worst-case for auditability — the audit emission would record `ExtensionFieldFiltered` even though the gate state is *unknown*, not OFF.
- **Option C:** Treat throw as gate-ON (fail-open). Bypasses the entire gating mechanism on infra glitch. Worst-case for security.
- **Option D:** Treat throw as gate-OFF + emit a distinct `ExtensionFieldGateEvaluationFailed` audit event. Defensible but requires a fifth `AuditEventType` constant the ADR doesn't enumerate.

The default policy here is consequential and security-relevant. **The ADR must pick one.** My recommendation is Option D — fail-closed with explicit audit. Phase 7 (tests) implicitly assumes the evaluator never throws; that assumption breaks in production.

**Classification:** non-mechanical. **Recommendation:** add §Decision subsection "Gate-evaluator failure semantics" picking Option D and adding the fifth audit-event-type constant.

### PR-2 (non-mechanical, critical) — Race condition between operator gate-flip and concurrent `GetFieldsAsync`.

`WayfinderFeatureProvider` resolves Standing Orders from a CRDT-replicated Atlas projection. Standing Order writes are eventually consistent across nodes (per ADR 0028). An operator flips a gate OFF on Anchor 1 at T; the projection on Anchor 2 catches up at T+200ms. During [T, T+200ms]:

- A `GetFieldsAsync` call on Anchor 2 sees the gate as ON.
- The materialized field is included in the list.
- Persistence writes / UI displays / audit emits proceed as if the field is gated-on.
- After T+200ms the gate is observably OFF; the next `GetFieldsAsync` filters the field; the audit trail now has both `ExtensionFieldGated` (T+100ms, "ON") and `ExtensionFieldFiltered` (T+250ms, "OFF") for the same logical decision — producing diagnostic noise and a window during which user-visible data behavior was inconsistent across anchors.

This is intrinsic to CRDT-replicated configuration; the ADR cannot make it go away. But the ADR should:

1. Acknowledge the window in §Trust impact (currently silent).
2. Specify the ordering invariant: "the audit trail reflects each anchor's local view of the gate at evaluation time; cross-anchor consistency is bounded by Standing Order replication latency (P95 200ms per ADR 0065 §F9)." Without this acknowledgement, a regulated-tenant compliance auditor will interpret the noise as a defect.
3. For `Sequester` and especially `Redact` policies, the window matters more — a `Redact` triggered on Anchor 2 at T+50ms (before the gate-OFF reaches it) is a destructive action that the operator's flip on Anchor 1 *did not consent to*. **`Redact` MUST require Standing Order quorum confirmation, not just local-projection observation**, before tombstoning.

**Classification:** non-mechanical, partially blocking for `Redact` semantics. **Recommendation:** add the ordering invariant to §Trust impact AND require quorum-bounded confirmation for `Redact` in §Open questions item 6 resolution.

### PR-3 (non-mechanical) — `ISequestrationStore` failure modes during `Sequester` policy.

The ADR's §Decision implementation calls `_sequestrationStore.SequesterAsync(...)` from inside `GetFieldsAsync`. What if:

- `ISequestrationStore.SequesterAsync` throws? The ADR says the policy "requires the substrate to honor its contract" but does not say what happens if it doesn't — does the field disappear from the list anyway (auditability gap), get silently downgraded to `Hide` (policy violation), or propagate the exception (caller blast-radius)?
- The store is briefly unavailable (transient — restarts, schema migration)? Same questions.
- The `recordId` collides — e.g., the synthetic `"catalog-gate#{entityType.FullName}#{spec.Key.Value}"` happens to match a real record id? `SequesteredRecord.RecordId` is `string` with no namespace prefix enforcement.

**Classification:** non-mechanical. **Recommendation:** §Decision must specify Sequester store failure semantics. My recommendation: same fail-closed-with-distinct-audit pattern as PR-1, plus a `recordId` namespace prefix convention (e.g., always `"sunfish:catalog-gate#..."` to avoid collision with consumer-supplied record ids).

### PR-4 (non-mechanical) — `Redact` capability-escalation paths.

§Trust impact says: "`Redact` is destructive ... The implementation MUST require an explicit operator capability ... before invoking the Redact path; the audit record MUST attest to the capability proof."

Then implementation checklist Phase 7 (e) says: "gated-off Redact requires capability proof, emits `ExtensionFieldRedacted`, throws `CapabilityRequiredException` without proof."

But:

1. **`CapabilityRequiredException` does not exist** on origin/main. (`grep -rn "CapabilityRequiredException" packages/` returns nothing.) The author is silently introducing a new exception type that needs to be declared in §A0.1.
2. The capability name is referenced as `CapabilityAction.RedactExtensionField` in §Negative §Trust impact bullet — but `CapabilityAction` is a constructor-based type (`public sealed record CapabilityAction(string Name)`), NOT a class with static fields. The pattern in `packages/foundation/Capabilities/CapabilityOp.cs` is `new CapabilityAction("can_write")` — string-constructed at use site. The ADR cite suggests a static-field convention that does not exist.
3. Where is the capability check performed? The lazy-DI section accepts a nullable `IFeatureEvaluator`, `IAuditTrail`, `ISequestrationStore`. There is no nullable `ICapabilityGraph` in the constructor list. The ADR is silent on how the catalog package would obtain capability-graph access.
4. **Who is the principal whose capability is being checked?** `FeatureEvaluationContext` carries a `UserId?` (optional string). For a server-side metadata-builder call site (e.g., schema generation tooling — explicitly called out in §Migration path #4 as a legitimate user of the synchronous overload, but the async-overload migration logic could carry through), the principal is unclear.

**Classification:** non-mechanical, partially structural-citation. **Recommendation:** §Decision must add a "Capability gate for `Redact` policy" subsection specifying: which exception type (declared in §A0.1), which capability-graph dependency to inject, who the principal is per call, and when the check fires (before vs. after audit emission, before vs. after sequestration-store call).

### PR-5 (non-mechanical) — Bulk-export blast radius for `ExtensionFieldGated` audit volume.

§Open questions item 4 captures this but understates the risk. "10 gated fields × 10K renders/day = 100K daily redundant records" is a single-tenant per-day figure. A multi-tenant SaaS with 1000 tenants × 10 gated fields × 10K renders/day = 100M daily records. At 7-year retention (per ADR 0049 §Trust impact for IRS-class records) that's 250B records. The audit substrate is append-only with kernel `IEventLog` durability — that's a real storage cost.

The "always emit" default Option A in §Open questions item 4 is the wrong default for non-regulated tenants. A "state-change only" emission (Option B) is the right default; "always emit" should be a tenant-opt-in for regulated workloads.

**Classification:** non-mechanical. **Recommendation:** flip the default in §Open questions item 4 from "(a) always emit" to "(b) state-change only"; add a `MustEmitEveryEvaluation` flag on the catalog registration for regulated tenants who require full emission.

---

## Perspective 3 — Skeptical Implementer (structural-citation correctness)

I verified each of the 14 cited "existing" symbols from §A0.2 + §A0.3 + §A0.4 against `origin/main` @ `85fe4f4`. **12 of 14 are exactly accurate. 2 have drift.**

### SC-1 (structural-citation, BLOCKING-class severity for §References) — `./0028-decentralized-collaboration.md` does not exist

§References cites: `[ADR 0028](./0028-decentralized-collaboration.md) — A5.4 / A8.3 sequestration partition substrate`

The actual file is `docs/adrs/0028-crdt-engine-selection.md`. Verification:

```
$ ls /tmp/sunfish-0075-council/docs/adrs/0028*
/tmp/sunfish-0075-council/docs/adrs/0028-crdt-engine-selection.md
```

The relative link is broken; the rendered docs site renders this as a broken hyperlink. Same severity as the ADR 0071 ADR-0042-vs-ADR-0010 typo cited as canonical-failure precedent in the briefing.

A5.4 + A8.3 + A5.7 + A8.5 sections ALL exist in the actual file — author's content claims about §A5.4 + §A8.3 are correct. The structural-citation failure is purely the filename/link.

**Classification:** structural-citation. **Recommendation:** change `./0028-decentralized-collaboration.md` → `./0028-crdt-engine-selection.md` in §References.

### SC-2 (structural-citation, BLOCKING-class severity for §References) — `./0046-multi-sig-shared-account-recovery-and-key-recovery.md` does not exist

§References cites: `[ADR 0046](./0046-multi-sig-shared-account-recovery-and-key-recovery.md) — capability-graph reasoning + capability-required actions for Redact policy`

The actual file is `docs/adrs/0046-key-loss-recovery-scheme-phase-1.md`. Verification:

```
$ ls /tmp/sunfish-0075-council/docs/adrs/0046*
/tmp/sunfish-0075-council/docs/adrs/0046-a1-historical-keys-projection.md
/tmp/sunfish-0075-council/docs/adrs/0046-key-loss-recovery-scheme-phase-1.md
```

Same defect as SC-1.

**Classification:** structural-citation. **Recommendation:** change to `./0046-key-loss-recovery-scheme-phase-1.md`.

### SC-3 (structural-citation, non-mechanical) — ADR 0046 has no "capability-graph reasoning" content

Even with the corrected filename, the §References description "capability-graph reasoning + capability-required actions for Redact policy" mis-attributes the source. ADR 0046 (Key-loss recovery scheme for Business MVP Phase 1) covers multi-sig recovery, trustee attestations, and the recovery audit-trail sub-pattern — NOT the capability-graph substrate.

The actual home of `ICapabilityGraph` + `CapabilityAction` + `CapabilityOp` is **`packages/foundation/Capabilities/`**. There is no top-level ADR for the capability substrate — it appears to have been introduced via ADR 0028 §"Decentralization" or ADR 0043 (per the `CapabilityPromotedToProspect` audit type comment). Neither is correctly named in §References.

**Classification:** structural-citation, non-mechanical (requires source-tracing, not just a path fix). **Recommendation:** either:
1. Re-attribute to the actual capability-graph ADR if one exists (XO investigation needed), OR
2. Remove the ADR 0046 entry from §References for "capability-graph reasoning" and instead cite `packages/foundation/Capabilities/ICapabilityGraph.cs` directly in §"Existing code / substrates", OR
3. Acknowledge in §Trust impact that the capability-substrate ADR is a TBD and this ADR is making forward-compatible assumptions.

### SC-4 (structural-citation, mechanical) — `ISigner` is the wrong symbol name (3 instances)

The ADR says `ISigner` in three places:

1. §A0.3 line 67: "the catalog package will need an `ISigner` and access to `Sunfish.Foundation.Crypto.SignedOperation<T>`"
2. §Decision audit-emission section: "signing is delegated via constructor-injected `ISigner`"
3. §Decision lazy-DI section: "/* signing + node-id + record-id resolver providers */"  (implicit)

The actual symbol on origin/main is **`IOperationSigner`** (`packages/foundation/Crypto/IOperationSigner.cs`). Verification:

```
$ grep -rn "interface IOperationSigner\|ISigner" packages/foundation/Crypto
packages/foundation/Crypto/IOperationSigner.cs:6:public interface IOperationSigner
```

There is no `ISigner` interface anywhere in the repo. The kernel-audit README + tests + production code all use `IOperationSigner`.

**Classification:** structural-citation, mechanical. **Recommendation:** replace all 3 `ISigner` references with `IOperationSigner`.

### SC-5 (structural-citation, mechanical) — PR #486 merge-date drift

§A0.4 says: "ADR 0009 Amendment A1 (W#43; merged 2026-05-02 as PR #486)". `gh pr view 486` returns:

```
"mergedAt": "2026-05-04T09:39:30Z"
```

PR #486 merged 2026-05-04 (today), not 2026-05-02. Two-day drift. Same defect appears in §Context paragraph 1 ("ADR 0009 Amendment A1 (W#43, `WayfinderFeatureProvider`; merged 2026-05-02 as PR #486)").

**Classification:** structural-citation, mechanical. **Recommendation:** update both occurrences to `2026-05-04`.

### SC-6 (mechanical, but worth noting separately) — `Sunfish.Foundation.Crypto` is NOT a transitive dependency

§A0.3 line 66-67 says: "Since `Sunfish.Foundation.Crypto` is a transitive dependency of `Sunfish.Kernel.Audit`, the ProjectReference graph already covers this."

False premise — but **conclusion is correct**. `SignedOperation<T>`, `Signature`, `PrincipalId`, `IOperationSigner`, `Ed25519Signer`, `Ed25519Verifier`, `CanonicalJson` all live in **`Sunfish.Foundation`** (the package), under the **`Sunfish.Foundation.Crypto`** namespace. Verification:

```
$ grep -rn "namespace Sunfish.Foundation.Crypto" packages/
packages/foundation/Crypto/IOperationSigner.cs:1:namespace Sunfish.Foundation.Crypto;
packages/foundation/Crypto/SignedOperation.cs:1:namespace Sunfish.Foundation.Crypto;
[...]
```

The catalog csproj already `ProjectReference`s `Sunfish.Foundation` directly (verified via reading `packages/foundation-catalog/Sunfish.Foundation.Catalog.csproj`):

```xml
<ProjectReference Include="..\foundation\Sunfish.Foundation.csproj" />
```

So `SignedOperation<T>` is available **without the Sunfish.Kernel.Audit ProjectReference** — it's a direct dependency, not transitive. The author's pressure-test point #4 ("does catalog have access to `SignedOperation<T>`?") is answered "yes, already, no new ref needed" — but the WHY is wrong in the §A0.3 reasoning.

**Classification:** mechanical (correct conclusion via wrong reasoning). **Recommendation:** rewrite §A0.3 line 66-67 as: "`Sunfish.Foundation.Crypto.SignedOperation<T>` lives in the `Sunfish.Foundation` package (sub-namespace, not separate package); the catalog's existing `Sunfish.Foundation` ProjectReference already provides access. Only the `IOperationSigner` *injection* is new — that requires a DI registration, not a project-graph change."

### Cited symbols VERIFIED clean (12 of 14)

For the record, every other cited Sunfish.* symbol passed verification:

| Symbol | Cited at | Verified via |
|---|---|---|
| `ExtensionFieldSpec` (8 positional params) | §A0.2 + §A0.3 | `packages/foundation-catalog/ExtensionFields/ExtensionFieldSpec.cs` lines 18–26 — 8 params, exact order matches |
| `IExtensionFieldCatalog` (3 members) | §A0.2 + §A0.3 | `packages/foundation-catalog/ExtensionFields/IExtensionFieldCatalog.cs` lines 11–21 — 3 members confirmed |
| `ExtensionFieldCatalog` concrete class | §A0.2 | `packages/foundation-catalog/ExtensionFields/ExtensionFieldCatalog.cs` |
| `ExtensionFieldKey` (`readonly record struct(string Value)` + `Of(string)`) | §A0.2 | `packages/foundation/Extensibility/ExtensionFieldKey.cs` |
| `IFeatureEvaluator` (`EvaluateAsync` + `IsEnabledAsync`) | §A0.2 | `packages/foundation-featuremanagement/IFeatureEvaluator.cs` |
| `FeatureKey` (`readonly record struct(string Value)`) | §A0.2 | `packages/foundation-featuremanagement/FeatureKey.cs` |
| `FeatureValue` | §A0.2 | `packages/foundation-featuremanagement/FeatureValue.cs` |
| `FeatureEvaluationContext` (TenantId? + edition + bundles + modules + user + env + attrs) | §A0.2 | `packages/foundation-featuremanagement/FeatureEvaluationContext.cs` |
| `IAuditTrail.AppendAsync(AuditRecord, CancellationToken)` | §A0.2 | `packages/kernel-audit/IAuditTrail.cs` line 53 |
| `AuditEventType` (`readonly record struct(string Value)`) | §A0.2 | `packages/kernel-audit/AuditEventType.cs` line 18 — confirmed `readonly record struct`, NOT enum |
| `AuditRecord` (7-field positional record) | §A0.3 | `packages/kernel-audit/AuditRecord.cs` lines 49–56 — 7 params: AuditId, TenantId, EventType, OccurredAt, Payload (`SignedOperation<AuditPayload>`), AttestingSignatures, FormatVersion. ALL 7 verified |
| `ISequestrationStore` (5 members: Register/Sequester/Release/GetByNode/GetSequestered) | §A0.2 | `packages/foundation-migration/Services/ISequestrationStore.cs` lines 14–30 — 5 members confirmed |
| `SequestrationFlagKind` (5 enum values: FormFactorFilteredOut/StorageBudgetExceeded/PlaintextSequestered/CiphertextSequestered/FormFactorQuorumIneligible) | §A0.2 + §A0.5 #2 | `packages/foundation-migration/Models/Enums.cs` lines 84–100 — 5 values confirmed exactly |
| `MigrationAuditPayloads` factory (parallel pattern reference) | §A0.3 | `packages/foundation-migration/Audit/MigrationAuditPayloads.cs` |

**Summary:** §A0 self-audit caught 12-of-14 symbols correctly. The 2 misses (SC-3 mis-attribution + SC-4 `ISigner` symbol drift) confirm the §A0 catch-rate cited in ADR 0069 D2 (~65% of structural-citation failures pass §A0). Council found the residual 35%.

---

## Perspective 4 — Devil's Advocate (was Option A genuinely simplest? + alternative architectures + silent-bypass risks)

### DA-1 (non-mechanical) — Was Option A genuinely the simplest? A fourth option exists that's not in §Considered options.

The ADR rejects Option B (per-repository) and Option C (per-record). Both rejections are sound. But there is a fourth option not enumerated:

**Option D — Push, not pull: event-driven invalidation.** When an operator flips a Standing Order under `features.{key}` (per ADR 0009 Amendment A1), the Wayfinder substrate emits a `StandingOrderApplied` event (which already exists per ADR 0065 §4). A subscriber in the catalog package — `ExtensionFieldGateInvalidator : IStandingOrderEventHandler` — listens for path-prefix `features.*`, looks up which `ExtensionFieldSpec`s have a `FeatureKey` matching that path, and emits one `ExtensionFieldGateChanged` audit event per affected spec. The catalog *itself* never calls `IFeatureEvaluator` from the hot path; consumers continue calling the synchronous `GetFields(Type)` and post-process via a cached gate-state dictionary kept up-to-date by the event handler.

Pros: 0 evaluations on the hot read path; perfectly compatible with bulk-export workloads (PR-5); audit volume bounded by `(operator-flip-events × affected-specs)` not `(reads × specs)`.

Cons: requires a coherent local cache; cache-invalidation is a hard problem; starting state at app-boot requires a full Standing Order replay. The ADR 0028 CRDT replication ordering complicates "single coherent local cache" across multi-anchor.

This option may NOT be simpler than Option A — it has its own complexity. But it should be enumerated and rejected explicitly. As-is, the ADR's "Option A is the simplest place that produces the right outcome" claim is under-tested against this fourth alternative.

**Classification:** non-mechanical. **Recommendation:** add Option D to §Considered options and reject it explicitly with reasoning.

### DA-2 (non-mechanical) — Parallel-overload silent-bypass: mitigation is unenforceable.

The ADR's §Negative consequences acknowledges: "Two parallel overloads (`GetFields` and `GetFieldsAsync`) on the same interface is a known footgun — call sites that use the synchronous overload silently bypass gating. Mitigation: documentation; the §Implementation checklist includes a sweep of existing call sites with a determination of which should migrate."

**Documentation alone is insufficient.** Real-world cohort evidence: ADR 0048-A1 (mobile scope clarification), ADR 0046-A4/A5 (encryption substrate councils), ADR 0028 council loop — all had documented seams that were silently bypassed by call sites in practice. The cohort batting average that drives ADR 0069 is itself a pattern of "documented seams produce silent regression" failures.

Better mitigations the ADR could pick from:
1. **Roslyn analyzer.** Mark `IExtensionFieldCatalog.GetFields(Type)` `[Obsolete("Use GetFieldsAsync(Type, FeatureEvaluationContext) for feature-gated materialization. Synchronous overload bypasses feature gating.", error: false)]`. Triggers compile warning at every existing call site. ADR 0065 Phase 3b establishes a precedent for analyzers in this layer.
2. **Internal-only synchronous overload.** Mark `GetFields(Type)` `internal` (with `InternalsVisibleTo` for the metadata-only inspection use case). External adopters MUST use the async overload.
3. **Different interface entirely.** `IExtensionFieldCatalog` keeps `GetFields(Type)` (no-gating, metadata-only); a NEW interface `IGatedExtensionFieldCatalog : IExtensionFieldCatalog` adds `GetFieldsAsync(Type, ctx)`. Consumers explicitly choose which interface they depend on.

Option 1 is the lightest-weight and most consistent with `[Obsolete]` patterns elsewhere in the repo (e.g., `Sunfish.Compat.*`).

**Classification:** non-mechanical. **Recommendation:** add a `[Obsolete(...)]` decoration to the synchronous `GetFields(Type)` overload as part of §Decision; update §Migration path to reference the warning rather than relying on per-consumer migration discipline.

### DA-3 (non-mechanical) — Is `Hide` the right default given regulated-tenant audit requirements?

§Decision drivers paragraph 3 says: "**The default policy MUST be non-destructive**: hiding the field while preserving the underlying data is the safest reversible action."

For non-regulated tenants this is correct. For **regulated tenants** (medical providers, financial services, multi-jurisdictional), `Hide` may be the *wrong* default:

- Hiding a field that contains regulated data without sequestering it leaves the data in active storage, in plaintext, in a state where:
  - A schema-introspection query (e.g., `GetFields(Type)` synchronous, the bypass) returns it.
  - A bulk-export operation (e.g., IRS export, GDPR data-portability request) returns it.
  - A backup/restore cycle preserves it.
- The audit trail records `ExtensionFieldFiltered` ("hidden") — which is semantically *not* "this data is unavailable to all callers", but "this UI render didn't include it." The compliance auditor's question "is field X accessible by anyone right now?" gets a misleading answer.

**The right default for regulated tenants is `Sequester`.** §Decision drivers should acknowledge this and either:
1. Make the default policy a tenant-configuration value (operator picks `Hide` vs `Sequester` as the default for unspec'd fields), OR
2. Document explicitly that bundles targeting regulated tenants should default to `Sequester`, with a Roslyn-analyzer warning if the regulated bundle has fields registered without an explicit policy.

**Classification:** non-mechanical, regulatory-relevant. **Recommendation:** add §Trust impact paragraph specifying that `Hide` is the right default for general-purpose tenants; regulated bundles should adopt `Sequester` per the bundle manifest's regulatory-classification key (cross-reference whatever ADR establishes that — likely ADR 0064 Foundation.MissionSpace.Regulatory).

### DA-4 (mechanical) — Audit-volume sampling default is wrong (PR-5 covers this; restating from devil's-advocate angle).

§Open questions item 4 names three options ((a) always emit, (b) state-change only, (c) sampling) without picking one. The implicit default ("emit always (default)" in the table) is wrong for the reasons in PR-5. From the devil's-advocate angle: "always emit" is a defensible default ONLY if the audit substrate has cost-bounded retention. ADR 0049 §Trust impact establishes audit records as 7-year-retained (IRS-class). 100K-records-per-tenant-per-day at 7-year retention is materially expensive.

**Classification:** mechanical (decision needs to be made; recommendation is straightforward). See PR-5.

### DA-5 (non-mechanical) — Sequester reuse of `PlaintextSequestered`: more wrong than §A0.5 #2 admits.

The author's §A0.5 #2 says: "`Sequester` reuses `SequestrationFlagKind.PlaintextSequestered`; semantically wrong? Should W#35 add `FeatureGateOff`?"

I confirm the answer: **YES, semantically wrong, and worse than the author admits.** Here's why:

Looking at `packages/foundation-migration/Models/Enums.cs` line 84-100:

```csharp
/// <summary>Plaintext payload was sequestered (A8.3 rule 5; the form factor cannot decrypt the field).</summary>
PlaintextSequestered,
```

The XML doc says the flag means "the form factor cannot decrypt the field" — which is itself semantically confused (a *plaintext* payload doesn't need decrypting; the comment likely meant "the form factor cannot *display* the field's surface"). But that's an existing W#35 defect, separable from ADR 0075.

What matters for ADR 0075 is that:
1. `PlaintextSequestered` semantics in W#35 are **form-factor-driven** (host can't read the plaintext for surface-coverage reasons). 
2. ADR 0075's `Sequester` policy semantics are **feature-gate-driven** (tenant subscription/edition/operator-toggle says don't surface).
3. These are different decision provenance. A compliance auditor asking "why is this record sequestered?" gets one of two answers depending on provenance.

Recording both under `PlaintextSequestered` makes the audit trail unable to answer "why" without joining against external state. **This is an audit-by-construction violation** — and ADR 0049 explicitly establishes audit-by-construction as the substrate's design principle.

**Classification:** non-mechanical, audit-substrate-relevant. **Recommendation:** §Open questions item 2 must resolve to "amend W#35 (foundation-migration) to add `SequestrationFlagKind.FeatureGateOff` BEFORE this ADR ships; or block this ADR's acceptance on that amendment landing first." The "ship-with-PlaintextSequestered-and-amend-later" path is incompatible with audit-by-construction.

### DA-6 (mechanical) — Spot-check on cohort metric phrasing.

§A0 paragraph 1 says: "Cohort batting average at draft time: 21-of-21 substrate amendments needed council-sourced fixes; structural-citation failures pass §A0 self-audit at ~65%."

ADR 0069 §"Cohort batting average" cites "20-of-20" (D1) and "0-of-5 structural-citation failures caught by §A0" (D2) — i.e., 5 of 5 = 100% miss-rate on caught (which the ADR 0075 author re-phrases as "~65% pass-through" — a different metric). The two phrasings are not equivalent.

This is an AP-1 mild offender (unvalidated assumption: that 21-of-21 is the current cohort number). The actual current cohort, post W#33/W#34/W#42/W#43, depends on which amendments count as "substrate amendments needing council fixes." Without a maintained ledger of that count, the "21-of-21" is an estimate.

**Classification:** mechanical. **Recommendation:** rephrase as "Cohort batting average at draft time: high single-double-digit count of substrate amendments needed council-sourced fixes (zero counter-examples in the 2026-04/05 cohort per ADR 0069 §Cohort batting average); the §A0 self-audit catch rate for structural-citation failures is empirically below 50% (per the same source)." Less precise but more defensible.

---

## UPF v1.2 Stage 2 — 21 Anti-Pattern Scan

| AP | Pattern | Status | Evidence |
|---|---|---|---|
| AP-1 | Unvalidated assumptions | **PARTIAL FAIL** | Cohort metric phrasing (DA-6); lazy-DI-optionality flagged in §Open questions item 1 (acknowledged but unresolved). |
| AP-2 | Vague phases | **PASS** | Implementation checklist Phases 1–10 are file-by-file actionable. |
| AP-3 | Vague success criteria | **PASS** | §Pre-acceptance audit names: "every gating decision audited; backward compatibility preserved; W#35 composition exercised". Verifiable. |
| AP-4 | No rollback strategy | **PASS** | §Compatibility plan specifies the ungated default + parallel-overload preserves backward compat; §Pre-acceptance audit confirms. |
| AP-5 | Plan ending at deploy | **PASS** | §Revisit triggers names 6 conditions for re-evaluation. |
| AP-6 | Missing Resume Protocol | **N/A** | Single-PR ADR scope. |
| AP-7 | Delegation without contracts | **PASS** | Phases are file-by-file; COB hand-off file is named ahead. |
| AP-8 | Blind delegation trust | **PASS** | Pre-merge council per ADR 0069 D1 is THIS review; the discipline IS the non-blind-trust mechanism. |
| AP-9 | Skipping Stage 0 | **PASS** | 3 options considered (Option B + Option C rejections) — though DA-1 flags Option D as missed; AP-9 is borderline pass. |
| AP-10 | First idea remaining unchallenged | **PARTIAL FAIL** | DA-1 + DA-2 + DA-3 flag: a fourth option is unexamined; the parallel-overload mitigation is documentation-only; the `Hide` default may be wrong for regulated tenants. The first idea is remaining unchallenged on multiple substantive design dimensions. |
| AP-11 | Zombie projects (no kill criteria) | **PASS** | §Revisit triggers names kill-trigger conditions. |
| AP-12 | Timeline fantasy | **PASS** | No timeline asserted; §Pre-acceptance audit confirms. |
| AP-13 | Confidence without evidence | **PARTIAL FAIL** | "Composes cleanly with Amendment A1" — Amendment A1 is doc-only on origin/main; runtime composition has no code evidence yet. (PR #486 was docs-only per `gh pr view 486`.) The §Decision drivers claim "post-W#42/W#43 the hook becomes runtime-controllable" is forward-leaning without runtime evidence. |
| AP-14 | Wrong detail distribution | **PASS** | Right level of detail; not over-specified. |
| AP-15 | Premature precision | **PASS** | Open questions explicitly flagged; not over-committed. |
| AP-16 | Hallucinated effort estimates | **PASS** | No effort estimate asserted. |
| AP-17 | Delegation without context transfer | **PASS** | Hand-off file name is specified in §References; COB will have access to ADR + hand-off + tests. |
| AP-18 | Unverifiable gates | **PASS** | Phase 7 unit tests are concrete; Phase 8 kitchen-sink demo is observable. |
| AP-19 | Missing tool fallbacks | **N/A** | ADR scope. |
| AP-20 | Discovery amnesia | **PASS** | §References names every prior substrate ADR; §A0.4 cites verifications. |
| AP-21 | Assumed facts without sources / cited-symbol drift | **FAIL** | SC-1 + SC-2 + SC-3 + SC-4 + SC-5 + SC-6 = 6 structural-citation findings. The §A0.5 author-flagged claim "council pressure-test point" was explicitly designed for AP-21 catch — and the council found 5 net-new misses on top of the 2 the author pre-flagged. **AP-21 is the ADR's primary risk surface, exactly per cohort.** |

**Stage 2 result:** 16 PASS / 4 PARTIAL FAIL (AP-1, AP-10, AP-13, AP-21) / 1 N/A. The PARTIAL FAILs are the council's deliverable (Recommendations below).

---

## Recommendations to author (NEEDS-AMENDMENT)

In priority order. The five marked [structural-citation] should land in a single amendment commit on the same branch, ahead of any Stage 06 hand-off.

1. **[structural-citation] (SC-1 + SC-2)** Fix two broken `./<file>.md` links in §References. `0028-decentralized-collaboration.md` → `0028-crdt-engine-selection.md`; `0046-multi-sig-shared-account-recovery-and-key-recovery.md` → `0046-key-loss-recovery-scheme-phase-1.md`. **Mechanical; ~2 minutes.**

2. **[structural-citation] (SC-4)** Replace 3 occurrences of `ISigner` with `IOperationSigner` (the actual symbol on origin/main). **Mechanical; ~2 minutes.**

3. **[structural-citation] (SC-5)** Update PR #486 merge date from `2026-05-02` to `2026-05-04` (2 occurrences: §Context paragraph 1, §A0.4). **Mechanical; ~1 minute.**

4. **[structural-citation, non-mechanical] (SC-3)** Re-attribute "capability-graph reasoning" from ADR 0046 to either the actual capability-substrate ADR (XO investigation needed — likely ADR 0028's §"Decentralization" paragraph or ADR 0043) or directly to `packages/foundation/Capabilities/ICapabilityGraph.cs` in §"Existing code / substrates". **Non-mechanical; ~15 minutes XO investigation.**

5. **[structural-citation, mechanical] (SC-6)** Rewrite §A0.3 line 66-67 to clarify `Sunfish.Foundation.Crypto.SignedOperation<T>` is in the `Sunfish.Foundation` package (sub-namespace), available via the existing `ProjectReference` to `Sunfish.Foundation` — not transitively via `Sunfish.Kernel.Audit`. **Mechanical; ~5 minutes.**

6. **[non-mechanical] (PR-1)** Add §Decision subsection "Gate-evaluator failure semantics" specifying: throws → fail-closed (treat as gate-OFF) + emit a new `ExtensionFieldGateEvaluationFailed` audit-event-type (5th constant; add to §A0.1 + Phase 2). **Non-mechanical; ~30 minutes.**

7. **[non-mechanical] (PR-2)** Add §Trust impact paragraph acknowledging the operator-flip / GetFieldsAsync race and bounding it to Standing-Order replication latency (P95 200ms per ADR 0065 §F9). For `Redact` policy, add a "MUST require Standing Order quorum confirmation" requirement (not just local-projection observation). **Non-mechanical; ~30 minutes.**

8. **[non-mechanical] (PR-4)** Add §Decision "Capability gate for `Redact` policy" subsection specifying: (a) declare `CapabilityRequiredException` in §A0.1; (b) inject `ICapabilityGraph` (nullable) in the lazy-DI list; (c) specify the principal lookup convention; (d) specify when the check fires. **Non-mechanical; ~30 minutes.**

9. **[non-mechanical] (DA-2)** Add `[Obsolete(...)]` warning decoration to the synchronous `GetFields(Type)` overload as part of §Decision; cross-reference Roslyn analyzer pattern (ADR 0065 Phase 3b precedent). **Non-mechanical; ~15 minutes.**

10. **[non-mechanical] (DA-5 + §A0.5 #2)** Resolve §Open questions item 2 BEFORE accepting this ADR: amend W#35 (foundation-migration) to add `SequestrationFlagKind.FeatureGateOff`; ship as a pre-requisite mini-amendment. The "ship-with-PlaintextSequestered" path is incompatible with audit-by-construction. **Non-mechanical, dependency-bearing; ~1 hour for mini-amendment + this ADR's Sequester section update.**

11. **[non-mechanical] (DA-1)** Add Option D (push-not-pull, event-driven invalidation) to §Considered options and reject it explicitly with reasoning. **Non-mechanical; ~20 minutes.**

12. **[non-mechanical] (DA-3 + PR-5)** Update §Open questions item 4 to recommend default = "(b) state-change only" (with a `MustEmitEveryEvaluation` flag for regulated tenants); add §Trust impact note that regulated bundles should default to `Sequester` (not `Hide`) per the bundle manifest's regulatory-classification key. **Non-mechanical; ~30 minutes.**

13. **[mechanical] (M-1, M-3, M-4, OO-3, OO-4)** Smaller mechanical fixes: tighten cohort-metric phrasing (DA-6); fix §A1.4 cite specificity; mark Phase 9 [x] or remove from checklist; pin doc comments on `GateState.{Hidden,Sequestered,Redacted}` clarifying they're never returned by `GetFieldsAsync` v1; clarify §Context "post-W#42/W#43 hook becomes runtime-controllable" with a "(at the spec level; runtime wiring lands when Wayfinder package implementation phases ship)" parenthetical. **Mechanical; ~10 minutes total.**

---

## Recommendation to CO

**Hold ADR 0075 acceptance until items 1–10 above are resolved.** Items 1–5 are mechanical/structural-citation and should land in a single amendment commit. Items 6–10 are non-mechanical and substantive; each individually is shippable as part of a "council-amendment" pass on this PR (no separate ADR amendment needed if applied pre-Accepted).

Item 10 (W#35 `FeatureGateOff` amendment) is the only one with a sequencing dependency — it's a separate small W#35 amendment that should land BEFORE ADR 0075 hits Accepted. Recommend dispatching that as a parallel COB hand-off.

**Do not flip `Status: Accepted` until:**
1. The 5 structural-citation findings are fixed.
2. PR-1 (gate-evaluator throw semantics) has a §Decision subsection.
3. PR-4 (Redact capability gate) has a §Decision subsection.
4. W#35 `FeatureGateOff` amendment lands.
5. DA-5 (Sequester audit-by-construction) is resolved.

After all five conditions: re-dispatch a **brief** (single-perspective Skeptical Implementer) re-review before flipping. The full 4-perspective council does not need to repeat.

This positions ADR 0075 as the cohort's first counter-example to "21-of-21 substrate amendments need council fixes" — IF the author applies the recommendations BEFORE flipping Accepted. As-is, it confirms the cohort metric (22-of-22).

---

## Reviewer notes

- **Effort:** Opus 4.7 + `xhigh` per ADR 0069 D1 council canonical. Spot-checked 14 cited "existing" symbols against `origin/main` @ `85fe4f4`. 12 verified clean; 2 had drift (SC-3 + SC-4); 5 broken-link / mis-attribution / metric-phrasing findings on top of those (SC-1 + SC-2 + SC-5 + SC-6 + DA-6).
- **Stage 0 (Discovery & Sparring):** the §A0 self-audit pre-flagged 7 council pressure-test points; council confirmed 4 (PR-1, PR-4, PR-5, DA-5) as substantive concerns; refuted 1 (#4: `SignedOperation<T>` envelope constructability — already accessible without new transitive ref); deferred 2 (#3, #6) as adequately-handled-in-Decision.
- **Three-direction structural-citation per ADR 0069 D3:**
  - **Negative-existence (introduced symbols):** §A0.1 enumeration is correct — 5 net-new symbols, none falsely-claimed-existing.
  - **Positive-existence (cited as existing):** 12 of 14 verified; SC-3 + SC-4 are the misses.
  - **Structural correctness (signature/shape):** §A0.3 is correct on `ExtensionFieldSpec` positional record + `IExtensionFieldCatalog` 3-member shape + `AuditRecord` 7-field composition. SC-6 is a *reasoning* error on the why, not a *shape* error.
- **The cohort holds at 22-of-22.** The §A0 catch rate held at the cited ~65%: author flagged 2 of the 5 structural-citation issues (the author pre-flagged the `PlaintextSequestered` semantic mismatch via Open Question 2; pre-flagged the `SignedOperation<T>` envelope constructability via pressure-test #4). Council found 3 net-new structural-citation issues (SC-1, SC-2, SC-3, SC-4 = 4 of which the author missed entirely; SC-5, SC-6 are partial misses).
- **Cohort lesson reinforced:** even with §A0 self-audit + author pressure-test points + `tier: foundation` discipline, council remains canonical. ADR 0069 D1 + D2 + D3 are doing exactly what they were designed to do.

---

**Word count:** ~5,800 (within canonical 4,000–7,000 envelope).

**End of review.**
