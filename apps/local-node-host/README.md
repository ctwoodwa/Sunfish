# Sunfish Local-Node Host

The headless background process that hosts the Sunfish kernel runtime.
Wave 2.5 of the [paper-alignment plan](../../_shared/product/paper-alignment-plan.md).

## What it does

Paper [§4](../../_shared/product/local-node-architecture-paper.md) and
[§5.1](../../_shared/product/local-node-architecture-paper.md) describe the
local-node kernel as a persistent background service. This app is that
service. On start-up it composes and hosts:

- **Plugin registry + `INodeHost`** — lifecycle + extension-point contracts
  (Wave 1.1, `packages/kernel-runtime`)
- **Event log** — paper §2.5 / §8 persistent append-only log
  (Wave 1.3, `packages/kernel-event-bus`)
- **Encrypted local store** — SQLCipher + Argon2id + platform keystore
  (Wave 1.4, `packages/foundation-localfirst/Encryption`)
- **Quarantine queue** — paper §11.2 Layer 4 holding pen for failed offline writes
  (Wave 1.5, `packages/foundation-localfirst/Quarantine`)
- **Security primitives** — Ed25519 signing, X25519 key agreement, role keys
  (Wave 1.6, `packages/kernel-security`)
- **CRDT engine** — document abstraction over the chosen CRDT backend
  (Wave 1.2, `packages/kernel-crdt`)

At idle the host parks on `Task.Delay(Timeout.Infinite, stoppingToken)` so
it consumes no CPU while waiting for work. All actual per-tick activity
(event dispatch, sync gossip, projection rebuilds) is driven by the kernel
itself.

## Running locally

```bash
dotnet run --project apps/local-node-host/Sunfish.LocalNodeHost.csproj
```

Stop with `Ctrl+C`. The worker traps cancellation, tears the node host down
through `Running → Stopping → Stopped`, then unloads plugins in reverse
topological order.

## Configuration

`appsettings.json` exposes the `LocalNode` section:

```json
{
  "LocalNode": {
    "NodeId": "…generated on first boot…",
    "TeamId": null,
    "DataDirectory": "…platform default…"
  }
}
```

Platform-conventional `DataDirectory` defaults:

| Platform | Default |
|---|---|
| Windows | `%LOCALAPPDATA%\Sunfish\LocalNode` |
| macOS | `~/Library/Application Support/Sunfish/LocalNode` |
| Linux | `$XDG_DATA_HOME/sunfish/local-node` (falls back to `~/.local/share/sunfish/local-node`) |

## Service-manager integration (roadmap — Wave 4)

Paper §4: *"run the container stack as a persistent background service
registered with the OS service manager (systemd on Linux, launchd on macOS,
Windows Service on Windows), starting at login and running quietly at idle."*

Wave 2.5 (this wave) ships the headless host **process**. Wave 4.5 is
expected to add:

- `Platforms/linux/sunfish-local-node.service` — systemd unit, user-scoped
- `Platforms/macos/com.sunfish.localnode.plist` — launchd agent (LaunchAgent)
- `Platforms/windows/SunfishLocalNode.wxs` — Windows Service installer or WiX
  fragment

Because the host is a generic-host Worker Service, no host-code changes are
needed to register under any of these service managers — the same binary runs
under `dotnet run` during development and under the service manager in
production. See [`Platforms/README.md`](Platforms/README.md).

## How other processes connect

The application shell (Anchor — Wave 3.3) and the relay-mode Bridge do not
re-host the kernel. They connect to **this** already-running process over
the sync-daemon transport (Unix domain socket on macOS / Linux, named pipe
on Windows). The transport itself lands in Wave 2.1; until that wave lands
the host is usable only for in-process scenarios and integration tests.

## What does not live here

- Platform-specific service installers → Wave 4.5 (`Platforms/…` stubs only)
- Sync-daemon transport listener → Wave 2.1 (`packages/kernel-sync-daemon/`)
- Anchor shell → Wave 3.3 (`accelerators/anchor/`)
- Example / seed plugins → not this wave; empty plugin set is the expected
  boot state. The kernel logs `Loaded 0 plugin(s)` and moves on.
