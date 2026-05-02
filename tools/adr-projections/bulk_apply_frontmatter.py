#!/usr/bin/env python3
"""Bulk-apply YAML frontmatter to ADRs that don't have it yet.

Reads each ADR file, extracts (id, title, status, date) from the existing
H1 + bold-prefixed lines, infers (tier, concern, pipeline_variant, amendments)
from the filename + body via heuristics, and writes the frontmatter block
above the H1.

ADRs that already have frontmatter (start with `---`) are skipped.

This is a one-time tool — designed to be run once during the foundation
migration. After this runs, new ADRs get frontmatter from the template.

Heuristics fail gracefully: any field the heuristic can't determine is
left empty (e.g., `concern: []`) — humans refine manually post-script.
"""

import re
import sys
from pathlib import Path

ADR_DIR = Path(__file__).resolve().parents[2] / "docs" / "adrs"

# Filename keyword → tier (rough heuristic; refine manually for misses)
TIER_KEYWORDS = [
    ("kernel-", "kernel"),
    ("foundation-", "foundation"),
    ("blocks-", "block"),
    ("ui-", "ui-core"),
    ("adapter", "adapter"),
    ("anchor", "accelerator"),
    ("bridge", "accelerator"),
    ("react", "adapter"),
    ("blazor", "adapter"),
    ("scaffolding", "tooling"),
    ("ci", "governance"),
    ("license", "governance"),
    ("branch-protection", "governance"),
    ("threat-model", "policy"),
    ("regulatory", "policy"),
    ("compliance", "policy"),
    ("subagent", "process"),
    ("translation", "process"),
    ("naming", "governance"),
    ("schema-registry", "governance"),
    ("crdt", "kernel"),
    ("audit-trail", "kernel"),
    ("multitenancy", "foundation"),
    ("featuremanagement", "foundation"),
    ("templates-boundary", "tooling"),
    ("react-adapter", "adapter"),
    ("a11y-harness", "ui-core"),
    ("global-domain-types", "foundation"),
    ("syncstate", "foundation"),
    ("kernel-runtime-split", "kernel"),
    ("kernel-module-format", "kernel"),
    ("event-bus", "kernel"),
    ("post-quantum-signature", "kernel"),
    ("type-customization", "foundation"),
    ("bundle-manifest", "foundation"),
    ("module-entity-registration", "foundation"),
    ("local-first", "foundation"),
    ("integrations", "foundation"),
    ("adapter-parity", "ui-core"),
    ("dialog-provider", "ui-core"),
    ("button-variant", "ui-core"),
    ("css-class-prefix", "ui-core"),
    ("dual-namespace", "ui-core"),
    ("dynamic-forms", "foundation"),
    ("taxonomy", "foundation"),
    ("payments", "foundation"),
    ("messaging", "foundation"),
    ("work-order", "block"),
    ("electronic-signature", "block"),
    ("vendor-onboarding", "block"),
    ("public-listing", "block"),
    ("right-of-entry", "policy"),
    ("peer-transport", "foundation"),
    ("mission-space", "foundation"),
    ("federation", "foundation"),
    ("reporting", "foundation"),
    ("example-catalog", "tooling"),
    ("docs-taxonomy", "tooling"),
    ("multi-team-anchor", "accelerator"),
    ("browser-shell", "accelerator"),
    ("hybrid-multi-tenant", "accelerator"),
    ("posture", "accelerator"),
    ("recovery", "foundation"),
    ("leasing-pipeline", "block"),
    ("web-components-lit", "ui-core"),
    ("rulesets", "governance"),
    ("required-check", "governance"),
    ("workflow", "process"),
    ("velocity", "process"),
]

# Filename keyword → concern tags (multi-tag possible)
CONCERN_KEYWORDS = [
    ("schema", ["persistence", "version-management"]),
    ("registry", ["governance"]),
    ("kernel", ["distribution"]),
    ("event-bus", ["distribution", "audit"]),
    ("post-quantum", ["security", "version-management"]),
    ("type-customization", ["dev-experience"]),
    ("bridge-is-saas", ["operations", "commercial"]),
    ("bundle-manifest", ["persistence", "version-management"]),
    ("multitenancy", ["multi-tenancy"]),
    ("featuremanagement", ["configuration"]),
    ("templates", ["dev-experience"]),
    ("versioning", ["version-management"]),
    ("local-first", ["distribution", "persistence"]),
    ("integrations", ["operations"]),
    ("parity", ["ui"]),
    ("module-entity", ["persistence"]),
    ("naming", ["governance", "dev-experience"]),
    ("web-components", ["ui"]),
    ("license", ["governance", "commercial"]),
    ("rulesets", ["governance"]),
    ("required-check", ["governance"]),
    ("crdt", ["persistence", "distribution", "version-management"]),
    ("federation", ["distribution"]),
    ("react-adapter", ["ui"]),
    ("hybrid-multi-tenant", ["operations", "commercial", "multi-tenancy"]),
    ("anchor-workspace", ["accessibility", "ui"]),
    ("browser-shell", ["ui", "threat-model"]),
    ("a11y-harness", ["accessibility", "ui"]),
    ("global-domain", ["dev-experience"]),
    ("syncstate", ["distribution", "ui"]),
    ("ci-platform", ["governance", "operations"]),
    ("branch-protection", ["governance", "threat-model"]),
    ("required-check-minimalism", ["governance"]),
    ("translation-workflow", ["dev-experience", "ui"]),
    ("dual-namespace", ["ui", "dev-experience"]),
    ("subagent-driven", ["dev-experience"]),
    ("threat-model", ["threat-model", "security"]),
    ("anchor-windows", ["accelerator" if False else "operations"]),  # accelerator not a concern
    ("recovery-scheme", ["security"]),
    ("historical-keys", ["security", "audit"]),
    ("anchor-multi-backend", ["ui", "operations"]),
    ("audit-trail", ["audit", "security"]),
    ("payments", ["operations", "commercial"]),
    ("messaging", ["distribution"]),
    ("work-order", ["operations"]),
    ("signature", ["security", "audit"]),
    ("dynamic-forms", ["ui", "configuration"]),
    ("taxonomy", ["dev-experience"]),
    ("leasing-pipeline", ["regulatory", "operations"]),
    ("vendor-onboarding", ["security", "operations"]),
    ("public-listing", ["ui", "threat-model"]),
    ("right-of-entry", ["regulatory"]),
    ("peer-transport", ["distribution"]),
    ("mission-space", ["mission-space", "capability-model"]),
    ("regulatory", ["regulatory", "policy"]),
    ("dialog-provider", ["ui"]),
    ("button-variant", ["ui"]),
    ("css-class-prefix", ["ui"]),
    ("reporting-pipeline", ["operations"]),
    ("example-catalog", ["dev-experience"]),
]


def detect_tier(name):
    for kw, tier in TIER_KEYWORDS:
        if kw in name:
            return tier
    return "foundation"  # fallback


def detect_concerns(name):
    out = set()
    for kw, tags in CONCERN_KEYWORDS:
        if kw in name:
            out.update(tags)
    return sorted(out) if out else []


def detect_amendments(body):
    """Pattern matches '## Amendment A1', 'Amendment A2.3', etc."""
    found = set()
    for m in re.finditer(r"Amendment\s+(A\d+(?:\.\d+)?)", body):
        found.add(m.group(1))
    return sorted(found)


def parse_existing_header(text):
    """Extract (id, title, status, date) from the H1 + Status/Date lines."""
    lines = text.split("\n")
    id_ = None
    title = None
    status = None
    date = None

    h1 = lines[0] if lines else ""
    m = re.match(r"^# ADR[-\s]+(\d+)(?:-A\d+)?\s*[—–\-]\s*(.+)$", h1)
    if m:
        id_ = int(m.group(1))
        title = m.group(2).strip()
        # Strip trailing parens like " (bundled)" if any
    for line in lines[:10]:
        sm = re.match(r"^\*\*Status:\*\*\s*([A-Za-z]+)", line)
        if sm:
            status = sm.group(1)
        dm = re.match(r"^\*\*Date:\*\*\s*(\d{4}-\d{2}-\d{2})", line)
        if dm:
            date = dm.group(1)
    return id_, title, status, date


def make_frontmatter(meta):
    """Generate YAML frontmatter block. Empty arrays use [] inline syntax."""
    lines = ["---"]
    lines.append(f"id: {meta['id']}")
    title = meta['title'].replace("'", "''")
    if ":" in title or "[" in title or "—" in title:
        lines.append(f"title: '{title}'")
    else:
        lines.append(f"title: {title}")
    lines.append(f"status: {meta['status']}")
    lines.append(f"date: {meta['date']}")
    lines.append(f"tier: {meta['tier']}")
    if meta.get("concern"):
        lines.append("concern:")
        for c in meta["concern"]:
            lines.append(f"  - {c}")
    else:
        lines.append("concern: []")
    lines.append("composes: []")
    lines.append("extends: []")
    lines.append("supersedes: []")
    lines.append("superseded_by: null")
    if meta.get("amendments"):
        lines.append("amendments:")
        for a in meta["amendments"]:
            lines.append(f"  - {a}")
    else:
        lines.append("amendments: []")
    lines.append("---")
    lines.append("")
    return "\n".join(lines)


def main():
    files = sorted(ADR_DIR.glob("[0-9][0-9][0-9][0-9]-*.md"))
    skipped = 0
    applied = 0
    failed = []

    for path in files:
        text = path.read_text(encoding="utf-8")
        if text.startswith("---\n"):
            skipped += 1
            continue

        id_, title, status, date = parse_existing_header(text)
        if not all([id_, title, status, date]):
            failed.append(f"{path.name}: incomplete header (id={id_}, title={title!r}, status={status}, date={date})")
            continue

        # Special handling for amendment-promoted-to-file (e.g., 0046-a1-...)
        # — they have id reusing the parent ADR's number; keep that behavior
        # but tier/concern still inferred from name
        meta = {
            "id": id_,
            "title": title,
            "status": status if status in {"Proposed", "Accepted", "Superseded", "Deprecated", "Withdrawn"} else "Accepted",
            "date": date,
            "tier": detect_tier(path.name),
            "concern": detect_concerns(path.name),
            "amendments": detect_amendments(text),
        }
        fm = make_frontmatter(meta)
        path.write_text(fm + text, encoding="utf-8")
        applied += 1

    print(f"Applied frontmatter to {applied} ADRs; skipped {skipped} (already had frontmatter); {len(failed)} failures", file=sys.stderr)
    for f in failed:
        print(f"  FAIL: {f}", file=sys.stderr)
    return 0 if not failed else 1


if __name__ == "__main__":
    sys.exit(main())
