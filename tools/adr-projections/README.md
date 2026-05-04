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

## Semantic search (companion tool)

`embed_search.py` adds **semantic search over the ADR portfolio** via local Ollama embeddings (default model: `nomic-embed-text`; 768-dim; ~274 MB; runs locally).

```bash
# (Re)build the embedding index — incremental; only re-embeds changed ADRs
python3 tools/adr-projections/embed_search.py index

# Search top-5 by cosine similarity
python3 tools/adr-projections/embed_search.py search "feature gate evaluation"

# Top-N
python3 tools/adr-projections/embed_search.py search "audit trail" --top 10

# Collapse to unique ADRs (best chunk per ADR)
python3 tools/adr-projections/embed_search.py search "audit trail" --top 5 --collapse

# Use a remote Ollama (e.g., Windows GPU box)
python3 tools/adr-projections/embed_search.py search "..." --ollama http://desktop-umt08rn:11434
```

**Index location**: `tools/adr-projections/.embeddings.json` — content-hash-keyed; incremental rebuild.

**What gets embedded** (format v2; bumped 2026-05-04):
- Each ADR's main body (frontmatter + first ~800 words)
- **Each amendment as a separate chunk** (e.g., ADR 0028 A1, A2, ..., A11 are 11 separate entries)

The amendment chunking captures content that the original v1 indexer missed (anything past the 800-word body budget). Coverage at 2026-05-04: **67 ADRs + 85 amendment chunks = 152 total entries**. Roughly 2.3× the index size of v1.

Index entries have `chunk_id` (e.g., `"28"` for main body or `"28-A8"` for amendment), `adr_id`, and `amendment` fields. Search results include the amendment label when relevant; use `--collapse` to dedupe to unique ADRs.

**When to rebuild**: after adding a new ADR or amendment, or when frontmatter changes. Incremental rebuild via content-hash; unchanged chunks are fast (<1 sec for a no-op rebuild). Format-version mismatch triggers full rebuild.

**Ollama prerequisite**: `ollama pull nomic-embed-text` (one-time; ~274 MB). The tool fails gracefully if Ollama is unreachable.

## CI integration (future)

A CI job will run `--check-only` to fail builds when:

- A new ADR is added without frontmatter
- An existing ADR has invalid frontmatter (missing required fields, invalid enum values, dangling cross-references)
- A `Superseded` ADR is missing its `superseded_by` link

The projections (`STATUS.md`, `INDEX.md`, `GRAPH.md`) themselves are committed to the repo (not generated at CI time) so PR reviewers can see them in diffs.
