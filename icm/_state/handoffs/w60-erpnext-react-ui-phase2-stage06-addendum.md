# Stage 06 Addendum — W#60 Phase 2: ERPNext React UI Skin

**Date:** 2026-05-12
**Author:** XO
**Original hand-off:** `icm/_state/handoffs/w60-erpnext-react-ui-phase2-stage06-handoff.md` (do not edit — kept for audit trail)
**Audit:** `icm/07_review/output/2026-05-12_w60-phase2-handoff-audit.md`
**Reason for addendum:** Original hand-off authored before Phase 1 PASS; Phase 1's actual outcome differs from assumptions. This addendum resolves 4 blocking + 7 non-blocking gaps so sunfish-PM can build without mid-flight halts.

**How to use:** read the original hand-off first for phase structure and deliverables, then apply this addendum's overrides where they conflict.

---

## Override 1 — ERPNext doctypes (resolves audit G1)

**Original assumption:** ERPNext has `Property`, `Lease`, `Maintenance Visit` doctypes that the React UI consumes.
**Reality:** Default ERPNext install has none of these. Phase 1 modeled properties as **Companies** + Real Estate **Fixed Assets**; leases and maintenance were tabled.

**Phase 2 approach — manual doctype creation** (option (a) from audit):

Before Phase 2 Phase 1 PR, CO (or sunfish-PM) creates three Frappe custom doctypes via the ERPNext UI at `http://erp.localhost:8080/app/doctype/new`:

### `Property` doctype

| Field | Type | Required | Notes |
|---|---|---|---|
| `property_name` | Data | ✓ | Human label, e.g., "150 Lexington Ct" |
| `company` | Link → Company | ✓ | Which LLC owns this property |
| `address_line_1` | Data | ✓ |  |
| `address_line_2` | Data | — |  |
| `city`, `state`, `postal_code` | Data | ✓ |  |
| `units` | Int | ✓ | Default 1 |
| `fixed_asset_account` | Link → Account (account_type=Fixed Asset) | — | Links to "Real Estate - {address}" account |
| `status` | Select (`Active`/`Vacant`/`Maintenance`/`Sold`) | ✓ | Default `Active` |
| `acquisition_date` | Date | — |  |
| `notes` | Text Editor | — | Free-form |

Naming series: `PROP-.####`. Permissions: System Manager + Accounts Manager full; everyone else read.

### `Lease` doctype

| Field | Type | Required | Notes |
|---|---|---|---|
| `lease_name` | Data | ✓ | Auto-generated from `property` + `tenant` |
| `property` | Link → Property | ✓ |  |
| `tenant` | Link → Customer | ✓ |  |
| `unit_designation` | Data | — | e.g., "Unit A", for multi-unit properties |
| `start_date` | Date | ✓ |  |
| `end_date` | Date | ✓ |  |
| `monthly_rent` | Currency | ✓ |  |
| `rent_due_day` | Int (1–28) | ✓ | Day of month rent is due |
| `security_deposit` | Currency | — |  |
| `renewal_month` | Int (1–12) | — | Month of year lease renews |
| `late_fee_policy` | Small Text | — | Free-form, formalized later |
| `status` | Select (`Active`/`Expired`/`Terminated`/`Pending`) | ✓ | Default `Active` |

Naming series: `LEASE-.YYYY.-.####`.

### `Maintenance Ticket` doctype

| Field | Type | Required | Notes |
|---|---|---|---|
| `subject` | Data | ✓ | Short summary |
| `property` | Link → Property | ✓ |  |
| `lease` | Link → Lease | — | If reported by tenant |
| `reporter` | Link → Customer | — | Tenant or staff |
| `description` | Text Editor | ✓ |  |
| `status` | Select (`Open`/`In Progress`/`Awaiting Parts`/`Done`/`Closed`) | ✓ | Default `Open` |
| `priority` | Select (`Low`/`Normal`/`High`/`Urgent`) | ✓ | Default `Normal` |
| `assigned_to` | Link → User | — |  |
| `cost` | Currency | — | Filled when work order closes |
| `expense_account` | Link → Account | — | Default: `Repairs & Maintenance - {company}` |

Naming series: `MAINT-.YYYY.-.####`.

**Sunfish-PM responsibility:** Phase 2 Phase 1 PR documentation (`CONTRIBUTING-REACT.md`) must include a section "ERPNext doctype prerequisites" with the field tables above and a screenshot of the ERPNext doctype admin UI.

**Future work (post-W#60):** package these as a Frappe custom app `frappe-sunfish-property` for one-command install — defer to Phase 5 (`docker-compose` self-hosting guide) or a follow-on workstream.

---

## Override 2 — ERPNext base URL (resolves audit G2)

Original Phase 1 deliverables line 51:
```json
"ERPNext": {
  "BaseUrl": "http://localhost:8000",
  ...
}
```

**Replace with:**
```json
"ERPNext": {
  "BaseUrl": "http://erp.localhost:8080",
  "SiteName": "erp.localhost",
  ...
}
```

**Rationale:** the `frappe_docker` install uses `HTTP_PUBLISH_PORT=8080` and `FRAPPE_SITE_NAME_HEADER=erp.localhost`. The `SiteName` is required as the `Host` header on every request (Frappe routes by host).

`ERPNextHttpClient` must include:
```csharp
request.Headers.Host = options.SiteName;  // required for multi-site Frappe routing
request.Headers.Authorization = new AuthenticationHeaderValue("token", $"{options.ApiKey}:{options.ApiSecret}");
```

---

## Override 3 — Multi-company scoping (resolves audit G3)

**Reality:** CO's ERPNext has 7 companies (Royal Key Management LLC, Elbrus Holding LLC, Acero/Bosco/Escola/Shirin Properties LLC, Wood Family Personal). Every read/write must specify which company.

**Required additions to Phase 1 PR:**

### Bridge auth claim

Add `company` to Bridge's auth principal claims (alongside the existing `role` claim — see Override 4). Default = CO's primary company (Royal Key Management LLC). Settable per user in Bridge's user admin.

### `ERPNextOptions` enrichment

```csharp
public sealed record ERPNextOptions
{
    public const string SectionName = "ERPNext";
    public string BaseUrl { get; init; } = "http://erp.localhost:8080";
    public string SiteName { get; init; } = "erp.localhost";
    public string ApiKey { get; init; } = "";
    public string ApiSecret { get; init; } = "";
}
```

### `IERPNextClient` API takes company explicitly

```csharp
public interface IERPNextClient
{
    Task<JsonElement> GetResourceListAsync(
        string doctype,
        string company,                              // ← REQUIRED
        IDictionary<string, object>? extraFilters = null,
        int limit = 20,
        CancellationToken ct = default);

    Task<JsonElement> GetResourceAsync(
        string doctype,
        string name,
        string company,                              // ← REQUIRED (validated in client)
        CancellationToken ct = default);

    Task<JsonElement> PostAsync(
        string endpoint,
        object payload,
        string company,                              // ← REQUIRED — injected as "company" field
        CancellationToken ct = default);
}
```

Implementation: `GetResourceListAsync` adds `filters=[["company","=","<company>"]]` to the URL (merged with `extraFilters`). `GetResourceAsync` reads the result and validates `result.data.company == company` (throw on mismatch — defense in depth). `PostAsync` injects `payload.company = company`.

### Bridge endpoint — extracts company from claim, passes to client

```csharp
app.MapGet("/api/v1/erpnext/properties", async (
    IERPNextClient client, ClaimsPrincipal user, CancellationToken ct) =>
{
    var company = user.FindFirstValue("company")
        ?? throw new UnauthorizedAccessException("Missing company claim");
    var result = await client.GetResourceListAsync("Property", company, ct: ct);
    return Results.Ok(result);
});
```

### React company switcher

Add to Phase 2 Phase 1 (`apps/anchor-react/`):

- `src/stores/companyStore.ts` — Zustand store holding `activeCompany` (default from Bridge's `/api/v1/whoami` endpoint, which returns `{ user, role, defaultCompany, availableCompanies }`)
- `src/components/CompanySwitcher.tsx` — shadcn Select in app header. Lists `availableCompanies`. Changing it updates the Zustand store + triggers TanStack Query invalidation (all queries are scoped to `activeCompany`).
- All TanStack Query hooks (`useProperties`, `useLeases`, etc.) include `activeCompany` in their query key.

### Bridge `/api/v1/whoami` endpoint

```csharp
app.MapGet("/api/v1/whoami", (ClaimsPrincipal user) => Results.Ok(new {
    user = user.Identity!.Name,
    role = user.FindFirstValue("role"),
    defaultCompany = user.FindFirstValue("company"),
    availableCompanies = user.FindAll("available_company").Select(c => c.Value).ToList(),
}));
```

`available_company` claims sourced from Bridge user-admin config (list of ERPNext companies this user can switch into). Stub for Phase 2: hardcode CO's user to all 7 companies; proper user management in Phase 4.

---

## Override 4 — Authentication scheme (resolves audit G4)

**Pin explicitly:** Phase 2 uses **Bridge's existing default authentication scheme** — `MockOktaService` in dev, Okta in production. Do **not** introduce a new auth scheme.

References for sunfish-PM:
- `accelerators/bridge/MockOktaService/Program.cs` — dev OIDC issuer
- `accelerators/bridge/Sunfish.Bridge/Program.cs` — wired auth scheme (search for `AddAuthentication`)
- `accelerators/bridge/Sunfish.Bridge/appsettings.json` — OIDC config

Phase 2 Phase 1 PR additions to Program.cs auth pipeline:

```csharp
// Add 'role' + 'company' + 'available_company' claims to the cookie/JWT
options.Events.OnTokenValidated = async ctx =>
{
    var userId = ctx.Principal!.Identity!.Name;
    var profile = await userService.GetProfileAsync(userId);  // Bridge-side user store
    var identity = (ClaimsIdentity)ctx.Principal.Identity!;
    identity.AddClaim(new Claim("role", profile.Role));
    identity.AddClaim(new Claim("company", profile.DefaultCompany));
    foreach (var c in profile.AvailableCompanies)
        identity.AddClaim(new Claim("available_company", c));
};
```

`UserService` is a new Bridge-side primitive (Phase 2 Phase 1 deliverable): minimal user-profile store keyed by Okta `sub` claim, with `role`, `defaultCompany`, `availableCompanies`. SQLite-backed for dev; same Postgres as Bridge in prod.

**HALT condition:** if `UserService` design proves bigger than expected, escalate to XO — do **not** invent ad-hoc claim wiring inside `OnTokenValidated`.

---

## Build conventions (resolves audit G5–G11)

### .NET target (G5)

Phase 2's Bridge additions target **.NET 11 preview** (matching existing Bridge `.csproj`). Reference: `accelerators/bridge/Sunfish.Bridge/Sunfish.Bridge.csproj` `TargetFramework`.

### Dev-config seeding (G6)

Phase 2 Phase 1 PR adds `accelerators/bridge/Sunfish.Bridge/appsettings.Development.json.example`:

```json
{
  "ERPNext": {
    "BaseUrl": "http://erp.localhost:8080",
    "SiteName": "erp.localhost",
    "ApiKey": "REPLACE_ME",
    "ApiSecret": "REPLACE_ME"
  }
}
```

`appsettings.Development.json` is `.gitignore`d (already in repo's `.gitignore`). `CONTRIBUTING-REACT.md` instructs:
```
cp appsettings.Development.json.example appsettings.Development.json
# Then edit ERPNext.ApiKey + ERPNext.ApiSecret with your credentials from
# http://erp.localhost:8080/app/user → Profile → API Access → Generate Keys
```

### Phase 2 is online-only — explicit declaration (G7)

Phase 2 has **zero local-first / offline behavior**. The React app calls Bridge; Bridge calls ERPNext; if either is unreachable, the UI shows an error state and retries. Offline cache + write queue + CRDT are **Phase 3 deliverables**, not Phase 2.

Phase 2 `CONTRIBUTING-REACT.md` must include this declaration explicitly so reviewers and CO don't expect offline behavior.

### Error handling pattern (G8)

- **React top-level:** `<ErrorBoundary>` (TanStack Query + react-error-boundary) at the root of `<App />` showing a Sunfish-styled retry card with the error message + a "Retry" button (clears TanStack Query cache, refetches).
- **Per-screen:** `useQuery({ ...options, retry: 2, retryDelay: exponentialBackoff })` — retries twice on network errors before throwing to the boundary.
- **Bridge → ERPNext failures:** Bridge proxy returns `502 Bad Gateway` with the ERPNext error message in `error.message` (don't leak the underlying URL). React surfaces this as a user-visible error.
- **Network offline:** detect via `navigator.onLine` (and `online`/`offline` events). Show a top banner: "Offline — changes can't save yet." Phase 3 adds queue + retry; Phase 2 just blocks writes.

### CI workflow (G9)

Phase 2 Phase 1 PR adds `.github/workflows/anchor-react-ci.yml`:

```yaml
name: anchor-react CI
on:
  pull_request:
    paths:
      - 'apps/anchor-react/**'
      - '.github/workflows/anchor-react-ci.yml'
jobs:
  lint-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with: { node-version: '20', cache: 'npm', cache-dependency-path: apps/anchor-react/package-lock.json }
      - run: cd apps/anchor-react && npm ci
      - run: cd apps/anchor-react && npm run lint
      - run: cd apps/anchor-react && npm run typecheck
      - run: cd apps/anchor-react && npm test -- --run
      - run: cd apps/anchor-react && npm run build
```

No e2e tests in Phase 2 — defer to Phase 3 (Playwright via Tauri shell).

### Telemetry contract (G10)

- **React error boundary** posts to Bridge: `POST /api/v1/telemetry/error` with `{ message, stack, route, userAgent, timestamp }`.
- **Bridge** logs via existing Serilog pipeline. Endpoint can be a stub that just logs in Phase 2.
- **No third-party telemetry** (Sentry, Datadog) — local-first principle. Telemetry stays on CO's Bridge.

### Phase 3 prerequisites (G11)

Explicit Phase 5 PASS-gate addition: **after Phase 5 merges, both of these must be true** for Phase 3 to start:

1. `apps/anchor-react/` runs `npm run build` and produces a static bundle in `apps/anchor-react/dist/` with no console errors.
2. `packages/ui-react/` (`@sunfish/ui-react`) builds as a publishable ESM library (`npm run build` produces `dist/index.mjs` + `dist/index.d.ts`).

These properties let Phase 3's Tauri shell consume both as static assets.

---

## Summary of addendum impact on Phase 2 estimate

| Item | Original estimate | Adjustment | Reason |
|---|---|---|---|
| Phase 1 PR | 1 PR | unchanged in count but ~50% larger | Adds: `UserService`, `whoami` endpoint, `company` claim wiring, `appsettings.Development.json.example`, anchor-react-ci.yml |
| Pre-Phase-1 task | n/a | **+ ~2 hours** | CO creates 3 doctypes (`Property`, `Lease`, `Maintenance Ticket`) in ERPNext admin |
| Phase 2 PR | unchanged | adds `CompanySwitcher.tsx` | |
| Phases 3–5 | unchanged in shape | endpoints get `company` param | |

**Revised total Phase 2 estimate:** 9–14h COB time + 2h CO doctype prep (was 8–12h COB).

---

## Workstream flip update

Original hand-off line 346–348 still applies; no change to ledger flip mechanics.
