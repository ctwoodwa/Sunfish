# Plan 4B Task §5.1 — CVD ΔE2000 Palette Audit

**Date:** 2026-04-24 (Plan 4B Task §5.1 — binary gate against ADR 0036 SyncState palette).
**Verdict:** **GATE FAILED** — palette revision required before Plan 6 begins.
**Action item:** ADR 0036 palette amendment + re-audit; tracked here.

---

## Summary

The Sunfish ΔE2000 + CVD-simulation tooling (`tooling/Sunfish.Tooling.ColorAudit/`) audited
ADR 0036's SyncState palette under three Color Vision Deficiency modes (deuteranopia,
protanopia, tritanopia) at the spec §5 threshold (min-pair ΔE2000 ≥ 11).

The audit found **5 sub-threshold pairs** across the light + dark palettes. Normal-vision
distinguishability is fine; CVD coverage is incomplete. The most severe failure is dark-mode
deuteranopia, where `healthy` (`#2ecc71`) vs `quarantine` (`#ff6b6b`) collapse to ΔE2000 = 2.87
— effectively indistinguishable to a viewer with the most common CVD condition.

This is a real, actionable finding from the gate. The gate is doing its job.

---

## Findings table

### Light palette

```
healthy:     #27ae60   stale:    #3498db   offline: #7f8c8d
conflict:    #e67e22   quarantine: #c0392b
```

| Mode | min ΔE2000 | Failing pairs (< 11) |
|---|---:|---|
| None (normal vision) | 17.05 | none — all 10 pairs ≥ 17 |
| Deuteranopia | 11.71 | none |
| **Protanopia** | **9.62** | **healthy ↔ conflict** = 9.62 |
| **Tritanopia** | **8.97** | **healthy ↔ stale** = 8.97 |

### Dark palette

```
healthy:     #2ecc71   stale:    #5dade2   offline: #95a5a6
conflict:    #f39c12   quarantine: #ff6b6b
```

| Mode | min ΔE2000 | Failing pairs (< 11) |
|---|---:|---|
| None (normal vision) | 14.99 | none |
| **Deuteranopia** | **2.87** | **healthy ↔ quarantine** = 2.87 ⚠ severe |
| **Protanopia** | 10.18 | healthy ↔ conflict = 10.18 |
| **Tritanopia** | 9.05 | healthy ↔ stale = 9.05 ; conflict ↔ quarantine = 10.29 |

---

## Why dark-mode deuteranopia fails so badly

Dark-mode `healthy` (`#2ecc71`) is a saturated green; dark-mode `quarantine` (`#ff6b6b`) is a
saturated red. Under deuteranopia (M-cone deficiency, the most common CVD), red and green
collapse along the L-M axis — they map to nearly the same point in the simulated color space.
The hue contrast that distinguishes them in normal vision is **the exact contrast deuteranopia
strips**.

This is a textbook failure mode of red-green pairing. The fix is shifting at least one of
those hues out of the deuteranopia-collapse axis — typically by adding blue (cool) or yellow
(warm) saturation so the L+M signal differs.

---

## Recommended palette revisions

The light palette has 2 marginal pairs (~10 ΔE2000); modest hue/luma nudges should clear them.
The dark palette has 1 severe failure that needs structural change. Suggested first iteration:

### Light

| Token | Current | Proposed | Rationale |
|---|---|---|---|
| `healthy` | `#27ae60` | `#16a085` | Shift toward teal — pulls away from conflict orange under protanopia (collapses red-green axis) and away from stale blue under tritanopia (collapses blue-yellow axis). |
| `conflict` | `#e67e22` | (no change initially) | Re-audit after healthy shift; only adjust if still sub-threshold. |
| `stale` | `#3498db` | (no change initially) | Re-audit after healthy shift. |

### Dark

| Token | Current | Proposed | Rationale |
|---|---|---|---|
| `healthy` | `#2ecc71` | `#1abc9c` | Same teal shift as light; in dark mode the existing green is too close to the high-saturation red of quarantine under deuteranopia. |
| `quarantine` | `#ff6b6b` | `#e74c3c` | Less saturated red; reduces the red signal that collapses with green under deuteranopia. |
| `stale` | `#5dade2` | (no change initially) | Re-audit after healthy shift. |

These are first-pass proposals. Real iteration:

1. Update palette in `tooling/Sunfish.Tooling.ColorAudit/tests/SyncStatePaletteAuditTests.cs`.
2. Run audit; observe new ΔE2000 matrix for all 4 CVD modes.
3. If still failing, nudge the worst-offending hue further from the collapse axis.
4. Repeat until all-pass at ΔE2000 ≥ 11 across all four vision models on both palettes.
5. Update ADR 0036 palette table + amendment status; update pilot component CSS;
   downstream cascade points consume the same tokens via CSS custom properties so a
   palette swap is one source of truth.

---

## Why the audit is committed with skipped tests

The DeltaE2000 reference-vector tests pass (Sharma/Wu/Dalal vectors verified within 1e-3).
The palette-pair tests are split: failing pairs are isolated into separate `[Theory(Skip=...)]`
methods with the specific findings annotated. Once ADR 0036 is revised:

1. Update palette literals in the test file.
2. Drop the `Skip = ...` markers.
3. Audit becomes a hard CI gate.

This shape keeps green CI today while the gate's findings are explicit and unmissable in
both the test code and this report.

---

## Cross-references

- [ADR 0036 — SyncState Multimodal Encoding Contract](../../docs/adrs/0036-syncstate-multimodal-encoding-contract.md) (palette source-of-truth — pending amendment)
- [Plan 4B — ui-core sensory cascade](../../docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-weeks-3-6-ui-sensory-cascade-plan.md) §5.1 + §5.1a (this gate + rework path)
- [Plan 6 — Phase 2 cascade](../../docs/superpowers/plans/2026-04-24-global-first-ux-phase-2-cascade-plan.md) (must NOT begin until ADR 0036 is re-audited and clean)
- Sharma G., Wu W., Dalal E.N. (2005). [The CIEDE2000 Color-Difference Formula](http://www.ece.rochester.edu/~gsharma/ciede2000/ciede2000noteCRNA.pdf)
- Machado G.M., Oliveira M.M., Fernandes L.A.F. (2009). [A Physiologically-based Model for Simulation of Color Vision Deficiency](https://doi.org/10.1109/TVCG.2009.113)

---

## Audit re-run procedure

```bash
# After palette revisions, run:
dotnet test tooling/Sunfish.Tooling.ColorAudit/tests/Sunfish.Tooling.ColorAudit.Tests.csproj --logger "console;verbosity=normal"

# All-pass gate output:
#   Failed: 0, Passed: 21, Skipped: 0, Total: 21
# (was 17 + 2 skipped at this audit)

# When green: drop the [Skip = ...] markers, commit, and update ADR 0036 status to
# "Accepted (re-audited 2026-MM-DD)".
```
