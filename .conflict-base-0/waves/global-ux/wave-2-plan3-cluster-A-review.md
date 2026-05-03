# Wave 2 Review — Plan 3 Husky hook sentinel

**Date:** 2026-04-25
**Code commit:** fd299831
**Report commit:** 65e0faa2
**Branch:** global-ux/wave-2-plan3-husky-hook
**Reviewer:** Wave 2 reviewer (Cluster A)

---

## Per-criterion results

### (a) Diff shape — PASS
`git show --name-only --pretty=format:"" fd299831 | sort` returns exactly the four prescribed paths and nothing else:

```
.config/dotnet-tools.json
.husky/.gitignore
.husky/pre-commit
package.json
```

`.config/dotnet-tools.json` and `package.json` are modifications; `.husky/pre-commit` and `.husky/.gitignore` are new files. Matches the brief's allow-list with no extras.

### (b) Husky tool entry — PASS
`git show fd299831:.config/dotnet-tools.json` contains:

```json
"husky": {
  "version": "0.9.1",
  "commands": [ "husky" ],
  "rollForward": false
}
```

Correctly registered as a local dotnet tool alongside the pre-existing `docfx` entry. The incidental addition of `rollForward: false` to the `docfx` entry is a known side-effect of `dotnet tool install` rewriting the manifest — harmless and documented in the report.

### (c) `prepare` script — PASS
`git show fd299831:package.json` shows the exact required form:

```json
"prepare": "dotnet tool restore && dotnet husky install",
```

Sequenced correctly (restore before install) and uses `&&` so a failed restore aborts the install. Will fire on any contributor's first `pnpm/npm install`.

### (d) `.husky/pre-commit` script — PASS (with notes below)
Reading `git show fd299831:.husky/pre-commit` confirms all five required behaviours:

1. Lists staged `.resx` via `git diff --cached --name-only --diff-filter=ACMR | grep '\.resx$'` — exact match to spec, with `|| true` to survive empty grep results under `set -e`.
2. `find_csproj()` walks parent directories from `dirname "$resx"` until it finds a sibling `*.csproj`, using `ls "$dir"/*.csproj | head -n 1`.
3. Runs `dotnet build "$csproj" --nologo --verbosity quiet` for each unique csproj (deduplicated via case/glob).
4. Greps build output for `SUNFISH_I18N_001`.
5. Exits non-zero with a clear remediation message referencing the missing `<comment>` child and citing the loc-comments contract.

Edge-case notes are in the script-analysis section below.

### (e) `.husky/.gitignore` — PASS
`git show fd299831:.husky/.gitignore` contents are exactly `_/` — Husky.Net's framework-cache directory, correctly excluded from version control per Husky's own guidance.

### (f) Commit message token — PASS
Subject line: `feat(tooling): wave-2-plan3-husky-sentinel — Husky pre-commit hook for SUNFISH_I18N_001`. Body also includes a bare `Token: wave-2-plan3-husky-sentinel` line. Matches required token verbatim.

### (g) `task-runner.json` pivot — PASS
The sentinel correctly identified that `dotnet husky install` auto-generates `.husky/task-runner.json` and chose to leave it untracked because (i) the brief's diff-shape constraint forbade it and (ii) the pre-commit hook invokes `dotnet build` directly without going through Husky's task runner. Verified by reading `.husky/pre-commit` — there is no `dotnet husky run …` invocation, so the task runner is never needed at hook execution time. Each contributor's local `prepare` regenerates the file. Pivot is sound and explicitly scoped as out-of-band for this commit.

### (h) Worktree contamination — PASS
`git status --short` in the parent worktree returns:

```
 M .wolf/anatomy.md
 M .wolf/buglog.json
 M .wolf/memory.md
```

These are exactly the user's pre-existing OpenWolf working-tree modifications (matches the conversation-start git status, which already showed `.wolf/memory.md` modified; the other two are siblings under the same OpenWolf-managed directory and are part of the user's WIP, not the sentinel's dispatch). No source files, packages, or other repository paths show drift. The sentinel's self-correction note (early `cd C:/Projects/sunfish` mishap, fully reverted, redone via `git -C "$WORKTREE"`) is corroborated by the clean status.

---

## Husky hook script analysis

The script is small (76 LOC), POSIX-sh portable, and handles the common cases correctly. Edge-case evaluation:

- **No staged `.resx`:** Early-return `exit 0` after the empty `STAGED_RESX` check — fast no-op for non-i18n commits. Good.
- **Staged `.resx` with no enclosing `.csproj`:** `find_csproj` returns non-zero; the loop emits `[husky] WARN: no .csproj found above $resx — skipping` and continues. After the loop, if `CSPROJS` is still empty the script `exit 0`s. Reasonable for orphan/test fixture RESX files; will silently pass them, which is acceptable because there's no analyzer to run. Minor suggestion (non-blocking): consider promoting this to a hard failure once the repo's RESX layout stabilizes — an orphan `.resx` is almost always a mistake.
- **Multiple `.csproj` at the same level:** `ls "$dir"/*.csproj | head -n 1` picks the alphabetically first one. Sunfish's package layout has one `.csproj` per directory, so this is fine in practice. Worth a follow-up if any package ever ships paired projects (e.g., `Foo.csproj` + `Foo.Tests.csproj` co-located).
- **Same `.csproj` referenced by multiple staged `.resx`:** Deduplicated via the `case " $CSPROJS " in *" $csproj "*` membership check before append — avoids redundant builds. Good.
- **Build failure unrelated to SUNFISH_I18N_001:** `dotnet build … || true` captures stderr+stdout into `BUILD_OUTPUT`, then greps only for the analyzer code. A genuine build break (e.g., a C# syntax error in the same package) would be silently swallowed because the grep is scoped to the diagnostic ID. Minor weakness — a follow-up could surface non-zero exit codes from `dotnet build` separately. Not a blocker because (i) other CI gates catch hard build breaks and (ii) the goal here is specifically the i18n cascade.
- **Windows / Git-Bash compatibility:** Uses `#!/bin/sh` and `. "$(dirname "$0")/_/husky.sh"`, both Husky-supported on Windows via Git Bash. The script avoids Bashisms. Confirmed working in the sentinel's bad-RESX test on Windows.
- **`set -e` discipline:** Combined with `|| true` on grep/find calls that legitimately return non-zero. Correct pattern.

The remediation message includes the offending `.resx` path, line number, key name, and (via the analyzer's own diagnostic format) a link to the docs. Developer feedback quality is high.

---

## Final verdict: GREEN
