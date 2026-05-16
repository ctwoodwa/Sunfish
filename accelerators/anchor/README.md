# Sunfish Anchor

**Status:** Active — shell functional; Crew Comms, device pairing, and kernel stack wired; reporting/packaging deferred.
**Tier:** Accelerator
**Platform:** .NET MAUI Blazor Hybrid (Windows, macOS; iOS/Android excluded pending MAUI 11 stable)

Anchor is the **local-first desktop reports and admin dashboard** accelerator for
Sunfish. It is the desktop counterpart to Bridge (the Zone-C Hybrid multi-tenant
SaaS per [ADR 0031](../../docs/adrs/0031-bridge-hybrid-multi-tenant-saas.md)) and
exists to validate that the platform's local-first pillar (vision Pillar 1) has a
first-class deliverable — not just a principle.

## Role in the architecture

> **Paper §20.7 Zone A — Local-First Node.** Anchor is the canonical Zone-A
> implementation of the local-node architecture paper — a hardware-bound
> desktop node running the full kernel stack (§5.1) with ciphertext-at-rest
> (§11.2) and optional managed-relay peering (§17.2). Its sibling is Bridge,
> the Zone-C Hybrid implementation
> ([ADR 0031](../../docs/adrs/0031-bridge-hybrid-multi-tenant-saas.md)).
> Together they cover the two canonical deployment shapes identified by the
> paper; any future accelerator inherits from one.

| | Bridge | Anchor |
|---|---|---|
| **Shape** | Multi-tenant SaaS shell | Single-user desktop dashboard |
| **Deployment** | Hosted (Aspire / Azure / self-hosted server) | On-device (.msix, .dmg, Mac Catalyst, iOS, Android) |
| **Target user** | End-users working inside a tenant | Owner / administrator / auditor |
| **Data locus** | Hosted Postgres, per-tenant | Local SQLite (via `Sunfish.Foundation.LocalFirst`), syncs opt-in |
| **Network required** | Yes | No (offline-by-default) |
| **UI composition** | Same component surface | Same component surface |

Anchor exists to prove that the same component surface, bundle manifests, and
Foundation primitives compose cleanly into both shapes — if something only works
in the SaaS case, it isn't really local-first.

## Scope — what has shipped and what is deferred

The kernel stack, onboarding, crew comms, and device pairing are all wired and
functional. Bundle-selection, report catalog, and packaging remain deferred.

Deliverable checklist:

- [x] **Kernel stack wiring** — `AddSunfishEncryptedStore()`, `AddSunfishKernelRuntime()`,
      `AddSunfishKernelSecurity()`, `AddSunfishKernelSync()`, `AddSunfishKernelCrdt()`,
      and `AddSunfishTransport()` wired in `MauiProgram.cs`. `AnchorSyncHostedService`
      runs the gossip daemon; `AnchorBootstrapHostedService` applies startup migration.
- [x] **Onboarding flow (paper §13.4)** — `Onboarding.razor` (three-step paste-bundle /
      generate-founder flow), `QrOnboardingService`, `AnchorSessionService.OnboardAsync`.
      Camera/QR-decode stub present in `QrScanner.razor`; paste-bundle is the current
      reference transport.
- [x] **Authentication model** — device-bound Ed25519 keypair issued at onboarding;
      self-signed founder attestation vs. joiner attestation signed by founder key.
      Passphrase recovery and OS-keystore cache of the derived encrypted-DB key remain
      pending (`Services/Pairing/`).
- [x] **Crew Comms consumer (W#59)** — `CrewChatPage.razor` wires `ICrewCommsChannel`,
      `CrewCommsListenerHostedService`, `CrewCommsInvitationBus`, and
      `TeamMembershipCrewRoster` adapter. Full transport + roster + TYPING/DELIVERED
      indicators live.
- [x] **Team switcher surface** — `TeamSwitcherPage.razor` exposes the multi-team
      workspace model (ADR 0032) in the nav shell. `AnchorV1MigrationService` handles
      the v1 → v2 in-place migration.
- [x] **Device pairing (W#23 Phase 0)** — `Services/Pairing/` implements the pairing
      handshake surface used by the companion iOS Field app.
- [x] **Localization** — 12-locale roster (en-US, es-419, pt-BR, fr, de, ja, zh-Hans,
      ar-SA, hi, he-IL, fa-IR, ko) via satellite RESX assemblies under
      `Resources/Localization/`.
- [ ] **Bundle selection UI** — which blocks compose into Anchor? Reference verticals:
      `blocks-rent-collection + blocks-leases + blocks-maintenance + blocks-financial-ledger`
      (small-landlord); `blocks-scheduling + blocks-tasks` (small-medical-office).
- [ ] **Report catalog** — ties to [ADR 0021](../../docs/adrs/0021-reporting-pipeline-policy.md);
      Anchor is the natural demo surface for the PDF / XLSX / DOCX / PPTX / CSV
      contract-and-adapter pipeline.
- [ ] **Audit log surface** — read-only view over the Foundation audit log for compliance posture.
- [ ] **Sync toggle** — per-bundle opt-in sync UI against a federated peer (ADR 0013).
      `SunfishNodeHealthBar` (paper §13.2 three-indicator status bar) is live in the nav
      shell; the per-bundle toggle is pending.
- [ ] **Platform packaging** — .msix (Windows Store + sideload), .dmg (macOS),
      Mac Catalyst notarization, App Store submission flows.
- [ ] **Auto-update** — Sparkle (macOS) or MSIX AppInstaller (Windows).
- [ ] **Crash reporting** — pre-production OTel pipeline per `_shared/engineering/operations-sre.md`.

### Onboarding flow (paper §13.4)

Three steps surfaced by `Components/Pages/Onboarding.razor`:

1. **Install** — the MAUI app is installed; the local node runtime is ready.
2. **Authenticate** — the user either (a) pastes a base64 onboarding bundle
   (`QrOnboardingService.DecodePayloadAsync`) or (b) generates a new team with the
   founder flow (`QrOnboardingService.GenerateFounderBundleAsync`). The camera /
   QR-decode path is documented as a TODO in `Components/QrScanner.razor` — the
   .NET 11 MAUI preview's camera surface wasn't readily available in this wave,
   so the paste-bundle fallback is the reference transport.
3. **Sync** — `AnchorSessionService.OnboardAsync(attestation, snapshot, ct)` applies
   the attestation, stamps `LastSyncedAt`, and transitions `NodeHealth` + `DataFreshness`
   to `Healthy`. `LinkStatus` stays `Offline` until a peer is reached (Wave 4+).

### QR payload wire format

```
[4 bytes: CBOR bundle length, little-endian uint32]
[N bytes: CBOR-encoded AttestationBundle (kernel-security §11.3)]
[4 bytes: snapshot length, little-endian uint32]
[M bytes: raw snapshot bytes]
```

The bundle uses the canonical-CBOR encoding defined by `AttestationBundle.ToCbor`.
Each attestation in the bundle is verified with `IAttestationVerifier` against its
own declared `IssuerPublicKey` at decode time; founder bundles are self-signed
(issuer == subject), joiner bundles are signed by the founder's Ed25519 private key.

## Running it today

Anchor builds and runs with a functional shell (Home, Onboarding, CrewChat, TeamSwitcher
pages) and the full kernel stack. From this directory:

```bash
# Windows
dotnet build Sunfish.Anchor.csproj -f net11.0-windows10.0.19041.0
dotnet run  --project Sunfish.Anchor.csproj -f net11.0-windows10.0.19041.0

# macOS
dotnet build Sunfish.Anchor.csproj -f net11.0-maccatalyst
dotnet run  --project Sunfish.Anchor.csproj -f net11.0-maccatalyst
```

**Framework note:** Anchor targets `net11.0-windows*` and `net11.0-maccatalyst`.
iOS and Android target frameworks are commented out in the .csproj — MAUI 11 preview
pins to a specific Xcode release and the mobile targets are re-enabled once the
preview stabilizes. macOS builds require Xcode + the MAUI workload
(`dotnet workload install maui`); see the auto-memory note on Xcode license acceptance
and the `xcode-select` link before first build.

## Why MAUI Blazor Hybrid

Chosen as the first Anchor host because it:

1. **Reuses Bridge's Razor components verbatim** via `BlazorWebView` — zero component duplication.
2. **Is pure .NET** — matches the existing toolchain; contributors only need one stack.
3. **Is forward-compatible with Web Components** — the embedded WebView (Edge WebView2 on Windows,
   WKWebView on macOS/iOS, Chrome on Android) handles custom elements natively, so the ADR 0017
   Web Components migration will land here without reshelling.
4. **Covers mobile from day one** — iPad / Android tablet as inspection-in-the-field surfaces
   for the small-landlord and small-medical-office reference verticals are plausible futures;
   paying for mobile as a bonus of the desktop choice is cheap insurance.

Tradeoffs accepted: binary size (~80MB vs ~10MB for Tauri 2), longer build times, MAUI workload
setup overhead. These are acceptable for a first-party desktop accelerator; a leaner
alternative (Photino.Blazor or Tauri 2) can ship as a second accelerator later if a specific
deployment demands smaller binaries.

## Multi-team Anchor (v2 roadmap)

Anchor v1 ships **single-team per install**. v2 adopts the Slack-style
workspace-switcher model — one installation, one UI shell, a workspace
switcher, per-workspace state isolation — per
[ADR 0032](../../docs/adrs/0032-multi-team-anchor-workspace-switching.md).

Key properties of the v2 model:

- **Non-destructive upgrade.** v1's single team becomes `team-0` in v2 with
  no data migration required; the first-launch handler migrates the data
  directory layout in place.
- **In-process per-team scoping.** A new `TeamContext` type (kernel-runtime)
  holds everything team-scoped — SQLCipher DB, event log, CRDT documents,
  gossip daemon state, role attestations, plugin set. `ITeamContextFactory`
  resolves contexts lazily; `IActiveTeamAccessor` exposes the foreground team
  to UI bindings.
- **Per-team HKDF subkeys defeat operator cross-team correlation.** The
  install holds one root Ed25519 keypair; each team gets a subkey derived via
  `HKDF(root_private, "sunfish-team-subkey-v1:" + team_id)`. Operators of
  different teams see different public keys and cannot correlate the same
  user across teams.
- **All teams sync, only one renders.** Background gossip runs for every
  joined team; a `ResourceGovernor` caps concurrent-rounds-per-tick so small
  laptops stay bounded.
- **Slack-style switcher component.** `SunfishTeamSwitcher` (Blazor first;
  React parity backlog for Wave 3.5 extension) renders known teams as a
  sidebar with per-team badge counts.
- **Compliance escape hatch.** Regulated-industry tenants opt into
  `"isolation": "process"` which spawns a dedicated `local-node-host` child
  process per-team — OS-level isolation for teams that can't accept
  intra-process sharing.

See [ADR 0032](../../docs/adrs/0032-multi-team-anchor-workspace-switching.md)
for the full decision (isolation boundary, device-identity model, concurrency
policy) and [`docs/specifications/multi-team-settings-scoping.md`](../../docs/specifications/multi-team-settings-scoping.md)
for the global-vs-per-team settings scope.

Implementation rolls out as Wave 6 of the paper-alignment plan.

## References

- [ADR 0006](../../docs/adrs/0006-bridge-is-saas-shell.md) — Bridge's scope; Anchor is the complementary non-SaaS shell.
- [ADR 0031](../../docs/adrs/0031-bridge-hybrid-multi-tenant-saas.md) — Bridge as Zone-C Hybrid; Anchor's Zone-A sibling deployment shape.
- [ADR 0032](../../docs/adrs/0032-multi-team-anchor-workspace-switching.md) — multi-team Anchor v2 roadmap.
- [ADR 0012](../../docs/adrs/0012-foundation-localfirst.md) — Foundation.LocalFirst contracts (Anchor's data layer).
- [ADR 0013](../../docs/adrs/0013-foundation-integrations.md) — federation relationship for optional sync.
- [ADR 0014](../../docs/adrs/0014-adapter-parity-policy.md) — the adapter parity Anchor's multi-platform reach exercises.
- [ADR 0016](../../docs/adrs/0016-app-and-accelerator-naming.md) — the naming convention this project follows (`Sunfish.Anchor`, flat).
- [ADR 0017](../../docs/adrs/0017-web-components-lit-technical-basis.md) — Web Components migration that Anchor's scope is deferred behind.
- [ADR 0021](../../docs/adrs/0021-reporting-pipeline-policy.md) — reporting pipeline; Anchor is the natural demo surface.
- [`_shared/product/vision.md`](../../_shared/product/vision.md) — Pillar 1 (local-first) that Anchor exists to make real.
