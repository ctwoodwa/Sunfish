# @sunfish/a11y-audit-runner

Node.js orchestrator that fans Storybook a11y tests across N shards deterministically and runs `pnpm test-storybook` per shard. Drives the accessibility CI gate defined in Plan 5 (Wave global-ux).

## Why

A monolithic Storybook a11y run is slow and a single point of failure. Fanning across 4 parallel shards cuts wall-clock time, surfaces flakes per-shard, and lets CI retry one shard without rerunning the rest. The allocator is **deterministic** so the same story always lands on the same shard across machines and runs — that property is what makes per-shard caches and "rerun this shard" workflows safe.

## Usage

```bash
# In CI, one job per shard (typically 4 jobs):
node tooling/a11y-audit-runner/bin/run.mjs --shard 0 --total-shards 4
node tooling/a11y-audit-runner/bin/run.mjs --shard 1 --total-shards 4
node tooling/a11y-audit-runner/bin/run.mjs --shard 2 --total-shards 4
node tooling/a11y-audit-runner/bin/run.mjs --shard 3 --total-shards 4
```

The runner:
1. Reads `packages/ui-core/storybook-static/index.json` (must be built first).
2. Computes `SHA-256(storyId) mod totalShards` to assign each story to one shard.
3. Invokes `pnpm test-storybook --include-tags <comma-separated-story-ids>` for stories on this shard.

If the storybook static build is missing the runner exits 1 with a build-first message.

If a shard receives zero stories (rare with N=4 and dozens of stories) the runner exits 0 cleanly.

## Tests

```bash
node --test tooling/a11y-audit-runner/tests/
```

Four unit tests cover: determinism, full-coverage partitioning, empty input, and N=1 degenerate case.

## Security note (Plan 5 Threat Model)

`bin/run.mjs` invokes child processes with `execFileSync` and an explicit `args[]` array — never `execSync` with shell interpolation. Story IDs come from a JSON file on disk and are treated as untrusted-shaped data; routing them through a shell would expose a command-injection vector if a story name ever contained shell metacharacters (`;`, `$()`, backticks, `&&`, etc.).

## Fallback manifest

`shards/manifest.json` is a placeholder for an explicit-shard-assignment override (future `--use-manifest` and `--write-manifest` flags). Today it is empty; the SHA allocator is the source of truth.

## References

- Plan 5 (CI Gates) — `waves/global-ux/plan-5/` (planning artifacts)
- Sister tool: `tooling/locale-completeness-check/` (same Node 20+ ESM pattern)
