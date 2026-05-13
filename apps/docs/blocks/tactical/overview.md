---
uid: block-tactical-overview
title: Tactical — Overview
description: Blazor UI blocks for the Tactical Anomaly Detection + Threat-Trigger surface (ADR 0081) — SonarRoomPanel, LookoutPanel, FireControlPanel, EmergencyStandingOrderDialog, and LookoutQuarterdeckAlertSource.
keywords:
  - sunfish
  - blocks
  - tactical
  - anomaly-detection
  - threat-trigger
  - accessibility
  - adr-0081
---

# Tactical — Overview

## What this block is

`Sunfish.Blocks.Tactical` is the **Blazor UI composition layer** for the Tactical Anomaly
Detection + Threat-Trigger surface (ADR 0081). It bridges `foundation-tactical` domain types
with WCAG 2.2 AA–compliant sub-room panels and the Quarterdeck alert ticker.

The block ships:

- **`SonarRoomPanel`** — Informational Sonar alert list + signal-rate gauge + registered-rules
  list. Live region: `aria-live="polite"`.
- **`LookoutPanel`** — Assertive live region for high-priority alerts + pause control +
  `aria-disabled` acknowledge pattern per §7.5.
- **`FireControlPanel`** — Active incident list + runbook-step display + Emergency Order
  trigger button per §7.3. Live region: `aria-live="polite"` for status transitions.
- **`EmergencyStandingOrderDialog`** — `role="alertdialog"` + 2000 ms deliberation pause
  (SC 3.3.4) + fail-close template token validation + Escape-key handler per §7.6.
- **`LookoutQuarterdeckAlertSource`** — `IQuarterdeckAlertSource` plug-in (source name
  `"sunfish.tactical.lookout"`) that surfaces Lookout alerts onto the Quarterdeck ticker
  per ADR 0080 §2.3 + ADR 0081 §7.2.

## Package

- Package: `Sunfish.Blocks.Tactical`
- Source: `packages/blocks-tactical/`
- Namespace root: `Sunfish.Blocks.Tactical`

## Dependencies

| Package | Role |
|---|---|
| `Sunfish.Foundation.Tactical` | Domain contracts: `ILookout`, `ISonarStore`, `TacticalAlert`, `IncidentRecord`, etc. |
| `Sunfish.Foundation.Quarterdeck` | `IQuarterdeckAlertSource`, `QuarterdeckAlert` |
| `Sunfish.UICore` | `ILiveAnnouncer`, `IFocusTrap`, `SunfishA11yAssertions` |
| `Sunfish.UIAdapters.Blazor` | Blazor-specific adapter implementations |
| `Sunfish.Foundation.Ship.Common` | `ShipRole` — used for authority-chain context |

**MUST NOT** directly depend on `foundation-wayfinder` or `foundation-engine-room`.

## Sub-rooms

All panels implement the **sub-room** layout contract: `<section role="region"
aria-labelledby="{id}-heading" id="{id}" tabindex="-1">`. The `id` attribute is a
skip-link target — host layouts MUST wire skip links per ADR 0077 §4.

| Panel | Section id | Skip-link label |
|---|---|---|
| `SonarRoomPanel` | `sonar-room` | Sonar Room |
| `LookoutPanel` | `lookout` | Lookout |
| `FireControlPanel` | `fire-control` | Fire Control |

## Accessibility compliance

All components ship with `SunfishA11yAssertions`-verified WCAG 2.2 AA patterns.
WCAG/a11y + security-engineering council review was mandatory for Phases 3a/3b per ADR 0081
§A1 and is verified by the 28 bUnit tests in `Sunfish.Blocks.Tactical.Tests`.

Key patterns:
- **Assertive live region** (LookoutPanel): `aria-live="assertive" aria-atomic="false"
  aria-relevant="additions"` — new alerts only, not removals.
- **Polite live region** (SonarRoomPanel, FireControlPanel): `aria-live="polite"
  aria-atomic="true"` — status transitions.
- **Deliberation pause** (EmergencyStandingOrderDialog): Confirm `aria-disabled` + `disabled`
  for 2000 ms; enabled on timer fire with "Confirm available" polite announcement.
- **alertdialog** (EmergencyStandingOrderDialog): `role="alertdialog"` (not `dialog`) signals
  security-critical urgency; `aria-modal="true"` communicates virtual-buffer containment.
- **Template token fail-close**: consequence text with unresolved `{{tokens}}` permanently
  disables Confirm for that open cycle.

## ADR references

- **ADR 0081** — Tactical Anomaly Detection + Threat-Trigger Surface (primary)
- **ADR 0080** — Quarterdeck Entry-Point (LookoutQuarterdeckAlertSource integration)
- **ADR 0077** — Shared Design System (live region + focus-trap contracts)
