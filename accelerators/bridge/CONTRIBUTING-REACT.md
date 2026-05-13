# Contributing — Bridge ERPNext Proxy + React UI (W#60 Phase 2)

This guide covers local setup for the Bridge → ERPNext proxy layer and the
`apps/anchor-react/` React SPA that consumes it.

## Prerequisites

### 1. ERPNext running

ERPNext must be running via `frappe_docker` at `http://erp.localhost:8080`.

```
cd ~/frappe_docker
docker compose up -d
```

Verify: `curl http://erp.localhost:8080` should return the Frappe login page.

### 2. ERPNext doctype prerequisites

Before running Phase 2 features, create three custom doctypes in ERPNext
at `http://erp.localhost:8080/app/doctype/new`.

#### `Property` doctype

| Field | Type | Required | Notes |
|---|---|---|---|
| `property_name` | Data | ✓ | Human label, e.g., "150 Lexington Ct" |
| `company` | Link → Company | ✓ | Which LLC owns this property |
| `address_line_1` | Data | ✓ | |
| `address_line_2` | Data | — | |
| `city`, `state`, `postal_code` | Data | ✓ | |
| `units` | Int | ✓ | Default 1 |
| `fixed_asset_account` | Link → Account | — | Links to "Real Estate - {address}" account |
| `status` | Select (Active/Vacant/Maintenance/Sold) | ✓ | Default Active |
| `acquisition_date` | Date | — | |
| `notes` | Text Editor | — | Free-form |

Naming series: `PROP-.####`.

#### `Lease` doctype

| Field | Type | Required | Notes |
|---|---|---|---|
| `lease_name` | Data | ✓ | Auto-generated from property + tenant |
| `property` | Link → Property | ✓ | |
| `tenant` | Link → Customer | ✓ | |
| `unit_designation` | Data | — | e.g., "Unit A" |
| `start_date` | Date | ✓ | |
| `end_date` | Date | ✓ | |
| `monthly_rent` | Currency | ✓ | |
| `rent_due_day` | Int (1–28) | ✓ | Day of month rent is due |
| `security_deposit` | Currency | — | |
| `status` | Select (Active/Expired/Terminated/Pending) | ✓ | Default Active |

Naming series: `LEASE-.YYYY.-.####`.

#### `Maintenance Ticket` doctype

| Field | Type | Required | Notes |
|---|---|---|---|
| `subject` | Data | ✓ | Short summary |
| `property` | Link → Property | ✓ | |
| `lease` | Link → Lease | — | If reported by tenant |
| `description` | Text Editor | ✓ | |
| `status` | Select (Open/In Progress/Awaiting Parts/Done/Closed) | ✓ | Default Open |
| `priority` | Select (Low/Normal/High/Urgent) | ✓ | Default Normal |
| `assigned_to` | Link → User | — | |
| `cost` | Currency | — | |

Naming series: `MAINT-.YYYY.-.####`.

### 3. ERPNext API key

Generate API keys at `http://erp.localhost:8080/app/user` → your user →
**API Access** → **Generate Keys**.

### 4. Bridge development config

```
cp accelerators/bridge/Sunfish.Bridge/appsettings.Development.json.example \
   accelerators/bridge/Sunfish.Bridge/appsettings.Development.json
```

Edit `appsettings.Development.json` and fill in:
- `ERPNext:ApiKey` — from step 3 above
- `ERPNext:ApiSecret` — from step 3 above
- `ERPNext:DefaultCompany` — the ERPNext company name to scope requests to
  (e.g., `"Royal Key Management LLC"`)

`appsettings.Development.json` is gitignored; never commit it.

## Running locally

```bash
# Terminal 1 — Bridge
dotnet run --project accelerators/bridge/Sunfish.Bridge

# Terminal 2 — React SPA (Phase 2+)
cd apps/anchor-react && npm run dev
```

**API base URL:** `http://localhost:5000` (Bridge dev server)  
**React dev URL:** `http://localhost:5173`

CORS is pre-configured to allow `http://localhost:5173` in development.

## Verify the proxy

```bash
curl http://localhost:5000/api/v1/erpnext/properties
```

Should return `{ "data": [...] }` with your properties from ERPNext.

```bash
curl http://localhost:5000/api/v1/whoami
```

Should return `{ "user": "dev-user", "role": "owner", "defaultCompany": "...", ... }`.

## Architecture notes

- **Phase 2 is online-only.** No offline cache or write queue in Phase 2. If
  Bridge or ERPNext is unreachable, the UI shows an error state. Offline support
  arrives in Phase 3 (Tauri shell + SQLite write queue).
- **React never calls ERPNext directly.** All calls go through Bridge's
  `/api/v1/erpnext/*` proxy, which handles auth, CORS, and multi-company scoping.
- **Company scoping.** Phase 1 reads the company from `ERPNext:DefaultCompany` in
  config. Phase 2 wires a `UserService` + OIDC claim so each user gets their
  assigned companies.

## ERPNext doctype names used

| Doctype | ERPNext name |
|---|---|
| Properties | `Property` |
| Leases | `Lease` |
| Maintenance | `Maintenance Ticket` |
| Payments | `Payment Entry` (standard ERPNext) |
| Journal entries | `Journal Entry` (standard ERPNext) |

## Role setup (Phase 5)

The `whoami` endpoint (`GET /api/v1/whoami`) returns `{ user, role, defaultCompany, availableCompanies }`.
In production, `role` is populated from OIDC claims. In development it defaults to `"owner"`.

Valid roles: `owner`, `manager`, `viewer`.

To test role-gated UI locally, temporarily edit `Program.cs`:
```csharp
role = "viewer"  // instead of "owner"
```

Role-gated React components use `<RoleGate allow={['owner', 'manager']}>` from
`apps/anchor-react/src/components/RoleGate.tsx`. The underlying primitive
`@sunfish/ui-react` `RoleGate` is role-value-agnostic (accepts any `allow` string[]).

## Adding a new screen

1. Add API functions to `apps/anchor-react/src/api/erpnext.ts`.
2. Create `apps/anchor-react/src/pages/YourPage.tsx` using `useQuery` from
   `@tanstack/react-query`.
3. Add the Bridge route in `accelerators/bridge/Sunfish.Bridge/Proxy/ERPNextProxy.cs`
   by adding a `group.MapGet/MapPost/MapMethods(...)` call and a handler.
4. Update `apps/anchor-react/src/app.tsx`: add a `<NavLink>` and a `<Route>`.
5. If the route is role-protected, wrap `<Route element>` children in `<RoleGate allow={[...]}>`.
