# ADR Projections Tool

Reads YAML frontmatter from `docs/adrs/[0-9]{4}-*.md` and emits derived projections.

The journal (`docs/adrs/`) is authoritative; projections are rebuilt from it. This is the event-sourcing-with-snapshots pattern applied to architecture documentation.

## Usage

```bash
# Validate frontmatter without writing projections
python3 tools/adr-projections/project.py --check-only --verbose

# Generate projections (writes to docs/adrs/)
python3 tools/adr-projections/project.py

# Generate + verbose validation summary
python3 tools/adr-projections/project.py --verbose
```

## Outputs

- `docs/adrs/STATUS.md` — current-state projection (by status: Proposed / Accepted / Superseded / Deprecated / Withdrawn).
- `docs/adrs/INDEX.md` — topical projection (by tier × concern).
- `docs/adrs/GRAPH.md` — dependency graph (Mermaid: composes / extends / supersedes edges).

## Schema

See `docs/adrs/_FRONTMATTER.md` for the canonical schema definition + controlled vocabularies.

## Dependencies

Pure Python 3 stdlib — no external packages. Uses a minimal hand-rolled YAML parser sufficient for the schema's subset (key/value, lists). PyYAML is **not** required (avoiding a dependency add for tooling).

## CI integration (future)

A CI job will run `--check-only` to fail builds when:

- A new ADR is added without frontmatter
- An existing ADR has invalid frontmatter (missing required fields, invalid enum values, dangling cross-references)
- A `Superseded` ADR is missing its `superseded_by` link

The projections (`STATUS.md`, `INDEX.md`, `GRAPH.md`) themselves are committed to the repo (not generated at CI time) so PR reviewers can see them in diffs.
