---
uid: block-leases-overview
title: Leases — Overview
description: Canonical lease, unit, party, and document records plus a ready-made lease-list Blazor block for property-management apps.
---

# Leases — Overview

## What this block is

`Sunfish.Blocks.Leases` is the domain block for lease records. It ships a deliberately
thin first-pass surface — just the primary shapes and a minimal `ILeaseService` —
because the full workflow (signature, execution, renewal, termination, DocuSign envelopes,
commencement-date triggers) is deferred to follow-up passes.

In this pass the block provides:

- Canonical records: `Lease`, `Unit`, `Party`, `Document`.
- A lifecycle enum (`LeasePhase`) that enumerates every phase the workflow will eventually
  support. Today's service only produces `Draft`; transitions are deferred.
- A read/create service (`ILeaseService`) with in-memory default.
- A read-display Blazor block (`LeaseListBlock`) that renders a lease table via the service.

## Package

- Package: `Sunfish.Blocks.Leases`
- Source: `packages/blocks-leases/`
- Namespace roots:
  - `Sunfish.Blocks.Leases.Models`
  - `Sunfish.Blocks.Leases.Services`
  - `Sunfish.Blocks.Leases.State`
  - `Sunfish.Blocks.Leases.DependencyInjection`
- Razor components: `LeaseListBlock.razor`

## When to use it

Use this block when your app needs:

- A canonical shape for leases, units, and lease parties (tenant, landlord, manager,
  guarantor).
- A first-pass `ILeaseService` you can wire into a back-end or demo.
- A drop-in `LeaseListBlock` for an admin or tenant view.

Do not use it when you need signature workflow, rent-ledger integration, or phase
transitions beyond `Draft` — those are deferred.

## Key entities and services

- `Lease` / `LeasePhase` — the lease record and its lifecycle enum.
- `Unit` — the rentable unit being leased.
- `Party` / `PartyKind` — a tenant, landlord, manager, or guarantor on a lease.
- `Document` — a blob reference (PDF, disclosure form) linked to a lease.
- `ILeaseService` — create / get / list surface.
- `LeaseState` — placeholder state-machine holder (phase + transition timestamp).
- `LeaseListBlock` — read-display Blazor component.

See [entity-model.md](entity-model.md), [service-contract.md](service-contract.md), and
[demo-lease-list.md](demo-lease-list.md).

## DI wiring

```csharp
using Sunfish.Blocks.Leases.DependencyInjection;

services.AddInMemoryLeases();
```

Registers `InMemoryLeaseService` as the singleton `ILeaseService`. Suitable for
development and demo scenarios.

## Related ADRs

- ADR 0015 — Module-Entity Registration (for when the persistence-backed service lands).
- ADR 0013 — Foundation.Integrations (DocuSign is the expected signature provider; the
  integration surface sits outside this block).

## Related pages

- [Entity Model](entity-model.md)
- [Service Contract](service-contract.md)
- [Demo — Lease List](demo-lease-list.md)
