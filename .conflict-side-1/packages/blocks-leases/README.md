# Sunfish.Blocks.Leases

Block for the lease-management surface — `Lease` entity, `LeasePhase` lifecycle, document versioning, per-party signature events, and a read-display lease list.

Cluster module under the property-operations cluster; **EXTEND target** for the W#27 leases workstream.

## What this ships

### Models

- **`Lease`** — root lease entity (`IMustHaveTenant`); references `PropertyId`, `UnitRef`, parties (`PartyKind` discriminator: Tenant / Cosigner / Guarantor / Occupant), term dates, rent amount, `LeasePhase` lifecycle.
- **`LeasePhase`** — enum lifecycle (Draft / Pending / AwaitingSignature / Executed / Renewed / Terminated / Expired).
- **`Party`** + **`PartyKind`** — lease-party child entities.
- **`Unit`** — per-lease unit reference (placeholder until `blocks-properties.PropertyUnit` ships).
- **`Document`** + **`LeaseDocumentVersion`** — versioned lease-document storage with per-version signature events (W#27 P2+P3 extension).

### Services

- **`ILeaseService`** + `InMemoryLeaseService` — CRUD + phase transitions + document-version + signature event recording.

### UI

- **`SunfishLeaseListBlock.razor`** — read-display Razor component showing the lease list at the configured tenant.

## Cluster role

Per the 2026-04-29 reconciliation, this block is the **EXTEND target** for the W#27 cluster module. The cluster delta added `LeaseDocumentVersion` versioning, per-party signature events, renewal/termination state-machine transitions, and a `LeaseHolderRole` projection (leveraging existing `PartyKind.Tenant` per UPF Rule 5).

## Deferred follow-ups

- Renewal automation
- Termination notice generator (state-specific notice content)
- Eviction-record integration
- Kitchen-sink seed page (precedent: matches Properties / Equipment / Inspections first-slice pattern)

## ADR map

- [ADR 0015](../../docs/adrs/0015-module-entity-registration.md) — module-entity registration pattern
- [ADR 0028](../../docs/adrs/0028-per-record-class-consistency.md) — Lease creation interface boundary (cross-block; consumed by `blocks-property-leasing-pipeline.LeaseOffer`)
- [ADR 0054](../../docs/adrs/0054-electronic-signature-capture-and-document-binding.md) — signature events on lease document versions
- Property-ops cluster reconciliation: `icm/07_review/output/property-ops-cluster-vs-existing-reconciliation-2026-04-28.md` §#27

## See also

- [apps/docs Overview](../../apps/docs/blocks/leases/overview.md)
- [Sunfish.Blocks.Properties](../blocks-properties/README.md) — `PropertyId` FK target
- [Sunfish.Blocks.PropertyLeasingPipeline](../blocks-property-leasing-pipeline/README.md) — upstream `LeaseOffer` producer
