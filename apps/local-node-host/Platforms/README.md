# Platforms — service-manager integration stubs

**Status:** placeholder. Wave 4.5 territory.

Paper §4 calls for the local-node host to run as a persistent background
service under each OS's service manager:

| Platform | Service manager | Expected artifact (Wave 4.5) |
|---|---|---|
| Linux | systemd (user scope) | `linux/sunfish-local-node.service` |
| macOS | launchd (LaunchAgent) | `macos/com.sunfish.localnode.plist` |
| Windows | Windows Service (per-user where possible) | `windows/SunfishLocalNode.wxs` + installer target |

Wave 2.5 (the current wave) ships the host process itself — a standard .NET
generic-host Worker Service. No host-code changes are required to run under
any of the above; service-manager integration is a packaging concern layered
on top of the binary produced by `dotnet publish`.

## When Wave 4.5 lands

1. Each sub-folder gets the appropriate unit / plist / installer fragment.
2. An installer tool (`tooling/local-node-installer/`, out of scope here)
   renders the template against the user's install path and registers the
   service.
3. The CI publish pipeline produces per-OS install bundles.

Until then this folder is intentionally empty apart from this README so the
project layout already signals where those artifacts will live.
