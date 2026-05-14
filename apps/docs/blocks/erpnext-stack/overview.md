---
uid: block-erpnext-stack-overview
title: ERPNext Stack — Overview
description: ERPNext as the accounting and property engine, Sunfish as the local-first sync, offline, and React UI layer over it (W#60 architecture).
keywords:
  - sunfish
  - erpnext
  - local-first
  - react
  - tauri
  - offline
  - property-management
  - accounting
---

# ERPNext Stack — Overview

## What this is

The ERPNext Stack is Sunfish's Zone C hybrid accelerator for self-hosted property management and accounting. It positions ERPNext (GPLv3, Docker-hosted) as the primary data engine for properties, leases, payments, and GL entries, and Sunfish as the **local-first sync + offline + React UI + tenant comms** layer above it.

The split follows the inverted-stack principle: ERPNext owns persistence and double-entry accounting correctness; Sunfish owns the experience layer — offline capability, role routing, crew comms, and reporting.

## Layer diagram

```
┌─────────────────────────────────────────────────────────────┐
│  Sunfish React UI   (apps/anchor-react)                     │
│  Properties · Leases · Rent · Accounting · Comms ·         │
│  Maintenance · Reports (Rent Roll + P&L)                    │
├─────────────────────────────────────────────────────────────┤
│  Sunfish Bridge   (accelerators/bridge)                     │
│  ASP.NET Core proxy · /api/v1/erpnext/* · /api/v1/reports/* │
│  ERPNextClient · Role routing · Audit events                │
├─────────────────────────────────────────────────────────────┤
│  Tauri Shell   (apps/anchor-tauri)    [Phase 3+]            │
│  Offline SQLite cache · Write queue · Loro CRDT sync        │
├─────────────────────────────────────────────────────────────┤
│  ERPNext   (self-hosted Docker, GPLv3)                      │
│  Property · Unit · Lease · Sales Invoice · GL Entry · Bank  │
└─────────────────────────────────────────────────────────────┘
```

## Phases

| Phase | What ships | Status |
|---|---|---|
| 1 | ERPNext self-hosted; 6 properties + leases configured; accounting loop validated | ✅ PASS (CO, 2026-05-12) |
| 2 | React UI (6 screens) + Bridge proxy + `@sunfish/ui-react` | ✅ Built (7 PRs, 2026-05-13) |
| 3 | Tauri v2 shell + offline SQLite cache + write queue | ✅ Code-complete; awaiting CO Surface Pro acceptance |
| 4 | Accountant peer node (Headscale Tier 2); CPA read-only; tenant magic-link portal | 🔒 Gated on Phase 3 PASS |
| 5 | `@sunfish/contracts` npm package; Rent Roll + P&L reporting | ✅ Built (PRs #844 + #847) |

## Key packages

| Package | Language | Description |
|---|---|---|
| `apps/anchor-react` | TypeScript / React 19 | React SPA — 8 pages, React Query, Tailwind |
| `accelerators/bridge` | C# / ASP.NET Core | Bridge proxy + ERPNext API client + reporting endpoints |
| `apps/anchor-tauri` | Rust + TypeScript | Tauri v2 desktop shell (Phase 3) |
| `packages/contracts` | TypeScript | `@sunfish/contracts` — standalone type contracts for all 4 ERPNext domains |

## `@sunfish/contracts` package

The `packages/contracts` package (`@sunfish/contracts` on npm) provides a single import for all Sunfish ERPNext stack TypeScript types, without requiring a dependency on `@sunfish/ui-adapters-react`.

Namespaces:

| Namespace | Types |
|---|---|
| `property` | `Property`, `Unit`, `OccupancyStatus`, `RentStatus`, `RentRollRow` |
| `accounting` | `LedgerEntry`, `JournalEntry`, `BankTransaction`, `PLSummary`, `OutstandingInvoice` |
| `tenant` | `Tenant`, `Lease`, `PaymentRecord`, `MessageThread` |
| `sync` | `SyncStatus`, `OfflineQueueEntry`, `ConflictRecord` |
| `integrations` | Integration Atlas types (ADR 0067) |
| `system-requirements` | SystemRequirementsResult types (ADR 0063-A1.1) |

```ts
import { type Property, type RentRollRow, OccupancyStatus } from '@sunfish/contracts'
```

## Reporting endpoints

Two reporting endpoints ship in Phase 5:

| Endpoint | Description |
|---|---|
| `GET /api/v1/reports/rent-roll` | All properties × units with occupancy + payment status |
| `GET /api/v1/reports/profit-loss` | P&L from GL Entry, filtered by period (month/quarter/year) and optional property |
| `GET /api/v1/reports/profit-loss/export` | CSV download of P&L line items |

The rent roll computes `status` (Current / Overdue / Vacant) from Lease status + outstanding Sales Invoice balance.

## ADR references

- **ADR 0086** — Anchor Tauri React product surface (Phase 3 offline shell)
- **ADR 0063** — SystemRequirements wire format
- **ADR 0067** — Integration Atlas
- **W#60 UPF plan** — `~/.claude/plans/noble-crunching-hopper.md`

## Self-hosting

See `accelerators/bridge/README.md` for `docker-compose` setup instructions for ERPNext self-hosting alongside Bridge.
