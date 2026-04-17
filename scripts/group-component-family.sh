#!/usr/bin/env bash
# Usage: scripts/group-component-family.sh <Category> <FamilyFolder> <file1> [file2 ...]
# Example: scripts/group-component-family.sh Buttons Chip SunfishChip.razor SunfishChipSet.razor
#
# Moves the named files from Components/<Category>/ into a
# Components/<Category>/<FamilyFolder>/ subfolder via `git mv` and ensures
# every .razor file has the flat-per-category @namespace directive.
#
# Assumes you're on a feature branch; does NOT commit.

set -euo pipefail

CATEGORY="${1:?category required}"
FAMILY="${2:?family folder required}"
shift 2

CAT_DIR="packages/ui-adapters-blazor/Components/$CATEGORY"
DST="$CAT_DIR/$FAMILY"
NS="Sunfish.Components.Blazor.Components.$CATEGORY"

[ -d "$CAT_DIR" ] || { echo "FAIL: category not found: $CAT_DIR"; exit 1; }
mkdir -p "$DST"

for f in "$@"; do
  src="$CAT_DIR/$f"
  [ -f "$src" ] || { echo "SKIP (not found): $src"; continue; }
  git mv "$src" "$DST/$f"
  echo "  moved: $f"
done

# Ensure every .razor in DST has a @namespace directive — Razor SDK would
# otherwise infer a nested namespace from the folder path.
for r in "$DST"/*.razor; do
  [ -f "$r" ] || continue
  if ! head -1 "$r" | grep -q "^@namespace"; then
    sed -i "1i @namespace $NS" "$r"
    echo "  @namespace added: $(basename "$r")"
  fi
done

echo "OK: $CATEGORY/$FAMILY ($(ls "$DST" | wc -l) files)"
