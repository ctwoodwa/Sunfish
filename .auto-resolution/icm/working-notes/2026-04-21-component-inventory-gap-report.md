# Component Inventory Gap Report

**Date:** 2026-04-21
**Context:** Pre-Wave-1 validation pass against `_shared/product/example-catalog.yaml`
**Tool:** Inventory subagent (Explore) walked `packages/ui-adapters-blazor/Components/`

---

## Summary

Catalog accuracy is approximately 40%. Three classes of drift were found:

| Class | Count | Severity |
|---|---|---|
| Catalog says implemented, tree missing | 13 | Blocking — would queue Wave 1 authoring against nonexistent components |
| Tree has, catalog missing | ~105 | Blocking — Wave 1 would leave these undocumented |
| Mislabeled aspirational (catalog says aspirational, tree implements) | 6 | High — placeholder pages would hide real components |

Additionally, **~148 legacy `Overview.razor` pages already exist** under `apps/kitchen-sink/Pages/Components/<Component>/Overview.razor`, following a pre-ADR-0022 layout. The new pattern is `Pages/Components/<Family>/<Component>/Overview/Demo.razor`.

---

## Catalog says sunfish-implemented, tree missing

Most are naming-convention mismatches (PascalCase in catalog vs actual tree casing):

| Catalog entry | Tree has | Resolution |
|---|---|---|
| `SunfishAutoComplete` | `SunfishAutocomplete` | Rename in catalog |
| `SunfishCheckBox` | `SunfishCheckbox` | Rename in catalog |
| `SunfishDropDownButton` | `SunfishDropDownButton` (different path) | Verify path |
| `SunfishFileSelect` | `SunfishFileUpload` | Rename in catalog |
| `SunfishFontIcon` | (none) | Move to aspirational or remove |
| `SunfishImageEditor` | (none) | Move to aspirational |
| `SunfishMaskedTextBox` | `SunfishMaskedInput` | Rename in catalog |
| `SunfishNumericTextBox` | `SunfishNumericInput` | Rename in catalog |
| `SunfishPager` | (none) | Move to aspirational |
| `SunfishPanelBar` | (none) | Move to aspirational |
| `SunfishPopupBox` | (none) | Move to aspirational |
| `SunfishStackLayout` | (none) | Move to aspirational |
| `SunfishToolBar` | `SunfishToolbar` | Rename in catalog |

---

## Tree has, catalog missing (~105)

Highlights (full list in corrected catalog):
Accordion, AIPrompt, Alert, AlertStrip, AllocationScheduler, ArcGauge, Barcode, Callout, Chat, Chip/ChipSet, CircularGauge, ConfirmDialog, DataSheet, Diagram, Divider, DockManager, Fab, Highlighter, IconButton, Image, NavBar/NavMenu/NavItem, Popover, QRCode, RadialGauge, Sankey, SegmentedControl, SearchBox, SignalRConnectionStatus, Snackbar, Spinner, Timeline, Toast.

Plus many architectural sub-components (GridColumn, MenuItem, Step, DockPane, etc.).

---

## Mislabeled aspirational

Catalog marked as aspirational but tree implements fully:

- `smart-ai.aiprompt` → `SunfishAIPrompt.razor` exists
- `smart-ai.chatui` → `SunfishChat.razor` exists
- `smart-ai.promptbox` → `SunfishPromptBox.razor` exists
- `smart-ai.smartpastebutton` → `SunfishSmartPasteButton.razor` exists
- `smart-ai.speechtotextbutton` → `SunfishSpeechToTextButton.razor` exists
- `charts.sankey` → `SunfishSankey.razor` exists

Flip all six to `status: sunfish-implemented`, `aspirational: false`.

---

## Legacy Overview pages (148)

Pattern: `apps/kitchen-sink/Pages/Components/<Component>/Overview.razor`

These were authored pre-ADR-0022 under a flat component-per-folder layout. The new pattern nests by family and uses a `Demo.razor` leaf file per canonical example.

**Migration options considered:**

- **(a) En-masse migration now** — rewrite all 148 to new pattern before Wave 1 proceeds. Large blocking effort but clean slate.
- **(b) Gradual migration during Wave 1** — each Wave 1 subagent that touches a component also migrates its legacy Overview. Integrates with fan-out; spreads the cost.
- **(c) Keep legacy coexisting** — accept the duplication; Wave 1 authors new `Overview/Demo.razor` alongside legacy `Overview.razor`; delete legacy later.

**Recommendation: (b)** — migration happens per-component as part of Wave 1 fan-out. Each subagent's contract includes "if a legacy `<Component>/Overview.razor` exists, migrate its example content into the new `<Family>/<Component>/Overview/Demo.razor` and delete the legacy file." This avoids a separate migration wave while ensuring no duplication lingers post-Wave-1.

---

## Next actions

1. Rewrite `_shared/product/example-catalog.yaml` from the tree (in progress).
2. Adopt (b) as the legacy migration policy in the Wave 1 subagent brief.
3. Proceed to landing page nav tree (Tier 3) once catalog is accurate.
