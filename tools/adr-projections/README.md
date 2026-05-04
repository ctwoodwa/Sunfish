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

# Use a remote Ollama (e.g., Windows GPU box)
python3 tools/adr-projections/embed_search.py search "..." --ollama http://desktop-umt08rn:11434
```

**Index location**: `tools/adr-projections/.embeddings.json` (one row per ADR; cached by content hash; incremental rebuild)

**What gets embedded**: structured frontmatter (title / tier / concerns / status) + first ~800 words of the body. This captures Context + Decision drivers reliably; misses content in long Amendments sections (e.g., ADR 0028's A1-A11). Future enhancement: embed each amendment separately.

**When to rebuild**: after adding a new ADR, or when frontmatter changes. The index is content-hash-keyed, so re-running on unchanged ADRs is fast (<1 sec for a no-op rebuild).

**Ollama prerequisite**: `ollama pull nomic-embed-text` (one-time; ~274 MB). The tool fails gracefully if Ollama is unreachable.

## CI integration (future)

A CI job will run `--check-only` to fail builds when:

- A new ADR is added without frontmatter
- An existing ADR has invalid frontmatter (missing required fields, invalid enum values, dangling cross-references)
- A `Superseded` ADR is missing its `superseded_by` link

The projections (`STATUS.md`, `INDEX.md`, `GRAPH.md`) themselves are committed to the repo (not generated at CI time) so PR reviewers can see them in diffs.
