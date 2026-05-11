# Stage 06 Hand-off — W#60 Phase 2: ERPNext React UI Skin

**Workstream:** W#60 — ERPNext Composition Pivot  
**Phase:** 2 of 5 — React UI skin + Sunfish ASP.NET Core proxy layer  
**Pipeline variant:** `sunfish-feature-change`  
**Status gate:** Phase 1 PASS confirmed (CO 2026-05-11 — ERPNext self-hosted, core accounting loop working)  
**Estimate:** ~5 PRs / 5 phases / 8–12h COB time  
**Pre-merge council:** Required for Phase 5 only (introduces public `@sunfish/ui-react` package API surface)

---

## Context

ERPNext is now running self-hosted and owns the property management + accounting domain. Sunfish's job for Phase 2 is to wrap it with a React UI that replaces ERPNext's Frappe UI for CO's daily workflows, adds tenant communication (via existing `blocks-crew-comms`), and establishes the TypeScript UI layer that Phases 3–5 build on.

**Architecture:**
```
React app (Vite SPA)
  ↓  fetch /api/v1/erpnext/* + /api/v1/crew-comms/*
ASP.NET Core Bridge (proxy + enrichment)
  ↓  HttpClient → ERPNext REST API (existing)
  ↓  SignalR → blocks-crew-comms (existing)
ERPNext (GPLv3, self-hosted Docker on localhost:8000)
```

The React app **never calls ERPNext directly** — all calls go through Bridge's proxy, which handles auth, CORS, and enrichment. CO's ERPNext API token lives only in Bridge's `appsettings.json`.

---

## Pre-build checklist

Before writing a single line of code:

1. Verify ERPNext is reachable from Bridge server: `curl -H "Authorization: Token {api_key}:{api_secret}" http://localhost:8000/api/resource/Property` returns a JSON object.
2. Verify Bridge runs: `cd accelerators/bridge && dotnet run --project Sunfish.Bridge`.
3. Verify no open PR is already touching `apps/anchor-react/` or `accelerators/bridge/Sunfish.Bridge/Proxy/ERPNextProxy.cs`.
4. Read `accelerators/bridge/Sunfish.Bridge/Listings/ListingsEndpoints.cs` — this is the minimal-API pattern to follow for the ERPNext proxy.
5. Read `accelerators/bridge/Sunfish.Bridge/Proxy/TenantWebSocketReverseProxy.cs` — existing proxy pattern.

---

## Phase 1 — Bridge ERPNext proxy + one working endpoint

**Goal:** Prove the Bridge → ERPNext connection. One endpoint, one test.

**Deliverables (1 PR):**

- [ ] `accelerators/bridge/Sunfish.Bridge/appsettings.json` — add section:
  ```json
  "ERPNext": {
    "BaseUrl": "http://localhost:8000",
    "ApiKey": "",
    "ApiSecret": ""
  }
  ```
  `appsettings.Development.json` carries CO's actual values (gitignored; document in `CONTRIBUTING-REACT.md`).

- [ ] `accelerators/bridge/Sunfish.Bridge/Proxy/ERPNextOptions.cs` — typed config record:
  ```csharp
  public sealed record ERPNextOptions
  {
      public const string SectionName = "ERPNext";
      public string BaseUrl { get; init; } = "http://localhost:8000";
      public string ApiKey { get; init; } = "";
      public string ApiSecret { get; init; } = "";
      
      public string AuthorizationHeader =>
          $"Token {ApiKey}:{ApiSecret}";
  }
  ```

- [ ] `accelerators/bridge/Sunfish.Bridge/Proxy/IERPNextClient.cs` — minimal interface:
  ```csharp
  public interface IERPNextClient
  {
      Task<JsonElement> GetResourceListAsync(
          string doctype,
          int limit = 20,
          CancellationToken ct = default);
      Task<JsonElement> GetResourceAsync(
          string doctype,
          string name,
          CancellationToken ct = default);
      Task<JsonElement> PostAsync(
          string endpoint,
          object payload,
          CancellationToken ct = default);
  }
  ```

- [ ] `accelerators/bridge/Sunfish.Bridge/Proxy/ERPNextHttpClient.cs` — HttpClient-factory implementation. Uses `IOptions<ERPNextOptions>` for `Authorization: Token {key}:{secret}` header. Typed client registered in DI.

- [ ] `accelerators/bridge/Sunfish.Bridge/Proxy/ERPNextProxy.cs` — minimal API endpoint family:
  ```csharp
  app.MapGroup("/api/v1/erpnext")
     .RequireAuthorization()     // Bridge auth gate
     .MapGet("/properties", ...)
     .WithName("GetProperties");
  ```
  Phase 1 ships only `/api/v1/erpnext/properties` (list). Other endpoints follow in Phases 2–4.

- [ ] `accelerators/bridge/Sunfish.Bridge/Program.cs` — add `app.MapERPNextProxy()` in SaaS posture branch (after `app.MapListingsEndpoints()`).

- [ ] Add `AddERPNextClient()` DI extension registering `IERPNextClient` as typed HttpClient.

- [ ] One integration test in `accelerators/bridge/tests/`: `ERPNextProxyTests.cs` — mock `IERPNextClient`, assert `/api/v1/erpnext/properties` returns 200 with correct JSON shape.

**PASS gate:** `curl -H "Cookie: ..." http://localhost:5000/api/v1/erpnext/properties` (authenticated) returns `{ "data": [...] }` with CO's properties. No React yet.

**HALT conditions:**
- ERPNext `Property` doctype doesn't exist → CO creates it in ERPNext admin before Phase 1; Phase 1 cannot proceed without Phase 1 (ERPNext setup) PASS
- Bridge auth middleware rejects unauthenticated requests to `/api/v1/erpnext/*` → expected; test with an authenticated session cookie

---

## Phase 2 — React app scaffold + Properties screen

**Goal:** "Hello, World" React screen showing CO's properties. Proves the full stack.

**Deliverables (1 PR):**

- [ ] Create `apps/anchor-react/` — Vite 6 + React 19 + TypeScript 5:
  ```
  apps/anchor-react/
    src/
      api/        ← ERPNext API client (typed fetch wrapper over /api/v1/erpnext/)
      components/ ← property-management components
      hooks/      ← TanStack Query hooks
      pages/      ← screen components
      stores/     ← Zustand stores
      app.tsx     ← router + layout
      main.tsx    ← entry point
    public/
    index.html
    package.json
    vite.config.ts
    tailwind.config.ts  (Tailwind v4)
    tsconfig.json
    CONTRIBUTING-REACT.md
  ```

- [ ] `package.json` — key dependencies:
  ```json
  {
    "name": "@sunfish/anchor-react",
    "dependencies": {
      "react": "^19.0.0",
      "react-dom": "^19.0.0",
      "react-router-dom": "^7.0.0",
      "@tanstack/react-query": "^5.0.0",
      "zustand": "^5.0.0",
      "react-hook-form": "^7.0.0",
      "zod": "^3.0.0"
    },
    "devDependencies": {
      "vite": "^6.0.0",
      "@vitejs/plugin-react": "^4.0.0",
      "typescript": "^5.5.0",
      "tailwindcss": "^4.0.0",
      "@tailwindcss/vite": "^4.0.0",
      "vitest": "^2.0.0",
      "@testing-library/react": "^16.0.0",
      "@testing-library/user-event": "^14.0.0"
    }
  }
  ```

- [ ] **shadcn/ui** — init with `npx shadcn@latest init` after scaffold. Add components as needed (Card, Table, Badge, Button, Form, Input). DO NOT install the entire component library upfront — add components when a screen needs them.

- [ ] `vite.config.ts` — proxy config: `/api/v1` → Bridge at `http://localhost:5000` (dev only; production serves from same origin).

- [ ] `src/api/erpnext.ts` — typed fetch wrapper (NOT openapi-ts in Phase 2 — ERPNext OpenAPI spec coverage is partial; hand-author the 6 endpoint types needed):
  ```typescript
  export interface Property {
    name: string;
    property_name: string;
    address: string;
    units: number;
  }
  export async function getProperties(): Promise<Property[]> { ... }
  ```

- [ ] `src/hooks/useProperties.ts` — TanStack Query hook wrapping `getProperties()`.

- [ ] `src/pages/PropertiesPage.tsx` — Properties list (shadcn Card grid). Shows: property name, address, unit count, status badge.

- [ ] `src/app.tsx` — React Router v7 with a single route for now: `/properties` → `PropertiesPage`.

- [ ] `CONTRIBUTING-REACT.md` at `apps/anchor-react/CONTRIBUTING-REACT.md`:
  - API base URL: `http://localhost:5000` (Bridge dev server)
  - Auth: Bridge session cookie (log in at `http://localhost:5000/login` first)
  - ERPNext config: Bridge `appsettings.Development.json` under `"ERPNext"` section
  - How to run: `dotnet run --project accelerators/bridge/Sunfish.Bridge` + `cd apps/anchor-react && npm run dev`
  - ERPNext doctype names used: `Property`, `Lease`, `Payment Entry`, `Journal Entry`

- [ ] Two Vitest tests: `PropertiesPage.test.tsx` — mock fetch, assert property cards render.

**PASS gate:** CO opens `http://localhost:5173`, sees 6 properties in React UI. Browser dev tools show successful `GET /api/v1/erpnext/properties` call to Bridge.

**HALT conditions:**
- Vite proxy to Bridge fails CORS → add `app.UseCors()` in Bridge with `WithOrigins("http://localhost:5173")` for dev
- React 19 + react-router-dom v7 breaking changes → check docs; fallback to react-router-dom v6 if needed

---

## Phase 3 — Leases + Rent collection screens

**Goal:** CO's two most-used daily screens.

**Deliverables (1 PR):**

**Bridge additions:**
- `/api/v1/erpnext/leases` — GET list (maps ERPNext `Lease` doctype)
- `/api/v1/erpnext/leases/{name}` — GET detail
- `/api/v1/erpnext/payments` — GET list + POST (record rent payment)
- `/api/v1/erpnext/payments/{name}` — GET detail

**React screens:**
- `src/pages/LeasesPage.tsx` — Lease list with renewal alerts (highlight leases expiring in <60 days with amber badge). Columns: tenant name, property, unit, start/end date, monthly rent, status.
- `src/pages/LeaseDetailPage.tsx` — Lease detail with payment history.
- `src/pages/RentCollectionPage.tsx` — Record payment form: select lease, amount, date, payment method. Posts to Bridge → ERPNext. Success shows confirmation + updated balance.

**Typed API additions to `src/api/erpnext.ts`:**
```typescript
export interface Lease { name: string; tenant: string; property: string; ... }
export interface Payment { name: string; amount: number; date: string; lease: string; ... }
export function getLeases(): Promise<Lease[]>
export function getLease(name: string): Promise<Lease>
export function recordPayment(payload: RecordPaymentInput): Promise<Payment>
```

**PASS gate:** CO records a rent payment in the React UI. Bridge posts to ERPNext. ERPNext ledger shows the payment. CO verifies in ERPNext admin.

---

## Phase 4 — Accounting summary + Crew Comms panel

**Goal:** Month-end summary screen + tenant messaging.

**Deliverables (1 PR):**

**Bridge additions:**
- `/api/v1/erpnext/accounting/summary` — GET (calls ERPNext `execute` API to run a `Profit and Loss Statement` report by property; returns simplified JSON)
- `/api/v1/erpnext/accounting/outstanding` — GET (outstanding balances by tenant)

**React screens:**
- `src/pages/AccountingPage.tsx` — P&L by property (current month + YTD). Table: income (rent, late fees) vs expenses (maintenance, insurance, mortgage interest). Use shadcn/ui Table component.
- `src/pages/CrewCommsPage.tsx` — Tenant message threads. Uses the **existing** `blocks-crew-comms` SignalR hub at Bridge's `/hubs/bridge`. Thread list on left; message pane on right.

**Crew Comms integration note:**  
`blocks-crew-comms` is already wired to Bridge (W#45). The React page connects via `@microsoft/signalr` npm package to the existing hub. Do NOT re-implement the signaling logic — just wire the React component to the existing hub events (`ReceiveMessage`, `SendMessage`). Read `accelerators/bridge/Sunfish.Bridge/Hubs/BridgeHub.cs` before writing the React page.

**PASS gate:** CO sends a test message to a tenant contact in the React Crew Comms panel. Message appears in the existing Anchor desktop app (if running) or Bridge UI. Accounting summary shows correct P&L figures matching ERPNext reports.

**HALT conditions:**
- ERPNext `Profit and Loss Statement` report API requires admin privileges → use a narrower `General Ledger` query instead; document in hand-off notes
- SignalR hub auth model differs from REST cookie auth → check `BridgeHub.cs` for `[Authorize]` policy and replicate in the React SignalR connection options

---

## Phase 5 — Maintenance + role routing + `@sunfish/ui-react` package

**Goal:** Complete the 6-screen set. Extract reusable components. Role-based access.

**Deliverables (1 PR — pre-merge council REQUIRED):**

**Bridge additions:**
- `/api/v1/erpnext/maintenance` — GET list + POST (maps ERPNext `Maintenance Visit` doctype)
- `/api/v1/erpnext/maintenance/{name}` — PATCH (update status)

**React screen:**
- `src/pages/MaintenancePage.tsx` — Maintenance queue. Columns: property, issue, reported date, assigned, status. Inline status update (dropdown → PATCH).

**Role routing:**
```typescript
// src/stores/authStore.ts
type Role = 'owner' | 'accountant' | 'cpa' | 'tenant';
```

Route guards:
- `owner` — all screens
- `accountant` — Leases, RentCollection, Accounting, CrewComms  
- `cpa` — Accounting (read-only view)
- `tenant` — CrewComms only (scoped to their own thread)

Roles sourced from Bridge auth claim (add `role` claim to Bridge's auth token/cookie).

**`packages/ui-react/` — new npm package `@sunfish/ui-react`:**

Extract from `apps/anchor-react/src/components/`:
- `SyncStateBadge` — green/amber/offline sync indicator (per paper §13.2 three states)
- `OfflineIndicator` — banner shown when `navigator.onLine === false`
- `FreshnessBadge` — shows last-sync time (e.g., "synced 2m ago")
- `PropertyCard` — reusable property card for the properties grid
- `RoleGate` — `<RoleGate roles={['owner', 'accountant']}>` wrapper

Package setup: mirror `packages/ui-adapters-react/` structure (Vite library mode, ESM + CJS, TypeScript declarations).

**`CONTRIBUTING-REACT.md` update:** Add role setup instructions and how to add a new screen.

**PASS gate (full Phase 2 acceptance):**
1. CO uses React UI for a full working session: checks leases, records a payment, views accounting summary, sends a tenant message.
2. Accountant role can access Leases + Accounting but not Maintenance.
3. `@sunfish/ui-react` package builds cleanly (`npm run build` in `packages/ui-react/`).
4. All 3 Vitest test files pass (`npm test` in `apps/anchor-react/`).

**Pre-merge council scope:** `@sunfish/ui-react` public API surface (component prop contracts) only. Use standard 4-perspective council. No WCAG subagent in Phase 2 — accessibility sweep is Phase 3 milestone.

---

## Accepted decisions

| Decision | Choice | Rationale |
|---|---|---|
| React app location | `apps/anchor-react/` | Separate from Bridge; can point at local Anchor server or Bridge interchangeably |
| Bridge proxy path | `/api/v1/erpnext/*` | Versioned; doesn't collide with existing `/api/resource/` Bridge routes |
| TypeScript client | Hand-authored in Phase 2 | ERPNext OpenAPI spec is partially documented; openapi-ts deferred to Phase 3 once stable endpoint set is known |
| shadcn/ui install | On-demand per screen | Avoid installing unused components; each `npx shadcn@latest add <component>` is a separate commit |
| Tailwind version | v4 (`@tailwindcss/vite` plugin) | No `tailwind.config.js` needed; CSS-first config. If v4 has blockers, fall back to v3 |
| Crew Comms | Wire to existing SignalR hub | Do NOT re-implement; `blocks-crew-comms` is the canonical impl (W#45 built) |
| `@sunfish/ui-react` | New package, separate from `@sunfish/ui-adapters-react` | `ui-adapters-react` is the ADR 0014 parity adapter; `ui-react` is property-management-specific + domain components |

---

## Symbol verification (XO confirmed against main 2026-05-11)

- `accelerators/bridge/Sunfish.Bridge/Listings/ListingsEndpoints.cs` — minimal API pattern (verified)
- `accelerators/bridge/Sunfish.Bridge/Hubs/BridgeHub.cs` — SignalR hub (verified present)
- `accelerators/bridge/Sunfish.Bridge/Proxy/TenantWebSocketReverseProxy.cs` — proxy pattern (verified)
- `packages/ui-adapters-react/src/` — existing React adapter (library mode, React 18/19, Vite, Vitest) (verified)
- ERPNext `Property` doctype — **NOT verified against CO's ERPNext install; CO must confirm doctype names match** before Phase 3

---

## FAILED triggers (inherit from UPF plan)

- React development velocity slower than Blazor for equivalent CRUD screens → reassess at end of Phase 2
- Phase 2 elapsed time exceeds 4 calendar months → halt W#60; document what shipped; reassess
- Bridge CORS configuration causes persistent unresolvable issues with React dev server → escalate to XO; DO NOT bypass security middleware

---

## Workstream flip

After Phase 5 PR merges:
1. Update `icm/_state/workstreams/W60-erpnext-composition-pivot-react-ui.md` → `status: "built"`
2. Run `python3 tools/icm/render-ledger.py`
3. Commit both (one commit: `chore(icm): W#60 ledger flip — ERPNext React UI Phase 2 built`)
