#!/usr/bin/env bash
# Rename Marilo -> Sunfish identifiers and selectors in adapter JS files (in place).
# Token pass first (tokens/selectors/string prefixes), then brand-text pass (PascalCase identifiers
# and bare words). Ordering matters: if brand pass ran first, it could corrupt already-renamed
# --sf-* / .sf-* tokens. The \b word-boundary on sed in Windows Git Bash is unreliable for the
# PascalCase boundary (e.g. MariloDataSheet -> o|D transition), so we also run an unanchored
# Marilo -> Sunfish sweep as a safety net.
#
# Usage: rename-marilo-js.sh <path-to-js-dir>
set -euo pipefail
DIR="${1:?usage: rename-marilo-js.sh <js-dir>}"

mapfile -t FILES < <(find "$DIR" -maxdepth 1 -type f -name '*.js')

for f in "${FILES[@]}"; do
  # Pass 1: tokens and CSS selectors (most specific first).
  sed -i \
    -e 's/--marilo-/--sf-/g' \
    -e 's/\.marilo-/.sf-/g' \
    -e 's/\.mar-/.sf-/g' \
    -e "s/'marilo-/'sf-/g" \
    -e 's/"marilo-/"sf-/g' \
    -e 's/`marilo-/`sf-/g' \
    -e "s/'mar-/'sf-/g" \
    -e 's/"mar-/"sf-/g' \
    -e 's/`mar-/`sf-/g' \
    "$f"

  # Pass 2: brand text. PascalCase compound first (word-boundary anchored), then unanchored
  # safety-net for identifiers the \b pass missed, then lowercase residuals.
  sed -i \
    -e 's/\bMarilo\([A-Z][A-Za-z0-9_]*\)/Sunfish\1/g' \
    -e 's/\bMarilo\b/Sunfish/g' \
    -e 's/Marilo/Sunfish/g' \
    -e 's/marilo/sunfish/g' \
    "$f"

  # Pass 3: repair any over-broad brand substitutions that would have recreated long-form tokens.
  sed -i \
    -e 's/--sunfish-/--sf-/g' \
    -e 's/\.sunfish-/.sf-/g' \
    "$f"
done

echo "Renamed JS content in ${#FILES[@]} files under $DIR"
