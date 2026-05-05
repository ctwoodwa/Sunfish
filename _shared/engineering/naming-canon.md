# Sunfish naming canon

**Status**: living document. Updated as conventions are established or vocabulary is locked. The companion `tools/naming/check.py` is the machine-readable view of this same intent.

This doc + `_shared/engineering/naming-registry.yaml` together form the canon. AI sessions should consult both BEFORE proposing any new name.

---

## Why this exists

Naming friction has been a recurring source of cohort-quality issues. Specific incidents:

- **W#24 Assets first-slice collision** (2026-04-28): proposed `blocks-assets` already existed as a UI-only catalog package. Resolved by renaming to `blocks-property-assets` + adopting `blocks-property-*` cluster prefix. Cost: ~1 hr re-work + a hand-off rewrite.
- **ADR 0073 frontmatter drift** (2026-05-04): used `adr:` instead of `id:`, `concerns:` instead of `concern:`, `pipeline-variant:` instead of `pipeline_variant:` — silently broke the projection tool. Cost: 2 PR rebases + a follow-up frontmatter-fix PR.
- **ADR 0028 amendment number drift** (2026-05-04): I told a subagent "use Amendment A9" but A9 + A10 were already taken; subagent's §A0 negative-existence check caught this and corrected to A11. The cohort discipline saved the work.
- **Wayfinder brainstorm rejected 8 candidates** (2026-05-01): Mission Control / Capability Center / Control Center / Bridge / Cockpit / Flight Deck / Flight Plan / Trade Space / Chart Table — each had collisions with macOS UI, Sunfish accelerators, prior ADRs, or commerce vocabulary.
- **Naval-org "Yeoman" collision** (2026-05-01): Sunfish-side "Yeoman" role would have collided with the book-side technical-writer "Yeoman" session. Renamed Sunfish-side to "Scribe."
- **ADR 0066 parallel-session collision** (2026-05-04): two parallel sessions both reached for ADR 0066 on the same day — XO (Helm + Identity Atlas, intake filed 2026-05-01) and ONR (Crew Comms — foundation-channels, drafted 2026-05-04). Resolved by first-claim discipline: Helm + Identity Atlas kept 0066 (3-day-prior intake); Crew Comms renumbered to 0076. **Reinforces**: cross-session naming requires registry discipline AND multi-session ADR-number-claim broadcasts. Future cross-session work should consult `naming-registry.yaml § reserved_adrs` BEFORE drafting (registry was already populated; ONR just didn't read it).
- **W#54 + W#55 parallel-session collisions** (2026-05-05): two parallel XO author subagents independently allocated W#54 (Sick Bay, PR #601) and W#55 (Ship's Office, PR #603 + Bridge React renderer, PR #602) on the same day. Root cause: no atomic check for "is W#NN already claimed in main OR an open PR." Resolved by re-creating PR #601's ledger orphan row and renumbering #602 → W#56. Cost: 2 redo-PRs. **Fix shipped**: `tools/naming/check.py workstream <NN>` + `next-workstream` now check both disk state AND open PRs atomically.

Each of these wasted 30 min – 2 hr. The pattern is clear: **propose-then-discover** is expensive; **search-then-propose** is cheap. **Multi-session caveat**: each session must also broadcast its claims to the registry, not just consult it.

## How to use this canon

When proposing a new name (any kind):

1. **Run `tools/naming/check.py`** first. It's <100 ms; no excuse to skip.
2. **Read the relevant section below** for the category you're naming.
3. **If the name passes both gates**, propose it.
4. **If a brainstorm rejects a candidate**, add it to `naming-registry.yaml` under `rejected_vocabulary` with a one-line reason. Future sessions won't re-propose.

```bash
# Quick checks
tools/naming/check.py adr 76                  # is ADR number 76 available?
tools/naming/check.py adr-amendment 28 A12    # is ADR 0028 amendment A12 available?
tools/naming/check.py package blocks-foo      # is package name blocks-foo available?
tools/naming/check.py namespace Sunfish.X.Y   # is C# namespace available?
tools/naming/check.py vocabulary "MyTerm"     # is vocabulary term reserved/rejected?
tools/naming/check.py auto Sunfish.Foo.Bar    # auto-detect what kind & check
tools/naming/check.py workstream 57           # is W#57 free? (checks disk + open PRs)
tools/naming/check.py next-workstream         # what is the next-available W# integer?
tools/naming/check.py W57                     # auto-detect W# shape; same as `workstream 57`
```

### Tool limitation: in-flight PRs (ADR names and general names)

`check.py` reflects the **on-disk origin/main state** for most checks (ADR numbers, packages, namespaces, vocabulary). If a name is reserved by an open PR (e.g., a new ADR amendment authored in a branch but not yet merged), the tool reports CLEAN. To check in-flight reservations for those categories, also run:

```bash
gh pr list --state open --search "in:title <candidate-name>"
```

When in doubt, search the in-flight workstream ledger at `icm/_state/active-workstreams.md` and the open PR list.

**Exception — workstream numbers**: the `workstream` and `next-workstream` subcommands DO check open PRs automatically (via `gh pr list --json number,files`). No manual PR scan needed for W# allocation.

---

## Naming conventions by category

### ADR numbers

- 4-digit zero-padded: `0001` through `0099` so far.
- Gap-free chronological numbering — first available; don't skip for aesthetic reasons.
- **Reserved tentative numbers** (intake stub filed; not yet authored): see `naming-registry.yaml § reserved_adrs`. As of 2026-05-04: 0066, 0067, 0068.
- ADR amendments: `A1`, `A2`, `A11` — gap-free per parent ADR. Next-available is `max(existing) + 1`. Sub-numbers (`A1.2`, `A2.5`) are sub-bullets within a single amendment, not separate amendments.

### Package directories (`packages/`)

- Lowercase, kebab-case.
- Cluster prefixes (see `naming-registry.yaml § cluster_conventions`):
  - `foundation-*` — framework-agnostic contracts (`Sunfish.Foundation.*` namespace)
  - `kernel-*` — runtime substrates (`Sunfish.Kernel.*` namespace)
  - `blocks-*` — composition layer
  - `blocks-property-*` — property operations cluster (established 2026-04-28 after W#24 collision)
  - `ui-adapters-*` — UI adapter layers
  - `compat-*` — vendor-compatibility layers (`Sunfish.Compat.*` namespace)
  - `providers-mesh-*` — mesh-VPN providers
- For new clusters: pick a prefix that won't collide with existing prefixes; document in registry.

### C# namespaces

- PascalCase; period-separated.
- Top level: `Sunfish.<Tier>.<Component>`
- Tier vocabulary: `Foundation`, `Kernel`, `UI`, `Bridge`, `Anchor`, `Compat`.
- Within a package: namespace reflects directory structure where reasonable.
- See `_shared/engineering/coding-standards.md` for the full convention.

### ADR YAML frontmatter fields

These are the **exact** field names per `docs/adrs/_FRONTMATTER.md` (the schema). Drift breaks the projection tool:

| Field | NOT |
|---|---|
| `id:` (integer) | NOT `adr:` |
| `concern:` (singular; list) | NOT `concerns:` (plural) |
| `pipeline_variant:` (underscore) | NOT `pipeline-variant:` (hyphen) |
| `composes:` (integer list) | NOT `composes_adrs:` |
| `superseded_by:` (single int or null) | NOT `superseded-by:` |

Run `python3 tools/adr-projections/project.py --check-only` after authoring; 0 errors required before commit.

### Workstream numbers

- Sequential integers; one per-workstream file at `icm/_state/workstreams/W{NN}-{slug}.md`.
- Files are the source of truth; `icm/_state/active-workstreams.md` is a regenerated roll-up.
- **Pre-flight check is mandatory before allocating a new W#** — parallel author sessions
  independently picking "next available" caused 2 collisions on 2026-05-05 (W#54 + W#55).

```bash
# Allocate the next free number
python3 tools/naming/check.py next-workstream    # → "NEXT WORKSTREAM: W#57"

# Verify a specific number before using it
python3 tools/naming/check.py workstream 57      # → EXACT MATCH / RESERVED / CLEAN

# Auto-detect shorthand (W56, W#56, w56 all work)
python3 tools/naming/check.py W57
```

The `workstream` and `next-workstream` subcommands check **both** the local disk checkout
(EXACT MATCH) **and** open GitHub PRs (RESERVED) in a single call. No separate `gh pr list`
scan is needed for W# allocation. Requires the `gh` CLI; if unavailable a warning is printed
and only the disk check runs.

- Sub-workstreams: not used yet; if needed, propose via ADR.

### Locked Sunfish vocabulary

Canonical names. Don't reuse for unrelated concepts. See `naming-registry.yaml § locked_vocabulary` for the authoritative list with definitions.

Highlights:
- **Wayfinder / Helm / Atlas / Standing Order** — configuration system (ADR 0065)
- **Mission Space / Mission Envelope** — capability dimensions (ADR 0062)
- **Anchor / Bridge** — accelerator names (ADRs 0031, 0032)
- **Quarterdeck / Engine Room / Tactical / Sick Bay / Ship's Office** — Ship Architecture locations (W#35)
- **CO / XO / COB / PAO / Yeoman** — multi-session roles (ADR 0070)

### Rejected vocabulary

Don't re-propose. See `naming-registry.yaml § rejected_vocabulary` for the full list with reasons. As of 2026-05-04: 17 rejected names.

If a brainstorm session re-raises one of these, point at the registry's `rejected_in:` field and continue.

---

## Workflow for proposing a brand-new name

1. **Frame the concept in 1-2 sentences.** What does it represent? What's its scope?
2. **Brainstorm 3-5 candidates.** Mix of: descriptive, metaphorical, naval (per Sunfish convention), short-and-punchy.
3. **For each candidate, run `check.py`** + grep for industry collisions (Google search if internet available; otherwise rely on registry).
4. **Eliminate collisions.** Note WHY each rejected candidate was rejected; this becomes registry input.
5. **Pick the winner.** Document the choice + rejected alternatives in the relevant ADR or intake doc.
6. **Update the registry**:
   - If the name becomes locked vocabulary → add to `locked_vocabulary`
   - Add rejected candidates to `rejected_vocabulary` with reasons
   - If a new cluster convention emerges → add to `cluster_conventions`

---

## Cohort lessons

- **First-time names get §A0'd**: any new name in an ADR's body should be in §A0.1 (introduced by this ADR) or §A0.2 (verified existing). The cohort batting average shows ~65% of structural-citation failures are caught by council, NOT §A0 — assume your first naming attempt is wrong-in-some-way until verified.
- **Naming brainstorms cost more than naming registries**: the time spent rejecting "Mission Control" 3 times across 3 brainstorms is more than the time to maintain the registry.
- **Rejected names are valuable signal**: future contributors (human + AI) will independently arrive at the same names. The registry preserves the institutional memory of why we said no.

---

## Cross-references

- `_shared/engineering/naming-registry.yaml` — machine-readable registry
- `tools/naming/check.py` — collision-check CLI
- `_shared/engineering/coding-standards.md` — broader code-style canon
- `docs/adrs/0070-multi-session-naval-org-structure.md` — naval-org role names (CO/XO/COB/PAO/Yeoman)
- `docs/adrs/0069-adr-authoring-discipline.md` — pre-merge council canonical (catches naming drift among other things)
- `docs/adrs/_FRONTMATTER.md` — ADR frontmatter schema (field name authority)
- `tools/adr-projections/project.py` — ADR projection tool (validates frontmatter conventions)

---

**Living document**: append-only in spirit (rejected names stay rejected; reasons accumulate). When a convention is broken intentionally, document the exception here with rationale and date.
