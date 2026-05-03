# ADR 0054 — Council Review (Stage 1.5 Adversarial)

**Date:** 2026-04-29
**Reviewer:** research session, six-perspective adversarial council per UPF Stage 1.5
**ADR under review:** [`0054-electronic-signature-capture-and-document-binding.md`](../../../docs/adrs/0054-electronic-signature-capture-and-document-binding.md) (Status: Proposed)
**Companion intake:** [`property-signatures-intake-2026-04-28.md`](../../00_intake/output/property-signatures-intake-2026-04-28.md)

---

## 1. Verdict

**Accept with amendments.**

The architectural shape is right (kernel-tier substrate, content-hash binding, two-identity model, append-only revocation, audit-substrate emission, provider-neutral). Three load-bearing mechanics are under-specified to a degree that would survive Stage 06 build but fail the *forensic-dispute* test the ADR exists to win. All three are fixable inside the existing structure — none require a Reject-and-redraft.

The CTO should sign Acceptance conditional on the six amendments in §5 landing before `kernel-signatures` ships its first persisted `SignatureEvent`.

---

## 2. Anti-pattern findings (21-pattern sweep)

| AP # | Name | Severity | Where it fires |
|---|---|---|---|
| **#21** | Assumed facts without sources | **Critical** | "canonical bytes" of document content + canonical payload of `DeviceAttestation` are referenced 3× but never defined. SHA-256 binding is only legally defensible if both parties can recompute the same bytes — JSON Canonical Form (RFC 8785), CBOR (RFC 8949), Protobuf, deterministic UTF-8? The ADR is silent. Without a pinned canonicalization rule, the `ContentHash.Compute` contract is undefined behavior and the legal-binding claim is a hand-wave. |
| **#13** | Confidence without evidence | **Critical** | "Compliance is structural, not policy — substrate cannot accidentally produce a non-compliant signature." Stated as a finding, not proven. Required-field gating proves elements (a)/(c)/(d) are *present*; it does not prove *intent-to-sign* (the act, not the canvas). UETA §2(8)/E-SIGN §106(5) require the act be *attributable* to the signer — required fields don't establish attribution unless the consent flow + canvas affordance + capture audit jointly do, and that joint claim is asserted, not modeled. |
| **#3** | Vague success criteria | **Major** | "Document-binding integrity test: SignatureEvent verified valid against original document … invalid against modified content" — does not specify what test fixtures cover (whitespace? PDF metadata? lease-numbering reflow? Unicode normalization?). Without a fixture matrix, the test passes in trivial cases and silently fails the cases that produce actual disputes. |
| **#1** | Unvalidated assumptions | **Major** | "Append-only revocation; projections compute current validity." Concurrent revocations under the AP/CRDT model (ADR 0028) are not analyzed — two operators revoke from offline devices, sync at T+N hours, what's the resolved order? The ADR claims "safe under concurrent revocations" implicitly but provides no merge rule. |
| **#19** | Discovery amnesia | **Major** | The ADR 0046 amendment is described in one paragraph in `Compatibility plan` ("adds a `historical_keys[]` projection") but no companion ADR-amendment file exists, and ADR 0046 itself does not yet name signature-survival as a property. Per the ADR 0046 audit's existing #19 finding (kernel-security vs foundation-recovery package drift), layering another implicit amendment risks compounding the same gap. |
| **#11** | Zombie projects (no kill criteria) | **Minor** | Revisit Triggers exist (8 of them) but most are observation-driven without thresholds ("forensic dispute reveals an evidence gap" — what kind? severity? recurrence?). Acceptable for an architectural ADR; flagged because signature law is one place where measurable kill criteria would have outsized value. |
| **#17** | Delegation without context transfer | **Minor** | Stage 06 implementer is told "XML doc + nullability + `required` enforced" but is not told the canonicalization rule, the historical-keys projection shape, or the consent-flow attestability evidence chain. This is the same workload the ADR delegates downstream without the necessary context. |

**Anti-patterns avoided cleanly:** #2 (clean three-option Stage-0 sparring), #4 (rollback path is concrete — no production consumers), #5 (consequences extend past Decision), #6 (Resume Protocol implicit via checklist), #10 (first idea was challenged), #12 (no fantasy timelines), #15 (precision matches phase — schema sketch acknowledges "Stage 06 enforces").

---

## 3. Top 3 risks (legal-defensibility weighted)

1. **Canonicalization is undefined → content-hash binding is theatre.** `ContentHash.Compute(ReadOnlySpan<byte>)` only works if "the bytes" are deterministic. PDF generation re-runs produce byte-different PDFs (xref tables, embedded fonts, creation timestamps); HTML lease templates with different newline conventions hash differently; PencilKit-rendered raster images vary across iOS versions. A leaseholder's defense lawyer will ask "show me the bytes you hashed and prove they are recoverable from the document I have" — and without a pinned rule, the answer is "we hashed whatever was in the buffer." This single gap defeats the binding mechanic in any contested case. **Highest impact; must fix before substrate ships.**
2. **Operator-as-witness device-key rotation has no end-to-end verification path.** ADR 0054 says signatures survive operator rotation via an ADR 0046 amendment that hasn't been authored; ADR 0046 currently lacks any `historical_keys[]` surface; ADR 0049's audit substrate is locked to Ed25519 today and ADR 0049 itself flags this as needing pre-v1 algorithm-agility. The chain "verify a 2026 signature in 2030 after the operator rotated 3 times and Ed25519 was deprecated" has *three* unsealed joints. Any one breaking voids the evidentiary value of every signature captured before the fix.
3. **Algorithm-agility is referenced, not implemented.** `DeviceAttestation.Algorithm` is `string`; ADR 0004 already specifies a `SignatureEnvelope { SignatureAlgorithm Algorithm; bytes Payload }` shape that ADR 0049 explicitly flagged as a *prerequisite* for long-retention data. Shipping `kernel-signatures` with a string-typed algorithm field and Ed25519-hardcoded `SignatureBytes` repeats the exact ADR 0049 anti-pattern — and signatures are 10-year-retained legal artifacts, the longest-retention data class in the system.

---

## 4. Top 3 strengths

1. **Two-identity model is cryptographically sound and legally novel-but-defensible.** Operator-as-witness via device-key Ed25519 signature over event payload + signer-as-signatory via pen-stroke biometric is a stronger evidence package than either alone. UETA accepts wet-equivalent signatures and most courts accept "witness affidavit"; this substrate produces both as cryptographic artifacts. The decision to make `WitnessIdentity` a non-nullable contract field is correct.
2. **Kernel-tier placement is right.** Option A's verdict is well-reasoned; the cross-cutting consumer set (5 cluster modules) plus security-critical posture matches kernel-audit + kernel-security siblings. Option B (block-tier opt-in) would have created the foot-gun the rejection rationale names. The package home is also consistent with the ADR 0046 audit's outstanding "kernel-security vs foundation-recovery" friction — staying in kernel-tier defers that fight rather than worsening it.
3. **Append-only revocation preserves the legal trail.** Many signature systems silently delete revoked artifacts; this one captures the *fact-of-revocation* as its own event. That's the right answer for evidentiary preservation and matches ADR 0049's append-only substrate. Governance (who can revoke) is correctly deferred to OQ-S8 with a recommended resolution.

---

## 5. Specific amendments (Accept-with-amendments conditions)

1. **A1 (Critical):** Add a `### Canonicalization` subsection pinning the byte-serialization rule for both document content (lease template → bytes → SHA-256) and `DeviceAttestation` payload. Recommend RFC 8785 (JSON Canonical Form) for structured payloads + UTF-8-NFC + LF normalization for text documents + an explicit "PDF is convenience-only; do not hash PDF bytes" callout. Without this, A2 cannot be tested.
2. **A2 (Critical):** Replace `DeviceAttestation.Algorithm: string` with the ADR 0004 `SignatureEnvelope { SignatureAlgorithm Algorithm; byte[] Payload }` shape. Add an Implementation checklist item: "uses Sunfish.Foundation.Crypto.SignatureEnvelope per ADR 0004; no algorithm-string fields." Sequencing note: gate `kernel-signatures` ship on the ADR 0004 envelope refactor landing first (or on a binding pre-commit) — same rule ADR 0049 should have followed.
3. **A3 (Major):** Author the ADR 0046 amendment as a separate ADR (0046-A1) before `kernel-signatures` Stage 06 build — not as embedded amendment text in 0054. The amendment must specify: (a) `historical_keys[]` projection schema, (b) verification flow for a SignatureEvent against a rotated operator key, (c) how recovery state machine emits the rotation event into kernel-audit so the verifier can reconstruct historical key validity. Without this, risk #2 is unsealed.
4. **A4 (Major):** Add `### Concurrent revocation semantics` paragraph specifying merge rule under ADR 0028 AP semantics. Recommend: revocations are total (any revoke wins); revocation timestamp is the earlier of any concurrent revoke; multiple revoke events for the same SignatureEvent collapse to the earliest-by-Lamport-timestamp; revoking-actor list is the union.
5. **A5 (Major):** Add a fixture matrix to the document-binding integrity test deliverable: (a) whitespace-only edit, (b) Unicode NFC vs NFD reordering, (c) trailing-newline difference, (d) lease numbering reflow (renumber clause 3 → clause 3a), (e) embedded-image swap, (f) PDF re-generation from same source. Each must produce a verifiable hash mismatch.
6. **A6 (Minor):** Tighten Revisit Triggers with measurable thresholds where possible. Suggested: "forensic dispute" → "any forensic dispute where the substrate cannot produce the original canonical bytes within 1 business day of subpoena"; "PDF rendering drift" → "iOS PDFKit and Bridge QuestPDF visual byte-diff exceeds 0.5% on the parity-test corpus."

---

## 6. Quality rubric grade

**Grade: B (Solid).** Rationale:

- **C threshold (Viable):** All 5 CORE sections present (Context, Considered Options as Success-Criteria-equivalent, Consequences as Assumption/Validation surface, Implementation checklist as Phases, Verification implicit in checklist + revisit triggers). No critical anti-patterns of the planning kind (#9 skipping Stage 0, #11 zombie, #12 timeline fantasy) fire on the *plan* — the criticals fire on the *substrate's legal mechanics*, which is technically a content gate not a planning gate. Passes C.
- **B threshold (Solid):** Stage 0 sparring clearly executed (3 named options with explicit rejection rationale per option); Confidence Level declared (HIGH) with rationale; Cold Start Test claim is plausible (13-task implementation checklist); FAILED conditions present (8 revisit triggers, though #6 amendment would tighten them). Passes B.
- **A threshold (Excellent):** Misses on three counts: (1) the Stage 1.5 adversarial pass appears to have been skipped or short — a Pedantic Lawyer perspective would have caught the canonicalization gap immediately; (2) Reference Library is present but does not link to RFC 8785/8949/CBOR as canonicalization candidates, the most consequential coding-domain references this ADR needs; (3) Knowledge Capture / Replanning Triggers are observation-driven without measurement. Does not reach A.

A grade of **B** with the §5 amendments applied promotes the ADR to **A**. Without the amendments, the algorithm-agility + canonicalization + 0046-amendment gaps will surface during Stage 06 build or — worse — during a real forensic dispute, at which point the cost-to-fix is multiples of the cost-to-fix-now.

---

## Council perspective notes (compressed)

- **Outside Observer:** "Cross-cutting kernel substrate, two-identity model, append-only revocation — the shape is recognizable and matches industry signature systems." Pass.
- **Pessimistic Risk Assessor:** "Three unsealed joints in the verify-a-2026-signature-in-2030 chain. One forensic dispute exposes all three." Drives risks #1–#3.
- **Pedantic Lawyer:** "What are 'the bytes'? Show me the canonicalization rule. Show me the ADR 0046 amendment. Show me how a deprecated Ed25519 key still verifies in 2031." Drives A1, A2, A3.
- **Skeptical Implementer:** "Stage 06 implementer cannot ship without the canonicalization rule (cannot write `ContentHash.Compute`) and cannot ship without knowing whether to use `SignatureEnvelope` or string algorithm." Drives A1, A2.
- **The Manager:** "5 cluster modules block on this. Acceptance unblocks them. Three amendments are 1–2 days of research session work; the alternative is shipping a substrate we have to re-author in 18 months when ADR 0004 lands." Endorses Accept-with-amendments.
- **Devil's Advocate:** "Could this just be a thinner ADR — `Phase 2.1b ships in-person Ed25519 signatures with a known v1 rework path` — and accept the algorithm-string and undefined canonicalization as Phase-2-only debt? Counter: signatures captured today retain for 10 years. The Phase-2 substrate IS the v1 substrate from the leaseholder's perspective." Devil's argument loses on retention math.
