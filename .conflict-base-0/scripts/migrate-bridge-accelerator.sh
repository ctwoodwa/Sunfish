#!/usr/bin/env bash
# One-shot rename pass for the Bridge accelerator.
# Usage: scripts/migrate-bridge-accelerator.sh
# Idempotent: re-running on an already-migrated tree is a no-op.

set -euo pipefail

DST="${SUNFISH:-C:/Projects/Sunfish}/accelerators/bridge"
[ -d "$DST" ] || { echo "FAIL: $DST not found (run Task 9-1 first)"; exit 1; }

echo "-> Removing backup/scratch files (*.bak, *.orig, *~)"
find "$DST" -type f \( -name "*.bak" -o -name "*.orig" -o -name "*~" \) -delete

echo "-> Rewriting content - code, razor, csproj, json, slnx, md"
find "$DST" -type f \
  \( -name "*.cs" -o -name "*.razor" -o -name "*.razor.cs" -o -name "*.razor.css" \
     -o -name "*.csproj" -o -name "*.json" -o -name "*.slnx" -o -name "*.md" \) \
  -exec sed -i \
    -e 's/\bMarilo\.PmDemo\.AppHost\b/Sunfish.Bridge.AppHost/g' \
    -e 's/\bMarilo\.PmDemo\.Client\b/Sunfish.Bridge.Client/g' \
    -e 's/\bMarilo\.PmDemo\.Data\b/Sunfish.Bridge.Data/g' \
    -e 's/\bMarilo\.PmDemo\.MigrationService\b/Sunfish.Bridge.MigrationService/g' \
    -e 's/\bMarilo\.PmDemo\.ServiceDefaults\b/Sunfish.Bridge.ServiceDefaults/g' \
    -e 's/\bMarilo\.PmDemo\.Tests\.Unit\b/Sunfish.Bridge.Tests.Unit/g' \
    -e 's/\bMarilo\.PmDemo\.Tests\.Integration\b/Sunfish.Bridge.Tests.Integration/g' \
    -e 's/\bMarilo\.PmDemo\.Tests\.Performance\b/Sunfish.Bridge.Tests.Performance/g' \
    -e 's/\bMarilo\.PmDemo\b/Sunfish.Bridge/g' \
    -e 's/\bMarilo_PmDemo\b/Sunfish_Bridge/g' \
    -e 's/\bPmDemoDbContext\b/SunfishBridgeDbContext/g' \
    -e 's/\bPmDemoHub\b/BridgeHub/g' \
    -e 's/\bIPmDemoHubClient\b/IBridgeHubClient/g' \
    -e 's/\bPmDemoSeeder\b/BridgeSeeder/g' \
    -e 's/\bMarilo\.Core\.Contracts\b/Sunfish.UICore.Contracts/g' \
    -e 's/\bMarilo\.Core\.Services\b/Sunfish.Foundation.Services/g' \
    -e 's/\bMarilo\.Core\.Extensions\b/Sunfish.Foundation.Extensions/g' \
    -e 's/\bMarilo\.Core\.Models\b/Sunfish.Foundation.Models/g' \
    -e 's/\bMarilo\.Core\.Enums\b/Sunfish.Foundation.Enums/g' \
    -e 's/\bMarilo\.Components\.Shell\b/Sunfish.Components.Blazor.Shell/g' \
    -e 's/\bMarilo\.Components\b/Sunfish.Components.Blazor/g' \
    -e 's/\bMarilo\.Providers\.FluentUI\b/Sunfish.Components.Blazor.Providers.FluentUI/g' \
    -e 's/\bMarilo\.Providers\.Bootstrap\b/Sunfish.Components.Blazor.Providers.Bootstrap/g' \
    -e 's/\bMarilo\.Providers\.Material\b/Sunfish.Components.Blazor.Providers.Material/g' \
    -e 's/\bIMariloCssProvider\b/ISunfishCssProvider/g' \
    -e 's/\bIMariloIconProvider\b/ISunfishIconProvider/g' \
    -e 's/\bIMariloJsInterop\b/ISunfishJsInterop/g' \
    -e 's/\bIMariloNotificationService\b/ISunfishNotificationService/g' \
    -e 's/\bIMariloThemeService\b/ISunfishThemeService/g' \
    -e 's/\bMariloNotificationService\b/SunfishNotificationService/g' \
    -e 's/\bMariloToastUserNotificationForwarder\b/SunfishToastUserNotificationForwarder/g' \
    -e 's/\bAddMariloCoreServices\b/AddSunfishCoreServices/g' \
    -e 's/\bAddMarilo\b/AddSunfish/g' \
    -e 's/\bMariloComponentBase\b/SunfishComponentBase/g' \
    -e 's/\bMariloTheme\b/SunfishTheme/g' \
    -e 's/\bMarilo\b/Sunfish/g' \
    -e 's/class="mar-/class="sf-/g' \
    -e 's/class="marilo-/class="sf-/g' \
    -e "s/class='mar-/class='sf-/g" \
    -e "s/class='marilo-/class='sf-/g" \
    {} \;

echo "-> Patching plain .cs files that inherit SunfishComponentBase"
find "$DST" -type f \( -name "*.razor.cs" -o -name "*.cs" \) ! -name "*.AssemblyInfo.cs" | while read -r f; do
  if grep -q "SunfishComponentBase" "$f" && ! grep -q "using Sunfish\.Components\.Blazor\.Base;" "$f"; then
    if grep -q "^using Sunfish\.Foundation\.Base;" "$f"; then
      sed -i '0,/^using Sunfish\.Foundation\.Base;/{s//using Sunfish.Foundation.Base;\nusing Sunfish.Components.Blazor.Base;/}' "$f"
    else
      sed -i '1i using Sunfish.Components.Blazor.Base;' "$f"
    fi
  fi
done

echo "-> Contamination gate - grep for surviving Marilo identifiers"
if grep -rE '\bMarilo[A-Za-z]|Marilo\.(Core|Components|Providers|PmDemo)' "$DST" \
       --include='*.cs' --include='*.razor' --include='*.razor.cs' \
       --include='*.csproj' --include='*.json' --include='*.slnx'; then
  echo "FAIL: 'Marilo' identifiers remain in $DST"
  exit 1
fi

echo "OK: Bridge accelerator rename complete"
