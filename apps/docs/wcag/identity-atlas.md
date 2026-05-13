# Identity Atlas — WCAG 2.2 AA Conformance

Block family: Identity Atlas pages · ADR 0066 §Phase 3–4 · W#58

Documents WCAG 2.2 AA conformance for the five Identity Atlas pages across
the Anchor Blazor accelerator, Bridge Blazor accelerator, and Bridge React
adapter. Per ADR 0066 §3, SC 3.3.7 / 3.3.8 / 3.3.9 apply specifically to
recovery-contact enrollment and key-rotation flows.

> **Not a legal conformance claim.** Sunfish does not make a formal WCAG 2.2
> AA conformance claim for these pages in Phase 4. Conformance is established
> when a full assisted-technology audit completes + accessibility counsel is
> engaged (per ADR 0064 pattern). This document is the engineering baseline.

---

## Pages in scope

| Page | Route | Mutation surface |
|---|---|---|
| Identity Profile | `/identity/profile` | Display name, contact email, phone number |
| Key Management | `/identity/keys` | Key rotation window |
| Recovery Contacts | `/identity/recovery` | Enroll / remove recovery contacts |
| Historical Keys | `/identity/keys/history` | Read-only browsing (no mutation) |
| Active Team Overview | `/identity/teams` | Read-only browsing (no mutation) |

---

## Addressed success criteria

All five pages are read-only display surfaces. Mutations flow through the Helm
widget via Standing Orders; pages receive a `PendingDiff` cascade only when a
Standing Order is awaiting confirmation.

| SC | Level | How it is met |
|---|---|---|
| 1.1.1 Non-text Content | A | No decorative images on identity pages; all meaningful content is text. |
| 1.3.1 Info and Relationships | A | Definition lists (`<dl>`) for profile/key fields; WCAG-conformant table with `<caption>` + `<th scope>` for diff-preview section (ADR 0077 §4). |
| 1.3.2 Meaningful Sequence | A | Field pairs render in logical `<dt>/<dd>` sequence; diff table: Field → Current → New value. |
| 1.3.5 Identify Input Purpose | AA | Identity pages are display-only; no input fields in Phase 4 (mutations via Helm). |
| 2.1.1 Keyboard | A | All interactive elements (links, keyboard-focusable rows) reachable and operable by keyboard. |
| 2.4.3 Focus Order | A | Heading → field list → diff-preview section (when present) → nav footer. |
| 2.4.6 Headings and Labels | AA | H1 per page; H2 per section; diff-preview section heading includes `PendingDiff.Summary` text. |
| 3.3.7 Redundant Entry | AA | Recovery-contact enrollment does not re-ask data already supplied in a prior session step (contract enforced by Standing Order flow, not by the display page). |
| 3.3.8 Accessible Authentication (Minimum) | AA | Recovery-contact verification uses device-push confirmation, not cognitive-recall challenges. |
| 3.3.9 Accessible Authentication (Enhanced) | AAA | Same as 3.3.8 — no CAPTCHAs, no transcription tasks. |
| 4.1.2 Name, Role, Value | A | All structural elements use semantic HTML; `aria-label` on contact status badge names the verification state. |
| 4.1.3 Status Messages | AA | Loading states: `role="status"` with `aria-live="polite"`. Error states: `role="alert"` pre-mounted (M4 pattern — AT observes before content injected per ARIA22). |

---

## Diff-preview confirmation surface (ADR 0077 §4, Phase 4)

When a pending Standing Order is cascaded to a mutation page (profile, keys,
recovery contacts), a `DiffPreviewView.Expanded` confirmation table renders:

- **SC 1.3.1**: `<table aria-describedby>` with `<caption class="sf-sr-only">` (includes entry count) + `<th scope="col">` column headers ("Field", "Previous", "New value") + `<th scope="row">` field-name headers. Text alternative for all visual diff rendering.
- **SC 1.4.1 Use of Color**: Old value: `text-decoration: line-through` + dark muted color `#4b5563` (≥7.4:1 contrast on `#fffbeb`). New value: dark green `#065f46` (≥9.6:1 on white) AND `→ ` prefix text with `aria-hidden="true"` on the arrow (text is conveyed structurally by table column position, not color alone). Column header "Previous" + "New value" provide semantic context via `<th scope="col">`.
- **SC 2.4.6**: Diff-preview section heading includes the human-readable `summary` string (e.g., "Pending change — 2 changes"); fallback "pending fields below" when `Summary` is empty/whitespace.
- **SC 4.1.2**: Per-instance heading IDs generated via `Guid.NewGuid():N` (Blazor) / `useId()` (React) — no DOM ID collisions when multiple instances render simultaneously (e.g., Helm modal stacked over page).
- **SC 4.1.3**: Diff-preview section appears when `PendingDiff.Entries.Count > 0`; no live-region announcement required since the cascade is synchronous pre-render.

---

## Per-adapter notes

### Anchor Blazor
- `[CascadingParameter] public IDiffPreview? PendingDiff { get; set; }` on each mutation page.
- `IActiveTeamAccessor` + `AnchorSessionService` guard unauthenticated/un-onboarded renders.
- Recovery contacts: `B3 fix` applied — no per-row `aria-live`; `aria-label` on status badge.

### Bridge Blazor
- `[CascadingParameter] public IDiffPreview? PendingDiff { get; set; }` on each mutation page.
- `IBridgeRequestContext.IsResolved` guards unauthenticated renders.
- Recovery contacts: `aria-live` removed from list (read-only page; status live-region is above).

### Bridge React
- `pendingDiff?: PendingDiffPreview | null` prop on each mutation page component.
- `PendingDiffPreview` and `DiffEntry` exported from `@sunfish/ui-react`.
- `M4 fix` applied across all React identity pages: `role="alert"` error container always mounted.

---

## Test evidence

WCAG/a11y council subagent (W#58 Phase 3) returned PASS-WITH-AMENDMENTS
(amendments B1–B4 applied in Phase 1b; M-series amendments applied in Phase 3).
Phase 4 diff-preview surface follows established Ship's Office pattern
(ADR 0083 §8) — `<table>` + `<caption>` + `<th scope>` pattern confirmed by
prior council review of DocumentDiffPanel.

Full assisted-technology sweep recommended before any commercial conformance claim.
