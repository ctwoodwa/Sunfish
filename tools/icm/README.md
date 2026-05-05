# tools/icm/

ICM tooling for the workstream ledger.

## Files

- **`render-ledger.py`** — regenerates `icm/_state/active-workstreams.md`
  from per-workstream files at `icm/_state/workstreams/W*.md`.  Use after
  any state change.  CI runs `--check` mode on every PR.
- **`migrate-ledger.py`** — one-shot migration that split the original
  monolithic `active-workstreams.md` into per-workstream files.  Retained
  for reference; should not need to run again.

## Why

Before this split, every state-flip / row-update / new-workstream
addition produced a PR-level conflict on `icm/_state/active-workstreams.md`.
With ~5 parallel sessions (XO, COB, ONR, sometimes PAO + Yeoman) all
editing the same Markdown file, ~5–7 forced rebases per day were the
norm.  Per-workstream files let parallel edits land independently; only
the regenerated roll-up table is a shared write-target, and it's a
mechanical artifact (regen-on-merge) instead of a hand-edited file.

## How to add a new workstream

1. Pick the next available `W#` (search `ls icm/_state/workstreams/W*.md`
   for the highest number; pick that + 1).
2. Create `icm/_state/workstreams/W{NN}-{slug}.md` with this shape:
   ```yaml
   ---
   sort_order: <next available; usually max(existing sort_order) + 1>
   number: <NN>
   slug: <kebab-case slug; matches filename>
   title: "<title cell text; may include markdown emphasis + code spans>"
   status: "design-in-flight"  # or ready-to-build / building / built / held / blocked / superseded
   status_cell: "`design-in-flight` (free-text qualifier)"
   owner: "research"  # or sunfish-PM / pao / yeoman / onr
   owner_cell: "research (XO)"
   reference_cell: "<intake / ADR / hand-off paths, '+' separated>"
   ---

   ## Notes

   <free-text context — preserved verbatim under the original Notes column>
   ```
3. Run `python3 tools/icm/render-ledger.py`.
4. Commit BOTH the new `W{NN}-*.md` file AND the regenerated
   `icm/_state/active-workstreams.md`.

## How to flip a workstream's status

1. Edit `icm/_state/workstreams/W{NN}-*.md`: update `status:` AND
   `status_cell:` (the cell text shown in the regen'd table; usually
   includes the canonical token in backticks plus a free-text qualifier).
   Update `## Notes` body if relevant.
2. Run `python3 tools/icm/render-ledger.py`.
3. Commit both files.

## CI

`.github/workflows/ledger-check.yml` runs `python3 tools/icm/render-ledger.py
--check` on every PR that touches `icm/_state/active-workstreams.md`,
`icm/_state/workstreams/`, or `tools/icm/`.  Fails with a unified diff if
the committed roll-up does not match the regen output (catches direct
hand-edits to the generated file).

## Frontmatter notes

- `sort_order` is the original row position in the pre-migration ledger.
  New rows pick the next free integer.  This keeps the regen output
  byte-stable and lets us preserve original ordering even when `W#`
  numbers don't sort cleanly (the pre-migration ledger had two parallel
  sets of `W#34/35/36/37` rows from a parallel-branch numbering split).
- `status_cell` and `owner_cell` are the **rendered cell** text — what
  shows up in the table.  `status` and `owner` are the canonical
  programmatic tokens (extracted by `migrate-ledger.py` from the cell
  text); they're convenient for downstream tooling that wants a clean
  enum but never feed back into the rendered table.
- The `## Notes` body is preserved verbatim and rendered as the table's
  Notes cell.  Markdown formatting (bold, code spans, links) round-trips
  cleanly.

## Related

- `icm/_state/handoffs/` — Stage-06 hand-off specs (research → sunfish-PM).
- `tools/adr-projections/project.py` — adjacent journal-and-projections
  pattern; same idiom (markdown source-of-truth + regenerated read-models).
