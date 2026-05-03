# cross-plan-health

Plan 5 Task 7 — cross-plan health gate. Reads `waves/global-ux/status.md`,
parses each plan's verdict from the **Plans authored** table, and exits
non-zero if any plan is RED. Carries forward the Wave 1 finding that
multi-plan execution drift (Plan 3 stalled at RED, Plan 4 drifting toward
its 30-day kill trigger) needs an automated surfacing mechanism rather than
relying on a human reading the status doc each week.

## Usage

```bash
# From repo root
node tooling/cross-plan-health/check.mjs waves/global-ux/status.md
```

Exit codes:

| Code | Meaning |
|---|---|
| `0` | Healthy — no plan is RED. |
| `1` | One or more plans are RED; names are printed to stderr under the `SUNFISH_PLAN_HEALTH:` prefix. |
| `2` | Could not read the status file (path missing, permission, etc.). |

## Tests

```bash
node --test tooling/cross-plan-health/tests/parser.test.mjs
```

Three TDD-first tests cover:

1. Markdown-table parsing (`parseStatusTable`).
2. RED detection (`evaluateHealth` flags any RED).
3. GREEN/YELLOW pass-through (`evaluateHealth` passes when no RED).

## CI

Wired into `.github/workflows/global-ux-gate.yml` as the `cross-plan-health`
job. It runs on:

- `push` to `main` (catches a status update merged in).
- `schedule` (Monday 12:00 UTC) — weekly drift check.

The job is `continue-on-error: true` for the first two weeks so it produces
informational signal without blocking CI. Promote to a required check
after the baseline is stable per Plan 5 spec.

## Source-of-truth

The status table parsed by this tool is `waves/global-ux/status.md`'s
`## Plans authored` section. Verdicts are parsed by these precedence rules
(first match wins):

1. `RED` → `RED`
2. `YELLOW` → `YELLOW`
3. `GREEN` → `GREEN`
4. `READY` or `COMPLETE` → `GREEN`
5. `BLOCKED` → `RED`
6. anything else → `UNKNOWN` (does not trigger the gate)

## Security

- No shell interpolation. The status path is read via `readFileSync`.
- The CLI accepts one argument (path); never `eval`'d.
- Untrusted inputs (the markdown body) are parsed as data only.
