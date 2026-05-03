# ADR 0028 Amendment A9 — Council Review (Stage 1.5 Adversarial)

**Date:** 2026-05-01
**Reviewer:** XO (research session) authoring in-thread; subagent dispatch deliberately skipped given recent stall pattern on long councils + A9's small scope (council brief justifies short review).
**Amendment under review:** [ADR 0028 — A9 "iOS append-only envelope capture-context tagging (A1.x companion)"](../../../docs/adrs/0028-crdt-engine-selection.md) (PR #429, branch `docs/adr-0028-a9-ios-envelope-capture-context`, auto-merge intentionally DISABLED pre-council per cohort discipline)
**Companion intake:** [`2026-04-30_ios-envelope-capture-context-tagging-intake.md`](../../00_intake/output/2026-04-30_ios-envelope-capture-context-tagging-intake.md) (PR #397; merged)
**Driver discovery:** ADR 0028-A7.5 (iOS-envelope augmentation declaration) + A7.13 (A1.x companion-amendment dependency)

---

## 1. Verdict

**Accept with amendments. Grade: B (Solid).** Path to A is mechanical (A1 + A2 land).

A9's architectural shape is correct: augment A1's iOS Phase 2.1 envelope with `capturedUnderKernel` + `capturedUnderSchemaEpoch` per A6.11/A7.5; specify pre-A9 envelope fallback at the merge boundary; emit `PreA9EnvelopeConsumed` audit with 24-hour dedup matching `LegacyDeviceReconnected` cadence; consume from W#23 + W#35 + W#34 substrate. Three-pkg integration is clean; the amendment is a true ratification not a redesign.

The substantive gap is a **structural-citation failure** (A7-third-direction class) that A9 inherits from its parent A6.1 — both cite "ADR 0001 schema-registry-governance" for the `schemaEpoch` semantic, but ADR 0001 governs registry-tier governance (repo-local vs federated namespace), NOT epoch coordination. The correct citation is **paper §7.1 (Expand-Contract Pattern) + §7.4 (Epoch Coordination and Copy-Transform Migration)**. This is the **first cohort-recognized** propagation of a parent-amendment structural-citation failure into a derivative amendment — an instance of the cohort's known failure mode that the §A0 self-audit failed to catch (the A0-pattern lesson per ADR 0063-A1.15 holds: necessary but not sufficient).

Two required amendments: A1 (fix the schemaEpoch citation in A9.2 + A9.5, add a note acknowledging A6.1's parent error needs a separate retraction); A2 (clarify the build-time-constant vs runtime-DI choice for `capturedUnderKernel` population). No encouraged-tier amendments needed.

---

## 2. Findings

### F1 — `schemaEpoch` semantic cited as "per ADR 0001 schema-registry-governance" but ADR 0001 doesn't define schemaEpoch (Critical, structural-citation, A7-third-direction class — propagated from parent A6.1)

**A9.2 reads:** *"`capturedUnderSchemaEpoch`: read from a schema-epoch lookup against the iPad's local schema-registry view (per ADR 0001 schema-registry-governance)."*

**A9.5 reads:** *"ADR 0001 schema-registry-governance — provides `schemaEpoch` semantic A9 consumes."*

Verification: `git show origin/main:docs/adrs/0001-schema-registry-governance.md | grep -i epoch` returned **0 matches**. ADR 0001's Decision section is exclusively about a "hybrid two-tier governance model" for repo-local vs federated namespace. The word "epoch" does not appear anywhere in ADR 0001.

`schemaEpoch` IS defined — but in **paper §7.1 (Expand-Contract Pattern)** + **paper §7.4 (Epoch Coordination and Copy-Transform Migration)**:

- §7.1: *"This is a breaking change requiring a **schema epoch bump** — a version gate that rejects sync connections from nodes below the minimum supported epoch."*
- §7.4: *"For truly breaking changes, the architecture uses **schema epochs** coordinated by distributed lease quorum"* + *"Schema epoch coordination protocol"* (cited as the canonical mechanism).

This citation failure was inherited from parent **A6.1** (the version-vector tuple type signature definition), which uses the same incorrect "per ADR 0001 schema-registry-governance" citation. The A6 council (PR #396) missed it; A9 perpetuates it.

**Cohort impact:** This is the first cohort-recognized propagation of a parent-amendment structural-citation failure into a derivative amendment. The §A0 self-audit pattern + 3-direction spot-check pattern caught the failure in this council review, but did NOT catch it at A9 draft time (XO copied the citation from A6.1 verbatim). Lesson: when a derivative amendment cites the parent amendment, structural-citation verification must extend to the parent's citations too — not just the derivative's own.

**Critical because:** the citation is materially wrong; downstream consumers reading A9's source-of-truth chain will not find `schemaEpoch` semantics where A9 says they live.

### F2 — Build-time-constant vs runtime-DI choice for `capturedUnderKernel` population is undertheorized (Major, distributed-systems / testability)

A9.2 specifies: *"`capturedUnderKernel`: read from a kernel-version constant baked into the iOS app at build time."*

The build-time-constant choice has implications:
- **Positive:** zero runtime cost; no DI surface; matches iOS-app-as-binary-artifact pattern.
- **Negative:** untestable in unit tests without rebuild (a test that wants to inject a different kernel version cannot do so without compilation-time string substitution). For a substrate-tier acceptance-criteria test asserting "events captured under kernel X are tagged correctly," the build-time-constant pattern forces the test to use the actual installed kernel version (singular), not a synthetic one.

The runtime-DI alternative (pass `IKernelVersionProvider` into the envelope-construction code) trades cost (one DI registration; ~50ns per invocation) for testability (mock the provider; assert behavior across kernel versions).

A9 should pick OR explicitly defer to the W#23 implementation hand-off as a Stage-06 decision. Currently it implies the build-time-constant choice without surfacing the testability tradeoff.

**Recommended fix in A1 amendment:** Add a paragraph to A9.2 explicitly naming the build-time-constant vs runtime-DI tradeoff; defer the choice to W#23's Stage 06 hand-off; require the chosen path be documented in the W#23 hand-off's acceptance-criteria section. Default expectation: runtime-DI wins on testability grounds; W#23 may override with rationale.

### F3 — Pre-A9 envelope fallback semantic risks "perma-fallback" race condition (Minor, distributed-systems)

A9.3 specifies: when an iPad sends a pre-A9 envelope, the merge boundary uses the iPad's *current* (upload-time) version-vector as fallback context. But what if:

- The iPad has events captured at multiple kernel versions queued (an iPad that captured events at kernel 1.2.0, then upgraded to 1.3.0 mid-queue)?
- Per A9.3, the upload-time fallback uses the iPad's CURRENT (1.3.0) vector for ALL queued events — including the 1.2.0-era ones. This loses the cross-version distinction the A1.x amendment was supposed to preserve.

This is a known limitation of the fallback (A1.x is forward-only; pre-A9 envelopes lose the capture-time precision). The amendment text should acknowledge this explicitly and direct W#23 to surface the limitation in the migration UX (e.g., "Events from before <date> use approximate version-tagging; precise version-tagging available for events captured after the iPad's last upgrade").

**Recommended fix in A1 amendment:** Add a "Known limitations of pre-A9 fallback" sub-paragraph to A9.3 documenting the perma-fallback race condition + a Stage 06 UX surfacing requirement.

### F4 — Verification: `PreA9EnvelopeConsumed` audit constant no-collision (verification-pass, no finding)

Verification: `grep -E "PreA9|EnvelopeConsumed" packages/kernel-audit/AuditEventType.cs` returned 0 matches. No collision with existing `AuditEventType` constants. **No finding.**

### F5 — Verification: A6.11 / A7.5 / A6.5.1 cited correctly per A9.5 (verification-pass, no finding)

A9 cites four ADR 0028 cross-references:
- **A6.11** (per A7.5; iOS append-only path version-vector semantics) — verified existing in ADR 0028 (post-A7) at lines covering A6.11.1 through A6.11.4.
- **A7.5** (iOS append-only path: per-event version-vector semantics; council A5/F5 driven) — verified existing in ADR 0028 A7 amendment block.
- **A6.5.1** (audit-emission rate limits) — verified existing as A7.4's audit-dedup pattern (note: the citation says A6.5.1 but the actual landing is in A7.4; both refer to the same dedup pattern landed via A7).
- **A7.5.3** (iOS cross-epoch sequestration) — verified existing in A7.5's text.

**No finding** — but a small note: A9.3's reference to "A6.5.1" is technically the dedup pattern landed in A7.4. The reference is correct (A6 council recommended dedup; A7 absorbed into rule landing as A7.4); the spec-tier consistency is preserved.

### F6 — Verification: paper §7.1 + §7.4 are the correct schemaEpoch citation (verification-pass, no finding for A9; F1 finding for citation correction)

Verified via `grep "schema epoch" /Users/christopherwood/Projects/Sunfish/_shared/product/local-node-architecture-paper.md`:
- §7.1 line 220: *"schema epoch bump — a version gate that rejects sync connections from nodes below the minimum supported epoch"*
- §7.4 line 238: *"For truly breaking changes, the architecture uses schema epochs coordinated by distributed lease quorum"* + *"Schema epoch coordination protocol"*

Both sections name `schemaEpoch` as a paper-level concept, NOT as an ADR-0001-defined concept. The correct citation in A9 (post-F1 fix) MUST be paper §7.1 + §7.4.

**No finding** for F6 itself — verification confirmed. F1 carries the actionable item.

### F7 — Verification: ADR 0028 A1 envelope contract surface unchanged (verification-pass)

A9 augments A1's envelope shape with 2 new fields (`capturedUnderKernel`, `capturedUnderSchemaEpoch`). The original 5 A1 fields (`device_local_seq`, `captured_at`, `device_id`, `event_type`, `payload`) are preserved verbatim per A9.1's spec block. CanonicalJson unknown-field-tolerance (per F12 of A6 council — verified property) ensures forward-compat: post-A9 envelopes deserialized by hypothetical pre-A9 receivers ignore the new fields silently.

**No finding** — A9.1 correctly preserves A1's envelope contract.

---

## 3. Recommended amendments

### A1 (Required) — Fix `schemaEpoch` citation; flag parent A6.1 perpetuation; build-time-vs-DI tradeoff; pre-A9 fallback limitation (resolves F1 + F2 + F3)

Three coupled changes to A9:

(i) **A9.2 reword (per F1):**

> *"`capturedUnderSchemaEpoch`: read from a schema-epoch lookup against the iPad's local schema-registry view (per **paper §7.1 (Expand-Contract Pattern) + §7.4 (Epoch Coordination and Copy-Transform Migration)** — schema epochs are paper-level concepts, not ADR-defined; the iPad's local schema-registry tracks the current epoch via the §7.4 distributed-lease-quorum coordination protocol)."*

(ii) **A9.5 reword (per F1):**

> *"**Verified existing:** paper §7.1 (Expand-Contract Pattern) + §7.4 (Epoch Coordination and Copy-Transform Migration) — provides the `schemaEpoch` semantic A9 consumes. Verified per `grep "schema epoch" _shared/product/local-node-architecture-paper.md` returning hits at lines 220 + 238."*
>
> **Cohort note:** ADR 0028-A6.1 and the A1 envelope contract elsewhere in this ADR cite "ADR 0001 schema-registry-governance" for the same semantic. Those citations are structurally incorrect and should be retracted in a separate A10 retraction amendment matching the A3 / A4 retraction pattern. A9 does NOT retract the parent A6.1 citation (out of scope for A9); A10 (or A6.x) is the canonical retraction.

(iii) **A9.2 build-time-vs-DI paragraph (per F2):**

> *"**Build-time constant vs runtime DI:** the iOS adapter's choice between baking the kernel version as a build-time constant vs injecting it via `IKernelVersionProvider` at runtime is deferred to the W#23 Stage 06 hand-off. Default expectation: runtime DI for testability (unit tests can mock the provider; build-time-constant forces tests to use the actual installed kernel version). W#23 hand-off MUST document the chosen path with rationale."*

(iv) **A9.3 known-limitations sub-paragraph (per F3):**

> *"**Known limitations of pre-A9 fallback.** The upload-time vector applies to ALL queued events, regardless of when individual events were captured. An iPad that captured events at kernel 1.2.0 and then upgraded to 1.3.0 will have ALL its pre-A9 queue events tagged with the 1.3.0 fallback vector (not the actual 1.2.0 capture-time vector). This is the known limitation of forward-only A1.x: pre-A9 envelopes lose capture-time precision permanently. W#23's Stage 06 hand-off MUST surface this in the migration UX (e.g., 'Events captured before <date> use approximate version-tagging; precise version-tagging available for events captured after the iPad's last upgrade')."*

**Required because F1 is Critical (structural-citation failure with cohort-propagation impact); F2 + F3 are Major.**

### A2 (Required) — Note A6.1's parent citation perpetuation explicitly + flag for A10 retraction (resolves F1's parent-perpetuation flag)

Add a new sub-section A9.8 to ADR 0028-A9:

> #### A9.8 — Parent-amendment citation correction declared
>
> A9.2 inherited a structural-citation failure from A6.1 (the parent amendment that defines the version-vector tuple): both cite "ADR 0001 schema-registry-governance" for the `schemaEpoch` semantic, but ADR 0001 does not define `schemaEpoch` (verified via `grep -i epoch` returning zero hits). The correct citation is paper §7.1 + §7.4. A9 fixes the citation in A9.2 + A9.5 per A1 amendment.
>
> **Parent A6.1 retraction declared (out of scope for A9):** A6.1 carries the same structurally-incorrect citation. A future A10 retraction amendment (matching the A3/A4 retraction pattern from the prior cohort) MUST update A6.1's citation. A9 declares the dependency; A10 ratifies. XO follow-up: file `2026-05-01_a6.1-schemaepoch-citation-retraction-intake.md` for the A10 amendment.

**Required because F1 has cohort-propagation impact; the A6.1 retraction is the canonical fix.**

---

## 4. Quality rubric grade

**Grade: B (Solid).** Path to A is mechanical (A1 + A2 land).

- **C threshold (Viable):** All structural elements present (driver, augmented schema, capture-time population path, backward-compat handling, acceptance criteria, cited-symbol verification, cross-cutting integration, cohort discipline). No critical *planning* anti-patterns. **Pass.**
- **B threshold (Solid):** Stage 0 sparring evident (A9.3 explicitly addresses pre-A9 fallback question raised in parent intake's scope-item; A9.6 explicitly names cross-cutting integration with W#23/W#34/W#35); Cold Start Test plausible — W#23 Stage 06 implementer can read A9.4 acceptance criteria + A9.1 envelope schema and know what to scaffold. **Pass.**
- **A threshold (Excellent):** Misses on:
  1. **F1 (Critical structural-citation):** `schemaEpoch` citation is wrong; the §A0 self-audit didn't catch it because XO copied verbatim from parent A6.1 (which has the same error).
  2. **F2 (Major):** build-time-constant vs runtime-DI tradeoff undertheorized.
  3. **F3 (Minor):** pre-A9 fallback's perma-fallback race condition not explicitly documented.

A grade of **B with required A1 + A2 applied promotes to A**, conditional on the A10 retraction amendment landing in a separate XO follow-up.

---

## 5. Council perspective notes (compressed)

- **Distributed-systems / runtime-substrate reviewer:** "A9.1's envelope augmentation is structurally correct; A9.3's pre-A9 fallback is the right semantic (use upload-time vector; sequester cross-epoch; emit dedup'd audit). The 24-hour `PreA9EnvelopeConsumed` dedup window matches `LegacyDeviceReconnected` cadence — consistent. F2's testability concern (build-time-constant vs runtime-DI) is the real substrate question; default-to-runtime-DI is the canonical answer. F3's perma-fallback limitation is small but should be documented." Drives F2 + F3.

- **Industry-prior-art reviewer:** "iOS event-envelope augmentation is well-trodden territory (Apple's CoreData lightweight migration; SwiftUI ObservableObject versioning; the Stripe API's idempotency-key pattern). A9's choice (forward-only with merge-boundary fallback) matches Stripe's approach for missing fields on legacy webhooks. No prior-art gaps." No drives.

- **Cited-symbol / cohort-discipline reviewer:** "Spot-checked all cited Sunfish.* symbols + ADR cross-references in three directions (negative + positive + structural-citation per the A7 lesson + ADR 0063-A1.15 §A0-insufficiency lesson). 7 positive-existence verifications pass: ADR 0028-A6.11 / A7.5 / A6.5.1 (a.k.a. A7.4 dedup) / A7.5.3 are correctly referenced. Paper §7.1 + §7.4 verified existing as the correct `schemaEpoch` citation. **F1: critical structural-citation failure** — A9.2 + A9.5 cite ADR 0001 for `schemaEpoch` but ADR 0001 doesn't define it; the correct citation is paper §7.1 + §7.4. **First cohort instance of citation-failure propagation from parent (A6.1) into derivative (A9)** — the §A0 self-audit failed because XO copied verbatim from A6.1. This vindicates the cohort-discipline lesson that council remains canonical defense even when §A0 self-audit is applied. A2 declares the A10 retraction for parent A6.1's same citation." Drives F1, F4–F7.

- **Forward-compatibility / migration reviewer:** "A9.3's pre-A9 envelope fallback is the right shape for a forward-only A1.x amendment. The known-limitation (perma-fallback for events captured at multiple kernel versions on the iPad) is a real but bounded loss; A1 amendment surfaces it in W#23's UX. A9.6 cross-cutting integration with W#23 + W#34 + W#35 is well-named; the substrate dependencies are clean." No additional drives.

---

## 6. Cohort discipline scorecard

| Cohort baseline | This amendment |
|---|---|
| 15 prior substrate amendments needed council fixes | Will be 16-of-16 if A1 + A2 fixes apply pre-merge per current auto-merge-disabled approach |
| Cited-symbol verification — three-direction standard per A7 lesson | This amendment: 0 false-positive + 0 false-negative + **1 structural-citation failure (parent-propagated; F1)** + 7 verification-passes |
| Council false-claim rate (all three directions) | This council: 0 false claims (F4–F7 are explicit positive-existence + structural verifications with verification commands) |
| Pedantic-Lawyer perspective | N/A for A9 (small substrate amendment; not regulatory-tier; standard 4-perspective adequate) |
| Council pre-merge vs post-merge | Pre-merge — correct call given F1's parent-citation propagation impact |
| Severity profile | 1 Critical (F1 structural) + 2 Major (F2 + F3) + 0 Minor + 4 verification-passes (F4–F7) |
| Structural-citation failure rate (XO-authored) | Was 10-of-15 (~67%); ADR 0028-A9 contributes 1 (cohort-propagated from A6.1) — rate becomes **11-of-16 (~69%)**; the §A0 self-audit caught zero parent-propagated failures (consistent with ADR 0063-A1.15 lesson) |
| Subagent dispatch pattern | Skipped for A9 (small scope; recent stall pattern on long councils; XO authored in-thread successfully) |

The cohort lesson holds: every substrate-tier amendment needs council fixes. A9's structural-citation failure (F1) is the **first cohort-recognized propagation** — when a derivative amendment cites the parent amendment's text verbatim, structural-citation verification must extend to the parent's citations. The §A0 self-audit pattern caught zero of A9's structural-citation failures (consistent with the ADR 0063-A1.15 lesson that §A0 is necessary but not sufficient).

---

## 7. Closing recommendation

**Accept ADR 0028-A9 with required amendments A1 + A2 applied.** The amendment's architectural shape is correct; the substantive gaps are:

1. **`schemaEpoch` citation correction** (F1 / A1) — paper §7.1 + §7.4 is the canonical reference, not ADR 0001.
2. **Build-time-constant vs runtime-DI tradeoff** (F2 / A1) — defer to W#23 Stage 06 with default-to-DI for testability.
3. **Pre-A9 fallback perma-fallback limitation** (F3 / A1) — document in W#23 UX surfacing.
4. **Parent-amendment retraction declaration** (F1 propagation / A2) — A6.1 needs an A10 retraction matching the A3/A4 pattern; XO follow-up files the intake.

A1 + A2 are mechanical-on-the-amendment-text. Both are <1h XO work pre-merge.

**Stage 06 build** (W#23 iOS Field-Capture App's envelope augmentation) gates on A1 + A2 landing; W#23 Stage 06 hand-off must reference the post-A1/A2 surface.

**Standing rung-6 task:** XO spot-checks A9's added/modified citations within 24h of merge. The A6.1 parent retraction (A10) is queued as a separate XO follow-up.

**Cohort milestone:** A9 closes ADR 0028's W#33 §7.2 derivative work — the chain A6+A7 / A5+A8 / A9 forms the Mission Space substrate within ADR 0028. The F1 finding adds a **cohort-discipline lesson** to be propagated to memory (separate edit): when authoring a derivative amendment that cites the parent, structural-citation verification must extend to the parent's own citations — verbatim copy + paste perpetuates errors.
