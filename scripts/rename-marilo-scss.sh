#!/usr/bin/env bash
# Rename Marilo -> Sunfish tokens/selectors in an SCSS tree (in place).
# Usage: rename-marilo-scss.sh <path-to-styles-dir>
set -euo pipefail
DIR="${1:?usage: rename-marilo-scss.sh <styles-dir>}"

mapfile -t FILES < <(find "$DIR" -type f -name '*.scss')

# Order matters:
#   --marilo- first (longest / CSS custom properties)
#   .marilo-  (selectors)
#   \bmarilo- (bare mixin/function names)
#   .mar-     (BEM block selectors)
#   \bmar-    (bare mar- tokens)
for f in "${FILES[@]}"; do
  sed -i \
    -e 's/--marilo-/--sf-/g' \
    -e 's/\.marilo-/.sf-/g' \
    -e 's/\bmarilo-/sf-/g' \
    -e 's/\.mar-/.sf-/g' \
    -e 's/\bmar-/sf-/g' \
    "$f"
done

echo "Renamed tokens/selectors in ${#FILES[@]} SCSS files under $DIR"
