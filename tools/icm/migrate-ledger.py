#!/usr/bin/env python3
"""One-shot migration: split icm/_state/active-workstreams.md into per-workstream
files at icm/_state/workstreams/W{NN}-{slug}.md.

After this runs:
  - icm/_state/workstreams/_preamble.md captures everything ABOVE the table
    (intro + status vocab + the "## Current state ..." H2 header line)
  - icm/_state/workstreams/_postamble.md captures everything BELOW the table
    (how-to-use + last-updated timeline)
  - icm/_state/workstreams/W{NN}-{slug}.md captures one file per current row
    with structured frontmatter + the full original notes cell preserved as
    free-text body under "## Notes"

Idempotent: running twice produces the same output.

Pure Python 3 stdlib.

Usage:
    python3 tools/icm/migrate-ledger.py
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
LEDGER = ROOT / "icm" / "_state" / "active-workstreams.md"
WORKSTREAMS_DIR = ROOT / "icm" / "_state" / "workstreams"

# Table column shape:
#   | # | Workstream | Status | Owner (current phase) | Reference | Notes |
TABLE_HEADER_RE = re.compile(
    r"^\|\s*#\s*\|\s*Workstream\s*\|\s*Status\s*\|\s*Owner.*\|\s*Reference\s*\|\s*Notes\s*\|\s*$"
)
SEPARATOR_RE = re.compile(r"^\|[\s\-:|]+\|\s*$")
ROW_RE = re.compile(r"^\|\s*(\d+)\s*\|")


def slugify(s: str) -> str:
    """Lowercase, hyphenated, ASCII-only slug.  Strips markdown emphasis + code."""
    s = re.sub(r"[*_`]", "", s)               # strip markdown emphasis/code
    s = re.sub(r"\([^)]*\)", "", s)           # strip parenthetical asides
    s = re.sub(r"\[[^\]]*\]\([^)]*\)", "", s) # strip markdown links
    s = re.sub(r"[^\w\s-]", " ", s, flags=re.ASCII)
    s = re.sub(r"\s+", "-", s.strip())
    s = s.lower()
    s = re.sub(r"-+", "-", s).strip("-")
    # Cap length so filenames stay reasonable.
    if len(s) > 60:
        s = s[:60].rstrip("-")
    return s or "unnamed"


def split_row(line: str) -> list[str]:
    """Split a markdown table row into its cell contents.  Strips outer pipes
    and the leading/trailing whitespace from each cell, but preserves internal
    formatting (links, code spans, bold, etc.) verbatim."""
    # Drop the leading and trailing pipe; markdown allows '|' inside cells only
    # if escaped, and the current ledger doesn't use escaped pipes.
    inner = line.strip()
    if inner.startswith("|"):
        inner = inner[1:]
    if inner.endswith("|"):
        inner = inner[:-1]
    cells = inner.split("|")
    return [c.strip() for c in cells]


def parse_status(status_cell: str) -> str:
    """Extract the canonical status from the Status cell.  The cell often looks
    like "`built` (5 phases shipped 2026-05-01)" or "`design-in-flight`
    (substrate ADRs all accepted...)".  We pull out the first backtick-quoted
    token as the canonical status; if none, fall back to the cell verbatim."""
    m = re.search(r"`([^`]+)`", status_cell)
    if m:
        return m.group(1).strip()
    return status_cell.strip()


def parse_owner(owner_cell: str) -> str:
    """Owner cell can be 'sunfish-PM' / 'sunfish-PM ✓' / 'research' /
    'research (XO)' / 'research session (exception turn)' etc.  We strip the
    trailing checkmark and any parenthetical, then map to a canonical token."""
    s = re.sub(r"\s*\([^)]*\)\s*", " ", owner_cell)
    s = s.replace("✓", "").strip()
    s = re.sub(r"\s+", " ", s)
    return s


def parse_table(text: str) -> tuple[str, list[dict], str]:
    """Locate the workstream table, return (preamble, rows, postamble).

    preamble = text before the table separator line (inclusive of the table's
               header + the column-shape line, since those are not generated).
    rows     = list of dicts, one per data row.
    postamble = text after the table.
    """
    lines = text.splitlines(keepends=True)
    header_idx = None
    sep_idx = None
    last_row_idx = None
    for i, line in enumerate(lines):
        if header_idx is None and TABLE_HEADER_RE.match(line):
            header_idx = i
            continue
        if header_idx is not None and sep_idx is None and SEPARATOR_RE.match(line):
            sep_idx = i
            continue
        if sep_idx is not None and ROW_RE.match(line):
            last_row_idx = i
            continue
        if sep_idx is not None and last_row_idx is not None and not ROW_RE.match(line):
            # Found end of table.
            break
    if header_idx is None or sep_idx is None or last_row_idx is None:
        raise SystemExit("Could not locate workstream table in ledger.")

    preamble = "".join(lines[:header_idx])
    table_header = "".join(lines[header_idx:sep_idx + 1])
    row_lines = lines[sep_idx + 1:last_row_idx + 1]
    postamble = "".join(lines[last_row_idx + 1:])

    # Preamble captures the introduction + Status vocab + "## Current state"
    # H2 header.  We append the header + separator to the preamble so the
    # render tool re-emits them verbatim.
    preamble = preamble + table_header

    rows = []
    for sort_order, line in enumerate(row_lines):
        if not ROW_RE.match(line):
            continue
        cells = split_row(line)
        if len(cells) < 6:
            raise SystemExit(f"Row has fewer than 6 cells: {line!r}")
        number = int(cells[0])
        title_cell = cells[1]
        status_cell = cells[2]
        owner_cell = cells[3]
        reference_cell = cells[4]
        notes_cell = cells[5]
        # Title used for slug — strip markdown emphasis + emoji + asides.
        slug_source = re.sub(r"`[^`]+`", "", title_cell)  # strip code spans
        slug = slugify(slug_source)
        rows.append({
            "sort_order": sort_order,
            "number": number,
            "slug": slug,
            "title_cell": title_cell,
            "status_cell": status_cell,
            "owner_cell": owner_cell,
            "reference_cell": reference_cell,
            "notes_cell": notes_cell,
            "status_token": parse_status(status_cell),
            "owner_token": parse_owner(owner_cell),
        })
    return preamble, rows, postamble


def disambiguate_filenames(rows: list[dict]) -> None:
    """Two rows may share a workstream number (parallel-branch ledger drift,
    pre-existing in active-workstreams.md).  Ensure each row has a unique
    filename by appending '-2', '-3', ... to colliding (number, slug) pairs."""
    seen: dict[tuple[int, str], int] = {}
    for r in rows:
        key = (r["number"], r["slug"])
        if key not in seen:
            seen[key] = 0
            r["filename_slug"] = r["slug"]
        else:
            seen[key] += 1
            r["filename_slug"] = f"{r['slug']}-{seen[key] + 1}"
    # Second pass: even with same slug disambiguated, two ROWS with the same
    # (number, different slug) coexist fine.  Only same-slug-same-number needs
    # the suffix.


def write_workstream_file(row: dict) -> None:
    """Write a per-workstream file at workstreams/W{NN}-{filename_slug}.md."""
    number = row["number"]
    fname = f"W{number:02d}-{row['filename_slug']}.md"
    path = WORKSTREAMS_DIR / fname
    # YAML frontmatter — only structured / regenerate-able fields.
    fm_lines = [
        "---",
        f"sort_order: {row['sort_order']}",
        f"number: {number}",
        f"slug: {row['filename_slug']}",
        # Title cell preserved verbatim (may contain markdown emphasis + code).
        f"title: {yaml_quote(row['title_cell'])}",
        f"status: {yaml_quote(row['status_token'])}",
        f"status_cell: {yaml_quote(row['status_cell'])}",
        f"owner: {yaml_quote(row['owner_token'])}",
        f"owner_cell: {yaml_quote(row['owner_cell'])}",
        f"reference_cell: {yaml_quote(row['reference_cell'])}",
        "---",
    ]
    body_lines = [
        "",
        "## Notes",
        "",
        row["notes_cell"],
        "",
    ]
    path.write_text("\n".join(fm_lines) + "\n" + "\n".join(body_lines), encoding="utf-8")


def yaml_quote(s: str) -> str:
    """Emit a YAML-safe scalar for our hand-rolled parser.  We always
    double-quote and backslash-escape internal double quotes + backslashes."""
    if s is None:
        return "null"
    s = s.replace("\\", "\\\\").replace("\"", "\\\"")
    return f"\"{s}\""


def write_preamble_postamble(preamble: str, postamble: str) -> None:
    (WORKSTREAMS_DIR / "_preamble.md").write_text(preamble, encoding="utf-8")
    (WORKSTREAMS_DIR / "_postamble.md").write_text(postamble, encoding="utf-8")


def main() -> int:
    if not LEDGER.exists():
        print(f"ledger not found: {LEDGER}", file=sys.stderr)
        return 2
    text = LEDGER.read_text(encoding="utf-8")
    preamble, rows, postamble = parse_table(text)
    disambiguate_filenames(rows)
    WORKSTREAMS_DIR.mkdir(parents=True, exist_ok=True)
    # Clear any prior per-workstream files (idempotent re-runs); preserve
    # _preamble.md / _postamble.md if present (overwritten below).
    for p in WORKSTREAMS_DIR.glob("W*.md"):
        p.unlink()
    for r in rows:
        write_workstream_file(r)
    write_preamble_postamble(preamble, postamble)
    print(f"wrote {len(rows)} workstream files + _preamble.md + _postamble.md "
          f"under {WORKSTREAMS_DIR.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
