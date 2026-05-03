# Plan 2 binary gate

Permanent CI assertion that the Plan 2 Task 3.6 outcomes — completed during the
Wave 4 close-out — never regress on `main`.

## What it asserts

Two binary gates, both fail-closed:

1. **All 14 `packages/blocks-*` packages have a `Resources/Localization/SharedResource.resx`
   bundle.** This is the foundation that lets every block participate in the
   layered cascade (foundation → block → app override) without per-block scaffolding
   skew. Originally established by PR #85 (Wave 4 close-out).
2. **At least 3 `services.AddLocalization()` call sites exist** across `packages/`,
   `apps/`, and `accelerators/`. The minimum-3 floor proves the wiring is reachable
   from more than one composition root (currently 12 call sites — well above floor).
   Lowering below 3 would mean a regression in how blocks/apps register the
   localization services with the DI container.

If either count slips below its threshold, the gate prints
`SUNFISH_PLAN_2_GATE: …` to stderr and exits non-zero.

## How to run locally

From the repo root:

```bash
bash tooling/plan-2-binary-gate/check.sh
```

Expected output on a clean tree:

```
Plan 2 binary gate: PASS
  blocks-* SharedResource.resx: 14 / 14
  services.AddLocalization() call sites: 12 (min 3)
```

## CI wiring

The gate runs as the `plan-2-binary-gate` job in
`.github/workflows/global-ux-gate.yml` on every PR that touches the existing
`global-ux-gate` paths and on every push to `main`. It is intentionally cheap
(a `find` + a `grep -r`) so it can run on every PR without slowing the gate.

## Why this is a permanent gate

Plan 2 Task 3.6 was a one-time discovery binary gate; once the artifacts existed
the gate's job was nominally done. Plan 5 Task 8 promotes it to a permanent
assertion so any future refactor that accidentally drops a blocks-* RESX bundle
or unwires the DI registrations fails CI immediately rather than being caught
by a translator months later.

## Related

- `tooling/locale-completeness-check/` — the canonical Node-based completeness
  tool (per-bundle, per-locale percentages). This script is intentionally
  narrower: it asserts *existence* and *call-site count*, not coverage.
- `_shared/engineering/coding-standards.md` — Sunfish coding standards.
