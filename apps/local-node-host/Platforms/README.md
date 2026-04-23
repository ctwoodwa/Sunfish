# Platforms — service-manager integration

**Status:** Wave 4.5 — delegates platform packaging to `installers/*`.

Paper §4 calls for the local-node host to run as a persistent background
service under each OS's service manager. Wave 2.5 ships the host process
itself — a standard .NET generic-host Worker Service. Wave 4.5 ships the
platform-packaging scaffolding that installs that process as a service.

The service-manager artifacts no longer live in this folder. They live
next to the installer scripts that consume them:

| Platform | Service manager | Installer location                                                         | Service-manager artifact |
|----------|-----------------|----------------------------------------------------------------------------|--------------------------|
| Windows  | Windows Service | [`installers/windows/`](../../../installers/windows/README.md)             | WiX v4 authoring inlined into `build-msi.ps1`; registers `SunfishLocalNode` via `ServiceInstall`. |
| Linux    | systemd         | [`installers/linux/`](../../../installers/linux/README.md)                 | [`debian/sunfish-local-node.service`](../../../installers/linux/debian/sunfish-local-node.service) |
| macOS    | launchd         | [`installers/macos/`](../../../installers/macos/README.md)                 | [`launchd/com.sunfish.local-node-host.plist`](../../../installers/macos/launchd/com.sunfish.local-node-host.plist) |

## Related specifications

- [`docs/specifications/air-gap-deployment.md`](../../../docs/specifications/air-gap-deployment.md)
  — operating the host with no internet access.
- [`docs/specifications/mdm-config-schema.md`](../../../docs/specifications/mdm-config-schema.md)
  — pre-seeded `node-config.json` schema.
- [`docs/specifications/byod-path-separation.md`](../../../docs/specifications/byod-path-separation.md)
  — team-data vs. user-preferences path split; paper §16.3.

## Why this folder still exists

Kept as a stable in-tree anchor for future host-code that is genuinely
platform-specific (e.g., a Windows Service installer helper CLI, or
launchd notify-socket glue for macOS). For now it is documentation only.

## Carve-outs (still deferred)

- Code signing of the built installers — separate operational workstream.
- MDM-vendor-specific wrappers (Intune `.intunewin`, Jamf `.mobileconfig`).
- Auto-update implementation — design spec in `air-gap-deployment.md` §3.3.
- macOS notarization / stapling pipeline.
