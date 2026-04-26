# Plan 5 Task 2 — a11y-audit-runner

**Status:** GREEN
**Token:** plan-5-task-2
**Code commit:** `9250f61d`
**Branch:** `worktree-agent-a44a3092c306f43fc`

---

## File list

Seven files committed under `tooling/a11y-audit-runner/` (one commit, path-scoped):

| Path | Purpose |
|---|---|
| `tooling/a11y-audit-runner/package.json` | Minimal Node 20+ ESM package (`@sunfish/a11y-audit-runner@0.1.0`, private) |
| `tooling/a11y-audit-runner/src/shard-allocator.mjs` | Pure SHA-256 deterministic shard allocator (`hash mod N`) |
| `tooling/a11y-audit-runner/tests/shard-allocator.test.mjs` | 4 TDD unit tests (determinism, partitioning, empty input, N=1) |
| `tooling/a11y-audit-runner/bin/run.mjs` | CLI entrypoint — reads Storybook `index.json`, allocates shard, invokes `pnpm test-storybook` |
| `tooling/a11y-audit-runner/shards/manifest.json` | Placeholder fallback for future explicit-manifest mode |
| `tooling/a11y-audit-runner/README.md` | Tool overview, usage, security note, Plan 5 reference |
| `tooling/a11y-audit-runner/.gitignore` | Local override of repo-root `**/[Bb]in/*` rule (needed so `bin/run.mjs` is tracked — the repo-root rule is for .NET build output) |

---

## Test pass count

```
node --test tooling/a11y-audit-runner/tests/shard-allocator.test.mjs

PASS allocator is deterministic - same story IDs always go to same shard (1.009ms)
PASS allocator partitions all stories across N shards (no duplicates, no drops) (7.784ms)
PASS allocator handles empty input (0.0782ms)
PASS allocator handles totalShards of 1 - all stories on shard 0 (0.1704ms)

tests 4
pass 4
fail 0
duration_ms 152.3413
```

**4/4 PASS.**

---

## Runner help / behavior output

Run against the test worktree (no `storybook-static/` build present):

```
$ node tooling/a11y-audit-runner/bin/run.mjs --shard 0 --total-shards 4
Storybook index not found at packages/ui-core/storybook-static/index.json; run pnpm --filter @sunfish/ui-core build-storybook first.
EXIT=1
```

Build-gate behavior matches the brief's expected output exactly: missing storybook index leads to exit 1 with an actionable build-first message.

Argument-parsing path also exercised:

```
$ node tooling/a11y-audit-runner/bin/run.mjs   # no args
Usage: run.mjs --shard N --total-shards M
EXIT=2
```

`NaN` arg-parse path exits 2 (distinct from missing-build exit 1).

---

## Security-pattern note: execFileSync vs the unsafe alternative

`bin/run.mjs` uses `execFileSync` from `node:child_process` with **array-form arguments**:

```javascript
import { execFileSync } from 'node:child_process';
// ...
execFileSync('pnpm', ['test-storybook', '--include-tags', myStories.join(',')], { stdio: 'inherit' });
```

This is the SECURITY-CRITICAL pattern called out by the Plan 5 Threat Model and the v1.3 carry-forward trust boundary in this brief. The contrast:

| Pattern | Behavior | Risk |
|---|---|---|
| `execFileSync('pnpm', [...args])` (chosen) | Spawns the binary directly with `argv` set from the array. **No shell.** Each array element becomes one literal argv slot regardless of its content. | None from injection - story IDs cannot escape into shell metacharacters. |
| `execSync` with a concatenated command string (rejected) | Spawns a shell (`/bin/sh -c <command-string>`). Story IDs are interpolated into a shell-parsed command line. | Command injection. A story ID containing `;`, `$()`, backticks, or `&&` would execute arbitrary code. |

Story IDs originate from `packages/ui-core/storybook-static/index.json`, which is a build artifact derived from `*.stories.*` files in the workspace. Even though those source files are repo-controlled today, treating the contents of any JSON file as untrusted-shaped data is the correct defensive posture: a malicious or buggy Storybook plugin, a future external story source, or a typo introducing a `$` in a story name would all silently introduce shell-injection surface under the unsafe alternative. With `execFileSync` + array args, none of those scenarios are exploitable.

The runner's source comment block also documents this rationale inline so future maintainers cannot regress the pattern without confronting the warning.

---

## Diff-shape verification

```
$ git show --stat HEAD | grep tooling
 tooling/a11y-audit-runner/.gitignore               |  5 +++
 tooling/a11y-audit-runner/README.md                | 47 ++++++++++++++++++++++
 tooling/a11y-audit-runner/bin/run.mjs              | 35 ++++++++++++++++
 tooling/a11y-audit-runner/package.json             |  9 +++++
 tooling/a11y-audit-runner/shards/manifest.json     |  4 ++
 tooling/a11y-audit-runner/src/shard-allocator.mjs  | 26 ++++++++++++
 tooling/a11y-audit-runner/tests/shard-allocator.test.mjs | 28 +++++++++++++
```

All 7 paths under `tooling/a11y-audit-runner/`. NO other files touched.

---

## Verdict: GREEN

- 4/4 tests PASS
- Runner exits 1 with expected message when storybook index missing
- Single commit, path-scoped to `tooling/a11y-audit-runner/`
- `execFileSync` + array args (security pattern enforced)
- No push performed (per brief)
