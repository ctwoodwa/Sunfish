# Sunfish — Windows MSI Installer (scaffolding)

**Status:** Wave 4.5 scaffolding. Unsigned. Not a production-release artifact.

Paper [§16.1](../../_shared/product/local-node-architecture-paper.md) requires
MDM-compatible silent installation. This folder contains the starting point:
a WiX Toolset v4 authoring skeleton that an ops engineer can extend into a
signed, distributable MSI. Full code-signing (signtool with an EV cert),
Intune-specific `IntuneWinAppUtil` packaging, and MSIX conversion are
follow-up workstreams.

## Layout

```
installers/windows/
├── README.md                (this file)
├── build-msi.ps1            (orchestration script)
└── wix/                     (WiX v4 authoring — created on first build)
    ├── SunfishLocalNodeHost.wxs
    └── bundle.wxs
```

`wix/` is generated on first invocation of `build-msi.ps1` rather than
checked in, because the `.wxs` authoring needs to reference the absolute
publish path and the current package version. Regenerate with the script.

## Install target

- **Binaries:** `%PROGRAMFILES%\Sunfish\LocalNodeHost\` (HKLM, per-machine)
- **Service:** `SunfishLocalNode` — `Automatic` start type, registered via
  `sc.exe create` (the WiX `ServiceInstall` element wraps this).
- **User data (policy-excluded, see paper §16.3):** `%LOCALAPPDATA%\Sunfish\`
  — untouched by uninstall / enterprise wipe of the product.
- **Team data (MDM wipe target):** `%LOCALAPPDATA%\Sunfish\TeamData\` —
  see [`byod-path-separation.md`](../../docs/specifications/byod-path-separation.md).
- **MDM pre-seeded config:** `%PROGRAMDATA%\Sunfish\node-config.json` — see
  [`mdm-config-schema.md`](../../docs/specifications/mdm-config-schema.md).

## Prerequisites

- Windows 10 / 11 or Windows Server 2019+
- .NET SDK (version pinned in `global.json`)
- WiX Toolset v4 as a local dotnet tool: `dotnet tool install --global wix`
- PowerShell 7+ (`pwsh`)

## Usage

```powershell
# Silent build, no interaction. Output: installers/windows/output/Sunfish.msi
pwsh -File installers/windows/build-msi.ps1 -Version 0.1.0-preview
```

## Silent install (MDM scenario)

```powershell
msiexec /i Sunfish.msi /qn /norestart `
    INSTALLFOLDER="C:\Program Files\Sunfish\LocalNodeHost" `
    SERVICESTART=1
```

MDM-specific options:

| Property           | Meaning                                              |
|--------------------|------------------------------------------------------|
| `INSTALLFOLDER`    | Override install path (default `%PROGRAMFILES%\Sunfish\LocalNodeHost`). |
| `SERVICESTART`     | `1` to start service post-install, `0` to defer.     |
| `PRESEEDCONFIG`    | Path to a `node-config.json` to drop into `%PROGRAMDATA%\Sunfish\`. |

## Silent uninstall

```powershell
msiexec /x Sunfish.msi /qn /norestart
```

By design, uninstall **does not touch** `%LOCALAPPDATA%\Sunfish\`. Admins
performing an enterprise wipe should target the
[BYOD wipe paths](../../docs/specifications/byod-path-separation.md) directly.

## Signing (out of scope for Wave 4.5)

`build-msi.ps1` has a commented `Invoke-SignTool` call site. A production
pass must:

1. Fetch the signing cert from the org's HSM (Azure Key Vault,
   YubiKey, etc.) — never bake the PFX into the repo.
2. Invoke `signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td
   SHA256 /a Sunfish.msi` on the built MSI.
3. Verify with `signtool verify /pa /v Sunfish.msi`.
4. Optionally convert to MSIX for Microsoft Store / Intune distribution
   via `MSIX Packaging Tool`.

## Known gaps (explicit, for the follow-up wave)

- Not signed.
- No Intune `.intunewin` wrapping.
- No MSIX variant.
- No group-policy ADMX template.
- No WER / ETW manifest registration.
- No `Platforms/windows/SunfishLocalNode.wxs` fragment referenced from
  `apps/local-node-host/Platforms/README.md` — this installer pulls its
  author-time WiX inline for now; a future refactor should extract the
  service-manager fragment into the app.
