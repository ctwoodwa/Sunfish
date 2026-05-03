# Anchor Demo-Readiness

**Snapshot date:** 2026-04-29

This page captures Anchor's current ability to demo the property-management vertical (Properties / Equipment / Inspections) **as observed from the Sunfish repo today**. It exists to answer the question "if I clone Sunfish and `dotnet build accelerators/anchor/` on a fresh box, what do I see?" without having to re-derive the answer.

## Build status

### macOS (Mac Catalyst)

`dotnet build accelerators/anchor/Sunfish.Anchor.csproj -c Debug` builds successfully on macOS once the four prerequisites in [`docs/dev/anchor-maccatalyst-build-prereqs.md`](../../../../docs/dev/anchor-maccatalyst-build-prereqs.md) are in place:

1. `sudo xcodebuild -license accept`
2. `sudo xcode-select -switch /Applications/Xcode.app/Contents/Developer`
3. `Xamarin Settings.plist` `AppleSdkRoot` uses canonical case
4. `dotnet workload install maui-maccatalyst`

Output bundle: `accelerators/anchor/bin/Debug/net11.0-maccatalyst/maccatalyst-x64/Sunfish Anchor.app/`.

Build emits ~80 warnings, all in two categories documented as harmless under the `## Known harmless build-output warnings` heading of the prereqs doc:

- `NETSDK1206` — `linux-x64-musl` RID for `YDotNet.Native.Linux` (irrelevant on macOS)
- "does not specify a `'PublishFolderType'` metadata" — transitive `ui-adapters-blazor/tests` static-web-asset cache JSON

### Windows

Anchor targets `net11.0-windows10.0.19041.0` on Windows. The csproj guards (`ValidateXcodeVersion`, `_SunfishStripAspNetCoreFromMacCatalystPacks`, `UseRidGraph`) are all OS-conditioned to macOS so they shouldn't fire on Windows builds. First-hand Windows verification is not yet captured in repo.

## What Anchor demos today

Anchor's `Components/Pages/` contains:

- `Home.razor` — dashboard
- `Onboarding.razor` — three-step QR-code onboarding flow (paper §13.4)
- `TeamSwitcherPage.razor` — Wave 6.8 multi-team join/switch surface
- `NotFound.razor`

`MauiProgram.cs` wires:

- LocalFirst encrypted store, kernel-security attestation + Ed25519, kernel-runtime node host (paper §11.3, §5.1)
- Kernel-sync gossip daemon + mDNS LAN discovery + managed-relay WAN discovery (paper §6.1, §17.2; ADR 0029, 0031)
- Kernel-CRDT (YDotNet) wired via the AnchorCrdtDeltaBridge (DELTA_STREAM application loop, ADR 0028)
- Anchor v1→v2 keystore migration + bootstrap hosted service + sync hosted service

## What Anchor does NOT demo today

The shipped property-management blocks (`blocks-properties` PR #210, `blocks-property-equipment` PR #213+#216, the `blocks-inspections` Property extension PR #222) are **not yet wired into Anchor**. Specifically:

- `Sunfish.Anchor.csproj` does not reference `blocks-properties`, `blocks-property-equipment`, or `blocks-inspections`
- `MauiProgram.cs` does not call `AddInMemoryProperties()`, `AddInMemoryPropertyEquipment()`, or `AddInMemoryInspections()`
- `Components/Pages/` has no Properties / Equipment / Inspections pages
- `Components/Layout/NavMenu.razor` exposes only Dashboard / Onboarding / Teams nav items
- `apps/kitchen-sink` has no seed data for these domain blocks (kitchen-sink's purpose is the component-primitive catalogue, not domain demos)

The Phase 1 G6 host-integration work that would close this gap is **deliberately deferred** pending the dynamic-forms substrate ADR currently in design — runtime-defined types + JSONB storage + section-based permissions would change the approach to host-page wiring, so building Razor pages against the in-memory services today would create rework.

## What's available for reuse

For consumers building their own host shell (Anchor analog, Bridge analog, Photino+Blazor experiment, etc.) the package surface is ready to consume directly:

| Package | DI extension | Razor block |
|---|---|---|
| `Sunfish.Blocks.Properties` | `AddInMemoryProperties()` | _(no Razor block — domain types + repo only)_ |
| `Sunfish.Blocks.PropertyEquipment` | `AddInMemoryPropertyEquipment()` | _(no Razor block — domain types + repo + lifecycle event store)_ |
| `Sunfish.Blocks.Inspections` | `AddInMemoryInspections()` | `<InspectionListBlock />` |

All three packages ship as `Microsoft.NET.Sdk.Razor` with no MAUI dependency — they are reusable from any Blazor-rendering host.

## Related docs

- [Anchor README](../../../../accelerators/anchor/README.md) — paper alignment, scope, deliverable checklist
- [`docs/dev/anchor-maccatalyst-build-prereqs.md`](../../../../docs/dev/anchor-maccatalyst-build-prereqs.md) — first-time macOS build prerequisites
- [`apps/docs/blocks/properties/overview.md`](../../blocks/properties/overview.md) — Properties block
- [`apps/docs/blocks/property-equipment/overview.md`](../../blocks/property-equipment/overview.md) — Property Equipment block
- [`apps/docs/blocks/inspections/overview.md`](../../blocks/inspections/overview.md) — Inspections block
