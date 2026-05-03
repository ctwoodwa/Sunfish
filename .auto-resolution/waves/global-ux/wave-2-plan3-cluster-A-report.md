# Wave 2 / Plan 3 — Cluster A (Husky Sentinel) Report

**Branch:** `global-ux/wave-2-plan3-husky-hook`
**Token:** `wave-2-plan3-husky-sentinel`
**Code commit:** `fd299831`
**Self-verdict:** GREEN
**Sentinel role:** Install Husky.Net local-tool + pre-commit hook that enforces SUNFISH_I18N_001 on staged `.resx` files.

---

## Summary

The pre-commit gate for the Wave 2 cascade discipline is now installed. Any contributor who runs `pnpm install` (or `npm install`) auto-installs Husky.Net via the `prepare` script. On `git commit`, if any staged `.resx` lives under a `.csproj` and contains a `<data>` entry without a `<comment>`, the commit is rejected with a clear, file/line-anchored message.

Empirically verified end-to-end: a temp bad RESX → staged → hook fires → exit code 1 → unstaged + cleaned. No drift remains in the worktree.

---

## Files in the commit

| Path | Action | LOC delta |
|---|---|---|
| `.config/dotnet-tools.json` | modified — added Husky 0.9.1 entry | +8 / −1 |
| `package.json` | modified — added `scripts.prepare` | +1 |
| `.husky/pre-commit` | created — SUNFISH_I18N_001 enforcement script | +76 |
| `.husky/.gitignore` | created — ignores Husky framework cache `_/` | +1 |

`git diff --cached --stat` against the commit:

```
.config/dotnet-tools.json | 10 ++++++-
.husky/.gitignore         |  1 +
.husky/pre-commit         | 76 +++++++++++++++++++++++++++++++++++++++++++++++
package.json              |  1 +
4 files changed, 87 insertions(+), 1 deletion(-)
```

### `.config/dotnet-tools.json` diff

```diff
   "tools": {
     "docfx": {
       "version": "2.78.5",
       "commands": [ "docfx" ],
+      "rollForward": false
+    },
+    "husky": {
+      "version": "0.9.1",
+      "commands": [ "husky" ],
       "rollForward": false
     }
   }
```

(The `rollForward: false` field on the existing `docfx` tool is added by `dotnet tool install` when it touches the manifest — unavoidable, harmless, default behavior.)

### `package.json` diff

```diff
   "scripts": {
+    "prepare": "dotnet tool restore && dotnet husky install",
     "scss:build:fluentui": "...",
```

### `.husky/.gitignore`

```
_/
```

### `.husky/pre-commit` (key logic)

```sh
STAGED_RESX=$(git diff --cached --name-only --diff-filter=ACMR | grep '\.resx$' || true)
[ -z "$STAGED_RESX" ] && exit 0

# Walk up to find the enclosing .csproj for each staged .resx
# Build each affected csproj, grep for SUNFISH_I18N_001
# If any match: print path/line/key and exit 1
```

Full file is in the commit. Uses POSIX `sh` (compatible with the shipped `_/husky.sh` framework, Git Bash on Windows, and Linux/macOS shells contributors use).

---

## Build-gate evidence

### `dotnet tool install Husky` (in worktree)

```
You can invoke the tool from this directory using the following commands:
'dotnet tool run husky' or 'dotnet husky'.
Tool 'husky' (version '0.9.1') was successfully installed.
Entry is added to the manifest file
C:\Projects\sunfish\.claude\worktrees\agent-a56723870e0b90c97\.config\dotnet-tools.json.
```

### `dotnet tool restore`

```
Tool 'docfx' (version '2.78.5') was restored. Available commands: docfx
Tool 'husky' (version '0.9.1') was restored. Available commands: husky
Restore was successful.
```

### `dotnet husky install`

```
Git hooks installed
```

(Idempotent — safe to re-run; that is how the `prepare` script behaves on subsequent contributor `pnpm install`s.)

### `pnpm install --frozen-lockfile` (proves `prepare` is wired)

Invoked via `npx pnpm@10` (pnpm not on this sandbox's PATH; npx works for everyone).

```
. prepare$ dotnet tool restore && dotnet husky install
. prepare: Tool 'docfx' (version '2.78.5') was restored. Available commands: docfx
. prepare: Tool 'husky' (version '0.9.1') was restored. Available commands: husky
. prepare: Restore was successful.
. prepare: Git hooks installed
. prepare: Done
Done in 8.7s using pnpm v10.33.2
```

The `prepare` lifecycle hook fired exactly once during install, restored both .NET tools, and installed the git hooks. Zero errors. Workspace projects (`Scope: all 3 workspace projects`) detected and respected.

---

## Step 6 — Manual hook validation (the bad-RESX experiment)

**Setup.** Created `packages/foundation/Resources/Localization/HuskyTestBad.resx` with one offending entry:

```xml
<data name="husky.test.bad" xml:space="preserve">
  <value>X</value>
</data>
```

(No `<comment>` child — exactly the SUNFISH_I18N_001 violation shape.)

**Action.** `git add` the file (do not commit), then invoke `bash .husky/pre-commit` directly.

**Observed output:**

```
[husky] SUNFISH_I18N_001 — validating staged .resx files...
[husky]   building packages/foundation/Sunfish.Foundation.csproj

[husky] BLOCKED — SUNFISH_I18N_001 violations in packages/foundation/Sunfish.Foundation.csproj:
    ...HuskyTestBad.resx(28,1): error SUNFISH_I18N_001: RESX entry 'husky.test.bad'
    has no <comment> — translators need context to localize correctly
    (https://github.com/ctwoodwa/Sunfish/blob/main/docs/diagnostic-codes.md#sunfish_i18n_001)
    [...Sunfish.Foundation.csproj]

[husky] Add a <comment> child to each <data> element in the affected
[husky] .resx file. The comment is the translator hint; it is mandatory.
husky - pre-commit hook exited with code 1 (error)
EXIT=1
```

**Verdict.** The hook:
1. Correctly detected the staged `.resx`.
2. Correctly walked up to the parent `Sunfish.Foundation.csproj`.
3. Correctly invoked `dotnet build` and surfaced the analyzer diagnostic.
4. Correctly exited non-zero with file path, line, key name, and remediation guidance.

The diagnostic message includes the canonical doc link, the offending key (`husky.test.bad`), and tells the developer exactly what to do.

**Cleanup.** `git restore --staged` + `rm` of the temp file. Worktree clean before commit.

---

## Pivots / deviations

**Pivot 1 — `task-runner.json` not staged.** `dotnet husky install` auto-creates `.husky/task-runner.json` (a Husky-defined task runner config). The brief's diff-shape constraint says ONLY `.config/dotnet-tools.json`, `package.json`, `.husky/pre-commit`, `.husky/.gitignore`. The `task-runner.json` is therefore intentionally left untracked. Contributors regenerate it when their local `prepare` runs `dotnet husky install`. Our pre-commit hook does not depend on the task runner (it invokes shell directly), so this is benign. Documenting for the driver: if you want `task-runner.json` versioned later, add it in a follow-up commit; out of sentinel scope.

**Pivot 2 — pnpm not on sandbox PATH.** Worked around with `npx pnpm@10 install --frozen-lockfile`. Identical semantics for the prepare-script test; contributor environments will have pnpm on PATH.

**Worktree contamination self-correction.** Early in execution I ran `cd C:/Projects/sunfish && dotnet tool install Husky` — that's the *parent* checkout, not this isolated worktree. Discovered when post-test `git status` reported a different branch. Fully reverted the parent worktree (only the unrelated `.wolf/*` working-tree changes from the parent's WIP branch remained, untouched by me), then redid every step inside the correct worktree using `git -C "$WORKTREE"` and absolute paths. The commit on `global-ux/wave-2-plan3-husky-hook` is the only artifact of this work; the parent worktree on `global-ux/wave-0-workflow-followup` is untouched relative to its pre-sentinel state.

No other deviations. No pivot to plain `.git/hooks/pre-commit` was needed — Husky.Net behaves correctly with the pnpm + .NET hybrid.

---

## What this enables for Plan 3

- **Local enforcement parity with the analyzer.** The SUNFISH_I18N_001 cascade Wave 2 wired (`Directory.Build.props`) is now matched by a fast local pre-commit gate. Bad RESX entries cannot reach `origin`.
- **Zero contributor friction.** `pnpm install` is the existing onboarding step; the hook installs as a side effect.
- **Forward-extensible.** Future hooks (e.g., `commit-msg` for token-discipline, `pre-push` for dotnet test) plug into the same `.husky/` directory and same `prepare` script — no new tooling required.

---

## Self-verdict: GREEN

- Branch correct (`global-ux/wave-2-plan3-husky-hook`).
- Diff-shape correct (4 files, exactly the prescribed paths).
- Build gates all passed: `dotnet tool restore` OK, `dotnet husky install` OK, `pnpm install --frozen-lockfile` OK with `prepare` firing inline.
- Empirical hook test passed: bad RESX → exit 1 with actionable diagnostic.
- Token present in commit body.
- Not pushed to remote (per brief).
- Parent worktree contamination cleanly reverted.

**Code commit SHA:** `fd299831`
**Report commit SHA:** _(separate follow-up commit — this file)_
