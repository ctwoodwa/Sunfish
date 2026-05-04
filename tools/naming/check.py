#!/usr/bin/env python3
"""Sunfish naming-collision check tool.

Scans the repo + a hand-curated registry for naming collisions before you
propose a new name (package, namespace, ADR number, amendment ID, etc.).

USAGE
    tools/naming/check.py <name>                  # auto-detect + check
    tools/naming/check.py adr 76                  # specific ADR number
    tools/naming/check.py adr-amendment 28 A12    # ADR amendment number
    tools/naming/check.py package blocks-foo      # package directory name
    tools/naming/check.py namespace Sunfish.X.Y   # C# namespace
    tools/naming/check.py vocabulary "MyTerm"     # arbitrary vocabulary term

OUTPUT (one of)
    EXACT MATCH       — name is taken on disk
    RESERVED          — name has an intake stub claim (~ADR NNNN; not yet authored)
    COLLISION         — name conflicts with locked-vocabulary registry entry
    REJECTED          — name was rejected during a prior brainstorm; reason cited
    FUZZY MATCH       — Levenshtein-1 or substring overlap with existing names
    CLEAN             — no collision found; safe to propose

Runs against the local checkout in <100 ms for typical queries.
Pure Python 3 stdlib; minimal hand-rolled YAML parser (no PyYAML).

Companion to docs/_shared/engineering/naming-canon.md (the human-readable
canon). This tool is the machine-readable view of the same intent.
"""

from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PACKAGES_DIR = ROOT / "packages"
ADR_DIR = ROOT / "docs" / "adrs"
INTAKE_DIR = ROOT / "icm" / "00_intake" / "output"
REGISTRY_FILE = ROOT / "_shared" / "engineering" / "naming-registry.yaml"


# ────────────────────────────────────────────────────────────────────────────
# Minimal YAML loader (subset matching naming-registry.yaml shape)
# ────────────────────────────────────────────────────────────────────────────


def _load_yaml(path: Path) -> dict:
    """Parse the naming-registry.yaml — subset YAML matching that file's shape:
    top-level dict; list-of-dicts; nested dicts. Hand-rolled to avoid PyYAML dep."""
    if not path.exists():
        return {}
    text = path.read_text(encoding="utf-8")
    # Strip comments + blank lines; keep raw indent for parsing
    lines = []
    for raw in text.split("\n"):
        if not raw.strip() or raw.lstrip().startswith("#"):
            continue
        lines.append(raw.rstrip())

    root: dict = {}
    # Stack of (indent, container, list_item_or_none)
    # When a list item starts at indent N, sub-keys at indent N+2 populate THAT item.
    # Container kinds: "dict" or "list".
    stack: list[tuple[int, str, object]] = [(-1, "dict", root)]

    i = 0
    while i < len(lines):
        line = lines[i]
        indent = len(line) - len(line.lstrip())
        content = line.strip()

        # Pop stack to current scope
        while stack and stack[-1][0] >= indent and stack[-1][0] != -1:
            stack.pop()

        parent_indent, parent_kind, parent = stack[-1]

        if content.startswith("- "):
            # List item
            item_content = content[2:]
            if not isinstance(parent, list):
                # Defensive: skip orphan list items
                i += 1
                continue
            # Two cases: scalar list item, or first key of a dict-item
            m = re.match(r"^([A-Za-z_][A-Za-z0-9_ '-]*):\s*(.*)$", item_content)
            if m:
                # Dict-item starting with key: value
                key, val = m.group(1).strip(), m.group(2).strip()
                item: dict = {}
                if val == "":
                    # Subsequent indented keys belong to this item
                    item[key] = None  # placeholder; will be set by next iteration
                    parent.append(item)
                    stack.append((indent, "dict", item))
                    # The just-named key becomes a nested container — push it too
                    stack.append((indent + 2, "dict", item))
                    item[key] = []  # default-list; actual type set by next line
                elif val.startswith("[") and val.endswith("]"):
                    inner = val[1:-1].strip()
                    item[key] = [_coerce(x.strip()) for x in inner.split(",")] if inner else []
                    parent.append(item)
                    stack.append((indent, "dict", item))
                else:
                    item[key] = _coerce(val)
                    parent.append(item)
                    stack.append((indent, "dict", item))
            else:
                # Scalar list item
                parent.append(_coerce(item_content))
            i += 1
            continue

        # Key: value (in dict context)
        m = re.match(r"^([A-Za-z_][A-Za-z0-9_ '-]*):\s*(.*)$", content)
        if not m:
            i += 1
            continue
        key, val = m.group(1).strip(), m.group(2).strip()
        if not isinstance(parent, dict):
            i += 1
            continue

        if val == "":
            # Look ahead: next line starts with "- " → list, else dict
            next_idx = i + 1
            is_list = False
            if next_idx < len(lines):
                next_line = lines[next_idx]
                next_indent = len(next_line) - len(next_line.lstrip())
                if next_indent > indent and next_line.lstrip().startswith("- "):
                    is_list = True
            if is_list:
                parent[key] = []
                stack.append((indent, "list", parent[key]))
            else:
                parent[key] = {}
                stack.append((indent, "dict", parent[key]))
        elif val.startswith("[") and val.endswith("]"):
            inner = val[1:-1].strip()
            parent[key] = [_coerce(x.strip()) for x in inner.split(",")] if inner else []
        else:
            parent[key] = _coerce(val)

        i += 1

    return root


def _coerce(s: str):
    s = s.strip()
    if s.startswith('"') and s.endswith('"'):
        return s[1:-1]
    if s.startswith("'") and s.endswith("'"):
        return s[1:-1]
    if re.match(r"^-?\d+$", s):
        return int(s)
    if s.lower() == "true":
        return True
    if s.lower() == "false":
        return False
    if s.lower() in ("null", "~"):
        return None
    return s


# ────────────────────────────────────────────────────────────────────────────
# Filesystem scanners
# ────────────────────────────────────────────────────────────────────────────


def _list_packages() -> set[str]:
    if not PACKAGES_DIR.exists():
        return set()
    return {p.name for p in PACKAGES_DIR.iterdir() if p.is_dir()}


def _list_namespaces() -> set[str]:
    """Scan all .cs files for `namespace ...` declarations."""
    out: set[str] = set()
    for cs in PACKAGES_DIR.rglob("*.cs"):
        if "/obj/" in str(cs) or "/bin/" in str(cs):
            continue
        try:
            for line in cs.read_text(encoding="utf-8", errors="ignore").splitlines()[:20]:
                m = re.match(r"^\s*namespace\s+([A-Za-z0-9_.]+)\s*[;{]?", line)
                if m:
                    out.add(m.group(1))
                    break
        except (OSError, UnicodeDecodeError):
            continue
    return out


def _list_adrs() -> dict[int, str]:
    """{adr_number: filename}"""
    out: dict[int, str] = {}
    for f in ADR_DIR.glob("[0-9][0-9][0-9][0-9]-*.md"):
        m = re.match(r"^(\d{4})-", f.name)
        if m:
            out[int(m.group(1))] = f.name
    return out


def _list_amendments(adr_num: int) -> list[str]:
    """Find all amendment IDs in a given ADR's body.

    Matches BOTH:
      ## Amendment A1 — title    (canonical convention)
      ### A1 — title              (some ADRs like 0028 use this style)
      ### Amendment A1...

    Top-level amendments only (A1, A2, A11) — not sub-bullets (A1.2, A2.5)
    which are sub-numbering within a single amendment.
    """
    files = list(ADR_DIR.glob(f"{adr_num:04d}-*.md"))
    if not files:
        return []
    out: set[str] = set()
    # Top-level amendment heading: ## or ### with A<digits> as the first token after the hash group
    # (so `### A1 — ...` matches; `#### A1.2 ...` does not because we anchor at A\d+\b)
    pattern = re.compile(r"^(##|###)\s+(?:Amendment\s+)?(A\d+)\b", re.MULTILINE)
    for f in files:
        text = f.read_text(encoding="utf-8", errors="ignore")
        for m in pattern.finditer(text):
            out.add(m.group(2))
    return sorted(out, key=lambda a: int(a[1:]))


def _intake_reservations() -> dict[int, str]:
    """{tentative_adr_number: intake_filename} from `~ADR NNNN` mentions."""
    out: dict[int, str] = {}
    if not INTAKE_DIR.exists():
        return out
    for f in INTAKE_DIR.glob("*.md"):
        try:
            text = f.read_text(encoding="utf-8", errors="ignore")
        except OSError:
            continue
        for m in re.finditer(r"~ADR\s+0?(\d{2,4})", text):
            num = int(m.group(1))
            out.setdefault(num, f.name)
    return out


def _levenshtein(a: str, b: str) -> int:
    """Tiny Levenshtein for fuzzy match — only computes up to distance 2."""
    if abs(len(a) - len(b)) > 2:
        return 99
    prev = list(range(len(b) + 1))
    for i, ca in enumerate(a, 1):
        cur = [i]
        for j, cb in enumerate(b, 1):
            cost = 0 if ca == cb else 1
            cur.append(min(prev[j] + 1, cur[j - 1] + 1, prev[j - 1] + cost))
        prev = cur
    return prev[-1]


def _fuzzy_matches(name: str, candidates: set[str], max_dist: int = 2) -> list[tuple[str, int]]:
    name_l = name.lower()
    out = []
    for c in candidates:
        c_l = c.lower()
        if name_l == c_l:
            continue
        if name_l in c_l or c_l in name_l:
            out.append((c, 0))
            continue
        d = _levenshtein(name_l, c_l)
        if d <= max_dist:
            out.append((c, d))
    return sorted(out, key=lambda x: (x[1], x[0]))[:5]


# ────────────────────────────────────────────────────────────────────────────
# Check commands
# ────────────────────────────────────────────────────────────────────────────


def cmd_adr(args) -> int:
    num = args.number
    adrs = _list_adrs()
    if num in adrs:
        print(f"EXACT MATCH: ADR {num:04d} exists at docs/adrs/{adrs[num]}")
        return 1
    reservations = _intake_reservations()
    if num in reservations:
        print(f"RESERVED: ADR {num:04d} has intake stub at icm/00_intake/output/{reservations[num]}")
        return 1
    registry = _load_yaml(REGISTRY_FILE)
    for r in registry.get("reserved_adrs") or []:
        if r.get("number") == num:
            print(f"RESERVED (registry): ADR {num:04d} — \"{r.get('title')}\" reserved by {r.get('reserver')}")
            print(f"  Intake: {r.get('intake')}")
            return 1
    next_avail = max(adrs) + 1 if adrs else 1
    while next_avail in reservations or any(r.get("number") == next_avail for r in (registry.get("reserved_adrs") or [])):
        next_avail += 1
    print(f"CLEAN: ADR {num:04d} is available.")
    print(f"  Highest taken: ADR {max(adrs):04d}; next-available unreserved: ADR {next_avail:04d}")
    return 0


def cmd_adr_amendment(args) -> int:
    adr_num = args.adr
    amend = args.amendment.upper().lstrip("A")
    amend_id = f"A{amend}"
    existing = _list_amendments(adr_num)
    if amend_id in existing:
        print(f"EXACT MATCH: ADR {adr_num:04d} {amend_id} already exists")
        print(f"  Existing amendments: {', '.join(existing)}")
        return 1
    last_int = max([int(re.findall(r'\d+', a)[0]) for a in existing], default=0)
    print(f"CLEAN: ADR {adr_num:04d} {amend_id} is available.")
    print(f"  Existing amendments: {', '.join(existing) if existing else '(none)'}")
    print(f"  Next sequential available: A{last_int + 1}")
    return 0


def cmd_package(args) -> int:
    name = args.name
    packages = _list_packages()
    if name in packages:
        print(f"EXACT MATCH: package '{name}' exists at packages/{name}/")
        return 1
    fuzzy = _fuzzy_matches(name, packages)
    if fuzzy:
        print(f"FUZZY MATCH: '{name}' is similar to existing packages:")
        for c, d in fuzzy:
            print(f"  - {c} (distance: {d})")
        # Don't exit non-zero on fuzzy alone; warn
    registry = _load_yaml(REGISTRY_FILE)
    for cluster in registry.get("cluster_conventions") or []:
        prefix = cluster.get("prefix", "").rstrip("*")
        if prefix and name.startswith(prefix):
            print(f"CONVENTION OK: '{name}' matches cluster '{cluster.get('cluster')}' ({cluster.get('prefix')})")
            return 0
    print(f"CLEAN: package name '{name}' is available.")
    print(f"  No matching cluster convention; verify naming aligns with project conventions.")
    return 0


def cmd_namespace(args) -> int:
    name = args.name
    namespaces = _list_namespaces()
    if name in namespaces:
        print(f"EXACT MATCH: namespace '{name}' is in use")
        return 1
    fuzzy = _fuzzy_matches(name, namespaces)
    if fuzzy:
        print(f"FUZZY MATCH: '{name}' is similar to existing namespaces:")
        for c, d in fuzzy:
            print(f"  - {c} (distance: {d})")
    registry = _load_yaml(REGISTRY_FILE)
    for cluster in registry.get("cluster_conventions") or []:
        cluster_ns = cluster.get("namespace", "").rstrip("*")
        if cluster_ns and name.startswith(cluster_ns):
            print(f"CONVENTION OK: '{name}' matches cluster '{cluster.get('cluster')}' (namespace prefix {cluster.get('namespace')})")
            return 0
    print(f"CLEAN: namespace '{name}' is available.")
    return 0


def cmd_vocabulary(args) -> int:
    name = args.name
    registry = _load_yaml(REGISTRY_FILE)
    locked = registry.get("locked_vocabulary") or {}
    if isinstance(locked, dict) and name in locked:
        entry = locked[name]
        print(f"COLLISION (locked vocabulary): '{name}' is reserved.")
        if isinstance(entry, dict):
            print(f"  Definition: {entry.get('definition', '(none)')}")
        return 1
    rejected = registry.get("rejected_vocabulary") or []
    for r in rejected:
        if isinstance(r, dict) and r.get("name", "").lower() == name.lower():
            print(f"REJECTED: '{name}' was rejected.")
            print(f"  Reason: {r.get('reason')}")
            print(f"  Rejected in: {r.get('rejected_in')}")
            return 1
    # Fuzzy against locked vocabulary
    if isinstance(locked, dict):
        fuzzy = _fuzzy_matches(name, set(locked.keys()))
        if fuzzy:
            print(f"FUZZY MATCH: '{name}' is similar to locked vocabulary:")
            for c, d in fuzzy:
                print(f"  - {c} (distance: {d})")
    print(f"CLEAN: vocabulary '{name}' is available.")
    return 0


def cmd_auto(args) -> int:
    """Auto-detect: try each check in turn."""
    name = args.name

    # Heuristics:
    # - All digits, 1-4 chars → ADR number
    # - "An" or "An.m" → amendment (need adr context though; can't auto-detect)
    # - Contains "/" or starts with "Sunfish." → namespace
    # - Starts with "blocks-" / "foundation-" / "kernel-" / contains "-" → package
    # - Else → vocabulary

    if name.isdigit() and len(name) <= 4:
        args.number = int(name)
        return cmd_adr(args)

    if name.startswith("Sunfish.") or "." in name:
        return cmd_namespace(args)

    if "-" in name and any(name.startswith(p) for p in ("blocks-", "foundation-", "kernel-", "ui-", "compat-", "providers-")):
        return cmd_package(args)

    return cmd_vocabulary(args)


# ────────────────────────────────────────────────────────────────────────────
# CLI
# ────────────────────────────────────────────────────────────────────────────


def main() -> int:
    p = argparse.ArgumentParser(description="Sunfish naming-collision check.")
    sub = p.add_subparsers(dest="cmd")

    a = sub.add_parser("adr", help="Check ADR number availability")
    a.add_argument("number", type=int)
    a.set_defaults(fn=cmd_adr)

    am = sub.add_parser("adr-amendment", help="Check ADR amendment ID availability")
    am.add_argument("adr", type=int)
    am.add_argument("amendment")
    am.set_defaults(fn=cmd_adr_amendment)

    pk = sub.add_parser("package", help="Check package directory name")
    pk.add_argument("name")
    pk.set_defaults(fn=cmd_package)

    ns = sub.add_parser("namespace", help="Check C# namespace availability")
    ns.add_argument("name")
    ns.set_defaults(fn=cmd_namespace)

    voc = sub.add_parser("vocabulary", help="Check vocabulary term against registry")
    voc.add_argument("name")
    voc.set_defaults(fn=cmd_vocabulary)

    auto = sub.add_parser("auto", help="Auto-detect type from name shape")
    auto.add_argument("name")
    auto.set_defaults(fn=cmd_auto)

    # Allow bare "check.py NAME" as shorthand for "check.py auto NAME"
    args, leftover = p.parse_known_args()
    if not args.cmd and leftover:
        return cmd_auto(argparse.Namespace(name=leftover[0]))
    if not args.cmd:
        p.print_help()
        return 2

    return args.fn(args)


if __name__ == "__main__":
    sys.exit(main())
