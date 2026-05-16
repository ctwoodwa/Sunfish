# W#60 Phase 5 — Reporting + Contracts
## Stage 06 Build Hand-off

**Workstream:** W#60 — ERPNext Composition Pivot  
**Phase:** 5 of 5 (ongoing — no hard gate)  
**Owner:** sunfish-PM (COB)  
**Gate:** PR 1 (`@sunfish/contracts`) is independent — start immediately. PR 2 (reporting) requires Phase 2 React UI on main (done). Phase 4 completion NOT required.  
**Estimated effort:** ~3–4 dev-weeks (~10–14h focused sessions)  
**PR count:** 3 PRs

---

## Context

Phase 3 ships Tauri offline shell; Phase 4 ships the collaboration layer (Accountant + CPA + Tenant portal + Bank CSV) — gated on CO Surface Pro acceptance. Phase 5 delivers reporting views and the TypeScript contracts package. These are independent of Phase 4 — PR 1 and PR 2 can ship while Phase 4 is pending.

Phase 5 completes the W#60 workstream. After Phase 5, W#60 flips to `built`.

---

## Pre-build checklist

1. Phase 2 React UI on `main` (done — `apps/anchor-react/` + `@sunfish/ui-react`).
2. `gh pr list --state open` — no parallel Phase 5 PRs in flight.
3. Verify `accelerators/bridge/` is on latest main (Phase 2 endpoints present).
4. PR 1 is fully independent — no gate beyond checking `packages/` naming: run `ls packages/ | grep contracts` to confirm no collision.

---

## PR 1 — `@sunfish/contracts` npm package

**Goal:** Extract TypeScript interface definitions into a standalone published package so downstream consumers (book examples, community self-hosters, future mobile clients) can depend on Sunfish types without importing from the app source.

**Location:** `packages/contracts/` (new package)

**Deliverables:**
- `packages/contracts/package.json` — `name: "@sunfish/contracts"`, `version: "0.1.0"`, `main: "dist/index.js"`, `types: "dist/index.d.ts"`, `files: ["dist"]`
- `packages/contracts/src/property.ts` — `Property`, `Unit`, `OccupancyStatus`, `RentStatus` interfaces (shape from ERPNext Property + Unit doctypes)
- `packages/contracts/src/accounting.ts` — `LedgerEntry`, `JournalEntry`, `BankTransaction`, `ReconciliationStatus`, `PLSummary`, `PLLineItem` interfaces
- `packages/contracts/src/tenant.ts` — `Tenant`, `Lease`, `PaymentRecord`, `MessageThread` interfaces
- `packages/contracts/src/sync.ts` — `SyncStatus` (online|offline|syncing), `OfflineQueueEntry`, `ConflictRecord` interfaces (mirror of Phase 3 Tauri state)
- `packages/contracts/src/index.ts` — barrel export of all above
- `packages/contracts/tsconfig.json` + build script (`tsc -p tsconfig.json`)
- Re-export existing contracts from `packages/ui-adapters-react/src/contracts/` (Integrations.ts, SystemRequirements.ts) via `packages/contracts/src/index.ts` re-exports
- Tests: `packages/contracts/src/__tests__/index.test.ts` — 4 smoke tests: import each namespace, verify shape has expected keys (no runtime logic — just type shape verification via `satisfies`)

**Halt conditions:**
- Run `ls packages/ | grep contracts` before creating — if `packages/contracts/` already exists under another name, use that package instead of creating a new one
- Do NOT pull types from the running ERPNext API dynamically — define interfaces manually matching the shapes COB has been working with in Phases 1–4. Ground truth is the Bridge endpoint response shapes, not ERPNext's Frappe schema explorer

**PASS gate:** `cd packages/contracts && npm run build` succeeds; `packages/contracts/dist/index.d.ts` exports all 4 namespaces.

---

## PR 2 — Rent roll + P&L reporting

**Goal:** CO can view a rent roll (all 6 properties × units × occupancy + rent status) and export a P&L by property from the React UI — covering the core landlord reporting needs without leaving the Sunfish UI.

**Location:** `accelerators/bridge/` + `apps/anchor-react/src/`

**Deliverables (Bridge ASP.NET):**
- `GET /api/v1/reports/rent-roll` — returns array of `{propertyId, propertyName, unit, tenantName, leaseStart, leaseEnd, monthlyRent, lastPaymentDate, balanceDue, status}` by calling ERPNext `GET /api/resource/Rental Payment?fields=...` (or equivalent Property Management module report endpoint)
- `GET /api/v1/reports/profit-loss?propertyId={id}&period={month|quarter|year}&asOf={ISO date}` — returns `PLSummary` with `{income: PLLineItem[], expenses: PLLineItem[], netIncome: number}` by calling ERPNext Reports API `GET /api/method/frappe.desk.query_report.run?report_name=Profit%20and%20Loss%20Statement`
- `GET /api/v1/reports/profit-loss/export?propertyId={id}&period={period}&asOf={date}&format={csv|json}` — CSV export for accountant/CPA hand-off
- Audit: `RentRollViewed` + `PLReportViewed` + `PLReportExported` `AuditEventType` constants
- Tests: 4 tests (rent-roll endpoint: returns list, handles empty; P&L endpoint: returns summary, handles unknown property)

**Deliverables (React):**
- `src/pages/RentRoll.tsx` — table of all properties × units × rent status; columns: property / unit / tenant / lease dates / monthly rent / last payment / balance / status badge (current|overdue|vacant); sortable by property, status; CO and accountant roles see this
- `src/pages/PLReport.tsx` — property selector + period selector (month/quarter/year) + as-of date picker; renders income vs expenses tree; "Export CSV" button (calls export endpoint); CO and accountant roles see this
- Add both pages to Bridge sidebar nav under "Reports" section; gate with `useRole(['co', 'accountant'])`
- Tests: `RentRoll.test.tsx` (5 tests: render, load data, sort by status, vacant unit shown, error state) + `PLReport.test.tsx` (5 tests: render, property selector, period selector, export CSV, error state)

**Halt conditions:**
- ERPNext Property Management module must be active on CO's instance — if `GET /api/resource/Property` returns 404, fall back to composing the rent roll from `GET /api/resource/Lease` + `GET /api/resource/Sales Invoice` (document the fallback in code comment)
- ERPNext P&L report name may vary by version — check `GET /api/method/frappe.desk.query_report.run?report_name=Profit+and+Loss+Statement` before hardcoding; if 404, use `GET /api/method/frappe.desk.query_report.run?report_name=General%20Ledger` with account-type filtering
- Do NOT hardcode property IDs — all property selectors pull from `GET /api/v1/properties` (existing Phase 2 endpoint)

**PASS gate:** CO opens Rent Roll in Bridge React UI, sees all 6 properties × units with payment status. CO selects a property, picks "Year to date," clicks Export CSV, file downloads with income/expense line items.

---

## PR 3 — Phase 5 ledger flip + docs

**Location:** `icm/_state/workstreams/W60-*.md` + `apps/docs/`

**Deliverables:**
- `apps/docs/blocks/erpnext-stack/overview.md` — W#60 architecture summary: ERPNext + Bridge + React + Tauri layer diagram; link to ADR 0086; link to self-hosting README
- `apps/docs/blocks/erpnext-stack/toc.yml` + parent `toc.yml` entry
- `icm/_state/workstreams/W60-*.md` — flip `status: "building"` → `status: "built"`; run `render-ledger.py`

**PASS gate:** `python3 tools/icm/render-ledger.py` exits 0; W#60 row shows `built`.

---

## Phase 5 overall PASS criteria

- `@sunfish/contracts` npm package builds cleanly; all 4 type namespaces export correctly
- CO views rent roll for all 6 properties and exports a year-to-date P&L CSV from the React UI
- W#60 ledger flipped to `built`

## Phase 5 FAIL triggers

| Failure | Fallback |
|---|---|
| ERPNext P&L report API differs from documented | Use General Ledger API with account-type grouping; document the adaptation |
| ERPNext Property Management module not installed | Compose rent roll from Lease + Invoice queries; document in code |
| npm publish of `@sunfish/contracts` blocked (npm registry, auth) | Publish to GitHub Packages instead; or skip publish, keep as internal package |

---

## What COB must NOT do

- Do NOT wait for Phase 4 to complete before starting PR 1 or PR 2 — they are independent.
- Do NOT hardcode ERPNext property names or IDs — all data comes from the API.
- Do NOT ship `@sunfish/contracts` without the re-exports from `packages/ui-adapters-react/src/contracts/` — the goal is a single contracts entry point.

---

## References

- **Phase 4 hand-off** — `icm/_state/handoffs/w60-collaboration-phase4-stage06-handoff.md`
- **ADR 0086** — `docs/adrs/0086-anchor-tauri-react-product-surface.md` (Tauri shell)
- **Phase 2 React screens** — `apps/anchor-react/src/pages/` (existing property/accounting screens)
- **IERPNextClient** — `accelerators/bridge/Sunfish.Bridge/Proxy/IERPNextClient.cs` (ERPNext proxy)
- **UPF plan Phase 5** — `~/.claude/plans/noble-crunching-hopper.md` §Phase 5
