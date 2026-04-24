# Global-First UX — Decisions Log

Append-only. New entries at the top. Older entries at the bottom.

---

## 2026-04-24 — Shared-worktree race when dispatching parallel subagents (LESSON)

**Triggering condition:** Executing Plan 2 Tasks 1.1-1.3 in foreground while a background
subagent was dispatched for Plan 3 Task 1.1. The subagent ran `git checkout -b` to switch
to its own branch — this switched HEAD globally in the shared working tree, so my
foreground commits landed on the subagent's branch (`global-ux/plan3-madlad-cli`) rather
than the intended `global-ux/plan2-xliff`.

**Recovery:** `git branch -f plan2-xliff <my-commits-tip>` + `git rebase --onto main
<my-commits-tip> plan3-madlad-cli` to cleanly separate the branch histories. No commits
lost, no force-push required (nothing was pushed).

**Chosen alternative for future parallel execution:**
1. **Preferred:** Use `git worktree add ../sunfish-plan3 main` so each parallel workstream
   has its own working directory pointed at the same `.git` directory. Subagent operates
   in the worktree; parent Claude keeps the main working directory.
2. **Fallback:** Run parallel workstreams serially when worktree setup is not feasible
   (e.g., when only one tool can hold the dotnet package-manager lock at a time).

**Do NOT:** Dispatch background subagents that execute `git checkout` or `git switch` in
the shared working tree. The task-notification model assumes agents are isolated; in
practice git working tree state is shared and must be coordinated.

**Downstream impact:**
- Plans 4 / 4B / 5 execution waves must use `git worktree` for any subagent that needs
  to land commits in parallel.
- Plans 3 / 6 execution agents that only modify their own package-scoped files can run
  in parallel IFF their commit happens via a worktree or after the foreground is done.

---

## 2026-04-25 — ICU4N → SmartFormat.NET + .NET 8 System.Globalization (PIVOT)

**Triggering condition:** Week-0 triage gate (spec Section 3A) on ICU4N health.
[Memo](../../icm/01_discovery/output/icu4n-health-check-2026-04-25.md) finding:
ICU4N is maintained but ports ICU 60 (Oct 2017) with CLDR 32. Latest CLDR is 48.2
(Mar 2026) — 16 major versions behind. Arabic/Hindi plural rules materially stale;
MessageFormat 2.0 absent from roadmap; single-maintainer bus factor; 439 pre-release
alphas with no v1.x in 8 years.

**Original spec position:** Adopt ICU4N as the CLDR / ICU MessageFormat implementation
behind `IStringLocalizer<T>`.

**Chosen alternative — two-layer strategy:**
- **Plural/select/message logic:** [SmartFormat.NET](https://github.com/axuno/SmartFormat)
  (MIT, actively maintained, CLDR plural rules kept current). Supports ICU-style
  `{count, plural, one{...} other{...}}` syntax. Wired behind `IStringLocalizer<T>`.
- **Number/date/currency formatting:** .NET 8+ `System.Globalization` in ICU mode
  (`DOTNET_SYSTEM_GLOBALIZATION_USENLS=false`; bundles ICU 74+).

**Downstream impact:**
- Task 14 (ICU4N wrapper scaffold) — wrapper interface stays, implementation pivots
  to SmartFormat.NET. Rename `SunfishLocalizer` implementation class if useful, but
  the public `ISunfishLocalizer` contract is unchanged.
- Task 15 (en/ar/ja smoke tests) — test SmartFormat.NET behavior rather than ICU4N.
- Spec Section 3A needs a revision note pointing to this decisions.md entry; the
  ICU4N → SmartFormat swap does not change the broader Section 3A architecture.
- If transliteration becomes needed in a later workstream, ICU4N may be adopted as
  an optional, feature-gated dependency only (not the core i18n foundation).

**Confidence:** High. Three independent evidence lines converge (GitHub API confirms
`v60.1.0-alpha.*` release train; ICU 60 release notes confirm CLDR 32 bundling;
upstream CLDR 48.2 release confirms the gap).

---

*(Decisions are appended here as rollback criteria trigger or tool choices pivot.)*
