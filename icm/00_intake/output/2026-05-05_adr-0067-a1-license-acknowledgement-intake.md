# Intake — ADR 0067-A1: License-Acknowledgement Track

**Date:** 2026-05-05
**Requestor:** XO research session (council fix-pass on ADR 0067 PR #539)
**Request:** Follow-up amendment to ADR 0067 designing the SSPL/BSL admin-acknowledgement opt-in track. Cut from ADR 0067 v1 per the council fix-pass because ADR 0061's actual posture excludes such adapters at compile time via `BannedSymbols.txt` analyzer enforcement; an admin-opt-in path requires its own ADR amendment (touching ADR 0061 and/or general counsel sign-off).
**Pipeline variant:** `sunfish-feature-change` (amendment to ADR 0067)
**Stage:** 00 — closed; will not proceed
**Status:** `won't-do` — deferred indefinitely per CO directive 2026-05-06

---

## Problem Statement

ADR 0067 v1 (PR #539) ships the Atlas Integration-Config UI surface without a license-acknowledgement track. Per the council fix-pass on PR #539 (canonical council file: `icm/07_review/output/adr-audits/0067-council-review-2026-05-04.md`), an earlier draft of ADR 0067 invented a `LicensePostureKind` enum + `LicenseAcknowledgementRequiredException` + `IssueLicenseAcknowledgementAsync` issuance method + `IntegrationLicenseAcknowledged` audit event + `license-acknowledged.{provider}` Standing-Order path + admin-acknowledgement modal flow that DIRECTLY CONTRADICTED ADR 0061's actual posture: ADR 0061 EXCLUDES SSPL/BSL adapters at compile time via `BannedSymbols.txt` analyzer enforcement, with NO admin-acknowledgement opt-in path.

The council disposition was: cut the entire track from v1; defer to a follow-up amendment that addresses the ADR 0061 contradiction explicitly. This intake stub captures that follow-up.

---

## Predecessors

- **ADR 0061** (Three-Tier Peer Transport) — owns the `BannedSymbols.txt` analyzer enforcement; ADR 0067-A1 must address whether ADR 0061 needs an amendment to admit an opt-in path, OR whether the opt-in lives entirely above ADR 0061 in a separate compliance layer.
- **ADR 0067** (Atlas Integration-Config UI Surface) v1 — the surface ADR 0067-A1 amends.
- **W#37 Tenant Security Policy** — captures the revoked-principal acknowledgement question (§9.5 of ADR 0067 deferred).
- **General counsel engagement** — ADR 0067-A1 cannot proceed to Acceptance without legal review of the modal copy (`SSPL/BSL` license-text obligations summarization is itself a legal-content artifact).

---

## Why net-new (not just an ADR 0067 amendment)

ADR 0067-A1 is technically an amendment to ADR 0067 (frontmatter `amendments:` list). It is filed as a separate intake because:

1. The contradiction with ADR 0061 is non-trivial — needs a council-tier decision on whether to amend ADR 0061's `BannedSymbols.txt` enforcement or carve a separate compliance layer above it.
2. The general-counsel engagement loop is asynchronous — modal copy review can take weeks; ADR 0067 v1 cannot block on that.
3. The Pedantic-Lawyer council perspective is mandatory for ADR 0067-A1 (license-text obligations summarization). ADR 0067 v1 does not need this perspective for its remaining v1 surface.

---

## Scope

**In:**

- Reintroduce `LicensePostureKind` enum (`Permissive` / `WeakCopyleft` / `StrongCopyleft`) or a successor shape.
- Reintroduce or replace `LicenseAcknowledgementRequiredException` (positional ctor per the §1.5 council disposition).
- Define `IssueLicenseAcknowledgementAsync` shape (or a successor pattern).
- Define `IntegrationLicenseAcknowledged` audit event + payload.
- Define `license-acknowledged.{provider}` Standing-Order path (or alternative storage) + issuance ordering invariant relative to `active-provider`.
- Define §5.5 modal flow + WCAG SC 3.3.4 (explicit-action) + SC 2.1.2 (no keyboard trap) + SC 2.4.3 (focus order) compliance.
- Define schema-version + license-posture migration ladder (the §4.2.1 disposition cut from ADR 0067 v1).
- Define revoked-principal acknowledgement behavior (W#37 composition; the §9.5 question cut from ADR 0067 v1).
- Resolve whether ADR 0061's `BannedSymbols.txt` enforcement gets an exception path (general counsel posture).
- Specify modal copy acceptance criteria (legal review, locale support, accessibility).

**Out:**

- ADR 0067 v1 surface — already shipping.
- OAuth-flow provider support — separate ADR per ADR 0067 §9.6.

---

## Industry prior-art

- Microsoft EULA acceptance pattern (Windows Out-of-Box Experience).
- AWS Marketplace SSPL/AGPL acknowledgement flow (when subscribing to MongoDB/Elastic AMIs).
- HashiCorp BSL acknowledgement (Terraform Cloud user agreement).
- GitHub Enterprise license-acceptance modal.

---

## Dependencies and Constraints

- **Hard prerequisite:** ADR 0067 (v1) shipping at `Status: Accepted`. This amendment cannot be authored against a still-`Proposed` predecessor.
- **Hard prerequisite:** general-counsel engagement on the `BannedSymbols.txt` exception posture. Without legal sign-off the amendment cannot leave `Proposed`.
- **Composes on:** ADR 0061 (license posture canonical), ADR 0049 (audit retention), ADR 0046-A2 (encrypted-field substrate).
- **Cohort discipline:** pre-merge council canonical (per ADR 0069 D1); Pedantic-Lawyer perspective mandatory.
- **Effort estimate:** medium (~10–14h authoring + extended council review including legal loop; legal loop may take 2–4 weeks calendar-time).

---

## Success Criteria

- ADR 0067-A1 reaches `Status: Accepted` with general-counsel sign-off captured in the council appendix.
- ADR 0061's `BannedSymbols.txt` enforcement either (a) gets an explicit exception path for acknowledged adapters, OR (b) remains canonical and ADR 0067-A1's track lives above it as a separate compliance layer with documented bypass mechanism.
- Modal copy passes WCAG SC 3.3.4 / 2.1.2 / 2.4.3 audit + legal-content review.
- ADR 0067 v1's open questions §9.5 + §9.7 + §4.2 license-posture-migration are absorbed into this amendment.
- Reference implementation lands in `packages/ui-core/Wayfinder/Integrations/` per ADR 0066 / ADR 0067 v1 package shape.

---

## Open Questions for Stage 02 Architecture

1. Does ADR 0061's `BannedSymbols.txt` enforcement get amended, or does the acknowledgement track live above it?
2. Is the acknowledgement a tenant-level legal commitment (the §1.5 disposition from ADR 0067's earlier draft) or per-actor?
3. Does revocation of the acknowledging principal's role invalidate the acknowledgement (composes with W#37)?
4. Is the modal copy versioned per locale / per license-version / per Sunfish-version?
5. What is the audit-record retention obligation for license acknowledgements? (Composes with ADR 0049.)

---

## Filing Notes

- Filed by XO during the ADR 0067 PR #539 council fix-pass.
- Cohort discipline: 28-of-29 substrate amendments needed council fixes. ADR 0067 makes it 29-of-30; ADR 0067-A1 will be 30-of-31.
- Pre-merge council canonical from intake.

---

## Disposition

**Decision:** Won't Do — deferred indefinitely.

**Date:** 2026-05-06

**Authority:** CO directive (Christopher Wood, BDFL) — XO session 2026-05-06.

**Rationale:** OSS substitutability principle. Tailscale (BSL-1.1), the canonical example prompting this intake, is fully substitutable by Headscale (BSD-3),
Netbird (BSD-3), OpenVPN, or any WireGuard-based mesh that ships under a permissive license. Sunfish's design contract — ADR 0013 provider neutrality + ADR 0014
adapter parity — means any provider-gated feature can be satisfied by a permissive-licensed substitute. No proprietary or restrictive-license provider justifies
new acknowledgement substrate. CO verbatim (2026-05-06): *"Tailscale is not essential only the features it provides. Any similar provider or alternative is
allowed. This same applies to all features, any can be substituted replaced or extended. This is the point of sunfish and why oss was choosen."*

**Effect on cohort:**

- ADR 0061's `BannedSymbols.txt` enforcement remains absolute — no exception path added, no `LicensePostureKind` enum, no
  `LicenseAcknowledgementRequiredException`, no `IssueLicenseAcknowledgementAsync` method, no `IntegrationLicenseAcknowledged` audit event, no
  `license-acknowledged.{provider}` Standing-Order path.
- No admin-acknowledgement modal flow will be added to the Atlas Integration-Config surface (ADR 0067).
- General-counsel engagement loop is unnecessary — the answer is substitution, not accommodation.
- The filing-notes cohort counter (`30-of-31`) is voided: this amendment will not ship, so the cohort counter does not advance.

**Principle codified:** OSS substitutability is the substrate (see feedback memory `feedback_oss_substitutability_principle.md`). When a capability is gated on
a restrictive-license or otherwise-complex provider, the answer is to use a permissive substitute — not to add infrastructure that accommodates the restricted
provider.

---

*End of intake stub.*
