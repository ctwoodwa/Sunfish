---
type: idle
workstream-or-chapter: W#44 — ExtensionFields Feature-Evaluation Hook (ADR 0075 Accepted + Stage 06 hand-off)
last-pr: "#567 (ADR 0075 council amendments + Status: Proposed → Accepted; ledger W#44 → ready-to-build)"
resolved-by: "PR #567 merged; W#44 is ready-to-build"
resolved-date: 2026-05-05
---

ADR 0075 council amendments applied (PR #567): SC-3 ADR 0046 reference corrected +
FeatureGateOff code sample updated (ADR 0028-A11 prerequisite already on origin/main
via PR #512). Brief Skeptical Implementer re-review: APPROVED. Status flipped Accepted.

Stage 06 hand-off authored at `icm/_state/handoffs/extension-fields-feature-gate-stage06-handoff.md`
(4 phases, ~11-15h, `sunfish-api-change` pipeline). W#44 ledger flipped `ready-to-build`.

What would unblock me: CO accepts PR #567 (ADR 0075 → W#44 unblocked for COB). W#43 build
(COB capacity) is NOT a prerequisite — `foundation-catalog` has no compile-time dep on
`foundation-wayfinder`. PRs #537/#529/#539 (ADRs 0065-A1/0066/0067) already merged.
