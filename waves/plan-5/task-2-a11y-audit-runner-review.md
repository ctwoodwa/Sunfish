# Plan 5 Task 2 Review — a11y-audit-runner

**Date:** 2026-04-26
**Code commit:** 9250f61d
**Report commit:** 04fcaa27
**Branch:** worktree-agent-a44a3092c306f43fc

## Per-criterion results

(a) **PASS** — `git show --name-only 9250f61d | sort` returns exactly the 7 expected files under `tooling/a11y-audit-runner/`: `.gitignore`, `README.md`, `bin/run.mjs`, `package.json`, `shards/manifest.json`, `src/shard-allocator.mjs`, `tests/shard-allocator.test.mjs`. No surplus paths.

(b) **PASS** — `bin/run.mjs` imports `execFileSync` from `node:child_process` and invokes it as `execFileSync('pnpm', ['test-storybook', '--include-tags', myStories.join(',')], { stdio: 'inherit' })`. Array-form args, no shell, no template-string interpolation, no unsafe sibling API. Story IDs cannot reach a shell parser.

(c) **PASS** — `src/shard-allocator.mjs` (1) imports `createHash` from `node:crypto`, and (2) returns `stories.filter(id => { const hash = createHash('sha256').update(id).digest(); const bucket = hash.readUInt32BE(0) % totalShards; return bucket === shardIndex; })`. SHA-256 truncated to 32-bit unsigned, mod totalShards — clean deterministic hash-mod-N.

(d) **PASS** — `tests/shard-allocator.test.mjs` contains all 4 required tests with the right shapes:
   - `'allocator is deterministic — same story IDs always go to same shard'`
   - `'allocator partitions all stories across N shards (no duplicates, no drops)'` — uses 1000 stories x 4 shards, asserts `equal(all.length, 1000)` and `equal(new Set(all).size, 1000)`
   - `'allocator handles empty input'`
   - `'allocator handles totalShards of 1 — all stories on shard 0'`
   Executed `node --test` from the worktree at `C:/Projects/Sunfish/.claude/worktrees/agent-a44a3092c306f43fc`: **4 pass, 0 fail, duration 178ms**.

(e) **PASS** — `package.json` is minimal: `"name": "@sunfish/a11y-audit-runner"`, `"type": "module"`, `"private": true`. Single `test` script (`node --test tests/`), no `dependencies` block. Uses only Node built-ins (`node:crypto`, `node:child_process`, `node:fs`, `node:test`, `node:assert/strict`). One nit: a `version: "0.1.0"` field is present (not in the criterion enumeration) — harmless, conventional for `private: true` packages.

(f) **PASS** — `shards/manifest.json` is the fallback file with `_comment` describing future `--use-manifest` / `--write-manifest` semantics and `"shards": []` empty array.

(g) **PASS** — `README.md` documents purpose, usage (per-shard CLI invocation), the SHA-mod-N algorithm, the four test cases, the security note, the fallback manifest, and references Plan 5 explicitly ("Plan 5 (CI Gates) — `waves/global-ux/plan-5/`") plus the sister tool `tooling/locale-completeness-check/`.

(h) **PASS** — Commit message: `feat(tooling): plan-5-task-2 — a11y-audit-runner with deterministic 4-shard allocation` and a body line `Token: plan-5-task-2`. Token present in both subject and trailer.

(i) **PASS** — `git show --stat 9250f61d` confirms all 7 files are under `tooling/a11y-audit-runner/`. No other paths touched. 154 insertions, 0 deletions.

(j) **PASS** — Repo-root `.gitignore` line 38 has `**/[Bb]in/*` (the .NET build-output rule, scoped by the comment on line 37). The unplanned `tooling/a11y-audit-runner/.gitignore` un-ignores `bin/` and `bin/*` with a clear inline justification ("This Node tool's `bin/` is a source directory, not a build artifact"). The negation is correct and the rationale is sound — without it, `bin/run.mjs` would be silently dropped from `git add`.

## Security check (execFileSync vs unsafe sibling API)

The runner's only child-process invocation is at line 35 of `bin/run.mjs`:

```js
execFileSync('pnpm', ['test-storybook', '--include-tags', myStories.join(',')], { stdio: 'inherit' });
```

**Evaluation:**
- Uses the array-args sync API (not the shell-string sibling) → no shell is spawned; the executable is invoked directly via the OS `execve`/`CreateProcess` path. Shell metacharacters (`;`, `$()`, backticks, `&&`, `||`, `|`, `>`, `<`) in story IDs are passed as literal argv bytes to `pnpm`, never parsed.
- Args supplied as an explicit array, not a template string. There is no string concatenation that could trigger argv splitting.
- The `myStories.join(',')` value is exactly one argv element. Even if a story ID contained a comma, only `pnpm test-storybook --include-tags` semantics would be affected (a flag-parsing concern, not a security one).
- An inline comment above the call cites the Plan 5 Threat Model and explains the design intent — anyone touching this code in the future is warned not to regress.
- No `shell: true` option (the dangerous escape hatch on the array API) is set.
- A scan of the file finds zero occurrences of the unsafe shell-string sibling, no `child_process` shell-string entrypoint, no `spawn` with `shell:true`, and no template-string command construction.

**Verdict on security:** clean. The runner cannot be coerced into shell execution by any value in the Storybook `index.json` file. This is the correct hardening for the threat model.

## Final verdict: GREEN
