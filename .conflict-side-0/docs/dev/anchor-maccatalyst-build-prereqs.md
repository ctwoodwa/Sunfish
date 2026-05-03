# Anchor MAUI Build Prerequisites — macOS

Anchor (`accelerators/anchor/Sunfish.Anchor.csproj`) targets `net11.0-maccatalyst` on macOS in addition to `net11.0-windows*` on Windows. Building the macOS `.app` bundle requires three host-environment prerequisites that are not captured in the csproj because they're per-developer system state. Run each once after a fresh macOS workstation setup or after upgrading Xcode.

## 1. Xcode license accepted

The .NET MAUI MacCatalyst SDK invokes `xcodebuild` during asset compilation. After installing or upgrading Xcode, `xcodebuild` refuses to run until the license has been re-accepted, surfacing as cryptic `actool exited with code 72` errors during the MAUI build (with no obvious license-prompt message).

```sh
sudo xcodebuild -license accept
```

Verify:

```sh
defaults read /Library/Preferences/com.apple.dt.Xcode IDEXcodeVersionForAgreedToGMLicense
# Should print the current Xcode major version (e.g. "26.4")
```

## 2. `xcode-select` symlink points at the active Xcode

Some upgrade paths (drag-from-Downloads, Homebrew Xcode rotation, replacing a beta) leave `/var/db/xcode_select_link` pointing at the old install location. The symlink is what most build tools resolve before falling back to `xcode-select -p`'s lookup heuristics, so a stale link surfaces as `xcrun: error: unable to find utility "actool"` even when `xcode-select -p` reports the right path.

```sh
sudo xcode-select -switch /Applications/Xcode.app/Contents/Developer
```

Verify:

```sh
readlink /var/db/xcode_select_link
# Should print: /Applications/Xcode.app/Contents/Developer
```

## 3. Xamarin `Settings.plist` `AppleSdkRoot` uses canonical case

`~/Library/Preferences/Xamarin/Settings.plist` caches the Xcode bundle path the first time the .NET MAUI MacCatalyst tooling probes for one. If that probe captured a non-canonical case (e.g. `/Applications/xcode.app/`, lowercase), the plist persists the lowercase form. The MAUI MSBuild tasks then forward this lowercase `DEVELOPER_DIR` to `xcrun`, which fails inside `xcodebuild` with `SDK "/Applications/xcode.app/.../MacOSX.sdk" cannot be located` even though APFS is case-insensitive — `xcodebuild`'s SDK lookup is more strict than the filesystem.

```sh
plutil -replace AppleSdkRoot \
  -string "/Applications/Xcode.app/" \
  ~/Library/Preferences/Xamarin/Settings.plist
```

If the plist doesn't exist yet, the MAUI tooling will create it on first build using whatever case it discovers — usually fine. Only run this if the probe captured a non-canonical case.

Verify:

```sh
plutil -p ~/Library/Preferences/Xamarin/Settings.plist
# Should print: "AppleSdkRoot" => "/Applications/Xcode.app/"
```

## 4. .NET workload installed

```sh
dotnet workload install maui-maccatalyst
```

Verify:

```sh
dotnet workload list
# Should list maui-maccatalyst with a manifest version matching the MAUI workload pinned in the SDK.
```

## Build

Once all four are in place:

```sh
dotnet build accelerators/anchor/Sunfish.Anchor.csproj -c Debug
```

Build outputs land at `accelerators/anchor/bin/Debug/net11.0-maccatalyst/maccatalyst-x64/Sunfish Anchor.app`.

## Known harmless build-output warnings

The MAUI Catalyst runtime resolver currently emits ~36 `warning : The file 'X.json' does not specify a 'PublishFolderType' metadata` messages for static-web-asset cache JSON files in transitive `ui-adapters-blazor/tests` output directories. The files are cache artifacts that shouldn't ship in the bundle and the warning correctly skips them. Tracked upstream — the warning will quiet down once the MAUI workload's Xamarin.Shared.Sdk.targets adds default `PublishFolderType` handling for these artifact types.
