# Sunfish — macOS .pkg Installer (scaffolding)

**Status:** Wave 4.5 scaffolding. Unsigned. Not a production-release artifact.

Paper [§16.1](../../_shared/product/local-node-architecture-paper.md) requires
MDM-compatible silent installation. On macOS the canonical MDM-friendly
format is a signed, notarized `.pkg` consumed by Jamf, Kandji, Mosyle, or
Apple Business Manager. This folder scaffolds the unsigned variant.

## Layout

```
installers/macos/
├── README.md                              (this file)
├── build-pkg.sh                           (orchestration)
├── launchd/
│   └── com.sunfish.local-node-host.plist  (LaunchDaemon)
└── scripts/                               (preinstall / postinstall — generated)
```

## Install target

- **Anchor shell (desktop GUI, Wave 3):** `/Applications/Sunfish Anchor.app`
- **Local-node host (headless):** `/Library/Application Support/Sunfish/LocalNodeHost/`
- **LaunchDaemon:** `/Library/LaunchDaemons/com.sunfish.local-node-host.plist`
  (system-wide, runs at boot; no user login required).
- **User data (policy-excluded, paper §16.3):**
  - `~/Library/Preferences/com.sunfish.anchor.plist` (UI prefs)
  - `~/Library/Application Support/Sunfish/UserPreferences/`
- **Team data (MDM wipe target):**
  - `~/Library/Application Support/Sunfish/TeamData/`
- **MDM pre-seeded config:** `/Library/Application Support/Sunfish/node-config.json`
  — see [`mdm-config-schema.md`](../../docs/specifications/mdm-config-schema.md).

## Prerequisites

- macOS 13+ (the supported floor for the paper's crypto choices).
- Xcode Command Line Tools (`xcode-select --install`) — provides `pkgbuild`
  and `productbuild`.
- .NET SDK (version pinned in `global.json`).

## Usage

```bash
./installers/macos/build-pkg.sh --version 0.1.0-preview
# Output: installers/macos/output/Sunfish-0.1.0-preview.pkg
```

## Silent install (MDM scenario)

```bash
sudo installer -pkg Sunfish-0.1.0-preview.pkg -target /
```

MDM vendors (Jamf / Kandji / Mosyle) all accept the resulting `.pkg` as a
distribution payload. A signed + notarized build is required for Gatekeeper
to allow silent install without user interaction; this scaffolding build
will be blocked by Gatekeeper unless run from a trusted vendor.

## Uninstall

Uninstall is currently **manual** — a dedicated `uninstall.pkg` is a
separate work item. Admins may run:

```bash
# Stop + unload the LaunchDaemon.
sudo launchctl bootout system/com.sunfish.local-node-host || true

# Remove program files.
sudo rm -rf "/Library/Application Support/Sunfish/LocalNodeHost"
sudo rm -rf "/Applications/Sunfish Anchor.app"
sudo rm -f "/Library/LaunchDaemons/com.sunfish.local-node-host.plist"

# NOTE: user data under ~/Library/Application Support/Sunfish/TeamData is
# NOT removed by this script. Enterprise wipe is a separate operation —
# see docs/specifications/byod-path-separation.md.
```

## Signing + notarization (out of scope for Wave 4.5)

A production pass must:

1. Build with `pkgbuild --sign "Developer ID Installer: Sunfish LLC (TEAMID)"`.
2. Wrap with `productbuild --sign` using the same Developer ID Installer cert.
3. Submit to Apple's notary service: `xcrun notarytool submit` then
   `xcrun stapler staple`.
4. Verify via `spctl --assess --type install <pkg>`.

## Known gaps

- Unsigned + un-notarized — Gatekeeper will block silent install.
- No `uninstall.pkg`.
- No MDM config profile (`.mobileconfig`) template — those are per-vendor.
- No Universal Binary — build is host-arch only; CI matrix pass owed.
- `Sunfish Anchor.app` is currently built by `dotnet publish` for MAUI;
  a separate Xcode-archive pipeline is likely required for proper App
  Store distribution.
