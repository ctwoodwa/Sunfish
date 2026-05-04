#!/usr/bin/env python3
"""ADR portfolio projection tool — reads YAML frontmatter from docs/adrs/*.md
and emits INDEX.md, STATUS.md, GRAPH.md as derived read-models.

Usage:  python3 tools/adr-projections/project.py [--check-only] [--verbose]

The journal (docs/adrs/) is authoritative; projections are rebuilt from it.
See docs/adrs/_FRONTMATTER.md for the frontmatter schema."""

import os
import re
import sys
from pathlib import Path
from collections import defaultdict

ADR_DIR = Path(__file__).resolve().parents[2] / "docs" / "adrs"
VALID_STATUS = {"Proposed", "Accepted", "Superseded", "Deprecated", "Withdrawn"}
VALID_TIER = {"foundation", "kernel", "ui-core", "adapter", "block",
              "accelerator", "governance", "policy", "tooling", "process"}
VALID_PIPELINE = {"sunfish-feature-change", "sunfish-api-change",
                  "sunfish-scaffolding", "sunfish-docs-change",
                  "sunfish-quality-control", "sunfish-test-expansion",
                  "sunfish-gap-analysis"}
VALID_CONCERN = {"security", "persistence", "ui", "accessibility", "regulatory",
                 "distribution", "multi-tenancy", "audit", "identity",
                 "capability-model", "configuration", "observability",
                 "threat-model", "governance", "dev-experience", "operations",
                 "commercial", "mission-space", "data-residency",
                 "version-management"}


def parse_frontmatter(text):
    """Minimal YAML frontmatter parser for the schema in _FRONTMATTER.md.
    Supports: key: value (str/int/null/bool), key: [], key:\\n  - item lists."""
    if not text.startswith("---\n"):
        return None, text
    end = text.find("\n---\n", 4)
    if end < 0:
        return None, text
    block = text[4:end]
    body = text[end + 5:]
    meta = {}
    cur_key = None
    for line in block.split("\n"):
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        if line.startswith("  - "):
            if cur_key is not None:
                meta[cur_key].append(_coerce(line[4:].strip()))
            continue
        m = re.match(r"^([a-z_]+):\s*(.*)$", line)
        if not m:
            continue
        key, val = m.group(1), m.group(2).strip()
        if val == "":
            meta[key] = []
            cur_key = key
        elif val == "[]":
            meta[key] = []
            cur_key = None
        else:
            meta[key] = _coerce(val)
            cur_key = None
    return meta, body


def _coerce(s):
    s = s.strip()
    if s.lower() == "null" or s == "":
        return None
    if s.lower() == "true":
        return True
    if s.lower() == "false":
        return False
    if re.match(r"^-?\d+$", s):
        return int(s)
    if s.startswith("'") and s.endswith("'"):
        return s[1:-1]
    if s.startswith('"') and s.endswith('"'):
        return s[1:-1]
    return s


def validate(meta, path):
    errs = []
    if meta is None:
        return [f"{path}: no frontmatter"]
    for k in ("id", "title", "status", "date", "tier"):
        if k not in meta or meta[k] is None:
            errs.append(f"{path}: missing required field '{k}'")
    if "status" in meta and meta["status"] not in VALID_STATUS:
        errs.append(f"{path}: invalid status '{meta['status']}'")
    if "tier" in meta and meta["tier"] not in VALID_TIER:
        errs.append(f"{path}: invalid tier '{meta['tier']}'")
    if meta.get("pipeline_variant") and meta["pipeline_variant"] not in VALID_PIPELINE:
        errs.append(f"{path}: invalid pipeline_variant '{meta['pipeline_variant']}'")
    for c in meta.get("concern") or []:
        if c not in VALID_CONCERN:
            errs.append(f"{path}: invalid concern '{c}'")
    if meta.get("status") == "Superseded" and not meta.get("superseded_by"):
        errs.append(f"{path}: status=Superseded requires superseded_by")
    if meta.get("date") and not re.match(r"^\d{4}-\d{2}-\d{2}$", str(meta["date"])):
        errs.append(f"{path}: date must be YYYY-MM-DD, got '{meta['date']}'")
    return errs


def collect():
    metas = []
    for p in sorted(ADR_DIR.glob("[0-9][0-9][0-9][0-9]-*.md")):
        text = p.read_text(encoding="utf-8")
        meta, _ = parse_frontmatter(text)
        metas.append((p.name, meta))
    return metas


def derive_consumed_by(metas):
    """Build a reverse-index: for each ADR X, collect all ADRs Y where X appears
    in Y's `composes` or `extends` arrays.  Returns a dict mapping ADR id → sorted
    list of consumer ADR ids.  consumed_by is tooling-derived; it is never written
    back to the ADR files on disk."""
    consumers: dict[int, list[int]] = defaultdict(list)
    for _name, m in metas:
        if m is None:
            continue
        source_id = m.get("id")
        if source_id is None:
            continue
        for target in (m.get("composes") or []) + (m.get("extends") or []):
            if isinstance(target, int):
                consumers[target].append(source_id)
    # Sort each list so output is deterministic
    return {k: sorted(v) for k, v in consumers.items()}


def project_status(metas):
    by_status = defaultdict(list)
    for name, m in metas:
        if m is None:
            by_status["(no-frontmatter)"].append(name)
            continue
        by_status[m["status"]].append((m["id"], m["title"], name, m.get("date")))
    out = ["# ADR Status Projection",
           "",
           "_Auto-generated from frontmatter by `tools/adr-projections/project.py`. Do not edit by hand._",
           ""]
    for status in ("Proposed", "Accepted", "Superseded", "Deprecated", "Withdrawn", "(no-frontmatter)"):
        if status not in by_status:
            continue
        out.append(f"## {status} ({len(by_status[status])})")
        out.append("")
        for entry in sorted(by_status[status]):
            if isinstance(entry, tuple):
                id_, title, name, date = entry
                out.append(f"- ADR {id_:04d} — [{title}](./{name}) — {date}")
            else:
                out.append(f"- {entry}")
        out.append("")
    return "\n".join(out)


def project_topical(metas, consumed_by=None):
    by_tier = defaultdict(list)
    by_concern = defaultdict(list)
    id_to_entry: dict[int, tuple] = {}
    for name, m in metas:
        if m is None:
            continue
        by_tier[m["tier"]].append((m["id"], m["title"], name))
        for c in m.get("concern") or []:
            by_concern[c].append((m["id"], m["title"], name))
        id_to_entry[m["id"]] = (m["id"], m["title"], name)
    out = ["# ADR Topical Index",
           "",
           "_Auto-generated from frontmatter by `tools/adr-projections/project.py`. Do not edit by hand._",
           "",
           "## By tier",
           ""]
    for tier in sorted(by_tier):
        out.append(f"### {tier} ({len(by_tier[tier])})")
        out.append("")
        for id_, title, name in sorted(by_tier[tier]):
            out.append(f"- ADR {id_:04d} — [{title}](./{name})")
        out.append("")
    out.append("## By concern")
    out.append("")
    for concern in sorted(by_concern):
        out.append(f"### {concern} ({len(by_concern[concern])})")
        out.append("")
        for id_, title, name in sorted(by_concern[concern]):
            out.append(f"- ADR {id_:04d} — [{title}](./{name})")
        out.append("")

    # consumed_by section — tooling-derived reverse-index of composes + extends
    if consumed_by is not None:
        out.append("## ADRs by usage (consumed_by)")
        out.append("")
        out.append("_Each row lists the ADR and the other ADRs that compose or extend it._")
        out.append("_`consumed_by` is auto-derived from `composes`/`extends` arrays; never hand-authored._")
        out.append("")
        # Sort by descending consumer count, then ascending ADR id for tiebreak
        ranked = sorted(consumed_by.items(), key=lambda kv: (-len(kv[1]), kv[0]))
        if ranked:
            for target_id, consumer_ids in ranked:
                entry = id_to_entry.get(target_id)
                if entry:
                    _, title, name = entry
                    consumers_str = ", ".join(
                        f"[ADR {c:04d}](./{id_to_entry[c][2]})"
                        if c in id_to_entry
                        else f"ADR {c:04d}"
                        for c in consumer_ids
                    )
                    out.append(
                        f"- ADR {target_id:04d} — [{title}](./{name}) "
                        f"← consumed by {len(consumer_ids)}: {consumers_str}"
                    )
                else:
                    out.append(
                        f"- ADR {target_id:04d} (not found) "
                        f"← consumed by {len(consumer_ids)}: {consumer_ids}"
                    )
            out.append("")
        else:
            out.append("_No `composes` or `extends` relationships populated yet._")
            out.append("")

    return "\n".join(out)


def project_graph(metas):
    out = ["# ADR Dependency Graph",
           "",
           "_Auto-generated from frontmatter by `tools/adr-projections/project.py`. Do not edit by hand._",
           "",
           "Edges:",
           "- `composes` (X uses Y's contracts) — solid arrow",
           "- `extends` (X adds to Y) — dashed arrow",
           "- `supersedes` (X replaces Y) — bold arrow",
           "",
           "```mermaid",
           "graph LR"]
    for name, m in metas:
        if m is None:
            continue
        for c in m.get("composes") or []:
            out.append(f"  ADR{m['id']:04d} --> ADR{c:04d}")
        for e in m.get("extends") or []:
            out.append(f"  ADR{m['id']:04d} -.-> ADR{e:04d}")
        for s in m.get("supersedes") or []:
            out.append(f"  ADR{m['id']:04d} ==> ADR{s:04d}")
    out.append("```")
    out.append("")
    return "\n".join(out)


def main():
    check_only = "--check-only" in sys.argv
    verbose = "--verbose" in sys.argv

    metas = collect()
    all_errs = []
    for name, meta in metas:
        all_errs.extend(validate(meta, name))

    # Derive consumed_by reverse-index (tooling-derived; never written to disk ADR files)
    consumed_by = derive_consumed_by(metas)

    if verbose or check_only:
        coverage = sum(1 for _, m in metas if m is not None)
        print(f"ADRs scanned: {len(metas)}; with frontmatter: {coverage}; "
              f"errors: {len(all_errs)}", file=sys.stderr)
        for e in all_errs:
            print(f"  {e}", file=sys.stderr)
        if consumed_by:
            ranked = sorted(consumed_by.items(), key=lambda kv: (-len(kv[1]), kv[0]))
            print(f"consumed_by entries: {len(consumed_by)} ADRs have consumers; "
                  f"top-5: {[(k, len(v)) for k, v in ranked[:5]]}",
                  file=sys.stderr)

    if check_only:
        return 1 if all_errs else 0

    (ADR_DIR / "STATUS.md").write_text(project_status(metas), encoding="utf-8")
    (ADR_DIR / "INDEX.md").write_text(project_topical(metas, consumed_by=consumed_by),
                                      encoding="utf-8")
    (ADR_DIR / "GRAPH.md").write_text(project_graph(metas), encoding="utf-8")
    print(f"Wrote STATUS.md, INDEX.md, GRAPH.md to {ADR_DIR}", file=sys.stderr)
    return 0


if __name__ == "__main__":
    sys.exit(main())
