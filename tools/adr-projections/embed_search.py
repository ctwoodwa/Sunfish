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


def _build_embed_text(meta: dict, body: str) -> str:
    """Compose the text we embed: structured metadata + first N words of body."""
    parts = []
    if "title" in meta:
        parts.append(f"Title: {meta['title']}")
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


def _load_index() -> dict:
    if INDEX_FILE.exists():
        return json.loads(INDEX_FILE.read_text())
    return {"model": DEFAULT_MODEL, "dim": None, "entries": []}


def _save_index(idx: dict) -> None:
    INDEX_FILE.write_text(json.dumps(idx, indent=1, ensure_ascii=False))


def cmd_index(args: argparse.Namespace) -> int:
    """(Re)build the index. Skips entries whose content_hash is unchanged."""
    idx = _load_index()
    if idx.get("model") != args.model:
        # Model changed → rebuild from scratch
        print(f"Model changed ({idx.get('model')} → {args.model}); rebuilding from scratch", file=sys.stderr)
        idx = {"model": args.model, "dim": None, "entries": []}
    by_id = {e["id"]: e for e in idx["entries"]}

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

        embed_text = _build_embed_text(meta, body)
        chash = _content_hash(embed_text)

        existing = by_id.get(adr_id)
        if existing and existing.get("content_hash") == chash:
            new_entries.append(existing)
            skipped_unchanged += 1
            continue

        embedding = _embed(embed_text, args.model, args.ollama)
        new_entries.append({
            "id": adr_id,
            "title": meta.get("title", ""),
            "filename": path.name,
            "tier": meta.get("tier", ""),
            "concern": meta.get("concern", []),
            "status": meta.get("status", ""),
            "content_hash": chash,
            "embedding": embedding,
        })
        embedded += 1
        if embedded % 5 == 0:
            print(f"  ...embedded {embedded}", file=sys.stderr)

    new_entries.sort(key=lambda e: e["id"])
    idx["entries"] = new_entries
    if new_entries:
        idx["dim"] = len(new_entries[0]["embedding"])
    _save_index(idx)

    print(f"Done. {embedded} embedded, {skipped_unchanged} unchanged, {skipped_no_meta} skipped (no meta).", file=sys.stderr)
    print(f"Index: {INDEX_FILE} (dim={idx['dim']})", file=sys.stderr)
    return 0


def _cosine(a: list[float], b: list[float]) -> float:
    dot = sum(x * y for x, y in zip(a, b))
    na = math.sqrt(sum(x * x for x in a))
    nb = math.sqrt(sum(y * y for y in b))
    return dot / (na * nb) if na and nb else 0.0


def cmd_search(args: argparse.Namespace) -> int:
    """Search the index for top-K matches to `query`."""
    idx = _load_index()
    if not idx["entries"]:
        print("Index is empty. Run `index` first.", file=sys.stderr)
        return 1

    query_emb = _embed(args.query, idx["model"], args.ollama)
    scored = [(e, _cosine(query_emb, e["embedding"])) for e in idx["entries"]]
    scored.sort(key=lambda x: -x[1])

    print(f"Top {args.top} ADRs matching: {args.query!r}\n")
    for entry, score in scored[: args.top]:
        concerns = entry["concern"]
        if isinstance(concerns, list):
            concern_str = ", ".join(concerns) if concerns else "(none)"
        else:
            concern_str = str(concerns)
        print(f"  ADR {entry['id']:04d} (score {score:.3f}) — {entry['title']}")
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
    s.set_defaults(fn=cmd_search)

    args = p.parse_args()
    return args.fn(args)


if __name__ == "__main__":
    sys.exit(main())
