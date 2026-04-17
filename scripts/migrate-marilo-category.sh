#!/usr/bin/env bash
# Usage: scripts/migrate-marilo-category.sh <category-name>
# Example: scripts/migrate-marilo-category.sh Buttons
#
# Prerequisites: SUNFISH and MARILO env vars exported.

set -euo pipefail

CATEGORY="${1:?category name required, e.g. Buttons}"
SRC="$MARILO/src/Marilo.Components/$CATEGORY"
DST="$SUNFISH/packages/ui-adapters-blazor/Components/$CATEGORY"

[ -d "$SRC" ] || { echo "FAIL: Marilo category not found: $SRC"; exit 1; }
[ ! -d "$DST" ] || { echo "FAIL: Sunfish category already exists: $DST (migration re-run?)"; exit 1; }

echo "→ Copying $CATEGORY: $SRC → $DST"
mkdir -p "$DST"
cp -r "$SRC/." "$DST/"

echo "→ Renaming Marilo-prefixed files"
find "$DST" -type f \( -name "Marilo*.razor" -o -name "Marilo*.cs" \) | while read -r f; do
  new="$(dirname "$f")/$(basename "$f" | sed 's/^Marilo/Sunfish/')"
  mv "$f" "$new"
done

echo "→ Rewriting content (sed pass — code files)"
find "$DST" -type f \( -name "*.razor" -o -name "*.cs" -o -name "*.razor.cs" \) -exec sed -i \
  -e 's/\bIMarilo/ISunfish/g' \
  -e 's/\bMarilo\.Core\.Contracts/Sunfish.UICore.Contracts/g' \
  -e 's/\bMarilo\.Core\./Sunfish.Foundation./g' \
  -e 's/\bMarilo\.Components\.Internal\b/Sunfish.Components.Blazor.Internal/g' \
  -e 's/\bMarilo\.Components\./Sunfish.Components.Blazor.Components./g' \
  -e 's/namespace Marilo\.Components;/namespace Sunfish.Components.Blazor;/g' \
  -e 's/@inherits MariloComponentBase/@inherits SunfishComponentBase/g' \
  -e 's/\bMariloResizeEdges\b/ResizeEdges/g' \
  -e 's/\bMarilo/Sunfish/g' \
  -e 's/class="mar-/class="sf-/g' \
  -e 's/class="marilo-/class="sf-/g' \
  -e "s/class='mar-/class='sf-/g" \
  -e "s/class='marilo-/class='sf-/g" \
  {} \;

# Markdown research/gap-analysis files also ship with categories.
# Narrow sed: rename identifier references; leave prose intact.
echo "→ Rewriting content (sed pass — markdown docs)"
find "$DST" -type f -name "*.md" -exec sed -i \
  -e 's/\bIMarilo/ISunfish/g' \
  -e 's/\bMariloResizeEdges\b/ResizeEdges/g' \
  -e 's/\bMarilo/Sunfish/g' \
  {} \;

# Component code-behind (.razor.cs) files that inherit SunfishComponentBase
# need an explicit using for Sunfish.Components.Blazor.Base (where that type
# lives — NOT Foundation.Base, which holds the framework-agnostic CssClassBuilder).
# Marilo had both in Marilo.Core.Base, so the original only needed one using.
# Insert after the (already renamed) Sunfish.Foundation.Base line if missing.
echo "→ Patching code-behind usings for SunfishComponentBase"
find "$DST" -name "*.razor.cs" | while read -r f; do
  if grep -q "SunfishComponentBase" "$f" && ! grep -q "using Sunfish\.Components\.Blazor\.Base;" "$f"; then
    sed -i '0,/^using Sunfish\.Foundation\.Base;/{s//using Sunfish.Foundation.Base;\nusing Sunfish.Components.Blazor.Base;/}' "$f"
  fi
done

echo "→ Grepping for leftover Marilo references"
if grep -rE '\bMarilo[A-Za-z]|Marilo\.(Core|Components)' "$DST"; then
  echo "FAIL: 'Marilo' identifiers remain in $DST"
  exit 1
fi

echo "OK: $CATEGORY migration complete"
