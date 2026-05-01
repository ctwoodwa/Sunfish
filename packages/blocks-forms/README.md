# Sunfish.Blocks.Forms

Form orchestration block — opinionated composition over `SunfishForm` and `SunfishValidation`.

## What this ships

- Razor components for form composition (header, field group, validation summary, footer/submit).
- Wiring conventions over `SunfishForm` so consumer blocks can render a form without re-implementing layout + submit + cancel + validation flow.
- `Sunfish.Blocks.Forms.Resources` localization strings for standard form labels (Save / Cancel / Submit / etc.).

## DI

The forms block is presentation-layer (Razor + localization). No service registration required beyond a host calling `services.AddSunfishLocalization()` and including the `Sunfish.UIAdapters.Blazor` Razor components.

## Cluster role

Used by every block that renders an authoring or editing surface. Intentionally thin — the heavy lifting lives in `SunfishForm`'s component layer; this block ships the opinionated wiring.

## Future scope (if a forcing function surfaces)

- Dynamic-forms substrate consumer (ADR 0055 Phase 1 would feed this block's components).
- Form-template registry (per-tenant configurable forms).

## See also

- [apps/docs Overview](../../apps/docs/blocks/forms/overview.md)
