---
uid: accelerator-anchor-reports-admin-scope
title: Anchor - Reports and Admin Scope
description: How the reporting and admin surfaces plug into Anchor - the ADR 0021 reporting pipeline as the canonical demo, the planned admin surfaces, and how bundle selection shapes the catalog.
---

# Anchor - Reports and Admin Scope

## Overview

Anchor's product identity is **local-first desktop reports and admin
dashboard**. This page documents the planned reporting and admin scope,
how it composes against Sunfish Foundation contracts, and where it sits
on the roadmap.

**The current story is honest: this scope is deferred.** Anchor today is
scaffolded - a placeholder Home page - and the surfaces below unfreeze
when the [ADR 0017](../../articles/adrs/) Web Components migration
reaches the relevant milestones. This page describes what Anchor will be,
not what it is in today's build.

## Reporting - the ADR 0021 pipeline

Anchor is the natural demo surface for the reporting pipeline established
in **ADR 0021 - Reporting Pipeline Policy**. That pipeline defines a
**contract + adapter** model for generating reports in multiple formats
from a single report definition:

| Format | Adapter |
|---|---|
| PDF | PDF adapter |
| XLSX | XLSX adapter |
| DOCX | DOCX adapter |
| PPTX | PPTX adapter |
| CSV | CSV adapter |

Anchor will surface:

- **Report catalog** - browse available reports (scoped to the bundles
  activated on this device).
- **Run a report** - pick parameters (date range, entities, filters),
  preview, export to any supported format.
- **Saved runs** - local history of generated reports stored under the
  LocalFirst store, with file-on-disk links for the exported artifacts.

Because Anchor is on-device, report generation runs **locally** against
the local SQLite store - no round-trip through a hosted server. This is
the meaningful local-first demo: an auditor with no network can still
produce audit-ready PDFs.

## LocalFirst data layer

Reports query the **Sunfish.Foundation.LocalFirst** store (ADR 0012).
When scope unfreezes, Anchor will:

- Register the LocalFirst contracts in MauiProgram.cs.
- Wire embedded SQLite as the default backing store.
- Expose **export** as a first-class operation - export the LocalFirst
  database to a portable artifact, and import it back. Export is a
  platform deliverable, not a bonus.

The LocalFirst contract keeps reports decoupled from the physical store:
the same report definition runs against the hosted Postgres in Bridge and
against embedded SQLite in Anchor; only the adapter behind the contract
changes.

## Admin surfaces

The admin story on Anchor is **single-user, single-tenant**. It is the
desktop counterpart to the Bridge tenant-admin surfaces documented in
[Bridge - Tenant Admin](../bridge/tenant-admin.md), scaled down for an
owner / administrator / auditor working alone on a device.

Planned admin surfaces:

| Surface | Purpose |
|---|---|
| **Audit log** | Read-only view over the Foundation audit log. Filter by entity, actor, timestamp, action; export. This is the compliance-posture surface for a solo operator. |
| **Bundle activation** | Which bundles are composed into this Anchor install. For small-landlord: blocks-rent-collection, blocks-leases, blocks-maintenance, blocks-accounting. For small-medical-office: TBD. |
| **Sync toggle** | Per-bundle opt-in sync against a federated peer (ADR 0013). Off by default - Anchor is offline-by-default; sync is a conscious decision a user makes per data category. |
| **Preferences** | Theme, density, locale, default home, accessibility flags - same contract (ISunfishThemeService, IPreferencesService) as Bridge. |
| **Account / device identity** | Device-bound credentials, optional passphrase, recovery mechanism. Ties to Foundation.MultiTenancy in single-tenant mode (ADR 0008). |

## Bundle selection shapes the catalog

Anchor does not ship every Sunfish bundle. Each Anchor deployment picks
its bundles based on the reference vertical it targets:

- **Small-landlord deployment** - blocks-rent-collection, blocks-leases,
  blocks-maintenance, blocks-accounting. Reports: rent roll, lease
  expirations, maintenance aging, P&L by property.
- **Small-medical-office deployment** - TBD based on practice workflow.
  Likely ingests from ingestion-voice and ingestion-forms.

The report catalog is therefore **tenant-shaped** - Anchor resolves the
catalog against the active bundles via the same
IBusinessCaseService.GetSnapshotAsync contract Bridge uses (see
[Bridge - Bundle Provisioning](../bridge/bundle-provisioning.md)),
filtered to the single local tenant.

## Why Anchor is the right demo surface for reporting

Three reasons, in order of importance:

1. **Offline is the honest test.** A reporting pipeline that only works
   against a hosted SQL server is a hosted-reporting pipeline. Anchor
   forces the report definitions to be portable - the same definition
   runs against Postgres in Bridge and SQLite in Anchor.
2. **Export is a product, not a feature.** On-device, an exported PDF is
   the deliverable. The pipeline's correctness is trivially verifiable -
   open the file.
3. **Small-vertical users are real users.** Small landlords and small
   medical offices are the reference verticals ADR 0006 names. Their
   workflows are a reporting-and-admin workflow, not a multi-user
   collaborative-SaaS workflow. Anchor matches the shape of the work.

## Admin parity with Bridge

Anchor's admin surfaces consume the same Sunfish package contracts as
Bridge's:

| Contract | Source | Bridge usage | Anchor usage |
|---|---|---|---|
| ITenantAdminService | packages/blocks-tenant-admin | Tenant profile, tenant users, bundle activation | Single-tenant profile; bundle activation per device |
| IBusinessCaseService | packages/blocks-businesscases | EntitlementSnapshotBlock on /account/bundles | Resolves the local tenant's entitlement snapshot; scopes the report catalog |
| IUserNotificationService | packages/foundation/Notifications | Sidebar bell + Notifications settings page | In-app notification surface for run-complete / sync-complete events |
| ISunfishThemeService | packages/foundation/Services | Light / Dark / System toggle | Same |

Parity is verified by the tests mandated under **ADR 0014 - Adapter
Parity Policy**.

## What today's build demonstrates

Even with scope deferred, the current Anchor build demonstrates the
important thing: the MAUI Blazor Hybrid shell loads, Razor pages render
inside the embedded WebView, the Sunfish component surface compiles
against the Anchor csproj, and the repo's tooling (F5, DocFX) recognizes
the accelerator. The hard decisions - which bundles, which auth, which
sync policy - are the ones being deferred.

## Related ADRs

- **ADR 0008** - Foundation.MultiTenancy (single-tenant posture for a
  solo-operator device).
- **ADR 0012** - Foundation.LocalFirst (embedded SQLite + export).
- **ADR 0013** - Foundation.Integrations (federated sync when opt-in).
- **ADR 0014** - Adapter Parity Policy (Bridge / Anchor parity tests).
- **ADR 0021** - Reporting Pipeline Policy (the contract Anchor demos).

## Next

- [Overview](overview.md) - what Anchor is.
- [MAUI Blazor Hybrid](maui-blazor-hybrid.md) - the platform story.
- [Getting Started](getting-started.md) - build and launch today's
  scaffold.
