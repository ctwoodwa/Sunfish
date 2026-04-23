# Sunfish Anchor

**Status:** Re-activated (Wave 3.3 + 3.4). Three deferred items landed; five still deferred.
**Tier:** Accelerator
**Platform:** .NET MAUI Blazor Hybrid (Windows, macOS, iOS, Android)

Anchor is the **local-first desktop reports and admin dashboard** accelerator for
Sunfish. It is the desktop counterpart to Bridge (the multi-tenant SaaS shell
per [ADR 0006](../../docs/adrs/0006-bridge-is-saas-shell.md)) and exists to
validate that the platform's local-first pillar (vision Pillar 1) has a
first-class deliverable — not just a principle.

## Role in the architecture

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

## Scope — Wave 3.3 + 3.4 landed; further scope still deferred

Paper-alignment Wave 3.3 + 3.4 de-gates Anchor from the [ADR 0017](../../docs/adrs/0017-web-components-lit-technical-basis.md)
Web Components migration (the paper at `_shared/product/local-node-architecture-paper.md`
is now the load-bearing spec; WC migration is downstream). The current slice wires
LocalFirst + kernel-security + kernel-runtime into the MAUI shell and ships the
three-step QR-code onboarding flow from paper §13.4. Bundle-selection and
report-catalog scope remain deferred.

Deliverable checklist:

- [x] **LocalFirst store wiring** — `AddSunfishEncryptedStore()` + `AddSunfishKernelRuntime()`
      + `AddSunfishKernelSecurity()` wired in `MauiProgram.cs` (Wave 3.3). Actual
      encrypted-DB open-on-login + export-as-first-class-operation still pending the
      follow-up slice — Wave 4 work.
- [ ] **Bundle selection UI** — which bundles does Anchor compose? For the small-landlord
      reference vertical: `blocks-rent-collection`, `blocks-leases`, `blocks-maintenance`,
      `blocks-accounting`. For small-medical-office: TBD based on practice workflow.
- [ ] **Report catalog** — ties to [ADR 0021](../../docs/adrs/0021-reporting-pipeline-policy.md);
      Anchor is the natural home to demo the PDF / XLSX / DOCX / PPTX / CSV contract-and-adapter
      model end-to-end.
- [ ] **Audit log surface** — read-only view over the Foundation audit log for compliance posture.
- [ ] **Sync toggle** — per-bundle opt-in sync UI against a federated peer (ADR 0013).
      The three-indicator status bar (paper §13.2) is live via `SunfishNodeHealthBar`;
      the per-bundle toggle is still pending.
- [x] **Authentication model** (partial) — device-bound Ed25519 keypair issued at
      onboarding, self-signed founder attestation vs. joiner attestation issued by
      the founder's key (Wave 3.4). Passphrase recovery + OS-keystore cache of the
      derived encrypted-DB key still pending.
- [ ] **Platform packaging** — .msix (Windows Store + sideload), .dmg (macOS),
      Mac Catalyst notarization, App Store submission flows.
- [ ] **Auto-update** — delivery channel (Sparkle for macOS, MSIX AppInstaller for Windows,
      or an OSS alternative).
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

Anchor builds and launches, but the shell is a placeholder. From this directory:

```bash
dotnet build Sunfish.Anchor.csproj -f net10.0-windows10.0.19041.0
dotnet run  --project Sunfish.Anchor.csproj -f net10.0-windows10.0.19041.0
```

Multi-target frameworks: `net10.0-windows*`, `net10.0-maccatalyst`, `net10.0-ios`,
`net10.0-android`. `net10.0-maccatalyst` and `net10.0-ios` are Windows-excluded;
they must be built on macOS.

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

## References

- [ADR 0006](../../docs/adrs/0006-bridge-is-saas-shell.md) — Bridge's scope; Anchor is the complementary non-SaaS shell.
- [ADR 0012](../../docs/adrs/0012-foundation-localfirst.md) — Foundation.LocalFirst contracts (Anchor's data layer).
- [ADR 0013](../../docs/adrs/0013-foundation-integrations.md) — federation relationship for optional sync.
- [ADR 0014](../../docs/adrs/0014-adapter-parity-policy.md) — the adapter parity Anchor's multi-platform reach exercises.
- [ADR 0016](../../docs/adrs/0016-app-and-accelerator-naming.md) — the naming convention this project follows (`Sunfish.Anchor`, flat).
- [ADR 0017](../../docs/adrs/0017-web-components-lit-technical-basis.md) — Web Components migration that Anchor's scope is deferred behind.
- [ADR 0021](../../docs/adrs/0021-reporting-pipeline-policy.md) — reporting pipeline; Anchor is the natural demo surface.
- [`_shared/product/vision.md`](../../_shared/product/vision.md) — Pillar 1 (local-first) that Anchor exists to make real.
