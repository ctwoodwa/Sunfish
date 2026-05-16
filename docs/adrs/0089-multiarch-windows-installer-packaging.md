---
id: 89
title: Multi-arch Windows Installer Packaging Strategy
status: Proposed
date: 2026-05-16
tier: accelerator
pipeline_variant: sunfish-feature-change

concern:
  - distribution
  - ui
  - supply-chain

enables:
  - windows-arm64-installer-delivery
  - zero-friction-windows-install-ux

composes:
  - 86   # Anchor Tauri-React product surface (x64 + ARM64 CI matrix)

extends: []
supersedes: []
superseded_by: null
deprecated_in_favor_of: null

amendments: []
---

# ADR 0088 — Multi-arch Windows Installer Packaging Strategy

**Status:** Proposed
**Date:** 2026-05-16

---

## Context

W#60 Phase 4 (collaboration) builds on the Tauri Anchor desktop shell established in Phase 3.
ADR 0086 § CI matrix already requires the winhub cross-compile pipeline to produce both
`x64-setup.exe` and `arm64-setup.exe` artifacts as first-class release outputs.

On 2026-05-16, CO attempted to install the ARM MSI on the Surface Pro 7 and received:

> *"This installation package is not supported by this processor type."*

SSH investigation confirmed the device is Intel i3-10100Y AMD64, not ARM. The ARM MSI
was rejected because it was the wrong arch for the hardware. This incident exposes a
customer-facing UX gap: a user who downloads the wrong installer variant gets a confusing
error message instead of a working install. CO's directive: *"the installation should
support cpu detection and work no matter what hardware."*

Three approaches were evaluated:

| # | Shape | UX | Cost |
|---|---|---|---|
| **A** | Bootstrapper `.exe` | Two-step (launcher then real installer) | Small — ~50 lines C/.NET wrapping ShellExecute + IsWow64Process2 |
| **B** | Multi-arch MSI/MSIX | Single click; unified package | Medium — custom WiX template with per-arch component blocks |
| **C** | Web-layer arch detection | Single click on download page; browser detects arch and serves the right link | Lowest — concentrates logic in a few lines of JS on the download page |

This is the pattern used by VS Code, Discord, and Slack: a download page detects CPU
architecture client-side via `navigator.userAgentData.platform` (or UA-string fallback)
and surfaces the matching artifact URL. The user clicks one "Download" button.

---

## Decision

### Phase 1 — Web-layer arch detection (ships with W#60 PR 5)

Implement Approach C on the Anchor download / self-hosting docs page. The page:

1. Calls `navigator.userAgentData.getHighEntropyValues(['architecture'])` if supported.
2. Falls back to UA-string heuristics (`Win64`, `WOW64`) if `userAgentData` is absent
   (Edge Legacy, Firefox, non-Chromium browsers).
3. Surfaces the matching download link (`_x64-setup.exe` or `arm64-setup.exe`).
4. Includes a manual arch toggle ("Show all downloads") so power users and CI pipelines
   can bypass detection.
5. Displays a one-line note when falling back: "Pick `arm64` if you're on a Snapdragon X
   device; pick `x64` otherwise."

This requires no changes to the Tauri bundler pipeline. Both artifacts are already produced
by ADR 0086's CI matrix. Web detection is verifiable in DevTools without ARM hardware.

### Phase 2 — Multi-arch MSIX/WiX (deferred until Windows-ARM device on tailnet)

Implement Approach B — a single unified installer that installs the correct binary for the
host CPU — once the following gate clears:

- A Windows-ARM device (Snapdragon X / Surface Pro X/9/11 / SQ-series) is on the tailnet
  and can PASS-test the ARM binary end-to-end (login flow + Stronghold persistence).

Until that gate clears, Phase 2 is a tracked follow-on, not a blocking gap. ADR 0086's CI
matrix continues producing ARM artifacts so the binary is ready when hardware materializes.

### Approach A — Skipped permanently

The bootstrapper `.exe` pattern is explicitly ruled out. Engineering cost is similar to
Approach C; UX is meaningfully worse (two-step install: launcher downloads + re-launches the
real installer); and Phase 2 (MSIX) would retire it anyway. No reason to ship a kludge that
requires its own retire-on-sight cycle.

---

## Consequences

**Positive:**
- Zero installer engineering cost for Phase 1 — shipping exactly what VS Code, Discord, and
  Slack ship, using established browser APIs.
- Both x64 and ARM artifacts continue to be produced and hosted; ARM users are not forced
  onto the x64 emulation path.
- Phase 2 gate is concrete: ARM hardware on the tailnet → PASS test → MSIX implementation.
- The web detection layer is testable without ARM hardware (DevTools `userAgentData` override).

**Negative / Risks:**
- Web detection does not help a user who obtained the installer via a direct link, email
  attachment, or offline distribution. The manual toggle mitigates this for technical users.
- The download page becomes a deployment dependency for Windows installer UX. If the page is
  down, users must know to pick x64 vs arm64 themselves.
- `navigator.userAgentData` is Chromium-only (Chrome, Edge, Brave). Firefox and Safari users
  fall back to UA-string heuristics, which are less precise for ARM detection.

**Neutral:**
- ADR 0086 CI matrix is unaffected; both artifact types must continue to be produced and
  hosted at known URLs.
- The Tauri auto-updater operates after first install; it targets the correct arch binary by
  construction (update check originates from the running binary's arch). Web detection is a
  first-install concern only.

---

## Rationale for deferring Phase 2

Multi-arch MSI/MSIX (WiX template with `<Component Architecture="x64">` +
`<Component Architecture="arm64">` blocks) cannot be PASS-verified without actual ARM
hardware to run the ARM-native binary. The Surface Pro 7 on the current tailnet is x64.

Building and shipping a multi-arch installer that has never been tested on the target
arch inverts the ADR 0086 PASS criterion. Phase 2 waits for the gate because the gate
is the correct quality bar, not because the engineering is hard.

---

## Implementation notes (Phase 1, W#60 PR 5)

The detection snippet is ~20 lines of JS, suitable for either a standalone download page in
`apps/docs/` or a dedicated route in `accelerators/bridge/`'s static pages:

```js
async function detectWindowsArch() {
  try {
    const data = await navigator.userAgentData.getHighEntropyValues(['architecture']);
    return data.architecture === 'arm' ? 'arm64' : 'x64';
  } catch {
    // UA-string fallback: WOW64 = x64 on x64; ARM64 shows ARM in some UAs
    const ua = navigator.userAgent;
    if (/ARM/i.test(ua)) return 'arm64';
    return 'x64'; // safe default; x64 covers the vast majority of Windows installs
  }
}
```

Both artifact URLs must be parameterised; the page renders both in the "Show all downloads"
fallback view regardless of detected arch.

---

## Related

- [ADR 0086](./0086-anchor-tauri-react-product-surface.md) — Anchor Tauri-React product
  surface; mandates CI matrix producing x64 + ARM64 artifacts
- [ADR 0087](./0087-role-key-forward-secrecy-explicit-acceptance.md) — Role key forward
  secrecy (unrelated; cited here only to confirm 0088 is the correct next slot)
- W#60 P4 hand-off doc — `icm/_state/handoffs/w60-collaboration-phase4-stage06-handoff.md`
- XO ruling 2026-05-16 T15-46Z — canonical ruling establishing C-now / B-deferred / A-skip
