# Sunfish Anchor

**Status:** Scaffolded. Scope deliberately deferred.
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

## Scope — deliberately deferred

Per user direction, Anchor's scope is **reserved for a future build-out**. The
current state is scaffolded skeleton only: solution entry, project file, one
placeholder page. Landing the scope now would either (a) produce Blazor-only code
that has to be rewritten during the [ADR 0017](../../docs/adrs/0017-web-components-lit-technical-basis.md)
Web Components migration, or (b) pre-commit to bundle-selection and
auth-model decisions before the migration surfaces the right answers.

Deferred deliverables — each triggers its own intake when Web Components migration
reaches the relevant milestone:

- [ ] **LocalFirst store wiring** — register `Sunfish.Foundation.LocalFirst` contracts,
      wire embedded SQLite, expose export as a first-class operation per ADR 0012.
- [ ] **Bundle selection UI** — which bundles does Anchor compose? For the small-landlord
      reference vertical: `blocks-rent-collection`, `blocks-leases`, `blocks-maintenance`,
      `blocks-accounting`. For small-medical-office: TBD based on practice workflow.
- [ ] **Report catalog** — ties to [ADR 0021](../../docs/adrs/0021-reporting-pipeline-policy.md);
      Anchor is the natural home to demo the PDF / XLSX / DOCX / PPTX / CSV contract-and-adapter
      model end-to-end.
- [ ] **Audit log surface** — read-only view over the Foundation audit log for compliance posture.
- [ ] **Sync toggle** — per-bundle opt-in sync UI against a federated peer (ADR 0013).
- [ ] **Authentication model** — how does a single-user desktop app authenticate? Device-bound
      credentials, optional passphrase, recovery mechanism. Ties to Foundation.MultiTenancy
      contracts (ADR 0008) in single-tenant mode.
- [ ] **Platform packaging** — .msix (Windows Store + sideload), .dmg (macOS),
      Mac Catalyst notarization, App Store submission flows.
- [ ] **Auto-update** — delivery channel (Sparkle for macOS, MSIX AppInstaller for Windows,
      or an OSS alternative).
- [ ] **Crash reporting** — pre-production OTel pipeline per `_shared/engineering/operations-sre.md`.

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
