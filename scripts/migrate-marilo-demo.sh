#!/usr/bin/env bash
# Usage: scripts/migrate-marilo-demo.sh
# Prerequisites: SUNFISH and MARILO env vars exported.
# Copies Marilo.Demo -> apps/kitchen-sink and applies the Sunfish rename pass.

set -euo pipefail

SRC="$MARILO/samples/Marilo.Demo"
DST="$SUNFISH/apps/kitchen-sink"

[ -d "$SRC" ] || { echo "FAIL: Marilo.Demo not found: $SRC"; exit 1; }

# Do NOT fail if DST exists — only fail if already populated beyond README
if [ -d "$DST" ] && [ "$(ls -A "$DST" 2>/dev/null | grep -v '^README' | wc -l)" -gt 0 ]; then
  echo "FAIL: $DST already populated (migration re-run?)"
  exit 1
fi

echo "→ Copying Marilo.Demo → apps/kitchen-sink"
mkdir -p "$DST"
# Copy everything except bin, obj, and csproj (we'll write the csproj fresh)
rsync -a \
  --exclude="bin/" --exclude="obj/" --exclude="Marilo.Demo.csproj" \
  "$SRC/" "$DST/"

echo "→ Removing backup/scratch files"
find "$DST" -type f \( -name "*.bak" -o -name "*.orig" -o -name "*~" \) -delete

echo "→ Renaming Marilo-prefixed files (Marilo.Demo.styles.css etc.)"
find "$DST" -type f -name "Marilo*" | while read -r f; do
  new="$(dirname "$f")/$(basename "$f" | sed 's/^Marilo/Sunfish/')"
  mv "$f" "$new"
done

echo "→ Renaming wwwroot/icons/marilo-icons.json"
if [ -f "$DST/wwwroot/icons/marilo-icons.json" ]; then
  mv "$DST/wwwroot/icons/marilo-icons.json" "$DST/wwwroot/icons/sunfish-icons.json"
fi

echo "→ Rewriting content (sed pass — code & razor)"
find "$DST" -type f \( -name "*.razor" -o -name "*.cs" -o -name "*.razor.cs" -o -name "*.json" \) -exec sed -i \
  -e 's/\bIMarilo/ISunfish/g' \
  -e 's/\bMarilo\.Demo\b/Sunfish.KitchenSink/g' \
  -e 's/\bMarilo\.Core\.Contracts/Sunfish.UICore.Contracts/g' \
  -e 's/\bMarilo\.Core\./Sunfish.Foundation./g' \
  -e 's/\bMarilo\.Components\.Internal\b/Sunfish.Components.Blazor.Internal/g' \
  -e 's/\bMarilo\.Components\./Sunfish.Components.Blazor.Components./g' \
  -e 's/\bMarilo\.Components\b/Sunfish.Components.Blazor/g' \
  -e 's/\bMarilo\.Providers\.FluentUI/Sunfish.Providers.FluentUI/g' \
  -e 's/\bMarilo\.Providers\.Bootstrap/Sunfish.Providers.Bootstrap/g' \
  -e 's/\bMarilo\.Providers\.Material/Sunfish.Providers.Material/g' \
  -e 's/@inherits MariloComponentBase/@inherits SunfishComponentBase/g' \
  -e 's/\bMariloResizeEdges\b/ResizeEdges/g' \
  -e 's/\bMarilo/Sunfish/g' \
  -e 's/class="mar-/class="sf-/g' \
  -e 's/class="marilo-/class="sf-/g' \
  -e "s/class='mar-/class='sf-/g" \
  -e "s/class='marilo-/class='sf-/g" \
  -e "s/'marilo:/'sunfish:/g" \
  -e 's/"marilo:/"sunfish:/g' \
  -e 's/marilo-provider-/sunfish-provider-/g' \
  -e 's/_content\/Marilo\.Providers\./_content\/Sunfish.Providers./g' \
  -e 's/css\/marilo-fluentui\.css/css\/sunfish-fluentui.css/g' \
  -e 's/css\/marilo-bootstrap\.css/css\/sunfish-bootstrap.css/g' \
  -e 's/css\/marilo-material\.css/css\/sunfish-material.css/g' \
  {} \;

echo "→ Rewriting CSS and HTML files"
find "$DST" -type f \( -name "*.css" -o -name "*.html" -o -name "*.cshtml" \) -exec sed -i \
  -e 's/\bMarilo Demo\b/Sunfish Kitchen Sink/g' \
  -e 's/\bMarilo\b/Sunfish/g' \
  -e 's/class="mar-/class="sf-/g' \
  -e 's/class="marilo-/class="sf-/g' \
  {} \;

echo "→ Rewriting JS storage keys and identifiers"
find "$DST/wwwroot/js" -type f -name "*.js" -exec sed -i \
  -e "s/'marilo:/'sunfish:/g" \
  -e 's/"marilo:/"sunfish:/g' \
  -e 's/marilo-provider-/sunfish-provider-/g' \
  -e 's/window\.Marilo\b/window.Sunfish/g' \
  -e 's/\bMarilo\b/Sunfish/g' \
  {} \;

echo "→ Grepping for leftover Marilo references (informational)"
if grep -rE '\bMarilo[A-Za-z]|\bMarilo\.' "$DST" 2>/dev/null | grep -v "^Binary" | head -20; then
  echo "WARN: leftover 'Marilo' identifiers remain (review manually)"
fi

echo "OK: Marilo.Demo migration complete → $DST"
