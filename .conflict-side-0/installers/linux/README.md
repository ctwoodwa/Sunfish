# Sunfish — Linux .deb Installer (scaffolding)

**Status:** Wave 4.5 scaffolding. Unsigned. Not a production-release artifact.

Paper [§16.1](../../_shared/product/local-node-architecture-paper.md) requires
MDM-compatible silent installation. For Linux fleet management — Jamf Connect
Linux, MicroMDM (limited), GPO-equivalent via Ansible / Puppet / Chef — the
canonical deliverable is a signed `.deb` with a `postinst` that registers a
systemd unit. This folder scaffolds the unsigned `.deb` build.

An `.rpm` variant for RHEL / SUSE fleets is intentionally deferred — the
same `systemd` unit is reusable, the packaging wrapper is not.

## Layout

```
installers/linux/
├── README.md                       (this file)
└── debian/
    ├── build-deb.sh                (orchestration)
    ├── sunfish-local-node.service  (systemd unit)
    └── DEBIAN/                     (control / postinst / postrm — created on build)
```

## Install target

- **Binaries:** `/opt/sunfish/local-node-host/`
- **Service:** `sunfish-local-node.service` — `Type=notify`, `After=network.target`,
  `User=sunfish`, `RestartSec=5`.
- **User data (policy-excluded, paper §16.3):** `$XDG_CONFIG_HOME/sunfish/`
  (falls back to `~/.config/sunfish/` per XDG).
- **Team data (MDM wipe target):** `$XDG_DATA_HOME/sunfish/team-data/`
  (falls back to `~/.local/share/sunfish/team-data/`).
- **MDM pre-seeded config:** `/etc/sunfish/node-config.json` — see
  [`mdm-config-schema.md`](../../docs/specifications/mdm-config-schema.md).

## Prerequisites

- Debian 12+, Ubuntu 22.04+, or compatible derivative.
- `dpkg-deb`, `fakeroot` (usually pre-installed).
- .NET SDK (version pinned in `global.json`).

## Usage

```bash
./installers/linux/debian/build-deb.sh --version 0.1.0-preview
# Output: installers/linux/output/sunfish_0.1.0-preview_amd64.deb
```

## Silent install (MDM / Ansible scenario)

```bash
sudo dpkg -i sunfish_0.1.0-preview_amd64.deb
# postinst creates the `sunfish` user, enables + starts the service.

# Pre-seeded config:
sudo install -m 0644 node-config.json /etc/sunfish/node-config.json
sudo systemctl restart sunfish-local-node
```

## Purge

```bash
# `purge` removes program files + the `sunfish` user.
# It does NOT remove user data under $XDG_DATA_HOME — enterprise wipe of team
# data is a separate, MDM-driven operation (see byod-path-separation.md).
sudo apt-get purge sunfish
```

## Signing (out of scope for Wave 4.5)

Production pass must:

1. Publish to a signed `apt` repository (`dpkg-sig` + `gpg --detach-sign`
   the `Release` file).
2. Include the repo's signing key in the installer's `postinst` for
   subsequent auto-update, or distribute via an internal mirror — see
   [`air-gap-deployment.md`](../../docs/specifications/air-gap-deployment.md).
3. Run `lintian` pre-release and fix any E: errors.

## Known gaps

- Unsigned `.deb`.
- No `.rpm` variant.
- No SELinux / AppArmor policy.
- `postinst` does not register a polkit rule for non-root Anchor
  integration with the service — future wave.
- No `dh_systemd_enable` integration — `postinst` wires systemd manually.
