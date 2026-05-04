#!/usr/bin/env python3
"""ADR semantic search via local Ollama embeddings.

Builds a searchable index over `docs/adrs/[0-9]{4}-*.md` using
`nomic-embed-text` (default; ~274 MB; runs locally) and answers
queries with top-K most-semantically-similar ADRs by cosine similarity.

Usage:
    python3 tools/adr-projections/embed_search.py index            # build/refresh index
    python3 tools/adr-projections/embed_search.py search "query"   # top-5 hits
    python3 tools/adr-projections/embed_search.py search "query" --top 10
    python3 tools/adr-projections/embed_search.py search "query" --ollama http://desktop-umt08rn:11434

Companion to the journal+projections+snapshot pattern (ADR 0071).
The journal is authoritative; this index is rebuilt from it on demand.

Index format: tools/adr-projections/.embeddings.json
  { "model": "nomic-embed-text",
    "dim": 768,
    "entries": [
      {"id": 1, "title": "...", "filename": "...", "content_hash": "...",
       "embedding": [0.123, -0.456, ...]} ]
  }

Cosine similarity: numpy not required; pure stdlib math.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import re
import sys
import urllib.request
import urllib.error
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ADR_DIR = ROOT / "docs" / "adrs"
INDEX_FILE = Path(__file__).parent / ".embeddings.json"

DEFAULT_OLLAMA_URL = "http://localhost:11434"
DEFAULT_MODEL = "nomic-embed-text"

# How much of each ADR to embed: frontmatter is metadata-rich; the first
# ~600 words of the body capture Context + Decision drivers reliably.
EMBED_WORD_BUDGET = 800


def _embed(text: str, model: str, ollama_url: str) -> list[float]:
    """Call Ollama's /api/embed endpoint (current API; /api/embeddings is deprecated)."""
    body = json.dumps({"model": model, "input": text}).encode("utf-8")
    req = urllib.request.Request(
        f"{ollama_url}/api/embed",
        data=body,
        headers={"Content-Type": "application/json"},
    )
    try:
        with urllib.request.urlopen(req, timeout=60) as resp:
            data = json.loads(resp.read())
    except urllib.error.URLError as e:
        raise RuntimeError(f"Ollama at {ollama_url} unreachable: {e}") from e
    embeddings = data.get("embeddings")
    if not embeddings:
        raise RuntimeError(f"Ollama returned unexpected shape: {data}")
    return embeddings[0]


def _strip_frontmatter(text: str) -> tuple[str, str]:
    """Return (frontmatter_block, body)."""
    if not text.startswith("---\n"):
        return "", text
    end = text.find("\n---\n", 4)
    if end < 0:
        return "", text
    return text[4:end], text[end + 5:]


def _extract_meta(frontmatter: str) -> dict:
    """Minimal YAML extraction for id, title, status, tier, concern."""
    meta: dict = {}
    cur_key: str | None = None
    for line in frontmatter.split("\n"):
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        if line.startswith("  - ") and cur_key:
            meta.setdefault(cur_key, []).append(line[4:].strip())
            continue
        m = re.match(r"^([a-z_]+):\s*(.*)$", line)
        if not m:
            continue
        key, val = m.group(1), m.group(2).strip()
        if val.startswith("[") and val.endswith("]"):
            inner = val[1:-1].strip()
            meta[key] = [v.strip() for v in inner.split(",")] if inner else []
            cur_key = None
        elif val == "":
            meta[key] = []
            cur_key = key
        else:
            meta[key] = val
            cur_key = None
    return meta


def _split_amendments(body: str) -> list[tuple[str | None, str]]:
    """Split ADR body into (amendment_id, text) chunks.

    Returns at minimum [(None, main_body)]. If the body contains amendment
    headings (## or ### with `Amendment AN` or bare `An` form), each amendment
    becomes its own chunk: [(None, main), ("A1", amendment_1_body), ...].

    Heading patterns matched (top-level amendments only; nested A1.2 sub-bullets
    are part of their parent A1's chunk):
      ## Amendment A1 — title
      ### Amendment A1 — title
      ### A1 — title              (ADR 0028 style)
    """
    pattern = re.compile(
        r"^(##|###)\s+(?:Amendment\s+)?(A\d+)\b.*$",
        re.MULTILINE,
    )
    matches = list(pattern.finditer(body))
    if not matches:
        return [(None, body)]
    chunks: list[tuple[str | None, str]] = []
    # Main body: everything before the first amendment heading
    chunks.append((None, body[: matches[0].start()]))
    for i, m in enumerate(matches):
        amend_id = m.group(2)
        end = matches[i + 1].start() if i + 1 < len(matches) else len(body)
        chunks.append((amend_id, body[m.start():end]))
    return chunks


def _build_embed_text(meta: dict, body: str, amendment: str | None = None) -> str:
    """Compose the text we embed: structured metadata + first N words of body chunk."""
    parts = []
    title = meta.get("title", "")
    if amendment:
        parts.append(f"Title: ADR {meta.get('id', '?')} — {title} (Amendment {amendment})")
    elif title:
        parts.append(f"Title: ADR {meta.get('id', '?')} — {title}")
    if "tier" in meta:
        parts.append(f"Tier: {meta['tier']}")
    if "concern" in meta and meta["concern"]:
        parts.append(f"Concern: {', '.join(meta['concern']) if isinstance(meta['concern'], list) else meta['concern']}")
    if "status" in meta:
        parts.append(f"Status: {meta['status']}")
    parts.append("")
    body_words = body.split()
    parts.append(" ".join(body_words[:EMBED_WORD_BUDGET]))
    return "\n".join(parts)


def _content_hash(text: str) -> str:
    return hashlib.sha256(text.encode("utf-8")).hexdigest()[:16]


INDEX_FORMAT_VERSION = 2  # bumped 2026-05-04 for amendment-chunking


def _load_index() -> dict:
    if INDEX_FILE.exists():
        idx = json.loads(INDEX_FILE.read_text())
        if idx.get("format_version") != INDEX_FORMAT_VERSION:
            print(
                f"Index format changed ({idx.get('format_version', 1)} → {INDEX_FORMAT_VERSION}); "
                "discarding cached entries (full rebuild on next index)",
                file=sys.stderr,
            )
            return {"format_version": INDEX_FORMAT_VERSION, "model": DEFAULT_MODEL, "dim": None, "entries": []}
        return idx
    return {"format_version": INDEX_FORMAT_VERSION, "model": DEFAULT_MODEL, "dim": None, "entries": []}


def _save_index(idx: dict) -> None:
    INDEX_FILE.write_text(json.dumps(idx, indent=1, ensure_ascii=False))


def cmd_index(args: argparse.Namespace) -> int:
    """(Re)build the index. Splits each ADR into main-body + per-amendment chunks.

    Each chunk is a separate index entry keyed by `chunk_id` (e.g., "28" for
    main body, "28-A8" for ADR 0028's A8 amendment). Skips entries whose
    content_hash is unchanged (incremental rebuild)."""
    idx = _load_index()
    if idx.get("model") != args.model:
        print(f"Model changed ({idx.get('model')} → {args.model}); rebuilding from scratch", file=sys.stderr)
        idx = {"format_version": INDEX_FORMAT_VERSION, "model": args.model, "dim": None, "entries": []}
    by_chunk = {e["chunk_id"]: e for e in idx["entries"] if "chunk_id" in e}

    files = sorted(ADR_DIR.glob("[0-9][0-9][0-9][0-9]-*.md"))
    print(f"Indexing {len(files)} ADRs against {args.ollama} (model={args.model})", file=sys.stderr)

    new_entries: list[dict] = []
    embedded = 0
    skipped_unchanged = 0
    skipped_no_meta = 0
    for path in files:
        text = path.read_text(encoding="utf-8")
        fm, body = _strip_frontmatter(text)
        if not fm:
            skipped_no_meta += 1
            continue
        meta = _extract_meta(fm)
        if "id" not in meta:
            skipped_no_meta += 1
            continue
        try:
            adr_id = int(meta["id"])
        except (ValueError, TypeError):
            skipped_no_meta += 1
            continue

        # Split body into main + amendment chunks
        chunks = _split_amendments(body)
        for amendment_id, chunk_body in chunks:
            chunk_id = f"{adr_id}" if amendment_id is None else f"{adr_id}-{amendment_id}"

            embed_text = _build_embed_text(meta, chunk_body, amendment=amendment_id)
            chash = _content_hash(embed_text)

            existing = by_chunk.get(chunk_id)
            if existing and existing.get("content_hash") == chash:
                new_entries.append(existing)
                skipped_unchanged += 1
                continue

            embedding = _embed(embed_text, args.model, args.ollama)
            new_entries.append({
                "chunk_id": chunk_id,
                "adr_id": adr_id,
                "amendment": amendment_id,
                "title": meta.get("title", ""),
                "filename": path.name,
                "tier": meta.get("tier", ""),
                "concern": meta.get("concern", []),
                "status": meta.get("status", ""),
                "content_hash": chash,
                "embedding": embedding,
            })
            embedded += 1
            if embedded % 10 == 0:
                print(f"  ...embedded {embedded}", file=sys.stderr)

    # Sort: by adr_id, then main body before amendments, then amendments by number
    def _sort_key(e: dict):
        amend = e.get("amendment")
        amend_num = int(amend[1:]) if amend and amend[0] == "A" else -1
        return (e["adr_id"], amend_num)
    new_entries.sort(key=_sort_key)
    idx["entries"] = new_entries
    if new_entries:
        idx["dim"] = len(new_entries[0]["embedding"])
    _save_index(idx)

    main_count = sum(1 for e in new_entries if e.get("amendment") is None)
    amend_count = len(new_entries) - main_count
    print(f"Done. {embedded} embedded, {skipped_unchanged} unchanged, {skipped_no_meta} skipped (no meta).", file=sys.stderr)
    print(f"Index: {INDEX_FILE} (dim={idx['dim']})", file=sys.stderr)
    print(f"Coverage: {main_count} main-body chunks + {amend_count} amendment chunks = {len(new_entries)} total entries", file=sys.stderr)
    return 0


def _cosine(a: list[float], b: list[float]) -> float:
    dot = sum(x * y for x, y in zip(a, b))
    na = math.sqrt(sum(x * x for x in a))
    nb = math.sqrt(sum(y * y for y in b))
    return dot / (na * nb) if na and nb else 0.0


def cmd_search(args: argparse.Namespace) -> int:
    """Search the index for top-K matches to `query`. Searches across main-body
    and amendment chunks; collapses multiple chunks of the same ADR if requested."""
    idx = _load_index()
    if not idx["entries"]:
        print("Index is empty. Run `index` first.", file=sys.stderr)
        return 1

    query_emb = _embed(args.query, idx["model"], args.ollama)
    scored = [(e, _cosine(query_emb, e["embedding"])) for e in idx["entries"]]
    scored.sort(key=lambda x: -x[1])

    # Optional: collapse to top-K UNIQUE ADRs (best chunk per ADR)
    if args.collapse:
        seen_adrs: set[int] = set()
        collapsed = []
        for entry, score in scored:
            adr_id = entry.get("adr_id", entry.get("id"))
            if adr_id in seen_adrs:
                continue
            seen_adrs.add(adr_id)
            collapsed.append((entry, score))
        scored = collapsed

    print(f"Top {args.top} matches for: {args.query!r}\n")
    for entry, score in scored[: args.top]:
        concerns = entry["concern"]
        if isinstance(concerns, list):
            concern_str = ", ".join(concerns) if concerns else "(none)"
        else:
            concern_str = str(concerns)
        adr_id = entry.get("adr_id", entry.get("id", 0))
        amend = entry.get("amendment")
        if amend:
            print(f"  ADR {adr_id:04d} {amend} (score {score:.3f}) — {entry['title']}")
        else:
            print(f"  ADR {adr_id:04d}    (score {score:.3f}) — {entry['title']}")
        print(f"    tier: {entry['tier']:<12} concerns: {concern_str}")
        print(f"    file: docs/adrs/{entry['filename']}")
        print()
    return 0


def main() -> int:
    p = argparse.ArgumentParser(description="Semantic search over Sunfish ADRs via local Ollama embeddings.")
    p.add_argument("--ollama", default=DEFAULT_OLLAMA_URL, help=f"Ollama URL (default: {DEFAULT_OLLAMA_URL})")
    p.add_argument("--model", default=DEFAULT_MODEL, help=f"Embedding model (default: {DEFAULT_MODEL})")
    sub = p.add_subparsers(dest="cmd", required=True)

    sub.add_parser("index", help="(Re)build the embedding index").set_defaults(fn=cmd_index)

    s = sub.add_parser("search", help="Top-K most-similar ADRs to a query")
    s.add_argument("query")
    s.add_argument("--top", type=int, default=5)
    s.add_argument("--collapse", action="store_true",
                   help="Collapse to top-K UNIQUE ADRs (best chunk per ADR)")
    s.set_defaults(fn=cmd_search)

    args = p.parse_args()
    return args.fn(args)


if __name__ == "__main__":
    sys.exit(main())
