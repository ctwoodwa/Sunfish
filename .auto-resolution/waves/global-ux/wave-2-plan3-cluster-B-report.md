# Wave 2 — Plan 3 Cluster B Report

**Token:** `wave-2-plan3-cluster-B`
**Status:** GREEN
**Code SHA:** `8a9b55b395c55774826c62558699035bbeb44f69`
**Report SHA:** _populated post-commit_
**Branch (worktree):** `global-ux/wave-3-plan-5-implementation-plan` (see Deviations)

---

## What landed

Scaffold of `tooling/Sunfish.Tooling.LocQuality/` — Plan 3 Task 2.1's
translator-quality CLI surface. Stub-only; MADLAD MT runtime wiring is
explicitly deferred to a user-driven task per the brief.

### File list (4 new, 1 modified)

| Path | Status | Purpose |
|---|---|---|
| `tooling/Sunfish.Tooling.LocQuality/Sunfish.Tooling.LocQuality.csproj` | new | .NET 11 console SDK; mirrors `Sunfish.Tooling.LocExtraction.csproj` byte-for-byte on framework / packaging / `System.CommandLine` pin |
| `tooling/Sunfish.Tooling.LocQuality/Program.cs` | new | `System.CommandLine` root + `validate` / `quality-check` stubs + `--version` interceptor |
| `tooling/Sunfish.Tooling.LocQuality/README.md` | new | Purpose / subcommand status table / Plan 3 reference / MADLAD-deferred note |
| `Sunfish.slnx` | modified | Added new `/tooling/` solution folder containing the new project (one entry — see Deviations re: pattern) |

Total commit footprint: 4 files, +277 / −0.

---

## Build evidence

```
$ dotnet build tooling/Sunfish.Tooling.LocQuality/Sunfish.Tooling.LocQuality.csproj
  Determining projects to restore...
  Restored .../Sunfish.Tooling.LocQuality.csproj (in 163 ms).
  Sunfish.Tooling.LocQuality -> .../bin/Debug/net11.0/Sunfish.Tooling.LocQuality.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:01.07
```

Solution-wide restore (`dotnet restore Sunfish.slnx`) completed cleanly,
confirming the slnx edit registers correctly without breaking the 147-project
graph.

---

## Subcommand `dotnet run` evidence

### `--help`

```
$ dotnet run --project tooling/Sunfish.Tooling.LocQuality -- --help
Description:
  Sunfish translator-quality CLI — validates translator-context completeness
  on XLIFF inputs and (deferred) drives MADLAD-400-MT pre-publish draft
  generation. See docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-weeks-2-4-translator-assist-plan.md.

Usage:
  Sunfish.Tooling.LocQuality [command] [options]

Options:
  --version       Show version information
  -?, -h, --help  Show help and usage information

Commands:
  validate <path>       Validate translator-context completeness on the given
                        XLIFF input. Stub today; implementation lands in
                        Plan 3 Task 2.x.
  quality-check <path>  Run translator-quality heuristics against the given
                        XLIFF input and (eventually) compare against MADLAD
                        draft output. Stub today; will integrate MADLAD draft
                        generation per Plan 3 Task 3.x.
```

### `--version`

```
$ dotnet run --project tooling/Sunfish.Tooling.LocQuality -- --version
Sunfish.Tooling.LocQuality v0.1.0-scaffold
```

(Matches brief specification exactly.)

### `validate /tmp`

```
$ dotnet run --project tooling/Sunfish.Tooling.LocQuality -- validate /tmp
[scaffold-stub] LocQuality validate not yet implemented; tracked at Plan 3
task 2.x. (received path=C:/Users/Chris/AppData/Local/Temp)
EXIT=0
```

(`/tmp` resolves to the Windows temp path on this host; behaviour is correct.)

### `quality-check /tmp`

```
$ dotnet run --project tooling/Sunfish.Tooling.LocQuality -- quality-check /tmp
[scaffold-stub] LocQuality quality-check not yet implemented; will integrate
MADLAD draft generation per Plan 3 task 3.x. (received path=C:/Users/Chris/AppData/Local/Temp)
EXIT=0
```

All four brief-mandated build-gate checks pass.

---

## Design notes

1. **Mirrored the LocExtraction pattern exactly.** Same `<TargetFramework>`,
   same `<PackAsTool>`, same `DevelopmentDependency` flag, same
   `System.CommandLine` `VersionOverride="2.0.0-beta4.22272.1"` pin. The pin
   is byte-for-byte identical to avoid restore-graph drift between sister
   tools — when the eventual Directory.Packages.props promotion happens, both
   tools migrate together.
2. **Stub exit code = 0, not 70.** Brief specifies stubs exit 0 with stub
   message. This contrasts with LocExtraction stubs (which exit 70). Honoured
   the brief; documented the contrast in the README so the contract flip
   to 70 is intentional once implementations land.
3. **`--version` interceptor.** `System.CommandLine` 2.0.0-beta4 surfaces
   `--version` automatically when the .csproj has a `<Version>` set, but its
   default format ("AssemblyName Major.Minor.Patch") doesn't match the brief's
   exact required string ("Sunfish.Tooling.LocQuality v0.1.0-scaffold").
   Intercepted `--version` / `-v` in `Main` before `InvokeAsync` to honour the
   exact contract.
4. **`ToolCommandName=sunfish-locquality`.** Sister to LocExtraction's
   `sunfish-loc`. Two-word disambiguation prevents clash and keeps both tools
   independently installable as `dotnet tool install`s.

---

## Deviations

1. **Branch name.** Brief specified
   `global-ux/wave-2-plan3-locquality-clusterB`; the worktree was
   pre-provisioned on `global-ux/wave-3-plan-5-implementation-plan`. Did not
   rename because the parent agent's worktree-tracking may rely on the
   provisioned branch name; renaming mid-flight is a higher risk than the
   naming mismatch. Brief commit token (`wave-2-plan3-cluster-B`) is honoured
   in the commit message and report filename, which is the actual identity
   anchor. No push happened, so the parent agent can rename or rebase as
   needed.
2. **slnx pattern.** Brief said "find `<Project>` entries for `tooling/*`; add
   a new entry for LocQuality matching the existing pattern." There are
   currently **zero** tooling/* entries in `Sunfish.slnx` (verified with
   `Grep tooling/`). The other three tooling projects (`LocExtraction`,
   `ColorAudit`, `LocalizationXliff`) are not registered in the slnx today.
   Took the minimal-impact extension: created a new `<Folder Name="/tooling/">`
   block following the slnx folder convention used everywhere else, and
   registered LocQuality as its sole entry. The other tooling projects
   remain unregistered (out of scope for this brief — only the LocQuality
   entry was permitted per "ONE PERMITTED .csproj/.slnx EDIT" rule).
3. **No other tooling projects added to slnx.** As above, this respected
   the "ONE PERMITTED EDIT" constraint. A follow-up housekeeping commit on
   a separate branch could register the other three.

---

## Diff-shape verification

```
$ git show --stat HEAD
 Sunfish.slnx                                                                |   3 +
 tooling/Sunfish.Tooling.LocQuality/Program.cs                               | 116 +++++++++++++++++++++++++++
 tooling/Sunfish.Tooling.LocQuality/README.md                                |  78 ++++++++++++++++++
 tooling/Sunfish.Tooling.LocQuality/Sunfish.Tooling.LocQuality.csproj        |  60 +++++++++++++
 4 files changed, 277 insertions(+)
```

Touched ONLY `tooling/Sunfish.Tooling.LocQuality/*` and `Sunfish.slnx`.
Zero touches to packages, tests, other tooling, .wolf/, or waves/ in the
code commit.

---

## Verdict

**GREEN** — all build-gate checks pass; commit is path-scoped per brief; no
push performed; deviations are minor and documented.
