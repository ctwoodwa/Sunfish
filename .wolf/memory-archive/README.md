# `.wolf/memory-archive/` — Two-Tier Memory Policy

This directory holds **Tier 2 (warm) memory** — weekly summaries derived from `.wolf/memory.md` (the Tier 1 hot action log) plus authoritative sources (`git log`, ADR Status changes, ICM ledger transitions).

## Two-tier model

| Tier | File(s) | Cadence | Granularity | Content kept |
|---|---|---|---|---|
| **Tier 1 (hot)** | `.wolf/memory.md` | Auto-appended by OpenWolf hooks per session; rolling **7-day** window | Per-edit timestamps, file-level granularity, token counts | Verbatim action log |
| **Tier 2 (warm)** | `.wolf/memory-archive/YYYY-WNN.md` | Generated weekly (ISO 8601 week number) when Tier 1 ages out content > 7 days | Per-week thematic summary | **Decisions** (ADR Status flips, PR merges that change architecture, conventions adopted) + **Non-obvious outcomes** (halts, reversals, surprises, root-causes from buglog) |

## Consolidation policy

When `.wolf/memory.md` contains entries older than **7 days** (rolling), the consolidation daemon (or a manual run) MUST:

1. **Group** all sessions in the > 7-day region by ISO week (Mon–Sun).
2. **Generate** a `YYYY-WNN.md` summary file in `.wolf/memory-archive/` per week, deriving content from:
   - `git log --since=<week-start> --until=<week-end>` (authoritative decision log)
   - ADR Status flips (`grep -E "^\*\*Status:\*\*" docs/adrs/*.md` cross-referenced with `git log --since=`)
   - ICM ledger transitions (`git log --since= -- icm/_state/active-workstreams.md`)
   - The original `.wolf/memory.md` entries — but only entries that match these classifications:
     - "Task N complete" lines (decision summaries)
     - Lines containing commit SHAs (committed work)
     - Halts, errors, reversals (non-obvious outcomes)
3. **Drop** these from each weekly summary (high-volume noise):
   - Routine `Edited <file>` log lines without a commit reference
   - `Session end: N writes M reads` token-counter lines
   - Repeated session-end summaries from prior writes
   - Mechanical refactor edit lines (renames, reformats) — `git log` preserves these
4. **Trim** `.wolf/memory.md` to remove the consolidated content; leave a note at the top pointing at the archive file(s).

## What survives consolidation (weekly summary content)

Each weekly summary file contains:

- **Header:** week range (Mon–Sun) + commit count + sessions count
- **Major decisions** — 3–7 bullets summarizing architectural / convention / scope decisions (linked to ADR + PR + ledger row where applicable)
- **Non-obvious outcomes** — 3–5 bullets covering halts, reversals, surprises, dead-ends; each includes the resolution
- **State at week-end** — 1–2 bullets snapshot (which workstreams are `built` / `ready-to-build` / `design-in-flight`; ADR set status)
- **Pointer to authoritative sources** — `git log --since=<week-start> --until=<week-end>` for raw detail; `icm/_state/active-workstreams.md` for ledger snapshot

## What gets dropped (intentionally)

- **Routine file edits** — preserved in `git log`; redundant in memory log
- **Token-count summaries** — operational metric; not project memory
- **Read-without-write entries** — observation, not state change
- **Re-runs of identical session-end summaries** (artifact of hook re-firing)
- **Mechanical reformatting / lint fixes** — preserved in `git log`; not a decision
- **Inline file-content diffs** — preserved in `git diff` per commit

## How to run consolidation manually

```bash
# 1. Identify weeks to archive (oldest week with content >7d ago)
WEEK=2026-W17

# 2. Identify week's date range (ISO 8601)
START=$(date -d "$WEEK-1" +%Y-%m-%d)        # Monday of W17
END=$(date -d "$WEEK-7" +%Y-%m-%d)          # Sunday of W17

# 3. Generate summary inputs
git log --since="$START" --until="$END" --pretty=format:"%h %ad %s" --date=short > /tmp/${WEEK}-commits.txt
git log --since="$START" --until="$END" -- docs/adrs/ > /tmp/${WEEK}-adr-changes.txt
git log --since="$START" --until="$END" -- icm/_state/active-workstreams.md > /tmp/${WEEK}-ledger.txt

# 4. Author .wolf/memory-archive/${WEEK}.md with the synthesized themes
#    (manual or daemon-assisted; per the §"What survives consolidation" template above)

# 5. Trim .wolf/memory.md to drop sessions in [$START..$END]
#    (preserve the file; just delete the consolidated session blocks)

# 6. Update top of .wolf/memory.md to reference the new archive file
```

## Daemon hook (future)

A scheduled task should run weekly (e.g., Mondays at 04:00 local) to:

1. Compute "oldest week with content > 7 days ago" from `memory.md`
2. Generate the summary per the spec above
3. Trim `memory.md`
4. Commit both changes via the established `chore(openwolf): weekly memory consolidation` pattern

This README documents the policy; the daemon implementation lives in `.wolf/hooks/` (not yet authored — see `.wolf/cron-manifest.json` for existing scheduled hooks).

## File naming convention

ISO 8601 week numbering: `YYYY-WNN.md` where:
- `YYYY` = 4-digit year (use the year that contains the **Thursday** of that week per ISO 8601)
- `WNN` = 2-digit week number, zero-padded (`W01` through `W53`)

Examples:
- `2026-W01.md` — week containing 2026-01-01 (Thursday)
- `2026-W17.md` — Apr 20–26
- `2026-W53.md` — only exists in years with 53 ISO weeks

## Why two-tier (instead of one big log or no log)

- **One big log** — `memory.md` grows ~26KB/day; would hit 1MB by midyear; loads at every session start; signal-to-noise drops as bulk grows
- **No log** — loses cross-session context for conversations that span weeks; loses non-obvious outcomes (halts, reversals) that aren't in `git log`
- **Two-tier** — recent week's verbatim log stays cheap-to-load (≤50KB target); older weeks compress to ~30-60-line summaries; total archive footprint stays bounded; signal-to-noise stays high in both tiers

Inspired by [time-series database hot/warm/cold tier patterns](https://www.elastic.co/guide/en/elasticsearch/reference/current/data-tiers.html); same separation between high-frequency ingest (hot) and low-frequency analytics (warm).
